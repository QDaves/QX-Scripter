using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read entries from the room chat journal.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomChatHistory"/>. The journal keeps the latest 2000 chat
/// messages received in any room, and sequences keep increasing across rooms and sessions.
/// </remarks>
/// <param name="AfterSequence">
/// The sequence to read after, or 0 to read from the oldest retained entry. Must not be negative.
/// </param>
/// <param name="Limit">The maximum number of entries to return, from 1 to 500.</param>
public sealed record RoomChatHistoryRequest(long AfterSequence = 0, int Limit = 100);

/// <summary>
/// Represents a page of entries read from the room chat journal.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomChatHistory"/>. Pass <see cref="Next"/> as the
/// <see cref="RoomChatHistoryRequest.AfterSequence"/> of the next request to continue reading.
/// </remarks>
public sealed record RoomChatHistoryPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoomChatHistoryPage"/> record.
    /// </summary>
    /// <param name="entries">The entries in the page.</param>
    /// <param name="after">The sequence the page was read after.</param>
    /// <param name="next">The sequence to read after for the next page.</param>
    /// <param name="oldest">The sequence of the oldest retained entry, or 0 when the journal is empty.</param>
    /// <param name="latest">The sequence of the latest recorded entry, or 0 when none was recorded.</param>
    /// <param name="hasMore">Whether more entries follow the page.</param>
    /// <param name="gap">Whether entries after the cursor were dropped or the cursor is ahead of the latest entry.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is <see langword="null"/>.</exception>
    public RoomChatHistoryPage(
        IEnumerable<RoomChatEntry> entries,
        long after,
        long next,
        long oldest,
        long latest,
        bool hasMore,
        bool gap)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = Array.AsReadOnly(entries.ToArray());
        After = after;
        Next = next;
        Oldest = oldest;
        Latest = latest;
        HasMore = hasMore;
        Gap = gap;
    }

    /// <summary>
    /// Gets the entries in the page, ordered by sequence.
    /// </summary>
    public IReadOnlyList<RoomChatEntry> Entries { get; }
    /// <summary>
    /// Gets the sequence the page was read after.
    /// </summary>
    public long After { get; }
    /// <summary>
    /// Gets the sequence to read after for the next page.
    /// </summary>
    /// <remarks>
    /// The sequence of the last entry in the page, or the smaller of <see cref="After"/> and
    /// <see cref="Latest"/> when the page is empty.
    /// </remarks>
    public long Next { get; }
    /// <summary>
    /// Gets the sequence of the oldest retained entry, or 0 when the journal is empty.
    /// </summary>
    public long Oldest { get; }
    /// <summary>
    /// Gets the sequence of the latest recorded entry, or 0 when none was recorded.
    /// </summary>
    public long Latest { get; }
    /// <summary>
    /// Gets whether more entries follow the page.
    /// </summary>
    public bool HasMore { get; }
    /// <summary>
    /// Gets whether entries after <see cref="After"/> were dropped from the journal or <see cref="After"/> is ahead of <see cref="Latest"/>.
    /// </summary>
    public bool Gap { get; }
}

/// <summary>
/// Represents a chat message recorded in the room chat journal.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomChatHistory"/> and published by
/// <see cref="ApplicationMemberIds.RoomChatReceived"/> for every talk, shout and whisper message received.
/// </remarks>
public sealed record RoomChatEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoomChatEntry"/> record.
    /// </summary>
    /// <param name="sequence">The journal sequence of the entry.</param>
    /// <param name="receivedAtUtc">The time the message was received.</param>
    /// <param name="roomId">The id of the room the message was received in, or <see langword="null"/> when unknown.</param>
    /// <param name="roomGeneration">The room state generation the message was received in.</param>
    /// <param name="speakerIndex">The room index of the speaking avatar.</param>
    /// <param name="speakerId">The id of the speaker, or <see langword="null"/> when the avatar was not found.</param>
    /// <param name="speakerName">The name of the speaker, or <see langword="null"/> when the avatar was not found.</param>
    /// <param name="speakerType">The avatar type of the speaker, or <see langword="null"/> when the avatar was not found.</param>
    /// <param name="speakerFigure">The figure string of the speaker, or <see langword="null"/> when the avatar was not found.</param>
    /// <param name="chat">The chat message as received from the hotel.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chat"/> is <see langword="null"/>.</exception>
    public RoomChatEntry(
        long sequence,
        DateTimeOffset receivedAtUtc,
        Id? roomId,
        long roomGeneration,
        int speakerIndex,
        Id? speakerId,
        string? speakerName,
        AvatarType? speakerType,
        string? speakerFigure,
        AvatarChat chat)
    {
        ArgumentNullException.ThrowIfNull(chat);
        Sequence = sequence;
        ReceivedAtUtc = receivedAtUtc;
        RoomId = roomId;
        RoomGeneration = roomGeneration;
        SpeakerIndex = speakerIndex;
        SpeakerId = speakerId;
        SpeakerName = speakerName;
        SpeakerType = speakerType;
        SpeakerFigure = speakerFigure;
        Chat = CopyChat(chat);
    }

    /// <summary>
    /// Gets the journal sequence of the entry, counted from 1.
    /// </summary>
    public long Sequence { get; }
    /// <summary>
    /// Gets the time the message was received.
    /// </summary>
    public DateTimeOffset ReceivedAtUtc { get; }
    /// <summary>
    /// Gets the id of the room the message was received in, or <see langword="null"/> when no room id was known.
    /// </summary>
    public Id? RoomId { get; }
    /// <summary>
    /// Gets the room state generation the message was received in.
    /// </summary>
    public long RoomGeneration { get; }
    /// <summary>
    /// Gets the room index of the speaking avatar.
    /// </summary>
    public int SpeakerIndex { get; }
    /// <summary>
    /// Gets the id of the speaker, or <see langword="null"/> when no avatar with the index was in the room.
    /// </summary>
    public Id? SpeakerId { get; }
    /// <summary>
    /// Gets the name of the speaker, or <see langword="null"/> when no avatar with the index was in the room.
    /// </summary>
    public string? SpeakerName { get; }
    /// <summary>
    /// Gets the avatar type of the speaker, or <see langword="null"/> when no avatar with the index was in the room.
    /// </summary>
    public AvatarType? SpeakerType { get; }
    /// <summary>
    /// Gets the figure string of the speaker, or <see langword="null"/> when no avatar with the index was in the room.
    /// </summary>
    public string? SpeakerFigure { get; }
    /// <summary>
    /// Gets the chat message as received from the hotel.
    /// </summary>
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

/// <summary>
/// Represents a request to whisper a chat message to a user in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomChatWhisper"/>. The room must be ready.
/// </remarks>
/// <param name="Recipient">The name of the user in the room to whisper to. Must not be blank.</param>
/// <param name="Message">The message text. Must not be blank.</param>
/// <param name="Bubble">The chat bubble style.</param>
public sealed record RoomChatWhisperRequest(string Recipient, string Message, int Bubble = 0);

/// <summary>
/// Represents a request to send a public chat message in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomChatTalk"/>. The room must be ready.
/// </remarks>
/// <param name="Message">The message text. Must not be blank.</param>
/// <param name="Bubble">The chat bubble style.</param>
public sealed record RoomChatTalkRequest(string Message, int Bubble = 0);

/// <summary>
/// Represents a request to shout a chat message in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomChatShout"/>. The room must be ready.
/// </remarks>
/// <param name="Message">The message text. Must not be blank.</param>
/// <param name="Bubble">The chat bubble style.</param>
public sealed record RoomChatShoutRequest(string Message, int Bubble = 0);

/// <summary>
/// Represents the result of sending a public chat message or shout.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomChatTalk"/> and <see cref="ApplicationMemberIds.RoomChatShout"/>.
/// The message is only sent while the room is ready and its generation is unchanged, and the hotel's
/// response is not awaited.
/// </remarks>
/// <param name="RoomId">The id of the current room, or <see langword="null"/> when no room id is known.</param>
/// <param name="RoomGeneration">The room state generation the message was sent in.</param>
/// <param name="Dispatched">Whether the message was sent.</param>
/// <param name="ServerConfirmed">Whether the hotel confirmed the message, which is always <see langword="false"/>.</param>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
public sealed record RoomChatSendResult(
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);

/// <summary>
/// Represents the result of sending a whisper.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomChatWhisper"/>. The message is only sent while the
/// room is ready and its generation is unchanged, and the hotel's response is not awaited.
/// </remarks>
/// <param name="RoomId">The id of the current room, or <see langword="null"/> when no room id is known.</param>
/// <param name="RoomGeneration">The room state generation the message was sent in.</param>
/// <param name="Dispatched">Whether the message was sent.</param>
/// <param name="ServerConfirmed">Whether the hotel confirmed the message, which is always <see langword="false"/>.</param>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
public sealed record RoomChatWhisperResult(
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
