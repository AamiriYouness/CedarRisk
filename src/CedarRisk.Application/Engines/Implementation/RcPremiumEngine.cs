using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.Common;

namespace CedarRisk.Application.Engines.Implementation;

/// <summary>
/// Chaîne de calcul RC :
///   PrimeRC_HT   = Barème(puissance, usage) × CRM × Prorata
///   CatNatRC_HT  = PrimeRC_HT  × TauxCatNatRC  (3.5%)
///   TaxeRC       = PrimeRC_HT  × TauxTaxeRC     (14%)
///   CatNatTaxeRC = CatNatRC_HT × TauxTaxeRC     (14%) — même taux, Article 284 CGI
///   ParafiscaleRC= (PrimeRC_HT + CatNatRC_HT) × TauxParafiscaleRC (1%)
///   TimbreCNPAC  = montant fixe
/// </summary>
public sealed class RcPremiumEngine : IRcPremiumEngine
{
    public Result<RcPremiumResult> Calculer(RcPremiumContexte contexte)
    {
        var primeBaseResult = contexte.Referentiel.TrouverPrimeBase(
            contexte.Usage, contexte.PuissanceFiscale);

        if (primeBaseResult.IsFailure)
            return Result<RcPremiumResult>.Failure(primeBaseResult.Error);

        var primeRC = primeBaseResult.Value
            * contexte.CrmCoefficient.Value
            * contexte.ProrateFactor.Value;

        var tauxCatNat = contexte.Referentiel.TauxCatNatRC.Value;  // 3.5%
        var tauxTaxe = contexte.Referentiel.TauxTaxeRC.Value;    // 14%
        var tauxParafiscale = contexte.Referentiel.TauxParafiscaleRC.Value;

        var catNatRC = primeRC.AppliquerCatNat(tauxCatNat);
        var taxeRC = primeRC.AppliquerTaxeCA(tauxTaxe);
        var catNatTaxeRC = catNatRC.AppliquerTaxeCA(tauxTaxe);         // 14% sur CatNat
        var parafiscaleRC = primeRC.AppliquerParafiscale(tauxParafiscale, catNatRC);

        return Result<RcPremiumResult>.Success(new RcPremiumResult(
            PrimeRC: primeRC,
            CatNatRC: catNatRC,
            TaxeRC: taxeRC,
            CatNatTaxeRC: catNatTaxeRC,
            ParafiscaleRC: parafiscaleRC,
            TimbreCNPAC: contexte.Referentiel.TimbreCNPAC));
    }
}
