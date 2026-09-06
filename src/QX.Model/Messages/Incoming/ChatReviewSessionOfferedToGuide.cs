using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record ChatReviewSessionOfferedToGuide(int AcceptanceTimeout)
    : IParserComposer<ChatReviewSessionOfferedToGuide>
{
    public static ChatReviewSessionOfferedToGuide Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(AcceptanceTimeout);
}
