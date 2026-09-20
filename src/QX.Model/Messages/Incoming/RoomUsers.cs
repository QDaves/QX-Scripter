using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RoomUsers(IReadOnlyList<Avatar> Avatars) : IParserComposer<RoomUsers>
{
    public static RoomUsers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RoomUsers ParseFlash(in PacketReader p) => new(p.ParseArray<Avatar>());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RoomUsers value, in PacketWriter p) =>
        p.ComposeArray(value.Avatars);
}
