using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;

namespace CedarRisk.Application.Engines.Implementation;

/// <summary>
/// Orchestrateur principal du calcul de devis.
///
/// Pipeline :
///   1. Éligibilité    — EligibilityEngine
///   2. Prime RC       — RcPremiumEngine     (obligatoire)
///   3. Prime GC       — GarantiePremiumEngine × garantie éligible
///   4. Prime Remorque — RemorquePremiumEngine (si NbrRemorque > 0)
///   5. Agrégation     — PremiumAggregator   (parafiscale une fois, Math.Ceiling)
///
/// </summary>
public sealed class QuoteEngine : IQuoteEngine
{
    private readonly IEligibilityEngine _eligibilityEngine;
    private readonly IRcPremiumEngine _rcEngine;
    private readonly IGarantiePremiumEngine _gcEngine;
    private readonly IRemorquePremiumEngine _remorqueEngine;
    private readonly IPremiumAggregator _aggregator;

    public QuoteEngine(
        IEligibilityEngine eligibilityEngine,
        IRcPremiumEngine rcEngine,
        IGarantiePremiumEngine gcEngine,
        IRemorquePremiumEngine remorqueEngine,
        IPremiumAggregator aggregator)
    {
        _eligibilityEngine = eligibilityEngine;
        _rcEngine = rcEngine;
        _gcEngine = gcEngine;
        _remorqueEngine = remorqueEngine;
        _aggregator = aggregator;
    }

    public async Task<Result<QuoteResult>> CalculerAsync(
        QuoteContexte contexte,
        CancellationToken ct = default)
    {
        var prorataResult = ProrataFactor.Calculer(contexte.DateEffet, contexte.DateEcheance);
        if (prorataResult.IsFailure)
            return Result<QuoteResult>.Failure(prorataResult.Error);

        var prorata = prorataResult.Value;

        var eligibilityContexte = new EligibilityContext(
            contexte.GarantiesGC.Select(g => g.GarantieCode).ToList(),
            contexte.AgeVehicule,
            contexte.Usage,
            contexte.ValeurVenale,
            contexte.Today);

        var eligibilite = await _eligibilityEngine.EvaluerAsync(eligibilityContexte, ct);

        // ── Étape 2 — Prime RC ────────────────────────────────────────────────
        var rcContexte = new RcPremiumContexte(
            contexte.PuissanceFiscale,
            contexte.Usage,
            prorata,
            contexte.CrmCoefficient,
            contexte.Referentiel);

        var rcResult = _rcEngine.Calculer(rcContexte);
        if (rcResult.IsFailure)
            return Result<QuoteResult>.Failure(rcResult.Error);

        // ── Étape 3 — Prime GC par garantie éligible ─────────────────────────
        var primesGC = PrimesGarantiesHT.Vide;

        var codesEligibles = eligibilite.Eligibles
            .Select(e => e.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var garantie in contexte.GarantiesGC.Where(g => codesEligibles.Contains(g.GarantieCode)))
        {
            var baseTarifaire = new BaseTarifaire(
                contexte.ValeurVenale,
                garantie.CapitalClient,
                garantie.CapitalGarantieReference,
                garantie.OptionChoisie);

            var gcContexte = new GarantiePremiumContexte(
                garantie.GarantieCode,
                baseTarifaire,
                prorata,
                contexte.Today);

            var gcResult = await _gcEngine.CalculerAsync(gcContexte, ct);
            if (gcResult.IsFailure)
                return Result<QuoteResult>.Failure(gcResult.Error);

            primesGC = primesGC + gcResult.Value;
        }

        // ── Étape 4 — Prime Remorque ──────────────────────────────────────────
        RemorquePremiumResult? remorqueResult = null;

        if (contexte.NbrRemorque > 0)
        {
            var remorqueContexte = new RemorquePremiumContexte(
                rcResult.Value.PrimeRC,
                contexte.NbrRemorque,
                prorata,
                contexte.Referentiel);

            var remorque = _remorqueEngine.Calculer(remorqueContexte);
            if (remorque.IsFailure)
                return Result<QuoteResult>.Failure(remorque.Error);

            remorqueResult = remorque.Value;
        }

        // ── Étape 5 — Agrégation ──────────────────────────────────────────────
        var breakdown = _aggregator.Agreger(
            rcResult.Value,
            primesGC,
            remorqueResult,
            contexte.Referentiel);

        return Result<QuoteResult>.Success(
            new QuoteResult(breakdown, eligibilite));
    }
}
