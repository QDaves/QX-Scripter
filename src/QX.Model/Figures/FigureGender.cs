namespace Qx.Model.Figures;

/// <summary>
/// Specifies the gender partition used by figure data.
/// </summary>
/// <remarks>
/// A figure string never carries a gender; the client always receives it as a separate value next to the figure.
/// </remarks>
public enum FigureGender
{
    /// <summary>No gender, used when a gender is unknown or cannot be inferred.</summary>
    Undefined = 0,
    /// <summary>Any gender, code <c>U</c>.</summary>
    Unisex = 1,
    /// <summary>Male, code <c>M</c>.</summary>
    Male = 2,
    /// <summary>Female, code <c>F</c>.</summary>
    Female = 3
}

/// <summary>Provides conversions between <see cref="FigureGender"/> values, gender codes and <see cref="Gender"/>.</summary>
public static class FigureGenderCode
{
    /// <summary>Parses a figure data gender code (<c>M</c>, <c>F</c> or <c>U</c>).</summary>
    /// <param name="value">The gender code, case insensitive.</param>
    /// <returns>The parsed gender.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="value"/> is not a known gender code.</exception>
    public static FigureGender Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryParse(value, out FigureGender gender))
            throw new FormatException($"Unknown figure gender '{value}'.");

        return gender;
    }

    /// <summary>Tries to parse a figure data gender code (<c>M</c>, <c>F</c> or <c>U</c>).</summary>
    /// <param name="value">The gender code, case insensitive.</param>
    /// <param name="gender">The parsed gender, or <see cref="FigureGender.Undefined"/> when parsing fails.</param>
    /// <returns><see langword="true"/> if the code was parsed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>Composes the figure data gender code (<c>M</c>, <c>F</c> or <c>U</c>).</summary>
    /// <param name="gender">The gender to compose.</param>
    /// <returns>The gender code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="gender"/> is <see cref="FigureGender.Undefined"/> or not a defined value.</exception>
    public static string Compose(FigureGender gender) => gender switch
    {
        FigureGender.Unisex => "U",
        FigureGender.Male => "M",
        FigureGender.Female => "F",
        _ => throw new ArgumentOutOfRangeException(nameof(gender))
    };

    /// <summary>Composes the gender code the way the client normalizes it, falling back to <c>U</c>.</summary>
    /// <param name="gender">The gender to compose.</param>
    /// <returns><c>M</c>, <c>F</c>, or <c>U</c> for any other value.</returns>
    public static string ToClientString(this FigureGender gender) => gender switch
    {
        FigureGender.Male => "M",
        FigureGender.Female => "F",
        _ => "U"
    };

    /// <summary>Maps the avatar gender carried by user and room messages onto the figure data partition.</summary>
    /// <param name="gender">The avatar gender.</param>
    /// <returns>The matching figure gender, or <see cref="FigureGender.Undefined"/> for <see cref="Gender.None"/>.</returns>
    public static FigureGender FromAvatarGender(Gender gender) => gender switch
    {
        Gender.Male => FigureGender.Male,
        Gender.Female => FigureGender.Female,
        Gender.Unisex => FigureGender.Unisex,
        _ => FigureGender.Undefined
    };

    /// <summary>Maps the figure data partition onto the avatar gender carried by user and room messages.</summary>
    /// <param name="gender">The figure gender.</param>
    /// <returns>The matching avatar gender, or <see cref="Gender.None"/> for <see cref="FigureGender.Undefined"/>.</returns>
    public static Gender ToAvatarGender(this FigureGender gender) => gender switch
    {
        FigureGender.Male => Gender.Male,
        FigureGender.Female => Gender.Female,
        FigureGender.Unisex => Gender.Unisex,
        _ => Gender.None
    };
}
