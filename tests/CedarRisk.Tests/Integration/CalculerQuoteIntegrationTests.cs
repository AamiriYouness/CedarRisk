using System.Net;
using System.Net.Http.Json;
using CedarRisk.Application.Quotes;
using CedarRisk.Infrastructure.Persistence;
using CedarRisk.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace CedarRisk.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class CalculerQuoteIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine3.23")
        .WithDatabase("cedarrisk_test")
        .WithUsername("cedar")
        .WithPassword("cedar")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host =>
            {
                host.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<CedarRiskDbContext>>();
                    services.RemoveAll<CedarRiskDbContext>();

                    services.AddDbContext<CedarRiskDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString()));
                });
            });

        // Migration + seed
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CedarRiskDbContext>();
        await db.Database.MigrateAsync();
        await ReferentielTarifaireSeed.Seed2026T1Async(db);
        await GarantiesSeed.SeedAsync(db);

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculerDevis_VehiculeTourismeRC_Seul_Retourne200AvecPrimeTTC()
    {
        var request = new
        {
            puissanceFiscale = 8,
            usage = "VehiculeTourisme",
            valeurVenale = 120000m,
            ageVehicule = 3,
            dateEffet = "2026-02-01",
            dateEcheance = "2027-02-01",
            crmCoefficient = 1.00m,
            nbrRemorque = 0,
            garantiesGC = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Breakdown.PrimeTotalTTC.ShouldBeGreaterThan(0);
        result.Breakdown.TimbreCNPAC.ShouldBe(10m); // Timbre CNPAC fixe
        result.Eligibilite.CodesEligibles.ShouldBeEmpty();
        result.Eligibilite.Ineligibles.ShouldBeEmpty();
    }

    [Fact]
    public async Task CalculerDevis_AvecVOL_EligibleRetournePrimeGC()
    {
        // VOL : Age <= 10, TauxDirectValeurVenale(3%), CatNat true
        // ageVehicule=3, valeurVenale=200_000 -> PrimeVOL_HT = 6_000 MAD
        var request = new
        {
            puissanceFiscale = 6,
            usage = "VehiculeTourisme",
            valeurVenale = 200000m,
            ageVehicule = 3,
            dateEffet = "2026-03-01",
            dateEcheance = "2027-03-01",
            crmCoefficient = 0.85m,
            nbrRemorque = 0,
            garantiesGC = new[]
            {
                new
                {
                    garantieCode = "VOL",
                    capitalClient = (decimal?)null,
                    capitalGarantieReference = (decimal?)null,
                    optionChoisie = (object?)null
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Eligibilite.CodesEligibles.ShouldContain("VOL");
        result.Breakdown.PrimesGC.ShouldHaveSingleItem();
        result.Breakdown.PrimesGC[0].GarantieCode.ShouldBe("VOL");
        result.Breakdown.PrimesGC[0].PrimeHT.ShouldBe(6000m);
        result.Breakdown.PrimesGC[0].CatNatHT.ShouldBe(72m);
        result.Breakdown.PrimeTotalTTC.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CalculerDevis_BRISRequiertVOLEtDOM_IneligibleSiManquant()
    {
        var request = new
        {
            puissanceFiscale = 8,
            usage = "VehiculeTourisme",
            valeurVenale = 150000m,
            ageVehicule = 4,
            dateEffet = "2026-03-01",
            dateEcheance = "2027-03-01",
            crmCoefficient = 1.00m,
            nbrRemorque = 0,
            garantiesGC = new[]
            {
                new { garantieCode = "BRIS", capitalClient = (decimal?)null,
                      capitalGarantieReference = (decimal?)null, optionChoisie = (object?)null }
            }
        };

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Eligibilite.CodesEligibles.ShouldNotContain("BRIS");
        result.Eligibilite.Ineligibles.ShouldContain(i => i.Code == "BRIS");
    }

    [Fact]
    public async Task CalculerDevis_DFEtPJIncompatibles_LesDeux_Ineligibles()
    {
        var request = new
        {
            puissanceFiscale = 8,
            usage = "VehiculeTourisme",
            valeurVenale = 150000m,
            ageVehicule = 4,
            dateEffet = "2026-03-01",
            dateEcheance = "2027-03-01",
            crmCoefficient = 1.00m,
            nbrRemorque = 0,
            garantiesGC = new[]
            {
                new { garantieCode = "DF", capitalClient = (decimal?)null,
                      capitalGarantieReference = (decimal?)null, optionChoisie = (object?)null },
                new { garantieCode = "PJ", capitalClient = (decimal?)null,
                      capitalGarantieReference = (decimal?)null, optionChoisie = (object?)null }
            }
        };

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        var eligibles = result.Eligibilite.CodesEligibles;
        var ineligibles = result.Eligibilite.Ineligibles.Select(i => i.Code).ToList();
        // Exactly one of DF/PJ eligible — bidirectional incompatibility
        result.Eligibilite.CodesEligibles.ShouldNotContain("DF");
        result.Eligibilite.CodesEligibles.ShouldNotContain("PJ");
        result.Eligibilite.Ineligibles.ShouldContain(i => i.Code == "DF");
        result.Eligibilite.Ineligibles.ShouldContain(i => i.Code == "PJ");
    }

    [Fact]
    public async Task CalculerDevis_RcConducteur_ExclusTaxi()
    {
        var request = new
        {
            puissanceFiscale = 8,
            usage = "Taxi",
            valeurVenale = 100000m,
            ageVehicule = 3,
            dateEffet = "2026-03-01",
            dateEcheance = "2027-03-01",
            crmCoefficient = 1.00m,
            nbrRemorque = 0,
            garantiesGC = new[]
            {
                new { garantieCode = "RC_CONDUCTEUR", capitalClient = (decimal?)80000m,
                      capitalGarantieReference = (decimal?)null, optionChoisie = (object?)null }
            }
        };

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Eligibilite.CodesEligibles.ShouldNotContain("RC_CONDUCTEUR");
        result.Eligibilite.Ineligibles.ShouldContain(i => i.Code == "RC_CONDUCTEUR");
    }

    [Fact]
    public async Task CalculerDevis_AvecRemorque_RetournePrimeRemorque()
    {
        var request = new
        {
            puissanceFiscale = 10,
            usage = "TransportMarchandises",
            valeurVenale = 80000m,
            ageVehicule = 5,
            dateEffet = "2026-04-01",
            dateEcheance = "2027-04-01",
            crmCoefficient = 1.25m,
            nbrRemorque = 1,
            garantiesGC = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Breakdown.PrimeRemorqueHT.ShouldNotBeNull();
        result.Breakdown.PrimeRemorqueHT!.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CalculerDevis_SansReferentielActif_Retourne404()
    {
        var request = new
        {
            puissanceFiscale = 8,
            usage = "VehiculeTourisme",
            valeurVenale = 100000m,
            ageVehicule = 3,
            dateEffet = "2010-01-01", // avant tout référentiel
            dateEcheance = "2011-01-01",
            crmCoefficient = 1.00m,
            nbrRemorque = 0,
            garantiesGC = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CalculerDevis_RequeteInvalide_Retourne400AvecDetails()
    {
        var request = new
        {
            puissanceFiscale = 0,       // invalide — min 1
            usage = "UsageInconnu",     // invalide
            valeurVenale = -100m,       // invalide
            ageVehicule = -1,           // invalide
            dateEffet = "2026-06-01",
            dateEcheance = "2026-05-01", // antérieure à dateEffet
            crmCoefficient = 5.00m,     // hors [0.50, 3.50]
            nbrRemorque = 5,            // hors [0, 2]
            garantiesGC = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/quotes/calculate", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CalculerDevis_Prorata_PrimeTTC_CoherenceAvecAnneeComplete()
    {
        // Pleine année → prorata = 1 → PrimeTTC identique à année complète
        var requestAnneePleine = new
        {
            puissanceFiscale = 8,
            usage = "VehiculeTourisme",
            valeurVenale = 120000m,
            ageVehicule = 3,
            dateEffet = "2026-01-01",
            dateEcheance = "2027-01-01",
            crmCoefficient = 1.00m,
            nbrRemorque = 0,
            garantiesGC = Array.Empty<object>()
        };

        var requestDemiAnnee = requestAnneePleine with
        {
            dateEcheance = "2026-07-01"
        };

        var r1 = await _client.PostAsJsonAsync("/quotes/calculate", requestAnneePleine, TestContext.Current.CancellationToken);
        var r2 = await _client.PostAsJsonAsync("/quotes/calculate", requestDemiAnnee, TestContext.Current.CancellationToken);

        r1.StatusCode.ShouldBe(HttpStatusCode.OK);
        r2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var full = (await r1.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken))!;
        var half = (await r2.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken))!;

        half.Breakdown.PrimeTotalTTC.ShouldBeLessThan(full.Breakdown.PrimeTotalTTC);
    }
}
