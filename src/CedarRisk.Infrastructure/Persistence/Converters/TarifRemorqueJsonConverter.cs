using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires.ValueObjects;
using CedarRisk.Domain.ValueObjects;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CedarRisk.Infrastructure.Persistence.Converters;

/// <summary>
/// Convertit TarifRemorque (union discriminée) vers/depuis JSONB Postgres.
///
/// Format sur disque :
///   { "type": "Taux",        "valeur": 0.20 }
///   { "type": "MontantFlat", "montantParRemorque": 150.00 }
/// </summary>
public sealed class TarifRemorqueJsonConverter : JsonConverter<TarifRemorque>
{
    private const string TypeField = "type";
    private const string ValeurField = "valeur";
    private const string MontantParRemorqueField = "montantParRemorque";

    private const string DiscriminantTaux = "Taux";
    private const string DiscriminantMontantFlat = "MontantFlat";

    public override TarifRemorque Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty(TypeField, out var typeElement))
            throw new JsonException($"TarifRemorque JSONB: propriété '{TypeField}' manquante.");

        var discriminant = typeElement.GetString()
            ?? throw new JsonException($"TarifRemorque JSONB: '{TypeField}' est null.");

        return discriminant switch
        {
            DiscriminantTaux => ReadTaux(root),
            DiscriminantMontantFlat => ReadMontantFlat(root),
            _ => throw new JsonException(
                $"TarifRemorque JSONB: discriminant inconnu '{discriminant}'. " +
                $"Attendu : '{DiscriminantTaux}' ou '{DiscriminantMontantFlat}'.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        TarifRemorque value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case TarifRemorque.Taux taux:
                writer.WriteString(TypeField, DiscriminantTaux);
                writer.WriteNumber(ValeurField, taux.Valeur.Value);
                break;

            case TarifRemorque.MontantFlat flat:
                writer.WriteString(TypeField, DiscriminantMontantFlat);
                writer.WriteNumber(MontantParRemorqueField, flat.MontantParRemorque.Montant);
                break;

            default:
                throw new JsonException(
                    $"TarifRemorque JSONB: sous-type non géré '{value.GetType().Name}'.");
        }

        writer.WriteEndObject();
    }

    private static TarifRemorque.Taux ReadTaux(JsonElement root)
    {
        if (!root.TryGetProperty(ValeurField, out var valeurEl))
            throw new JsonException(
                $"TarifRemorque.Taux JSONB: propriété '{ValeurField}' manquante.");

        var valeur = valeurEl.GetDecimal();

        // Revalide l'invariant domaine — protège contre une corruption de données en DB
        return new TarifRemorque.Taux(TariffRate.Of(valeur));
    }

    private static TarifRemorque.MontantFlat ReadMontantFlat(JsonElement root)
    {
        if (!root.TryGetProperty(MontantParRemorqueField, out var montantEl))
            throw new JsonException(
                $"TarifRemorque.MontantFlat JSONB: propriété '{MontantParRemorqueField}' manquante.");

        var montant = montantEl.GetDecimal();

        return new TarifRemorque.MontantFlat(new PrimeHT(montant));
    }
}
