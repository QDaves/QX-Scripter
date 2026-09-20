using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record HandItemReceived(Id GiverId, int HandItemType) : IParserComposer<HandItemReceived>
{
    public static HandItemReceived Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static HandItemReceived ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(HandItemReceived value, in PacketWriter p)
    {
        p.WriteInt(checked((int)value.GiverId));
        p.WriteInt(value.HandItemType);
    }
}
