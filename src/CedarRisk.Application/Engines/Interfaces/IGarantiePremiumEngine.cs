using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;

namespace CedarRisk.Application.Engines.Interfaces;

public interface IGarantiePremiumEngine
{
    Task<Result<PrimeGarantieHT>> CalculerAsync(
        GarantiePremiumContexte contexte,
        CancellationToken ct = default);
}

public record GarantiePremiumContexte(
    string GarantieCode,
    BaseTarifaire BaseTarifaire,
    ProrataFactor ProrataFactor,
    DateOnly Today);
