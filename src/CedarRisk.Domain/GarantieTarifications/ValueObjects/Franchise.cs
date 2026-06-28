using CedarRisk.Domain.Common;

namespace CedarRisk.Domain.GarantieTarifications.ValueObjects;

/// <summary>
/// Franchise contractuelle — ne modifie JAMAIS la PrimeHT.
/// Utilisée pour le calcul du seuil de sinistre et figurant sur les Conditions Particulières.
/// MontantFranchise = max(Capital × TauxFranchise, MontantMinimum ?? 0)
/// </summary>
public sealed record Franchise
{
    public decimal TauxFranchise { get; }
    public decimal? MontantMinimum { get; }

    private Franchise(decimal tauxFranchise, decimal? montantMinimum)
    {
        TauxFranchise = tauxFranchise;
        MontantMinimum = montantMinimum;
    }

    public static Franchise Create(decimal tauxFranchise, decimal? montantMinimum = null)
    {
        return new(tauxFranchise, montantMinimum);
    }

    /// <summary>EF Core hydration.</summary>
    public static Franchise Hydrate(decimal tauxFranchise, decimal? montantMinimum) =>
        new(tauxFranchise, montantMinimum);

    public decimal CalculerMontant(decimal capital) =>
        Math.Max(
            Math.Round(capital * TauxFranchise, 2, MidpointRounding.AwayFromZero),
            MontantMinimum ?? 0m);
}
