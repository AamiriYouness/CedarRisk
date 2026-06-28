using CedarRisk.Domain.Common;
using CedarRisk.Domain.ReferentielTarifaires.Errors;
using CedarRisk.Domain.ReferentielTarifaires.ValueObjects;
using CedarRisk.Domain.ValueObjects;

namespace CedarRisk.Domain.ReferentielTarifaires;

/// <summary>
/// Référentiel tarifaire — source unique de vérité pour les taux RC et le barème puissance × usage.
///
/// Fiscalité (Article 284 CGI Maroc) :
///   TauxTaxeRC / TauxTaxeGC = 14% sur PrimeHT ET sur CatNatHT
///   TauxCatNatRC = 3.5% sur PrimeRC_HT → CatNatRC_HT
///   TauxCatNatGC = per guarantee sur GarantieTarification (pas ici)
/// </summary>
public sealed class ReferentielTarifaire
{
    private readonly List<BaremeRC> _bareme = [];

    // EF Core constructor
    private ReferentielTarifaire() { }

    private ReferentielTarifaire(
        TariffRate tauxCatNatRC,
        TariffRate tauxTaxeRC,
        TariffRate tauxParafiscaleRC,
        TariffRate tauxTaxeGC,
        TariffRate tauxParafiscaleGC,
        TarifRemorque tarifRemorque,
        Timbre timbreCNPAC,
        IEnumerable<BaremeRC> bareme,
        DateOnly validFrom,
        DateOnly? validTo,
        DateTimeOffset createdAt)
    {
        TauxCatNatRC = tauxCatNatRC;
        TauxTaxeRC = tauxTaxeRC;
        TauxParafiscaleRC = tauxParafiscaleRC;
        TauxTaxeGC = tauxTaxeGC;
        TauxParafiscaleGC = tauxParafiscaleGC;
        TarifRemorque = tarifRemorque;
        TimbreCNPAC = timbreCNPAC;
        ValidFrom = validFrom;
        ValidTo = validTo;
        CreatedAt = createdAt;
        _bareme.AddRange(bareme);
    }

    public int Id { get; private set; }

    // ── RC ────────────────────────────────────────────────────────────────────
    /// <summary>3.5% — PrimeRC_HT × TauxCatNatRC = CatNatRC_HT</summary>
    public TariffRate TauxCatNatRC { get; private set; } = default!;
    /// <summary>14% — appliqué sur PrimeRC_HT ET sur CatNatRC_HT</summary>
    public TariffRate TauxTaxeRC { get; private set; } = default!;
    public TariffRate TauxParafiscaleRC { get; private set; } = default!;

    // ── GC ────────────────────────────────────────────────────────────────────
    // TauxCatNatGC : sur GarantieTarification par garantie — pas ici
    /// <summary>14% — appliqué sur TotalPrimeHT_GC ET sur TotalCatNatHT_GC</summary>
    public TariffRate TauxTaxeGC { get; private set; } = default!;
    public TariffRate TauxParafiscaleGC { get; private set; } = default!;

    public TarifRemorque TarifRemorque { get; private set; } = default!;
    public Timbre TimbreCNPAC { get; private set; } = default!;
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<BaremeRC> Bareme => _bareme.AsReadOnly();

    public static Result<ReferentielTarifaire> Create(
        TariffRate tauxCatNatRC,
        TariffRate tauxTaxeRC,
        TariffRate tauxParafiscaleRC,
        TariffRate tauxTaxeGC,
        TariffRate tauxParafiscaleGC,
        TarifRemorque tarifRemorque,
        Timbre timbreCNPAC,
        IEnumerable<BaremeRC> bareme,
        DateOnly validFrom,
        DateOnly? validTo,
        DateTimeOffset now)
    {
        var lignes = bareme.ToList();

        var chevauchement = DetecterChevauchement(lignes);
        if (chevauchement is not null)
            return Result<ReferentielTarifaire>.Failure(chevauchement);

        return Result<ReferentielTarifaire>.Success(new ReferentielTarifaire(
            tauxCatNatRC, tauxTaxeRC, tauxParafiscaleRC,
            tauxTaxeGC, tauxParafiscaleGC,
            tarifRemorque, timbreCNPAC, lignes,
            validFrom, validTo, now));
    }

    public Result<PrimeHT> TrouverPrimeBase(UsageVehicule usage, int puissanceFiscale)
    {
        var ligne = _bareme.FirstOrDefault(b => b.CorrespondA(usage, puissanceFiscale));
        return ligne is not null
            ? Result<PrimeHT>.Success(ligne.PrimeHT)
            : Result<PrimeHT>.Failure(new BaremeRCIntrouvableError(usage, puissanceFiscale));
    }

    private static DomainError? DetecterChevauchement(List<BaremeRC> lignes)
    {
        var parUsage = lignes.GroupBy(l => l.Usage);
        foreach (var groupe in parUsage)
        {
            var triees = groupe.OrderBy(l => l.PuissanceMin).ToList();
            for (int i = 0; i < triees.Count - 1; i++)
            {
                var courante = triees[i];
                var suivante = triees[i + 1];

                if (courante.PuissanceMax == null)
                    return new ReferentielTarifaireInvalideError(
                        $"Tranche ouverte (sans PuissanceMax) pour {courante.Usage} " +
                        $"à partir de {courante.PuissanceMin} CV suivie d'une autre tranche.");

                if (courante.PuissanceMax >= suivante.PuissanceMin)
                    return new ReferentielTarifaireInvalideError(
                        $"Chevauchement de tranches pour {courante.Usage} : " +
                        $"[{courante.PuissanceMin}-{courante.PuissanceMax}] et [{suivante.PuissanceMin}-{suivante.PuissanceMax}].");
            }
        }
        return null;
    }
}