using CedarRisk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CedarRisk.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CedarRiskDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("CedarRisk"),
                npgsql => npgsql.MigrationsAssembly("CedarRisk.Infrastructure")));
        return services;
    }
}
