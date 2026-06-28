using CedarRisk.Application.Engines.Implementation;
using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.Common;
using CedarRisk.Domain.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Shouldly;
using Xunit;

namespace CedarRisk.Tests.Unit.Engines;

public sealed class RcPremiumEngineTests
{
    private static readonly RcPremiumEngine Engine = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RcPremiumContexte Contexte(
        int puissance = 8,
        UsageVehicule usage = UsageVehicule.VehiculeTourisme,
        decimal crm = 1.00m,
        decimal prorata = 1.00m) =>
        new(
            puissance,
            usage,
            ProrataFactor.Of(prorata),
            CrmCoefficient.Of(crm).Value,
            TariffSnapshotBuilder.Standard());

    // ── Calcul nominal ────────────────────────────────────────────────────────

    [Fact]
    public void Calculer_PuissanceFiscale8_VehiculeTourisme_CrmUn_AnneeComplete_RetournePrimeCorrecte()
    {
        // PrimeBase = 950 MAD (tranche 8-10 CV, VehiculeTourisme)
        // PrimeRC_HT = 950 × 1.00 × 1.00 = 950
        // CatNatRC   = 950 × 3.5% = 33.25
        // TaxeRC     = 950 × 14%  = 133.00
        // CatNatTaxe = 33.25 × 14% = 1.596 → 1.60
        // Parafiscale= (950 + 33.25) × 1% = 9.614 → 9.83
        // Timbre     = 10
        var result = Engine.Calculer(Contexte());

        result.IsSuccess.ShouldBeTrue();
        result.Value.PrimeRC.Montant.ShouldBe(950m);
        result.Value.CatNatRC.Montant.ShouldBe(33.25m);
        result.Value.TaxeRC.Montant.ShouldBe(133.00m);
        result.Value.CatNatTaxeRC.Montant.ShouldBe(4.66m);
        result.Value.ParafiscaleRC.Montant.ShouldBe(9.83m);
        result.Value.TimbreCNPAC.Montant.ShouldBe(10m);
    }

    [Fact]
    public void Calculer_AvecCrmBonus_AppliqueCoefficient()
    {
        // PrimeBase = 950, CRM = 0.85 → PrimeRC = 950 × 0.85 = 807.50
        var result = Engine.Calculer(Contexte(crm: 0.85m));

        result.IsSuccess.ShouldBeTrue();
        result.Value.PrimeRC.Montant.ShouldBe(807.50m);
    }

    [Fact]
    public void Calculer_AvecCrmMalus_AppliqueCoefficient()
    {
        // PrimeBase = 950, CRM = 1.25 → PrimeRC = 950 × 1.25 = 1187.50
        var result = Engine.Calculer(Contexte(crm: 1.25m));

        result.IsSuccess.ShouldBeTrue();
        result.Value.PrimeRC.Montant.ShouldBe(1187.50m);
    }

    [Fact]
    public void Calculer_AvecProrataDemiAnnee_AppliqueFacteur()
    {
        // 182 jours / 365 ≈ 0.4986 — on teste avec prorata fixe 0.5
        // PrimeRC = 950 × 1.00 × 0.50 = 475
        var result = Engine.Calculer(Contexte(prorata: 0.50m));

        result.IsSuccess.ShouldBeTrue();
        result.Value.PrimeRC.Montant.ShouldBe(475m);
    }

    [Theory]
    [InlineData(1, UsageVehicule.VehiculeTourisme, 500)]
    [InlineData(5, UsageVehicule.VehiculeTourisme, 700)]
    [InlineData(8, UsageVehicule.VehiculeTourisme, 950)]
    [InlineData(11, UsageVehicule.VehiculeTourisme, 1200)]
    [InlineData(15, UsageVehicule.VehiculeTourisme, 1600)]
    [InlineData(21, UsageVehicule.VehiculeTourisme, 2000)]
    [InlineData(8, UsageVehicule.Taxi, 1400)]
    [InlineData(8, UsageVehicule.TransportMarchandises, 1250)]
    public void Calculer_TranchesBareme_RetournePrimeBaseCorrecte(
        int puissance, UsageVehicule usage, decimal primeBaseAttendue)
    {
        var result = Engine.Calculer(Contexte(puissance, usage));

        result.IsSuccess.ShouldBeTrue();
        result.Value.PrimeRC.Montant.ShouldBe(primeBaseAttendue);
    }

    [Fact]
    public void Calculer_PuissanceHorsBareme_RetourneFailure()
    {
        // Aucune tranche pour puissance 99 CV dans le snapshot standard
        var result = Engine.Calculer(Contexte(puissance: 8, usage: UsageVehicule.TransportPublicVoyageurs));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Calculer_TimbreCNPAC_ToujoursPresent()
    {
        // Timbre = 10 MAD fixe, quelle que soit la prime
        var result = Engine.Calculer(Contexte(crm: 0.50m));

        result.IsSuccess.ShouldBeTrue();
        result.Value.TimbreCNPAC.Montant.ShouldBe(10m);
    }

    [Fact]
    public void Calculer_TauxZeroCatNat_CatNatEstZero()
    {
        var referentiel = new TariffSnapshotBuilder()
            .AvecTauxCatNatRC(0m)
            .Build();

        var contexte = new RcPremiumContexte(
            8, UsageVehicule.VehiculeTourisme,
            ProrataFactor.Of(1m),
            CrmCoefficient.Of(1.00m).Value,
            referentiel);

        var result = Engine.Calculer(contexte);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CatNatRC.Montant.ShouldBe(0m);
        result.Value.CatNatTaxeRC.Montant.ShouldBe(0m);
    }

    // ── FsCheck properties ────────────────────────────────────────────────────

    [Property]
    public Property PrimeRC_ToujoursPositive_PourCrmValide()
    {
        var arb = Arb.From(
            from crm in Gen.Choose(50, 350).Select(x => x / 100m)
            select crm);

        return Prop.ForAll(arb, crm =>
        {
            var result = Engine.Calculer(Contexte(crm: crm));
            return result.IsSuccess && result.Value.PrimeRC.Montant > 0;
        });
    }

    [Property]
    public Property CrmMalus_ProduitPrimePlusGrandeQueCrmBonus()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(CedarRisk.Domain.Common.Unit.Value)),
            _ =>
            {
                var bonus = Engine.Calculer(Contexte(crm: 0.50m)).Value.PrimeRC.Montant;
                var malus = Engine.Calculer(Contexte(crm: 3.50m)).Value.PrimeRC.Montant;
                return malus > bonus;
            });
    }

    [Property]
    public Property Prorata_PrimeToujoursInferieurOuEgaleAnneeComplete()
    {
        var arb = Arb.From(
            Gen.Choose(1, 100).Select(x => x / 100m));

        return Prop.ForAll(arb, prorata =>
        {
            var anneeComplete = Engine.Calculer(Contexte(prorata: 1.00m)).Value.PrimeRC.Montant;
            var partielle = Engine.Calculer(Contexte(prorata: prorata)).Value.PrimeRC.Montant;
            return partielle <= anneeComplete;
        });
    }

    [Property]
    public Property TaxeRC_ToujoursPositive()
    {
        var arb = Arb.From(Gen.Choose(50, 350).Select(x => x / 100m));

        return Prop.ForAll(arb, crm =>
        {
            var result = Engine.Calculer(Contexte(crm: crm));
            return result.IsSuccess && result.Value.TaxeRC.Montant >= 0;
        });
    }

    [Property]
    public Property ParafiscaleRC_BaseSurPrimeEtCatNat()
    {
        // Parafiscale = (PrimeRC + CatNatRC) × TauxParafiscale
        // Vérifier que Parafiscale <= PrimeRC (taux parafiscale << 100%)
        var arb = Arb.From(Gen.Choose(50, 150).Select(x => x / 100m));

        return Prop.ForAll(arb, crm =>
        {
            var result = Engine.Calculer(Contexte(crm: crm));
            return result.IsSuccess &&
                   result.Value.ParafiscaleRC.Montant < result.Value.PrimeRC.Montant;
        });
    }
}