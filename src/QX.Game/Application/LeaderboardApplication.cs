using Qx.Interception;

namespace Qx.Game.Application;

internal sealed partial class LeaderboardApplication : IApplicationFeature, ILeaderboardOperations
{
    private readonly IConnection connection;
    private readonly LeaderboardManager leaderboards;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly GuardedEventSource<LeaderboardChanged> changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object lifecycle_sync = new();
    private readonly TaskCompletionSource disposal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<int> invocation_depth = new();
    private int active_invocations;
    private bool dispose_started;
    private bool cleanup_finished;
    private bool disposal_finished;

    public LeaderboardApplication(
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
        leaderboards = game.Leaderboards;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new GuardedEventSource<LeaderboardChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<LeaderboardStateRequest, LeaderboardStateView>(
                LeaderboardApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<LeaderboardEntryPageRequest, LeaderboardEntryPage>(
                LeaderboardApplicationDescriptors.Entries,
                (request, _) => ValueTask.FromResult(ReadEntries(request))),
            new ApplicationCallBinding<LeaderboardRefreshRequest, LeaderboardRefreshResult>(
                LeaderboardApplicationDescriptors.Refresh,
                Refresh),
            new ApplicationCallBinding<LeaderboardWeekOffsetRequest, LeaderboardWeekOffsetResult>(
                LeaderboardApplicationDescriptors.WeekOffsetSet,
                SetWeekOffset),
            new ApplicationEventBinding<LeaderboardChanged>(
                LeaderboardApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        leaderboards.StateCommitted += ObserveCommit;
        leaderboards.StateChanged += PublishChanged;
        try
        {
            leaderboards.BindOperations(this);
        }
        catch
        {
            leaderboards.StateCommitted -= ObserveCommit;
            leaderboards.StateChanged -= PublishChanged;
            changed.Dispose();
            lifetime.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public void Dispose()
    {
        bool first;
        bool wait = invocation_depth.Value == 0;
        lock (lifecycle_sync)
        {
            first = !dispose_started;
            dispose_started = true;
        }
        if (first)
        {
            leaderboards.UnbindOperations(this);
            leaderboards.StateCommitted -= ObserveCommit;
            leaderboards.StateChanged -= PublishChanged;
            lifetime.Cancel();
            ClearOperationState();
            ClearLeases();
            changed.Dispose();
            lock (lifecycle_sync)
                cleanup_finished = true;
            CompleteDisposalIfReady();
        }
        if (wait)
            disposal.Task.GetAwaiter().GetResult();
    }

    private async ValueTask<TResult> InvokeAsync<TResult>(
        CancellationToken cancellation_token,
        Func<CancellationToken, ValueTask<TResult>> invocation)
    {
        cancellation_token.ThrowIfCancellationRequested();
        Invocation active;
        try
        {
            active = EnterInvocation();
        }
        catch (ObjectDisposedException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        using (active)
        using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation_token,
            lifetime.Token))
        {
            try
            {
                TResult result = await invocation(linked.Token).ConfigureAwait(false);
                cancellation_token.ThrowIfCancellationRequested();
                ThrowIfDisposed();
                return result;
            }
            catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation_token);
            }
            catch (Exception) when (cancellation_token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation_token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(LeaderboardApplication));
            }
        }
    }

    private void InvokeLegacy(Action<CancellationToken> invocation)
    {
        using Invocation active = EnterInvocation();
        try
        {
            invocation(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(LeaderboardApplication));
        }
    }

    private Invocation EnterInvocation()
    {
        lock (lifecycle_sync)
        {
            ObjectDisposedException.ThrowIf(dispose_started, this);
            active_invocations++;
        }
        invocation_depth.Value++;
        return new Invocation(this);
    }

    private bool TryEnterInvocation(out Invocation? invocation)
    {
        try
        {
            invocation = EnterInvocation();
            return true;
        }
        catch (ObjectDisposedException)
        {
            invocation = null;
            return false;
        }
    }

    private void LeaveInvocation()
    {
        invocation_depth.Value = Math.Max(0, invocation_depth.Value - 1);
        lock (lifecycle_sync)
            active_invocations--;
        CompleteDisposalIfReady();
    }

    private void CompleteDisposalIfReady()
    {
        bool complete = false;
        lock (lifecycle_sync)
        {
            if (dispose_started && cleanup_finished && active_invocations == 0 && !disposal_finished)
            {
                disposal_finished = true;
                complete = true;
            }
        }
        if (complete)
        {
            lifetime.Dispose();
            disposal.TrySetResult();
        }
    }

    private bool DisposalStarted() => Volatile.Read(ref dispose_started);

    private bool PublicationCurrent(LeaderboardStateUpdate update) =>
        !DisposalStarted() && leaderboards.IsCurrentPublication(update);

    private void PublishChanged(LeaderboardStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
        {
            if (!PublicationCurrent(update) || update.Kind is LeaderboardStateChangeKind.Request)
                return;
            LeaderboardSnapshotLease? lease = TryStoreLease(update.State);
            LeaderboardChangeKind kind = update.Kind switch
            {
                LeaderboardStateChangeKind.Snapshot => LeaderboardChangeKind.Snapshot,
                LeaderboardStateChangeKind.Settings => LeaderboardChangeKind.Settings,
                LeaderboardStateChangeKind.Reset => LeaderboardChangeKind.Reset,
                _ => throw new ArgumentOutOfRangeException(nameof(update))
            };
            LeaderboardSummary? summary = update.Route is { } route
                ? Summary(update.State.Boards.GetValueOrDefault(route))
                : null;
            changed.Publish(
                new LeaderboardChanged(
                    kind,
                    time_provider.GetUtcNow(),
                    update.State.Session?.Client,
                    update.State.SessionGeneration,
                    update.State.Revision,
                    update.Kind is LeaderboardStateChangeKind.Settings
                        ? update.State.SettingsRevision
                        : update.State.BoardsRevision,
                    lease?.Revision,
                    update.Route?.Scope,
                    update.Route?.Weekly,
                    summary,
                    PeriodView(update.State.Period),
                    update.State.WeekOffset),
                () => PublicationCurrent(update));
        }
    }

    private LeaderboardSnapshotLease? TryStoreLease(LeaderboardState state)
    {
        try
        {
            return StoreLease(state);
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(DisposalStarted(), this);

    private sealed class Invocation(LeaderboardApplication owner) : IDisposable
    {
        private LeaderboardApplication? current = owner;

        public void Dispose() => Interlocked.Exchange(ref current, null)?.LeaveInvocation();
    }

    private sealed class GuardedEventSource<T>(Action<Exception>? observer_error) : IDisposable
    {
        private readonly object sync = new();
        private Action<T>? listeners;
        private bool disposed;

        public IDisposable Subscribe(Action<T> listener)
        {
            ArgumentNullException.ThrowIfNull(listener);
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                listeners += listener;
            }
            return new Subscription(this, listener);
        }

        public void Publish(T value, Func<bool> current)
        {
            Action<T>? snapshot;
            lock (sync)
            {
                if (disposed)
                    return;
                snapshot = listeners;
            }
            if (snapshot is null)
                return;
            foreach (Action<T> listener in snapshot.GetInvocationList().Cast<Action<T>>())
            {
                lock (sync)
                {
                    if (disposed)
                        return;
                }
                if (!current())
                    return;
                try
                {
                    listener(value);
                }
                catch (Exception error)
                {
                    observer_error?.Invoke(error);
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                listeners = null;
            }
        }

        private void Unsubscribe(Action<T> listener)
        {
            lock (sync)
                listeners -= listener;
        }

        private sealed class Subscription(GuardedEventSource<T> source, Action<T> listener) : IDisposable
        {
            private GuardedEventSource<T>? current_source = source;
            private Action<T>? current_listener = listener;

            public void Dispose()
            {
                GuardedEventSource<T>? source_value = Interlocked.Exchange(ref current_source, null);
                Action<T>? listener_value = Interlocked.Exchange(ref current_listener, null);
                if (source_value is not null && listener_value is not null)
                    source_value.Unsubscribe(listener_value);
            }
        }
    }
}
