using CedarRisk.Domain.Common;

namespace CedarRisk.Application.Engines.Interfaces;

public interface IEligibilityEngine
{
    Task<EligibilityResult> EvaluerAsync(EligibilityContext contexte, CancellationToken ct = default);
}

public record EligibilityContext(
    IReadOnlyList<string> CodesGaranties,
    int AgeVehicule,
    UsageVehicule Usage,
    decimal ValeurVenale,
    DateOnly Today);

public record EligibilityResult(
    IReadOnlyList<GarantieEligible> Eligibles,
    IReadOnlyList<GarantieIneligible> Ineligibles);

public record GarantieEligible(string Code);

public record GarantieIneligible(string Code, string Raison);