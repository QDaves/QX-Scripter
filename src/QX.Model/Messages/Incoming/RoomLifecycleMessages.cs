using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RoomReady(string RoomType, Id RoomId) : IParserComposer<RoomReady>
{
    public static RoomReady Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RoomReady ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadId());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RoomReady value, in PacketWriter p)
    {
        p.WriteString(value.RoomType);
        p.WriteId(value.RoomId);
    }
}

public sealed record RoomForward(Id RoomId) : IParserComposer<RoomForward>
{
    public static RoomForward Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RoomForward ParseFlash(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RoomForward value, in PacketWriter p) =>
        p.WriteId(value.RoomId);
}

public sealed record CloseConnection(short? Reason) : IParserComposer<CloseConnection>
{
    public static CloseConnection Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CloseConnection ParseFlash(in PacketReader p) =>
        new(p.Available >= 2 ? p.ReadShort() : null);

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CloseConnection value, in PacketWriter p)
    {
        if (value.Reason is short reason)
            p.WriteShort(reason);
    }
}
