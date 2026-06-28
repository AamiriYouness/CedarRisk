namespace CedarRisk.Domain.Common;

/// <summary>
/// Autorité unique d'arrondi.
///
/// Intermédiaires  → Math.Round(value, 2, MidpointRounding.AwayFromZero)
///                   Appliqué par les value objects eux-mêmes (PrimeHT, CatNatHT, etc.)
///
/// PrimeTotal_TTC  → Math.Ceiling(value)
///                   Appliqué UNIQUEMENT ici, UNIQUEMENT sur le total final.
///                   Jamais sur les intermédiaires.
/// </summary>
public static class PremiumRoundingPolicy
{
    public static PrimeTTC ArrondiTotal(PrimeTTC total) =>
        new(Math.Ceiling(total.Montant));
}