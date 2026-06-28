using CedarRisk.Application.Engines.Interfaces;
using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using CedarRisk.Domain.ReferentielTarifaires;

namespace CedarRisk.Application.Engines.Implementation;

/// <summary>
/// Agrège RC + GC + Remorque en un PremiumBreakdown final.
///
/// Fiscalité GC (Article 284 CGI) :
///   TotalTaxeGC      = TotalPrimeHT_GC  × TauxTaxeGC  (14%)
///   TotalCatNatTaxeGC= TotalCatNatHT_GC × TauxTaxeGC  (14%) — même taux
///   ParafiscaleGC    = (TotalPrimeHT_GC + TotalCatNatHT_GC) × TauxParafiscaleGC — UNE FOIS
///   Math.Ceiling sur PrimeTotalTTC — UNIQUEMENT ici via PremiumRoundingPolicy
/// </summary>
public sealed class PremiumAggregator : IPremiumAggregator
{
    public PremiumBreakdown Agreger(
        RcPremiumResult rc,
        PrimesGarantiesHT gc,
        RemorquePremiumResult? remorque,
        ReferentielTarifaire referentiel)
    {
        var tauxTaxeRC = referentiel.TauxTaxeRC.Value;
        var tauxCatNatRC = referentiel.TauxCatNatRC.Value;
        var tauxParafiscaleRC = referentiel.TauxParafiscaleRC.Value;
        var tauxTaxeGC = referentiel.TauxTaxeGC.Value;
        var tauxParafiscaleGC = referentiel.TauxParafiscaleGC.Value;

        var primeTTCRC = PrimeTTC.AssemblerRC(
            rc.PrimeRC, rc.TaxeRC, rc.CatNatRC,
            rc.CatNatTaxeRC, rc.ParafiscaleRC, rc.TimbreCNPAC);

        var totalTaxeGC = gc.TotalPrimeHT.AppliquerTaxeCA(tauxTaxeGC);
        var totalCatNatTaxeGC = gc.TotalCatNatHT.AppliquerTaxeCA(tauxTaxeGC); // 14% sur CatNat GC

        var parafiscaleGC = gc.TotalPrimeHT.AppliquerParafiscale(
            tauxParafiscaleGC, gc.TotalCatNatHT);

        var primeTTCGC = PrimeTTC.AssemblerGC(
            gc.TotalPrimeHT, totalTaxeGC,
            gc.TotalCatNatHT, totalCatNatTaxeGC, parafiscaleGC);

        PrimeHT? primeRemorque = null;
        CatNatHT? catNatRemorque = null;
        Parafiscale parafiscaleRemorque = Parafiscale.Zero;
        PrimeTTC primeTTCRemorque = PrimeTTC.Zero;

        if (remorque is not null)
        {
            primeRemorque = remorque.PrimeHT;
            catNatRemorque = remorque.CatNatHT;

            parafiscaleRemorque = remorque.PrimeHT.AppliquerParafiscale(
                tauxParafiscaleRC, remorque.CatNatHT);

            var taxeRemorque = remorque.PrimeHT.AppliquerTaxeCA(tauxTaxeRC);
            var catNatTaxeRemorque = remorque.CatNatHT.AppliquerTaxeCA(tauxCatNatRC);

            primeTTCRemorque = new PrimeTTC(
                Math.Round(
                    remorque.PrimeHT.Montant
                    + (taxeRemorque.Montant > 0 ? taxeRemorque.Montant : 0m)
                    + (remorque.CatNatHT.Montant > 0 ? remorque.CatNatHT.Montant : 0m)
                    + (catNatTaxeRemorque.Montant > 0 ? catNatTaxeRemorque.Montant : 0m)
                    + (parafiscaleRemorque.Montant > 0 ? parafiscaleRemorque.Montant : 0m),
                    2, MidpointRounding.AwayFromZero));
        }

        var totalBrut = primeTTCRC + primeTTCGC + primeTTCRemorque;
        var primeTotal = PremiumRoundingPolicy.ArrondiTotal(totalBrut);

        return new PremiumBreakdown(
            PrimeRC: rc.PrimeRC,
            CatNatRC: rc.CatNatRC,
            TaxeRC: rc.TaxeRC,
            CatNatTaxeRC: rc.CatNatTaxeRC,
            ParafiscaleRC: rc.ParafiscaleRC,
            TimbreCNPAC: rc.TimbreCNPAC,
            PrimeTTCRC: primeTTCRC,
            PrimesGC: gc,
            TotalTaxeGC: totalTaxeGC,
            TotalCatNatTaxeGC: totalCatNatTaxeGC,
            ParafiscaleGC: parafiscaleGC,
            PrimeTTCGC: primeTTCGC,
            PrimeRemorque: primeRemorque,
            CatNatRemorque: catNatRemorque,
            ParafiscaleRemorque: parafiscaleRemorque,
            PrimeTTCRemorque: primeTTCRemorque,
            PrimeTotalTTC: primeTotal);
    }
}