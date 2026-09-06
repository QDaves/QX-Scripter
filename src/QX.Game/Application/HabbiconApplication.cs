using Qx.Interception;
using Qx.Game.Protocol;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

internal sealed partial class HabbiconApplication : IApplicationFeature, IHabbiconOperations
{
    private readonly IConnection connection;
    private readonly HabbiconManager habbicons;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly GuardedEventSource<HabbiconChanged> changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object lifecycle_sync = new();
    private readonly TaskCompletionSource disposal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<int> invocation_depth = new();
    private int active_invocations;
    private bool dispose_started;
    private bool cleanup_finished;
    private bool disposal_finished;

    public HabbiconApplication(
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
        habbicons = game.Habbicons;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new GuardedEventSource<HabbiconChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<HabbiconStateRequest, HabbiconStateView>(
                HabbiconApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<HabbiconCollectionPageRequest, HabbiconCollectionPage>(
                HabbiconApplicationDescriptors.Collections,
                (request, _) => ValueTask.FromResult(ReadCollections(request))),
            new ApplicationCallBinding<HabbiconEntryPageRequest, HabbiconEntryPage>(
                HabbiconApplicationDescriptors.Entries,
                (request, _) => ValueTask.FromResult(ReadEntries(request))),
            new ApplicationCallBinding<HabbiconShopRefreshRequest, HabbiconShopRefreshResult>(
                HabbiconApplicationDescriptors.ShopRefresh,
                RefreshShop),
            new ApplicationCallBinding<HabbiconInfoRefreshRequest, HabbiconInfoRefreshResult>(
                HabbiconApplicationDescriptors.InfoRefresh,
                RefreshInfo),
            new ApplicationCallBinding<HabbiconBuyActionRequest, HabbiconDispatchResult>(
                HabbiconApplicationDescriptors.Buy,
                (request, token) => Dispatch(
                    request,
                    MessageContracts.Habbicons.Buy,
                    new HabbiconBuyRequest(request.HabbiconId),
                    request.ExpectedSessionGeneration,
                    token)),
            new ApplicationCallBinding<HabbiconCollectionBuyActionRequest, HabbiconDispatchResult>(
                HabbiconApplicationDescriptors.BuyCollection,
                (request, token) => Dispatch(
                    request,
                    MessageContracts.Habbicons.BuyCollection,
                    new HabbiconCollectionBuyRequest(request.CollectionId),
                    request.ExpectedSessionGeneration,
                    token)),
            new ApplicationCallBinding<HabbiconClaimActionRequest, HabbiconDispatchResult>(
                HabbiconApplicationDescriptors.Claim,
                (request, token) => Dispatch(
                    request,
                    MessageContracts.Habbicons.Claim,
                    new HabbiconClaimRequest(request.HabbiconId),
                    request.ExpectedSessionGeneration,
                    token)),
            new ApplicationCallBinding<HabbiconFavoriteActionRequest, HabbiconDispatchResult>(
                HabbiconApplicationDescriptors.Favorite,
                (request, token) => Dispatch(
                    request,
                    MessageContracts.Habbicons.Favorite,
                    new HabbiconFavoriteRequest(request.HabbiconId),
                    request.ExpectedSessionGeneration,
                    token)),
            new ApplicationCallBinding<HabbiconUnfavoriteActionRequest, HabbiconDispatchResult>(
                HabbiconApplicationDescriptors.Unfavorite,
                (request, token) => Dispatch(
                    request,
                    MessageContracts.Habbicons.Unfavorite,
                    new HabbiconUnfavoriteRequest(request.HabbiconId),
                    request.ExpectedSessionGeneration,
                    token)),
            new ApplicationEventBinding<HabbiconChanged>(
                HabbiconApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        habbicons.StateCommitted += ObserveCommit;
        habbicons.StateChanged += PublishChanged;
        try
        {
            habbicons.BindOperations(this);
        }
        catch
        {
            habbicons.StateCommitted -= ObserveCommit;
            habbicons.StateChanged -= PublishChanged;
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
            habbicons.UnbindOperations(this);
            habbicons.StateCommitted -= ObserveCommit;
            habbicons.StateChanged -= PublishChanged;
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
                throw new ObjectDisposedException(nameof(HabbiconApplication));
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
            throw new ObjectDisposedException(nameof(HabbiconApplication));
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

    private bool PublicationCurrent(HabbiconStateUpdate update) =>
        !DisposalStarted() && habbicons.IsCurrentPublication(update);

    private void PublishChanged(HabbiconStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
        {
            if (!PublicationCurrent(update) || update.Kind is HabbiconStateChangeKind.Request)
                return;
            HabbiconSnapshotLease? lease = TryStoreLease(update.State);
            HabbiconChangeKind kind = update.Kind switch
            {
                HabbiconStateChangeKind.ShopSnapshot => HabbiconChangeKind.ShopSnapshot,
                HabbiconStateChangeKind.InventorySnapshot => HabbiconChangeKind.InventorySnapshot,
                HabbiconStateChangeKind.Status => HabbiconChangeKind.Status,
                HabbiconStateChangeKind.Info => HabbiconChangeKind.Info,
                HabbiconStateChangeKind.RoomUsed => HabbiconChangeKind.RoomUsed,
                HabbiconStateChangeKind.Settings => HabbiconChangeKind.Settings,
                HabbiconStateChangeKind.Reset => HabbiconChangeKind.Reset,
                _ => throw new ArgumentOutOfRangeException(nameof(update))
            };
            HabbiconEntryView? icon = update.Info is null
                ? null
                : EntryView(update.Info, 0);
            HabbiconStatusView? status = update.Status is null
                ? null
                : new HabbiconStatusView(
                    update.Status.HabbiconId,
                    (int)update.Status.State,
                    update.Gained.Contains(update.Status.HabbiconId));
            changed.Publish(
                new HabbiconChanged(
                    kind,
                    time_provider.GetUtcNow(),
                    update.State.Session?.Client,
                    update.State.SessionGeneration,
                    update.State.Revision,
                    SourceRevision(update),
                    lease?.Revision,
                    Summary(update.State),
                    icon,
                    status,
                    update.RoomUse is null
                        ? null
                        : new HabbiconRoomUseView(
                            update.RoomUse.RoomIndex,
                            update.RoomUse.HabbiconId)),
                () => PublicationCurrent(update));
        }
    }

    private static long SourceRevision(HabbiconStateUpdate update) => update.Kind switch
    {
        HabbiconStateChangeKind.ShopSnapshot => update.State.ShopRevision,
        HabbiconStateChangeKind.InventorySnapshot => update.State.UserRevision,
        HabbiconStateChangeKind.Status => update.State.StatusRevision,
        HabbiconStateChangeKind.Info => update.State.InfoRevision,
        HabbiconStateChangeKind.RoomUsed => update.State.RoomRevision,
        HabbiconStateChangeKind.Settings => update.State.SettingsRevision,
        HabbiconStateChangeKind.Reset => update.State.Revision,
        _ => 0
    };

    private HabbiconSnapshotLease? TryStoreLease(HabbiconStateData state)
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

    private sealed class Invocation(HabbiconApplication owner) : IDisposable
    {
        private HabbiconApplication? current = owner;

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

        private sealed class Subscription(GuardedEventSource<T> source, Action<T> listener)
            : IDisposable
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
