using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GuestRoomResult(bool EnterRoom, RoomData Data) : IParserComposer<GuestRoomResult>
{
    public RoomResultDetails? Details { get; init; }

    public static GuestRoomResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GuestRoomResult ParseFlash(in PacketReader p)
    {
        return new GuestRoomResult(p.ReadBool(), p.Parse<RoomData>())
        {
            Details = p.Parse<RoomResultDetails>()
        };
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GuestRoomResult value, in PacketWriter p)
    {
        p.WriteBool(value.EnterRoom);
        p.Compose(value.Data);
        p.Compose(value.Details ?? new RoomResultDetails { OpeningConnection = false });
    }
}
