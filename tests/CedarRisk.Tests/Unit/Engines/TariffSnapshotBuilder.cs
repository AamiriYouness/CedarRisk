using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires;
using CedarRisk.Domain.ReferentielTarifaires.ValueObjects;
using CedarRisk.Domain.ValueObjects;

namespace CedarRisk.Tests.Unit.Engines;

/// <summary>
/// Builder de ReferentielTarifaire pour les tests.
/// Taux par défaut conformes au seed 2026-T1 et Article 284 CGI Maroc.
/// </summary>
public sealed class TariffSnapshotBuilder
{
    private decimal _tauxCatNatRC = 0.035m; // 3.5%
    private decimal _tauxTaxeRC = 0.14m;  // 14%
    private decimal _tauxParafiscaleRC = 0.01m;  // 1%
    private decimal _tauxTaxeGC = 0.14m;  // 14%
    private decimal _tauxParafiscaleGC = 0.01m;  // 1%
    private decimal _tauxRemorque = 0.20m;  // 20%
    private decimal _timbreCNPAC = 10m;
    private DateOnly _validFrom = new(2026, 1, 1);

    private readonly List<(UsageVehicule Usage, int Min, int? Max, decimal PrimeHT)> _bareme = new()
    {
        (UsageVehicule.VehiculeTourisme,  1,  4,    500m),
        (UsageVehicule.VehiculeTourisme,  5,  7,    700m),
        (UsageVehicule.VehiculeTourisme,  8,  10,   950m),
        (UsageVehicule.VehiculeTourisme,  11, 14,  1200m),
        (UsageVehicule.VehiculeTourisme,  15, 20,  1600m),
        (UsageVehicule.VehiculeTourisme,  21, null, 2000m),

        (UsageVehicule.TransportMarchandises, 1,  4,    700m),
        (UsageVehicule.TransportMarchandises, 5,  7,    950m),
        (UsageVehicule.TransportMarchandises, 8,  10,  1250m),
        (UsageVehicule.TransportMarchandises, 11, 14,  1600m),
        (UsageVehicule.TransportMarchandises, 15, 20,  2100m),
        (UsageVehicule.TransportMarchandises, 21, null, 2700m),

        (UsageVehicule.Taxi, 1,  4,    800m),
        (UsageVehicule.Taxi, 5,  7,   1100m),
        (UsageVehicule.Taxi, 8,  10,  1400m),
        (UsageVehicule.Taxi, 11, 14,  1800m),
        (UsageVehicule.Taxi, 15, 20,  2300m),
        (UsageVehicule.Taxi, 21, null, 3000m),
    };

    public TariffSnapshotBuilder AvecTauxCatNatRC(decimal taux) { _tauxCatNatRC = taux; return this; }
    public TariffSnapshotBuilder AvecTauxTaxeRC(decimal taux) { _tauxTaxeRC = taux; return this; }
    public TariffSnapshotBuilder AvecTauxParafiscaleRC(decimal taux) { _tauxParafiscaleRC = taux; return this; }
    public TariffSnapshotBuilder AvecTauxTaxeGC(decimal taux) { _tauxTaxeGC = taux; return this; }
    public TariffSnapshotBuilder AvecTauxRemorque(decimal taux) { _tauxRemorque = taux; return this; }
    public TariffSnapshotBuilder AvecTimbreCNPAC(decimal montant) { _timbreCNPAC = montant; return this; }
    public TariffSnapshotBuilder AvecValidFrom(DateOnly date) { _validFrom = date; return this; }

    public ReferentielTarifaire Build()
    {
        var bareme = _bareme.Select(b =>
            BaremeRC.Create(b.Usage, b.Min, b.Max, new PrimeHT(b.PrimeHT)).Value);

        var result = ReferentielTarifaire.Create(
            TariffRate.Of(_tauxCatNatRC),
            TariffRate.Of(_tauxTaxeRC),
            TariffRate.Of(_tauxParafiscaleRC),
            TariffRate.Of(_tauxTaxeGC),
            TariffRate.Of(_tauxParafiscaleGC),
            new TarifRemorque.Taux(TariffRate.Of(_tauxRemorque)),
            new Timbre(_timbreCNPAC),
            bareme,
            _validFrom,
            validTo: null,
            now: DateTimeOffset.UtcNow);

        if (result.IsFailure)
            throw new InvalidOperationException(
                $"TariffSnapshotBuilder invalide : {result.Error.Message}");

        return result.Value;
    }

    public static ReferentielTarifaire Standard() => new TariffSnapshotBuilder().Build();
}