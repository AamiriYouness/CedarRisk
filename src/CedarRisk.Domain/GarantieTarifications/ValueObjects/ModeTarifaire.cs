using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.Errors;
namespace CedarRisk.Domain.GarantieTarifications.ValueObjects;

/// <summary>
/// Contrat de calcul de prime HT pour une garantie complémentaire.
/// </summary>
public interface IModeTarifaire
{
    /// <summary>
    /// Calcule la PrimeHT brute — avant fiscalité.
    /// Le moteur applique ensuite CatNat, TaxeCA, Parafiscale via le type system.
    /// </summary>
    Result<PrimeHT> CalculerPrime(string garantieCode, BaseTarifaire contexte);

    string TypeDiscriminant { get; }
}

// =============================================================================
// Mode 1 — Taux appliqué directement sur ValeurVenale
// =============================================================================

/// <summary>
/// PrimeHT = ValeurVenale × Taux.
/// Aucune saisie capital — la base de calcul EST la valeur vénale.
/// </summary>
public sealed record TauxDirectValeurVenale(decimal Taux) : IModeTarifaire
{
    public string TypeDiscriminant => "TauxDirectValeurVenale";

    public Result<PrimeHT> CalculerPrime(string garantieCode, BaseTarifaire contexte)
    {
        var montant = Math.Round(contexte.ValeurVenale * Taux, 2, MidpointRounding.AwayFromZero);
        return Result<PrimeHT>.Success(new PrimeHT(montant));
    }

    public static Result<TauxDirectValeurVenale> Create(decimal taux)
    {
        if (taux <= 0)
            return Result<TauxDirectValeurVenale>.Failure(new TauxInvalideError(taux));

        return Result<TauxDirectValeurVenale>.Success(new TauxDirectValeurVenale(taux));
    }
}

// =============================================================================
// Mode 2 — Montant fixe
// =============================================================================

/// <summary>
/// PrimeHT = Montant.
/// Prime identique quelle que soit la situation du véhicule.
/// </summary>
public sealed record MontantFlat(decimal Montant) : IModeTarifaire
{
    public string TypeDiscriminant => "MontantFlat";

    public Result<PrimeHT> CalculerPrime(string garantieCode, BaseTarifaire contexte) =>
        Result<PrimeHT>.Success(new PrimeHT(Montant));

    public static Result<MontantFlat> Create(decimal montant)
    {
        if (montant <= 0)
            return Result<MontantFlat>.Failure(new TauxInvalideError(montant));

        return Result<MontantFlat>.Success(new MontantFlat(montant));
    }
}

// =============================================================================
// Mode 3 — Options fixes : le client choisit un palier (Capital, MontantHT)
// =============================================================================

/// <summary>
/// Le client choisit parmi des options prédéfinies (Capital → MontantHT).
/// PrimeHT = OptionChoisie.MontantHT — montant fixe par palier, pas de taux.
/// Contrainte domaine : OptionChoisie.Capital <= ValeurVenale.
/// </summary>
public sealed record CapitalOptionnel(
    IReadOnlyList<CapitalOption> Options,
    Franchise? Franchise = null) : IModeTarifaire
{
    public string TypeDiscriminant => "CapitalOptionnel";

    public Result<PrimeHT> CalculerPrime(string garantieCode, BaseTarifaire contexte)
    {
        if (contexte.OptionChoisie is null)
            return Result<PrimeHT>.Failure(new OptionCapitalIntrouvableError(0));

        var capitalDemande = contexte.OptionChoisie.Capital;
        var option = Options.FirstOrDefault(o => o.Capital == capitalDemande);

        if (option is null)
            return Result<PrimeHT>.Failure(new OptionCapitalIntrouvableError(capitalDemande));

        if (option.Capital > contexte.ValeurVenale)
            return Result<PrimeHT>.Failure(
                new OptionCapitalDepasseValeurVenaleError(option.Capital, contexte.ValeurVenale));

        return Result<PrimeHT>.Success(new PrimeHT(option.MontantHT));
    }

    public static Result<CapitalOptionnel> Create(
        IEnumerable<CapitalOption> options,
        Franchise? franchise = null)
    {
        var list = options?.OrderBy(o => o.Capital).ToList() ?? [];

        if (list.Count == 0)
            return Result<CapitalOptionnel>.Failure(new OptionsCapitalVidesError());

        if (list.Any(o => o.Capital <= 0 || o.MontantHT <= 0))
            return Result<CapitalOptionnel>.Failure(new TauxInvalideError(0));

        return Result<CapitalOptionnel>.Success(new CapitalOptionnel(list, franchise));
    }
}

// =============================================================================
// Mode 4 — Taux sur capital plafonné
// =============================================================================

/// <summary>
/// Le client déclare un capital soumis à RegleCapital.
/// PrimeHT = CapitalClient × Taux (sur le capital ÉCRIT, pas sur la limite).
///
/// RegleCapital valide que le capital respecte toutes les conditions (AND).
/// Invariant absolu : CapitalClient &lt;= ValeurVenale, toujours, évalué en premier.
///
/// Exemple : plafond = 50% ValeurVenale = 100 000 → plafond = 50 000
///   Client écrit 25 000 → valide → PrimeHT = 25 000 × Taux
///   Client écrit 60 000 → invalide (dépasse le plafond)
///   Client écrit 110 000 → invalide (dépasse ValeurVenale — vérifié avant RegleCapital)
/// </summary>
public sealed record TauxSurCapitalPlafonne(
    decimal Taux,
    RegleCapital Regle,
    Franchise? Franchise = null) : IModeTarifaire
{
    public string TypeDiscriminant => "TauxSurCapitalPlafonne";

    public Result<PrimeHT> CalculerPrime(string garantieCode, BaseTarifaire contexte)
    {
        var capitalClient = contexte.CapitalClient ?? 0m;

        // Invariant absolu — évalué avant tout
        if (capitalClient > contexte.ValeurVenale)
            return Result<PrimeHT>.Failure(
                new CapitalDepasseValeurVenaleError(capitalClient, contexte.ValeurVenale));

        if (Regle.TypePlafond == TypePlafond.CapitalGarantie)
        {
            if (contexte.CapitalGarantieReference is null)
                return Result<PrimeHT>.Failure(
                    new CapitalReferenceAbsentError(Regle.GarantieCodeRef ?? garantieCode));

            // Plafond = capital de la garantie de référence
            var plafondReference = contexte.CapitalGarantieReference.Value;
            var validation = Regle.ValiderCapital(capitalClient, contexte.ValeurVenale, plafondReference);
            if (validation.IsFailure)
                return Result<PrimeHT>.Failure(validation.Error);
        }
        else
        {
            // TypePlafond.ValeurVenale — plafond = ValeurVenale
            var validation = Regle.ValiderCapital(capitalClient, contexte.ValeurVenale, contexte.ValeurVenale);
            if (validation.IsFailure)
                return Result<PrimeHT>.Failure(validation.Error);
        }

        var montant = Math.Round(capitalClient * Taux, 2, MidpointRounding.AwayFromZero);
        return Result<PrimeHT>.Success(new PrimeHT(montant));
    }

    public static Result<TauxSurCapitalPlafonne> Create(
        decimal taux,
        RegleCapital regle,
        Franchise? franchise = null)
    {
        if (taux <= 0)
            return Result<TauxSurCapitalPlafonne>.Failure(new TauxInvalideError(taux));

        return Result<TauxSurCapitalPlafonne>.Success(new TauxSurCapitalPlafonne(taux, regle, franchise));
    }
}

// =============================================================================
// Mode 5 — Taux appliqué sur un capital fixe défini par la garantie
// =============================================================================

/// <summary>
/// PrimeHT = CapitalGarantie × Taux.
/// Le capital est fixé par la tarification — le client ne le saisit pas.
/// Exemple : Brise-Glace avec capital fixe 75 000 MAD × 0.8% = 600 MAD HT.
/// </summary>
public sealed record TauxDirectCapitalGarantie(decimal Taux, decimal CapitalGarantie) : IModeTarifaire
{
    public string TypeDiscriminant => "TauxDirectCapitalGarantie";

    public Result<PrimeHT> CalculerPrime(string garantieCode, BaseTarifaire contexte)
    {
        var montant = Math.Round(CapitalGarantie * Taux, 2, MidpointRounding.AwayFromZero);
        return Result<PrimeHT>.Success(new PrimeHT(montant));
    }

    public static Result<TauxDirectCapitalGarantie> Create(decimal taux, decimal capitalGarantie)
    {
        if (taux <= 0)
            return Result<TauxDirectCapitalGarantie>.Failure(new TauxInvalideError(taux));

        if (capitalGarantie <= 0)
            return Result<TauxDirectCapitalGarantie>.Failure(new TauxInvalideError(capitalGarantie));

        return Result<TauxDirectCapitalGarantie>.Success(
            new TauxDirectCapitalGarantie(taux, capitalGarantie));
    }
}
