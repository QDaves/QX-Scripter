using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record MiniMailUnreadCount(int Count) : IParserComposer<MiniMailUnreadCount>
{
    public static MiniMailUnreadCount Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(Count);
}
