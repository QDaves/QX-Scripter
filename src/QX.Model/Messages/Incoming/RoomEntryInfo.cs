using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RoomEntryInfo(Id GuestRoomId, bool Owner) : IParserComposer<RoomEntryInfo>
{
    public static RoomEntryInfo Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomEntryInfo ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadBool());

    private static RoomEntryInfo ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomEntryInfo value, in PacketWriter p)
    {
        p.WriteId(value.GuestRoomId);
        p.WriteBool(value.Owner);
    }

    private static void ComposeUnity(RoomEntryInfo value, in PacketWriter p)
    {
        p.WriteId(value.GuestRoomId);
        p.WriteBool(value.Owner);
    }
}
