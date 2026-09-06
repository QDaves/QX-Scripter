using Qx.Messages;

namespace Qx.Model;

public sealed record FriendCategory(Id Id, string Name) : IParserComposer<FriendCategory>
{
    public static FriendCategory Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FriendCategory ParseFlash(in PacketReader p) => new(p.ReadId(), p.ReadString());

    private static FriendCategory ParseUnity(in PacketReader p) => new(p.ReadId(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FriendCategory value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        p.WriteString(value.Name);
    }

    private static void ComposeUnity(FriendCategory value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        p.WriteString(value.Name);
    }
}
