using CedarRisk.Domain.GarantieConditions;
using CedarRisk.Domain.Garanties;
using CedarRisk.Domain.GarantieTarifications;
using CedarRisk.Domain.ReferentielTarifaires;
using Microsoft.EntityFrameworkCore;

namespace CedarRisk.Infrastructure.Persistence;

public class CedarRiskDbContext : DbContext
{
    public CedarRiskDbContext(DbContextOptions<CedarRiskDbContext> options)
        : base(options) { }

    public DbSet<GarantieDefinition> Garanties => Set<GarantieDefinition>();
    public DbSet<GarantieCondition> GarantieConditions => Set<GarantieCondition>();
    public DbSet<GarantieTarification> GarantieTarifications => Set<GarantieTarification>();
    public DbSet<ReferentielTarifaire> ReferentielTarifaires => Set<ReferentielTarifaire>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CedarRiskDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added
                && entry.Metadata.FindProperty("CreatedAt") is not null)
                entry.Property("CreatedAt").CurrentValue = now;

            if (entry.State == EntityState.Modified
                && entry.Metadata.FindProperty("UpdatedAt") is not null)
                entry.Property("UpdatedAt").CurrentValue = now;
        }

        return await base.SaveChangesAsync(ct);
    }
}
