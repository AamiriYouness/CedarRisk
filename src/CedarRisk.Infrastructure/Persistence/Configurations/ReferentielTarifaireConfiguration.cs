using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires;
using CedarRisk.Domain.ReferentielTarifaires.ValueObjects;
using CedarRisk.Domain.ValueObjects;
using CedarRisk.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace CedarRisk.Infrastructure.Persistence.Configurations;

public sealed class ReferentielTarifaireConfiguration
    : IEntityTypeConfiguration<ReferentielTarifaire>
{
    private static readonly ValueConverter<TariffRate, decimal> TariffRateConverter = new(
        v => v.Value,
        v => TariffRate.Of(v));

    private static readonly ValueConverter<Timbre, decimal> TimbreConverter = new(
        v => v.Montant,
        v => new Timbre(v));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new TarifRemorqueJsonConverter() }
    };

    private static readonly ValueConverter<TarifRemorque, string> TarifRemorqueConverter = new(
        v => JsonSerializer.Serialize(v, JsonOptions),
        v => JsonSerializer.Deserialize<TarifRemorque>(v, JsonOptions)!);

    public void Configure(EntityTypeBuilder<ReferentielTarifaire> builder)
    {
        builder.ToTable("referentiel_tarifaires");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        // ── RC ────────────────────────────────────────────────────────────────
        builder.Property(r => r.TauxCatNatRC)
            .HasColumnName("taux_catnat_rc")
            .HasColumnType("numeric(6,5)")
            .HasConversion(TariffRateConverter)
            .IsRequired();

        builder.Property(r => r.TauxTaxeRC)
            .HasColumnName("taux_taxe_rc")
            .HasColumnType("numeric(6,5)")
            .HasConversion(TariffRateConverter)
            .IsRequired();

        builder.Property(r => r.TauxParafiscaleRC)
            .HasColumnName("taux_parafiscale_rc")
            .HasColumnType("numeric(6,5)")
            .HasConversion(TariffRateConverter)
            .IsRequired();

        // ── GC ────────────────────────────────────────────────────────────────
        // TauxCatNatGC lives on GarantieTarification per guarantee — not here

        builder.Property(r => r.TauxTaxeGC)
            .HasColumnName("taux_taxe_gc")
            .HasColumnType("numeric(6,5)")
            .HasConversion(TariffRateConverter)
            .IsRequired();

        builder.Property(r => r.TauxParafiscaleGC)
            .HasColumnName("taux_parafiscale_gc")
            .HasColumnType("numeric(6,5)")
            .HasConversion(TariffRateConverter)
            .IsRequired();

        // ── Remorque + Timbre ─────────────────────────────────────────────────
        builder.Property(r => r.TarifRemorque)
            .HasColumnName("tarif_remorque")
            .HasColumnType("jsonb")
            .HasConversion(TarifRemorqueConverter)
            .IsRequired();

        builder.Property(r => r.TimbreCNPAC)
            .HasColumnName("timbre_cnpac")
            .HasColumnType("numeric(10,2)")
            .HasConversion(TimbreConverter)
            .IsRequired();

        // ── Versioning ────────────────────────────────────────────────────────
        builder.Property(r => r.ValidFrom)
            .HasColumnName("valid_from")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(r => r.ValidTo)
            .HasColumnName("valid_to")
            .HasColumnType("date");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(r => r.ValidFrom)
            .HasDatabaseName("ix_referentiel_tarifaires_valid_from");

        builder.HasMany(r => r.Bareme)
            .WithOne()
            .HasForeignKey("referentiel_tarifaire_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BaremeRCConfiguration : IEntityTypeConfiguration<BaremeRC>
{
    private static readonly ValueConverter<PrimeHT, decimal> PrimeHTConverter = new(
        v => v.Montant,
        v => new PrimeHT(v));

    public void Configure(EntityTypeBuilder<BaremeRC> builder)
    {
        builder.ToTable("bareme_rc");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(b => b.Usage)
            .HasColumnName("usage")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.PuissanceMin)
            .HasColumnName("puissance_min")
            .IsRequired();

        builder.Property(b => b.PuissanceMax)
            .HasColumnName("puissance_max");

        builder.Property(b => b.PrimeHT)
            .HasColumnName("prime_ht")
            .HasColumnType("numeric(10,2)")
            .HasConversion(PrimeHTConverter)
            .IsRequired();

        // FK shadow property
        builder.Property<int>("referentiel_tarifaire_id")
            .HasColumnName("referentiel_tarifaire_id")
            .IsRequired();

        builder.HasIndex("referentiel_tarifaire_id", nameof(BaremeRC.Usage), nameof(BaremeRC.PuissanceMin))
            .HasDatabaseName("ix_bareme_rc_referentiel_usage_puissance")
            .IsUnique();
    }
}
