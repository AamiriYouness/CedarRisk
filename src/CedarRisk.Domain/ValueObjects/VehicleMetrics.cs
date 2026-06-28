namespace CedarRisk.Domain.ValueObjects;

/// <summary>
/// Puissance fiscale du véhicule (chevaux fiscaux).
/// Plage valide : 1–99 CV.
/// </summary>
public readonly record struct PuissanceFiscale
{
    public int Value { get; }

    private PuissanceFiscale(int value) => Value = value;

    public static PuissanceFiscale Of(int value)
    {
        if (value < 1 || value > 99)
            throw new ArgumentOutOfRangeException(nameof(value),
                $"La puissance fiscale doit être entre 1 et 99 CV. Valeur reçue : {value}.");
        return new PuissanceFiscale(value);
    }

    public override string ToString() => $"{Value} CV";
}

/// <summary>
/// Âge du véhicule en années révolues à la date d'effet.
/// </summary>
public readonly record struct VehicleAge
{
    public int Value { get; }

    private VehicleAge(int value) => Value = value;

    public static VehicleAge Of(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value),
                $"L'âge du véhicule ne peut pas être négatif. Valeur reçue : {value}.");
        return new VehicleAge(value);
    }

    public static VehicleAge FromDates(DateOnly dateMiseEnCirculation, DateOnly dateEffet)
    {
        int age = dateEffet.Year - dateMiseEnCirculation.Year;
        if (dateEffet < dateMiseEnCirculation.AddYears(age)) age--;
        if (age < 0) age = 0;
        return new VehicleAge(age);
    }

    public override string ToString() => $"{Value} an(s)";
}
