using Qx.Messages;

namespace Qx.Model;

public sealed record FriendCategory(Id Id, string Name) : IParserComposer<FriendCategory>
{
    public static FriendCategory Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FriendCategory ParseFlash(in PacketReader p) => new(p.ReadId(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FriendCategory value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        p.WriteString(value.Name);
    }
}
