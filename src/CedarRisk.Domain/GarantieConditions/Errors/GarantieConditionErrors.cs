using CedarRisk.Domain.Common;

namespace CedarRisk.Domain.GarantieConditions.Errors;

/// <summary>
/// 409 — une condition active existe déjà pour cette garantie.
/// </summary>
public sealed record ConditionActiveDejaExisteError(string GarantieCode)
    : ConflictError(
        "condition.active_deja_existe",
        $"Une condition active existe déjà pour la garantie '{GarantieCode}'. Désactivez-la avant d'en créer une nouvelle.");

/// <summary>
/// 409 — tentative de désactivation d'une condition déjà inactive.
/// </summary>
public sealed record ConditionDejaInactiveError(Guid Id)
    : ConflictError(
        "condition.deja_inactive",
        $"La condition '{Id}' est déjà inactive.");

/// <summary>
/// 422 — une garantie ne peut pas être incompatible avec elle-même.
/// </summary>
public sealed record GarantieIncompatibleAvecElleMemeError(string Code)
    : UnprocessableError(
        "condition.garantie_incompatible_avec_elle_meme",
        $"La garantie '{Code}' ne peut pas être incompatible avec elle-même.");

/// <summary>
/// 422 — une garantie ne peut pas être à la fois requise et incompatible.
/// </summary>
public sealed record GarantieRequiseEtIncompatibleError(string Code)
    : UnprocessableError(
        "condition.garantie_requise_et_incompatible",
        $"La garantie '{Code}' ne peut pas être à la fois requise et incompatible.");
