using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record HandItemReceived(Id GiverId, int HandItemType) : IParserComposer<HandItemReceived>
{
    public static HandItemReceived Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static HandItemReceived ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static HandItemReceived ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(HandItemReceived value, in PacketWriter p)
    {
        p.WriteInt(checked((int)value.GiverId));
        p.WriteInt(value.HandItemType);
    }

    private static void ComposeUnity(HandItemReceived value, in PacketWriter p)
    {
        p.WriteInt(checked((int)value.GiverId));
        p.WriteInt(value.HandItemType);
    }
}
