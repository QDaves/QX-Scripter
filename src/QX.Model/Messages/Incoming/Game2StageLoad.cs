using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record Game2StageLoad(int GameType) : IParserComposer<Game2StageLoad>
{
    public static Game2StageLoad Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(GameType);
}
