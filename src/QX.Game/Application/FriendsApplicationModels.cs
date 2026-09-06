using Qx.Game.Snapshots;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record FriendsListRequest(
    string Query = "",
    bool OnlineOnly = false,
    int Offset = 0,
    int Limit = 200);

public sealed record FriendsRefreshRequest(
    string Query = "",
    bool OnlineOnly = false,
    int Offset = 0,
    int Limit = 200,
    int TimeoutMilliseconds = 10000);

public sealed record FriendListPage(
    bool Loaded,
    bool Loading,
    bool Stale,
    long Generation,
    long Revision,
    int Total,
    int Matched,
    int Online,
    int UserLimit,
    int NormalLimit,
    int ExtendedLimit,
    IReadOnlyList<FriendCategorySnapshot> Categories,
    int Offset,
    int? NextOffset,
    IReadOnlyList<FriendSnapshot> Friends);

public sealed record FriendsSearchRequest(
    string Query,
    int TimeoutMilliseconds = 10000);

public sealed record FriendsSearchResult(
    string Query,
    IReadOnlyList<UserSearchResult> Friends,
    IReadOnlyList<UserSearchResult> Others);

public sealed record FriendMessageHistoryRequest(
    long AfterSequence = 0,
    int Limit = 100);

public sealed record FriendMessageEntry(
    long Sequence,
    DateTimeOffset ReceivedAtUtc,
    ClientType Client,
    Id ChatId,
    int ContentType,
    string Text,
    int HabbiconId,
    int SecondsSinceSent,
    string MessageId,
    int ConfirmationId,
    Id SenderId,
    string SenderName,
    string SenderFigure,
    bool Offline,
    LegacyCompactConsoleMessage? LegacyCompact);

public sealed record FriendMessageHistoryPage(
    IReadOnlyList<FriendMessageEntry> Entries,
    long RequestedAfterSequence,
    long NextSequence,
    long OldestSequence,
    long LatestSequence,
    bool HasMore,
    bool Gap);

public sealed record FriendMessageSendRequest(Id RecipientId, string Message);

public sealed record FriendRequestSendRequest(string Name);

public sealed record FriendRequestIdsRequest(IReadOnlyList<Id> RequestIds);

public sealed record FriendRequestDeclineRequest(IReadOnlyList<Id> RequestIds);

public sealed record FriendRequestsDeclineAllRequest;

public sealed record FriendRequestsListRequest(int TimeoutMilliseconds = 10000);

public sealed record FriendsRemoveRequest(IReadOnlyList<Id> FriendIds);

public sealed record FriendFollowRequest(Id FriendId);

public sealed record FriendRelationshipSetRequest(
    Id FriendId,
    RelationshipType Relationship);

public sealed record FriendOperationResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    IReadOnlyList<Id> TargetIds,
    string? TargetName = null);

public enum FriendChangeKind
{
    Loaded,
    Added,
    Updated,
    Removed,
    Reset
}

public sealed record FriendChanged(
    FriendChangeKind Kind,
    long Generation,
    long Revision,
    DateTimeOffset ChangedAtUtc,
    FriendSnapshot? Friend);

internal interface IFriendOperations
{
    Task<IReadOnlyCollection<Friend>> EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token);

    void Follow(FriendFollowRequest request, CancellationToken cancellation_token);

    void AcceptRequests(
        FriendRequestIdsRequest request,
        Session expected_session,
        CancellationToken cancellation_token);
}
