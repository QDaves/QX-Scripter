using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

/// <summary>Represents a request for the forum state view.</summary>
/// <remarks>
/// Used by the <c>forums.state</c> query. The application keeps the four most recent forum
/// snapshots of the current hotel session, and a retained snapshot can no longer be read once it is
/// dropped or the session changes.
/// </remarks>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.</param>
public sealed record ForumStateRequest(long? SnapshotRevision = null);

/// <summary>Represents the forum data received in the session, read from one snapshot.</summary>
/// <remarks>
/// Returned by the <c>forums.state</c> query. The forum cache can also be read while no hotel
/// session is active, in which case <paramref name="Connected"/> is <see langword="false"/>.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to an active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the view was read from.</param>
/// <param name="Snapshot">The forum data received in the session.</param>
public sealed record ForumStateView(
    bool Connected,
    long SessionGeneration,
    long SnapshotRevision,
    ForumSnapshot Snapshot);

/// <summary>Represents a request that sends a forum details request without waiting for the answer.</summary>
/// <remarks>
/// Used by the <c>forums.details.request</c> operation. The answer, if any, updates the forum cache
/// and is published by the <c>forums.changed</c> event.
/// </remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumDetailsRequest(
    Id GroupId,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request that sends a forum directory page request without waiting for the answer.</summary>
/// <remarks>
/// Used by the <c>forums.list.request</c> operation. The answer, if any, updates the forum cache
/// and is published by the <c>forums.changed</c> event.
/// </remarks>
/// <param name="ListCode">The forum directory list to read.</param>
/// <param name="StartIndex">The zero-based index of the first forum on the page.</param>
/// <param name="MaxCount">The maximum number of forums to return, at least 1.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumListRequest(
    ForumListCode ListCode,
    int StartIndex = 0,
    int MaxCount = 20,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request that sends a forum thread page request without waiting for the answer.</summary>
/// <remarks>
/// Used by the <c>forums.threads.request</c> operation. The answer, if any, updates the forum cache
/// and is published by the <c>forums.changed</c> event.
/// </remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="StartIndex">The zero-based index of the first thread on the page.</param>
/// <param name="MaxCount">The maximum number of threads to return, at least 1.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumThreadsRequest(
    Id GroupId,
    int StartIndex = 0,
    int MaxCount = 20,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request that sends a forum message page request without waiting for the answer.</summary>
/// <remarks>
/// Used by the <c>forums.messages.request</c> operation. The answer, if any, updates the forum cache
/// and is published by the <c>forums.changed</c> event.
/// </remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the thread to read. The Flash client requires a 32-bit value.</param>
/// <param name="StartIndex">The zero-based index of the first message on the page.</param>
/// <param name="MaxCount">The maximum number of messages to return, at least 1.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumMessagesRequest(
    Id GroupId,
    Id ThreadId,
    int StartIndex = 0,
    int MaxCount = 20,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request that sends a forum thread request without waiting for the answer.</summary>
/// <remarks>
/// Used by the <c>forums.thread.request</c> operation. The answer, if any, updates the forum cache
/// and is published by the <c>forums.changed</c> event.
/// </remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the thread. The Flash client requires a 32-bit value.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumThreadRequest(
    Id GroupId,
    Id ThreadId,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request that sends an unread forum count request without waiting for the answer.</summary>
/// <remarks>
/// Used by the <c>forums.unread.request</c> operation. The answer, if any, updates the forum cache
/// and is published by the <c>forums.changed</c> event.
/// </remarks>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumUnreadRequest(long? ExpectedSessionGeneration = null);

/// <summary>Represents a request for a page of the forum directory.</summary>
/// <remarks>
/// Used by the <c>forums.list.refresh</c> operation. The call sends one request and waits for the
/// response with the same list code and start index.
/// </remarks>
/// <param name="ListCode">The forum directory list to read.</param>
/// <param name="StartIndex">The zero-based index of the first forum on the page.</param>
/// <param name="MaxCount">The maximum number of forums to return, at least 1.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumListRefreshRequest(
    ForumListCode ListCode,
    int StartIndex = 0,
    int MaxCount = 20,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a forum directory page refresh.</summary>
/// <remarks>Returned by the <c>forums.list.refresh</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="ObservedAtUtc">The UTC time the response was received.</param>
/// <param name="Page">The forum directory page the server returned.</param>
public sealed record ForumListRefreshResult(
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    ForumsList Page);

/// <summary>Represents a request for a page of threads in a group forum.</summary>
/// <remarks>
/// Used by the <c>forums.threads.refresh</c> operation. The call sends one request and waits for the
/// response with the same group id and start index.
/// </remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="StartIndex">The zero-based index of the first thread on the page.</param>
/// <param name="MaxCount">The maximum number of threads to return, at least 1.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumThreadsRefreshRequest(
    Id GroupId,
    int StartIndex = 0,
    int MaxCount = 20,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a forum thread page refresh.</summary>
/// <remarks>Returned by the <c>forums.threads.refresh</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="ObservedAtUtc">The UTC time the response was received.</param>
/// <param name="Page">The thread page the server returned.</param>
public sealed record ForumThreadsRefreshResult(
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    ForumThreads Page);

/// <summary>Represents a request for a page of messages in a forum thread.</summary>
/// <remarks>
/// Used by the <c>forums.messages.refresh</c> operation. The call sends one request and waits for
/// the response with the same group id, thread id and start index.
/// </remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the thread to read. The Flash client requires a 32-bit value.</param>
/// <param name="StartIndex">The zero-based index of the first message on the page.</param>
/// <param name="MaxCount">The maximum number of messages to return, at least 1.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumMessagesRefreshRequest(
    Id GroupId,
    Id ThreadId,
    int StartIndex = 0,
    int MaxCount = 20,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a forum message page refresh.</summary>
/// <remarks>Returned by the <c>forums.messages.refresh</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="ObservedAtUtc">The UTC time the response was received.</param>
/// <param name="Page">The message page the server returned.</param>
public sealed record ForumMessagesRefreshResult(
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    ThreadMessages Page);

/// <summary>Represents a request for the details of a group forum.</summary>
/// <remarks>
/// Used by the <c>forums.details.refresh</c> operation. The call sends one request and waits for the
/// details of the same group.
/// </remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumDetailsRefreshRequest(
    Id GroupId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a forum details refresh.</summary>
/// <remarks>Returned by the <c>forums.details.refresh</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="ObservedAtUtc">The UTC time the response was received.</param>
/// <param name="Details">The forum details the server returned.</param>
public sealed record ForumDetailsRefreshResult(
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    ForumDetails Details);

/// <summary>Represents a request for a single forum thread.</summary>
/// <remarks>
/// Used by the <c>forums.thread.refresh</c> operation. The call sends one request and waits for a
/// thread update with the same group id and thread id.
/// </remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the thread. The Flash client requires a 32-bit value.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumThreadRefreshRequest(
    Id GroupId,
    Id ThreadId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a forum thread refresh.</summary>
/// <remarks>Returned by the <c>forums.thread.refresh</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="ObservedAtUtc">The UTC time the response was received.</param>
/// <param name="Thread">The forum thread the server returned.</param>
public sealed record ForumThreadRefreshResult(
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    ForumThread Thread);

/// <summary>Represents a request for the number of forums with unread messages.</summary>
/// <remarks>
/// Used by the <c>forums.unread.refresh</c> operation. The call sends one request and accepts the
/// next unread count the server sends.
/// </remarks>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumUnreadRefreshRequest(
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of an unread forum count refresh.</summary>
/// <remarks>Returned by the <c>forums.unread.refresh</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="ObservedAtUtc">The UTC time the response was received.</param>
/// <param name="Count">The number of forums with unread messages.</param>
public sealed record ForumUnreadRefreshResult(
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    int Count);

/// <summary>Represents a request to post a message to a forum thread or to start a new thread.</summary>
/// <remarks>Used by the <c>forums.post</c> operation, which sends the post and returns without waiting.</remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the thread to reply to, or 0 to start a new thread. The Flash client requires a 32-bit value.</param>
/// <param name="Subject">The subject of a new thread.</param>
/// <param name="MessageText">The text to post.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumPostActionRequest(
    Id GroupId,
    Id ThreadId,
    string Subject,
    string MessageText,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request to hide or restore a forum thread.</summary>
/// <remarks>Used by the <c>forums.thread.moderate</c> operation, which sends the request and returns without waiting.</remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the thread to moderate. The Flash client requires a 32-bit value.</param>
/// <param name="State">The new moderation state, 0 for default, 1 for restored, 10 for hidden by a forum admin and 20 for hidden by staff.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumThreadModerationRequest(
    Id GroupId,
    Id ThreadId,
    int State,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request to hide or restore a forum message.</summary>
/// <remarks>Used by the <c>forums.message.moderate</c> operation, which sends the request and returns without waiting.</remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the thread the message belongs to. The Flash client requires a 32-bit value.</param>
/// <param name="MessageId">The id of the message to moderate. The Flash client requires a 32-bit value.</param>
/// <param name="State">The new moderation state, 0 for default, 1 for restored, 10 for hidden by a forum admin and 20 for hidden by staff.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumMessageModerationRequest(
    Id GroupId,
    Id ThreadId,
    Id MessageId,
    int State,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request to change the permission levels of a group forum.</summary>
/// <remarks>
/// Used by the <c>forums.settings.update</c> operation, which sends the request and returns without
/// waiting. All four levels are sent together, and levels run from 0, the least restrictive, to 3.
/// </remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ReadLevel">The permission level required to read the forum.</param>
/// <param name="PostMessageLevel">The permission level required to reply to threads.</param>
/// <param name="PostThreadLevel">The permission level required to start threads.</param>
/// <param name="ModerateLevel">The permission level required to moderate the forum.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumSettingsUpdateRequest(
    Id GroupId,
    int ReadLevel,
    int PostMessageLevel,
    int PostThreadLevel,
    int ModerateLevel,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request to update the read markers of group forums.</summary>
/// <remarks>Used by the <c>forums.read_markers.update</c> operation, which sends the request and returns without waiting.</remarks>
/// <param name="Markers">The read markers, one per forum, at most 65535 entries.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumReadMarkersUpdateRequest(
    IReadOnlyList<ForumReadMarker> Markers,
    long? ExpectedSessionGeneration = null)
{
    private IReadOnlyList<ForumReadMarker> markers = Freeze(Markers);

    /// <summary>Gets the read markers, one per forum.</summary>
    /// <remarks>
    /// The list is copied when set. A <see langword="null"/> list throws <see cref="ArgumentNullException"/>,
    /// and a list with more than 65535 entries throws <see cref="ArgumentOutOfRangeException"/>.
    /// </remarks>
    public IReadOnlyList<ForumReadMarker> Markers
    {
        get => markers;
        init => markers = Freeze(value);
    }

    private static IReadOnlyList<ForumReadMarker> Freeze(
        IReadOnlyList<ForumReadMarker> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(values));
        return Array.AsReadOnly(values.ToArray());
    }
}

/// <summary>Represents a request to change the sticky and locked flags of a forum thread.</summary>
/// <remarks>Used by the <c>forums.thread.update</c> operation, which sends the request and returns without waiting.</remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the thread. The Flash client requires a 32-bit value.</param>
/// <param name="IsSticky">Whether the thread is pinned to the top of the thread list.</param>
/// <param name="IsLocked">Whether the thread rejects further replies.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumThreadUpdateRequest(
    Id GroupId,
    Id ThreadId,
    bool IsSticky,
    bool IsLocked,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a call for help that reports a forum thread to the moderators.</summary>
/// <remarks>Used by the <c>forums.thread.report</c> operation, which sends the report and returns without waiting.</remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the reported thread. The Flash client requires a 32-bit value.</param>
/// <param name="CategoryId">The id of the call for help category.</param>
/// <param name="Report">The report text.</param>
/// <param name="FirstContext">The first context string sent with the report.</param>
/// <param name="SecondContext">The second context string sent with the report.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumThreadReportRequest(
    Id GroupId,
    Id ThreadId,
    int CategoryId,
    string Report,
    string FirstContext = "",
    string SecondContext = "",
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a call for help that reports a forum message to the moderators.</summary>
/// <remarks>Used by the <c>forums.message.report</c> operation, which sends the report and returns without waiting.</remarks>
/// <param name="GroupId">The id of the group that owns the forum. The Flash client requires a 32-bit value.</param>
/// <param name="ThreadId">The id of the thread the message belongs to. The Flash client requires a 32-bit value.</param>
/// <param name="MessageId">The id of the reported message. The Flash client requires a 32-bit value.</param>
/// <param name="CategoryId">The id of the call for help category.</param>
/// <param name="Report">The report text.</param>
/// <param name="FirstContext">The first context string sent with the report.</param>
/// <param name="SecondContext">The second context string sent with the report.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record ForumMessageReportRequest(
    Id GroupId,
    Id ThreadId,
    Id MessageId,
    int CategoryId,
    string Report,
    string FirstContext = "",
    string SecondContext = "",
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the receipt for a forum request or action that was sent.</summary>
/// <remarks>
/// Returned by the forum operations that send a message without waiting for an answer, such as
/// <c>forums.post</c> and <c>forums.threads.request</c>.
/// </remarks>
/// <param name="SessionGeneration">The generation of the hotel session the message was sent in.</param>
/// <param name="DispatchedAtUtc">The UTC time the message was sent.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record ForumDispatchResult(
    long SessionGeneration,
    DateTimeOffset DispatchedAtUtc,
    int MessagesDispatched);

/// <summary>Represents a change to the forum data received in the session.</summary>
/// <remarks>
/// Published by the <c>forums.changed</c> event each time a forum message from the server is
/// applied and when the forum data is cleared.
/// </remarks>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session the forum data belongs to.</param>
/// <param name="Snapshot">The forum data after the change.</param>
public sealed record ForumChanged(
    DateTimeOffset ChangedAtUtc,
    bool Connected,
    long SessionGeneration,
    ForumSnapshot Snapshot);
