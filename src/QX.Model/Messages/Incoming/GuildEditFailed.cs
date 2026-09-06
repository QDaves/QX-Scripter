using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GuildEditFailed(int Reason) : IParserComposer<GuildEditFailed>
{
    public static GuildEditFailed Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(Reason);
}
