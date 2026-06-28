using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.GarantieConditions;
using CedarRisk.Domain.Garanties;
using CedarRisk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CedarRisk.Application.Engines.Implementation;

public class EligibilityEngine(CedarRiskDbContext db) : IEligibilityEngine
{
    private readonly CedarRiskDbContext _db = db;

    public async Task<EligibilityResult> EvaluerAsync(EligibilityContext contexte, CancellationToken ct = default)
    {
        var codesRequis = contexte.CodesGaranties.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var definitions = await _db.Garanties
            .AsNoTracking()
            .Where(d => codesRequis.Contains(d.Code))
            .ToListAsync(ct);

        var toutesConditionsActives = await _db.GarantieConditions
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        var carteIncompatibilites = BuildCarteIncompatibilitesBidirectionnelle(toutesConditionsActives);

        var conditionsParCode = toutesConditionsActives
            .GroupBy(c => c.GarantieCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var definitionsParCode = definitions
            .ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);

        var eligibles = new List<GarantieEligible>();
        var ineligibles = new List<GarantieIneligible>();

        foreach (var code in contexte.CodesGaranties)
        {
            var raison = EvaluerEligibilite(
                code,
                contexte,
                codesRequis,
                definitionsParCode,
                conditionsParCode,
                carteIncompatibilites);

            if (raison is null)
                eligibles.Add(new GarantieEligible(code));
            else
                ineligibles.Add(new GarantieIneligible(code, raison));
        }

        return new EligibilityResult(eligibles, ineligibles);
    }

    private static string? EvaluerEligibilite(
        string code,
        EligibilityContext contexte,
        HashSet<string> tousCodesRequis,
        Dictionary<string, GarantieDefinition> definitionsParCode,
        Dictionary<string, List<GarantieCondition>> conditionsParCode,
        Dictionary<string, HashSet<string>> carteIncompatibilitesBidirectionnelles)
    {
        // Règle 1 — définition active
        if (!definitionsParCode.TryGetValue(code, out var definition))
            return $"Garantie '{code}' introuvable.";

        if (!definition.IsActive)
            return $"Garantie '{code}' est désactivée.";

        // Aucune condition active → éligible inconditionnellement
        if (!conditionsParCode.TryGetValue(code, out var conditions) || conditions.Count == 0)
            return EvaluerIncompatibilitesBidirectionnelles(code, tousCodesRequis, carteIncompatibilitesBidirectionnelles);

        foreach (var condition in conditions)
        {
            // Règle 2 — âge véhicule
            if (condition.AgeLimiteVehicule.HasValue && contexte.AgeVehicule > condition.AgeLimiteVehicule.Value)
                return $"Âge véhicule {contexte.AgeVehicule} ans dépasse la limite autorisée de {condition.AgeLimiteVehicule.Value} ans.";

            // Règle 3 — usage exclu
            if (condition.UsagesExclus is { Count: > 0 } && condition.UsagesExclus.Contains(contexte.Usage))
                return $"Usage '{contexte.Usage}' est exclu pour cette garantie.";

            // Règle 4 — exigence conjonctive (AND)
            if (condition.ExigenceConjonctive is not null && !condition.ExigenceConjonctive.EstSatisfaitePar(tousCodesRequis))
            {
                var manquants = condition.ExigenceConjonctive.CodesManquants(tousCodesRequis);
                return $"Garanties requises (AND) manquantes : {string.Join(", ", manquants)}.";
            }

            // Règle 5 — exigence disjonctive (OR)
            if (condition.ExigenceDisjonctive is not null && condition.ExigenceDisjonctive.AucunPresent(tousCodesRequis))
                return $"Au moins une garantie parmi {string.Join(", ", condition.ExigenceDisjonctive.Codes)} est requise.";

            // Règle 6 — incompatibilités déclarées sur cette condition (NONE)
            if (condition.Incompatibilites is not null)
            {
                var conflits = condition.Incompatibilites.CodesEnConflit(tousCodesRequis);
                if (conflits.Count > 0)
                    return $"Incompatible avec : {string.Join(", ", conflits)}.";
            }
        }

        // Règle 6 bis — incompatibilités bidirectionnelles
        return EvaluerIncompatibilitesBidirectionnelles(code, tousCodesRequis, carteIncompatibilitesBidirectionnelles);
    }

    private static string? EvaluerIncompatibilitesBidirectionnelles(
        string code,
        HashSet<string> tousCodesRequis,
        Dictionary<string, HashSet<string>> carte)
    {
        if (!carte.TryGetValue(code, out var declarantsIncompatibles))
            return null;

        // tousCodesRequis contient les codes qui ont déclaré `code` incompatible
        var conflits = declarantsIncompatibles
            .Where(tousCodesRequis.Contains)
            .ToList();

        return conflits.Count > 0
            ? $"Incompatible (bidirectionnel) avec : {string.Join(", ", conflits)}."
            : null;
    }

    // Carte: pour chaque code C, ensemble des codes qui ont déclaré C incompatible avec eux
    // Si A déclare [B, C] incompatibles → carte[B] ∋ A, carte[C] ∋ A
    private static Dictionary<string, HashSet<string>> BuildCarteIncompatibilitesBidirectionnelle(
        IEnumerable<GarantieCondition> toutesConditions)
    {
        var carte = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var condition in toutesConditions)
        {
            if (condition.Incompatibilites is null) continue;

            foreach (var codeIncompatible in condition.Incompatibilites.Codes)
            {
                if (!carte.TryGetValue(codeIncompatible, out var declarants))
                {
                    declarants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    carte[codeIncompatible] = declarants;
                }
                declarants.Add(condition.GarantieCode);
            }
        }

        return carte;
    }
}
