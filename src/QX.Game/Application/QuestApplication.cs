using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Quests;

namespace Qx.Game.Application;

internal sealed partial class QuestApplication : IApplicationFeature, IQuestOperations
{
    private readonly IConnection connection;
    private readonly QuestManager quests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly GuardedEventSource<QuestChanged> changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object lifecycle_sync = new();
    private readonly TaskCompletionSource disposal = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<int> invocation_depth = new();
    private int active_invocations;
    private bool dispose_started;
    private bool cleanup_finished;
    private bool disposal_finished;

    public QuestApplication(
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
        quests = game.Quests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new GuardedEventSource<QuestChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<QuestStateRequest, QuestStateView>(
                QuestApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<QuestEntryPageRequest, QuestEntryPage>(
                QuestApplicationDescriptors.Entries,
                (request, _) => ValueTask.FromResult(ReadEntries(request))),
            new ApplicationCallBinding<QuestAvailableRefreshRequest, QuestAvailableRefreshResult>(
                QuestApplicationDescriptors.AvailableRefresh,
                RefreshAvailable),
            new ApplicationCallBinding<QuestSeasonalRefreshRequest, QuestSeasonalRefreshResult>(
                QuestApplicationDescriptors.SeasonalRefresh,
                RefreshSeasonal),
            new ApplicationCallBinding<QuestDailyRefreshRequest, QuestDailyRefreshResult>(
                QuestApplicationDescriptors.DailyRefresh,
                RefreshDaily),
            new ApplicationCallBinding<QuestSelectionActionRequest, QuestSelectionDispatchReceipt>(
                QuestApplicationDescriptors.Accept,
                Accept),
            new ApplicationCallBinding<QuestSelectionActionRequest, QuestSelectionDispatchReceipt>(
                QuestApplicationDescriptors.Activate,
                Activate),
            new ApplicationCallBinding<QuestSelectionActionRequest, QuestSelectionDispatchReceipt>(
                QuestApplicationDescriptors.Reject,
                Reject),
            new ApplicationCallBinding<QuestDispatchRequest, QuestDispatchReceipt>(
                QuestApplicationDescriptors.Cancel,
                Cancel),
            new ApplicationCallBinding<QuestDispatchRequest, QuestDispatchReceipt>(
                QuestApplicationDescriptors.TrackerOpen,
                OpenTracker),
            new ApplicationCallBinding<QuestDispatchRequest, QuestDispatchReceipt>(
                QuestApplicationDescriptors.FriendRequestComplete,
                CompleteFriendRequestQuest),
            new ApplicationEventBinding<QuestChanged>(
                QuestApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        quests.StateCommitted += ObserveCommit;
        quests.StateChanged += PublishChanged;
        try
        {
            quests.BindOperations(this);
        }
        catch
        {
            quests.StateCommitted -= ObserveCommit;
            quests.StateChanged -= PublishChanged;
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
            quests.UnbindOperations(this);
            quests.StateCommitted -= ObserveCommit;
            quests.StateChanged -= PublishChanged;
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
                throw new ObjectDisposedException(nameof(QuestApplication));
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
            throw new ObjectDisposedException(nameof(QuestApplication));
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

    private bool PublicationCurrent(QuestStateUpdate update) =>
        !DisposalStarted() && quests.IsCurrentPublication(update);

    private void PublishChanged(QuestStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
            PublishChangedCore(update);
    }

    private void PublishChangedCore(QuestStateUpdate update)
    {
        if (!PublicationCurrent(update) || update.Kind is QuestStateChangeKind.Request)
            return;
        QuestSnapshotLease? lease = TryStoreLease(update.State);
        long? snapshot_revision = lease?.Revision;
        QuestChangeKind kind = update.Kind switch
        {
            QuestStateChangeKind.Available => QuestChangeKind.Available,
            QuestStateChangeKind.Seasonal => QuestChangeKind.Seasonal,
            QuestStateChangeKind.Current => QuestChangeKind.Current,
            QuestStateChangeKind.Completed => QuestChangeKind.Completed,
            QuestStateChangeKind.Cancelled => QuestChangeKind.Cancelled,
            QuestStateChangeKind.Daily => QuestChangeKind.Daily,
            QuestStateChangeKind.Reset => QuestChangeKind.Reset,
            _ => throw new ArgumentOutOfRangeException(nameof(update))
        };
        long source_revision = update.Kind switch
        {
            QuestStateChangeKind.Available => update.State.AvailableRevision,
            QuestStateChangeKind.Seasonal => update.State.SeasonalRevision,
            QuestStateChangeKind.Current => update.State.CurrentRevision,
            QuestStateChangeKind.Completed => update.State.CompletionRevision,
            QuestStateChangeKind.Cancelled => update.State.CancellationRevision,
            QuestStateChangeKind.Daily => update.State.DailyRevision,
            _ => update.State.Revision
        };
        QuestView? quest = update.Kind switch
        {
            QuestStateChangeKind.Current => View((QuestData)update.Value!),
            QuestStateChangeKind.Completed => View(((QuestCompleted)update.Value!).Data),
            QuestStateChangeKind.Cancelled => View(((QuestCancelled)update.Value!).Data),
            _ => null
        };
        QuestCompleted? completed = update.Value as QuestCompleted;
        QuestCancelled? cancelled = update.Value as QuestCancelled;
        var value = new QuestChanged(
            kind,
            time_provider.GetUtcNow(),
            update.State.Session?.Client,
            update.State.SessionGeneration,
            update.State.Revision,
            source_revision,
            snapshot_revision,
            update.Kind is QuestStateChangeKind.Available or
                QuestStateChangeKind.Seasonal or
                QuestStateChangeKind.Reset
                ? Summary(update.State)
                : null,
            quest,
            completed?.ShowDialog,
            cancelled?.IsExpired,
            update.Value is QuestDaily daily ? View(daily) : null);
        changed.Publish(value, () => PublicationCurrent(update));
    }

    private QuestSnapshotLease? TryStoreLease(QuestState state)
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

    private sealed class Invocation(QuestApplication owner) : IDisposable
    {
        private QuestApplication? current = owner;

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
