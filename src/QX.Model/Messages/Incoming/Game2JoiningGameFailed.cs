using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record Game2JoiningGameFailed(int Reason) : IParserComposer<Game2JoiningGameFailed>
{
    public static Game2JoiningGameFailed Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(Reason);
}
