using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

public sealed record EarningStatusRequest : IParserComposer<EarningStatusRequest>
{
    public static EarningStatusRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static EarningStatusRequest ParseFlash(in PacketReader p) => ParseEmpty(in p);

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(EarningStatusRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static EarningStatusRequest ParseEmpty(in PacketReader p)
    {
        EarningWire.RequireEmpty(in p, nameof(EarningStatusRequest));
        return new();
    }
}

public sealed record EarningClaimRequest(EarningCategory Category)
    : IParserComposer<EarningClaimRequest>
{
    public static EarningClaimRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static EarningClaimRequest ParseFlash(in PacketReader p) => ParseMessage(in p);

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(EarningClaimRequest value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static EarningClaimRequest ParseMessage(in PacketReader p)
    {
        EarningWire.RequireRemaining(in p, sizeof(byte), 0, nameof(EarningClaimRequest));
        var value = new EarningClaimRequest((EarningCategory)(sbyte)p.ReadByte());
        EarningWire.RequireEmpty(in p, nameof(EarningClaimRequest));
        return value;
    }

    private static void ComposeMessage(EarningClaimRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteByte(unchecked((byte)(sbyte)value.Category));
    }
}
