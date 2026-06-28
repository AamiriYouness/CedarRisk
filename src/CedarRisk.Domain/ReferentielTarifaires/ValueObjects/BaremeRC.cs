using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires.Errors;
namespace CedarRisk.Domain.ReferentielTarifaires.ValueObjects;

public class BaremeRC
{
    private BaremeRC() { }

    private BaremeRC(
        UsageVehicule usage,
        int puissanceMin,
        int? puissanceMax,
        PrimeHT primeHT)
    {
        Usage = usage;
        PuissanceMin = puissanceMin;
        PuissanceMax = puissanceMax;
        PrimeHT = primeHT;
    }

    public int Id { get; private set; }
    public int ReferentielTarifaireId { get; private set; }
    public UsageVehicule Usage { get; private set; }
    public int PuissanceMin { get; private set; }
    public int? PuissanceMax { get; private set; }  // null = unbounded
    public PrimeHT PrimeHT { get; private set; } = default!;

    public static Result<BaremeRC> Create(
        UsageVehicule usage,
        int puissanceMin,
        int? puissanceMax,
        PrimeHT primeHT)
    {
        if (puissanceMin < 1)
            return Result<BaremeRC>.Failure(new BaremeRCInvalideError("PuissanceMin doit être >= 1."));

        if (puissanceMax.HasValue && puissanceMax.Value < puissanceMin)
            return Result<BaremeRC>.Failure(new BaremeRCInvalideError("PuissanceMax doit être >= PuissanceMin."));

        if (primeHT.Montant < 0)
            return Result<BaremeRC>.Failure(new BaremeRCInvalideError("PrimeHT ne peut pas être négative."));

        return Result<BaremeRC>.Success(new BaremeRC(usage, puissanceMin, puissanceMax, primeHT));
    }

    /// <summary>Hydratation EF Core — contourne la validation</summary>
    public static BaremeRC Hydrate(
        UsageVehicule usage,
        int puissanceMin,
        int? puissanceMax,
        PrimeHT primeHT) =>
        new(usage, puissanceMin, puissanceMax, primeHT);

    public bool CorrespondA(UsageVehicule usage, int puissanceFiscale) =>
        Usage == usage &&
        puissanceFiscale >= PuissanceMin &&
        (PuissanceMax == null || puissanceFiscale <= PuissanceMax);
}
