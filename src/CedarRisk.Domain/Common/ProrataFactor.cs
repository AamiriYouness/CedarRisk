namespace CedarRisk.Domain.Common;

/// <summary>
/// Facteur de prorata — ratio de couverture sur l'année.
///
/// Règles :
///   nbJours       = dateEcheance - dateEffet (nombre de jours de couverture)
///   nbrJoursAnnee = 366 si année bissextile (année de dateEffet), sinon 365
///   Facteur       = Min(1, nbJours / nbrJoursAnnee)
///
/// Contrat pleine année ou plus → Facteur = 1 (jamais > 1).
/// Appliqué sur les trois pistes : RC, GC (par garantie), Remorque MontantFlat.
/// Remorque Taux : prorata déjà inclus via PrimeRC_HT — pas appliqué deux fois.
/// </summary>
public readonly record struct ProrataFactor
{
    public decimal Value { get; }

    private ProrataFactor(decimal value) => Value = value;

    public static readonly ProrataFactor PleineAnnee = new(1m);

    public static Result<ProrataFactor> Calculer(DateOnly dateEffet, DateOnly dateEcheance)
    {
        if (dateEcheance <= dateEffet)
            return Result<ProrataFactor>.Failure(
                new ProrataFactorInvalideError(
                    $"DateEcheance ({dateEcheance:yyyy-MM-dd}) doit être postérieure à DateEffet ({dateEffet:yyyy-MM-dd})."));

        var nbJours = dateEcheance.DayNumber - dateEffet.DayNumber;
        var nbrJoursAnnee = DateTime.IsLeapYear(dateEffet.Year) ? 366 : 365;

        var facteur = Math.Round((decimal)nbJours / nbrJoursAnnee, 6, MidpointRounding.AwayFromZero);

        return Result<ProrataFactor>.Success(new(Math.Min(1m, facteur)));
    }

    public static Result<ProrataFactor> Of(decimal value)
    {
        if (value <= 0 || value > 1)
            return Result<ProrataFactor>.Failure(
                new ProrataFactorInvalideError(
                    $"La valeur doit être dans ]0, 1]. Valeur reçue : {value}."));
        return Result<ProrataFactor>.Success(new(value));
    }

    public override string ToString() => $"Prorata {Value:P4}";
}

public sealed record ProrataFactorInvalideError(string Raison)
    : InvalidError(
        "prorata_factor.invalide",
        $"Facteur de prorata invalide : {Raison}");
