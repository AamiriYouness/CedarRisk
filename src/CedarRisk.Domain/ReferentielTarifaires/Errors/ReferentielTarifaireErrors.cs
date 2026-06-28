using CedarRisk.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CedarRisk.Domain.ReferentielTarifaires.Errors;

/// <summary>400 — structure du référentiel invalide (chevauchement de tranches, dates incohérentes).</summary>
public sealed record ReferentielTarifaireInvalideError(string Raison)
    : InvalidError(
        "referentiel_tarifaire.invalide",
        $"Référentiel tarifaire invalide : {Raison}");

/// <summary>404 — aucun référentiel tarifaire actif pour cette date.</summary>
public sealed record ReferentielTarifaireIntrouvableError(DateOnly Today)
    : NotFoundError(
        "referentiel_tarifaire.introuvable",
        $"Aucun référentiel tarifaire actif trouvé pour la date {Today:yyyy-MM-dd}.");

/// <summary>404 — aucune ligne de barème pour cette combinaison usage × puissance.</summary>
public sealed record BaremeRCIntrouvableError(UsageVehicule Usage, int PuissanceFiscale)
    : NotFoundError(
        "bareme_rc.introuvable",
        $"Aucune ligne de barème RC pour usage '{Usage}' et puissance {PuissanceFiscale} CV.");

/// <summary>400 — paramètres de construction d'une ligne de barème invalides.</summary>
public sealed record BaremeRCInvalideError(string Raison)
    : InvalidError(
        "bareme_rc.invalide",
        $"Barème RC invalide : {Raison}");