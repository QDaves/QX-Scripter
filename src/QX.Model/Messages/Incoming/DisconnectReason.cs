using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record DisconnectReason(int Reason) : IParserComposer<DisconnectReason>
{
    public static DisconnectReason Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DisconnectReason ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DisconnectReason value, in PacketWriter p) =>
        p.WriteInt(value.Reason);
}
