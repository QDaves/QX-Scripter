using Qx.Model.Forums;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

public sealed record ForumStateRequest(long? SnapshotRevision = null);

public sealed record ForumStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long SnapshotRevision,
    ForumSnapshot Snapshot);

public sealed record ForumDetailsRequest(
    Id GroupId,
    long? ExpectedSessionGeneration = null);

public sealed record ForumListRequest(
    ForumListCode ListCode,
    int StartIndex = 0,
    int MaxCount = 20,
    long? ExpectedSessionGeneration = null);

public sealed record ForumThreadsRequest(
    Id GroupId,
    int StartIndex = 0,
    int MaxCount = 20,
    long? ExpectedSessionGeneration = null);

public sealed record ForumMessagesRequest(
    Id GroupId,
    Id ThreadId,
    int StartIndex = 0,
    int MaxCount = 20,
    long? ExpectedSessionGeneration = null);

public sealed record ForumThreadRequest(
    Id GroupId,
    Id ThreadId,
    long? ExpectedSessionGeneration = null);

public sealed record ForumUnreadRequest(long? ExpectedSessionGeneration = null);

public sealed record ForumListRefreshRequest(
    ForumListCode ListCode,
    int StartIndex = 0,
    int MaxCount = 20,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record ForumListRefreshResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    ForumsList Page);

public sealed record ForumThreadsRefreshRequest(
    Id GroupId,
    int StartIndex = 0,
    int MaxCount = 20,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record ForumThreadsRefreshResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    ForumThreads Page);

public sealed record ForumMessagesRefreshRequest(
    Id GroupId,
    Id ThreadId,
    int StartIndex = 0,
    int MaxCount = 20,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record ForumMessagesRefreshResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    ThreadMessages Page);

public sealed record ForumDetailsRefreshRequest(
    Id GroupId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record ForumDetailsRefreshResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    ForumDetails Details);

public sealed record ForumThreadRefreshRequest(
    Id GroupId,
    Id ThreadId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record ForumThreadRefreshResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    Qx.Model.Forums.ForumThread Thread);

public sealed record ForumUnreadRefreshRequest(
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record ForumUnreadRefreshResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset ObservedAtUtc,
    int Count);

public sealed record ForumPostActionRequest(
    Id GroupId,
    Id ThreadId,
    string Subject,
    string MessageText,
    long? ExpectedSessionGeneration = null);

public sealed record ForumThreadModerationRequest(
    Id GroupId,
    Id ThreadId,
    int State,
    long? ExpectedSessionGeneration = null);

public sealed record ForumMessageModerationRequest(
    Id GroupId,
    Id ThreadId,
    Id MessageId,
    int State,
    long? ExpectedSessionGeneration = null);

public sealed record ForumSettingsUpdateRequest(
    Id GroupId,
    int ReadLevel,
    int PostMessageLevel,
    int PostThreadLevel,
    int ModerateLevel,
    long? ExpectedSessionGeneration = null);

public sealed record ForumReadMarkersUpdateRequest(
    IReadOnlyList<ForumReadMarker> Markers,
    long? ExpectedSessionGeneration = null)
{
    private IReadOnlyList<ForumReadMarker> markers = Freeze(Markers);

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

public sealed record ForumThreadUpdateRequest(
    Id GroupId,
    Id ThreadId,
    bool IsSticky,
    bool IsLocked,
    long? ExpectedSessionGeneration = null);

public sealed record ForumThreadReportRequest(
    Id GroupId,
    Id ThreadId,
    int CategoryId,
    string Report,
    string FirstContext = "",
    string SecondContext = "",
    long? ExpectedSessionGeneration = null);

public sealed record ForumMessageReportRequest(
    Id GroupId,
    Id ThreadId,
    Id MessageId,
    int CategoryId,
    string Report,
    string FirstContext = "",
    string SecondContext = "",
    long? ExpectedSessionGeneration = null);

public sealed record ForumDispatchResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset DispatchedAtUtc,
    int MessagesDispatched);

public sealed record ForumChanged(
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    ForumSnapshot Snapshot);
