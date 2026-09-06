using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record FriendFurniCancelLock(Id StuffId) : IParserComposer<FriendFurniCancelLock>
{
    public static FriendFurniCancelLock Parse(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) => p.WriteId(StuffId);
}
