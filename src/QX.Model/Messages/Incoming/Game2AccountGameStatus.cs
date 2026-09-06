using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record Game2AccountGameStatus(int GameTypeId, int FreeGamesLeft, int GamesPlayedTotal)
    : IParserComposer<Game2AccountGameStatus>
{
    public static Game2AccountGameStatus Parse(in PacketReader p) => new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(GameTypeId);
        p.WriteInt(FreeGamesLeft);
        p.WriteInt(GamesPlayedTotal);
    }
}
