using CedarRisk.Domain.Common;

namespace CedarRisk.Domain.Garanties.Errros;

/// <summary>
/// 404 — aucune garantie trouvée pour ce code.
/// </summary>
public sealed record GarantieIntrouvableError(string Code)
    : NotFoundError(
        "garantie.introuvable",
        $"Aucune garantie trouvée pour le code '{Code}'.");

/// <summary>
/// 422 — la garantie est référencée par un tarif actif, désactivation impossible.
/// </summary>
public sealed record GarantieReferenceeParTariffActifError(string Code)
    : UnprocessableError(
        "garantie.referencee_par_tarif_actif",
        $"La garantie '{Code}' ne peut pas être désactivée car elle est référencée par un tarif actif.");

/// <summary>
/// 409 — tentative de désactivation d'une garantie déjà inactive.
/// </summary>
public sealed record GarantieDejaInactiveError(string Code)
    : ConflictError(
        "garantie.deja_inactive",
        $"La garantie '{Code}' est déjà inactive.");

/// <summary>
/// 409 — tentative de réactivation d'une garantie déjà active.
/// </summary>
public sealed record GarantieDejaActiveError(string Code)
    : ConflictError(
        "garantie.deja_active",
        $"La garantie '{Code}' est déjà active.");