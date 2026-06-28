using CedarRisk.Domain.Common;

namespace CedarRisk.Domain.GarantieTarifications.ValueObjects;

/// <summary>
/// Résultat du calcul fiscal pour une garantie individuelle.
/// Ne contient PAS Parafiscale — celle-ci est calculée UNE FOIS sur la somme
/// (ΣPrimeHT + ΣCatNatHT) par le PremiumAggregator, comme pour la piste RC.
/// </summary>
public sealed record PrimeGarantieHT(
    string GarantieCode,
    PrimeHT Prime,
    CatNatHT CatNat);      // Zero si EstSoumisACatNat = false

/// <summary>
/// Agrégation des primes GC — toutes les garanties sélectionnées.
///
/// Parafiscale NON incluse ici — calculée une seule fois par PremiumAggregator :
///   Parafiscale_GC = (TotalPrimeHT + TotalCatNatHT) × TauxParafiscaleGC
///
/// Même logique que RC :
///   Parafiscale_RC = (PrimeRC_HT + CatNatRC_HT) × TauxParafiscaleRC
/// </summary>
public sealed record PrimesGarantiesHT
{
    public static readonly PrimesGarantiesHT Vide = new([]);

    public IReadOnlyList<PrimeGarantieHT> Lignes { get; }

    /// <summary>Somme des primes HT — base de calcul parafiscale partielle.</summary>
    public PrimeHT TotalPrimeHT =>
        Lignes.Aggregate(PrimeHT.Zero, (acc, l) => acc + l.Prime);

    /// <summary>Somme des CatNat HT — s'ajoute à TotalPrimeHT pour la base parafiscale.</summary>
    public CatNatHT TotalCatNatHT =>
        Lignes.Aggregate(CatNatHT.Zero, (acc, l) => acc + l.CatNat);

    private PrimesGarantiesHT(IReadOnlyList<PrimeGarantieHT> lignes) => Lignes = lignes;

    public static PrimesGarantiesHT Create(IEnumerable<PrimeGarantieHT> lignes) =>
        new(lignes?.ToList() ?? []);

    public static PrimesGarantiesHT operator +(PrimesGarantiesHT acc, PrimeGarantieHT ligne) =>
        new([.. acc.Lignes, ligne]);

    public static PrimesGarantiesHT operator +(PrimesGarantiesHT a, PrimesGarantiesHT b) =>
        new([.. a.Lignes, .. b.Lignes]);

    public PrimeGarantieHT? PourGarantie(string code) =>
        Lignes.FirstOrDefault(l =>
            l.GarantieCode.Equals(code.Trim().ToUpperInvariant(),
                StringComparison.Ordinal));
}