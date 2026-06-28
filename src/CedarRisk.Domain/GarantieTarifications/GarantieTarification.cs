using CedarRisk.Domain.Common;
using CedarRisk.Domain.GarantieTarifications.ValueObjects;
using CedarRisk.Domain.ValueObjects;

namespace CedarRisk.Domain.GarantieTarifications;

/// <summary>
/// Tarification d'une garantie complémentaire — ledger immuable.
///
/// Règles :
///   - ValidFrom immuable une fois écrit
///   - ValidTo géré par le système via Supersede() — jamais saisi manuellement
///   - Courante = ValidFrom <= today AND (ValidTo IS NULL OR ValidTo >= today)
///   - Création d'une nouvelle tarification auto-clôture la précédente (ValidTo = newValidFrom - 1j)
///   - Pas de chevauchement pour le même GarantieCode
///   - ModeTarifaire porte toute la logique de calcul — pas de TypeChargementGC
/// </summary>
public sealed class GarantieTarification
{

    // EF Core constructor
    private GarantieTarification() { }

    private GarantieTarification(
        string garantieCode,
        IModeTarifaire modeTarifaire,
        bool estSoumisACatNat,
        TariffRate tauxCatNatGC,
        DateOnly validFrom,
        DateTimeOffset now)
    {
        GarantieCode = garantieCode;
        ModeTarifaire = modeTarifaire;
        EstSoumisACatNat = estSoumisACatNat;
        TauxCatNatGC = tauxCatNatGC;
        ValidFrom = validFrom;
        CreatedAt = now;
    }

    public int Id { get; private set; }
    public string GarantieCode { get; private set; } = default!;
    public IModeTarifaire ModeTarifaire { get; private set; } = default!;

    /// <summary>
    /// Indique si cette garantie est soumise à la contribution CatNat.
    /// Domaine : VOL, INC → true. BRIS, MontantFlat → généralement false.
    /// Le moteur ignore TauxCatNatGC si false — même si le taux est non-nul.
    /// </summary>
    public bool EstSoumisACatNat { get; private set; }

    public TariffRate TauxCatNatGC { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public int? SupersededById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool EstCourante(DateOnly today) =>
        ValidFrom <= today && (ValidTo is null || ValidTo >= today);

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    public static Result<GarantieTarification> Create(
        string garantieCode,
        IModeTarifaire modeTarifaire,
        bool estSoumisACatNat,
        TariffRate tauxCatNatGC,
        DateOnly validFrom,
        DateTimeOffset now)
    {
        var code = garantieCode.Trim().ToUpperInvariant();

        var tarification = new GarantieTarification(
            code, modeTarifaire, estSoumisACatNat,
            tauxCatNatGC, validFrom, now);

        return Result<GarantieTarification>.Success(tarification);
    }

    /// <summary>
    /// Clôture cet enregistrement quand une nouvelle tarification le remplace.
    /// ValidTo = newValidFrom - 1 jour (système — jamais saisi manuellement).
    /// </summary>
    public void Supersede(int newId, DateOnly newValidFrom)
    {
        ValidTo = newValidFrom.AddDays(-1);
        SupersededById = newId;
    }
}
