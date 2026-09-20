using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public enum ChatType
{
    Talk,
    Shout,
    Whisper
}

public readonly record struct ChatLink(string Key, string Url, bool Flag);

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
        int? whisper_id = p.Available >= 4 ? p.ReadInt() : null;
        ChatType type = whisper_id.HasValue ? ChatType.Whisper : ChatType.Talk;
        return new AvatarChat(index, message, gesture, bubble_style, links, tracking_id, type, chat_id, whisper_id);
    }

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
                p.WriteInt(value.WhisperId.Value);
        }
    }
}
