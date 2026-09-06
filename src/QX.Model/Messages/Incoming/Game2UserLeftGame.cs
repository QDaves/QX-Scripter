using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record Game2UserLeftGame(Id UserId) : IParserComposer<Game2UserLeftGame>
{
    public static Game2UserLeftGame Parse(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) => p.WriteId(UserId);
}
