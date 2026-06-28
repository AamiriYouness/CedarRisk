
using CedarRisk.Domain.GarantieConditions;
using CedarRisk.Domain.GarantieTarifications;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using CedarRisk.Domain.ValueObjects;
using CedarRisk.Domain.Common;
using CedarRisk.Domain.Garanties;
using Microsoft.EntityFrameworkCore;

namespace CedarRisk.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed idempotent — vérifie l'existence par GarantieCode avant d'insérer.
///
/// Garanties :
///   VOL  — TauxDirectValeurVenale(3%)    CatNat ✅  Age ≤ 10
///   DOM  — TauxDirectValeurVenale(2.5%)  CatNat ✅  Age ≤ 8
///   BRIS — MontantFlat(400)              CatNat ❌  AND:[VOL,DOM]
///   DF   — MontantFlat(250)              CatNat ❌  Incompatible:[PJ]
///   PJ   — CapitalOptionnel(3 paliers)   CatNat ❌  Incompatible:[DF]
///   RC_CONDUCTEUR — TauxSurCapitalPlafonne(2%, ≤100% VV)  CatNat ✅  Exclus:[Taxi,TPV]
///   PT   — TauxSurCapitalPlafonne(1.5%, 10%-50% VV)       CatNat ✅  OR:[VOL,DOM] + Exclus:[Taxi]
/// </summary>
public static class GarantiesSeed
{
    private static readonly DateOnly ValidFrom2026T1 = new(2026, 1, 1);

    // ── Codes ──────────────────────────────────────────────────────────────────
    private const string VOL = "VOL";
    private const string DOM = "DOM";
    private const string BRIS = "BRIS";
    private const string DC = "DC";
    private const string DF = "DF";
    private const string PJ = "PJ";
    private const string RC_CONDUCTEUR = "RC_CONDUCTEUR";
    private const string PT = "PT";

    public static async Task SeedAsync(CedarRiskDbContext db)
    {
        var codesExistants = await db.Garanties
            .Select(g => g.Code)
            .ToHashSetAsync();

        if (codesExistants.Count >= 7) return; // idempotent fast path

        var now = DateTimeOffset.UtcNow;

        SeedDefinitions(db, codesExistants, now);
        SeedConditions(db, codesExistants, now);
        SeedTarifications(db, codesExistants, now);

        await db.SaveChangesAsync();
    }

    private static void SeedDefinitions(
        CedarRiskDbContext db,
        HashSet<string> codesExistants,
        DateTimeOffset now)
    {
        var definitions = new[]
        {
            (VOL,           "Vol & Incendie",              "Indemnise le vol total/partiel et l'incendie du véhicule."),
            (DOM,           "Dommages Collision",          "Couvre les dommages matériels suite à une collision."),
            (DC,            "Dommages Collision Capital",  "Dommages collision sur capital fixe garanti."),
            (BRIS,          "Brise-Glace",                 "Prise en charge du remplacement des vitrages."),
            (DF,            "Défense & Recours",           "Assistance juridique et recours contre tiers."),
            (PJ,            "Protection Juridique",        "Couverture étendue des frais de procédure judiciaire."),
            (RC_CONDUCTEUR, "RC Conducteur",               "Extension RC couvrant le conducteur responsable."),
            (PT,            "Personnes Transportées",      "Indemnisation des passagers en cas d'accident."),
        };

        foreach (var (code, libelle, description) in definitions)
        {
            if (codesExistants.Contains(code)) continue;

            var result = GarantieDefinition.Create(code, libelle, description, now);
            if (result.IsFailure)
                throw new InvalidOperationException(
                    $"Seed GarantieDefinition '{code}' invalide : {result.Error.Message}");

            db.Garanties.Add(result.Value);
        }
    }

    // ── Conditions ─────────────────────────────────────────────────────────────

    private static void SeedConditions(
        CedarRiskDbContext db,
        HashSet<string> codesExistants,
        DateTimeOffset now)
    {
        var specs = new (string Code, Func<Result<GarantieCondition>> Factory)[]
        {
            // VOL — Age ≤ 10, aucune exigence, aucune incompatibilité
            (VOL, () => GarantieCondition.Create(
                garantieCode:           VOL,
                ageLimiteVehicule:      10,
                usagesExclus:           [],
                garantiesIncompatibles: [],
                garantiesRequises:      [],
                garantiesAuMoinsUne:    [],
                now:                    now)),

            // DOM — Age ≤ 8
            (DOM, () => GarantieCondition.Create(
                garantieCode:           DOM,
                ageLimiteVehicule:      8,
                usagesExclus:           [],
                garantiesIncompatibles: [],
                garantiesRequises:      [],
                garantiesAuMoinsUne:    [],
                now:                    now)),

            (DC,   () => GarantieCondition.Create(
                garantieCode:           DC,
                ageLimiteVehicule:      10,
                usagesExclus:           [],
                garantiesIncompatibles: [],
                garantiesRequises:      [],
                garantiesAuMoinsUne:    [],
                now:                    now)),

            // BRIS — AND:[VOL, DOM]
            (BRIS, () => GarantieCondition.Create(
                garantieCode:           BRIS,
                ageLimiteVehicule:      null,
                usagesExclus:           [],
                garantiesIncompatibles: [],
                garantiesRequises:      [VOL, DOM],
                garantiesAuMoinsUne:    [],
                now:                    now)),

            // DF — Incompatible:[PJ]
            (DF, () => GarantieCondition.Create(
                garantieCode:           DF,
                ageLimiteVehicule:      null,
                usagesExclus:           [],
                garantiesIncompatibles: [PJ],
                garantiesRequises:      [],
                garantiesAuMoinsUne:    [],
                now:                    now)),

            // PJ — Incompatible:[DF]
            (PJ, () => GarantieCondition.Create(
                garantieCode:           PJ,
                ageLimiteVehicule:      null,
                usagesExclus:           [],
                garantiesIncompatibles: [DF],
                garantiesRequises:      [],
                garantiesAuMoinsUne:    [],
                now:                    now)),

            // RC_CONDUCTEUR — Exclus:[Taxi, TransportPublicVoyageurs]
            (RC_CONDUCTEUR, () => GarantieCondition.Create(
                garantieCode:           RC_CONDUCTEUR,
                ageLimiteVehicule:      null,
                usagesExclus:           [UsageVehicule.Taxi, UsageVehicule.TransportPublicVoyageurs],
                garantiesIncompatibles: [],
                garantiesRequises:      [],
                garantiesAuMoinsUne:    [],
                now:                    now)),

            // PT — OR:[VOL, DOM] + Exclus:[Taxi]
            (PT, () => GarantieCondition.Create(
                garantieCode:           PT,
                ageLimiteVehicule:      null,
                usagesExclus:           [UsageVehicule.Taxi],
                garantiesIncompatibles: [],
                garantiesRequises:      [],
                garantiesAuMoinsUne:    [VOL, DOM],
                now:                    now)),
        };

        foreach (var (code, factory) in specs)
        {
            if (codesExistants.Contains(code)) continue;

            var result = factory();
            if (result.IsFailure)
                throw new InvalidOperationException(
                    $"Seed GarantieCondition '{code}' invalide : {result.Error.Message}");

            db.GarantieConditions.Add(result.Value);
        }
    }

    private static void SeedTarifications(
        CedarRiskDbContext db,
        HashSet<string> codesExistants,
        DateTimeOffset now)
    {
        var tauxCatNatGC = TariffRate.Of(0.012m); // 1.2% — même taux que RC pour 2026-T1

        var specs = new (string Code, Func<Result<GarantieTarification>> Factory)[]
        {
            // VOL — TauxDirectValeurVenale(3%), CatNat ✅
            (VOL, () =>
            {
                var mode = TauxDirectValeurVenale.Create(0.03m);
                if (mode.IsFailure) return Result<GarantieTarification>.Failure(mode.Error);
                return GarantieTarification.Create(VOL, mode.Value, true, tauxCatNatGC, ValidFrom2026T1, now);
            }),

            // DOM — TauxDirectValeurVenale(2.5%), CatNat ✅
            (DOM, () =>
            {
                var mode = TauxDirectValeurVenale.Create(0.025m);
                if (mode.IsFailure) return Result<GarantieTarification>.Failure(mode.Error);
                return GarantieTarification.Create(DOM, mode.Value, true, tauxCatNatGC, ValidFrom2026T1, now);
            }),

            (DC,  () => 
            { 
                var mode = TauxDirectCapitalGarantie.Create(0.02m, 75_000m);
                if (mode.IsFailure) return Result<GarantieTarification>.Failure(mode.Error); 
                return GarantieTarification.Create(DC, mode.Value, true, tauxCatNatGC, ValidFrom2026T1, now);
            }),

            // BRIS — MontantFlat(400), CatNat ❌
            (BRIS, () =>
            {
                var mode = MontantFlat.Create(400m);
                if (mode.IsFailure) return Result<GarantieTarification>.Failure(mode.Error);
                return GarantieTarification.Create(BRIS, mode.Value, false, TariffRate.Zero, ValidFrom2026T1, now);
            }),

            // DF — MontantFlat(250), CatNat ❌
            (DF, () =>
            {
                var mode = MontantFlat.Create(250m);
                if (mode.IsFailure) return Result<GarantieTarification>.Failure(mode.Error);
                return GarantieTarification.Create(DF, mode.Value, false, TariffRate.Zero, ValidFrom2026T1, now);
            }),

            // PJ — CapitalOptionnel([50k→800, 100k→1400, 150k→1900]), CatNat ❌
            (PJ, () =>
            {
                var mode = CapitalOptionnel.Create([
                    new CapitalOption(50_000m,  800m),
                    new CapitalOption(100_000m, 1_400m),
                    new CapitalOption(150_000m, 1_900m),
                ]);
                if (mode.IsFailure) return Result<GarantieTarification>.Failure(mode.Error);
                return GarantieTarification.Create(PJ, mode.Value, false, TariffRate.Zero, ValidFrom2026T1, now);
            }),

            // RC_CONDUCTEUR — TauxSurCapitalPlafonne(2%, ≤100% VV), CatNat ✅
            (RC_CONDUCTEUR, () =>
            {
                var regle = RegleCapital.SurValeurVenale(OperateurComparaison.InferieurOuEgal, 100m);
                if (regle.IsFailure) return Result<GarantieTarification>.Failure(regle.Error);

                var mode = TauxSurCapitalPlafonne.Create(0.02m, regle.Value);
                if (mode.IsFailure) return Result<GarantieTarification>.Failure(mode.Error);

                return GarantieTarification.Create(RC_CONDUCTEUR, mode.Value, true, tauxCatNatGC, ValidFrom2026T1, now);
            }),

            // PT — TauxSurCapitalPlafonne(1.5%, 10%-50% VV), CatNat ✅
            (PT, () =>
            {
                var regle = RegleCapital.Create(
                    TypePlafond.ValeurVenale,
                    [
                        new RegleCondition(OperateurComparaison.SuperieurOuEgal, 10m),  // >= 10% VV
                        new RegleCondition(OperateurComparaison.InferieurOuEgal, 50m),  // <= 50% VV
                    ]);
                if (regle.IsFailure) return Result<GarantieTarification>.Failure(regle.Error);

                var mode = TauxSurCapitalPlafonne.Create(0.015m, regle.Value);
                if (mode.IsFailure) return Result<GarantieTarification>.Failure(mode.Error);

                return GarantieTarification.Create(PT, mode.Value, true, tauxCatNatGC, ValidFrom2026T1, now);
            }),
        };

        foreach (var (code, factory) in specs)
        {
            if (codesExistants.Contains(code)) continue;

            var result = factory();
            if (result.IsFailure)
                throw new InvalidOperationException(
                    $"Seed GarantieTarification '{code}' invalide : {result.Error.Message}");

            db.GarantieTarifications.Add(result.Value);
        }
    }
}
