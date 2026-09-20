using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RoomEntryInfo(Id GuestRoomId, bool Owner) : IParserComposer<RoomEntryInfo>
{
    public static RoomEntryInfo Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RoomEntryInfo ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RoomEntryInfo value, in PacketWriter p)
    {
        p.WriteId(value.GuestRoomId);
        p.WriteBool(value.Owner);
    }
}
