using Qx;
using Qx.Messages;

namespace Qx.Interception;

/// <summary>Represents an intercepted packet that callbacks can read, replace or block.</summary>
public sealed class Intercept
{
    private Packet? _packet;

    /// <summary>Gets or sets the intercepted packet.</summary>
    /// <remarks>
    /// Setting a new packet replaces the one that is forwarded. A new packet without a parser context
    /// takes the context of the packet it replaces.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    public required Packet Packet
    {
        get => _packet ?? throw new InvalidOperationException("The intercepted packet is unavailable.");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Context ??= _packet?.Context;
            _packet = value;
        }
    }

    /// <summary>Gets the sequence number the interceptor assigned to the packet.</summary>
    public int Sequence { get; init; }

    /// <summary>Gets the direction of the packet, taken from its header.</summary>
    public MessageDirection Direction => Packet.Header.Direction;

    /// <summary>Gets whether the packet is blocked and will not be forwarded.</summary>
    public bool IsBlocked { get; private set; }

    /// <summary>Blocks the packet so that it is not forwarded.</summary>
    public void Block() => IsBlocked = true;
}
