using CedarRisk.Domain.GarantieTarifications;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using CedarRisk.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CedarRisk.Infrastructure.Persistence.Configurations;

public sealed class GarantieTarificationConfiguration : IEntityTypeConfiguration<GarantieTarification>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public void Configure(EntityTypeBuilder<GarantieTarification> builder)
    {
        builder.ToTable("garantie_tarifications");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        builder.Property(t => t.GarantieCode)
            .HasColumnName("garantie_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.EstSoumisACatNat)
            .HasColumnName("est_soumis_a_catnat")
            .IsRequired();

        // TauxCatNatGC — TariffRate value object
        builder.Property(t => t.TauxCatNatGC)
            .HasColumnName("taux_catnat_gc")
            .HasColumnType("numeric(8,6)")
            .HasConversion(v => v.Value, v => TariffRate.Of(v));

        // ModeTarifaire — discriminated union stored as jsonb with type discriminant
        builder.Property(t => t.ModeTarifaire)
            .HasColumnName("mode_tarifaire")
            .HasColumnType("jsonb")
            .HasConversion(
                v => SerializeMode(v),
                v => DeserializeMode(v));

        builder.Property(t => t.ValidFrom)
            .HasColumnName("valid_from")
            .IsRequired();

        builder.Property(t => t.ValidTo)
            .HasColumnName("valid_to");

        builder.Property(t => t.SupersededById)
            .HasColumnName("superseded_by_id");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Un seul tarif ouvert par garantie à tout moment
        builder.HasIndex(t => new { t.GarantieCode, t.ValidTo })
            .HasDatabaseName("uix_garantie_tarifications_code_open")
            .HasFilter("valid_to IS NULL")
            .IsUnique();

        builder.HasIndex(t => new { t.GarantieCode, t.ValidFrom })
            .HasDatabaseName("ix_garantie_tarifications_code_validfrom");
    }

    // ── ModeTarifaire serialization ───────────────────────────────────────────

    private static string SerializeMode(IModeTarifaire mode)
    {
        JsonObject obj = mode switch
        {
            TauxDirectValeurVenale m => new JsonObject
            {
                ["type"] = m.TypeDiscriminant,
                ["taux"] = m.Taux
            },
            TauxDirectCapitalGarantie m => new JsonObject
            {
                ["type"] = m.TypeDiscriminant,
                ["taux"] = m.Taux,
                ["capitalGarantie"] = m.CapitalGarantie
            },
            MontantFlat m => new JsonObject
            {
                ["type"] = m.TypeDiscriminant,
                ["montant"] = m.Montant
            },
            CapitalOptionnel m => BuildCapitalOptionnelNode(m),
            TauxSurCapitalPlafonne m => BuildTauxPlafonneNode(m),
            _ => throw new InvalidOperationException($"Mode inconnu : {mode.GetType().Name}")
        };

        return obj.ToJsonString(JsonOpts);
    }

    private static JsonObject BuildCapitalOptionnelNode(CapitalOptionnel m)
    {
        var obj = new JsonObject
        {
            ["type"] = m.TypeDiscriminant,
            ["options"] = new JsonArray(m.Options.Select(o =>
                (JsonNode)new JsonObject
                {
                    ["capital"] = o.Capital,
                    ["montantHT"] = o.MontantHT
                }).ToArray())
        };
        if (m.Franchise is not null)
            obj["franchise"] = BuildFranchiseNode(m.Franchise);
        return obj;
    }

    private static JsonObject BuildTauxPlafonneNode(TauxSurCapitalPlafonne m)
    {
        var obj = new JsonObject
        {
            ["type"] = m.TypeDiscriminant,
            ["taux"] = m.Taux,
            ["regle"] = new JsonObject
            {
                ["typePlafond"] = m.Regle.TypePlafond.ToString(),
                ["garantieCodeRef"] = m.Regle.GarantieCodeRef,
                ["conditions"] = new JsonArray(m.Regle.Conditions.Select(c =>
                    (JsonNode)new JsonObject
                    {
                        ["operateur"] = c.Operateur.ToString(),
                        ["pourcentage"] = c.Pourcentage
                    }).ToArray())
            }
        };
        if (m.Franchise is not null)
            obj["franchise"] = BuildFranchiseNode(m.Franchise);
        return obj;
    }

    private static JsonObject BuildFranchiseNode(Franchise f) => new()
    {
        ["tauxFranchise"] = f.TauxFranchise,
        ["montantMinimum"] = f.MontantMinimum.HasValue
            ? JsonValue.Create(f.MontantMinimum.Value)
            : null
    };

    private static IModeTarifaire DeserializeMode(string json)
    {
        var node = JsonNode.Parse(json)!;
        var type = node["type"]!.GetValue<string>();

        return type switch
        {
            "TauxDirectValeurVenale" => new TauxDirectValeurVenale(
                node["taux"]!.GetValue<decimal>()),

            "TauxDirectCapitalGarantie" => new TauxDirectCapitalGarantie(
                node["taux"]!.GetValue<decimal>(),
                node["capitalGarantie"]!.GetValue<decimal>()),

            "MontantFlat" => new MontantFlat(
                node["montant"]!.GetValue<decimal>()),

            "CapitalOptionnel" => new CapitalOptionnel(
                node["options"]!.AsArray().Select(o => new CapitalOption(
                    o!["capital"]!.GetValue<decimal>(),
                    o!["montantHT"]!.GetValue<decimal>())).ToList(),
                DeserializeFranchise(node["franchise"])),

            "TauxSurCapitalPlafonne" => DeserializeTauxSurCapitalPlafonne(node),

            _ => throw new InvalidOperationException($"Type mode tarifaire inconnu : {type}")
        };
    }

    private static TauxSurCapitalPlafonne DeserializeTauxSurCapitalPlafonne(JsonNode node)
    {
        var regleNode = node["regle"]!;
        var typePlafond = Enum.Parse<TypePlafond>(regleNode["typePlafond"]!.GetValue<string>());
        var garantieCodeRef = regleNode["garantieCodeRef"]?.GetValue<string>();
        var conditions = regleNode["conditions"]!.AsArray()
            .Select(c => new RegleCondition(
                Enum.Parse<OperateurComparaison>(c!["operateur"]!.GetValue<string>()),
                c!["pourcentage"]!.GetValue<decimal>()))
            .ToList();

        var regle = RegleCapital.Hydrate(typePlafond, garantieCodeRef, conditions);
        var franchise = DeserializeFranchise(node["franchise"]);

        return new TauxSurCapitalPlafonne(node["taux"]!.GetValue<decimal>(), regle, franchise);
    }

    private static Franchise? DeserializeFranchise(JsonNode? node)
    {
        if (node is null) return null;
        return Franchise.Hydrate(
            node["tauxFranchise"]!.GetValue<decimal>(),
            node["montantMinimum"]?.GetValue<decimal?>());
    }
}