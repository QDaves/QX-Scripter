using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RespectNotification(Id RespectedUserId, int TotalRespect)
    : IParserComposer<RespectNotification>
{
    public static RespectNotification Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RespectNotification ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    private static RespectNotification ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RespectNotification value, in PacketWriter p)
    {
        p.WriteId(value.RespectedUserId);
        p.WriteInt(value.TotalRespect);
    }

    private static void ComposeUnity(RespectNotification value, in PacketWriter p)
    {
        p.WriteId(value.RespectedUserId);
        p.WriteInt(value.TotalRespect);
    }
}
