using Qx.Game.Snapshots;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>Represents a request for a filtered page of the friend list.</summary>
/// <remarks>
/// Used by the <c>friends.list</c> query, which reads the friend list held by the client without
/// sending a request. The friends are ordered online first, then by name ignoring case.
/// </remarks>
/// <param name="Query">The text to match against the name, real name or motto of each friend, ignoring case, or an empty string to match every friend.</param>
/// <param name="OnlineOnly">Whether to return only online friends.</param>
/// <param name="Offset">The zero-based index of the first matching friend to return.</param>
/// <param name="Limit">The maximum number of friends to return, from 1 to 500.</param>
public sealed record FriendsListRequest(
    string Query = "",
    bool OnlineOnly = false,
    int Offset = 0,
    int Limit = 200);

/// <summary>Represents a request to reload the friend list from the server and return a filtered page.</summary>
/// <remarks>
/// Used by the <c>friends.refresh</c> operation. Concurrent callers share one request. When the
/// hotel answered a messenger initialization less than 30 seconds ago, the loaded list is returned
/// without a new request.
/// </remarks>
/// <param name="Query">The text to match against the name, real name or motto of each friend, ignoring case, or an empty string to match every friend.</param>
/// <param name="OnlineOnly">Whether to return only online friends.</param>
/// <param name="Offset">The zero-based index of the first matching friend to return.</param>
/// <param name="Limit">The maximum number of friends to return, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the complete friend list, in milliseconds, from 1 to 120000.</param>
public sealed record FriendsRefreshRequest(
    string Query = "",
    bool OnlineOnly = false,
    int Offset = 0,
    int Limit = 200,
    int TimeoutMilliseconds = 10000);

/// <summary>Represents a filtered page of the friend list.</summary>
/// <remarks>Returned by the <c>friends.list</c> query and the <c>friends.refresh</c> operation.</remarks>
/// <param name="Loaded">Whether the complete friend list has been received.</param>
/// <param name="Loading">Whether the friend list is being received.</param>
/// <param name="Stale">Whether the friend list holds friends that a completed load has not confirmed.</param>
/// <param name="Generation">The load generation, which increases each time a friend list load starts, is abandoned or is reset.</param>
/// <param name="Revision">The friend state revision, which increases with every change.</param>
/// <param name="Total">The total number of friends.</param>
/// <param name="Matched">The number of friends that match the filters.</param>
/// <param name="Online">The number of online friends, counted over the whole list.</param>
/// <param name="UserLimit">The number of friend slots the account has, or 0 when the hotel has not reported it.</param>
/// <param name="NormalLimit">The number of friend slots an account without club membership gets, or 0 when the hotel has not reported it.</param>
/// <param name="ExtendedLimit">The number of friend slots a club account gets, or 0 when the hotel has not reported it.</param>
/// <param name="Categories">The friend list categories, ordered by name, then by id.</param>
/// <param name="Offset">The zero-based index of the first matching friend in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more matching friends.</param>
/// <param name="Friends">The matching friends in the page.</param>
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

/// <summary>Represents a request to search the hotel for users by name.</summary>
/// <remarks>
/// Used by the <c>friends.search</c> operation. A request that times out is sent once more, and the
/// timeout is split across both attempts.
/// </remarks>
/// <param name="Query">The user name or part of a name to search for, which must not be blank.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the search result, in milliseconds, from 1 to 120000.</param>
public sealed record FriendsSearchRequest(
    string Query,
    int TimeoutMilliseconds = 10000);

/// <summary>Represents the result of a user search.</summary>
/// <remarks>Returned by the <c>friends.search</c> operation.</remarks>
/// <param name="Query">The text that was searched for.</param>
/// <param name="Friends">The matching users who are already friends.</param>
/// <param name="Others">The matching users who are not friends.</param>
public sealed record FriendsSearchResult(
    string Query,
    IReadOnlyList<UserSearchResult> Friends,
    IReadOnlyList<UserSearchResult> Others);

/// <summary>Represents a request for a page of the private messages received in the session.</summary>
/// <remarks>
/// Used by the <c>friends.message.history</c> query. The journal keeps the 2000 most recent private
/// messages and is cleared when the hotel connection closes. Pass the returned
/// <see cref="FriendMessageHistoryPage.NextSequence"/> as <paramref name="AfterSequence"/> to read the next page.
/// </remarks>
/// <param name="AfterSequence">The sequence number after which to start, or 0 to start at the oldest retained message.</param>
/// <param name="Limit">The maximum number of messages to return, from 1 to 500.</param>
public sealed record FriendMessageHistoryRequest(
    long AfterSequence = 0,
    int Limit = 100);

/// <summary>Represents a private messenger message received in the session.</summary>
/// <remarks>Returned in <see cref="FriendMessageHistoryPage"/> and published by the <c>friends.message.received</c> event.</remarks>
/// <param name="Sequence">The sequence number of the message in the journal, starting at 1.</param>
/// <param name="ReceivedAtUtc">The UTC time the message was received.</param>
/// <param name="ChatId">The id of the conversation the message belongs to.</param>
/// <param name="ContentType">The content type, 0 for text and 1 for a habbicon.</param>
/// <param name="Text">The message text, or an empty string for a habbicon message.</param>
/// <param name="HabbiconId">The id of the habbicon, or 0 for a text message.</param>
/// <param name="SecondsSinceSent">The number of seconds since the message was sent, greater than 0 when it waited while the user was offline.</param>
/// <param name="MessageId">The message id sent by the hotel.</param>
/// <param name="ConfirmationId">The confirmation id sent by the hotel.</param>
/// <param name="SenderId">The id of the user who sent the message.</param>
/// <param name="SenderName">The name of the user who sent the message.</param>
/// <param name="SenderFigure">The figure string of the user who sent the message.</param>
/// <param name="Offline">Whether the message was waiting rather than sent just now.</param>
/// <param name="LegacyCompact">The legacy compact form of the message, or <see langword="null"/> when the message did not carry it.</param>
public sealed record FriendMessageEntry(
    long Sequence,
    DateTimeOffset ReceivedAtUtc,
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

/// <summary>Represents a page of the private message journal.</summary>
/// <remarks>Returned by the <c>friends.message.history</c> query.</remarks>
/// <param name="Entries">The messages in the page, oldest first.</param>
/// <param name="RequestedAfterSequence">The sequence number the page was requested after.</param>
/// <param name="NextSequence">The sequence number to pass as the next after sequence, which is the last message in the page, the latest sequence when the page is empty because of a gap, or the requested sequence otherwise.</param>
/// <param name="OldestSequence">The sequence number of the oldest retained message, or 0 when the journal is empty.</param>
/// <param name="LatestSequence">The sequence number of the newest message received so far.</param>
/// <param name="HasMore">Whether more messages follow the page.</param>
/// <param name="Gap">Whether messages after <paramref name="RequestedAfterSequence"/> were dropped or cleared, or the sequence is ahead of <paramref name="LatestSequence"/>.</param>
public sealed record FriendMessageHistoryPage(
    IReadOnlyList<FriendMessageEntry> Entries,
    long RequestedAfterSequence,
    long NextSequence,
    long OldestSequence,
    long LatestSequence,
    bool HasMore,
    bool Gap);

/// <summary>Represents a request to send a private message to a friend.</summary>
/// <remarks>Used by the <c>friends.message.send</c> operation, which sends the message and returns without waiting.</remarks>
/// <param name="RecipientId">The id of the friend to send the message to.</param>
/// <param name="Message">The message text, which must not be blank.</param>
public sealed record FriendMessageSendRequest(Id RecipientId, string Message);

/// <summary>Represents a request to send a friend request to a user by name.</summary>
/// <remarks>Used by the <c>friends.request.send</c> operation, which sends the request and returns without waiting.</remarks>
/// <param name="Name">The hotel user name, which must not be blank.</param>
public sealed record FriendRequestSendRequest(string Name);

/// <summary>Represents a request to accept pending friend requests.</summary>
/// <remarks>
/// Used by the <c>friends.request.accept</c> operation, which sends the request and returns without
/// waiting. Duplicate ids are sent once.
/// </remarks>
/// <param name="RequestIds">The ids of the pending friend requests, at least one and at most 65535 distinct values.</param>
public sealed record FriendRequestIdsRequest(IReadOnlyList<Id> RequestIds);

/// <summary>Represents a request to decline selected pending friend requests.</summary>
/// <remarks>
/// Used by the <c>friends.request.decline</c> operation, which sends the request and returns without
/// waiting. Duplicate ids are sent once.
/// </remarks>
/// <param name="RequestIds">The ids of the pending friend requests, at least one and at most 65535 distinct values.</param>
public sealed record FriendRequestDeclineRequest(IReadOnlyList<Id> RequestIds);

/// <summary>Represents a request to decline every pending friend request.</summary>
/// <remarks>Used by the <c>friends.requests.decline_all</c> operation, which sends the request and returns without waiting.</remarks>
public sealed record FriendRequestsDeclineAllRequest;

/// <summary>Represents a request for the pending friend requests.</summary>
/// <remarks>
/// Used by the <c>friends.requests.list</c> operation, which sends a request and waits for the
/// answer. A request that times out is sent once more, and the timeout is split across both attempts.
/// </remarks>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the answer, in milliseconds, from 1 to 120000.</param>
public sealed record FriendRequestsListRequest(int TimeoutMilliseconds = 10000);

/// <summary>Represents a request to remove users from the friend list.</summary>
/// <remarks>
/// Used by the <c>friends.remove</c> operation, which sends the request and returns without
/// waiting. Duplicate ids are sent once.
/// </remarks>
/// <param name="FriendIds">The ids of the friends to remove, at least one and at most 65535 distinct values.</param>
public sealed record FriendsRemoveRequest(IReadOnlyList<Id> FriendIds);

/// <summary>Represents a request to follow a friend to their current room.</summary>
/// <remarks>Used by the <c>friends.follow</c> operation, which sends the request and returns without waiting.</remarks>
/// <param name="FriendId">The id of the friend to follow.</param>
public sealed record FriendFollowRequest(Id FriendId);

/// <summary>Represents a request to change the relationship marker shown for a friend.</summary>
/// <remarks>Used by the <c>friends.relationship.set</c> operation, which sends the request and returns without waiting.</remarks>
/// <param name="FriendId">The id of the friend.</param>
/// <param name="Relationship">The relationship marker to set.</param>
public sealed record FriendRelationshipSetRequest(
    Id FriendId,
    RelationshipType Relationship);

/// <summary>Represents the receipt for a messenger operation that was sent.</summary>
/// <remarks>
/// Returned by the friend operations that send a message without waiting for an answer. A hotel
/// rejection is published later by the <c>friends.operation.failed</c> event.
/// </remarks>
/// <param name="DispatchedAtUtc">The UTC time the operation was sent.</param>
/// <param name="TargetIds">The ids the operation was sent for, without duplicates, or an empty list when it targets a name or every request.</param>
/// <param name="TargetName">The user name the operation was sent for, or <see langword="null"/> when it targets ids.</param>
public sealed record FriendOperationResult(
    DateTimeOffset DispatchedAtUtc,
    IReadOnlyList<Id> TargetIds,
    string? TargetName = null);

/// <summary>Specifies the kind of a friend list change.</summary>
public enum FriendChangeKind
{
    /// <summary>The complete friend list was received.</summary>
    Loaded,
    /// <summary>The hotel added a friend to the friend list.</summary>
    Added,
    /// <summary>The hotel updated a friend in the friend list.</summary>
    Updated,
    /// <summary>The hotel removed a friend from the friend list.</summary>
    Removed,
    /// <summary>The friend state was cleared because the hotel connection closed.</summary>
    Reset
}

/// <summary>Represents a change to the friend list.</summary>
/// <remarks>Published by the <c>friends.changed</c> event.</remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="Generation">The friend list load generation after the change.</param>
/// <param name="Revision">The friend state revision after the change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="Friend">The friend for <see cref="FriendChangeKind.Added"/>, <see cref="FriendChangeKind.Updated"/> and <see cref="FriendChangeKind.Removed"/>; otherwise, <see langword="null"/>.</param>
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
