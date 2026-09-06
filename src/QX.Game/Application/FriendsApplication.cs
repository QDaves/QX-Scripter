using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed class FriendsApplication : IApplicationFeature, IFriendOperations
{
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly FriendManager friends;
    private readonly TimeProvider time_provider;
    private readonly FriendMessageJournal messages;
    private readonly ApplicationEventSource<FriendChanged> changed;
    private readonly ApplicationEventSource<MessengerError> operation_failed;
    private readonly ApplicationEventSource<InstantMessageError> message_failed;
    private readonly ApplicationEventSource<NewFriendRequest> request_received;
    private int disposed;

    public FriendsApplication(
        IConnection connection,
        GameState game,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        this.game = game;
        friends = game.Friends;
        this.time_provider = time_provider;
        changed = new ApplicationEventSource<FriendChanged>(observer_error);
        operation_failed = new ApplicationEventSource<MessengerError>(observer_error);
        message_failed = new ApplicationEventSource<InstantMessageError>(observer_error);
        request_received = new ApplicationEventSource<NewFriendRequest>(observer_error);
        messages = new FriendMessageJournal(connection, friends, time_provider, observer_error);

        try
        {
            Bindings = Array.AsReadOnly<IApplicationBinding>(
            [
                new ApplicationCallBinding<FriendsListRequest, FriendListPage>(
                    FriendsApplicationDescriptors.List,
                    (request, _) => ValueTask.FromResult(List(request))),
                new ApplicationCallBinding<FriendsRefreshRequest, FriendListPage>(
                    FriendsApplicationDescriptors.Refresh,
                    Refresh),
                new ApplicationCallBinding<FriendsSearchRequest, FriendsSearchResult>(
                    FriendsApplicationDescriptors.Search,
                    Search),
                new ApplicationCallBinding<FriendMessageHistoryRequest, FriendMessageHistoryPage>(
                    FriendsApplicationDescriptors.MessageHistory,
                    (request, _) => ValueTask.FromResult(MessageHistory(request))),
                new ApplicationCallBinding<FriendMessageSendRequest, FriendOperationResult>(
                    FriendsApplicationDescriptors.MessageSend,
                    SendMessage),
                new ApplicationCallBinding<FriendRequestSendRequest, FriendOperationResult>(
                    FriendsApplicationDescriptors.RequestSend,
                    SendRequest),
                new ApplicationCallBinding<FriendRequestIdsRequest, FriendOperationResult>(
                    FriendsApplicationDescriptors.RequestAccept,
                    AcceptRequests),
                new ApplicationCallBinding<FriendRequestDeclineRequest, FriendOperationResult>(
                    FriendsApplicationDescriptors.RequestDecline,
                    DeclineRequests),
                new ApplicationCallBinding<FriendRequestsDeclineAllRequest, FriendOperationResult>(
                    FriendsApplicationDescriptors.RequestsDeclineAll,
                    DeclineAllRequests),
                new ApplicationCallBinding<FriendRequestsListRequest, PendingFriendRequests>(
                    FriendsApplicationDescriptors.RequestsList,
                    ListRequests),
                new ApplicationCallBinding<FriendsRemoveRequest, FriendOperationResult>(
                    FriendsApplicationDescriptors.Remove,
                    Remove),
                new ApplicationCallBinding<FriendFollowRequest, FriendOperationResult>(
                    FriendsApplicationDescriptors.Follow,
                    Follow),
                new ApplicationCallBinding<FriendRelationshipSetRequest, FriendOperationResult>(
                    FriendsApplicationDescriptors.RelationshipSet,
                    SetRelationship),
                new ApplicationEventBinding<FriendChanged>(
                    FriendsApplicationDescriptors.Changed,
                    changed.Subscribe),
                new ApplicationEventBinding<FriendMessageEntry>(
                    FriendsApplicationDescriptors.MessageReceived,
                    messages.Subscribe),
                new ApplicationEventBinding<InstantMessageError>(
                    FriendsApplicationDescriptors.MessageFailed,
                    message_failed.Subscribe),
                new ApplicationEventBinding<MessengerError>(
                    FriendsApplicationDescriptors.OperationFailed,
                    operation_failed.Subscribe),
                new ApplicationEventBinding<NewFriendRequest>(
                    FriendsApplicationDescriptors.RequestReceived,
                    request_received.Subscribe)
            ]);

            friends.Loaded += OnLoaded;
            friends.FriendAdded += OnAdded;
            friends.FriendUpdated += OnUpdated;
            friends.FriendRemoved += OnRemoved;
            friends.ResetCompleted += OnReset;
            friends.MessengerFailed += operation_failed.Publish;
            friends.MessageDeliveryFailed += message_failed.Publish;
            friends.FriendRequestReceived += request_received.Publish;
            game.BindFriendOperations(this);
        }
        catch
        {
            game.UnbindFriendOperations(this);
            messages.Dispose();
            changed.Dispose();
            operation_failed.Dispose();
            message_failed.Dispose();
            request_received.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public FriendListPage List(FriendsListRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Query);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
        if (request.Limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(request.Limit));

        FriendState state = CaptureState();
        FriendCollectionSnapshot snapshot = SnapshotFactory.Friends(
            state.Friends,
            state.Categories,
            state.UserLimit,
            state.NormalLimit,
            state.ExtendedLimit);
        IEnumerable<FriendSnapshot> selected = snapshot.Friends;
        if (request.OnlineOnly)
            selected = selected.Where(friend => friend.IsOnline);
        if (request.Query.Length != 0)
        {
            selected = selected.Where(friend =>
                friend.Name.Contains(request.Query, StringComparison.OrdinalIgnoreCase) ||
                friend.RealName.Contains(request.Query, StringComparison.OrdinalIgnoreCase) ||
                friend.Motto.Contains(request.Query, StringComparison.OrdinalIgnoreCase));
        }
        FriendSnapshot[] matches = selected.ToArray();
        FriendSnapshot[] page = matches
            .Skip(request.Offset)
            .Take(request.Limit)
            .ToArray();
        int end = checked(request.Offset + page.Length);
        int? next_offset = end < matches.Length ? end : null;
        return new FriendListPage(
            state.Loaded,
            state.Loading,
            state.Stale,
            state.Generation,
            state.Revision,
            snapshot.Total,
            matches.Length,
            snapshot.Online,
            snapshot.UserLimit,
            snapshot.NormalLimit,
            snapshot.ExtendedLimit,
            snapshot.Categories,
            request.Offset,
            next_offset,
            page);
    }

    public async ValueTask<FriendListPage> Refresh(
        FriendsRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (request.TimeoutMilliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(request.TimeoutMilliseconds));
        Session session = RequireSession(cancellation_token);
        await friends.RefreshAsync(session, request.TimeoutMilliseconds, cancellation_token).ConfigureAwait(false);
        FriendListPage page = List(new FriendsListRequest(
            request.Query,
            request.OnlineOnly,
            request.Offset,
            request.Limit));
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The hotel session changed while loading the friend list.");
        return page;
    }

    public async ValueTask<FriendsSearchResult> Search(
        FriendsSearchRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new ArgumentException("The search query cannot be empty.", nameof(request.Query));
        if (request.TimeoutMilliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(request.TimeoutMilliseconds));
        Session session = RequireSession(cancellation_token);
        UserSearchResults result = await game.Requests.RequestAsync(
            MessageContracts.Friends.SearchRequest,
            new Qx.Model.Messages.Outgoing.FriendSearchRequest(request.Query),
            MessageContracts.Friends.SearchResult,
            session,
            match: _ => ReferenceEquals(connection.Session, session),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2).ConfigureAwait(false);
        UserSearchResult[] friend_results = result.Friends.ToArray();
        UserSearchResult[] other_results = result.Others.ToArray();
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The hotel session changed during the friend search.");
        return new FriendsSearchResult(
            request.Query,
            friend_results,
            other_results);
    }

    public FriendMessageHistoryPage MessageHistory(FriendMessageHistoryRequest request)
    {
        ThrowIfDisposed();
        return messages.History(request);
    }

    public ValueTask<FriendOperationResult> SendMessage(
        FriendMessageSendRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("The private message cannot be empty.", nameof(request.Message));
        Session session = RequireSession(cancellation_token);
        friends.SendPrivateMessage(request.RecipientId, request.Message, session, cancellation_token);
        return Result(session, [request.RecipientId]);
    }

    public ValueTask<FriendOperationResult> SendRequest(
        FriendRequestSendRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("The friend name cannot be empty.", nameof(request.Name));
        Session session = RequireSession(cancellation_token);
        friends.RequestFriend(request.Name, session, cancellation_token);
        return Result(session, [], request.Name);
    }

    public ValueTask<FriendOperationResult> AcceptRequests(
        FriendRequestIdsRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        Id[] request_ids = RequiredIds(request?.RequestIds, nameof(request.RequestIds));
        Session session = RequireSession(cancellation_token);
        friends.AcceptFriendRequests(request_ids, session, cancellation_token);
        return Result(session, request_ids);
    }

    public ValueTask<FriendOperationResult> DeclineRequests(
        FriendRequestDeclineRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        Id[] request_ids = RequiredIds(request?.RequestIds, nameof(request.RequestIds));
        Session session = RequireSession(cancellation_token);
        friends.DeclineFriendRequests(request_ids, session, cancellation_token);
        return Result(session, request_ids);
    }

    public ValueTask<FriendOperationResult> DeclineAllRequests(
        FriendRequestsDeclineAllRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        Session session = RequireSession(cancellation_token);
        friends.DeclineAllFriendRequests(session, cancellation_token);
        return Result(session, []);
    }

    public async ValueTask<PendingFriendRequests> ListRequests(
        FriendRequestsListRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (request.TimeoutMilliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(request.TimeoutMilliseconds));
        Session session = RequireSession(cancellation_token);
        PendingFriendRequests result = await game.Requests.RequestAsync(
            MessageContracts.Friends.FriendRequestsRequest,
            new Qx.Model.Messages.Outgoing.PendingFriendRequestsRequest(),
            MessageContracts.Friends.FriendRequestsSnapshot,
            session,
            match: _ => ReferenceEquals(connection.Session, session),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2).ConfigureAwait(false);
        PendingFriendRequests snapshot = new(result.Total, result.Requests.ToArray());
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The hotel session changed while loading pending friend requests.");
        return snapshot;
    }

    public ValueTask<FriendOperationResult> Remove(
        FriendsRemoveRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        Id[] friend_ids = RequiredIds(request?.FriendIds, nameof(request.FriendIds));
        Session session = RequireSession(cancellation_token);
        friends.RemoveFriends(friend_ids, session, cancellation_token);
        return Result(session, friend_ids);
    }

    public ValueTask<FriendOperationResult> Follow(
        FriendFollowRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        Session session = RequireSession(cancellation_token);
        friends.Follow(request.FriendId, session, cancellation_token);
        return Result(session, [request.FriendId]);
    }

    public ValueTask<FriendOperationResult> SetRelationship(
        FriendRelationshipSetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Relationship))
            throw new ArgumentOutOfRangeException(nameof(request.Relationship));
        Session session = RequireSession(cancellation_token);
        friends.SetRelationship(request.FriendId, request.Relationship, session, cancellation_token);
        return Result(session, [request.FriendId]);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        friends.Loaded -= OnLoaded;
        friends.FriendAdded -= OnAdded;
        friends.FriendUpdated -= OnUpdated;
        friends.FriendRemoved -= OnRemoved;
        friends.ResetCompleted -= OnReset;
        friends.MessengerFailed -= operation_failed.Publish;
        friends.MessageDeliveryFailed -= message_failed.Publish;
        friends.FriendRequestReceived -= request_received.Publish;
        game.UnbindFriendOperations(this);
        messages.Dispose();
        changed.Dispose();
        operation_failed.Dispose();
        message_failed.Dispose();
        request_received.Dispose();
    }

    Task<IReadOnlyCollection<Friend>> IFriendOperations.EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => EnsureLoadedAsync(
            timeout_milliseconds,
            cancellation_token);

    void IFriendOperations.Follow(
        FriendFollowRequest request,
        CancellationToken cancellation_token) => Follow(request, cancellation_token)
            .GetAwaiter()
            .GetResult();

    void IFriendOperations.AcceptRequests(
        FriendRequestIdsRequest request,
        Session expected_session,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        Id[] request_ids = RequiredIds(request?.RequestIds, nameof(request.RequestIds));
        ArgumentNullException.ThrowIfNull(expected_session);
        cancellation_token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(connection.Session, expected_session))
            throw new InvalidOperationException("The hotel session changed before accepting friend requests.");
        friends.AcceptFriendRequests(request_ids, expected_session, cancellation_token);
    }

    private async Task<IReadOnlyCollection<Friend>> EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        if (timeout_milliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
        cancellation_token.ThrowIfCancellationRequested();
        if (friends.IsLoaded)
            return friends.Friends;
        Session session = RequireSession(cancellation_token);
        return await friends.RefreshAsync(
            session,
            timeout_milliseconds,
            cancellation_token).ConfigureAwait(false);
    }

    private FriendState CaptureState() => friends.Capture(state => new FriendState(
        state.IsLoaded,
        state.IsLoading,
        state.IsStale,
        state.Generation,
        state.Revision,
        state.UserLimit,
        state.NormalLimit,
        state.ExtendedLimit,
        state.Categories.ToArray(),
        state.Friends.ToArray()));

    private Session RequireSession(CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        return connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
    }

    private ValueTask<FriendOperationResult> Result(
        Session session,
        IReadOnlyList<Id> target_ids,
        string? target_name = null) => ValueTask.FromResult(new FriendOperationResult(
            session.Client,
            time_provider.GetUtcNow(),
            target_ids,
            target_name));

    private static Id[] RequiredIds(IReadOnlyList<Id>? values, string parameter_name)
    {
        ArgumentNullException.ThrowIfNull(values, parameter_name);
        if (values.Count == 0)
            throw new ArgumentException("At least one identifier is required.", parameter_name);
        Id[] distinct = values.Distinct().ToArray();
        if (distinct.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(parameter_name);
        return distinct;
    }

    private void OnLoaded() => PublishChange(FriendChangeKind.Loaded, null);
    private void OnAdded(Friend friend) => PublishChange(FriendChangeKind.Added, friend);
    private void OnUpdated(Friend friend) => PublishChange(FriendChangeKind.Updated, friend);
    private void OnRemoved(Friend friend) => PublishChange(FriendChangeKind.Removed, friend);
    private void OnReset() => PublishChange(FriendChangeKind.Reset, null);

    private void PublishChange(FriendChangeKind kind, Friend? friend)
    {
        FriendState state = CaptureState();
        FriendSnapshot? snapshot = friend is null
            ? null
            : SnapshotFactory.Friends([friend]).Friends[0];
        changed.Publish(new FriendChanged(
            kind,
            state.Generation,
            state.Revision,
            time_provider.GetUtcNow(),
            snapshot));
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private sealed record FriendState(
        bool Loaded,
        bool Loading,
        bool Stale,
        long Generation,
        long Revision,
        int UserLimit,
        int NormalLimit,
        int ExtendedLimit,
        IReadOnlyList<FriendCategory> Categories,
        IReadOnlyList<Friend> Friends);
}
