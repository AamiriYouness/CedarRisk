using System;
using System.Collections.Generic;
using System.Text;

namespace CedarRisk.Domain.Common;

// =============================================================================
// Tax algebra — each type represents a distinct fiscal concept.
// The type system enforces correct chaining — invalid operations don't compile.
//
// Chain per GC guarantee (in GarantiePremiumEngine):
//   PrimeHT
//     → CatNatHT        = PrimeHT × TauxCatNat           (only if EstSoumisACatNat)
//     → TaxeCA          = PrimeHT × TauxTaxe              (TCA on prime base)
//     → TaxeCA(catnat)  = CatNatHT × (1 + TauxTaxe)      (TCA on CatNat)
//
// Parafiscale calculated ONCE by PremiumAggregator on the cumulated GC base:
//   Parafiscale_GC = (ΣPrimeHT_GC + ΣCatNatHT_GC) × TauxParafiscaleGC
//   same as RC:
//   Parafiscale_RC = (PrimeRC_HT + CatNatRC_HT) × TauxParafiscaleRC
//
// Final PrimeTTC assembled by PremiumAggregator — not per-guarantee.
// =============================================================================

/// <summary>
/// Prime hors taxes — base de calcul avant toute fiscalité.
/// Produit par IModeTarifaire.CalculerPrime().
/// </summary>
public sealed record PrimeHT(decimal Montant)
{
    public static readonly PrimeHT Zero = new(0m);

    /// <summary>PrimeHT + PrimeHT → PrimeHT. Arrondi intermédiaire AwayFromZero.</summary>
    public static PrimeHT operator +(PrimeHT a, PrimeHT b) =>
        new(Round(a.Montant + b.Montant));

    public static PrimeHT operator *(PrimeHT a, decimal facteur) =>
        new(Round(a.Montant * facteur));

    /// <summary>
    /// Calcule la contribution CatNat HT.
    /// Retourne CatNatHT.Zero si taux = 0 — le moteur gate sur EstSoumisACatNat avant d'appeler.
    /// </summary>
    public CatNatHT AppliquerCatNat(decimal tauxCatNat) =>
        new(Round(Montant * tauxCatNat));

    /// <summary>
    /// TCA sur la prime HT.
    /// TaxeCA = PrimeHT × TauxTaxe.
    /// </summary>
    public TaxeCA AppliquerTaxeCA(decimal tauxTaxe) =>
        new(Round(Montant * tauxTaxe));

    /// <summary>
    /// Parafiscale = (PrimeHT + CatNatHT) × TauxParafiscale.
    /// CatNatHT obligatoirement fourni — le compilateur empêche de l'oublier.
    /// </summary>
    public Parafiscale AppliquerParafiscale(decimal tauxParafiscale, CatNatHT catNat) =>
        new(Round((Montant + catNat.Montant) * tauxParafiscale));

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Contribution CatNat hors taxes.
/// Occupe une position fiscale intermédiaire :
///   - Reçoit sa propre TCA → TaxeCA
///   - Gonfle la base parafiscale (PrimeHT + CatNatHT)
///   - Ne se cumule jamais sur elle-même
/// </summary>
public sealed record CatNatHT(decimal Montant)
{
    public static readonly CatNatHT Zero = new(0m);

    public static CatNatHT operator +(CatNatHT a, CatNatHT b) =>
        new(Math.Round(a.Montant + b.Montant, 2, MidpointRounding.AwayFromZero));

    /// <summary>
    /// TCA sur CatNat : CatNatHT × TauxTaxe.
    /// Retourne TaxeCA car c'est une contribution fiscale sur CatNat.
    /// </summary>
    public TaxeCA AppliquerTaxeCA(decimal tauxTaxe) =>
        new(Math.Round(Montant * tauxTaxe, 2, MidpointRounding.AwayFromZero));
}

/// <summary>
/// Taxe sur Contrats d'Assurance (TCA).
/// Appliquée sur PrimeHT et sur CatNatHT séparément.
/// </summary>
public sealed record TaxeCA(decimal Montant)
{
    public static readonly TaxeCA Zero = new(0m);

    public static TaxeCA operator +(TaxeCA a, TaxeCA b) =>
        new(Math.Round(a.Montant + b.Montant, 2, MidpointRounding.AwayFromZero));
}

/// <summary>
/// Contribution parafiscale.
/// Base = PrimeHT + CatNatHT — enforced by signature of PrimeHT.AppliquerParafiscale().
/// </summary>
public sealed record Parafiscale(decimal Montant)
{
    public static readonly Parafiscale Zero = new(0m);

    public static Parafiscale operator +(Parafiscale a, Parafiscale b) =>
        new(Math.Round(a.Montant + b.Montant, 2, MidpointRounding.AwayFromZero));
}

/// <summary>
/// Timbre CNPAC — montant fixe, piste RC uniquement.
/// Ne passe jamais par TCA ou parafiscale.
/// PrimeTTC.AssemblerRC() est le seul point d'entrée qui l'accepte.
/// </summary>
public sealed record Timbre(decimal Montant)
{
    public static readonly Timbre Zero = new(0m);
}

/// <summary>
/// Prime toutes taxes comprises — résultat final scellé.
/// Produit uniquement par PrimeTTC.Assembler*() — jamais construit manuellement.
/// PrimeTotal_TTC final arrondi au centime supérieur (Math.Ceiling) par PremiumRoundingPolicy.
/// </summary>
public sealed record PrimeTTC(decimal Montant)
{
    public static readonly PrimeTTC Zero = new(0m);

    public static PrimeTTC operator +(PrimeTTC a, PrimeTTC b) =>
        new(Math.Round(a.Montant + b.Montant, 2, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Assemblage final GC total — appelé par PremiumAggregator, pas par-garantie.
    /// PrimeTTC_GC = ΣPrimeHT + ΣTaxeCA + ΣCatNatHT + ΣCatNatTaxe + Parafiscale_GC
    /// Parafiscale calculée une fois sur (ΣPrimeHT + ΣCatNatHT) avant cet appel.
    /// Taxes sur montants positifs uniquement.
    /// </summary>
    public static PrimeTTC AssemblerGC(
        PrimeHT totalPrimeHT,
        TaxeCA totalTaxeCA,
        CatNatHT totalCatNatHT,
        TaxeCA totalCatNatTaxe,
        Parafiscale parafiscaleGC)
    {
        var montant = totalPrimeHT.Montant
            + (totalTaxeCA.Montant > 0 ? totalTaxeCA.Montant : 0m)
            + (totalCatNatHT.Montant > 0 ? totalCatNatHT.Montant : 0m)
            + (totalCatNatTaxe.Montant > 0 ? totalCatNatTaxe.Montant : 0m)
            + (parafiscaleGC.Montant > 0 ? parafiscaleGC.Montant : 0m);

        return new(Math.Round(montant, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Assemblage RC — avec Timbre CNPAC.
    /// Timbre ajouté tel quel — pas de TCA, pas de parafiscale dessus.
    /// </summary>
    public static PrimeTTC AssemblerRC(
        PrimeHT primeHT,
        TaxeCA taxeCA,
        CatNatHT catNatHT,
        TaxeCA catNatTaxe,
        Parafiscale parafiscale,
        Timbre timbre)
    {
        var montant = primeHT.Montant
            + (taxeCA.Montant > 0 ? taxeCA.Montant : 0m)
            + (catNatHT.Montant > 0 ? catNatHT.Montant : 0m)
            + (catNatTaxe.Montant > 0 ? catNatTaxe.Montant : 0m)
            + (parafiscale.Montant > 0 ? parafiscale.Montant : 0m)
            + timbre.Montant;

        return new(Math.Round(montant, 2, MidpointRounding.AwayFromZero));
    }
}
