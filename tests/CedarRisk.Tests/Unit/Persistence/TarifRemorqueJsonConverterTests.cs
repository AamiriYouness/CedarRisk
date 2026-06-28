using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires.ValueObjects;
using CedarRisk.Domain.ValueObjects;
using CedarRisk.Infrastructure.Persistence.Converters;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace CedarRisk.Tests.Unit.Persistence;

public sealed class TarifRemorqueJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new TarifRemorqueJsonConverter() }
    };

    // ── Taux ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Taux_Roundtrip_PreserveValeur()
    {
        var original = new TarifRemorque.Taux(TariffRate.Of(0.20m));

        var json = JsonSerializer.Serialize<TarifRemorque>(original, Options);
        var restored = JsonSerializer.Deserialize<TarifRemorque>(json, Options);

        restored.ShouldBeOfType<TarifRemorque.Taux>();
        ((TarifRemorque.Taux)restored!).Valeur.Value.ShouldBe(0.20m);
    }

    [Fact]
    public void Taux_Serialise_AvecDiscriminantCorrect()
    {
        var taux = new TarifRemorque.Taux(TariffRate.Of(0.15m));

        var json = JsonSerializer.Serialize<TarifRemorque>(taux, Options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("type").GetString().ShouldBe("Taux");
        doc.RootElement.GetProperty("valeur").GetDecimal().ShouldBe(0.15m);
        doc.RootElement.EnumerateObject().Count().ShouldBe(2); // pas de champs parasites
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(0.01)]
    [InlineData(0.50)]
    [InlineData(1.00)]
    public void Taux_Roundtrip_BornesValides(double valeur)
    {
        var original = new TarifRemorque.Taux(TariffRate.Of((decimal)valeur));

        var json = JsonSerializer.Serialize<TarifRemorque>(original, Options);
        var restored = (TarifRemorque.Taux)JsonSerializer.Deserialize<TarifRemorque>(json, Options)!;

        restored.Valeur.Value.ShouldBe((decimal)valeur);
    }

    // ── MontantFlat ───────────────────────────────────────────────────────────

    [Fact]
    public void MontantFlat_Roundtrip_PreserveMontant()
    {
        var original = new TarifRemorque.MontantFlat(new PrimeHT(150.50m));

        var json = JsonSerializer.Serialize<TarifRemorque>(original, Options);
        var restored = JsonSerializer.Deserialize<TarifRemorque>(json, Options);

        restored.ShouldBeOfType<TarifRemorque.MontantFlat>();
        ((TarifRemorque.MontantFlat)restored!).MontantParRemorque.Montant.ShouldBe(150.50m);
    }

    [Fact]
    public void MontantFlat_Serialise_AvecDiscriminantCorrect()
    {
        var flat = new TarifRemorque.MontantFlat(new PrimeHT(200m));

        var json = JsonSerializer.Serialize<TarifRemorque>(flat, Options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("type").GetString().ShouldBe("MontantFlat");
        doc.RootElement.GetProperty("montantParRemorque").GetDecimal().ShouldBe(200m);
        doc.RootElement.EnumerateObject().Count().ShouldBe(2);
    }

    // ── Erreurs de désérialisation ────────────────────────────────────────────

    [Fact]
    public void Deserialise_DiscriminantInconnu_LeveJsonException()
    {
        const string json = """{"type":"Inconnu","valeur":0.10}""";

        var ex = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<TarifRemorque>(json, Options));

        ex.Message.ShouldContain("Inconnu");
    }

    [Fact]
    public void Deserialise_SansChampType_LeveJsonException()
    {
        const string json = """{"valeur":0.10}""";

        Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<TarifRemorque>(json, Options));
    }

    [Fact]
    public void Deserialise_Taux_SansChampValeur_LeveJsonException()
    {
        const string json = """{"type":"Taux"}""";

        Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<TarifRemorque>(json, Options));
    }

    [Fact]
    public void Deserialise_MontantFlat_SansChampMontant_LeveJsonException()
    {
        const string json = """{"type":"MontantFlat"}""";

        Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<TarifRemorque>(json, Options));
    }

    [Fact]
    public void Deserialise_Taux_ValeurHorsBornes_LeveArgumentOutOfRangeException()
    {
        // TariffRate.Of valide les bornes [0,1] — corruption DB détectée à la lecture
        const string json = """{"type":"Taux","valeur":1.50}""";

        Should.Throw<ArgumentOutOfRangeException>(
            () => JsonSerializer.Deserialize<TarifRemorque>(json, Options));
    }

    // ── Symétrie serialize/deserialize ────────────────────────────────────────

    [Fact]
    public void Taux_EtMontantFlat_ProduisentJsonDistincts()
    {
        var taux = new TarifRemorque.Taux(TariffRate.Of(0.20m));
        var flat = new TarifRemorque.MontantFlat(new PrimeHT(100m));

        var jsonTaux = JsonSerializer.Serialize<TarifRemorque>(taux, Options);
        var jsonFlat = JsonSerializer.Serialize<TarifRemorque>(flat, Options);

        jsonTaux.ShouldNotBe(jsonFlat);
        jsonTaux.ShouldContain("\"type\":\"Taux\"");
        jsonFlat.ShouldContain("\"type\":\"MontantFlat\"");
    }
}