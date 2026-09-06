using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public enum ChatType
{
    Talk,
    Shout,
    Whisper
}

public readonly record struct ChatLink(string Key, string Url, bool Flag);

/// <param name="Index">The sender's room index.</param>
/// <param name="Message">The chat message.</param>
/// <param name="Gesture">The gesture carried with the message.</param>
/// <param name="BubbleStyle">The chat bubble style.</param>
/// <param name="Links">The links embedded in the message.</param>
/// <param name="TrackingId">The message tracking identifier.</param>
/// <param name="Type">Whether the message is regular chat, a shout, or a whisper.</param>
/// <param name="ChatId">
/// The trailing identifier the hotel appends after the tracking id, or <see langword="null"/> when
/// the message ended there.
/// </param>
/// <param name="WhisperId">
/// The second trailing identifier, which only <c>Whisper</c> carries. Never set unless
/// <paramref name="ChatId"/> is, because the two are positional.
/// </param>
/// <remarks>
/// Both trailing identifiers are read whenever the bytes are there, on either client flavour. They
/// are not Unity-only: the Unity schema marks them <c>HasRemaining</c>, meaning "present if bytes
/// remain" rather than "present on Unity", and hotel WIN63-202607011411 sends the first one on Flash
/// as well. The Flash client stops reading after the tracking id and silently ignores the rest, so
/// its decompiled parser gives the field no name and cannot be used to rule it out - gating these
/// reads on <see cref="ClientType.Unity"/> made every Flash chat message fail with four unparsed
/// bytes, which silently killed every consumer of the room chat event.
/// </remarks>
public sealed record AvatarChat(
    int Index,
    string Message,
    int Gesture,
    int BubbleStyle,
    IReadOnlyList<ChatLink> Links,
    int TrackingId,
    ChatType Type = ChatType.Talk,
    int? ChatId = null,
    int? WhisperId = null) : IParserComposer<AvatarChat>
{
    public static AvatarChat Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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
        int? whisper_id = p.Available >= 4 ? p.ReadInt() : null;
        ChatType type = whisper_id.HasValue ? ChatType.Whisper : ChatType.Talk;
        return new AvatarChat(index, message, gesture, bubble_style, links, tracking_id, type, chat_id, whisper_id);
    }

    private static AvatarChat ParseUnity(in PacketReader p)
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
        int? whisper_id = p.Available >= 4 ? p.ReadInt() : null;
        ChatType type = whisper_id.HasValue ? ChatType.Whisper : ChatType.Talk;
        return new AvatarChat(index, message, gesture, bubble_style, links, tracking_id, type, chat_id, whisper_id);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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
                p.WriteInt(value.WhisperId.Value);
        }
    }

    private static void ComposeUnity(AvatarChat value, in PacketWriter p)
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
                p.WriteInt(value.WhisperId.Value);
        }
    }
}
