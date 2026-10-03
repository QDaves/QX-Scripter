using System.Globalization;

namespace Qx;

/// <summary>Represents a float that is written to the wire as a string.</summary>
/// <remarks>Flash sends float values as strings, formatted with the invariant culture.</remarks>
/// <param name="value">The float value.</param>
internal readonly struct FloatString(float value)
{
    /// <summary>Formats a float with the invariant culture, with at least one and at most 15 decimal places.</summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted value, for example <c>1.0</c> or <c>0.25</c>.</returns>
    public static string Format(float value) => value.ToString("0.0##############", CultureInfo.InvariantCulture);

    /// <summary>Gets the float value.</summary>
    public float Value { get; } = value;

    /// <summary>Converts a float to a <see cref="FloatString"/>.</summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator FloatString(float value) => new(value);
    /// <summary>Converts a <see cref="FloatString"/> to its float value.</summary>
    /// <param name="floatString">The value to convert.</param>
    public static implicit operator float(FloatString floatString) => floatString.Value;

    /// <summary>Converts a <see cref="FloatString"/> to its string form, formatted by <see cref="Format(float)"/>.</summary>
    /// <param name="floatString">The value to convert.</param>
    public static implicit operator string(FloatString floatString) => Format(floatString.Value);
    /// <summary>Parses a string with the invariant culture into a <see cref="FloatString"/>.</summary>
    /// <param name="value">The string to parse.</param>
    /// <exception cref="FormatException">Thrown when <paramref name="value"/> is not a valid number.</exception>
    public static explicit operator FloatString(string value) => new(float.Parse(value, CultureInfo.InvariantCulture));

    /// <summary>Returns the value formatted by <see cref="Format(float)"/>.</summary>
    public override string ToString() => Format(Value);
}
