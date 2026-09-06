using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record RoomChatHistoryRequest(long AfterSequence = 0, int Limit = 100);

public sealed record RoomChatHistoryPage
{
    public RoomChatHistoryPage(
        IEnumerable<RoomChatEntry> entries,
        long after,
        long next,
        long oldest,
        long latest,
        bool has_more,
        bool gap)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = Array.AsReadOnly(entries.ToArray());
        After = after;
        Next = next;
        Oldest = oldest;
        Latest = latest;
        HasMore = has_more;
        Gap = gap;
    }

    public IReadOnlyList<RoomChatEntry> Entries { get; }
    public long After { get; }
    public long Next { get; }
    public long Oldest { get; }
    public long Latest { get; }
    public bool HasMore { get; }
    public bool Gap { get; }
}

public sealed record RoomChatEntry
{
    public RoomChatEntry(
        long sequence,
        DateTimeOffset received_at_utc,
        ClientType client,
        Id? room_id,
        long room_generation,
        int speaker_index,
        Id? speaker_id,
        string? speaker_name,
        AvatarType? speaker_type,
        string? speaker_figure,
        AvatarChat chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        Sequence = sequence;
        ReceivedAtUtc = received_at_utc;
        Client = client;
        RoomId = room_id;
        RoomGeneration = room_generation;
        SpeakerIndex = speaker_index;
        SpeakerId = speaker_id;
        SpeakerName = speaker_name;
        SpeakerType = speaker_type;
        SpeakerFigure = speaker_figure;
        Chat = CopyChat(chat);
    }

    public long Sequence { get; }
    public DateTimeOffset ReceivedAtUtc { get; }
    public ClientType Client { get; }
    public Id? RoomId { get; }
    public long RoomGeneration { get; }
    public int SpeakerIndex { get; }
    public Id? SpeakerId { get; }
    public string? SpeakerName { get; }
    public AvatarType? SpeakerType { get; }
    public string? SpeakerFigure { get; }
    public AvatarChat Chat { get; }

    private static AvatarChat CopyChat(AvatarChat chat)
    {
        IReadOnlyList<ChatLink> links = Array.AsReadOnly(chat.Links.ToArray());
        return new AvatarChat(
            chat.Index,
            chat.Message,
            chat.Gesture,
            chat.BubbleStyle,
            links,
            chat.TrackingId,
            chat.Type,
            chat.ChatId,
            chat.WhisperId);
    }
}

public sealed record RoomChatWhisperRequest(string Recipient, string Message, int Bubble = 0);

public sealed record RoomChatTalkRequest(string Message, int Bubble = 0);

public sealed record RoomChatShoutRequest(string Message, int Bubble = 0);

public sealed record RoomChatSendResult(
    ClientType Client,
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);

public sealed record RoomChatWhisperResult(
    ClientType Client,
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
