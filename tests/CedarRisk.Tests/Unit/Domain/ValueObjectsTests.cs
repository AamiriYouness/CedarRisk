using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using CedarRisk.Domain.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Shouldly;
using Xunit;

namespace CedarRisk.Tests.Unit.Domain;

// =============================================================================
// ProrataFactor
// =============================================================================

public sealed class ProrataFactorTests
{
    [Fact]
    public void Calculer_AnneeComplete_RetourneUn()
    {
        var result = ProrataFactor.Calculer(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(1m);
    }

    [Fact]
    public void Calculer_AnneeComplete_AnneeBissextile_RetourneUn()
    {
        // 2024 est bissextile — 366 jours
        var result = ProrataFactor.Calculer(new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(1m);
    }

    [Fact]
    public void Calculer_PlusDUnAn_ClampedA1()
    {
        var result = ProrataFactor.Calculer(new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(1m);
    }

    [Fact]
    public void Calculer_DemiAnnee_RetourneEnviron0_5()
    {
        // 181 jours / 365 ≈ 0.4959
        var result = ProrataFactor.Calculer(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBeInRange(0.49m, 0.51m);
    }

    [Fact]
    public void Calculer_DateEcheanceAnterieureOuEgaleADateEffet_RetourneFailure()
    {
        var result = ProrataFactor.Calculer(new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1));

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Calculer_MemeDate_RetourneFailure()
    {
        var result = ProrataFactor.Calculer(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1));

        result.IsFailure.ShouldBeTrue();
    }

    [Property]
    public Property Prorata_ToujoursEntre0Et1()
    {
        var arb = Arb.From(
            from jours in Gen.Choose(1, 366)
            let debut = new DateOnly(2026, 1, 1)
            let fin = debut.AddDays(jours)
            select (debut, fin));

        return Prop.ForAll(arb, t =>
        {
            var result = ProrataFactor.Calculer(t.debut, t.fin);
            return result.IsSuccess &&
                   result.Value.Value > 0m &&
                   result.Value.Value <= 1m;
        });
    }
}

// =============================================================================
// CrmCoefficient
// =============================================================================

public sealed class CrmCoefficientTests
{
    [Theory]
    [InlineData(0.50)]
    [InlineData(1.00)]
    [InlineData(3.50)]
    public void Of_BornesValides_RetourneSuccess(double valeur)
    {
        var result = CrmCoefficient.Of((decimal)valeur);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe((decimal)valeur);
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(3.51)]
    [InlineData(0.00)]
    [InlineData(-1.00)]
    public void Of_HorsBornes_RetourneFailure(double valeur)
    {
        var result = CrmCoefficient.Of((decimal)valeur);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ApplySansSinistre_ReduitDe5Pourcent()
    {
        var crm = CrmCoefficient.Of(1.00m).Value;
        var nouveau = crm.ApplySansSinistre();

        nouveau.Value.ShouldBe(0.95m);
    }

    [Fact]
    public void ApplyAvecSinistre_AugmenteDe25Pourcent()
    {
        var crm = CrmCoefficient.Of(1.00m).Value;
        var nouveau = crm.ApplyAvecSinistre();

        nouveau.Value.ShouldBe(1.25m);
    }

    [Fact]
    public void ApplySansSinistre_NePeutPasDescendreSous0_50()
    {
        var crm = CrmCoefficient.Of(0.50m).Value;
        var nouveau = crm.ApplySansSinistre();

        nouveau.Value.ShouldBeGreaterThanOrEqualTo(0.50m);
    }

    [Fact]
    public void ApplyAvecSinistre_NePeutPasDepasserMax()
    {
        var crm = CrmCoefficient.Of(3.50m).Value;
        var nouveau = crm.ApplyAvecSinistre();

        nouveau.Value.ShouldBeLessThanOrEqualTo(3.50m);
    }

    [Property]
    public Property CrmBornes_ToujoursEntre0_5Et3_5()
    {
        var arb = Arb.From(Gen.Choose(50, 350).Select(x => x / 100m));

        return Prop.ForAll(arb, valeur =>
        {
            var result = CrmCoefficient.Of(valeur);
            return result.IsSuccess &&
                   result.Value.Value >= 0.50m &&
                   result.Value.Value <= 3.50m;
        });
    }
}

// =============================================================================
// TariffRate
// =============================================================================

public sealed class TariffRateTests
{
    [Theory]
    [InlineData(0.00)]
    [InlineData(0.01)]
    [InlineData(0.50)]
    [InlineData(1.00)]
    public void Of_BornesValides_RetourneInstance(double valeur)
    {
        var rate = TariffRate.Of((decimal)valeur);
        rate.Value.ShouldBe((decimal)valeur);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(2.00)]
    public void Of_HorsBornes_LeveArgumentOutOfRange(double valeur)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => TariffRate.Of((decimal)valeur));
    }

    [Fact]
    public void Apply_MultiplieParLaBase()
    {
        var rate = TariffRate.Of(0.14m);
        rate.Apply(1000m).ShouldBe(140m);
    }
}

// =============================================================================
// PrimeHT — chaîne fiscale
// =============================================================================

public sealed class PrimeHTFiscalChainTests
{
    [Fact]
    public void AppliquerCatNat_CalculeCorrectement()
    {
        var prime = new PrimeHT(950m);
        var catNat = prime.AppliquerCatNat(0.012m);

        catNat.Montant.ShouldBe(11.40m);
    }

    [Fact]
    public void AppliquerTaxeCA_CalculeCorrectement()
    {
        var prime = new PrimeHT(950m);
        var taxe = prime.AppliquerTaxeCA(0.14m);

        taxe.Montant.ShouldBe(133.00m);
    }

    [Fact]
    public void AppliquerParafiscale_BaseSurPrimeEtCatNat()
    {
        var prime = new PrimeHT(950m);
        var catNat = new CatNatHT(11.40m);
        var para = prime.AppliquerParafiscale(0.01m, catNat);

        // (950 + 11.40) × 1% = 9.614 → 9.61 AwayFromZero
        para.Montant.ShouldBe(9.61m);
    }

    [Fact]
    public void CatNatHT_AppliquerTaxeCA_InclusTaxeSurBase()
    {
        // CatNat × TauxTaxe — formule spécifique CatNat
        var catNat = new CatNatHT(11.40m);
        var taxe = catNat.AppliquerTaxeCA(0.14m);

        // 11.40 × 0.14 = 12.996 → 13.00
        taxe.Montant.ShouldBe(1.6m);
    }

    [Fact]
    public void PrimeTTC_AssemblerRC_InclusTousComposants()
    {
        var ttc = PrimeTTC.AssemblerRC(
            new PrimeHT(950m),
            new TaxeCA(133m),
            new CatNatHT(11.40m),
            new TaxeCA(1.60m),
            new Parafiscale(9.61m),
            new Timbre(10m));

        ttc.Montant.ShouldBe(1115.61m);
    }

    [Fact]
    public void PrimeTTC_AssemblerGC_ExclutTimbre()
    {
        var ttc = PrimeTTC.AssemblerGC(
            new PrimeHT(6000m),
            new TaxeCA(840m),
            new CatNatHT(72m),
            new TaxeCA(72.86m),  // 72 × (1 + 1.2%)
            new Parafiscale(60.72m));

        // 6000 + 840 + 72 + 72.86 + 60.72 = 7045.58
        ttc.Montant.ShouldBe(7045.58m);
    }

    [Property]
    public Property TaxAlwaysPositive_PourPrimePositive()
    {
        var arb = Arb.From(Gen.Choose(1, 100000).Select(x => (decimal)x));

        return Prop.ForAll(arb, montant =>
        {
            var prime = new PrimeHT(montant);
            var taxe = prime.AppliquerTaxeCA(0.14m);
            var catNat = prime.AppliquerCatNat(0.012m);
            return taxe.Montant > 0 && catNat.Montant >= 0;
        });
    }
}

// =============================================================================
// ModeTarifaire — quatre variants
// =============================================================================

public sealed class ModeTarifaireTests
{
    private static BaseTarifaire Base(decimal valeurVenale = 120_000m,
        decimal? capitalClient = null, CapitalOption? option = null) =>
        new(valeurVenale, capitalClient, null, option);

    // ── TauxDirectValeurVenale ─────────────────────────────────────────────

    [Fact]
    public void TauxDirectValeurVenale_CalculePrimeCorrectement()
    {
        var mode = TauxDirectValeurVenale.Create(0.03m).Value;
        var result = mode.CalculerPrime("VOL", Base(valeurVenale: 200_000m));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Montant.ShouldBe(6000m); // 200_000 × 3%
    }

    [Fact]
    public void TauxDirectValeurVenale_TauxZero_RetourneFailure()
    {
        var result = TauxDirectValeurVenale.Create(0m);
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void TauxDirectValeurVenale_TauxNegatif_RetourneFailure()
    {
        var result = TauxDirectValeurVenale.Create(-0.01m);
        result.IsFailure.ShouldBeTrue();
    }

    // ── MontantFlat ────────────────────────────────────────────────────────

    [Fact]
    public void MontantFlat_RetourneMontantFixe_IndependantValeurVenale()
    {
        var mode = MontantFlat.Create(400m).Value;

        var r1 = mode.CalculerPrime("BRIS", Base(valeurVenale: 50_000m));
        var r2 = mode.CalculerPrime("BRIS", Base(valeurVenale: 500_000m));

        r1.Value.Montant.ShouldBe(400m);
        r2.Value.Montant.ShouldBe(400m);
    }

    [Fact]
    public void MontantFlat_MontantZero_RetourneFailure()
    {
        var result = MontantFlat.Create(0m);
        result.IsFailure.ShouldBeTrue();
    }

    // ── CapitalOptionnel ───────────────────────────────────────────────────

    [Fact]
    public void CapitalOptionnel_OptionChoisie_RetourneMontantHT()
    {
        var mode = CapitalOptionnel.Create([
            new CapitalOption(50_000m, 800m),
            new CapitalOption(100_000m, 1_400m),
            new CapitalOption(150_000m, 1_900m),
        ]).Value;

        var contexte = Base(valeurVenale: 200_000m, option: new CapitalOption(100_000m, 1_400m));
        var result = mode.CalculerPrime("PJ", contexte);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Montant.ShouldBe(1_400m);
    }

    [Fact]
    public void CapitalOptionnel_OptionNonChoisie_RetourneFailure()
    {
        var mode = CapitalOptionnel.Create([new CapitalOption(50_000m, 800m)]).Value;
        var result = mode.CalculerPrime("PJ", Base(option: null));

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void CapitalOptionnel_CapitalDepasse_ValeurVenale_RetourneFailure()
    {
        var mode = CapitalOptionnel.Create([new CapitalOption(200_000m, 2_000m)]).Value;
        // ValeurVenale = 100_000 < Capital = 200_000
        var contexte = Base(valeurVenale: 100_000m, option: new CapitalOption(200_000m, 2_000m));
        var result = mode.CalculerPrime("PJ", contexte);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void CapitalOptionnel_ListeVide_RetourneFailure()
    {
        var result = CapitalOptionnel.Create([]);
        result.IsFailure.ShouldBeTrue();
    }

    // ── TauxSurCapitalPlafonne ─────────────────────────────────────────────

    [Fact]
    public void TauxSurCapitalPlafonne_CapitalValide_CalculeCorrectement()
    {
        // RegleCapital: <= 100% VV, CapitalClient = 80_000, VV = 100_000
        // PrimeHT = 80_000 × 2% = 1_600
        var regle = RegleCapital.SurValeurVenale(OperateurComparaison.InferieurOuEgal, 100m).Value;
        var mode = TauxSurCapitalPlafonne.Create(0.02m, regle).Value;

        var contexte = new BaseTarifaire(100_000m, 80_000m, null, null);
        var result = mode.CalculerPrime("RC_CONDUCTEUR", contexte);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Montant.ShouldBe(1_600m);
    }

    [Fact]
    public void TauxSurCapitalPlafonne_CapitalDepasseValeurVenale_RetourneFailure()
    {
        var regle = RegleCapital.SurValeurVenale(OperateurComparaison.InferieurOuEgal, 100m).Value;
        var mode = TauxSurCapitalPlafonne.Create(0.02m, regle).Value;

        // CapitalClient = 110_000 > VV = 100_000
        var contexte = new BaseTarifaire(100_000m, 110_000m, null, null);
        var result = mode.CalculerPrime("RC_CONDUCTEUR", contexte);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void TauxSurCapitalPlafonne_CapitalDepasse50PctVV_Regle50Pct_RetourneFailure()
    {
        // PT: RegleCapital >= 10% AND <= 50% VV
        var regle = RegleCapital.Create(TypePlafond.ValeurVenale, [
            new RegleCondition(OperateurComparaison.SuperieurOuEgal, 10m),
            new RegleCondition(OperateurComparaison.InferieurOuEgal, 50m),
        ]).Value;
        var mode = TauxSurCapitalPlafonne.Create(0.015m, regle).Value;

        // CapitalClient = 60_000 > 50% de 100_000 = 50_000
        var contexte = new BaseTarifaire(100_000m, 60_000m, null, null);
        var result = mode.CalculerPrime("PT", contexte);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void TauxSurCapitalPlafonne_CapitalSousMinimum_RetourneFailure()
    {
        // PT: CapitalClient = 5_000 < 10% de 100_000 = 10_000
        var regle = RegleCapital.Create(TypePlafond.ValeurVenale, [
            new RegleCondition(OperateurComparaison.SuperieurOuEgal, 10m),
            new RegleCondition(OperateurComparaison.InferieurOuEgal, 50m),
        ]).Value;
        var mode = TauxSurCapitalPlafonne.Create(0.015m, regle).Value;

        var contexte = new BaseTarifaire(100_000m, 5_000m, null, null);
        var result = mode.CalculerPrime("PT", contexte);

        result.IsFailure.ShouldBeTrue();
    }

    [Property]
    public Property TauxSurCapitalPlafonne_PrimeProportionnelleAuCapital()
    {
        var arb = Arb.From(Gen.Choose(10_000, 90_000).Select(x => (decimal)x));

        return Prop.ForAll(arb, capital =>
        {
            var regle = RegleCapital.SurValeurVenale(OperateurComparaison.InferieurOuEgal, 100m).Value;
            var mode = TauxSurCapitalPlafonne.Create(0.02m, regle).Value;
            var ctx = new BaseTarifaire(100_000m, capital, null, null);
            var result = mode.CalculerPrime("RC_CONDUCTEUR", ctx);

            return result.IsSuccess &&
                   result.Value.Montant == Math.Round(capital * 0.02m, 2, MidpointRounding.AwayFromZero);
        });
    }
}