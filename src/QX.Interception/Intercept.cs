using Qx;
using Qx.Messages;

namespace Qx.Interception;

public sealed class Intercept
{
    private Packet? _packet;

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

    public int Sequence { get; init; }

    public Direction Direction => Packet.Header.Direction;

    public bool IsBlocked { get; private set; }

    public void Block() => IsBlocked = true;
}
