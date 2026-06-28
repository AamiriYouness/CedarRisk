using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires;
using CedarRisk.Domain.ReferentielTarifaires.ValueObjects;
using CedarRisk.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CedarRisk.Infrastructure.Persistence.Seed;

public static class ReferentielTarifaireSeed
{
    private static readonly DateOnly ValidFrom2026T1 = new(2026, 1, 1);

    public static async Task Seed2026T1Async(CedarRiskDbContext db)
    {
        var existe = await db.ReferentielTarifaires
            .AnyAsync(r => r.ValidFrom == ValidFrom2026T1);

        if (existe) return;

        var referentiel = Create2026T1();
        if (referentiel.IsFailure)
            throw new InvalidOperationException(
                $"Seed 2026-T1 invalide : {referentiel.Error.Message}");

        db.ReferentielTarifaires.Add(referentiel.Value);
        await db.SaveChangesAsync();
    }

    public static Domain.Common.Result<ReferentielTarifaire> Create2026T1()
    {
        // RC
        var tauxCatNatRC = TariffRate.Of(0.035m); // 3.5% — PrimeRC_HT → CatNatRC_HT
        var tauxTaxeRC = TariffRate.Of(0.14m);  // 14%  — sur PrimeRC_HT et CatNatRC_HT
        var tauxParafiscaleRC = TariffRate.Of(0.01m);  // 1%

        // GC — TauxCatNatGC est sur chaque GarantieTarification
        var tauxTaxeGC = TariffRate.Of(0.14m);  // 14%  — sur TotalPrimeHT_GC et TotalCatNatHT_GC
        var tauxParafiscaleGC = TariffRate.Of(0.01m);  // 1%

        var tarifRemorque = new TarifRemorque.Taux(TariffRate.Of(0.20m));
        var timbreCNPAC = new Timbre(10m);

        var bareme = new List<BaremeRC>
        {
            BaremeRC.Create(UsageVehicule.VehiculeTourisme,  1,  4,    new PrimeHT(500m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourisme,  5,  7,    new PrimeHT(700m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourisme,  8,  10,   new PrimeHT(950m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourisme,  11, 14,   new PrimeHT(1200m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourisme,  15, 20,   new PrimeHT(1600m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourisme,  21, null, new PrimeHT(2000m)),

            BaremeRC.Create(UsageVehicule.VehiculeTourismeEntreprise,  1,  4,    new PrimeHT(600m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourismeEntreprise,  5,  7,    new PrimeHT(850m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourismeEntreprise,  8,  10,   new PrimeHT(1100m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourismeEntreprise,  11, 14,   new PrimeHT(1400m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourismeEntreprise,  15, 20,   new PrimeHT(1800m)),
            BaremeRC.Create(UsageVehicule.VehiculeTourismeEntreprise,  21, null, new PrimeHT(2300m)),

            BaremeRC.Create(UsageVehicule.Taxi,  1,  4,    new PrimeHT(800m)),
            BaremeRC.Create(UsageVehicule.Taxi,  5,  7,    new PrimeHT(1100m)),
            BaremeRC.Create(UsageVehicule.Taxi,  8,  10,   new PrimeHT(1400m)),
            BaremeRC.Create(UsageVehicule.Taxi,  11, 14,   new PrimeHT(1800m)),
            BaremeRC.Create(UsageVehicule.Taxi,  15, 20,   new PrimeHT(2300m)),
            BaremeRC.Create(UsageVehicule.Taxi,  21, null, new PrimeHT(3000m)),

            BaremeRC.Create(UsageVehicule.TransportPublicVoyageurs,  1,  4,    new PrimeHT(900m)),
            BaremeRC.Create(UsageVehicule.TransportPublicVoyageurs,  5,  7,    new PrimeHT(1200m)),
            BaremeRC.Create(UsageVehicule.TransportPublicVoyageurs,  8,  10,   new PrimeHT(1600m)),
            BaremeRC.Create(UsageVehicule.TransportPublicVoyageurs,  11, 14,   new PrimeHT(2000m)),
            BaremeRC.Create(UsageVehicule.TransportPublicVoyageurs,  15, 20,   new PrimeHT(2600m)),
            BaremeRC.Create(UsageVehicule.TransportPublicVoyageurs,  21, null, new PrimeHT(3400m)),

            BaremeRC.Create(UsageVehicule.TransportMarchandises,  1,  4,    new PrimeHT(700m)),
            BaremeRC.Create(UsageVehicule.TransportMarchandises,  5,  7,    new PrimeHT(950m)),
            BaremeRC.Create(UsageVehicule.TransportMarchandises,  8,  10,   new PrimeHT(1250m)),
            BaremeRC.Create(UsageVehicule.TransportMarchandises,  11, 14,   new PrimeHT(1600m)),
            BaremeRC.Create(UsageVehicule.TransportMarchandises,  15, 20,   new PrimeHT(2100m)),
            BaremeRC.Create(UsageVehicule.TransportMarchandises,  21, null, new PrimeHT(2700m)),
        };

        return ReferentielTarifaire.Create(
            tauxCatNatRC, tauxTaxeRC, tauxParafiscaleRC,
            tauxTaxeGC, tauxParafiscaleGC,
            tarifRemorque, timbreCNPAC, bareme,
            validFrom: ValidFrom2026T1,
            validTo: null,
            now: DateTimeOffset.UtcNow);
    }
}
