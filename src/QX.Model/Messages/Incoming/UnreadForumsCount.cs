using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>UnreadForumsCount</c> message, received with the number of group forums that have unread messages.</summary>
/// <param name="Count">The number of forums with unread messages.</param>
public sealed record UnreadForumsCount(int Count) : IParserComposer<UnreadForumsCount>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UnreadForumsCount Parse(in PacketReader p)
    {
        UnreadForumsCount value = FlashWire.Parse(in p, ParseFlash);
        ForumProtocol.RequireEmpty(in p, nameof(UnreadForumsCount));
        return value;
    }

    private static UnreadForumsCount ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UnreadForumsCount value, in PacketWriter p) =>
        p.WriteInt(value.Count);
}
