using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RespectNotification(Id RespectedUserId, int TotalRespect)
    : IParserComposer<RespectNotification>
{
    public static RespectNotification Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RespectNotification ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RespectNotification value, in PacketWriter p)
    {
        p.WriteId(value.RespectedUserId);
        p.WriteInt(value.TotalRespect);
    }
}
