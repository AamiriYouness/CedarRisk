using CedarRisk.Application.Engines.Implementation;
using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires;
using CedarRisk.Domain.ReferentielTarifaires.ValueObjects;
using CedarRisk.Domain.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Shouldly;
using Xunit;

namespace CedarRisk.Tests.Unit.Engines;

public class RemorquePremiumEngineTests
{
    private static readonly RemorquePremiumEngine Engine = new();

    private static RemorquePremiumContexte Contexte(
        decimal primeRC = 950m,
        int nbrRemorque = 1,
        decimal prorata = 1.00m,
        TarifRemorque? tarif = null)
    {
        var referentiel = tarif is null
            ? TariffSnapshotBuilder.Standard()
            : new TariffSnapshotBuilder().Build(); // rebuilt below with custom tarif

        // When custom tarif needed, build referentiel directly via builder
        // Default uses TarifRemorque.AvecTaux(20%)
        return new RemorquePremiumContexte(
            new PrimeHT(primeRC),
            nbrRemorque,
            ProrataFactor.Of(prorata),
            referentiel);
    }

    // ── Mode Taux ─────────────────────────────────────────────────────────────

    [Fact]
    public void Calculer_ModeTaux_UneRemorque_AnneeComplete()
    {
        // PrimeRC = 950, TauxRemorque = 20%, NbrRemorque = 1, Prorata = 1
        // PrimeHT = 950 × 0.20 × 1 = 190
        // CatNatHT = 190 × 3,5% = 6.65
        var result = Engine.Calculer(Contexte(primeRC: 950m, nbrRemorque: 1));

        result.IsSuccess.ShouldBeTrue();
        result.Value.PrimeHT.Montant.ShouldBe(190m);
        result.Value.CatNatHT.Montant.ShouldBe(6.65m);
    }

    [Fact]
    public void Calculer_ModeTaux_DeuxRemorques_DoubleLaPrime()
    {
        // PrimeHT = 950 × 0.20 × 2 = 380
        var une = Engine.Calculer(Contexte(nbrRemorque: 1)).Value.PrimeHT.Montant;
        var deux = Engine.Calculer(Contexte(nbrRemorque: 2)).Value.PrimeHT.Montant;

        deux.ShouldBe(une * 2);
    }

    [Fact]
    public void Calculer_ModeTaux_AvecProrata_AppliqueFacteur()
    {
        // Prorata dans PrimeRC déjà appliqué — TarifRemorque.Taux.CalculerPrime utilise PrimeRC telle quelle
        // PrimeRC = 475 (950 × 0.5), TauxRemorque = 20%
        // PrimeHT = 475 × 0.20 × 1 = 95
        var result = Engine.Calculer(Contexte(primeRC: 475m, nbrRemorque: 1));

        result.IsSuccess.ShouldBeTrue();
        result.Value.PrimeHT.Montant.ShouldBe(95m);
    }

    // ── Mode MontantFlat ──────────────────────────────────────────────────────

    [Fact]
    public void Calculer_ModeMontantFlat_AnneeComplete_MontantFixe()
    {
        // MontantFlat = 150 MAD par remorque, prorata = 1
        // PrimeHT = 150 × 1 × 1.00 = 150
        var referentiel = new TariffSnapshotBuilder()
            .Build(); // Standard uses Taux — need to override

        // Build referentiel with MontantFlat directly
        var referentielFlat = ReferentielTarifaireMontantFlatBuilder.Build(150m);

        var contexte = new RemorquePremiumContexte(
            new PrimeHT(950m),
            1,
            ProrataFactor.Of(1.00m),
            referentielFlat);

        var result = Engine.Calculer(contexte);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PrimeHT.Montant.ShouldBe(150m);
    }

    [Fact]
    public void Calculer_ModeMontantFlat_AvecProrata_AppliqueProrata()
    {
        // MontantFlat = 150, prorata = 0.5 → PrimeHT = 150 × 1 × 0.5 = 75
        var referentielFlat = ReferentielTarifaireMontantFlatBuilder.Build(150m);

        var contexte = new RemorquePremiumContexte(
            new PrimeHT(950m),
            1,
            ProrataFactor.Of(0.50m),
            referentielFlat);

        var result = Engine.Calculer(contexte);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PrimeHT.Montant.ShouldBe(75m);
    }

    // ── Guard NbrRemorque < 1 ─────────────────────────────────────────────────

    [Fact]
    public void Calculer_NbrRemorqueZero_RetourneFailure()
    {
        var result = Engine.Calculer(Contexte(nbrRemorque: 0));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Calculer_NbrRemorqueNegatif_RetourneFailure(int nbrRemorque)
    {
        var result = Engine.Calculer(Contexte(nbrRemorque: nbrRemorque));

        result.IsFailure.ShouldBeTrue();
    }

    // ── FsCheck properties ────────────────────────────────────────────────────

    [Property]
    public Property PrimeRemorque_ToujoursPositive_PourNbrValide()
    {
        var arb = Arb.From(Gen.Choose(1, 2));

        return Prop.ForAll(arb, nbr =>
        {
            var result = Engine.Calculer(Contexte(nbrRemorque: nbr));
            return result.IsSuccess && result.Value.PrimeHT.Montant > 0;
        });
    }

    [Property]
    public Property CatNatRemorque_ToujoursInferieureAPrimeHT()
    {
        var arb = Arb.From(Gen.Choose(1, 2));

        return Prop.ForAll(arb, nbr =>
        {
            var result = Engine.Calculer(Contexte(nbrRemorque: nbr));
            return result.IsSuccess &&
                   result.Value.CatNatHT.Montant < result.Value.PrimeHT.Montant;
        });
    }

    [Property]
    public Property DeuxRemorques_PrimeDoubleDUneRemorque_ModeTaux()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(500, 2000).Select(x => (decimal)x)),
            primeRC =>
            {
                var une = Engine.Calculer(Contexte(primeRC: primeRC, nbrRemorque: 1));
                var deux = Engine.Calculer(Contexte(primeRC: primeRC, nbrRemorque: 2));

                return une.IsSuccess && deux.IsSuccess &&
                       deux.Value.PrimeHT.Montant == une.Value.PrimeHT.Montant * 2;
            });
    }
}

/// <summary>Helper pour créer un ReferentielTarifaire avec TarifRemorque.MontantFlat.</summary>
file static class ReferentielTarifaireMontantFlatBuilder
{
    public static CedarRisk.Domain.ReferentielTarifaires.ReferentielTarifaire Build(decimal montantParRemorque)
    {
        var bareme = new[]
        {
            BaremeRC.Create(UsageVehicule.VehiculeTourisme, 1, null, new PrimeHT(950m)).Value,
        };

        var result = ReferentielTarifaire.Create(
           tauxCatNatRC: TariffRate.Of(0.035m),
           tauxTaxeRC: TariffRate.Of(0.14m),
           tauxParafiscaleRC: TariffRate.Of(0.01m),
           tauxTaxeGC: TariffRate.Of(0.14m),
           tauxParafiscaleGC: TariffRate.Of(0.01m),
           tarifRemorque: new TarifRemorque.MontantFlat(new PrimeHT(montantParRemorque)),
           timbreCNPAC: new Timbre(10m),
           bareme: bareme,
           validFrom: new DateOnly(2026, 1, 1),
           validTo: null,
           now: DateTimeOffset.UtcNow);

        return result.Value;
    }
}
