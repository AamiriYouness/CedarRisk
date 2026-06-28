using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using CedarRisk.Domain.ReferentielTarifaires;
using CedarRisk.Domain.ValueObjects;

namespace CedarRisk.Application.Engines.Interfaces;

public interface IQuoteEngine
{
    Task<Result<QuoteResult>> CalculerAsync(QuoteContexte contexte, CancellationToken ct = default);
}

/// <summary>
/// Contexte complet pour le calcul d'un devis.
/// ReferentielTarifaire chargé en amont par le handler — QuoteEngine ne touche pas la DB directement.
/// </summary>
public record QuoteContexte(
    int PuissanceFiscale,
    UsageVehicule Usage,
    decimal ValeurVenale,
    int AgeVehicule,
    DateOnly DateEffet,
    DateOnly DateEcheance,
    CrmCoefficient CrmCoefficient,
    int NbrRemorque,
    IReadOnlyList<GarantieContexte> GarantiesGC,
    ReferentielTarifaire Referentiel,
    DateOnly Today);

/// <summary>
/// Contexte par garantie complémentaire — données client pour le calcul.
/// </summary>
public record GarantieContexte(
    string GarantieCode,
    decimal? CapitalClient,
    decimal? CapitalGarantieReference,
    CapitalOption? OptionChoisie);

/// <summary>
/// Résultat complet du devis — breakdown fiscal + éligibilité.
/// </summary>
public record QuoteResult(
    PremiumBreakdown Breakdown,
    EligibilityResult Eligibilite);