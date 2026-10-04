using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Game.Protocol;
using Qx.Model.Messages.Outgoing;
using Qx.Interception;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game;

/// <summary>
/// Manages the user's friend list, friend requests and private messages.
/// </summary>
/// <remarks>
/// <para>
/// The hotel sends the friend list in fragments. <see cref="EnsureLoadedAsync(int, CancellationToken)"/>
/// requests it and completes once every fragment has arrived. Later additions, updates and
/// removals are applied as the hotel reports them.
/// </para>
/// <para>
/// All members are safe to call from any thread. The state is cleared when the hotel connection closes.
/// </para>
/// </remarks>
public sealed class FriendManager : GameStateManager
{
    private static readonly TimeSpan request_lease = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan init_cooldown = TimeSpan.FromSeconds(30);
    private readonly object _sync = new();
    private readonly object _publication_sync = new();
    private readonly TimeProvider _time_provider;
    private readonly Dictionary<long, Friend> _friends = [];
    private readonly List<FriendCategory> _categories = [];
    private readonly Dictionary<int, IReadOnlyList<Friend>> _pending_fragments = [];
    private readonly Dictionary<long, FriendDelta> _pending_deltas = [];
    private readonly object _message_index_sync = new();
    private readonly Dictionary<long, int> _message_indices = [];
    private SharedLoadOperation<IReadOnlyCollection<Friend>>? _load_operation;
    private int _expected_fragments = -1;
    private bool _discard_until_zero;
    private bool _is_loaded;
    private bool _is_loading;
    private bool _is_stale;
    private int _user_limit;
    private int _normal_limit;
    private int _extended_limit;
    private bool _recovery_pending;
    private long _recovery_retired_epoch;
    private long _recovery_active_epoch;
    private long? _retired_request_epoch;
    private long _fragment_request_epoch;
    private long _request_epoch;
    private long? _last_init_answer;
    private long _generation;
    private long _revision;

    /// <summary>Initializes a new instance of the <see cref="FriendManager"/> class.</summary>
    public FriendManager()
        : this(TimeProvider.System)
    {
    }

    internal FriendManager(TimeProvider time_provider)
    {
        _time_provider = time_provider ?? throw new ArgumentNullException(nameof(time_provider));
    }

    /// <summary>Gets a snapshot of the friends in the friend list.</summary>
    public IReadOnlyCollection<Friend> Friends
    {
        get
        {
            lock (_sync)
                return _friends.Values.ToArray();
        }
    }

    /// <summary>Gets a snapshot of the user's friend categories.</summary>
    public IReadOnlyList<FriendCategory> Categories
    {
        get
        {
            lock (_sync)
                return _categories.ToArray();
        }
    }

    /// <summary>Gets whether the complete friend list has been received.</summary>
    public bool IsLoaded
    {
        get
        {
            lock (_sync)
                return _is_loaded;
        }
    }

    /// <summary>Gets whether the friend list is being received.</summary>
    public bool IsLoading
    {
        get
        {
            lock (_sync)
                return _is_loading;
        }
    }

    /// <summary>Gets whether <see cref="Friends"/> holds friends that a completed load has not confirmed.</summary>
    /// <remarks>
    /// <see langword="true"/> while a new load runs over an existing list, after a load was
    /// abandoned, or when the hotel reported changes before the list was loaded.
    /// </remarks>
    public bool IsStale
    {
        get
        {
            lock (_sync)
                return _is_stale;
        }
    }

    /// <summary>Gets the user's friend list limit from the messenger initialization.</summary>
    /// <remarks>0 until the hotel sends the messenger initialization.</remarks>
    public int UserLimit
    {
        get
        {
            lock (_sync)
                return _user_limit;
        }
    }

    /// <summary>Gets the normal friend list limit from the messenger initialization.</summary>
    /// <remarks>0 until the hotel sends the messenger initialization.</remarks>
    public int NormalLimit
    {
        get
        {
            lock (_sync)
                return _normal_limit;
        }
    }

    /// <summary>Gets the extended friend list limit from the messenger initialization.</summary>
    /// <remarks>0 until the hotel sends the messenger initialization.</remarks>
    public int ExtendedLimit
    {
        get
        {
            lock (_sync)
                return _extended_limit;
        }
    }

    /// <summary>Gets the number of fragments the current friend list load expects, or -1 when it is not known.</summary>
    public int ExpectedFragments
    {
        get
        {
            lock (_sync)
                return _expected_fragments;
        }
    }

    /// <summary>Gets the number of friend list fragments received for the current load.</summary>
    public int ReceivedFragments
    {
        get
        {
            lock (_sync)
                return _is_loaded && _expected_fragments >= 0
                    ? _expected_fragments
                    : _pending_fragments.Count;
        }
    }

    /// <summary>Gets the load generation, which increases each time a friend list load starts, is abandoned or is reset.</summary>
    public long Generation
    {
        get
        {
            lock (_sync)
                return _generation;
        }
    }

    /// <summary>Gets the revision, which increases on every change to the friend state.</summary>
    public long Revision
    {
        get
        {
            lock (_sync)
                return _revision;
        }
    }

    /// <summary>Occurs when the complete friend list has been received.</summary>
    public event Action? Loaded;
    /// <summary>Occurs when the messenger initialization with the friend limits and categories arrives.</summary>
    public event Action? Initialized;
    /// <summary>Occurs when the hotel adds a friend to the friend list; the argument is the new friend.</summary>
    public event Action<Friend>? FriendAdded;
    /// <summary>Occurs when the hotel updates a friend in the friend list; the argument is the updated friend.</summary>
    public event Action<Friend>? FriendUpdated;
    /// <summary>Occurs when the hotel removes a friend from the friend list; the argument is the removed friend.</summary>
    public event Action<Friend>? FriendRemoved;
    /// <summary>Occurs when another user sends the user a friend request; the argument is the request.</summary>
    public event Action<NewFriendRequest>? FriendRequestReceived;
    /// <summary>Occurs after the friend state has been cleared because the hotel connection closed.</summary>
    public event Action? ResetCompleted;

    /// <summary>
    /// Occurs when a private message from a friend arrives; the argument is the message.
    /// </summary>
    /// <remarks>
    /// Private messages are the messenger conversation, not room chat. Offline messages arrive on
    /// connect with a non-zero age, so a handler that acts on every message also acts on the
    /// backlog. <see cref="NewConsoleMessage.IsOffline"/> tells the two apart.
    /// </remarks>
    public event Action<NewConsoleMessage>? MessageReceived;

    /// <summary>Occurs when the hotel refuses a messenger operation; the argument holds the error code.</summary>
    public event Action<MessengerError>? MessengerFailed;

    /// <summary>Occurs when a private message could not be delivered; the argument describes the error.</summary>
    public event Action<InstantMessageError>? MessageDeliveryFailed;

    /// <summary>Reads several values from the manager while holding its state lock, so they are consistent with each other.</summary>
    /// <typeparam name="TResult">The type of the value the projection returns.</typeparam>
    /// <param name="projection">The function that reads the manager.</param>
    /// <returns>The value the projection returns.</returns>
    public TResult Capture<TResult>(Func<FriendManager, TResult> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        lock (_sync)
            return projection(this);
    }

    /// <summary>Gets the friend with the specified user id.</summary>
    /// <param name="id">The user id of the friend.</param>
    /// <returns>The friend, or <see langword="null"/> if the user is not in the friend list.</returns>
    public Friend? FriendById(Id id)
    {
        lock (_sync)
            return _friends.GetValueOrDefault(id);
    }

    /// <summary>Gets the friend with the specified name, ignoring case.</summary>
    /// <param name="name">The name of the friend.</param>
    /// <returns>The friend, or <see langword="null"/> if no friend has that name.</returns>
    public Friend? FriendByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_sync)
            return _friends.Values.FirstOrDefault(friend =>
                string.Equals(friend.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Gets whether a user with the specified name is in the friend list, ignoring case.</summary>
    /// <param name="name">The name of the user.</param>
    public bool IsFriend(string name) => FriendByName(name) is not null;

    /// <summary>Sends a private message to a friend.</summary>
    /// <param name="recipientId">The user id of the friend.</param>
    /// <param name="text">The message text, which must not be empty.</param>
    /// <remarks>
    /// The message carries the next sequence number from <see cref="NextMessageIndex(Id)"/>.
    /// </remarks>
    public void SendPrivateMessage(Id recipientId, string text) =>
    SendPrivateMessageCore(recipientId, text, null, default);

    internal void SendPrivateMessage(
        Id recipient_id,
        string text,
        Session expected_session,
        CancellationToken cancellation_token) =>
        SendPrivateMessageCore(recipient_id, text, expected_session, cancellation_token);

    private void SendPrivateMessageCore(
        Id recipient_id,
        string text,
        Session? expected_session,
        CancellationToken cancellation_token)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        Send(
            MessageContracts.Friends.PrivateMessageSend,
            new SendPrivateMessage(
                recipient_id,
                text,
                NextMessageIndex(recipient_id)),
            expected_session,
            cancellation_token);
    }

    /// <summary>
    /// Gets the next message sequence number for a conversation and advances it.
    /// </summary>
    /// <remarks>
    /// Every caller shares one sequence per conversation. Sequences start at 0 and are cleared when
    /// the hotel connection closes.
    /// </remarks>
    /// <param name="recipientId">The user id of the friend being written to.</param>
    /// <returns>The sequence number to send with the next message.</returns>
    public int NextMessageIndex(Id recipientId)
    {
        lock (_message_index_sync)
        {
            _message_indices.TryGetValue(recipientId, out int index);
            _message_indices[recipientId] = index + 1;
            return index;
        }
    }

    /// <summary>Sends a friend request to the user with the specified name.</summary>
    /// <param name="name">The name of the user to ask, which must not be empty.</param>
    public void RequestFriend(string name) =>
        RequestFriendCore(name, null, default);

    internal void RequestFriend(
        string name,
        Session expected_session,
        CancellationToken cancellation_token) =>
        RequestFriendCore(name, expected_session, cancellation_token);

    private void RequestFriendCore(
        string name,
        Session? expected_session,
        CancellationToken cancellation_token)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Send(
            MessageContracts.Friends.FriendRequestSend,
            new FriendRequest(name),
            expected_session,
            cancellation_token);
    }

    /// <summary>Accepts pending friend requests.</summary>
    /// <param name="requestIds">The ids of the requests to accept.</param>
    /// <remarks>Nothing is sent when <paramref name="requestIds"/> is empty.</remarks>
    public void AcceptFriendRequests(params IReadOnlyList<Id> requestIds) =>
        AcceptFriendRequestsCore(requestIds, null, default);

    internal void AcceptFriendRequests(
        IReadOnlyList<Id> request_ids,
        Session expected_session,
        CancellationToken cancellation_token) =>
        AcceptFriendRequestsCore(request_ids, expected_session, cancellation_token);

    private void AcceptFriendRequestsCore(
        IReadOnlyList<Id> request_ids,
        Session? expected_session,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request_ids);
        if (request_ids.Count == 0)
            return;
        Send(
            MessageContracts.Friends.FriendRequestAccept,
            new AcceptFriends(request_ids),
            expected_session,
            cancellation_token);
    }

    /// <summary>Declines pending friend requests.</summary>
    /// <param name="requestIds">The ids of the requests to decline.</param>
    /// <remarks>Nothing is sent when <paramref name="requestIds"/> is empty.</remarks>
    public void DeclineFriendRequests(params IReadOnlyList<Id> requestIds) =>
        DeclineFriendRequestsCore(requestIds, null, default);

    internal void DeclineFriendRequests(
        IReadOnlyList<Id> request_ids,
        Session expected_session,
        CancellationToken cancellation_token) =>
        DeclineFriendRequestsCore(request_ids, expected_session, cancellation_token);

    private void DeclineFriendRequestsCore(
        IReadOnlyList<Id> request_ids,
        Session? expected_session,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request_ids);
        if (request_ids.Count == 0)
            return;
        Send(
            MessageContracts.Friends.FriendRequestDecline,
            DeclineFriends.Only(request_ids),
            expected_session,
            cancellation_token);
    }

    /// <summary>Declines every pending friend request.</summary>
    public void DeclineAllFriendRequests() =>
        DeclineAllFriendRequestsCore(null, default);

    internal void DeclineAllFriendRequests(
        Session expected_session,
        CancellationToken cancellation_token) =>
        DeclineAllFriendRequestsCore(expected_session, cancellation_token);

    private void DeclineAllFriendRequestsCore(
        Session? expected_session,
        CancellationToken cancellation_token) =>
        Send(
            MessageContracts.Friends.FriendRequestDecline,
            DeclineFriends.All(),
            expected_session,
            cancellation_token);

    /// <summary>Removes friends from the friend list.</summary>
    /// <param name="friendIds">The user ids of the friends to remove.</param>
    /// <remarks>Nothing is sent when <paramref name="friendIds"/> is empty.</remarks>
    public void RemoveFriends(params IReadOnlyList<Id> friendIds) =>
        RemoveFriendsCore(friendIds, null, default);

    internal void RemoveFriends(
        IReadOnlyList<Id> friend_ids,
        Session expected_session,
        CancellationToken cancellation_token) =>
        RemoveFriendsCore(friend_ids, expected_session, cancellation_token);

    private void RemoveFriendsCore(
        IReadOnlyList<Id> friend_ids,
        Session? expected_session,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(friend_ids);
        if (friend_ids.Count == 0)
            return;
        Send(
            MessageContracts.Friends.Remove,
            new RemoveFriends(friend_ids),
            expected_session,
            cancellation_token);
    }

    /// <summary>Follows a friend to the room they are in.</summary>
    /// <param name="friendId">The user id of the friend to follow.</param>
    public void Follow(Id friendId) =>
        FollowCore(friendId, null, default);

    internal void Follow(
        Id friend_id,
        Session expected_session,
        CancellationToken cancellation_token) =>
        FollowCore(friend_id, expected_session, cancellation_token);

    private void FollowCore(
        Id friend_id,
        Session? expected_session,
        CancellationToken cancellation_token) =>
        Send(
            MessageContracts.Friends.Follow,
            new FollowFriendRequest(friend_id),
            expected_session,
            cancellation_token);

    /// <summary>Sets the relationship shown for a friend.</summary>
    /// <param name="friendId">The user id of the friend.</param>
    /// <param name="relationship">The relationship to show.</param>
    public void SetRelationship(Id friendId, RelationshipType relationship) =>
        SetRelationshipCore(friendId, relationship, null, default);

    internal void SetRelationship(
        Id friend_id,
        RelationshipType relationship,
        Session expected_session,
        CancellationToken cancellation_token) =>
        SetRelationshipCore(friend_id, relationship, expected_session, cancellation_token);

    private void SetRelationshipCore(
        Id friend_id,
        RelationshipType relationship,
        Session? expected_session,
        CancellationToken cancellation_token) =>
        Send(
            MessageContracts.Friends.RelationshipSet,
            new SetFriendRelationshipRequest(friend_id, relationship),
            expected_session,
            cancellation_token);

    /// <summary>Requests the friend list from the server unless it is already loaded.</summary>
    /// <param name="timeoutMs">The time to wait for the complete friend list, in milliseconds.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes with a snapshot of the friends.</returns>
    /// <remarks>
    /// A loaded list is returned without a request. Concurrent callers share one request.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is not positive.</exception>
    /// <exception cref="TimeoutException">Thrown when the friend list does not arrive in time.</exception>
    /// <exception cref="FragmentedLoadCorrelationException">Thrown when the fragments of an abandoned request cannot be told apart from the current one.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached, or the hotel connection closes during the load.</exception>
    public Task<IReadOnlyCollection<Friend>> EnsureLoadedAsync(
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default) =>
        LoadAsync(timeoutMs, cancellationToken, null, false);

    internal Task<IReadOnlyCollection<Friend>> RefreshAsync(
        Session expected_session,
        int timeout_milliseconds = 10000,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        return LoadAsync(timeout_milliseconds, cancellation_token, expected_session, true);
    }

    private async Task<IReadOnlyCollection<Friend>> LoadAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token,
        Session? expected_session,
        bool force_refresh)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout_milliseconds, 0);
        cancellation_token.ThrowIfCancellationRequested();

        SharedLoadOperation<IReadOnlyCollection<Friend>> operation;
        bool request_friends = false;
        lock (_sync)
        {
            if (_is_loaded && (!force_refresh || HotelStillCoolingDown()))
                return _friends.Values.ToArray();
            if (_recovery_pending)
                throw RecoveryError();

            if (_load_operation is { Waiters: 0 } inactive &&
                inactive.IsExpired(request_lease) &&
                !inactive.HasResponse)
            {
                _load_operation = null;
                RetireRequest(inactive.Epoch);
            }
            if (_load_operation is null)
            {
                operation = new SharedLoadOperation<IReadOnlyCollection<Friend>>(
                    _time_provider,
                    ++_request_epoch);
                _load_operation = operation;
                if (!_is_loading)
                {
                    BeginGeneration(-1, operation.Epoch);
                    request_friends = true;
                }
                operation.RequestSent = true;
            }
            else
            {
                operation = _load_operation;
            }
            operation.Waiters++;
        }

        if (request_friends)
        {
            try
            {
                if (expected_session is null)
                {
                    SendMessage(
                        MessageContracts.Friends.InitializeRequest,
                        new FriendInitializationRequest());
                }
                else
                {
                    SendMessage(
                        MessageContracts.Friends.InitializeRequest,
                        new FriendInitializationRequest(),
                        expected_session,
                        cancellation_token,
                        () => RequireDispatch(expected_session, cancellation_token));
                }
            }
            catch (Exception error)
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_load_operation, operation))
                    {
                        _load_operation = null;
                        AbortGeneration();
                    }
                    operation.Completion.TrySetException(error);
                }
            }
        }

        try
        {
            return await operation.Completion.Task.WaitAsync(
                TimeSpan.FromMilliseconds(timeout_milliseconds),
                cancellation_token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            lock (_sync)
            {
                if (ReferenceEquals(_load_operation, operation) && operation.Waiters == 1 && !operation.HasResponse)
                {
                    _load_operation = null;
                    RetireRequest(operation.Epoch);
                }
            }
            throw;
        }
        finally
        {
            lock (_sync)
                operation.Waiters--;
        }
    }

    /// <inheritdoc/>
    protected override void OnAttach()
    {
        OnIncoming(
            MessageContracts.Friends.Initialized,
            (message, state_generation) => PublishIncoming(
                state_generation,
                () => ApplyInit(message)));

        OnIncoming(
            MessageContracts.Friends.PrivateMessageReceived,
            (message, state_generation) => PublishIncoming(
                state_generation,
                () => MessageReceived?.Invoke(message)));
        OnIncoming(
            MessageContracts.Friends.OperationFailed,
            (message, state_generation) => PublishIncoming(
                state_generation,
                () => MessengerFailed?.Invoke(message)));
        OnIncoming(
            MessageContracts.Friends.PrivateMessageFailed,
            (message, state_generation) => PublishIncoming(
                state_generation,
                () => MessageDeliveryFailed?.Invoke(message)));
        OnIncoming(
            MessageContracts.Friends.FriendRequestReceived,
            (message, state_generation) => PublishIncoming(
                state_generation,
                () => FriendRequestReceived?.Invoke(message)));
        OnIncoming(
            MessageContracts.Friends.ListFragment,
            (message, state_generation) => PublishIncoming(
                state_generation,
                () => ApplyFragment(message)));
        OnIncoming(
            MessageContracts.Friends.ListUpdated,
            (message, state_generation) => PublishIncoming(
                state_generation,
                () => ApplyUpdate(message)));
    }

    private void ApplyInit(MessengerInit init)
    {
        IReadOnlyCollection<Friend>? published = null;
        long published_generation = -1;
        long published_request_epoch = -1;
        lock (_sync)
        {
            BeginGeneration(-1, TakeRequestEpoch());
            _discard_until_zero = false;
            _last_init_answer = _time_provider.GetTimestamp();
            if (_load_operation is { } active)
                active.Touch();
            _user_limit = init.UserLimit;
            _normal_limit = init.NormalLimit;
            _extended_limit = init.ExtendedLimit;
            _categories.Clear();
            _categories.AddRange(init.Categories);
            _revision++;
        }

        Initialized?.Invoke();
        if (published is not null)
            PublishLoaded(published_generation, published_request_epoch);
    }

    private void ApplyFragment(FriendListFragment fragment)
    {
        ValidateFragment(fragment);

        IReadOnlyCollection<Friend>? published = null;
        long published_generation = -1;
        long published_request_epoch = -1;
        lock (_sync)
        {
            if (_discard_until_zero)
            {
                if (fragment.Index != 0 &&
                    _retired_request_epoch is null &&
                    !_recovery_pending)
                {
                    return;
                }
                BeginGeneration(fragment.Total, TakeRequestEpoch());
                _discard_until_zero = false;
            }
            else if (_is_loaded)
            {
                return;
            }
            else if (!_is_loading)
            {
                BeginGeneration(fragment.Total, CurrentRequestEpoch());
            }
            else if (_expected_fragments < 0)
            {
                _expected_fragments = fragment.Total;
                _revision++;
            }
            else if (_expected_fragments != fragment.Total)
            {
                if (fragment.Index != 0)
                    return;
                BeginGeneration(fragment.Total, _fragment_request_epoch);
            }

            if (_load_operation is { } active)
                active.Touch();
            if (fragment.Total == 0)
            {
                published = PublishSnapshot();
                if (published is not null)
                {
                    published_generation = _generation;
                    published_request_epoch = _fragment_request_epoch;
                }
            }
            else
            {
                _pending_fragments[fragment.Index] = fragment.Friends.ToArray();
                _revision++;
                if (_pending_fragments.Count == _expected_fragments &&
                    Enumerable.Range(0, _expected_fragments).All(_pending_fragments.ContainsKey))
                {
                    published = PublishSnapshot();
                    if (published is not null)
                    {
                        published_generation = _generation;
                        published_request_epoch = _fragment_request_epoch;
                    }
                }
            }
        }

        if (published is not null)
            PublishLoaded(published_generation, published_request_epoch);
    }

    private void PublishLoaded(long generation, long request_epoch)
    {
        lock (_publication_sync)
        {
            lock (_sync)
            {
                if (!_is_loaded ||
                    _generation != generation ||
                    _fragment_request_epoch != request_epoch)
                {
                    return;
                }
            }
            Loaded?.Invoke();
        }
    }

    private void PublishIncoming(long state_generation, Action publication)
    {
        lock (_publication_sync)
        {
            if (state_generation != CurrentStateGeneration)
                return;
            publication();
        }
    }

    private void Send<T>(
        MessageContract<T> contract,
        T message,
        Session? expected_session,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        if (expected_session is null)
            SendMessage(contract, message);
        else
            SendMessage(
                contract,
                message,
                expected_session,
                cancellation_token,
                () => RequireDispatch(expected_session, cancellation_token));
    }

    private void RequireDispatch(Session expected_session, CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(Interceptor.Session, expected_session))
            throw new InvalidOperationException("The hotel session changed before friend dispatch.");
    }

    private void ApplyUpdate(FriendListUpdate update)
    {
        var events = new List<FriendEvent>();
        lock (_sync)
        {
            _categories.Clear();
            _categories.AddRange(update.Categories);

            foreach (FriendUpdateEntry entry in update.Updates)
            {
                switch (entry.Kind)
                {
                    case FriendUpdateKind.Removed:
                        QueueDelta(FriendDelta.Remove(entry.RemovedId));
                        if (_friends.Remove(entry.RemovedId, out Friend? removed))
                            events.Add(new FriendEvent(FriendUpdateKind.Removed, removed));
                        break;
                    case FriendUpdateKind.Added when entry.Friend is not null:
                        _friends[entry.Friend.Id] = entry.Friend;
                        QueueDelta(FriendDelta.Upsert(entry.Friend));
                        events.Add(new FriendEvent(FriendUpdateKind.Added, entry.Friend));
                        break;
                    case FriendUpdateKind.Updated when entry.Friend is not null:
                        _friends[entry.Friend.Id] = entry.Friend;
                        QueueDelta(FriendDelta.Upsert(entry.Friend));
                        events.Add(new FriendEvent(FriendUpdateKind.Updated, entry.Friend));
                        break;
                }
            }

            if (!_is_loaded && _friends.Count > 0)
                _is_stale = true;
            _revision++;
        }

        foreach (FriendEvent friend_event in events)
        {
            switch (friend_event.Kind)
            {
                case FriendUpdateKind.Removed:
                    FriendRemoved?.Invoke(friend_event.Friend);
                    break;
                case FriendUpdateKind.Added:
                    FriendAdded?.Invoke(friend_event.Friend);
                    break;
                case FriendUpdateKind.Updated:
                    FriendUpdated?.Invoke(friend_event.Friend);
                    break;
            }
        }
    }

    private static void ValidateFragment(FriendListFragment fragment)
    {
        if (fragment.Total < 0)
            throw new InvalidDataException($"Friend-list fragment count must be non-negative, received {fragment.Total}.");
        if (fragment.Index < 0 ||
            fragment.Total == 0 && fragment.Index != 0 ||
            fragment.Total > 0 && fragment.Index >= fragment.Total)
        {
            throw new InvalidDataException(
                $"Friend-list fragment index {fragment.Index} is invalid for {fragment.Total} fragments.");
        }
        if (fragment.Total == 0 && fragment.Friends.Count != 0)
            throw new InvalidDataException("An empty friend-list generation cannot contain friends.");
    }

    private void BeginGeneration(int expected_fragments, long request_epoch)
    {
        _generation++;
        _expected_fragments = expected_fragments;
        _pending_fragments.Clear();
        _fragment_request_epoch = request_epoch;
        _is_loaded = false;
        _is_loading = true;
        _is_stale = _friends.Count > 0;
        _revision++;
    }

    private IReadOnlyCollection<Friend>? PublishSnapshot()
    {
        var replacement = new Dictionary<long, Friend>();
        for (int index = 0; index < _expected_fragments; index++)
        {
            foreach (Friend friend in _pending_fragments[index])
                replacement[friend.Id] = friend;
        }
        foreach (FriendDelta delta in _pending_deltas.Values)
            delta.Apply(replacement);

        if (_fragment_request_epoch != 0 &&
            (_load_operation is null || _fragment_request_epoch != _load_operation.Epoch))
        {
            FailAmbiguousGeneration();
            return null;
        }

        _friends.Clear();
        foreach ((long friend_id, Friend friend) in replacement)
            _friends[friend_id] = friend;

        _pending_fragments.Clear();
        _pending_deltas.Clear();
        _discard_until_zero = false;
        _is_loaded = true;
        _is_loading = false;
        _is_stale = false;
        _recovery_pending = false;
        _revision++;

        IReadOnlyCollection<Friend> published = _friends.Values.ToArray();
        TaskCompletionSource<IReadOnlyCollection<Friend>>? completion = _load_operation?.Completion;
        _load_operation = null;
        completion?.TrySetResult(published);
        return published;
    }

    private bool HotelStillCoolingDown() =>
        _last_init_answer is { } answered && _time_provider.GetElapsedTime(answered) < init_cooldown;

    private long CurrentRequestEpoch() => _load_operation?.Epoch ?? 0;

    private long TakeRequestEpoch()
    {
        if (_retired_request_epoch is not { } retired)
            return CurrentRequestEpoch();

        _retired_request_epoch = null;
        return retired;
    }

    private void RetireRequest(long request_epoch)
    {
        _retired_request_epoch = request_epoch;
        _pending_deltas.Clear();
        AbortGeneration();
    }

    private void QueueDelta(FriendDelta delta)
    {
        if (!_is_loaded)
            _pending_deltas[delta.FriendId] = delta;
    }

    private void AbortGeneration()
    {
        _discard_until_zero = true;
        _pending_fragments.Clear();
        _expected_fragments = -1;
        _fragment_request_epoch = 0;
        _is_loading = false;
        _is_stale = _friends.Count > 0;
        _generation++;
        _revision++;
    }

    private void DiscardGeneration()
    {
        _discard_until_zero = true;
        _pending_fragments.Clear();
        _expected_fragments = -1;
        _fragment_request_epoch = 0;
        _is_loaded = false;
        _is_loading = _load_operation is not null;
        _is_stale = _friends.Count > 0;
        _generation++;
        _revision++;
    }

    private void FailAmbiguousGeneration()
    {
        SharedLoadOperation<IReadOnlyCollection<Friend>>? operation = _load_operation;
        long retired_epoch = _fragment_request_epoch;
        long active_epoch = operation?.Epoch ?? 0;
        _load_operation = null;
        DiscardGeneration();
        if (operation is null)
            return;

        _recovery_pending = true;
        _recovery_retired_epoch = retired_epoch;
        _recovery_active_epoch = active_epoch;
        operation.Completion.TrySetException(RecoveryError());
    }

    private FragmentedLoadCorrelationException RecoveryError() =>
        new("friend list", _recovery_retired_epoch, _recovery_active_epoch);

    /// <inheritdoc/>
    protected override void Reset()
    {
        lock (_publication_sync)
        {
            lock (_message_index_sync)
                _message_indices.Clear();
            lock (_sync)
            {
                _friends.Clear();
                _categories.Clear();
                _pending_fragments.Clear();
                _pending_deltas.Clear();
                _discard_until_zero = false;
                _is_loaded = false;
                _is_loading = false;
                _is_stale = false;
                _user_limit = 0;
                _normal_limit = 0;
                _extended_limit = 0;
                _expected_fragments = -1;
                _retired_request_epoch = null;
                _fragment_request_epoch = 0;
                _recovery_pending = false;
                _last_init_answer = null;
                _generation++;
                _revision++;

                TaskCompletionSource<IReadOnlyCollection<Friend>>? completion = _load_operation?.Completion;
                _load_operation = null;
                completion?.TrySetException(
                    new InvalidOperationException("The connection closed while loading the friend list."));
            }
            ResetCompleted?.Invoke();
        }
    }

    private readonly record struct FriendDelta(Friend? Friend, Id FriendId)
    {
        public static FriendDelta Upsert(Friend friend) => new(friend, friend.Id);

        public static FriendDelta Remove(Id friend_id) => new(null, friend_id);

        public void Apply(Dictionary<long, Friend> friends)
        {
            if (Friend is null)
                friends.Remove(FriendId);
            else
                friends[FriendId] = Friend;
        }
    }

    private readonly record struct FriendEvent(FriendUpdateKind Kind, Friend Friend);
}
