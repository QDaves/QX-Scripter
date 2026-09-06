using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class WalletApplication : IApplicationFeature, IWalletOperations
{
    private const int maximum_attempts = 2;
    private static readonly TimeSpan retry_delay = TimeSpan.FromMilliseconds(150);

    private readonly IConnection connection;
    private readonly GameState game;
    private readonly EconomyManager economy;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<WalletChanged> changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetime_token;
    private readonly object load_sync = new();
    private WalletLoadOperation? load;
    private int disposed;

    public WalletApplication(
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
        this.game = game;
        economy = game.Economy;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        lifetime_token = lifetime.Token;
        changed = new ApplicationEventSource<WalletChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<WalletStateRequest, WalletStateView>(
                WalletApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<WalletRefreshRequest, WalletStateView>(
                WalletApplicationDescriptors.Refresh,
                Refresh),
            new ApplicationEventBinding<WalletChanged>(
                WalletApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        economy.StateCommitted += OnStateCommitted;
        economy.StateChanged += OnStateChanged;
        try
        {
            game.BindWalletOperations(this);
        }
        catch
        {
            economy.StateCommitted -= OnStateCommitted;
            economy.StateChanged -= OnStateChanged;
            changed.Dispose();
            lifetime.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public WalletStateView ReadState(WalletStateRequest request)
    {
        ThrowIfDisposed();
        ValidateStateRequest(request);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            WalletState current = economy.State;
            Session? active_session = connection.Session;
            WalletStateView view = StateView(current, active_session, request);
            if (ReferenceEquals(economy.State, current) &&
                ReferenceEquals(connection.Session, active_session))
            {
                return view;
            }
        }
        throw new InvalidOperationException("The wallet changed while it was being read.");
    }

    public async ValueTask<WalletStateView> Refresh(
        WalletRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateLimit(request.PointLimit);
        ValidateTimeout(request.TimeoutMilliseconds);
        WalletState loaded = await LoadStateAsync(
            true,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        WalletOperationScope scope = ScopeOf(loaded);
        RequireScope(scope);
        WalletStateView view = StateView(
            loaded,
            scope.Session,
            new WalletStateRequest(PointLimit: request.PointLimit));
        RequireScope(scope);
        return view;
    }

    async Task IWalletOperations.EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        await LoadStateAsync(
            false,
            timeout_milliseconds,
            cancellation_token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (load_sync)
        {
            if (disposed != 0)
                return;
            Volatile.Write(ref disposed, 1);
            WalletLoadOperation? active = load;
            load = null;
            if (active is not null)
                FailAndCancel(active, new ObjectDisposedException(nameof(WalletApplication)));
        }
        game.UnbindWalletOperations(this);
        economy.StateCommitted -= OnStateCommitted;
        economy.StateChanged -= OnStateChanged;
        lifetime.Cancel();
        changed.Dispose();
        lifetime.Dispose();
    }

    private async Task<WalletState> LoadStateAsync(
        bool force_refresh,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ValidateTimeout(timeout_milliseconds);
        cancellation_token.ThrowIfCancellationRequested();
        long waiter_started = time_provider.GetTimestamp();
        WalletOperationScope scope = CaptureScope();
        WalletState current = economy.State;
        if (!force_refresh &&
            ScopeActive(scope, current) &&
            current.CreditsLoaded &&
            current.ActivityPointsLoaded)
        {
            return current;
        }

        WalletLoadOperation operation;
        bool start = false;
        lock (load_sync)
        {
            ThrowIfDisposed();
            current = economy.State;
            if (!force_refresh &&
                ScopeActive(scope, current) &&
                current.CreditsLoaded &&
                current.ActivityPointsLoaded)
            {
                return current;
            }
            if (load is { } existing && !SameScope(existing.Scope, scope))
            {
                load = null;
                FailAndCancel(existing, Disconnected());
            }
            if (load is { } active)
            {
                operation = active;
            }
            else
            {
                operation = new WalletLoadOperation(
                    scope,
                    time_provider.GetTimestamp());
                load = operation;
                start = true;
            }
            ExtendDeadline(operation, timeout_milliseconds);
            operation.Waiters = checked(operation.Waiters + 1);
        }

        try
        {
            if (start)
                _ = RunLoadOperation(operation);
            TimeSpan waiter_timeout = TimeSpan.FromMilliseconds(timeout_milliseconds) -
                time_provider.GetElapsedTime(waiter_started);
            if (waiter_timeout <= TimeSpan.Zero)
                throw Timeout(timeout_milliseconds);
            return await operation.Completion.Task.WaitAsync(
                waiter_timeout,
                time_provider,
                cancellation_token).ConfigureAwait(false);
        }
        catch (RequestTimeoutException)
        {
            throw Timeout(timeout_milliseconds);
        }
        catch (TimeoutException)
        {
            throw Timeout(timeout_milliseconds);
        }
        finally
        {
            ReleaseWaiter(operation);
        }
    }

    private async Task RunLoadOperation(WalletLoadOperation operation)
    {
        WalletState? result = null;
        Exception? failure = null;
        try
        {
            result = await RequestWallet(operation).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            failure = error;
        }

        lock (load_sync)
        {
            if (Volatile.Read(ref disposed) != 0)
                failure = new ObjectDisposedException(nameof(WalletApplication));
            if (failure is null)
                operation.Completion.TrySetResult(result!);
            else
                operation.Completion.TrySetException(failure);
            if (ReferenceEquals(load, operation))
                load = null;
        }
        operation.Cancellation.Dispose();
    }

    private async Task<WalletState> RequestWallet(WalletLoadOperation operation)
    {
        for (int attempt_number = 1; attempt_number <= maximum_attempts; attempt_number++)
        {
            operation.Token.ThrowIfCancellationRequested();
            RequireScope(operation.Scope);
            TimeSpan remaining = RequireRemaining(operation);
            int attempts_left = maximum_attempts - attempt_number + 1;
            TimeSpan attempt_timeout = TimeSpan.FromTicks(
                Math.Max(1, remaining.Ticks / attempts_left));
            long attempt_started = time_provider.GetTimestamp();
            var attempt = new WalletLoadAttempt();
            lock (operation.Sync)
                operation.Attempt = attempt;
            try
            {
                message_dispatcher.Dispatch(
                    MessageContracts.Wallet.CreditsRequest,
                    new WalletBalanceRequest(),
                    operation.Scope.Session,
                    operation.Token,
                    dispatch_guard: () => ArmAttempt(operation, attempt));
                TimeSpan wait_timeout = attempt_timeout -
                    time_provider.GetElapsedTime(attempt_started);
                if (wait_timeout <= TimeSpan.Zero)
                    throw new TimeoutException();
                WalletState result = await attempt.Completion.Task.WaitAsync(
                    wait_timeout,
                    time_provider,
                    operation.Token).ConfigureAwait(false);
                RequireScope(operation.Scope);
                return result;
            }
            catch (TimeoutException) when (attempt_number < maximum_attempts)
            {
            }
            catch (TimeoutException)
            {
                return await WaitForExtendedAttempt(operation, attempt).ConfigureAwait(false);
            }
            finally
            {
                lock (operation.Sync)
                {
                    if (ReferenceEquals(operation.Attempt, attempt))
                        operation.Attempt = null;
                }
            }

            TimeSpan available = RequireRemaining(operation);
            TimeSpan delay = TimeSpan.FromTicks(
                Math.Min(retry_delay.Ticks, Math.Max(1, available.Ticks / 4)));
            await Task.Delay(delay, time_provider, operation.Token).ConfigureAwait(false);
        }
        throw Timeout(OperationTimeout(operation));
    }

    private async Task<WalletState> WaitForExtendedAttempt(
        WalletLoadOperation operation,
        WalletLoadAttempt attempt)
    {
        while (true)
        {
            TimeSpan remaining = RequireRemaining(operation);
            try
            {
                WalletState result = await attempt.Completion.Task.WaitAsync(
                    remaining,
                    time_provider,
                    operation.Token).ConfigureAwait(false);
                RequireScope(operation.Scope);
                return result;
            }
            catch (TimeoutException)
            {
            }
        }
    }

    private void ArmAttempt(WalletLoadOperation operation, WalletLoadAttempt attempt)
    {
        operation.Token.ThrowIfCancellationRequested();
        RequireScope(operation.Scope);
        WalletState baseline = economy.State;
        if (!ScopeActive(operation.Scope, baseline))
            throw Disconnected();
        lock (operation.Sync)
        {
            if (!ReferenceEquals(operation.Attempt, attempt))
                throw new InvalidOperationException("The wallet load attempt is no longer active.");
            attempt.CreditsBaseline = baseline.CreditsSnapshotRevision;
            attempt.ActivityPointsBaseline = baseline.ActivityPointsSnapshotRevision;
            attempt.CreditsReceived = false;
            attempt.ActivityPointsReceived = false;
            attempt.Armed = true;
        }
    }

    private void OnStateCommitted(WalletStateUpdate update)
    {
        WalletLoadOperation? operation;
        lock (load_sync)
            operation = load;
        if (operation is null)
            return;
        lock (operation.Sync)
        {
            WalletLoadAttempt? attempt = operation.Attempt;
            if (attempt is null)
                return;
            if (update.Kind is WalletStateChangeKind.Reset ||
                !ScopeActive(operation.Scope, update.State))
            {
                attempt.Completion.TrySetException(Disconnected());
                return;
            }
            if (!attempt.Armed)
                return;
            if (update.Kind is WalletStateChangeKind.CreditsRefreshed &&
                update.State.CreditsSnapshotRevision > attempt.CreditsBaseline)
            {
                attempt.CreditsReceived = true;
            }
            else if (update.Kind is WalletStateChangeKind.ActivityPointsRefreshed &&
                update.State.ActivityPointsSnapshotRevision > attempt.ActivityPointsBaseline)
            {
                attempt.ActivityPointsReceived = true;
            }
            if (attempt.CreditsReceived &&
                attempt.ActivityPointsReceived &&
                update.State.CreditsLoaded &&
                update.State.ActivityPointsLoaded)
            {
                attempt.Completion.TrySetResult(update.State);
            }
        }
    }

    private void OnStateChanged(WalletStateUpdate update)
    {
        WalletState state = update.State;
        WalletPointUpdate? point = update.Point;
        changed.Publish(new WalletChanged(
            ChangeKind(update.Kind),
            time_provider.GetUtcNow(),
            state.Session?.Client,
            state.Generation,
            state.Revision,
            state.CreditsSnapshotRevision,
            state.ActivityPointsSnapshotRevision,
            state.CreditsLoaded,
            state.CreditsLoaded ? state.Credits : null,
            state.ActivityPointsLoaded,
            state.ActivityPoints.Count,
            point?.Type,
            point?.Amount,
            point?.Change));
    }

    private WalletOperationScope CaptureScope()
    {
        WalletState current = economy.State;
        Session active_session = current.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(connection.Session, active_session))
            throw new InvalidOperationException("The wallet is not bound to the active hotel session.");
        return new WalletOperationScope(active_session, current.Generation);
    }

    private static WalletOperationScope ScopeOf(WalletState state)
    {
        Session session = state.Session
            ?? throw new RequestDisconnectedException(
                MessageKeys.Wallet.CreditsRequest.Value,
                $"{MessageKeys.Wallet.CreditsBalance.Value} and {MessageKeys.Wallet.ActivityPoints.Value}");
        return new WalletOperationScope(session, state.Generation);
    }

    private void RequireScope(WalletOperationScope scope)
    {
        ThrowIfDisposed();
        lifetime_token.ThrowIfCancellationRequested();
        if (!ScopeActive(scope, economy.State))
            throw Disconnected();
    }

    private bool ScopeActive(WalletOperationScope scope, WalletState state) =>
        ReferenceEquals(connection.Session, scope.Session) &&
        ReferenceEquals(state.Session, scope.Session) &&
        state.Generation == scope.Generation;

    private static bool SameScope(WalletOperationScope left, WalletOperationScope right) =>
        ReferenceEquals(left.Session, right.Session) && left.Generation == right.Generation;

    private WalletStateView StateView(
        WalletState state,
        Session? active_session,
        WalletStateRequest request)
    {
        if (request.SnapshotRevision is long requested_revision &&
            requested_revision != state.ActivityPointsSnapshotRevision)
        {
            throw new InvalidOperationException("The activity-point snapshot changed while it was being read.");
        }
        WalletPointBalance[] selected = state.ActivityPoints
            .Where(entry => request.PointType is null || entry.Key == request.PointType.Value)
            .OrderBy(entry => entry.Key)
            .Select(entry => new WalletPointBalance(entry.Key, entry.Value))
            .ToArray();
        if (request.PointOffset > selected.Length)
            throw new InvalidOperationException("The activity-point offset exceeds the selected snapshot.");
        int available = Math.Max(0, selected.Length - request.PointOffset);
        int count = Math.Min(request.PointLimit, available);
        var page = new WalletPointBalance[count];
        if (count != 0)
            Array.Copy(selected, request.PointOffset, page, 0, count);
        int consumed = checked(request.PointOffset + count);
        int? next_offset = consumed < selected.Length ? consumed : null;
        bool connected = state.Session is not null &&
            ReferenceEquals(active_session, state.Session);
        return new WalletStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.Generation,
            state.Revision,
            state.CreditsSnapshotRevision,
            state.CreditsLoaded,
            state.CreditsLoaded ? state.Credits : null,
            state.ActivityPointsLoaded,
            new WalletPointPage(
                state.ActivityPointsSnapshotRevision,
                selected.Length,
                request.PointOffset,
                next_offset,
                Array.AsReadOnly(page)));
    }

    private void ExtendDeadline(WalletLoadOperation operation, int timeout_milliseconds)
    {
        lock (operation.Sync)
        {
            TimeSpan candidate = time_provider.GetElapsedTime(operation.StartedTimestamp) +
                TimeSpan.FromMilliseconds(timeout_milliseconds);
            if (candidate > operation.DeadlineElapsed)
                operation.DeadlineElapsed = candidate;
            operation.MaximumTimeoutMilliseconds = Math.Max(
                operation.MaximumTimeoutMilliseconds,
                timeout_milliseconds);
        }
    }

    private TimeSpan Remaining(WalletLoadOperation operation)
    {
        lock (operation.Sync)
        {
            TimeSpan remaining = operation.DeadlineElapsed -
                time_provider.GetElapsedTime(operation.StartedTimestamp);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    private TimeSpan RequireRemaining(WalletLoadOperation operation)
    {
        while (true)
        {
            operation.Token.ThrowIfCancellationRequested();
            TimeSpan remaining = Remaining(operation);
            if (remaining > TimeSpan.Zero)
                return remaining;
            if (Expire(operation))
                throw Timeout(OperationTimeout(operation));
        }
    }

    private bool Expire(WalletLoadOperation operation)
    {
        lock (load_sync)
        {
            if (!ReferenceEquals(load, operation))
                return true;
            if (Remaining(operation) > TimeSpan.Zero)
                return false;
            load = null;
            return true;
        }
    }

    private void ReleaseWaiter(WalletLoadOperation operation)
    {
        lock (load_sync)
        {
            operation.Waiters--;
            if (operation.Waiters < 0)
                throw new InvalidOperationException("The wallet load waiter count became negative.");
            if (operation.Waiters == 0 &&
                !operation.Completion.Task.IsCompleted &&
                ReferenceEquals(load, operation))
            {
                load = null;
                Cancel(operation);
            }
        }
    }

    private static int OperationTimeout(WalletLoadOperation operation)
    {
        lock (operation.Sync)
            return operation.MaximumTimeoutMilliseconds;
    }

    private static WalletChangeKind ChangeKind(WalletStateChangeKind kind) => kind switch
    {
        WalletStateChangeKind.CreditsRefreshed => WalletChangeKind.CreditsRefreshed,
        WalletStateChangeKind.ActivityPointsRefreshed => WalletChangeKind.ActivityPointsRefreshed,
        WalletStateChangeKind.ActivityPointUpdated => WalletChangeKind.ActivityPointUpdated,
        WalletStateChangeKind.Reset => WalletChangeKind.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void ValidateStateRequest(WalletStateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.PointOffset);
        ValidateLimit(request.PointLimit);
        if (request.SnapshotRevision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.SnapshotRevision));
        if (request.PointOffset != 0 && request.SnapshotRevision is null)
        {
            throw new ArgumentException(
                "Continuation pages require a snapshot revision.",
                nameof(request.SnapshotRevision));
        }
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

    private static RequestTimeoutException Timeout(int timeout_milliseconds) => new(
        MessageKeys.Wallet.CreditsRequest.Value,
        $"{MessageKeys.Wallet.CreditsBalance.Value} and {MessageKeys.Wallet.ActivityPoints.Value}",
        timeout_milliseconds);

    private static RequestDisconnectedException Disconnected() => new(
        MessageKeys.Wallet.CreditsRequest.Value,
        $"{MessageKeys.Wallet.CreditsBalance.Value} and {MessageKeys.Wallet.ActivityPoints.Value}");

    private static void FailAndCancel(WalletLoadOperation operation, Exception error)
    {
        lock (operation.Sync)
            operation.Attempt?.Completion.TrySetException(error);
        operation.Completion.TrySetException(error);
        operation.Cancellation.Cancel();
    }

    private static void Cancel(WalletLoadOperation operation)
    {
        operation.Cancellation.Cancel();
        lock (operation.Sync)
            operation.Attempt?.Completion.TrySetCanceled(operation.Token);
        operation.Completion.TrySetCanceled(operation.Token);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private readonly record struct WalletOperationScope(Session Session, long Generation);

    private sealed class WalletLoadOperation
    {
        public WalletLoadOperation(
            WalletOperationScope scope,
            long started_timestamp)
        {
            Scope = scope;
            StartedTimestamp = started_timestamp;
            Cancellation = new CancellationTokenSource();
            Token = Cancellation.Token;
        }

        public object Sync { get; } = new();
        public CancellationTokenSource Cancellation { get; }
        public TaskCompletionSource<WalletState> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public WalletOperationScope Scope { get; }
        public CancellationToken Token { get; }
        public long StartedTimestamp { get; }
        public TimeSpan DeadlineElapsed { get; set; }
        public int MaximumTimeoutMilliseconds { get; set; }
        public int Waiters { get; set; }
        public WalletLoadAttempt? Attempt { get; set; }
    }

    private sealed class WalletLoadAttempt
    {
        public TaskCompletionSource<WalletState> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public long CreditsBaseline { get; set; }
        public long ActivityPointsBaseline { get; set; }
        public bool CreditsReceived { get; set; }
        public bool ActivityPointsReceived { get; set; }
        public bool Armed { get; set; }
    }
}
