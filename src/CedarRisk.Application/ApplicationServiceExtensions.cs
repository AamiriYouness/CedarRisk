
using CedarRisk.Application.Common;
using CedarRisk.Application.Engines.Implementation;
using CedarRisk.Application.Quotes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace CedarRisk.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Scan(scan => scan
            .FromAssemblyOf<CalculerQuoteRequestHandler>()
            .AddClasses(classes => classes.Where(t =>
                t.Namespace?.StartsWith("CedarRisk.Application.Engines.Implementation") == true &&
                (t.Name.EndsWith("Engine") || t.Name.EndsWith("Aggregator"))))
            .AsImplementedInterfaces()
            .WithScopedLifetime());


        services.AddScoped<
            IRequestHandler<CalculerQuoteQuery, QuoteResponse>,
            CalculerQuoteRequestHandler>();

        return services;
    }
}
