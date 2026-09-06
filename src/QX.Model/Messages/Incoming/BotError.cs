using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record BotError(int ErrorCode) : IParserComposer<BotError>
{
    public static BotError Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(ErrorCode);
}
