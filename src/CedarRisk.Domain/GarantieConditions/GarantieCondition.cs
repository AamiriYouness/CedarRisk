using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieConditions.Errors;
using CedarRisk.Domain.GarantieConditions.ValueObjects;

namespace CedarRisk.Domain.GarantieConditions;

/// <summary>
/// Conditions d'éligibilité d'une garantie complémentaire.
///
/// Cycle de vie : une seule condition active par GarantieCode à tout moment.
/// La mise à jour crée un nouvel enregistrement, désactive l'ancien — historique complet conservé.
///
/// Sémantiques d'exigence :
///   ExigenceConjonctive (GarantiesRequises)  → TOUTES doivent être sélectionnées (AND)
///   ExigenceDisjonctive (GarantiesAuMoinsUne) → AU MOINS UNE doit être sélectionnée (OR)
///   IncompatibilitesGarantie                 → AUCUNE ne peut coexister (NONE) — bidirectionnel via EligibilityEngine
/// </summary>
public sealed class GarantieCondition
{
    private readonly List<UsageVehicule> _usagesExclus = [];

    // EF Core constructor
    private GarantieCondition() { }

    private GarantieCondition(
        Guid id,
        string garantieCode,
        int? ageLimiteVehicule,
        IEnumerable<UsageVehicule> usagesExclus,
        IncompatibilitesGarantie incompatibilites,
        ExigenceConjonctive exigenceConjonctive,
        ExigenceDisjonctive exigenceDisjonctive,
        DateTimeOffset now)
    {
        Id = id;
        GarantieCode = garantieCode;
        AgeLimiteVehicule = ageLimiteVehicule;
        Incompatibilites = incompatibilites;
        ExigenceConjonctive = exigenceConjonctive;
        ExigenceDisjonctive = exigenceDisjonctive;
        IsActive = true;
        CreatedAt = now;

        _usagesExclus.AddRange(usagesExclus);
    }

    public Guid Id { get; private set; }
    public string GarantieCode { get; private set; } = default!;
    public int? AgeLimiteVehicule { get; private set; }
    public IReadOnlyList<UsageVehicule> UsagesExclus => _usagesExclus.AsReadOnly();

    /// <summary>TOUTES ces garanties doivent être sélectionnées — AND.</summary>
    public ExigenceConjonctive ExigenceConjonctive { get; private set; } = ExigenceConjonctive.Vide;

    /// <summary>AU MOINS UNE de ces garanties doit être sélectionnée — OR.</summary>
    public ExigenceDisjonctive ExigenceDisjonctive { get; private set; } = ExigenceDisjonctive.Vide;

    /// <summary>Aucune de ces garanties ne peut coexister. Appliquée bidirectionnellement par EligibilityEngine.</summary>
    public IncompatibilitesGarantie Incompatibilites { get; private set; } = IncompatibilitesGarantie.Vide;

    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }


    public static Result<GarantieCondition> Create(
        string garantieCode,
        int? ageLimiteVehicule,
        IEnumerable<UsageVehicule> usagesExclus,
        IEnumerable<string> garantiesIncompatibles,
        IEnumerable<string> garantiesRequises,
        IEnumerable<string> garantiesAuMoinsUne,
        DateTimeOffset now)
    {
        var usagesList = usagesExclus?.ToList() ?? [];
        var code = garantieCode.Trim().ToUpperInvariant();

        var incompatibilitesResult = IncompatibilitesGarantie.Create(garantiesIncompatibles, code);
        if (incompatibilitesResult.IsFailure)
            return Result<GarantieCondition>.Failure(incompatibilitesResult.Error);

        var conjonctiveResult = ExigenceConjonctive.Create(garantiesRequises);
        if (conjonctiveResult.IsFailure)
            return Result<GarantieCondition>.Failure(conjonctiveResult.Error);

        var disjonctiveResult = ExigenceDisjonctive.Create(garantiesAuMoinsUne);
        if (disjonctiveResult.IsFailure)
            return Result<GarantieCondition>.Failure(disjonctiveResult.Error);

        var incompatibilites = incompatibilitesResult.Value;
        var conjonctive = conjonctiveResult.Value;
        var disjonctive = disjonctiveResult.Value;

        // Cross-constraint: a code cannot be both required (AND/OR) and incompatible
        var conflict = conjonctive.Codes
            .Concat(disjonctive.Codes)
            .FirstOrDefault(incompatibilites.Contains);

        if (conflict is not null)
            return Result<GarantieCondition>.Failure(new GarantieRequiseEtIncompatibleError(conflict));

        var condition = new GarantieCondition(
            Guid.NewGuid(),
            code,
            ageLimiteVehicule,
            usagesList,
            incompatibilites,
            conjonctive,
            disjonctive,
            now);

        return Result<GarantieCondition>.Success(condition);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = now;
    }

    public Result<Unit> DeactivateStrict(DateTimeOffset now)
    {
        if (!IsActive)
            return Result<Unit>.Failure(new ConditionDejaInactiveError(Id));

        Deactivate(now);
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Vérifie si toutes les contraintes d'exigence sont satisfaites par la sélection fournie.
    /// N'inclut PAS les incompatibilités — EligibilityEngine les applique bidirectionnellement.
    /// </summary>
    public bool ExigencesSatisfaitesPar(IEnumerable<string> codesSelectionnes)
    {
        var selection = codesSelectionnes.ToList();
        return ExigenceConjonctive.EstSatisfaitePar(selection)
            && ExigenceDisjonctive.EstSatisfaitePar(selection);
    }
}
