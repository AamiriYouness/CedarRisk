using CedarRisk.Domain.Common;

namespace CedarRisk.Domain.ValueObjects;

/// <summary>
/// Coefficient Bonus/Malus — bornes contractuelles 0.50 / 3.50.
/// </summary>
public readonly record struct CrmCoefficient
{
    public const decimal Minimum = 0.50m;
    public const decimal Maximum = 3.50m;
    public const decimal Initial  = 1.00m;

    public decimal Value { get; }

    private CrmCoefficient(decimal value) => Value = value;

    public static Result<CrmCoefficient> Of(decimal value)
    {
        if (value < Minimum || value > Maximum)
            return Result<CrmCoefficient>.Failure(
                new CrmCoefficientInvalideError($"Le coefficient CRM doit être compris entre {Minimum} et {Maximum}. Valeur reçue : {value}."));
        return new CrmCoefficient(value);
    }

    public static CrmCoefficient Initial_Coefficient => new(Initial);

    /// <summary>Applique une année sans sinistre : -5%, plancher 0.50.</summary>
    public CrmCoefficient ApplySansSinistre()
    {
        var next = Math.Round(Value * 0.95m, 2, MidpointRounding.AwayFromZero);
        return new CrmCoefficient(Math.Max(next, Minimum));
    }

    /// <summary>Applique un sinistre responsable : +25%, plafond 3.50.</summary>
    public CrmCoefficient ApplyAvecSinistre()
    {
        var next = Math.Round(Value * 1.25m, 2, MidpointRounding.AwayFromZero);
        return new CrmCoefficient(Math.Min(next, Maximum));
    }

    public override string ToString() => $"CRM {Value:F2}";
}

public sealed record CrmCoefficientInvalideError(string Raison)
    : InvalidError(
        "crm_coefficient.invalide",
        $"Coefficient CRM invalide : {Raison}");
