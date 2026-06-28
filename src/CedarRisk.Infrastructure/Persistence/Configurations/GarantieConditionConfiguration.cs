using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieConditions;
using CedarRisk.Domain.GarantieConditions.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace CedarRisk.Infrastructure.Persistence.Configurations;

public sealed class GarantieConditionConfiguration : IEntityTypeConfiguration<GarantieCondition>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public void Configure(EntityTypeBuilder<GarantieCondition> builder)
    {
        builder.ToTable("garantie_conditions");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id");

        builder.Property(c => c.GarantieCode)
            .HasColumnName("garantie_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.AgeLimiteVehicule)
            .HasColumnName("age_limite_vehicule");

        // UsagesExclus — backing field, stored as jsonb array of enum string names
        builder.Property<List<UsageVehicule>>("_usagesExclus")
            .HasColumnName("usages_exclus")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Select(u => u.ToString()).ToList(), JsonOpts),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOpts)!
                    .Select(s => Enum.Parse<UsageVehicule>(s))
                    .ToList());

        // ExigenceConjonctive — jsonb array of codes
        builder.Property(c => c.ExigenceConjonctive)
            .HasColumnName("exigence_conjonctive")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Codes, JsonOpts),
                v => ExigenceConjonctive.Hydrate(
                    JsonSerializer.Deserialize<List<string>>(v, JsonOpts)!));

        // ExigenceDisjonctive — jsonb array of codes
        builder.Property(c => c.ExigenceDisjonctive)
            .HasColumnName("exigence_disjonctive")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Codes, JsonOpts),
                v => ExigenceDisjonctive.Hydrate(
                    JsonSerializer.Deserialize<List<string>>(v, JsonOpts)!));

        // IncompatibilitesGarantie — jsonb array of codes
        builder.Property(c => c.Incompatibilites)
            .HasColumnName("incompatibilites")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Codes, JsonOpts),
                v => IncompatibilitesGarantie.Hydrate(
                    JsonSerializer.Deserialize<List<string>>(v, JsonOpts)!));

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at");

        // Une seule condition active par garantie à tout moment
        builder.HasIndex(c => c.GarantieCode)
            .HasDatabaseName("uix_garantie_conditions_code_active")
            .HasFilter("is_active = true")
            .IsUnique();

        builder.HasIndex(c => new { c.GarantieCode, c.IsActive })
            .HasDatabaseName("ix_garantie_conditions_code_active");
    }
}