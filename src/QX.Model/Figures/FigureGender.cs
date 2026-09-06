namespace Qx.Model.Figures;

/// <summary>
/// The gender partition used by figure data. A figure string never carries a gender;
/// the clients always receive it as a separate value next to the figure.
/// </summary>
public enum FigureGender
{
    Undefined = 0,
    Unisex = 1,
    Male = 2,
    Female = 3
}

public static class FigureGenderCode
{
    /// <summary>Parses a figure data gender code (<c>M</c>, <c>F</c> or <c>U</c>).</summary>
    public static FigureGender Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryParse(value, out FigureGender gender))
            throw new FormatException($"Unknown figure gender '{value}'.");

        return gender;
    }

    /// <summary>Parses a figure data gender code (<c>M</c>, <c>F</c> or <c>U</c>).</summary>
    public static bool TryParse(string? value, out FigureGender gender)
    {
        switch (value?.ToUpperInvariant())
        {
            case "U":
                gender = FigureGender.Unisex;
                return true;
            case "M":
                gender = FigureGender.Male;
                return true;
            case "F":
                gender = FigureGender.Female;
                return true;
            default:
                gender = FigureGender.Undefined;
                return false;
        }
    }

    /// <summary>Composes the figure data gender code. Throws for <see cref="FigureGender.Undefined"/>.</summary>
    public static string Compose(FigureGender gender) => gender switch
    {
        FigureGender.Unisex => "U",
        FigureGender.Male => "M",
        FigureGender.Female => "F",
        _ => throw new ArgumentOutOfRangeException(nameof(gender))
    };

    /// <summary>Composes the gender code the way the client normalises it, falling back to <c>U</c>.</summary>
    public static string ToClientString(this FigureGender gender) => gender switch
    {
        FigureGender.Male => "M",
        FigureGender.Female => "F",
        _ => "U"
    };

    /// <summary>Maps the avatar gender carried by user and room messages onto the figure data partition.</summary>
    public static FigureGender FromAvatarGender(Gender gender) => gender switch
    {
        Gender.Male => FigureGender.Male,
        Gender.Female => FigureGender.Female,
        Gender.Unisex => FigureGender.Unisex,
        _ => FigureGender.Undefined
    };

    /// <summary>Maps the figure data partition onto the avatar gender carried by user and room messages.</summary>
    public static Gender ToAvatarGender(this FigureGender gender) => gender switch
    {
        FigureGender.Male => Gender.Male,
        FigureGender.Female => Gender.Female,
        FigureGender.Unisex => Gender.Unisex,
        _ => Gender.None
    };
}
