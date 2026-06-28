namespace CedarRisk.Domain.GarantieTarifications.ValueObjects;

/// <summary>
/// Contexte d'entrée pour le calcul d'une prime GC.
/// Résolu par le moteur avant d'appeler IModeTarifaire.CalculerPrimeHT().
/// </summary>
public sealed record BaseTarifaire(
    /// <summary>Valeur vénale du véhicule — plafond absolu domaine.</summary>
    decimal ValeurVenale,

    /// <summary>Capital déclaré par le client pour cette garantie (null si MontantFlat ou TauxDirect).</summary>
    decimal? CapitalClient,

    /// <summary>Capital résolu de la garantie référencée (null si pas de référence).</summary>
    decimal? CapitalGarantieReference,

    /// <summary>Option choisie par le client (null si pas CapitalOptionnel).</summary>
    CapitalOption? OptionChoisie);

/// <summary>Option fixe dans le mode CapitalOptionnel — (Capital, MontantHT) immuable.</summary>
public sealed record CapitalOption(decimal Capital, decimal MontantHT);
