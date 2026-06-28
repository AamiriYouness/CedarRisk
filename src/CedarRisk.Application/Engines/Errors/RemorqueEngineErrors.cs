using CedarRisk.Domain.Common;

namespace CedarRisk.Application.Engines.Errors;

/// <summary>
/// 422 — nombre de remorques invalide.
/// La limite contractuelle (max remorques par usage) est gérée par le contexte appelant,
/// pas par ce moteur. Ce moteur ne connaît que les règles de calcul.
/// </summary>
public sealed record NombreRemorquesInvalideError(int NbrRemorque)
    : UnprocessableError(
        "remorque.nombre_invalide",
        $"Le nombre de remorques doit être supérieur à zéro. Valeur reçue : {NbrRemorque}.");