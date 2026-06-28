using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.Errors;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using CedarRisk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CedarRisk.Application.Engines.Implementation;

public sealed class GarantiePremiumEngine(CedarRiskDbContext db) : IGarantiePremiumEngine
{
    private readonly CedarRiskDbContext _db = db;

    public async Task<Result<PrimeGarantieHT>> CalculerAsync(GarantiePremiumContexte contexte, CancellationToken ct = default)
    {
        var tarification = await _db.GarantieTarifications
            .AsNoTracking()
            .Where(t =>
                t.GarantieCode == contexte.GarantieCode &&
                t.ValidFrom <= contexte.Today &&
                (t.ValidTo == null || t.ValidTo >= contexte.Today))
            .FirstOrDefaultAsync(ct);

        if (tarification is null)
            return Result<PrimeGarantieHT>.Failure(
                new TarificationIntrouvableError(contexte.GarantieCode, contexte.Today));

        var primeResult = tarification.ModeTarifaire.CalculerPrime(
            contexte.GarantieCode,
            contexte.BaseTarifaire);

        if (primeResult.IsFailure)
            return Result<PrimeGarantieHT>.Failure(primeResult.Error);

        var primeHT = primeResult.Value * contexte.ProrataFactor.Value;

        var catNatHT = tarification.EstSoumisACatNat
            ? primeHT.AppliquerCatNat(tarification.TauxCatNatGC.Value)
            : CatNatHT.Zero;

        return Result<PrimeGarantieHT>.Success(
            new PrimeGarantieHT(
                contexte.GarantieCode,
                primeHT,
                catNatHT
            ));
    }
}
