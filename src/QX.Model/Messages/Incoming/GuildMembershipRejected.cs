using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GuildMembershipRejected(Id GuildId, Id UserId) : IParserComposer<GuildMembershipRejected>
{
    public static GuildMembershipRejected Parse(in PacketReader p) => new(p.ReadId(), p.ReadId());

    public void Compose(in PacketWriter p)
    {
        p.WriteId(GuildId);
        p.WriteId(UserId);
    }
}
