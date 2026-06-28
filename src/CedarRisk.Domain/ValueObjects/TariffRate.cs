namespace CedarRisk.Domain.ValueObjects;

/// <summary>
/// Taux tarifaire — pourcentage décimal (ex: 0.12 = 12 %).
/// Doit être positif ou nul. Ne dépasse jamais 100 %.
/// </summary>
public readonly record struct TariffRate
{
    public decimal Value { get; }

    private TariffRate(decimal value) => Value = value;

    public static TariffRate Of(decimal value)
    {
        if (value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(nameof(value),
                $"Le taux tarifaire doit être entre 0 et 1 (représentation décimale). Valeur reçue : {value}.");
        return new TariffRate(value);
    }

    public static TariffRate Zero => new(0m);

    public decimal Apply(decimal baseAmount) => baseAmount * Value;

    public override string ToString() => $"{Value * 100:F4} %";
}
