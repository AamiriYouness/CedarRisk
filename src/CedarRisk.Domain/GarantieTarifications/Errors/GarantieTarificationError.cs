using CedarRisk.Domain.Common;

namespace CedarRisk.Domain.GarantieTarifications.Errors;

/// <summary>404 — aucune tarification active pour ce code garantie à cette date.</summary>
public sealed record TarificationIntrouvableError(string GarantieCode, DateOnly Today)
    : NotFoundError(
        "garantie_tarification.introuvable",
        $"Aucune tarification active pour la garantie '{GarantieCode}' à la date {Today:yyyy-MM-dd}.");

/// <summary>400 — taux ou montant invalide (nul ou négatif).</summary>
public sealed record TauxInvalideError(decimal Valeur)
    : InvalidError(
        "mode_tarifaire.taux_invalide",
        $"Le taux ou montant {Valeur} est invalide. La valeur doit être strictement positive.");

/// <summary>400 — aucune option de capital fournie pour CapitalOptionnel.</summary>
public sealed record OptionsCapitalVidesError()
    : InvalidError(
        "mode_tarifaire.options_capital_vides",
        "La liste des options de capital ne peut pas être vide.");

/// <summary>422 — l'option de capital demandée n'existe pas dans les options disponibles.</summary>
public sealed record OptionCapitalIntrouvableError(decimal OptionDemandee)
    : UnprocessableError(
        "mode_tarifaire.option_capital_introuvable",
        $"L'option de capital {OptionDemandee:N2} MAD n'existe pas dans les options disponibles pour cette garantie.");

/// <summary>422 — l'option choisie dépasse la valeur vénale. Invariant absolu.</summary>
public sealed record OptionCapitalDepasseValeurVenaleError(decimal Capital, decimal ValeurVenale)
    : UnprocessableError(
        "mode_tarifaire.option_capital_depasse_valeur_venale",
        $"L'option de capital {Capital:N2} MAD dépasse la valeur vénale {ValeurVenale:N2} MAD.");

/// <summary>422 — le capital client dépasse la valeur vénale. Invariant absolu du droit marocain.</summary>
public sealed record CapitalDepasseValeurVenaleError(decimal CapitalDeclare, decimal ValeurVenale)
    : UnprocessableError(
        "mode_tarifaire.capital_depasse_valeur_venale",
        $"Le capital déclaré {CapitalDeclare:N2} MAD dépasse la valeur vénale {ValeurVenale:N2} MAD. " +
        $"Le capital assuré ne peut jamais excéder la valeur vénale.");

/// <summary>422 — le capital client dépasse le plafond défini par la règle de capital.</summary>
public sealed record CapitalDepassePlafondError(decimal CapitalDeclare, decimal Plafond)
    : UnprocessableError(
        "mode_tarifaire.capital_depasse_plafond",
        $"Le capital déclaré {CapitalDeclare:N2} MAD dépasse le plafond autorisé {Plafond:N2} MAD.");

/// <summary>422 — le capital de référence est absent alors que la règle l'exige.</summary>
public sealed record CapitalReferenceAbsentError(string GarantieCodeReference)
    : UnprocessableError(
        "mode_tarifaire.capital_reference_absent",
        $"Le capital de référence pour la garantie '{GarantieCodeReference}' est requis mais absent du contexte.");

/// <summary>400 — aucune condition fournie pour la règle de capital.</summary>
public sealed record RegleCapitalConditionsVidesError()
    : InvalidError(
        "regle_capital.conditions_vides",
        "La règle de capital doit contenir au moins une condition.");

/// <summary>400 — GarantieCodeRef requis pour TypePlafond.CapitalGarantie.</summary>
public sealed record RegleCapitalGarantieRefRequiseError()
    : InvalidError(
        "regle_capital.garantie_ref_requise",
        "Le code de la garantie de référence est requis pour un plafond de type CapitalGarantie.");

/// <summary>400 — pourcentage hors bornes ]0, 100].</summary>
public sealed record RegleCapitalPourcentageInvalideError(decimal Pourcentage)
    : InvalidError(
        "regle_capital.pourcentage_invalide",
        $"Le pourcentage {Pourcentage} est invalide. La valeur doit être dans ]0, 100].");

/// <summary>422 — capital client hors règle contractuelle.</summary>
public sealed record CapitalHorsRegleError(decimal CapitalClient, string ConditionsEchouees)
    : UnprocessableError(
        "regle_capital.capital_hors_regle",
        $"Le capital déclaré {CapitalClient:N2} MAD ne respecte pas les conditions : {ConditionsEchouees}.");

/// <summary>409 — une tarification active existe déjà pour ce code garantie.</summary>
public sealed record TarificationDejaActiveError(string GarantieCode)
    : ConflictError(
        "garantie_tarification.deja_active",
        $"Une tarification active existe déjà pour la garantie '{GarantieCode}'.");

/// <summary>400 — dates de validité incohérentes.</summary>
public sealed record TarificationDatesInvalidesError(DateOnly ValidFrom, DateOnly ValidTo)
    : InvalidError(
        "garantie_tarification.dates_invalides",
        $"ValidTo ({ValidTo:yyyy-MM-dd}) ne peut pas être antérieur à ValidFrom ({ValidFrom:yyyy-MM-dd}).");

