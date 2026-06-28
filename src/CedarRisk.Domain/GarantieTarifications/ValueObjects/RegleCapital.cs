using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.Errors;

namespace CedarRisk.Domain.GarantieTarifications.ValueObjects;

public enum TypePlafond { ValeurVenale, CapitalGarantie }

public enum OperateurComparaison
{
    Inferieur,           // <
    InferieurOuEgal,     // <=
    Egal,                // ==
    SuperieurOuEgal,     // >=
    Superieur            // >
}

/// <summary>
/// Une condition individuelle : CapitalClient [op] Reference × Pourcentage/100
/// </summary>
public sealed record RegleCondition(OperateurComparaison Operateur, decimal Pourcentage)
{
    public decimal ResoudreValeurLimite(decimal referenceValue) =>
        Math.Round(referenceValue * (Pourcentage / 100m), 2, MidpointRounding.AwayFromZero);

    public bool Evaluer(decimal capitalClient, decimal referenceValue)
    {
        var limite = ResoudreValeurLimite(referenceValue);
        return Operateur switch
        {
            OperateurComparaison.Inferieur => capitalClient < limite,
            OperateurComparaison.InferieurOuEgal => capitalClient <= limite,
            OperateurComparaison.Egal => capitalClient == limite,
            OperateurComparaison.SuperieurOuEgal => capitalClient >= limite,
            OperateurComparaison.Superieur => capitalClient > limite,
            _ => false
        };
    }

    public string Decrire(decimal referenceValue)
    {
        var limite = ResoudreValeurLimite(referenceValue);
        var op = Operateur switch
        {
            OperateurComparaison.Inferieur => "<",
            OperateurComparaison.InferieurOuEgal => "<=",
            OperateurComparaison.Egal => "==",
            OperateurComparaison.SuperieurOuEgal => ">=",
            OperateurComparaison.Superieur => ">",
            _ => "?"
        };
        return $"{op} {limite:N2} MAD ({Pourcentage}% de {referenceValue:N2})";
    }
}

/// <summary>
/// Règle de validation du capital déclaré par le client.
/// Toutes les conditions s'appliquent en AND sur la même référence.
///
/// Invariant domaine absolu (assurance auto marocaine) :
///   CapitalClient <= ValeurVenale — toujours, inconditionnellement.
///
/// Flow de validation :
///   1. CapitalClient <= ValeurVenale          (hard stop domaine)
///   2. referenceValue = résoudre(TypePlafond)
///   3. Toutes les conditions évaluées en AND
///   4. PrimeHT = CapitalClient × Taux         (sur le capital ÉCRIT, pas la limite)
/// </summary>
public sealed record RegleCapital
{
    public TypePlafond TypePlafond { get; }
    public string? GarantieCodeRef { get; }
    public IReadOnlyList<RegleCondition> Conditions { get; }

    private RegleCapital(
        TypePlafond typePlafond,
        string? garantieCodeRef,
        IReadOnlyList<RegleCondition> conditions)
    {
        TypePlafond = typePlafond;
        GarantieCodeRef = garantieCodeRef;
        Conditions = conditions;
    }

    public static Result<RegleCapital> Create(
        TypePlafond typePlafond,
        IEnumerable<RegleCondition> conditions,
        string? garantieCodeRef = null)
    {
        var condList = conditions?.ToList() ?? [];

        if (!condList.Any())
            return Result<RegleCapital>.Failure(new RegleCapitalConditionsVidesError());

        if (typePlafond == TypePlafond.CapitalGarantie
            && string.IsNullOrWhiteSpace(garantieCodeRef))
            return Result<RegleCapital>.Failure(new RegleCapitalGarantieRefRequiseError());

        var invalide = condList.FirstOrDefault(c => c.Pourcentage <= 0 || c.Pourcentage > 100);
        if (invalide is not null)
            return Result<RegleCapital>.Failure(new RegleCapitalPourcentageInvalideError(invalide.Pourcentage));

        return Result<RegleCapital>.Success(new(
            typePlafond,
            garantieCodeRef?.Trim().ToUpperInvariant(),
            condList));
    }

    /// <summary>
    /// Shortcut — single condition, ValeurVenale reference.
    /// Most common case: <= 100% ValeurVenale, <= 50% ValeurVenale, etc.
    /// </summary>
    public static Result<RegleCapital> SurValeurVenale(
        OperateurComparaison op, decimal pourcentage) =>
        Create(TypePlafond.ValeurVenale,
            [new RegleCondition(op, pourcentage)]);

    /// <summary>EF Core hydration.</summary>
    public static RegleCapital Hydrate(
        TypePlafond typePlafond,
        string? garantieCodeRef,
        IReadOnlyList<RegleCondition> conditions) =>
        new(typePlafond, garantieCodeRef, conditions);

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Valide le capital déclaré par le client.
    /// referenceValue = capital résolu par le moteur (ValeurVenale ou capital de la garantie ref).
    /// </summary>
    public Result<Unit> ValiderCapital(decimal capitalClient, decimal valeurVenale, decimal referenceValue)
    {
        // Invariant absolu domaine assurance
        if (capitalClient > valeurVenale)
            return Result<Unit>.Failure(
                new CapitalDepasseValeurVenaleError(capitalClient, valeurVenale));

        var conditionsEchouees = Conditions
            .Where(c => !c.Evaluer(capitalClient, referenceValue))
            .ToList();

        if (!conditionsEchouees.Any())
            return Result<Unit>.Success(Unit.Value);

        var description = string.Join(" ET ",
            conditionsEchouees.Select(c => c.Decrire(referenceValue)));

        return Result<Unit>.Failure(new CapitalHorsRegleError(capitalClient, description));
    }

    public bool EstValide(decimal capitalClient, decimal valeurVenale, decimal referenceValue) =>
        capitalClient <= valeurVenale
        && Conditions.All(c => c.Evaluer(capitalClient, referenceValue));
}