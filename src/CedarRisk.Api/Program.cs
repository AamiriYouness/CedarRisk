using CedarRisk.Api.Endpoints;
using CedarRisk.Application;
using CedarRisk.Infrastructure;
using CedarRisk.Infrastructure.Persistence;
using CedarRisk.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((doc, context, ct) =>
    {
        doc.Info = new()
        {
            Title = "CedarRisk API",
            Version = "v1",
            Description = "Moteur de tarification Assurance Auto Open-source."
        };
        return Task.CompletedTask;
    });
});

builder.Services.AddValidatorsFromAssemblyContaining<CalculerQuoteRequestValidator>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "CedarRisk API";
        options.Theme = ScalarTheme.DeepSpace;
    });

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CedarRiskDbContext>();
    await db.Database.MigrateAsync();
    await ReferentielTarifaireSeed.Seed2026T1Async(db);
    await GarantiesSeed.SeedAsync(db);
}

app.MapQuotesEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    Service     = "CedarRisk Premium Rating Engine",
    Version     = "1.0.0",
    Description = "Moteur de tarification Assurance Auto",
    Endpoints = new[]
    {
        "POST /quotes/calclate    — Calculer un devis",
        "GET  /scalar/v1          — Documentation interactive"
    }
}));

app.Run();

public partial class Program { }
