using CedarRisk.Application.Engines.Implementation;
using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Shouldly;
using Xunit;

namespace CedarRisk.Tests.Unit.Engines;

public sealed class PremiumAggregatorTests
{
    private static readonly PremiumAggregator Aggregator = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RcPremiumResult RcNominal() => new(
        PrimeRC: new PrimeHT(950m),
        CatNatRC: new CatNatHT(33.25m),   // 950 × 3.5%
        TaxeRC: new TaxeCA(133.00m),     // 950 × 14%
        CatNatTaxeRC: new TaxeCA(4.66m),       // 33.25 × 14%
        ParafiscaleRC: new Parafiscale(9.83m),  // (950 + 33.25) × 1%
        TimbreCNPAC: new Timbre(10m));

    private static PrimesGarantiesHT GcVide() => PrimesGarantiesHT.Vide;

    private static PrimesGarantiesHT GcAvecVOL() =>
        PrimesGarantiesHT.Create([
            new PrimeGarantieHT("VOL", new PrimeHT(6000m), new CatNatHT(72m))
        ]);

    // ── RC seul ───────────────────────────────────────────────────────────────

    [Fact]
    public void Agreger_RcSeul_SansGC_SansRemorque_PrimeTotalCeiling()
    {
        // PrimeTTCRC = 950 + 133 + 33.25 + 4.66 + 9.83 + 10 = 1140.74
        // Math.Ceiling(1140.74) = 1141
        var referentiel = TariffSnapshotBuilder.Standard();

        var breakdown = Aggregator.Agreger(RcNominal(), GcVide(), null, referentiel);

        breakdown.PrimeTTCRC.Montant.ShouldBe(1140.74m);
        breakdown.PrimeTTCGC.Montant.ShouldBe(0m);
        breakdown.PrimeTTCRemorque.Montant.ShouldBe(0m);
        breakdown.PrimeTotalTTC.Montant.ShouldBe(1141m);
    }

    [Fact]
    public void Agreger_AvecGC_VOL_ParafiscaleGCCalculeeUneFois()
    {
        // VOL: PrimeHT = 6000, CatNatHT = 72
        // ParafiscaleGC = (6000 + 72) × 1% = 60.72
        // TotalTaxeGC   = 6000 × 14% = 840
        // TotalCatNatTaxeGC = 72 × 14% = 10.08
        var referentiel = TariffSnapshotBuilder.Standard();

        var breakdown = Aggregator.Agreger(RcNominal(), GcAvecVOL(), null, referentiel);

        breakdown.ParafiscaleGC.Montant.ShouldBe(60.72m);
        breakdown.TotalTaxeGC.Montant.ShouldBe(840m);
        breakdown.TotalCatNatTaxeGC.Montant.ShouldBe(10.08m); // 72 × 14% (TauxTaxeGC)
    }

    [Fact]
    public void Agreger_AvecRemorque_PrimeTTCRemorqueIncluse()
    {
        // PrimeRemorque = 190, CatNatRemorque = 190 × 3.5% = 6.65
        var remorque = new RemorquePremiumResult(new PrimeHT(190m), new CatNatHT(6.65m));
        var referentiel = TariffSnapshotBuilder.Standard();

        var breakdown = Aggregator.Agreger(RcNominal(), GcVide(), remorque, referentiel);

        breakdown.PrimeRemorque.ShouldNotBeNull();
        breakdown.PrimeRemorque!.Montant.ShouldBe(190m);
        breakdown.CatNatRemorque!.Montant.ShouldBe(6.65m);
        breakdown.PrimeTTCRemorque.Montant.ShouldBeGreaterThan(0m);
        breakdown.PrimeTotalTTC.Montant.ShouldBeGreaterThan(breakdown.PrimeTTCRC.Montant);
    }

    [Fact]
    public void Agreger_SansRemorque_PrimeRemorqueNull()
    {
        var breakdown = Aggregator.Agreger(RcNominal(), GcVide(), null, TariffSnapshotBuilder.Standard());

        breakdown.PrimeRemorque.ShouldBeNull();
        breakdown.CatNatRemorque.ShouldBeNull();
        breakdown.PrimeTTCRemorque.Montant.ShouldBe(0m);
        breakdown.ParafiscaleRemorque.Montant.ShouldBe(0m);
    }

    [Fact]
    public void Agreger_PrimeTotalTTC_EstCeilingSurSomme()
    {
        var rc = new RcPremiumResult(
            PrimeRC: new PrimeHT(950.33m),
            CatNatRC: new CatNatHT(33.26m),
            TaxeRC: new TaxeCA(133.05m),
            CatNatTaxeRC: new TaxeCA(4.66m),
            ParafiscaleRC: new Parafiscale(9.84m),
            TimbreCNPAC: new Timbre(10m));

        var breakdown = Aggregator.Agreger(rc, GcVide(), null, TariffSnapshotBuilder.Standard());

        var brut = breakdown.PrimeTTCRC.Montant;
        breakdown.PrimeTotalTTC.Montant.ShouldBe(Math.Ceiling(brut));
    }

    [Fact]
    public void Agreger_GCVide_TaxesGCZero()
    {
        var breakdown = Aggregator.Agreger(RcNominal(), GcVide(), null, TariffSnapshotBuilder.Standard());

        breakdown.TotalTaxeGC.Montant.ShouldBe(0m);
        breakdown.TotalCatNatTaxeGC.Montant.ShouldBe(0m);
        breakdown.ParafiscaleGC.Montant.ShouldBe(0m);
        breakdown.PrimeTTCGC.Montant.ShouldBe(0m);
    }

    // ── FsCheck properties ────────────────────────────────────────────────────

    [Property]
    public Property PrimeTotalTTC_ToujoursSuperieurOuEgaleRCSeul()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(CedarRisk.Domain.Common.Unit.Value)),
            _ =>
            {
                var referentiel = TariffSnapshotBuilder.Standard();
                var breakdown = Aggregator.Agreger(RcNominal(), GcVide(), null, referentiel);
                return breakdown.PrimeTotalTTC.Montant >= breakdown.PrimeTTCRC.Montant;
            });
    }

    [Property]
    public Property PrimeTotalTTC_AvecGC_SuperieureASansGC()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(CedarRisk.Domain.Common.Unit.Value)),
            _ =>
            {
                var referentiel = TariffSnapshotBuilder.Standard();
                var sansGC = Aggregator.Agreger(RcNominal(), GcVide(), null, referentiel);
                var avecGC = Aggregator.Agreger(RcNominal(), GcAvecVOL(), null, referentiel);
                return avecGC.PrimeTotalTTC.Montant > sansGC.PrimeTotalTTC.Montant;
            });
    }

    [Property]
    public Property PrimeTotalTTC_CeilingToujours_EntierSuperieurOuEgal()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(CedarRisk.Domain.Common.Unit.Value)),
            _ =>
            {
                var referentiel = TariffSnapshotBuilder.Standard();
                var breakdown = Aggregator.Agreger(RcNominal(), GcAvecVOL(), null, referentiel);
                var total = breakdown.PrimeTotalTTC.Montant;
                return total == Math.Ceiling(total) || total % 1 == 0;
            });
    }

    [Property]
    public Property ParafiscaleGC_ToujoursInferieureAuTotalPrimeGC()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(CedarRisk.Domain.Common.Unit.Value)),
            _ =>
            {
                var referentiel = TariffSnapshotBuilder.Standard();
                var breakdown = Aggregator.Agreger(RcNominal(), GcAvecVOL(), null, referentiel);
                return breakdown.ParafiscaleGC.Montant < breakdown.PrimesGC.TotalPrimeHT.Montant;
            });
    }
}
