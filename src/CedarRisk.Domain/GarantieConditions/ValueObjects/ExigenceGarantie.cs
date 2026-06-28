using CedarRisk.Domain.Common;

namespace CedarRisk.Domain.GarantieConditions.ValueObjects;

/// <summary>
/// TOUTES ces garanties doivent être sélectionnées — sémantique AND.
/// Ex: "VOL requiert DOM ET INC" → ExigenceConjonctive(["DOM", "INC"])
/// </summary>
public sealed record ExigenceConjonctive
{
    public static readonly ExigenceConjonctive Vide = new([]);

    public IReadOnlyList<string> Codes { get; }

    private ExigenceConjonctive(IReadOnlyList<string> codes) => Codes = codes;

    public static Result<ExigenceConjonctive> Create(IEnumerable<string>? codes)
    {
        var list = Normalize(codes);
        return Result<ExigenceConjonctive>.Success(new(list));
    }

    /// <summary>EF Core hydration — trusts DB data is already valid.</summary>
    public static ExigenceConjonctive Hydrate(IEnumerable<string>? codes) =>
        new(Normalize(codes));

    /// <summary>Toutes les codes présents dans la sélection ?</summary>
    public bool EstSatisfaitePar(IEnumerable<string> codesSelectionnes)
    {
        if (!Codes.Any()) return true;
        var selection = ToSet(codesSelectionnes);
        return Codes.All(c => selection.Contains(c));
    }

    public IReadOnlyList<string> CodesManquants(IEnumerable<string> codesSelectionnes)
    {
        var selection = ToSet(codesSelectionnes);
        return Codes.Where(c => !selection.Contains(c)).ToList();
    }

    public bool Contains(string code) => Codes.Contains(Norm(code));
    public bool EstVide => !Codes.Any();

    private static List<string> Normalize(IEnumerable<string>? codes) =>
        (codes ?? []).Select(c => Norm(c ?? string.Empty)).Distinct().OrderBy(c => c).ToList();

    private static HashSet<string> ToSet(IEnumerable<string> codes) =>
        codes.Select(c => Norm(c)).ToHashSet();

    private static string Norm(string c) => c.Trim().ToUpperInvariant();
}

/// <summary>
/// AU MOINS UNE de ces garanties doit être sélectionnée — sémantique OR.
/// Ex: "VOL requiert au moins DOM ou INC ou ASSIST" → ExigenceDisjonctive(["DOM", "INC", "ASSIST"])
/// </summary>
public sealed record ExigenceDisjonctive
{
    public static readonly ExigenceDisjonctive Vide = new([]);

    public IReadOnlyList<string> Codes { get; }

    private ExigenceDisjonctive(IReadOnlyList<string> codes) => Codes = codes;

    public static Result<ExigenceDisjonctive> Create(IEnumerable<string>? codes)
    {
        var list = Normalize(codes);
        return Result<ExigenceDisjonctive>.Success(new(list));
    }

    /// <summary>EF Core hydration — trusts DB data is already valid.</summary>
    public static ExigenceDisjonctive Hydrate(IEnumerable<string>? codes) =>
        new(Normalize(codes));

    /// <summary>Au moins un code présent dans la sélection ? Vide = toujours satisfait.</summary>
    public bool EstSatisfaitePar(IEnumerable<string> codesSelectionnes)
    {
        if (!Codes.Any()) return true;
        var selection = ToSet(codesSelectionnes);
        return Codes.Any(c => selection.Contains(c));
    }

    public bool AucunPresent(IEnumerable<string> codesSelectionnes) =>
        !EstSatisfaitePar(codesSelectionnes);

    public bool Contains(string code) => Codes.Contains(Norm(code));
    public bool EstVide => !Codes.Any();

    private static List<string> Normalize(IEnumerable<string>? codes) =>
        (codes ?? []).Select(c => Norm(c ?? string.Empty)).Distinct().OrderBy(c => c).ToList();

    private static HashSet<string> ToSet(IEnumerable<string> codes) =>
        codes.Select(c => Norm(c)).ToHashSet();

    private static string Norm(string c) => c.Trim().ToUpperInvariant();
}

/// <summary>
/// Aucune de ces garanties ne peut coexister avec la garantie porteuse — sémantique NONE.
/// Appliquée BIDIRECTIONNELLEMENT par EligibilityEngine.
/// </summary>
public sealed record IncompatibilitesGarantie
{
    public static readonly IncompatibilitesGarantie Vide = new([]);

    public IReadOnlyList<string> Codes { get; }

    private IncompatibilitesGarantie(IReadOnlyList<string> codes) => Codes = codes;

    public static Result<IncompatibilitesGarantie> Create(
        IEnumerable<string>? codes,
        string codePorteur)
    {
        var list = Normalize(codes);
        return Result<IncompatibilitesGarantie>.Success(new(list));
    }

    /// <summary>EF Core hydration — self-incompatibility was already enforced on write.</summary>
    public static IncompatibilitesGarantie Hydrate(IEnumerable<string>? codes) =>
        new(Normalize(codes));

    /// <summary>Aucun code incompatible présent dans la sélection ?</summary>
    public bool EstCompatibleAvec(IEnumerable<string> codesSelectionnes)
    {
        var selection = ToSet(codesSelectionnes);
        return !Codes.Any(c => selection.Contains(c));
    }

    public IReadOnlyList<string> CodesEnConflit(IEnumerable<string> codesSelectionnes)
    {
        var selection = ToSet(codesSelectionnes);
        return Codes.Where(c => selection.Contains(c)).ToList();
    }

    public bool Contains(string code) => Codes.Contains(Norm(code));
    public bool EstVide => !Codes.Any();

    private static List<string> Normalize(IEnumerable<string>? codes) =>
        (codes ?? []).Select(c => Norm(c ?? string.Empty)).Distinct().OrderBy(c => c).ToList();

    private static HashSet<string> ToSet(IEnumerable<string> codes) =>
        codes.Select(c => Norm(c)).ToHashSet();

    private static string Norm(string c) => c.Trim().ToUpperInvariant();
}