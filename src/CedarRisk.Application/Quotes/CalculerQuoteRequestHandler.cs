using CedarRisk.Application.Common;
using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using CedarRisk.Domain.ValueObjects;
using CedarRisk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CedarRisk.Application.Quotes;


/// <summary>
/// Requête de calcul de devis — données brutes client.
/// </summary>
public sealed record CalculerQuoteQuery(
    int PuissanceFiscale,
    UsageVehicule Usage,
    decimal ValeurVenale,
    int AgeVehicule,
    DateOnly DateEffet,
    DateOnly DateEcheance,
    decimal CrmCoefficient,
    int NbrRemorque,
    IReadOnlyList<GarantieContexteDto> GarantiesGC);

public sealed record GarantieContexteDto(
    string GarantieCode,
    decimal? CapitalClient,
    decimal? CapitalGarantieReference,
    CapitalOptionDto? OptionChoisie);

/// <summary>
/// CapitalOption
/// </summary>
public sealed record CapitalOptionDto(decimal Capital, decimal MontantHT);

public class CalculerQuoteRequestHandler(CedarRiskDbContext db, IQuoteEngine quoteEngine)
        : IRequestHandler<CalculerQuoteQuery, QuoteResponse>
{
    private readonly CedarRiskDbContext _db = db;
    private readonly IQuoteEngine _quoteEngine = quoteEngine;

    public async Task<Result<QuoteResponse>> HandleAsync(CalculerQuoteQuery query, CancellationToken ct = default)
    {
        var referentiel = await _db.ReferentielTarifaires
            .Include(r => r.Bareme)
            .Where(r =>
                r.ValidFrom <= query.DateEffet &&
                (r.ValidTo == null || r.ValidTo >= query.DateEffet))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync(ct);

        if (referentiel is null)
            return Result<QuoteResponse>.Failure(
                new NotFoundError(
                    "REFERENTIEL_INTROUVABLE",
                    $"Aucun référentiel tarifaire actif à la date d'effet {query.DateEffet:yyyy-MM-dd}."));

        var crmResult = CrmCoefficient.Of(query.CrmCoefficient);
        if (crmResult.IsFailure)
            return Result<QuoteResponse>.Failure(crmResult.Error);

        var garantiesGC = query.GarantiesGC
            .Select(g => new GarantieContexte(
                g.GarantieCode,
                g.CapitalClient,
                g.CapitalGarantieReference,
                g.OptionChoisie is null
                    ? null
                    : new CapitalOption(g.OptionChoisie.Capital, g.OptionChoisie.MontantHT)))
            .ToList();

        var contexte = new QuoteContexte(
            query.PuissanceFiscale,
            query.Usage,
            query.ValeurVenale,
            query.AgeVehicule,
            query.DateEffet,
            query.DateEcheance,
            crmResult,
            query.NbrRemorque,
            garantiesGC,
            referentiel,
            DateOnly.FromDateTime(DateTime.UtcNow.Date));

        var quoteResult = await _quoteEngine.CalculerAsync(contexte, ct);
        if (quoteResult.IsFailure)
            return Result<QuoteResponse>.Failure(quoteResult.Error);

        return Result<QuoteResponse>.Success(Map(quoteResult.Value));
    }
    private static QuoteResponse Map(QuoteResult result)
    {
        var breakDown = result.Breakdown;

        var primesGCDto = breakDown.PrimesGC.Lignes
            .Select(l => new PrimeGarantieDto(
                l.GarantieCode,
                l.Prime.Montant,
                l.CatNat.Montant))
            .ToList();

        var breakdown = new BreakdownDto(
            PrimeRCHT: breakDown.PrimeRC.Montant,
            CatNatRCHT: breakDown.CatNatRC.Montant,
            TaxeRC: breakDown.TaxeRC.Montant,
            CatNatTaxeRC: breakDown.CatNatTaxeRC.Montant,
            ParafiscaleRC: breakDown.ParafiscaleRC.Montant,
            TimbreCNPAC: breakDown.TimbreCNPAC.Montant,
            PrimeTTCRC: breakDown.PrimeTTCRC.Montant,

            PrimesGC: primesGCDto,
            TotalPrimeGCHT: breakDown.PrimesGC.TotalPrimeHT.Montant,
            TotalCatNatGCHT: breakDown.PrimesGC.TotalCatNatHT.Montant,
            TotalTaxeGC: breakDown.TotalTaxeGC.Montant,
            TotalCatNatTaxeGC: breakDown.TotalCatNatTaxeGC.Montant,
            ParafiscaleGC: breakDown.ParafiscaleGC.Montant,
            PrimeTTCGC: breakDown.PrimeTTCGC.Montant,

            PrimeRemorqueHT: breakDown.PrimeRemorque?.Montant,
            CatNatRemorqueHT: breakDown.CatNatRemorque?.Montant,
            ParafiscaleRemorque: breakDown.ParafiscaleRemorque.Montant,
            PrimeTTCRemorque: breakDown.PrimeTTCRemorque.Montant,

            PrimeTotalTTC: breakDown.PrimeTotalTTC.Montant);

        var eligibilite = new EligibiliteDto(
            CodesEligibles: result.Eligibilite.Eligibles
                .Select(e => e.Code)
                .ToList(),
            Ineligibles: result.Eligibilite.Ineligibles
                .Select(i => new GarantieIneligibleDto(i.Code, i.Raison))
                .ToList());

        return new QuoteResponse(breakdown, eligibilite);
    }

}

/// <summary>
/// Réponse du calcul de devis — tous les montants en decimal.
/// Arrondi déjà appliqué par PremiumAggregator/PremiumRoundingPolicy.
/// </summary>
public sealed record QuoteResponse(
    BreakdownDto Breakdown,
    EligibiliteDto Eligibilite);

public sealed record BreakdownDto(
    decimal PrimeRCHT,
    decimal CatNatRCHT,
    decimal TaxeRC,
    decimal CatNatTaxeRC,
    decimal ParafiscaleRC,
    decimal TimbreCNPAC,
    decimal PrimeTTCRC,

    IReadOnlyList<PrimeGarantieDto> PrimesGC,
    decimal TotalPrimeGCHT,
    decimal TotalCatNatGCHT,
    decimal TotalTaxeGC,
    decimal TotalCatNatTaxeGC,
    decimal ParafiscaleGC,
    decimal PrimeTTCGC,

    decimal? PrimeRemorqueHT,
    decimal? CatNatRemorqueHT,
    decimal ParafiscaleRemorque,
    decimal PrimeTTCRemorque,

    decimal PrimeTotalTTC);

public sealed record PrimeGarantieDto(
    string GarantieCode,
    decimal PrimeHT,
    decimal CatNatHT);

public sealed record EligibiliteDto(
    IReadOnlyList<string> CodesEligibles,
    IReadOnlyList<GarantieIneligibleDto> Ineligibles);

public sealed record GarantieIneligibleDto(string Code, string Raison);
