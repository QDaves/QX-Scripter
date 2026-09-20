using Qx.Messages;
using Qx.Model.Forums;

namespace Qx.Model.Messages.Incoming;

public sealed record UnreadForumsCount(int Count) : IParserComposer<UnreadForumsCount>
{
    public static UnreadForumsCount Parse(in PacketReader p)
    {
        UnreadForumsCount value = FlashWire.Parse(in p, ParseFlash);
        ForumProtocol.RequireEmpty(in p, nameof(UnreadForumsCount));
        return value;
    }

    private static UnreadForumsCount ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UnreadForumsCount value, in PacketWriter p) =>
        p.WriteInt(value.Count);
}
