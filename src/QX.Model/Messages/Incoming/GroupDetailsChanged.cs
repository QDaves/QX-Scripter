using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GroupDetailsChanged(Id GroupId) : IParserComposer<GroupDetailsChanged>
{
    public static GroupDetailsChanged Parse(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) => p.WriteId(GroupId);
}
