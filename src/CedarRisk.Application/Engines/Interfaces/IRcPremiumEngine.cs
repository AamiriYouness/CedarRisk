using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires;
using CedarRisk.Domain.ValueObjects;
namespace CedarRisk.Application.Engines.Interfaces;

public interface IRcPremiumEngine
{
    Result<RcPremiumResult> Calculer(RcPremiumContexte contexte);
}

public record RcPremiumContexte(
    int PuissanceFiscale,
    UsageVehicule Usage,
    ProrataFactor ProrateFactor,
    CrmCoefficient CrmCoefficient,
    ReferentielTarifaire Referentiel);

public record RcPremiumResult(
    PrimeHT PrimeRC,
    CatNatHT CatNatRC,
    TaxeCA TaxeRC,
    TaxeCA CatNatTaxeRC,
    Parafiscale ParafiscaleRC,
    Timbre TimbreCNPAC);
