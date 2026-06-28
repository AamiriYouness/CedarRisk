using CedarRisk.Domain.Garanties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CedarRisk.Infrastructure.Persistence.Configurations;

public sealed class GarantieDefinitionConfiguration : IEntityTypeConfiguration<GarantieDefinition>
{
    public void Configure(EntityTypeBuilder<GarantieDefinition> builder)
    {
        builder.ToTable("garantie_definitions");

        builder.HasKey(g => g.Code);
        builder.Property(g => g.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(g => g.Libelle)
            .HasColumnName("libelle")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(g => g.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(g => g.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(g => g.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(g => g.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(g => g.IsActive)
            .HasDatabaseName("ix_garantie_definitions_is_active");
    }
}