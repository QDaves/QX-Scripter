using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Specifies how a chat message was spoken.</summary>
public enum ChatType
{
    /// <summary>A normal chat message.</summary>
    Talk,
    /// <summary>A shouted chat message.</summary>
    Shout,
    /// <summary>A whispered chat message.</summary>
    Whisper
}

/// <summary>Represents a link embedded in a chat message.</summary>
/// <param name="Key">The key of the link in the message.</param>
/// <param name="Url">The URL of the link.</param>
/// <param name="Flag">The boolean flag the server sends with the link.</param>
public readonly record struct ChatLink(string Key, string Url, bool Flag);

/// <summary>
/// Represents the <c>Chat</c>, <c>Shout</c> and <c>Whisper</c> messages, received when an avatar speaks in the room.
/// </summary>
/// <param name="Index">The room index of the avatar that spoke.</param>
/// <param name="Message">The text of the message.</param>
/// <param name="Gesture">The gesture shown with the message.</param>
/// <param name="BubbleStyle">The chat bubble style.</param>
/// <param name="Links">The links embedded in the message.</param>
/// <param name="TrackingId">The tracking identifier sent with the message.</param>
/// <param name="Type">
/// How the message was spoken. Raw parsing defaults to talk; room state assigns the type from the message name.
/// </param>
/// <param name="ChatId">The chat identifier, or <see langword="null"/> when the message does not carry one.</param>
/// <param name="WhisperId">
/// Legacy name for the bubble width override. Use BubbleWidthOverride for its actual meaning.
/// </param>
/// <param name="HideUsername">Whether the bubble hides the username, or <see langword="null"/> when not sent.</param>
/// <param name="Location">The bubble location, or <see langword="null"/> when not sent.</param>
public sealed record AvatarChat(
    int Index,
    string Message,
    int Gesture,
    int BubbleStyle,
    IReadOnlyList<ChatLink> Links,
    int TrackingId,
    ChatType Type = ChatType.Talk,
    int? ChatId = null,
    int? WhisperId = null,
    bool? HideUsername = null,
    Point? Location = null) : IParserComposer<AvatarChat>
{
    public int? BubbleWidthOverride
    {
        get => WhisperId;
        init => WhisperId = value;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarChat Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarChat ParseFlash(in PacketReader p)
    {
        int index = p.ReadInt();
        string message = p.ReadString();
        int gesture = p.ReadInt();
        int bubble_style = p.ReadInt();

        int count = p.ReadLength();
        var links = new ChatLink[count];
        for (int i = 0; i < count; i++)
            links[i] = new ChatLink(p.ReadString(), p.ReadString(), p.ReadBool());

        int tracking_id = p.ReadInt();
        int? chat_id = p.Available >= 4 ? p.ReadInt() : null;
        int? bubble_width = p.Available >= 4 ? p.ReadInt() : null;
        bool? hide_username = p.Available >= 1 ? p.ReadBool() : null;
        Point? location = p.Available >= 8 ? Point.Parse(in p) : null;
        return new AvatarChat(index, message, gesture, bubble_style, links, tracking_id, ChatType.Talk, chat_id, bubble_width,
            hide_username, location);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <remarks>Each optional trailing field is only written when the one before it has a value.</remarks>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarChat value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteString(value.Message);
        p.WriteInt(value.Gesture);
        p.WriteInt(value.BubbleStyle);

        p.WriteLength((Length)value.Links.Count);
        foreach (ChatLink link in value.Links)
        {
            p.WriteString(link.Key);
            p.WriteString(link.Url);
            p.WriteBool(link.Flag);
        }

        p.WriteInt(value.TrackingId);
        if (value.ChatId.HasValue)
        {
            p.WriteInt(value.ChatId.Value);
            if (value.WhisperId.HasValue)
            {
                p.WriteInt(value.WhisperId.Value);
                if (value.HideUsername.HasValue)
                {
                    p.WriteBool(value.HideUsername.Value);
                    if (value.Location is { } location)
                        location.Compose(in p);
                }
            }
        }
    }
}
