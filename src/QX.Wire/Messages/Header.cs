namespace Qx.Messages;

/// <summary>Represents a message header, made of a direction and the numeric message ID.</summary>
/// <remarks>Header values are assigned per client build, so resolve them through a message manager instead of hard-coding them.</remarks>
/// <param name="Direction">The direction of the message.</param>
/// <param name="Value">The numeric message ID in the connected client build.</param>
public readonly record struct Header(MessageDirection Direction, short Value)
{
    /// <summary>An unknown header, with <see cref="MessageDirection.None"/> and a value of 0.</summary>
    public static readonly Header Unknown = new();

    /// <summary>Converts a direction and value tuple to a header.</summary>
    /// <param name="x">The direction and the numeric message ID.</param>
    public static implicit operator Header((MessageDirection direction, short value) x) => new(x.direction, x.value);
}
