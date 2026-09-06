using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class RoomModerationApplication : IApplicationFeature
{
    private const int commit_history_limit = 32;

    private readonly IConnection connection;
    private readonly RoomManager room;
    private readonly ProfileManager profile;
    private readonly RoomBanManager room_bans;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<RoomModerationChanged> changed;
    private readonly object updates_sync = new();
    private readonly List<RoomBanStateUpdate> refresh_commits = [];
    private int disposed;

    public RoomModerationApplication(
        IConnection connection,
        GameState game,
        ApplicationMessageDispatcher message_dispatcher,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(message_dispatcher);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        room = game.Room;
        profile = game.Profile;
        room_bans = game.RoomBans;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new ApplicationEventSource<RoomModerationChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<RoomModerationStateRequest, RoomModerationStateView>(
                RoomModerationApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<RoomModerationRefreshRequest, RoomModerationStateView>(
                RoomModerationApplicationDescriptors.Refresh,
                Refresh),
            new ApplicationCallBinding<RoomModerationMuteRequest, RoomModerationDispatchResult>(
                RoomModerationApplicationDescriptors.Mute,
                Mute),
            new ApplicationCallBinding<RoomModerationTargetRequest, RoomModerationDispatchResult>(
                RoomModerationApplicationDescriptors.Kick,
                Kick),
            new ApplicationCallBinding<RoomModerationBanRequest, RoomModerationDispatchResult>(
                RoomModerationApplicationDescriptors.Ban,
                Ban),
            new ApplicationCallBinding<RoomModerationUnbanRequest, RoomModerationDispatchResult>(
                RoomModerationApplicationDescriptors.Unban,
                Unban),
            new ApplicationCallBinding<RoomModerationTargetRequest, RoomModerationDispatchResult>(
                RoomModerationApplicationDescriptors.Bounce,
                Bounce),
            new ApplicationEventBinding<RoomModerationChanged>(
                RoomModerationApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        room_bans.StateCommitted += OnStateCommitted;
        room_bans.StateChanged += OnStateChanged;
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public RoomModerationStateView ReadState(RoomModerationStateRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        StateReadScope scope = CaptureState();
        if (request.SnapshotRevision is long revision && revision != scope.State.Revision)
            throw new InvalidOperationException("The requested room-ban snapshot revision is no longer current.");
        RoomModerationStateView result = StateView(
            scope.State,
            scope.Session,
            scope.Room,
            request.Offset,
            request.Limit);
        if (!ReferenceEquals(scope.Session, connection.Session) ||
            !ReferenceEquals(scope.State, room_bans.State) ||
            scope.Room != CaptureRoom())
        {
            throw new InvalidOperationException("The room-moderation state changed while it was being read.");
        }
        return result;
    }

    public async ValueTask<RoomModerationStateView> Refresh(
        RoomModerationRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateLimit(request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        ValidateId(request.ExpectedRoomId, nameof(request.ExpectedRoomId));
        ValidateGeneration(request.ExpectedRoomGeneration, nameof(request.ExpectedRoomGeneration));
        RefreshScope scope = CaptureRefreshScope(request, cancellation_token);
        long baseline_revision = -1;
        int armed = 0;
        RoomBanStateUpdate? accepted = null;
        await requests.RequestAsync(
            MessageContracts.Room.Moderation.BansRequest,
            new GetRoomBansRequest(scope.RoomId),
            MessageContracts.Room.Moderation.BansSnapshot,
            scope.Session,
            match: response =>
            {
                if (Volatile.Read(ref armed) == 0 || !RefreshScopeActive(scope))
                    return false;
                lock (updates_sync)
                {
                    if (Volatile.Read(ref armed) == 0)
                        return false;
                    RoomBanStateUpdate? update = FindRefreshCommitLocked(
                        scope,
                        Volatile.Read(ref baseline_revision),
                        response);
                    if (update is null)
                        return false;
                    Volatile.Write(ref accepted, update);
                    return true;
                }
            },
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () =>
            {
                RequireRefreshScope(scope);
                lock (updates_sync)
                {
                    Volatile.Write(ref baseline_revision, room_bans.State.Revision);
                    Volatile.Write(ref armed, 1);
                }
            },
            attempt_start: () =>
            {
                lock (updates_sync)
                {
                    Volatile.Write(ref armed, 0);
                    Volatile.Write(ref accepted, null);
                }
            }).ConfigureAwait(false);
        RequireRefreshScope(scope);
        RoomBanStateUpdate refresh = Volatile.Read(ref accepted)
            ?? throw new InvalidOperationException(
                "The accepted room-ban response was not committed by the passive state owner.");
        RoomSnapshot current_room = CaptureRoom();
        if (!ReferenceEquals(room_bans.State, refresh.State) ||
            !RefreshScopeActive(scope) ||
            current_room != CaptureRoom() ||
            !ReferenceEquals(room_bans.State, refresh.State))
        {
            throw new InvalidOperationException("The refreshed room-ban snapshot changed before it could be returned.");
        }
        RoomModerationStateView result = StateView(
            refresh.State,
            scope.Session,
            current_room,
            0,
            request.Limit);
        if (!ReferenceEquals(room_bans.State, refresh.State) || current_room != CaptureRoom())
            throw new InvalidOperationException("The refreshed room-ban snapshot changed before it could be returned.");
        return result;
    }

    public ValueTask<RoomModerationDispatchResult> Mute(
        RoomModerationMuteRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (request.Minutes is < 0 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(request.Minutes));
        TargetScope scope = CaptureTargetScope(
            request.UserId,
            request.ExpectedSessionGeneration,
            request.ExpectedRoomId,
            request.ExpectedRoomGeneration,
            request.ExpectedUserIndex,
            cancellation_token);
        long dispatch_revision = scope.StateRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.Moderation.UserMute,
            new MuteRoomUserRequest(scope.UserId, scope.RoomId, request.Minutes),
            scope.Session,
            cancellation_token,
            () => dispatch_revision = RequireTargetScope(scope));
        return ValueTask.FromResult(Result(scope, dispatch_revision, 1));
    }

    public ValueTask<RoomModerationDispatchResult> Kick(
        RoomModerationTargetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        TargetScope scope = CaptureTargetScope(request, cancellation_token);
        long dispatch_revision = scope.StateRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.Moderation.UserKick,
            new KickRoomUserRequest(scope.UserId),
            scope.Session,
            cancellation_token,
            () => dispatch_revision = RequireTargetScope(scope));
        return ValueTask.FromResult(Result(scope, dispatch_revision, 1));
    }

    public ValueTask<RoomModerationDispatchResult> Ban(
        RoomModerationBanRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        string duration = BanDuration(request.Length);
        TargetScope scope = CaptureTargetScope(
            request.UserId,
            request.ExpectedSessionGeneration,
            request.ExpectedRoomId,
            request.ExpectedRoomGeneration,
            request.ExpectedUserIndex,
            cancellation_token);
        long dispatch_revision = scope.StateRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.Moderation.UserBan,
            new BanRoomUserRequest(scope.UserId, scope.RoomId, duration),
            scope.Session,
            cancellation_token,
            () => dispatch_revision = RequireTargetScope(scope));
        room_bans.Invalidate(
            scope.Session,
            scope.SessionGeneration,
            scope.RoomGeneration,
            scope.RoomId);
        return ValueTask.FromResult(Result(scope, dispatch_revision, 1));
    }

    public ValueTask<RoomModerationDispatchResult> Unban(
        RoomModerationUnbanRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.UserId, nameof(request.UserId));
        ValidateId(request.RoomId, nameof(request.RoomId));
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        ValidateGeneration(request.ExpectedRoomGeneration, nameof(request.ExpectedRoomGeneration));
        ValidateGeneration(request.ExpectedSnapshotRevision, nameof(request.ExpectedSnapshotRevision));
        UnbanScope scope = CaptureUnbanScope(request, cancellation_token);
        long dispatch_revision = scope.StateRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.Moderation.UserUnban,
            new UnbanRoomUserRequest(scope.UserId, scope.RoomId),
            scope.Session,
            cancellation_token,
            () => dispatch_revision = RequireUnbanScope(scope));
        if (scope.RoomGeneration is long room_generation)
        {
            room_bans.Invalidate(
                scope.Session,
                scope.SessionGeneration,
                room_generation,
                scope.RoomId);
        }
        return ValueTask.FromResult(new RoomModerationDispatchResult(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            dispatch_revision,
            scope.RoomId,
            scope.RoomGeneration,
            scope.UserId,
            null,
            1));
    }

    public ValueTask<RoomModerationDispatchResult> Bounce(
        RoomModerationTargetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        TargetScope scope = CaptureTargetScope(request, cancellation_token);
        long dispatch_revision = scope.StateRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.Moderation.UserBan,
            new BanRoomUserRequest(scope.UserId, scope.RoomId, BanDuration(BanLength.Hour)),
            scope.Session,
            cancellation_token,
            () => dispatch_revision = RequireTargetScope(scope));
        try
        {
            message_dispatcher.Dispatch(
                MessageContracts.Room.Moderation.UserUnban,
                new UnbanRoomUserRequest(scope.UserId, scope.RoomId),
                scope.Session,
                CancellationToken.None,
                () => RequireSessionScope(scope.Session, scope.SessionGeneration));
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                "The bounce ban was dispatched, but its mandatory unban cleanup failed.",
                error);
        }
        finally
        {
            room_bans.Invalidate(
                scope.Session,
                scope.SessionGeneration,
                scope.RoomGeneration,
                scope.RoomId);
        }
        return ValueTask.FromResult(Result(scope, dispatch_revision, 2));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        room_bans.StateCommitted -= OnStateCommitted;
        room_bans.StateChanged -= OnStateChanged;
        lock (updates_sync)
            refresh_commits.Clear();
        changed.Dispose();
    }

    private StateReadScope CaptureState()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Session? session = connection.Session;
            RoomBanState state = room_bans.State;
            RoomSnapshot current_room = CaptureRoom();
            if (!ReferenceEquals(session, connection.Session) ||
                !ReferenceEquals(state, room_bans.State) ||
                current_room != CaptureRoom() ||
                !ReferenceEquals(state.Session, session) ||
                !RoomStateMatches(state, current_room))
            {
                continue;
            }
            return new StateReadScope(session, state, current_room);
        }
        throw new InvalidOperationException("The room-moderation state changed while it was being read.");
    }

    private RefreshScope CaptureRefreshScope(
        RoomModerationRefreshRequest request,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        ValidateWireId(session.Client, request.ExpectedRoomId, nameof(request.ExpectedRoomId));
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(connection.Session, session))
                throw new InvalidOperationException("The hotel session changed before the refresh started.");
            if (!current_room.IsReady)
                throw new InvalidOperationException("A ready hotel room is required.");
            Id room_id = (Id)current_room.RoomId;
            ValidateWireId(session.Client, room_id, nameof(current_room.RoomId));
            RoomBanState state = room_bans.State;
            if (!ReferenceEquals(state.Session, session) ||
                state.RoomGeneration != current_room.Generation ||
                state.RoomId != room_id)
            {
                throw new InvalidOperationException("The room-ban state is not bound to the current ready room.");
            }
            if (request.ExpectedSessionGeneration is long session_generation &&
                state.SessionGeneration != session_generation)
            {
                throw new InvalidOperationException("The expected session generation is no longer active.");
            }
            if (request.ExpectedRoomId is Id expected_room_id && room_id != expected_room_id)
                throw new InvalidOperationException("The expected room is no longer active.");
            if (request.ExpectedRoomGeneration is long room_generation &&
                current_room.Generation != room_generation)
            {
                throw new InvalidOperationException("The expected room generation is no longer active.");
            }
            return new RefreshScope(
                session,
                state.SessionGeneration,
                current_room.Generation,
                room_id);
        });
    }

    private TargetScope CaptureTargetScope(
        RoomModerationTargetRequest request,
        CancellationToken cancellation_token) => CaptureTargetScope(
        request.UserId,
        request.ExpectedSessionGeneration,
        request.ExpectedRoomId,
        request.ExpectedRoomGeneration,
        request.ExpectedUserIndex,
        cancellation_token);

    private TargetScope CaptureTargetScope(
        Id user_id,
        long? expected_session_generation,
        Id? expected_room_id,
        long? expected_room_generation,
        int? expected_user_index,
        CancellationToken cancellation_token)
    {
        ValidateId(user_id, nameof(user_id));
        ValidateGeneration(expected_session_generation, nameof(expected_session_generation));
        ValidateId(expected_room_id, nameof(expected_room_id));
        ValidateGeneration(expected_room_generation, nameof(expected_room_generation));
        if (expected_user_index is < 0)
            throw new ArgumentOutOfRangeException(nameof(expected_user_index));
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        ValidateWireId(session.Client, user_id, nameof(user_id));
        ValidateWireId(session.Client, expected_room_id, nameof(expected_room_id));
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(connection.Session, session))
                throw new InvalidOperationException("The hotel session changed before moderation started.");
            if (!current_room.IsReady)
                throw new InvalidOperationException("A ready hotel room is required.");
            Id room_id = (Id)current_room.RoomId;
            ValidateWireId(session.Client, room_id, nameof(current_room.RoomId));
            RoomBanState state = room_bans.State;
            if (!ReferenceEquals(state.Session, session) ||
                state.RoomGeneration != current_room.Generation ||
                state.RoomId != room_id)
            {
                throw new InvalidOperationException("The room-ban state is not bound to the current ready room.");
            }
            if (expected_session_generation is long session_generation &&
                state.SessionGeneration != session_generation)
            {
                throw new InvalidOperationException("The expected session generation is no longer active.");
            }
            if (expected_room_id is Id required_room_id && room_id != required_room_id)
                throw new InvalidOperationException("The expected room is no longer active.");
            if (expected_room_generation is long room_generation &&
                current_room.Generation != room_generation)
            {
                throw new InvalidOperationException("The expected room generation is no longer active.");
            }
            ProfileState profile_state = profile.State;
            if (!ReferenceEquals(profile_state.Session, session) || profile_state.Identity is not { } identity)
                throw new InvalidOperationException("The active local profile is required.");
            if (identity.Id == user_id)
                throw new InvalidOperationException("A user cannot moderate their own room avatar.");
            if (current_room.AvatarById(user_id) is not User target ||
                current_room.AvatarByIndex(target.Index) is not User indexed_target ||
                indexed_target.Id != user_id)
            {
                throw new InvalidOperationException("The target is not a current room user.");
            }
            if (expected_user_index is int required_index && target.Index != required_index)
                throw new InvalidOperationException("The expected room index no longer belongs to the target.");
            return new TargetScope(
                session,
                state.SessionGeneration,
                state.Revision,
                current_room.Generation,
                room_id,
                user_id,
                target.Index,
                identity.Id);
        });
    }

    private UnbanScope CaptureUnbanScope(
        RoomModerationUnbanRequest request,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        ValidateWireId(session.Client, request.UserId, nameof(request.UserId));
        ValidateWireId(session.Client, request.RoomId, nameof(request.RoomId));
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(connection.Session, session))
                throw new InvalidOperationException("The hotel session changed before the unban started.");
            RoomBanState state = room_bans.State;
            if (!ReferenceEquals(state.Session, session))
                throw new InvalidOperationException("The room-ban state is not bound to the active session.");
            if (request.ExpectedSessionGeneration is long session_generation &&
                state.SessionGeneration != session_generation)
            {
                throw new InvalidOperationException("The expected session generation is no longer active.");
            }
            Id current_room_id = (Id)current_room.RoomId;
            bool current = current_room.IsReady && current_room_id == request.RoomId;
            if (!current)
            {
                if (request.ExpectedRoomGeneration is not null ||
                    request.ExpectedSnapshotRevision is not null)
                {
                    throw new InvalidOperationException(
                        "Room-generation and snapshot-revision guards require the explicit room to be current and ready.");
                }
                return new UnbanScope(
                    session,
                    state.SessionGeneration,
                    state.Revision,
                    request.RoomId,
                    request.UserId,
                    null,
                    null);
            }
            if (state.RoomGeneration != current_room.Generation || state.RoomId != request.RoomId)
                throw new InvalidOperationException("The room-ban state is not bound to the current ready room.");
            if (request.ExpectedRoomGeneration is long room_generation &&
                current_room.Generation != room_generation)
            {
                throw new InvalidOperationException("The expected room generation is no longer active.");
            }
            if (request.ExpectedSnapshotRevision is long revision && state.Revision != revision)
                throw new InvalidOperationException("The expected room-ban snapshot revision is no longer current.");
            return new UnbanScope(
                session,
                state.SessionGeneration,
                state.Revision,
                request.RoomId,
                request.UserId,
                current_room.Generation,
                request.ExpectedSnapshotRevision);
        });
    }

    private bool RefreshScopeActive(RefreshScope scope)
    {
        if (Volatile.Read(ref disposed) != 0 || !ReferenceEquals(connection.Session, scope.Session))
            return false;
        RoomBanState state = room_bans.State;
        if (!ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration ||
            state.RoomGeneration != scope.RoomGeneration ||
            state.RoomId != scope.RoomId)
        {
            return false;
        }
        return room.Capture(current_room =>
            current_room.IsReady &&
            current_room.Generation == scope.RoomGeneration &&
            (Id)current_room.RoomId == scope.RoomId);
    }

    private void RequireRefreshScope(RefreshScope scope)
    {
        if (!RefreshScopeActive(scope))
            throw new InvalidOperationException("The room or hotel session changed during the ban-list refresh.");
    }

    private long RequireTargetScope(TargetScope scope)
    {
        ThrowIfDisposed();
        return room.Capture(current_room =>
        {
            if (!ReferenceEquals(connection.Session, scope.Session))
                throw new InvalidOperationException("The hotel session changed before moderation was dispatched.");
            RoomBanState state = room_bans.State;
            if (!ReferenceEquals(state.Session, scope.Session) ||
                state.SessionGeneration != scope.SessionGeneration ||
                !current_room.IsReady ||
                current_room.Generation != scope.RoomGeneration ||
                (Id)current_room.RoomId != scope.RoomId ||
                state.RoomGeneration != scope.RoomGeneration ||
                state.RoomId != scope.RoomId)
            {
                throw new InvalidOperationException("The captured room scope is no longer active.");
            }
            ProfileState profile_state = profile.State;
            if (!ReferenceEquals(profile_state.Session, scope.Session) ||
                profile_state.Identity?.Id != scope.LocalUserId ||
                scope.LocalUserId == scope.UserId)
            {
                throw new InvalidOperationException("The captured local profile is no longer active.");
            }
            if (current_room.AvatarByIndex(scope.UserIndex) is not User target ||
                target.Id != scope.UserId)
            {
                throw new InvalidOperationException("The target no longer owns the captured room index.");
            }
            return state.Revision;
        });
    }

    private long RequireUnbanScope(UnbanScope scope)
    {
        ThrowIfDisposed();
        if (scope.RoomGeneration is not long room_generation)
        {
            return room.Capture(current_room =>
            {
                long revision = RequireSessionScope(scope.Session, scope.SessionGeneration);
                if (current_room.IsReady && (Id)current_room.RoomId == scope.RoomId)
                {
                    throw new InvalidOperationException(
                        "The explicit unban room became current before dispatch and must be captured again.");
                }
                return revision;
            });
        }
        return room.Capture(current_room =>
        {
            RoomBanState state = room_bans.State;
            if (!ReferenceEquals(connection.Session, scope.Session) ||
                !ReferenceEquals(state.Session, scope.Session) ||
                state.SessionGeneration != scope.SessionGeneration ||
                !current_room.IsReady ||
                current_room.Generation != room_generation ||
                (Id)current_room.RoomId != scope.RoomId ||
                state.RoomGeneration != room_generation ||
                state.RoomId != scope.RoomId)
            {
                throw new InvalidOperationException("The captured room scope is no longer active.");
            }
            if (scope.SnapshotRevision is long expected_revision && state.Revision != expected_revision)
                throw new InvalidOperationException("The expected room-ban snapshot revision is no longer current.");
            return state.Revision;
        });
    }

    private long RequireSessionScope(Session session, long session_generation)
    {
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The hotel session changed before dispatch.");
        RoomBanState state = room_bans.State;
        if (!ReferenceEquals(state.Session, session) || state.SessionGeneration != session_generation)
            throw new InvalidOperationException("The room-ban session generation changed before dispatch.");
        return state.Revision;
    }

    private RoomBanStateUpdate? FindRefreshCommitLocked(
        RefreshScope scope,
        long baseline_revision,
        BannedUsersFromRoom response)
    {
        if (Volatile.Read(ref disposed) != 0)
            return null;
        for (int index = refresh_commits.Count - 1; index >= 0; index--)
        {
            RoomBanStateUpdate update = refresh_commits[index];
            RoomBanState state = update.State;
            if (ReferenceEquals(state.Session, scope.Session) &&
                state.SessionGeneration == scope.SessionGeneration &&
                state.RoomGeneration == scope.RoomGeneration &&
                state.RoomId == scope.RoomId &&
                state.Revision > baseline_revision &&
                RoomBanManager.Equivalent(state, response))
            {
                return update;
            }
        }
        return null;
    }

    private void OnStateCommitted(RoomBanStateUpdate update)
    {
        lock (updates_sync)
        {
            if (update.Kind is RoomBanStateChangeKind.Reset or RoomBanStateChangeKind.RoomChanged)
            {
                refresh_commits.Clear();
                return;
            }
            if (update.Kind is not RoomBanStateChangeKind.Refreshed)
                return;
            if (refresh_commits.Count == commit_history_limit)
                refresh_commits.RemoveAt(0);
            refresh_commits.Add(update);
        }
    }

    private void OnStateChanged(RoomBanStateUpdate update)
    {
        changed.Publish(new RoomModerationChanged(
            ChangeKind(update.Kind),
            time_provider.GetUtcNow(),
            StateSummary(update.State),
            update.UserId));
    }

    private RoomModerationDispatchResult Result(
        TargetScope scope,
        long state_revision,
        int messages_dispatched) => new(
        scope.Session.Client,
        time_provider.GetUtcNow(),
        scope.SessionGeneration,
        state_revision,
        scope.RoomId,
        scope.RoomGeneration,
        scope.UserId,
        scope.UserIndex,
        messages_dispatched);

    private static RoomModerationStateView StateView(
        RoomBanState state,
        Session? session,
        RoomSnapshot current_room,
        int offset,
        int limit)
    {
        RoomBan[] page = state.Bans.Skip(offset).Take(limit).ToArray();
        int next_offset = checked(offset + page.Length);
        return new RoomModerationStateView(
            session is not null,
            session?.Client,
            state.SessionGeneration,
            state.Revision,
            state.RoomGeneration,
            state.RoomId,
            session is not null &&
                current_room.Ready &&
                current_room.Generation == state.RoomGeneration &&
                current_room.RoomId == state.RoomId,
            state.Loaded,
            new RoomBanPage(
                state.Revision,
                state.Bans.Count,
                offset,
                next_offset < state.Bans.Count ? next_offset : null,
                Array.AsReadOnly(page.Select(ban => new RoomBanView(ban.UserId, ban.Name)).ToArray())));
    }

    private static RoomModerationStateSummary StateSummary(RoomBanState state) => new(
        state.Session is not null,
        state.Session?.Client,
        state.SessionGeneration,
        state.Revision,
        state.RoomGeneration,
        state.RoomId,
        state.Loaded,
        state.Bans.Count);

    private RoomSnapshot CaptureRoom() => room.Capture(current_room => new RoomSnapshot(
        current_room.State is RoomSessionState.Entering or RoomSessionState.Ready,
        current_room.IsReady,
        current_room.Generation,
        (Id)current_room.RoomId));

    private static bool RoomStateMatches(RoomBanState state, RoomSnapshot current_room) =>
        state.RoomGeneration == current_room.Generation &&
        state.RoomId == (current_room.Active ? current_room.RoomId : 0);

    private static string BanDuration(BanLength length) => length switch
    {
        BanLength.Hour => "RWUAM_BAN_USER_HOUR",
        BanLength.Day => "RWUAM_BAN_USER_DAY",
        BanLength.Permanent => "RWUAM_BAN_USER_PERM",
        _ => throw new ArgumentOutOfRangeException(nameof(length))
    };

    private static RoomModerationChangeKind ChangeKind(RoomBanStateChangeKind kind) => kind switch
    {
        RoomBanStateChangeKind.Refreshed => RoomModerationChangeKind.Refreshed,
        RoomBanStateChangeKind.UserUnbanned => RoomModerationChangeKind.UserUnbanned,
        RoomBanStateChangeKind.Invalidated => RoomModerationChangeKind.Invalidated,
        RoomBanStateChangeKind.RoomChanged => RoomModerationChangeKind.RoomChanged,
        RoomBanStateChangeKind.Reset => RoomModerationChangeKind.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void ValidatePage(int offset, int limit, long? snapshot_revision)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        ValidateLimit(limit);
        ValidateGeneration(snapshot_revision, nameof(snapshot_revision));
        if (offset != 0 && snapshot_revision is null)
            throw new ArgumentException("Continuation pages require a snapshot revision.", nameof(snapshot_revision));
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidateGeneration(long? generation, string name)
    {
        if (generation < 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateId(Id? value, string name)
    {
        if (value is Id id)
            ValidateId(id, name);
    }

    private static void ValidateId(Id value, string name)
    {
        if ((long)value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateWireId(ClientType client, Id? value, string name)
    {
        if (value is Id id)
            ValidateWireId(client, id, name);
    }

    private static void ValidateWireId(ClientType client, Id value, string name)
    {
        ValidateId(value, name);
        if (client is ClientType.Flash && (long)value > int.MaxValue)
            throw new ArgumentOutOfRangeException(name, "The identifier does not fit the Flash wire format.");
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private readonly record struct RoomSnapshot(
        bool Active,
        bool Ready,
        long Generation,
        Id RoomId);

    private readonly record struct StateReadScope(
        Session? Session,
        RoomBanState State,
        RoomSnapshot Room);

    private readonly record struct RefreshScope(
        Session Session,
        long SessionGeneration,
        long RoomGeneration,
        Id RoomId);

    private readonly record struct TargetScope(
        Session Session,
        long SessionGeneration,
        long StateRevision,
        long RoomGeneration,
        Id RoomId,
        Id UserId,
        int UserIndex,
        Id LocalUserId);

    private readonly record struct UnbanScope(
        Session Session,
        long SessionGeneration,
        long StateRevision,
        Id RoomId,
        Id UserId,
        long? RoomGeneration,
        long? SnapshotRevision);
}
