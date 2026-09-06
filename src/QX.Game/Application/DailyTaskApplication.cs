using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class DailyTaskApplication : IApplicationFeature, IDailyTaskOperations
{
    private readonly IConnection connection;
    private readonly DailyTaskManager daily_tasks;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly GuardedEventSource<DailyTaskChanged> changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object lifecycle_sync = new();
    private readonly TaskCompletionSource disposal = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<int> invocation_depth = new();
    private int active_invocations;
    private bool dispose_started;
    private bool cleanup_finished;
    private bool disposal_finished;

    public DailyTaskApplication(
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
        daily_tasks = game.DailyTasks;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new GuardedEventSource<DailyTaskChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<DailyTaskStateRequest, DailyTaskStateView>(
                DailyTaskApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<DailyTaskPageRequest, DailyTaskPage>(
                DailyTaskApplicationDescriptors.Entries,
                (request, _) => ValueTask.FromResult(ReadEntries(request))),
            new ApplicationCallBinding<DailyTaskRefreshRequest, DailyTaskRefreshResult>(
                DailyTaskApplicationDescriptors.Refresh,
                Refresh),
            new ApplicationCallBinding<
                DailyTaskClaimActionRequest,
                DailyTaskClaimDispatchReceipt>(
                DailyTaskApplicationDescriptors.Claim,
                Claim),
            new ApplicationEventBinding<DailyTaskChanged>(
                DailyTaskApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        daily_tasks.StateCommitted += ObserveCommit;
        daily_tasks.StateChanged += PublishChanged;
        try
        {
            daily_tasks.BindOperations(this);
        }
        catch
        {
            daily_tasks.StateCommitted -= ObserveCommit;
            daily_tasks.StateChanged -= PublishChanged;
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
            daily_tasks.UnbindOperations(this);
            daily_tasks.StateCommitted -= ObserveCommit;
            daily_tasks.StateChanged -= PublishChanged;
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
            catch (ObjectDisposedException) when (cancellation_token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation_token);
            }
            catch (Exception) when (cancellation_token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation_token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(DailyTaskApplication));
            }
        }
    }

    private TResult InvokeLegacy<TResult>(Func<CancellationToken, TResult> invocation)
    {
        using Invocation active = EnterInvocation();
        try
        {
            return invocation(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(DailyTaskApplication));
        }
    }

    private void InvokeLegacy(Action<CancellationToken> invocation) =>
        InvokeLegacy(token =>
        {
            invocation(token);
            return true;
        });

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
            if (dispose_started &&
                cleanup_finished &&
                active_invocations == 0 &&
                !disposal_finished)
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

    private bool PublicationCurrent(DailyTaskStateUpdate update) =>
        !DisposalStarted() && daily_tasks.IsCurrentPublication(update);

    private void PublishChanged(DailyTaskStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
            PublishChangedCore(update);
    }

    private void PublishChangedCore(DailyTaskStateUpdate update)
    {
        if (!PublicationCurrent(update) || update.Kind is DailyTaskStateChangeKind.Request)
            return;
        DailyTaskSnapshotLease? lease = TryStoreLease(update.State);
        DailyTaskUpdateCommit? task_update = update.Value as DailyTaskUpdateCommit;
        DailyTaskChangeKind kind = update.Kind switch
        {
            DailyTaskStateChangeKind.Snapshot => DailyTaskChangeKind.Snapshot,
            DailyTaskStateChangeKind.Added => DailyTaskChangeKind.Added,
            DailyTaskStateChangeKind.Updated when
                task_update is { } completed &&
                completed.PreviousStatus != completed.Task.Status &&
                completed.Task.Status is DailyTaskStatus.Completed =>
                    DailyTaskChangeKind.Completed,
            DailyTaskStateChangeKind.Updated when
                task_update is { } claimed &&
                claimed.PreviousStatus != claimed.Task.Status &&
                claimed.Task.Status is DailyTaskStatus.Claimed =>
                    DailyTaskChangeKind.Claimed,
            DailyTaskStateChangeKind.Updated => DailyTaskChangeKind.Updated,
            DailyTaskStateChangeKind.Reset => DailyTaskChangeKind.Reset,
            _ => throw new ArgumentOutOfRangeException(nameof(update))
        };
        var value = new DailyTaskChanged(
            kind,
            time_provider.GetUtcNow(),
            update.State.Session?.Client,
            update.State.SessionGeneration,
            update.State.Revision,
            update.Kind is DailyTaskStateChangeKind.Reset
                ? update.State.Revision
                : update.State.TasksRevision,
            lease?.Revision,
            update.Kind is DailyTaskStateChangeKind.Reset ? null : Summary(update.State),
            task_update?.Task.TaskId,
            task_update is null ? null : (int)task_update.Task.Status,
            task_update?.Task.Repeats);
        changed.Publish(value, () => PublicationCurrent(update));
    }

    private DailyTaskSnapshotLease? TryStoreLease(DailyTaskState state)
    {
        try
        {
            return StoreLease(state);
        }
        catch (Exception error) when (
            error is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(DisposalStarted(), this);

    private sealed class Invocation(DailyTaskApplication owner) : IDisposable
    {
        private DailyTaskApplication? current = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref current, null)?.LeaveInvocation();
        }
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
            ArgumentNullException.ThrowIfNull(current);
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
                if (disposed)
                    return;
                disposed = true;
                listeners = null;
            }
        }

        private void Unsubscribe(Action<T> listener)
        {
            lock (sync)
                listeners -= listener;
        }

        private sealed class Subscription(
            GuardedEventSource<T> source,
            Action<T> listener) : IDisposable
        {
            private GuardedEventSource<T>? current_source = source;
            private Action<T>? current_listener = listener;

            public void Dispose()
            {
                GuardedEventSource<T>? source_value = Interlocked.Exchange(
                    ref current_source,
                    null);
                Action<T>? listener_value = Interlocked.Exchange(
                    ref current_listener,
                    null);
                if (source_value is not null && listener_value is not null)
                    source_value.Unsubscribe(listener_value);
            }
        }
    }
}
