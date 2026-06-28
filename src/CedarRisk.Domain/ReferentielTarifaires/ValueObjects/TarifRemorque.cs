using CedarRisk.Domain.Common;
using CedarRisk.Domain.ValueObjects;

namespace CedarRisk.Domain.ReferentielTarifaires.ValueObjects;

/// <summary>
/// Mode de tarification de la remorque.
/// Deux cas :
///   Taux        — PrimeHT = PrimeRC × Taux × NbrRemorque
///   MontantFlat — PrimeHT = MontantParRemorque × NbrRemorque (indépendant de PrimeRC)
///
/// Stocké via TPH (Table Per Hierarchy) — une seule table, discriminant + colonnes nullable.
/// Aucune annotation EF dans le domaine — mapping 100% dans la configuration Infrastructure.
/// </summary>
public abstract record TarifRemorque
{
    // EF Core constructor
    protected TarifRemorque() { }

    /// <summary>Calcule la PrimeHT remorque selon le mode configuré.</summary>
    public abstract PrimeHT CalculerPrime(PrimeHT primeRC, int nbrRemorque, ProrataFactor prorataFactor);

    // ── Sous-types ────────────────────────────────────────────────────────────

    /// <summary>
    /// Surprime proportionnelle à la prime RC.
    /// PrimeHT = PrimeRC × Taux × NbrRemorque.
    /// </summary>
    public sealed record Taux : TarifRemorque
    {
        public TariffRate Valeur { get; init; }

        // EF Core constructor
        private Taux() { }

        public Taux(TariffRate valeur) => Valeur = valeur;

        public override PrimeHT CalculerPrime(PrimeHT primeRC, int nbrRemorque, ProrataFactor prorataFactor) =>
            primeRC * Valeur.Value * nbrRemorque;
    }

    /// <summary>
    /// Montant fixe par remorque — indépendant de la prime RC.
    /// PrimeHT = MontantParRemorque × NbrRemorque.
    /// </summary>
    public sealed record MontantFlat : TarifRemorque
    {
        public PrimeHT MontantParRemorque { get; init; }

        // EF Core constructor
        private MontantFlat() {
            MontantParRemorque = default!;
        }

        public MontantFlat(PrimeHT montantParRemorque) =>
            MontantParRemorque = montantParRemorque;

        public override PrimeHT CalculerPrime(PrimeHT primeRC, int nbrRemorque, ProrataFactor prorataFactor) =>
            new(Math.Round(MontantParRemorque.Montant * nbrRemorque * prorataFactor.Value, 2,
                MidpointRounding.AwayFromZero));
    }
}