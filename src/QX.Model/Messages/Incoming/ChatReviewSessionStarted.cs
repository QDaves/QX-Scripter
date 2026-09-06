using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record ChatReviewSessionStarted(int VotingTimeout, string ChatRecord)
    : IParserComposer<ChatReviewSessionStarted>
{
    public static ChatReviewSessionStarted Parse(in PacketReader p) => new(p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(VotingTimeout);
        p.WriteString(ChatRecord);
    }
}
