using CedarRisk.Api.Extensions;
using CedarRisk.Application.Common;
using CedarRisk.Application.Quotes;
using CedarRisk.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CedarRisk.Api.Endpoints;

public static class QuotesEndpoints
{
    public static IEndpointRouteBuilder MapQuotesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/quotes")
            .WithTags("Devis");

        group.MapPost("/calculate", CalculerAsync)
            .WithName("CalculerDevis")
            .WithSummary("Calcule la prime TTC complète pour un devis auto.")
            .WithDescription("""
                Calcule la prime RC + GC éligibles + Remorque sur la base du référentiel
                tarifaire actif à la date d'effet. Aucun devis n'est persisté.
                """)
            .Produces<QuoteResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> CalculerAsync(
        [FromBody] CalculerQuoteRequest request,
        [FromServices] IValidator<CalculerQuoteRequest> validator,
        [FromServices] IRequestHandler<CalculerQuoteQuery, QuoteResponse> handler,
        CancellationToken ct)
    {
        // ── Validation structurelle (frontière API) ────────────────────────────
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            return TypedResults.ValidationProblem(errors);
        }

        // ── Mapping request → query ────────────────────────────────────────────
        // Usage parsé ici — structurellement valide grâce au validator
        var usage = Enum.Parse<UsageVehicule>(request.Usage, ignoreCase: true);

        var query = new CalculerQuoteQuery(
            request.PuissanceFiscale,
            usage,
            request.ValeurVenale,
            request.AgeVehicule,
            request.DateEffet,
            request.DateEcheance,
            request.CrmCoefficient,
            request.NbrRemorque,
            request.GarantiesGC
                .Select(g => new GarantieContexteDto(
                    g.GarantieCode,
                    g.CapitalClient,
                    g.CapitalGarantieReference,
                    g.OptionChoisie is null
                        ? null
                        : new CapitalOptionDto(g.OptionChoisie.Capital, g.OptionChoisie.MontantHT)))
                .ToList());

        var result = await handler.HandleAsync(query, ct);
        return result.ToHttpResult();
    }
}

/// <summary>
/// Contrat HTTP — séparé du query pour permettre une validation structurelle
/// indépendante des types domaine (pas d'enum UsageVehicule exposé en JSON brut).
/// </summary>
public sealed record CalculerQuoteRequest(
    int PuissanceFiscale,
    string Usage,
    decimal ValeurVenale,
    int AgeVehicule,
    DateOnly DateEffet,
    DateOnly DateEcheance,
    decimal CrmCoefficient,
    int NbrRemorque,
    IReadOnlyList<GarantieContexteRequest> GarantiesGC);

public sealed record GarantieContexteRequest(
    string GarantieCode,
    decimal? CapitalClient,
    decimal? CapitalGarantieReference,
    CapitalOptionRequest? OptionChoisie);

public sealed record CapitalOptionRequest(decimal Capital, decimal MontantHT);

public sealed class CalculerQuoteRequestValidator : AbstractValidator<CalculerQuoteRequest>
{
    private static readonly string[] UsagesValides = Enum.GetNames<UsageVehicule>();

    public CalculerQuoteRequestValidator()
    {
        RuleFor(x => x.PuissanceFiscale)
            .InclusiveBetween(1, 40)
            .WithMessage("La puissance fiscale doit être comprise entre 1 et 40 CV.");

        RuleFor(x => x.Usage)
            .NotEmpty()
            .Must(u => UsagesValides.Contains(u, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Usage invalide. Valeurs acceptées : {string.Join(", ", UsagesValides)}.");

        RuleFor(x => x.ValeurVenale)
            .GreaterThan(0)
            .WithMessage("La valeur vénale doit être strictement positive.");

        RuleFor(x => x.AgeVehicule)
            .GreaterThanOrEqualTo(0)
            .WithMessage("L'âge du véhicule ne peut pas être négatif.");

        RuleFor(x => x.DateEcheance)
            .GreaterThan(x => x.DateEffet)
            .WithMessage("La date d'échéance doit être postérieure à la date d'effet.");

        RuleFor(x => x.CrmCoefficient)
            .InclusiveBetween(0.50m, 3.50m)
            .WithMessage("Le coefficient CRM doit être compris entre 0,50 et 3,50.");

        RuleFor(x => x.NbrRemorque)
            .InclusiveBetween(0, 2)
            .WithMessage("Le nombre de remorques doit être compris entre 0 et 2.");

        RuleForEach(x => x.GarantiesGC).ChildRules(g =>
        {
            g.RuleFor(x => x.GarantieCode)
                .NotEmpty()
                .WithMessage("Le code garantie est obligatoire.");

            g.RuleFor(x => x.CapitalClient)
                .GreaterThan(0)
                .When(x => x.CapitalClient.HasValue)
                .WithMessage("Le capital client doit être strictement positif.");

            g.RuleFor(x => x.OptionChoisie)
                .Must(o => o!.Capital > 0 && o.MontantHT > 0)
                .When(x => x.OptionChoisie is not null)
                .WithMessage("L'option choisie doit avoir un capital et un montant HT strictement positifs.");
        });
    }
}
