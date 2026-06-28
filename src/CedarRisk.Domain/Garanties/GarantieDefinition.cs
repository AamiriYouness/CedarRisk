using CedarRisk.Domain.Common;
using CedarRisk.Domain.Garanties.Errros;
namespace CedarRisk.Domain.Garanties;

/// <summary>
/// Définit ce qu'est une garantie complémentaire — identité, libellé, état actif.
/// Mutable. Jamais supprimée physiquement.
/// </summary>
public class GarantieDefinition
{
    private GarantieDefinition() { }

    private GarantieDefinition(string code, string libelle, string description, DateTimeOffset now)
    {
        Code = code;
        Libelle = libelle;
        Description = description;
        IsActive = true;
        CreatedAt = now;
    }

    public string Code { get; private set; } = default!;
    public string Libelle { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public static Result<GarantieDefinition> Create(
        string code,
        string libelle,
        string description,
        DateTimeOffset now)
    {
        var garantie = new GarantieDefinition(code.Trim().ToUpperInvariant(), libelle.Trim(), description.Trim(), now);
        return Result<GarantieDefinition>.Success(garantie);
    }

    public Result<Unit> Deactivate(DateTimeOffset now, bool isReferencedByActiveTariff)
    {
        if (!IsActive)
            return Result<Unit>.Failure(new GarantieDejaInactiveError(Code)); // 409 Conflict

        if (isReferencedByActiveTariff)
            return Result<Unit>.Failure(new GarantieReferenceeParTariffActifError(Code)); // 422

        IsActive = false;
        UpdatedAt = now;
        return Result<Unit>.Success(Unit.Value);
    }

    public Result<Unit> Reactivate(DateTimeOffset now)
    {
        if (IsActive)
            return Result<Unit>.Failure(new GarantieDejaActiveError(Code)); // 409

        IsActive = true;
        UpdatedAt = now;
        return Result<Unit>.Success(Unit.Value);
    }
}
