using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RoomUsers(IReadOnlyList<Avatar> Avatars) : IParserComposer<RoomUsers>
{
    public static RoomUsers Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomUsers ParseFlash(in PacketReader p) => new(p.ParseArray<Avatar>());

    private static RoomUsers ParseUnity(in PacketReader p) => new(p.ParseArray<Avatar>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomUsers value, in PacketWriter p) =>
        p.ComposeArray(value.Avatars);

    private static void ComposeUnity(RoomUsers value, in PacketWriter p) =>
        p.ComposeArray(value.Avatars);
}
