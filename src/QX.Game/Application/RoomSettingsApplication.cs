using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Text;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed record RoomSettingsCacheEntry(
    Id RoomId,
    long OperationRevision,
    long SnapshotRevision,
    long? RoomGeneration,
    bool Loaded,
    RoomSettings? Settings);

internal sealed record RoomSettingsManagerState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    bool RoomActive,
    long RoomGeneration,
    Id CurrentRoomId,
    IReadOnlyDictionary<Id, RoomSettingsCacheEntry> Rooms);

internal enum RoomSettingsStateChangeKind
{
    Refreshed,
    RequestFailed,
    Invalidated,
    SaveSucceeded,
    SaveFailed,
    InvalidSnapshot,
    RoomChanged,
    Reset
}

internal sealed record RoomSettingsStateUpdate(
    RoomSettingsStateChangeKind Kind,
    RoomSettingsManagerState State,
    Id RoomId,
    RoomSettingsCacheEntry? Entry = null,
    int? ErrorCode = null,
    string? ErrorInfo = null);

internal readonly record struct RoomSettingsRoomScope(
    bool Active,
    long Generation,
    Id RoomId);

internal sealed class RoomSettingsManager : GameStateManager
{
    internal const int MaximumCachedRooms = 64;
    internal const int MaximumCollectionEntries = 500;

    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<RoomSettingsStateUpdate> publications = [];
    private readonly Dictionary<long, Id> pins = [];
    private RoomSettingsManagerState state = new(
        null,
        0,
        0,
        false,
        0,
        0,
        EmptyRooms());
    private long next_pin;
    private long next_snapshot_revision;
    private bool publishing;

    internal RoomSettingsManagerState State => Volatile.Read(ref state);
    internal Func<RoomSettingsRoomScope>? RoomScope { get; set; }
    internal event Action<RoomSettingsStateUpdate>? StateCommitted;
    internal event Action<RoomSettingsStateUpdate>? StateChanged;

    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(CommitReset);
        OnIncoming(MessageContracts.Room.Settings.Snapshot, ApplySnapshot);
        OnIncoming(MessageContracts.Room.Settings.RequestFailed, ApplyRequestFailure);
        OnIncoming(MessageContracts.Room.Settings.SaveSucceeded, ApplySaveSuccess);
        OnIncoming(MessageContracts.Room.Settings.SaveFailed, ApplySaveFailure);
        OnOutgoing(MessageContracts.Room.Settings.Save, InvalidateSavedRoom);
    }

    internal void EnterRoom(Id _) => CommitRoomScope();

    internal void LeaveRoom() => CommitRoomScope();

    internal long PinRoom(Session session, long session_generation, Id room_id)
    {
        lock (state_sync)
        {
            RoomSettingsManagerState current = state;
            if (!ReferenceEquals(current.Session, session) ||
                current.SessionGeneration != session_generation)
            {
                throw new InvalidOperationException("The room-settings session changed before its cache entry could be pinned.");
            }
            if (pins.Count >= MaximumCachedRooms)
                throw new InvalidOperationException("The room-settings operation capacity is exhausted.");
            long pin = checked(++next_pin);
            pins.Add(pin, room_id);
            return pin;
        }
    }

    internal void UnpinRoom(long pin)
    {
        lock (state_sync)
            pins.Remove(pin);
    }

    protected override void Reset() => CommitReset(CurrentSession);

    private void ApplySnapshot(RoomSettings message)
    {
        if (message.Tags.Count > MaximumCollectionEntries ||
            message.NftGroupIds.Count > MaximumCollectionEntries)
        {
            CommitRoom(
                RoomSettingsStateChangeKind.InvalidSnapshot,
                message.RoomId,
                null,
                null,
                "The room-settings response exceeds the bounded collection limit.");
            return;
        }
        RoomSettings snapshot = Freeze(message);
        CommitRoom(RoomSettingsStateChangeKind.Refreshed, message.RoomId, snapshot);
    }

    private void ApplyRequestFailure(RoomSettingsError message) => CommitRoom(
        RoomSettingsStateChangeKind.RequestFailed,
        message.RoomId,
        null,
        message.ErrorCode);

    private void ApplySaveSuccess(RoomSettingsSaved message) => CommitRoom(
        RoomSettingsStateChangeKind.SaveSucceeded,
        message.RoomId);

    private void ApplySaveFailure(RoomSettingsSaveError message) => CommitRoom(
        RoomSettingsStateChangeKind.SaveFailed,
        message.RoomId,
        null,
        message.ErrorCode,
        message.Info);

    private void InvalidateSavedRoom(SaveRoomSettingsRequest message) => CommitRoom(
        RoomSettingsStateChangeKind.Invalidated,
        message.RoomId);

    private void CommitRoom(
        RoomSettingsStateChangeKind kind,
        Id room_id,
        RoomSettings? settings = null,
        int? error_code = null,
        string? error_info = null)
    {
        Session? session = CurrentSession;
        if (session is null)
            return;
        bool drain;
        lock (publication_sync)
        {
            RoomSettingsStateUpdate update;
            lock (state_sync)
            {
                RoomSettingsManagerState current = state;
                if (!ReferenceEquals(current.Session, session))
                    return;
                long revision = checked(current.Revision + 1);
                var rooms = new Dictionary<Id, RoomSettingsCacheEntry>(current.Rooms);
                rooms.TryGetValue(room_id, out RoomSettingsCacheEntry? entry);
                if (entry is null && !MakeRoom(rooms, room_id))
                    return;
                long? room_generation = current.RoomActive && current.CurrentRoomId == room_id
                    ? current.RoomGeneration
                    : null;
                RoomSettingsCacheEntry? updated_entry = kind switch
                {
                    RoomSettingsStateChangeKind.Refreshed => new RoomSettingsCacheEntry(
                        room_id,
                        revision,
                        checked(++next_snapshot_revision),
                        room_generation,
                        true,
                        settings ?? throw new InvalidOperationException()),
                    RoomSettingsStateChangeKind.RequestFailed or
                    RoomSettingsStateChangeKind.InvalidSnapshot or
                    RoomSettingsStateChangeKind.Invalidated => new RoomSettingsCacheEntry(
                        room_id,
                        revision,
                        checked(++next_snapshot_revision),
                        room_generation,
                        false,
                        null),
                    RoomSettingsStateChangeKind.SaveSucceeded or
                    RoomSettingsStateChangeKind.SaveFailed => new RoomSettingsCacheEntry(
                        room_id,
                        revision,
                        entry?.SnapshotRevision ?? checked(++next_snapshot_revision),
                        room_generation,
                        false,
                        null),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                };
                if (updated_entry is not null)
                    rooms[room_id] = updated_entry;
                RoomSettingsManagerState updated = current with
                {
                    Revision = revision,
                    Rooms = FreezeRooms(rooms)
                };
                Volatile.Write(ref state, updated);
                update = new RoomSettingsStateUpdate(
                    kind,
                    updated,
                    room_id,
                    updated_entry,
                    error_code,
                    error_info);
            }
            StateCommitted?.Invoke(update);
            drain = Enqueue(update);
        }
        if (drain)
            DrainPublications();
    }

    private void CommitRoomScope()
    {
        Session? session = CurrentSession;
        if (session is null)
            return;
        RoomSettingsRoomScope scope = CaptureRoomScope();
        bool drain;
        lock (publication_sync)
        {
            RoomSettingsStateUpdate update;
            lock (state_sync)
            {
                RoomSettingsManagerState current = state;
                if (!ReferenceEquals(current.Session, session) ||
                    current.RoomActive == scope.Active &&
                    current.RoomGeneration == scope.Generation &&
                    current.CurrentRoomId == scope.RoomId)
                {
                    return;
                }
                RoomSettingsManagerState updated = current with
                {
                    Revision = checked(current.Revision + 1),
                    RoomActive = scope.Active,
                    RoomGeneration = scope.Generation,
                    CurrentRoomId = scope.Active ? scope.RoomId : 0
                };
                Volatile.Write(ref state, updated);
                update = new RoomSettingsStateUpdate(
                    RoomSettingsStateChangeKind.RoomChanged,
                    updated,
                    scope.Active ? scope.RoomId : 0);
            }
            StateCommitted?.Invoke(update);
            drain = Enqueue(update);
        }
        if (drain)
            DrainPublications();
    }

    private void CommitReset(Session? session)
    {
        RoomSettingsRoomScope scope = session is null
            ? new RoomSettingsRoomScope(false, 0, 0)
            : CaptureRoomScope();
        bool drain;
        lock (publication_sync)
        {
            RoomSettingsStateUpdate update;
            lock (state_sync)
            {
                RoomSettingsManagerState current = state;
                bool session_changed = !ReferenceEquals(current.Session, session);
                if (!session_changed &&
                    current.Rooms.Count == 0 &&
                    current.RoomActive == scope.Active &&
                    current.RoomGeneration == scope.Generation &&
                    current.CurrentRoomId == scope.RoomId)
                {
                    return;
                }
                pins.Clear();
                RoomSettingsManagerState updated = new(
                    session,
                    session_changed
                        ? checked(current.SessionGeneration + 1)
                        : current.SessionGeneration,
                    checked(current.Revision + 1),
                    scope.Active,
                    scope.Generation,
                    scope.Active ? scope.RoomId : 0,
                    EmptyRooms());
                Volatile.Write(ref state, updated);
                update = new RoomSettingsStateUpdate(
                    RoomSettingsStateChangeKind.Reset,
                    updated,
                    scope.Active ? scope.RoomId : 0);
            }
            StateCommitted?.Invoke(update);
            drain = Enqueue(update);
        }
        if (drain)
            DrainPublications();
    }

    private bool MakeRoom(Dictionary<Id, RoomSettingsCacheEntry> rooms, Id room_id)
    {
        if (rooms.ContainsKey(room_id) || rooms.Count < MaximumCachedRooms)
            return true;
        HashSet<Id> pinned = pins.Values.ToHashSet();
        RoomSettingsCacheEntry? candidate = rooms.Values
            .Where(entry => !pinned.Contains(entry.RoomId))
            .OrderBy(entry => entry.OperationRevision)
            .ThenBy(entry => (long)entry.RoomId)
            .FirstOrDefault();
        if (candidate is null)
            return false;
        rooms.Remove(candidate.RoomId);
        return true;
    }

    private bool Enqueue(RoomSettingsStateUpdate update)
    {
        publications.Enqueue(update);
        if (publishing)
            return false;
        publishing = true;
        return true;
    }

    private void DrainPublications()
    {
        Exception? failure = null;
        while (true)
        {
            RoomSettingsStateUpdate update;
            lock (publication_sync)
            {
                if (!publications.TryDequeue(out update!))
                {
                    publishing = false;
                    break;
                }
            }
            try
            {
                StateChanged?.Invoke(update);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private RoomSettingsRoomScope CaptureRoomScope() =>
        RoomScope?.Invoke() ?? new RoomSettingsRoomScope(false, 0, 0);

    private static RoomSettings Freeze(RoomSettings value) => value with
    {
        Tags = Array.AsReadOnly(value.Tags.ToArray()),
        NftGroupIds = Array.AsReadOnly(value.NftGroupIds.ToArray())
    };

    private static IReadOnlyDictionary<Id, RoomSettingsCacheEntry> FreezeRooms(
        Dictionary<Id, RoomSettingsCacheEntry> rooms) =>
        new ReadOnlyDictionary<Id, RoomSettingsCacheEntry>(rooms);

    private static IReadOnlyDictionary<Id, RoomSettingsCacheEntry> EmptyRooms() =>
        FreezeRooms(new Dictionary<Id, RoomSettingsCacheEntry>());
}

internal sealed class RoomSettingsApplication : IApplicationFeature
{
    private const int maximum_lanes = 64;
    private const int maximum_lane_references = 64;
    private const int maximum_attempts = 2;
    private static readonly TimeSpan retry_delay = TimeSpan.FromMilliseconds(150);

    private readonly IConnection connection;
    private readonly RoomManager room;
    private readonly RoomSettingsManager settings;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<RoomSettingsChanged> changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetime_token;
    private readonly object lifecycle_sync = new();
    private readonly object lanes_sync = new();
    private readonly Dictionary<RoomSettingsLaneKey, RoomSettingsLane> lanes = [];
    private int active_dispatches;
    private int lane_references;
    private int disposed;

    public RoomSettingsApplication(
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
        settings = game.RoomSettings;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        lifetime_token = lifetime.Token;
        changed = new ApplicationEventSource<RoomSettingsChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<RoomSettingsStateRequest, RoomSettingsStateView>(
                RoomSettingsApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<RoomSettingsGetRequest, RoomSettingsStateView>(
                RoomSettingsApplicationDescriptors.Get,
                Get),
            new ApplicationCallBinding<RoomSettingsSaveRequest, RoomSettingsSaveReceipt>(
                RoomSettingsApplicationDescriptors.Save,
                Save),
            new ApplicationEventBinding<RoomSettingsChanged>(
                RoomSettingsApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        settings.StateCommitted += OnStateCommitted;
        settings.StateChanged += OnStateChanged;
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public RoomSettingsStateView ReadState(RoomSettingsStateRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.RoomId, nameof(request.RoomId));
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Session? session = connection.Session;
            RoomSettingsManagerState current = settings.State;
            RoomSettingsRoomScope room_scope = CaptureRoom();
            current.Rooms.TryGetValue(request.RoomId, out RoomSettingsCacheEntry? entry);
            RoomSettingsStateView view = StateView(current, session, room_scope, request.RoomId, entry);
            if (ReferenceEquals(session, connection.Session) &&
                ReferenceEquals(current, settings.State) &&
                room_scope == CaptureRoom())
            {
                return view;
            }
        }
        throw new InvalidOperationException("The room-settings state changed while it was being read.");
    }

    public async ValueTask<RoomSettingsStateView> Get(
        RoomSettingsGetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.RoomId, nameof(request.RoomId));
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        ValidateGeneration(request.ExpectedRoomGeneration, nameof(request.ExpectedRoomGeneration));
        long started = time_provider.GetTimestamp();
        RoomSettingsOperationScope initial_scope = CaptureScope(
            request.RoomId,
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration);
        using CancellationTokenSource caller = LinkCancellation(cancellation_token);
        RoomSettingsLaneKey key = new(initial_scope.SessionGeneration, request.RoomId);
        RoomSettingsLane lane = ReferenceLane(key);
        bool entered = false;
        RoomSettingsOperation? operation = null;
        try
        {
            await EnterLane(
                lane,
                started,
                request.TimeoutMilliseconds,
                RoomSettingsOperationKind.Get,
                caller.Token).ConfigureAwait(false);
            entered = true;
            ThrowIfDisposed();
            RoomSettingsOperationScope scope = CaptureScope(
                request.RoomId,
                request.ExpectedSessionGeneration,
                request.ExpectedRoomGeneration);
            RequireSameScope(initial_scope, scope);
            operation = StartOperation(lane, key, scope, RoomSettingsOperationKind.Get);
            entered = false;
            try
            {
                RoomSettingsStateUpdate terminal = await RunGet(
                    operation,
                    started,
                    request.TimeoutMilliseconds,
                    caller.Token).ConfigureAwait(false);
                if (terminal.Kind is RoomSettingsStateChangeKind.RequestFailed)
                {
                    throw new RoomSettingsRejectedException(
                        RoomSettingsOperationKind.Get,
                        request.RoomId,
                        terminal.ErrorCode ?? -1,
                        terminal.ErrorInfo);
                }
                if (terminal.Kind is RoomSettingsStateChangeKind.InvalidSnapshot)
                    throw new InvalidDataException(terminal.ErrorInfo);
                RoomSettingsCacheEntry entry = terminal.Entry
                    ?? throw new InvalidOperationException("The room-settings snapshot was not committed.");
                RequireReturnedEntry(operation.Scope, entry);
                RoomSettingsStateView view = StateView(
                    terminal.State,
                    operation.Scope.Session,
                    CaptureRoom(),
                    request.RoomId,
                    entry);
                RequireReturnedEntry(operation.Scope, entry);
                return view;
            }
            catch (Exception error)
            {
                ReleaseUnsent(operation, error);
                throw;
            }
        }
        catch (OperationCanceledException) when (lifetime_token.IsCancellationRequested)
        {
            throw Disposed();
        }
        finally
        {
            if (entered)
                ReleaseUnenteredLane(lane, key);
            ReleaseLaneReference(lane, key);
        }
    }

    public async ValueTask<RoomSettingsSaveReceipt> Save(
        RoomSettingsSaveRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Settings);
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        ValidateGeneration(request.ExpectedRoomGeneration, nameof(request.ExpectedRoomGeneration));
        ValidatePositiveRevision(request.ExpectedOperationRevision, nameof(request.ExpectedOperationRevision));
        ValidatePositiveRevision(request.ExpectedSnapshotRevision, nameof(request.ExpectedSnapshotRevision));
        RoomSettingsValues values = Freeze(request.Settings);
        ValidateValues(values, request.Password);
        long started = time_provider.GetTimestamp();
        RoomSettingsOperationScope initial_scope = CaptureScope(
            values.RoomId,
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            request.ExpectedOperationRevision,
            request.ExpectedSnapshotRevision);
        ValidateWireValues(initial_scope.Session.Client, values);
        SaveRoomSettingsRequest message = SaveMessage(values, request.Password);
        using CancellationTokenSource caller = LinkCancellation(cancellation_token);
        RoomSettingsLaneKey key = new(initial_scope.SessionGeneration, values.RoomId);
        RoomSettingsLane lane = ReferenceLane(key);
        bool entered = false;
        RoomSettingsOperation? operation = null;
        try
        {
            await EnterLane(
                lane,
                started,
                request.TimeoutMilliseconds,
                RoomSettingsOperationKind.Save,
                caller.Token).ConfigureAwait(false);
            entered = true;
            ThrowIfDisposed();
            RoomSettingsOperationScope scope = CaptureScope(
                values.RoomId,
                request.ExpectedSessionGeneration,
                request.ExpectedRoomGeneration,
                request.ExpectedOperationRevision,
                request.ExpectedSnapshotRevision);
            RequireSameScope(initial_scope, scope);
            operation = StartOperation(lane, key, scope, RoomSettingsOperationKind.Save);
            entered = false;
            try
            {
                ArmAndDispatch(operation, MessageContracts.Room.Settings.Save, message, caller.Token);
                RoomSettingsStateUpdate terminal;
                if (operation.Response.Task.IsCompleted)
                {
                    terminal = await operation.Response.Task.ConfigureAwait(false);
                }
                else if (operation.ScopeFailure.Task.IsCompleted)
                {
                    throw await operation.ScopeFailure.Task.ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        terminal = await WaitForTerminal(
                            operation,
                            RequireRemaining(started, request.TimeoutMilliseconds, RoomSettingsOperationKind.Save),
                            caller.Token).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        if (operation.Response.Task.IsCompleted)
                            terminal = await operation.Response.Task.ConfigureAwait(false);
                        else if (operation.ScopeFailure.Task.IsCompleted)
                            throw await operation.ScopeFailure.Task.ConfigureAwait(false);
                        else
                            throw Timeout(RoomSettingsOperationKind.Save, request.TimeoutMilliseconds);
                    }
                }
                if (terminal.Kind is RoomSettingsStateChangeKind.SaveFailed)
                {
                    throw new RoomSettingsRejectedException(
                        RoomSettingsOperationKind.Save,
                        values.RoomId,
                        terminal.ErrorCode ?? -1,
                        terminal.ErrorInfo);
                }
                RequireReturnedScope(operation.Scope);
                RoomSettingsCacheEntry entry = terminal.Entry
                    ?? throw new InvalidOperationException("The room-settings save acknowledgement was not committed.");
                return new RoomSettingsSaveReceipt(
                    operation.Scope.Session.Client,
                    time_provider.GetUtcNow(),
                    operation.Scope.SessionGeneration,
                    terminal.State.Revision,
                    values.RoomId,
                    operation.Scope.RoomGeneration,
                    entry.OperationRevision,
                    entry.SnapshotRevision);
            }
            catch (Exception error)
            {
                ReleaseUnsent(operation, error);
                throw;
            }
        }
        catch (OperationCanceledException) when (lifetime_token.IsCancellationRequested)
        {
            throw Disposed();
        }
        finally
        {
            if (entered)
                ReleaseUnenteredLane(lane, key);
            ReleaseLaneReference(lane, key);
        }
    }

    public void Dispose()
    {
        lock (lifecycle_sync)
        {
            if (disposed != 0)
                return;
            Volatile.Write(ref disposed, 1);
            while (active_dispatches != 0)
                Monitor.Wait(lifecycle_sync);
        }
        RoomSettingsOperation[] active;
        lock (lanes_sync)
        {
            active = lanes.Values
                .Select(lane => lane.Active)
                .Where(operation => operation is not null)
                .Cast<RoomSettingsOperation>()
                .ToArray();
        }
        settings.StateCommitted -= OnStateCommitted;
        settings.StateChanged -= OnStateChanged;
        foreach (RoomSettingsOperation operation in active)
        {
            ObjectDisposedException error = Disposed();
            lock (operation.Sync)
            {
                operation.ScopeFailure.TrySetResult(error);
                operation.Response.TrySetException(error);
                operation.LaneSettled.TrySetException(error);
            }
        }
        lifetime.Cancel();
        changed.Dispose();
        lifetime.Dispose();
    }

    private async Task<RoomSettingsStateUpdate> RunGet(
        RoomSettingsOperation operation,
        long started,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        for (int attempt = 1; attempt <= maximum_attempts; attempt++)
        {
            if (operation.Response.Task.IsCompleted)
                return await operation.Response.Task.ConfigureAwait(false);
            if (!ArmAndDispatch(
                operation,
                MessageContracts.Room.Settings.Request,
                new GetRoomSettingsRequest(operation.Scope.RoomId),
                cancellation_token))
            {
                return await operation.Response.Task.ConfigureAwait(false);
            }
            if (operation.Response.Task.IsCompleted)
                return await operation.Response.Task.ConfigureAwait(false);
            if (operation.ScopeFailure.Task.IsCompleted)
                throw await operation.ScopeFailure.Task.ConfigureAwait(false);
            TimeSpan remaining = RequireRemaining(
                started,
                timeout_milliseconds,
                RoomSettingsOperationKind.Get);
            int attempts_left = maximum_attempts - attempt + 1;
            TimeSpan attempt_timeout = TimeSpan.FromTicks(
                Math.Max(1, remaining.Ticks / attempts_left));
            try
            {
                return await WaitForTerminal(
                    operation,
                    attempt_timeout,
                    cancellation_token).ConfigureAwait(false);
            }
            catch (TimeoutException) when (attempt < maximum_attempts)
            {
                if (operation.Response.Task.IsCompleted)
                    return await operation.Response.Task.ConfigureAwait(false);
                if (operation.ScopeFailure.Task.IsCompleted)
                    throw await operation.ScopeFailure.Task.ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                if (operation.Response.Task.IsCompleted)
                    return await operation.Response.Task.ConfigureAwait(false);
                if (operation.ScopeFailure.Task.IsCompleted)
                    throw await operation.ScopeFailure.Task.ConfigureAwait(false);
                throw Timeout(RoomSettingsOperationKind.Get, timeout_milliseconds);
            }

            TimeSpan available = RequireRemaining(
                started,
                timeout_milliseconds,
                RoomSettingsOperationKind.Get);
            TimeSpan delay = TimeSpan.FromTicks(
                Math.Min(retry_delay.Ticks, Math.Max(1, available.Ticks / 4)));
            Task completed = await Task.WhenAny(
                operation.Response.Task,
                operation.ScopeFailure.Task,
                Task.Delay(delay, time_provider, cancellation_token)).ConfigureAwait(false);
            if (completed == operation.Response.Task)
                return await operation.Response.Task.ConfigureAwait(false);
            if (completed == operation.ScopeFailure.Task)
                throw await operation.ScopeFailure.Task.ConfigureAwait(false);
        }
        throw Timeout(RoomSettingsOperationKind.Get, timeout_milliseconds);
    }

    private bool ArmAndDispatch<T>(
        RoomSettingsOperation operation,
        MessageContract<T> contract,
        T message,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        EnterDispatch();
        try
        {
            try
            {
                message_dispatcher.Dispatch(
                    contract,
                    message,
                    operation.Scope.Session,
                    cancellation_token,
                    () =>
                    {
                        RequireScope(operation.Scope);
                        lock (operation.Sync)
                        {
                            if (operation.Response.Task.IsCompleted)
                                throw new RoomSettingsOperationSettledException();
                            if (!operation.Armed)
                            {
                                operation.BaselineRevision = settings.State.Revision;
                                operation.Armed = true;
                            }
                            operation.DispatchCount = checked(operation.DispatchCount + 1);
                        }
                    });
                return true;
            }
            catch (RoomSettingsOperationSettledException)
            {
                return false;
            }
        }
        finally
        {
            ExitDispatch();
        }
    }

    private async Task<RoomSettingsStateUpdate> WaitForTerminal(
        RoomSettingsOperation operation,
        TimeSpan timeout,
        CancellationToken cancellation_token)
    {
        Task completed = await Task.WhenAny(
            operation.Response.Task,
            operation.ScopeFailure.Task).WaitAsync(
                timeout,
                time_provider,
                cancellation_token).ConfigureAwait(false);
        if (completed == operation.ScopeFailure.Task)
            throw await operation.ScopeFailure.Task.ConfigureAwait(false);
        return await operation.Response.Task.ConfigureAwait(false);
    }

    private RoomSettingsOperation StartOperation(
        RoomSettingsLane lane,
        RoomSettingsLaneKey key,
        RoomSettingsOperationScope scope,
        RoomSettingsOperationKind kind)
    {
        long pin = settings.PinRoom(scope.Session, scope.SessionGeneration, scope.RoomId);
        try
        {
            var operation = new RoomSettingsOperation(scope, kind, pin);
            lock (lanes_sync)
            {
                ThrowIfDisposed();
                if (!lanes.TryGetValue(key, out RoomSettingsLane? current) ||
                    !ReferenceEquals(current, lane) ||
                    lane.Active is not null)
                {
                    throw new InvalidOperationException("The room-settings lane changed before the operation started.");
                }
                lane.Active = operation;
            }
            _ = SettleLane(lane, key, operation);
            return operation;
        }
        catch
        {
            settings.UnpinRoom(pin);
            throw;
        }
    }

    private async Task SettleLane(
        RoomSettingsLane lane,
        RoomSettingsLaneKey key,
        RoomSettingsOperation operation)
    {
        try
        {
            await operation.LaneSettled.Task.ConfigureAwait(false);
        }
        catch
        {
        }
        long pin = operation.Pin;
        lock (lanes_sync)
        {
            if (ReferenceEquals(lane.Active, operation))
            {
                lane.Active = null;
                lane.Gate.Release();
            }
            RemoveIdleLane(lane, key);
        }
        settings.UnpinRoom(pin);
    }

    private void ReleaseUnsent(RoomSettingsOperation operation, Exception error)
    {
        lock (operation.Sync)
        {
            if (operation.DispatchCount == 0)
            {
                operation.Response.TrySetException(error);
                operation.LaneSettled.TrySetException(error);
            }
        }
    }

    private RoomSettingsLane ReferenceLane(RoomSettingsLaneKey key)
    {
        lock (lanes_sync)
        {
            ThrowIfDisposed();
            if (lane_references >= maximum_lane_references)
                throw new InvalidOperationException("The room-settings operation capacity is exhausted.");
            if (!lanes.TryGetValue(key, out RoomSettingsLane? lane))
            {
                if (lanes.Count >= maximum_lanes)
                    throw new InvalidOperationException("The room-settings lane capacity is exhausted.");
                lane = new RoomSettingsLane();
                lanes.Add(key, lane);
            }
            lane.References = checked(lane.References + 1);
            lane_references++;
            return lane;
        }
    }

    private async Task EnterLane(
        RoomSettingsLane lane,
        long started,
        int timeout_milliseconds,
        RoomSettingsOperationKind kind,
        CancellationToken cancellation_token)
    {
        TimeSpan remaining = RequireRemaining(started, timeout_milliseconds, kind);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellation_token);
        Task gate_wait = lane.Gate.WaitAsync(wait.Token);
        try
        {
            await gate_wait.WaitAsync(remaining, time_provider, cancellation_token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            wait.Cancel();
            await ReleaseLateGate(gate_wait, lane).ConfigureAwait(false);
            ThrowIfDisposed();
            throw Timeout(kind, timeout_milliseconds);
        }
        catch
        {
            wait.Cancel();
            await ReleaseLateGate(gate_wait, lane).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ReleaseLateGate(Task gate_wait, RoomSettingsLane lane)
    {
        try
        {
            await gate_wait.ConfigureAwait(false);
            lane.Gate.Release();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ReleaseUnenteredLane(RoomSettingsLane lane, RoomSettingsLaneKey key)
    {
        lock (lanes_sync)
        {
            lane.Gate.Release();
            RemoveIdleLane(lane, key);
        }
    }

    private void ReleaseLaneReference(RoomSettingsLane lane, RoomSettingsLaneKey key)
    {
        lock (lanes_sync)
        {
            lane.References--;
            lane_references--;
            if (lane.References < 0)
                throw new InvalidOperationException("The room-settings lane reference count became negative.");
            if (lane_references < 0)
                throw new InvalidOperationException("The room-settings operation reference count became negative.");
            RemoveIdleLane(lane, key);
        }
    }

    private void RemoveIdleLane(RoomSettingsLane lane, RoomSettingsLaneKey key)
    {
        if (lane.References == 0 &&
            lane.Active is null &&
            lane.Gate.CurrentCount == 1 &&
            lanes.TryGetValue(key, out RoomSettingsLane? current) &&
            ReferenceEquals(current, lane))
        {
            lanes.Remove(key);
        }
    }

    private void OnStateCommitted(RoomSettingsStateUpdate update)
    {
        lock (lifecycle_sync)
        {
            if (disposed != 0)
                return;
            RoomSettingsOperation[] active;
            lock (lanes_sync)
            {
                active = lanes.Values
                    .Select(lane => lane.Active)
                    .Where(operation => operation is not null)
                    .Cast<RoomSettingsOperation>()
                    .ToArray();
            }
            foreach (RoomSettingsOperation operation in active)
            {
                lock (operation.Sync)
                {
                    if (update.Kind is RoomSettingsStateChangeKind.Reset ||
                        !ReferenceEquals(update.State.Session, operation.Scope.Session) ||
                        update.State.SessionGeneration != operation.Scope.SessionGeneration ||
                        !ReferenceEquals(connection.Session, operation.Scope.Session))
                    {
                        Exception error = Disconnected(operation.Kind);
                        operation.Response.TrySetException(error);
                        operation.LaneSettled.TrySetException(error);
                        continue;
                    }
                    if (operation.Scope.RoomGeneration is long room_generation &&
                        (!update.State.RoomActive ||
                         update.State.CurrentRoomId != operation.Scope.RoomId ||
                         update.State.RoomGeneration != room_generation))
                    {
                        operation.ScopeFailure.TrySetResult(
                            new InvalidOperationException("The active room generation changed during the room-settings operation."));
                    }
                    if (!operation.Armed ||
                        update.State.Revision <= operation.BaselineRevision ||
                        update.RoomId != operation.Scope.RoomId ||
                        !TerminalFor(operation.Kind, update.Kind) ||
                        operation.TerminalCount >= operation.DispatchCount)
                    {
                        continue;
                    }
                    operation.TerminalCount = checked(operation.TerminalCount + 1);
                    operation.Response.TrySetResult(update);
                    if (operation.TerminalCount >= operation.DispatchCount)
                        operation.LaneSettled.TrySetResult(true);
                }
            }
        }
    }

    private void OnStateChanged(RoomSettingsStateUpdate update)
    {
        RoomSettingsCacheEntry? entry = update.Entry;
        changed.Publish(new RoomSettingsChanged(
            ChangeKind(update.Kind),
            time_provider.GetUtcNow(),
            update.State.Session?.Client,
            update.State.SessionGeneration,
            update.State.Revision,
            update.RoomId,
            entry?.RoomGeneration,
            entry?.OperationRevision ?? 0,
            entry?.SnapshotRevision ?? 0,
            entry?.Loaded == true,
            update.ErrorCode,
            update.ErrorInfo));
    }

    private RoomSettingsOperationScope CaptureScope(
        Id room_id,
        long? expected_session_generation,
        long? expected_room_generation,
        long? expected_operation_revision = null,
        long? expected_snapshot_revision = null)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Session session = connection.Session
                ?? throw new InvalidOperationException("An active hotel session is required.");
            RoomSettingsManagerState current = settings.State;
            RoomSettingsRoomScope room_scope = CaptureRoom();
            if (!ReferenceEquals(session, connection.Session) ||
                !ReferenceEquals(current, settings.State) ||
                room_scope != CaptureRoom() ||
                !ReferenceEquals(current.Session, session))
            {
                continue;
            }
            if (expected_session_generation is long session_generation &&
                current.SessionGeneration != session_generation)
            {
                throw new InvalidOperationException("The expected hotel-session generation is no longer active.");
            }
            long? bound_room_generation = room_scope.Active && room_scope.RoomId == room_id
                ? room_scope.Generation
                : null;
            if (expected_room_generation is long required_room_generation &&
                bound_room_generation != required_room_generation)
            {
                throw new InvalidOperationException("The expected room generation is no longer active for the target room.");
            }
            current.Rooms.TryGetValue(room_id, out RoomSettingsCacheEntry? entry);
            if (expected_operation_revision is long operation_revision &&
                entry?.OperationRevision != operation_revision)
            {
                throw new InvalidOperationException("The expected room-settings operation revision is no longer current.");
            }
            if (expected_snapshot_revision is long snapshot_revision &&
                entry?.SnapshotRevision != snapshot_revision)
            {
                throw new InvalidOperationException("The expected room-settings snapshot revision is no longer current.");
            }
            return new RoomSettingsOperationScope(
                session,
                current.SessionGeneration,
                room_id,
                bound_room_generation,
                expected_operation_revision,
                expected_snapshot_revision);
        }
        throw new InvalidOperationException("The room-settings scope changed while it was being captured.");
    }

    private void RequireScope(RoomSettingsOperationScope scope)
    {
        ThrowIfDisposed();
        lifetime_token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(connection.Session, scope.Session))
            throw new InvalidOperationException("The hotel session changed before room settings were dispatched.");
        RoomSettingsManagerState current = settings.State;
        if (!ReferenceEquals(current.Session, scope.Session) ||
            current.SessionGeneration != scope.SessionGeneration)
        {
            throw new InvalidOperationException("The room-settings session generation changed before dispatch.");
        }
        RoomSettingsRoomScope room_scope = CaptureRoom();
        if (scope.RoomGeneration is long room_generation &&
            (!room_scope.Active ||
             room_scope.RoomId != scope.RoomId ||
             room_scope.Generation != room_generation))
        {
            throw new InvalidOperationException("The captured room generation is no longer active.");
        }
        if (scope.RoomGeneration is null &&
            room_scope.Active &&
            room_scope.RoomId == scope.RoomId)
        {
            throw new InvalidOperationException("The off-room settings target became current before dispatch and must be captured again.");
        }
        current.Rooms.TryGetValue(scope.RoomId, out RoomSettingsCacheEntry? entry);
        if (scope.ExpectedOperationRevision is long operation_revision &&
            entry?.OperationRevision != operation_revision)
        {
            throw new InvalidOperationException("The expected room-settings operation revision changed before dispatch.");
        }
        if (scope.ExpectedSnapshotRevision is long snapshot_revision &&
            entry?.SnapshotRevision != snapshot_revision)
        {
            throw new InvalidOperationException("The expected room-settings snapshot revision changed before dispatch.");
        }
    }

    private void RequireReturnedScope(RoomSettingsOperationScope scope)
    {
        if (!ReferenceEquals(connection.Session, scope.Session))
            throw new InvalidOperationException("The hotel session changed before the room-settings result could be returned.");
        RoomSettingsManagerState current = settings.State;
        if (!ReferenceEquals(current.Session, scope.Session) ||
            current.SessionGeneration != scope.SessionGeneration)
        {
            throw new InvalidOperationException("The room-settings session changed before the result could be returned.");
        }
        if (scope.RoomGeneration is long room_generation)
        {
            RoomSettingsRoomScope room_scope = CaptureRoom();
            if (!room_scope.Active ||
                room_scope.RoomId != scope.RoomId ||
                room_scope.Generation != room_generation)
            {
                throw new InvalidOperationException("The active room changed before the room-settings result could be returned.");
            }
        }
    }

    private void RequireReturnedEntry(
        RoomSettingsOperationScope scope,
        RoomSettingsCacheEntry accepted)
    {
        RequireReturnedScope(scope);
        RoomSettingsManagerState current = settings.State;
        if (!current.Rooms.TryGetValue(scope.RoomId, out RoomSettingsCacheEntry? entry) ||
            entry.OperationRevision != accepted.OperationRevision ||
            entry.SnapshotRevision != accepted.SnapshotRevision ||
            !entry.Loaded)
        {
            throw new InvalidOperationException("The room-settings snapshot changed before it could be returned.");
        }
    }

    private static void RequireSameScope(
        RoomSettingsOperationScope initial,
        RoomSettingsOperationScope current)
    {
        if (!ReferenceEquals(initial.Session, current.Session) ||
            initial.SessionGeneration != current.SessionGeneration ||
            initial.RoomId != current.RoomId ||
            initial.RoomGeneration != current.RoomGeneration ||
            initial.ExpectedOperationRevision != current.ExpectedOperationRevision ||
            initial.ExpectedSnapshotRevision != current.ExpectedSnapshotRevision)
        {
            throw new InvalidOperationException("The room-settings scope changed while waiting for its operation lane.");
        }
    }

    private RoomSettingsRoomScope CaptureRoom() => room.Capture(current => new RoomSettingsRoomScope(
        current.State is RoomSessionState.Entering or RoomSessionState.Ready,
        current.Generation,
        (Id)current.RoomId));

    private static RoomSettingsStateView StateView(
        RoomSettingsManagerState state,
        Session? session,
        RoomSettingsRoomScope room_scope,
        Id room_id,
        RoomSettingsCacheEntry? entry)
    {
        bool connected = state.Session is not null && ReferenceEquals(state.Session, session);
        bool loaded = connected && entry?.Loaded == true;
        long? room_generation = entry?.RoomGeneration is long generation &&
            room_scope.Active &&
            room_scope.RoomId == room_id &&
            room_scope.Generation == generation
                ? generation
                : null;
        return new RoomSettingsStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            room_id,
            room_generation,
            connected ? entry?.OperationRevision ?? 0 : 0,
            connected ? entry?.SnapshotRevision ?? 0 : 0,
            loaded,
            loaded && entry?.Settings is { } settings
                ? Values(settings)
                : null,
            loaded && entry?.Settings is { } metadata
                ? Metadata(metadata)
                : null);
    }

    private static RoomSettingsValues Values(RoomSettings settings) => new(
        settings.RoomId,
        settings.Name,
        settings.Description,
        settings.DoorMode,
        settings.CategoryId,
        settings.MaximumVisitors,
        Array.AsReadOnly(settings.Tags.ToArray()),
        settings.TradeMode,
        settings.AllowPets,
        settings.AllowFoodConsume,
        settings.AllowWalkThrough,
        settings.HideWalls,
        settings.WallThickness,
        settings.FloorThickness,
        settings.ChatFloodSensitivity,
        settings.LeaveOnDoorTile,
        settings.IdleSleepEnabled,
        settings.IdleSleepTimeoutSeconds,
        settings.IdleAutokickEnabled,
        settings.IdleAutokickTimeoutSeconds,
        settings.MuteAllPets,
        settings.WhoCanMute,
        settings.WhoCanKick,
        settings.WhoCanBan,
        Array.AsReadOnly(settings.NftGroupIds.ToArray()));

    private static RoomSettingsMetadata Metadata(RoomSettings settings) => new(
        settings.MaximumVisitorsLimit,
        settings.MaximumVisitorsLowerLimit,
        settings.HiddenByBc,
        settings.IsGroupRoom,
        settings.GroupRightsPolicy,
        settings.RequiresBuildersClub,
        settings.IsHabboXDemoRoom);

    private static RoomSettingsValues Freeze(RoomSettingsValues values) => values with
    {
        Tags = Array.AsReadOnly(values.Tags.ToArray()),
        NftGroupIds = Array.AsReadOnly(values.NftGroupIds.ToArray())
    };

    private static SaveRoomSettingsRequest SaveMessage(
        RoomSettingsValues settings,
        string password) => new()
    {
        RoomId = settings.RoomId,
        Name = settings.Name,
        Description = settings.Description,
        DoorMode = settings.DoorMode,
        Password = password,
        MaximumVisitors = settings.MaximumVisitors,
        CategoryId = settings.CategoryId,
        Tags = settings.Tags,
        TradeMode = settings.TradeMode,
        AllowPets = settings.AllowPets,
        AllowFoodConsume = settings.AllowFoodConsume,
        AllowWalkThrough = settings.AllowWalkThrough,
        HideWalls = settings.HideWalls,
        WallThickness = settings.WallThickness,
        FloorThickness = settings.FloorThickness,
        WhoCanMute = settings.WhoCanMute,
        WhoCanKick = settings.WhoCanKick,
        WhoCanBan = settings.WhoCanBan,
        ChatFloodSensitivity = settings.ChatFloodSensitivity,
        LeaveOnDoorTile = settings.LeaveOnDoorTile,
        IdleSleepEnabled = settings.IdleSleepEnabled,
        IdleSleepTimeoutSeconds = settings.IdleSleepTimeoutSeconds,
        IdleAutokickEnabled = settings.IdleAutokickEnabled,
        IdleAutokickTimeoutSeconds = settings.IdleAutokickTimeoutSeconds,
        MuteAllPets = settings.MuteAllPets,
        NftGroupIds = settings.NftGroupIds
    };

    private static bool TerminalFor(
        RoomSettingsOperationKind operation,
        RoomSettingsStateChangeKind kind) => operation switch
    {
        RoomSettingsOperationKind.Get => kind is
            RoomSettingsStateChangeKind.Refreshed or
            RoomSettingsStateChangeKind.RequestFailed or
            RoomSettingsStateChangeKind.InvalidSnapshot,
        RoomSettingsOperationKind.Save => kind is
            RoomSettingsStateChangeKind.SaveSucceeded or
            RoomSettingsStateChangeKind.SaveFailed,
        _ => false
    };

    private static RoomSettingsChangeKind ChangeKind(RoomSettingsStateChangeKind kind) => kind switch
    {
        RoomSettingsStateChangeKind.Refreshed => RoomSettingsChangeKind.Refreshed,
        RoomSettingsStateChangeKind.RequestFailed or
        RoomSettingsStateChangeKind.InvalidSnapshot => RoomSettingsChangeKind.GetRejected,
        RoomSettingsStateChangeKind.Invalidated => RoomSettingsChangeKind.Invalidated,
        RoomSettingsStateChangeKind.SaveSucceeded => RoomSettingsChangeKind.Saved,
        RoomSettingsStateChangeKind.SaveFailed => RoomSettingsChangeKind.SaveRejected,
        RoomSettingsStateChangeKind.RoomChanged => RoomSettingsChangeKind.RoomChanged,
        RoomSettingsStateChangeKind.Reset => RoomSettingsChangeKind.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private TimeSpan RequireRemaining(
        long started,
        int timeout_milliseconds,
        RoomSettingsOperationKind kind)
    {
        TimeSpan remaining = TimeSpan.FromMilliseconds(timeout_milliseconds) -
            time_provider.GetElapsedTime(started);
        if (remaining <= TimeSpan.Zero)
            throw Timeout(kind, timeout_milliseconds);
        return remaining;
    }

    private CancellationTokenSource LinkCancellation(CancellationToken cancellation_token) =>
        CancellationTokenSource.CreateLinkedTokenSource(cancellation_token, lifetime_token);

    private static RequestTimeoutException Timeout(
        RoomSettingsOperationKind kind,
        int timeout_milliseconds) => kind switch
    {
        RoomSettingsOperationKind.Get => new RequestTimeoutException(
            MessageKeys.Room.Settings.Request.Value,
            $"{MessageKeys.Room.Settings.Snapshot.Value} or {MessageKeys.Room.Settings.RequestFailed.Value}",
            timeout_milliseconds),
        RoomSettingsOperationKind.Save => new RequestTimeoutException(
            MessageKeys.Room.Settings.Save.Value,
            $"{MessageKeys.Room.Settings.SaveSucceeded.Value} or {MessageKeys.Room.Settings.SaveFailed.Value}",
            timeout_milliseconds),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static RequestDisconnectedException Disconnected(RoomSettingsOperationKind kind) => kind switch
    {
        RoomSettingsOperationKind.Get => new RequestDisconnectedException(
            MessageKeys.Room.Settings.Request.Value,
            $"{MessageKeys.Room.Settings.Snapshot.Value} or {MessageKeys.Room.Settings.RequestFailed.Value}"),
        RoomSettingsOperationKind.Save => new RequestDisconnectedException(
            MessageKeys.Room.Settings.Save.Value,
            $"{MessageKeys.Room.Settings.SaveSucceeded.Value} or {MessageKeys.Room.Settings.SaveFailed.Value}"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void ValidateValues(RoomSettingsValues values, string password)
    {
        ArgumentNullException.ThrowIfNull(values.Name);
        ArgumentNullException.ThrowIfNull(values.Description);
        ArgumentNullException.ThrowIfNull(values.Tags);
        ArgumentNullException.ThrowIfNull(values.NftGroupIds);
        ArgumentNullException.ThrowIfNull(password);
        ValidateId(values.RoomId, nameof(values.RoomId));
        ValidateText(values.Name, nameof(values.Name));
        ValidateText(values.Description, nameof(values.Description));
        ValidateText(password, nameof(password));
        if (values.Tags.Count > RoomSettingsManager.MaximumCollectionEntries)
            throw new ArgumentOutOfRangeException(nameof(values.Tags));
        foreach (string tag in values.Tags)
        {
            ArgumentNullException.ThrowIfNull(tag);
            ValidateText(tag, nameof(values.Tags));
        }
        if (values.NftGroupIds.Count > RoomSettingsManager.MaximumCollectionEntries)
            throw new ArgumentOutOfRangeException(nameof(values.NftGroupIds));
        foreach (Id id in values.NftGroupIds)
            ValidateId(id, nameof(values.NftGroupIds));
        ArgumentOutOfRangeException.ThrowIfNegative(values.CategoryId);
        ArgumentOutOfRangeException.ThrowIfNegative(values.MaximumVisitors);
        ArgumentOutOfRangeException.ThrowIfNegative(values.IdleSleepTimeoutSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(values.IdleAutokickTimeoutSeconds);
        ValidateEnum(values.DoorMode, nameof(values.DoorMode));
        ValidateEnum(values.TradeMode, nameof(values.TradeMode));
        ValidateEnum(values.WallThickness, nameof(values.WallThickness));
        ValidateEnum(values.FloorThickness, nameof(values.FloorThickness));
        ValidateEnum(values.ChatFloodSensitivity, nameof(values.ChatFloodSensitivity));
        ValidateEnum(values.WhoCanMute, nameof(values.WhoCanMute));
        ValidateEnum(values.WhoCanKick, nameof(values.WhoCanKick));
        ValidateEnum(values.WhoCanBan, nameof(values.WhoCanBan));
    }

    private static void ValidateWireValues(ClientType client, RoomSettingsValues values)
    {
        ValidateWireId(client, values.RoomId, nameof(values.RoomId));
        foreach (Id id in values.NftGroupIds)
            ValidateWireId(client, id, nameof(values.NftGroupIds));
    }

    private static void ValidateText(string value, string name)
    {
        if (Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateEnum<T>(T value, string name) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(name);
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

    private static void ValidatePositiveRevision(long? revision, string name)
    {
        if (revision is <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateId(Id id, string name)
    {
        if ((long)id <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateWireId(ClientType client, Id id, string name)
    {
        ValidateId(id, name);
        if (client is ClientType.Flash && (long)id > int.MaxValue)
            throw new ArgumentOutOfRangeException(name, "The identifier does not fit the Flash wire format.");
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private static ObjectDisposedException Disposed() =>
        new(nameof(RoomSettingsApplication));

    private void EnterDispatch()
    {
        lock (lifecycle_sync)
        {
            ThrowIfDisposed();
            active_dispatches = checked(active_dispatches + 1);
        }
    }

    private void ExitDispatch()
    {
        lock (lifecycle_sync)
        {
            active_dispatches--;
            if (active_dispatches < 0)
                throw new InvalidOperationException("The room-settings dispatch count became negative.");
            if (active_dispatches == 0)
                Monitor.PulseAll(lifecycle_sync);
        }
    }

    private readonly record struct RoomSettingsLaneKey(long SessionGeneration, Id RoomId);

    private readonly record struct RoomSettingsOperationScope(
        Session Session,
        long SessionGeneration,
        Id RoomId,
        long? RoomGeneration,
        long? ExpectedOperationRevision,
        long? ExpectedSnapshotRevision);

    private sealed class RoomSettingsLane
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int References { get; set; }
        public RoomSettingsOperation? Active { get; set; }
    }

    private sealed class RoomSettingsOperation(
        RoomSettingsOperationScope scope,
        RoomSettingsOperationKind kind,
        long pin)
    {
        public object Sync { get; } = new();
        public TaskCompletionSource<RoomSettingsStateUpdate> Response { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Exception> ScopeFailure { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> LaneSettled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RoomSettingsOperationScope Scope { get; } = scope;
        public RoomSettingsOperationKind Kind { get; } = kind;
        public long Pin { get; } = pin;
        public long BaselineRevision { get; set; }
        public bool Armed { get; set; }
        public int DispatchCount { get; set; }
        public int TerminalCount { get; set; }
    }

    private sealed class RoomSettingsOperationSettledException : Exception
    {
    }
}
