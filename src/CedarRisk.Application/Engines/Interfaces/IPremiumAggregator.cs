using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using CedarRisk.Domain.ReferentielTarifaires;
using CedarRisk.Domain.ValueObjects;

namespace CedarRisk.Application.Engines.Interfaces;

public interface IPremiumAggregator
{
    PremiumBreakdown Agreger(
        RcPremiumResult rc,
        PrimesGarantiesHT gc,
        RemorquePremiumResult? remorque,
        ReferentielTarifaire referentiel);
}

/// <summary>
/// Taux nécessaires à l'agrégateur — extraits du ReferentielTarifaire et des GarantieTarifications.
/// Évite de passer le ReferentielTarifaire entier dans l'agrégateur.
/// </summary>
public record AgregateurTaux(
    TariffRate TauxParafiscaleRC,
    TariffRate TauxTaxeRC,
    TariffRate TauxParafiscaleGC,
    TariffRate TauxTaxeGC);

/// <summary>
/// Ventilation complète de la prime — toutes pistes.
/// PrimeTotal_TTC = Math.Ceiling(RC + GC + Remorque).
/// </summary>
public record PremiumBreakdown(
    PrimeHT PrimeRC,
    CatNatHT CatNatRC,
    TaxeCA TaxeRC,
    TaxeCA CatNatTaxeRC,
    Parafiscale ParafiscaleRC,
    Timbre TimbreCNPAC,
    PrimeTTC PrimeTTCRC,
    PrimesGarantiesHT PrimesGC,
    TaxeCA TotalTaxeGC,
    TaxeCA TotalCatNatTaxeGC,
    Parafiscale ParafiscaleGC,
    PrimeTTC PrimeTTCGC,
    PrimeHT? PrimeRemorque,
    CatNatHT? CatNatRemorque,
    Parafiscale ParafiscaleRemorque,
    PrimeTTC PrimeTTCRemorque,
    PrimeTTC PrimeTotalTTC);
