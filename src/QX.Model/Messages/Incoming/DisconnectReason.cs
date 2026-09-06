using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record DisconnectReason(int Reason) : IParserComposer<DisconnectReason>
{
    public static DisconnectReason Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static DisconnectReason ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static DisconnectReason ParseUnity(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(DisconnectReason value, in PacketWriter p) =>
        p.WriteInt(value.Reason);

    private static void ComposeUnity(DisconnectReason value, in PacketWriter p) =>
        p.WriteInt(value.Reason);
}
