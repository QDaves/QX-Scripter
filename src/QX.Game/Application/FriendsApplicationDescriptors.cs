using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class FriendsApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor List { get; } = new(
        ApplicationMemberIds.FriendsList,
        "Friends",
        "Reads a filtered page from the current immutable friend-list snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(FriendsListRequest),
        typeof(FriendListPage),
        ListParameters(200),
        state_effects: [new(ApplicationStateKey.FriendsLoaded, ApplicationStateEffectKind.Reads)],
        messages: ListMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Refresh { get; } = new(
        ApplicationMemberIds.FriendsRefresh,
        "Refresh friends",
        "Loads the active session's complete friend list and returns a filtered page.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(FriendsRefreshRequest),
        typeof(FriendListPage),
        [.. ListParameters(200), TimeoutParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.FriendsLoaded, ApplicationStateEffectKind.Changes)],
        [
            new(MessageKeys.Friends.InitializeRequest, Direction.Out, ApplicationMessageRole.Send),
            .. ListMessages()
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor Search { get; } = new(
        ApplicationMemberIds.FriendsSearch,
        "Search users",
        "Searches the active hotel for users and separates existing friends from other results.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(FriendsSearchRequest),
        typeof(FriendsSearchResult),
        [RequiredText("query", "User name or fragment to search for."), TimeoutParameter()],
        [ApplicationStateKey.HotelConnected],
        messages:
        [
            new(MessageKeys.Friends.SearchRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Friends.SearchResult, Direction.In, ApplicationMessageRole.Observe)
        ],
        tool_hints: new(true, false, false, true));

    public static ApplicationDescriptor MessageHistory { get; } = new(
        ApplicationMemberIds.FriendMessageHistory,
        "Friend message history",
        "Reads the bounded private-message journal with stable cursor paging.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(FriendMessageHistoryRequest),
        typeof(FriendMessageHistoryPage),
        CursorParameters(),
        messages:
        [
            new(MessageKeys.Friends.PrivateMessageReceived, Direction.In, ApplicationMessageRole.Observe)
        ],
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor MessageSend { get; } = Operation<FriendMessageSendRequest>(
        ApplicationMemberIds.FriendMessageSend,
        "Send private message",
        "Sends a private messenger message with the active Flash or Unity sequence layout.",
        [
            RequiredId("recipient_id", "Friend identifier."),
            RequiredText("message", "Private message text.")
        ],
        MessageKeys.Friends.PrivateMessageSend,
        destructive: true);

    public static ApplicationDescriptor RequestSend { get; } = Operation<FriendRequestSendRequest>(
        ApplicationMemberIds.FriendRequestSend,
        "Send friend request",
        "Sends a friend request by hotel user name.",
        [RequiredText("name", "Hotel user name.")],
        MessageKeys.Friends.FriendRequestSend,
        destructive: true);

    public static ApplicationDescriptor RequestAccept { get; } = Operation<FriendRequestIdsRequest>(
        ApplicationMemberIds.FriendRequestAccept,
        "Accept friend requests",
        "Accepts one or more pending friend requests.",
        [RequiredIds("request_ids", "Pending friend-request identifiers.")],
        MessageKeys.Friends.FriendRequestAccept,
        destructive: true,
        changes_friends: true);

    public static ApplicationDescriptor RequestDecline { get; } = Operation<FriendRequestDeclineRequest>(
        ApplicationMemberIds.FriendRequestDecline,
        "Decline friend requests",
        "Declines one or more selected pending friend requests.",
        [RequiredIds("request_ids", "Pending friend-request identifiers.")],
        MessageKeys.Friends.FriendRequestDecline,
        destructive: true);

    public static ApplicationDescriptor RequestsDeclineAll { get; } = Operation<FriendRequestsDeclineAllRequest>(
        ApplicationMemberIds.FriendRequestsDeclineAll,
        "Decline all friend requests",
        "Declines every pending friend request.",
        [],
        MessageKeys.Friends.FriendRequestDecline,
        destructive: true);

    public static ApplicationDescriptor RequestsList { get; } = new(
        ApplicationMemberIds.FriendRequestsList,
        "Pending friend requests",
        "Loads the active session's current pending friend requests.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(FriendRequestsListRequest),
        typeof(PendingFriendRequests),
        [TimeoutParameter()],
        [ApplicationStateKey.HotelConnected],
        messages:
        [
            new(MessageKeys.Friends.FriendRequestsRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Friends.FriendRequestsSnapshot, Direction.In, ApplicationMessageRole.Observe)
        ],
        tool_hints: new(true, false, true, true));

    public static ApplicationDescriptor Remove { get; } = Operation<FriendsRemoveRequest>(
        ApplicationMemberIds.FriendsRemove,
        "Remove friends",
        "Removes one or more users from the friend list.",
        [RequiredIds("friend_ids", "Friend identifiers to remove.")],
        MessageKeys.Friends.Remove,
        destructive: true,
        changes_friends: true);

    public static ApplicationDescriptor Follow { get; } = Operation<FriendFollowRequest>(
        ApplicationMemberIds.FriendFollow,
        "Follow friend",
        "Follows a friend to their current room.",
        [RequiredId("friend_id", "Friend identifier to follow.")],
        MessageKeys.Friends.Follow);

    public static ApplicationDescriptor RelationshipSet { get; } = Operation<FriendRelationshipSetRequest>(
        ApplicationMemberIds.FriendRelationshipSet,
        "Set friend relationship",
        "Changes the relationship marker shown for a friend.",
        [
            RequiredId("friend_id", "Friend identifier."),
            new("relationship", typeof(RelationshipType), true, null, "Relationship marker.")
        ],
        MessageKeys.Friends.RelationshipSet,
        destructive: true,
        changes_friends: true);

    public static ApplicationDescriptor Changed { get; } = Event<FriendChanged>(
        ApplicationMemberIds.FriendsChanged,
        "Friend list changed",
        "Publishes immutable friend-list load, add, update, remove and reset changes.",
        ListMessages(),
        [new(ApplicationStateKey.FriendsLoaded, ApplicationStateEffectKind.Changes)]);

    public static ApplicationDescriptor MessageReceived { get; } = Event<FriendMessageEntry>(
        ApplicationMemberIds.FriendMessageReceived,
        "Friend message received",
        "Publishes immutable private messenger entries.",
        [new(MessageKeys.Friends.PrivateMessageReceived, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor MessageFailed { get; } = Event<InstantMessageError>(
        ApplicationMemberIds.FriendMessageFailed,
        "Friend message failed",
        "Publishes private-message delivery failures.",
        [new(MessageKeys.Friends.PrivateMessageFailed, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor OperationFailed { get; } = Event<MessengerError>(
        ApplicationMemberIds.FriendOperationFailed,
        "Friend operation failed",
        "Publishes hotel rejections for messenger operations.",
        [new(MessageKeys.Friends.OperationFailed, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor RequestReceived { get; } = Event<NewFriendRequest>(
        ApplicationMemberIds.FriendRequestReceived,
        "Friend request received",
        "Publishes immutable incoming friend requests.",
        [new(MessageKeys.Friends.FriendRequestReceived, Direction.In, ApplicationMessageRole.Observe)]);

    private static ApplicationDescriptor Operation<TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        MessageKey key,
        bool read_only = false,
        bool destructive = false,
        bool idempotent = false,
        bool changes_friends = false) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(FriendOperationResult),
            parameters,
            [ApplicationStateKey.HotelConnected],
            changes_friends
                ? [new(ApplicationStateKey.FriendsLoaded, ApplicationStateEffectKind.Changes)]
                : [],
            [new(key, Direction.Out, ApplicationMessageRole.Send)],
            new(read_only, destructive, idempotent, true));

    private static ApplicationDescriptor Event<TEvent>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationMessageRequirement> messages,
        IReadOnlyList<ApplicationStateEffect>? state_effects = null) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Event,
            event_exposure,
            null,
            typeof(TEvent),
            state_effects: state_effects,
            messages: messages);

    private static ApplicationParameterDescriptor[] ListParameters(int limit) =>
    [
        new(
            "query",
            typeof(string),
            false,
            string.Empty,
            "Optional local name, real-name or motto filter.",
            new(MaxUtf8Bytes: ushort.MaxValue)),
        new("online_only", typeof(bool), false, false, "Return only online friends."),
        new("offset", typeof(int), false, 0, "Zero-based result offset.", new(Minimum: 0)),
        new("limit", typeof(int), false, limit, "Maximum number of friends to return.", new(Minimum: 1, Maximum: 500))
    ];

    private static ApplicationParameterDescriptor[] CursorParameters() =>
    [
        new(
            "after_sequence",
            typeof(long),
            false,
            0L,
            "Return entries after this sequence.",
            new(Pattern: "^[0-9]+$")),
        new("limit", typeof(int), false, 100, "Maximum number of entries to return.", new(Minimum: 1, Maximum: 500))
    ];

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor RequiredText(string name, string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApplicationParameterDescriptor RequiredId(string name, string description) => new(
        name,
        typeof(Id),
        true,
        null,
        description,
        new(Pattern: "^-?[0-9]+$"));

    private static ApplicationParameterDescriptor RequiredIds(string name, string description) => new(
        name,
        typeof(IReadOnlyList<Id>),
        true,
        null,
        description,
        new(MinItems: 1, MaxItems: ushort.MaxValue));

    private static ApplicationMessageRequirement[] ListMessages() =>
    [
        new(MessageKeys.Friends.Initialized, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Friends.ListFragment, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Friends.ListUpdated, Direction.In, ApplicationMessageRole.Observe)
    ];
}
