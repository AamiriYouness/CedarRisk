namespace CedarRisk.Domain.ValueObjects;

/// <summary>
/// Représente un montant de prime — toujours positif ou nul.
/// Arrondi systématique via PremiumRoundingPolicy, jamais ici.
/// </summary>
public readonly record struct Premium
{
    public decimal Value { get; }

    private Premium(decimal value) => Value = value;

    public static Premium Of(decimal value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Une prime ne peut pas être négative.");
        return new Premium(value);
    }

    public static Premium Zero => new(0m);

    public static Premium operator +(Premium a, Premium b) => new(a.Value + b.Value);
    public static Premium operator *(Premium a, decimal factor) => new(a.Value * factor);
    public static Premium operator *(decimal factor, Premium a) => new(a.Value * factor);

    public override string ToString() => $"{Value:F2} MAD";
}
