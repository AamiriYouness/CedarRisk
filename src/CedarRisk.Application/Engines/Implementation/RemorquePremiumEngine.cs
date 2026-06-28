using CedarRisk.Application.Engines.Errors;
using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.Common;

namespace CedarRisk.Application.Engines.Implementation;

/// <summary>
/// Moteur de calcul de la prime remorque — moteur de rating pur.
///
/// Reçoit NbrRemorque comme input de confiance depuis le système appelant.
/// La limite contractuelle (ex: max 1 remorque par VP) est gérée en amont,
/// pas dans ce bounded context.
///
/// Délègue le calcul à TarifRemorque — taux sur RC ou montant fixe par remorque.
/// Parafiscale et TaxeCA absentes — agrégateur cumule (RC + Remorque) avant calcul unique.
/// </summary>
public sealed class RemorquePremiumEngine : IRemorquePremiumEngine
{
    public Result<RemorquePremiumResult> Calculer(RemorquePremiumContexte contexte)
    {
        if (contexte.NbrRemorque < 1)
            return Result<RemorquePremiumResult>.Failure(
                new NombreRemorquesInvalideError(contexte.NbrRemorque));

        PrimeHT primeHT = contexte.Referentiel.TarifRemorque
            .CalculerPrime(contexte.PrimeHT, contexte.NbrRemorque, contexte.ProrataFactor);

        CatNatHT catNatHT = primeHT.AppliquerCatNat(
            contexte.Referentiel.TauxCatNatRC.Value);

        return Result<RemorquePremiumResult>.Success(
            new RemorquePremiumResult(primeHT, catNatHT));
    }
}