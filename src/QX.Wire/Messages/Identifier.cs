namespace Qx.Messages;

/// <summary>Represents a message identifier, made of a direction and a message name.</summary>
/// <remarks>Identifiers compare message names without regard to case.</remarks>
/// <param name="Direction">The direction of the message.</param>
/// <param name="Name">The message name.</param>
public readonly record struct Identifier(MessageDirection Direction, string Name)
{
    /// <summary>An unknown identifier, with no direction and an empty name.</summary>
    public static readonly Identifier Unknown = new();

    /// <summary>Initializes a new identifier with no direction and an empty name.</summary>
    public Identifier()
        : this(MessageDirection.None, "")
    { }

    /// <summary>Returns a hash code that ignores the case of the name.</summary>
    public override int GetHashCode() =>
        HashCode.Combine(Direction, StringComparer.OrdinalIgnoreCase.GetHashCode(Name));

    /// <summary>Gets whether this identifier has the same direction and name as another, ignoring the case of the name.</summary>
    /// <param name="other">The identifier to compare with.</param>
    public bool Equals(Identifier other) =>
        Direction == other.Direction &&
        StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name);

    /// <summary>Returns the name, optionally with a direction prefix.</summary>
    /// <param name="includeDirection">Whether to prefix <c>in:</c> or <c>out:</c> for the direction. An identifier with no direction or with <see cref="MessageDirection.Both"/> gets no direction prefix.</param>
    /// <returns>The formatted identifier, for example <c>in:Chat</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="includeDirection"/> is <see langword="true"/> and the direction is not a defined <see cref="MessageDirection"/> value.</exception>
    public string ToString(bool includeDirection)
    {
        string result = "";
        if (includeDirection)
            result += Direction switch
            {
                MessageDirection.None or MessageDirection.Both => "",
                MessageDirection.In => "in:",
                MessageDirection.Out => "out:",
                _ => throw new ArgumentOutOfRangeException(nameof(Direction))
            };
        return result + Name;
    }

    /// <summary>Returns the message name.</summary>
    public override string ToString() => ToString(false);

    /// <summary>Converts a direction and name tuple to an identifier.</summary>
    /// <param name="x">The direction and the message name.</param>
    public static implicit operator Identifier((MessageDirection direction, string name) x) => new(x.direction, x.name);
}
