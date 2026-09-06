using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GuideSessionMessage(string ChatMessage, Id SenderId) : IParserComposer<GuideSessionMessage>
{
    public static GuideSessionMessage Parse(in PacketReader p) => new(p.ReadString(), p.ReadId());

    public void Compose(in PacketWriter p)
    {
        p.WriteString(ChatMessage);
        p.WriteId(SenderId);
    }
}
