using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires;

namespace CedarRisk.Application.Engines.Interfaces;

public interface IRemorquePremiumEngine
{
    Result<RemorquePremiumResult> Calculer(RemorquePremiumContexte contexte);
}

public record RemorquePremiumContexte(
    PrimeHT PrimeHT,        // PrimeHT RC après CRM + prorata — base du calcul remorque
    int NbrRemorque,        // max 2
    ProrataFactor ProrataFactor,
    ReferentielTarifaire Referentiel);

public record RemorquePremiumResult(
    PrimeHT PrimeHT,        // PrimeRC × TauxRemorque × NbrRemorque
    CatNatHT CatNatHT);     // PrimeRemorque × TauxCatNatRC
                            // Parafiscale NON incluse — agrégateur cumule RC + Remorque avant calcul