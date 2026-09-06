using Qx.Messages;
using Qx.Model.Forums;

namespace Qx.Model.Messages.Incoming;

public sealed record UnreadForumsCount(int Count) : IParserComposer<UnreadForumsCount>
{
    public static UnreadForumsCount Parse(in PacketReader p)
    {
        UnreadForumsCount value = ModernWireClients.Parse(in p, ParseFlash, ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(UnreadForumsCount));
        return value;
    }

    private static UnreadForumsCount ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static UnreadForumsCount ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<UnreadForumsCount>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UnreadForumsCount value, in PacketWriter p) =>
        p.WriteInt(value.Count);

    private static void ComposeUnity(UnreadForumsCount value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}
