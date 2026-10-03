namespace Qx;

/// <summary>Represents the numeric ID of a user, room, item or other game object.</summary>
/// <remarks>
/// The value is a 64-bit integer. Flash sends IDs as 32-bit big-endian integers, so
/// <see cref="Qx.Messages.PacketWriter.WriteId(Id)"/> throws for values outside that range.
/// </remarks>
public readonly record struct Id : IComparable<Id>, IComparable
{
    /// <summary>The smallest possible ID value.</summary>
    public const long MinValue = long.MinValue;
    /// <summary>The largest possible ID value.</summary>
    public const long MaxValue = long.MaxValue;

    private readonly long _value;
    private Id(long value) => _value = value;

    /// <summary>Converts an ID to its <see cref="long"/> value.</summary>
    /// <param name="id">The ID to convert.</param>
    public static implicit operator long(Id id) => id._value;
    /// <summary>Converts a <see cref="long"/> to an ID.</summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator Id(long value) => new(value);

    /// <summary>Parses a string into an ID.</summary>
    /// <param name="s">The string to parse.</param>
    /// <exception cref="FormatException">Thrown when <paramref name="s"/> is not a valid 64-bit integer.</exception>
    public static explicit operator Id(string s)
    {
        if (!long.TryParse(s, out long value))
            throw new FormatException($"Invalid ID: {s}");
        return new Id(value);
    }

    /// <summary>Tries to parse a string into an ID.</summary>
    /// <param name="s">The string to parse, or <see langword="null"/>.</param>
    /// <param name="id">The parsed ID, or 0 when parsing fails.</param>
    /// <returns><see langword="true"/> if <paramref name="s"/> is a valid 64-bit integer; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? s, out Id id)
    {
        if (!long.TryParse(s, out long value))
        {
            id = 0;
            return false;
        }
        id = value;
        return true;
    }

    /// <summary>Returns the ID value as a decimal string.</summary>
    public override string ToString() => _value.ToString();

    /// <summary>Compares this ID with another ID by value.</summary>
    /// <param name="other">The ID to compare with.</param>
    /// <returns>A negative number, zero or a positive number when this ID is less than, equal to or greater than <paramref name="other"/>.</returns>
    public int CompareTo(Id other) => _value.CompareTo(other._value);

    /// <summary>Compares this ID with an object that must be an <see cref="Id"/>.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>A negative number, zero or a positive number when this ID is less than, equal to or greater than <paramref name="obj"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not an <see cref="Id"/>.</exception>
    public int CompareTo(object? obj)
    {
        if (obj is not Id other)
            throw new ArgumentException($"Object must be of type {typeof(Id).FullName}.", nameof(obj));
        return CompareTo(other);
    }
}
