using Qx.Interception;

namespace Qx.Game.Application;

internal sealed partial class GiftApplication : IApplicationFeature, IGiftOperations
{
    private readonly IConnection connection;
    private readonly GiftManager gifts;
    private readonly RoomManager room;
    private readonly CatalogManager catalog_manager;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly GuardedEventSource<GiftChanged> changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim wrapping_refresh_lane = new(1, 1);
    private readonly SemaphoreSlim club_info_refresh_lane = new(1, 1);
    private readonly object lifecycle_sync = new();
    private readonly TaskCompletionSource disposal = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<int> invocation_depth = new();
    private int active_invocations;
    private bool dispose_started;
    private bool cleanup_finished;
    private bool disposal_finished;

    public GiftApplication(
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
        gifts = game.Gifts;
        room = game.Room;
        catalog_manager = game.Catalog;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new GuardedEventSource<GiftChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<GiftStateRequest, GiftStateView>(
                GiftApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<GiftWrappingPageRequest, GiftWrappingPage>(
                GiftApplicationDescriptors.WrappingList,
                (request, _) => ValueTask.FromResult(ReadWrapping(request))),
            new ApplicationCallBinding<GiftClubInfoPageRequest, GiftClubInfoPage>(
                GiftApplicationDescriptors.ClubInfoList,
                (request, _) => ValueTask.FromResult(ReadClubInfo(request))),
            new ApplicationCallBinding<GiftClubSelectedPageRequest, GiftClubSelectedPage>(
                GiftApplicationDescriptors.ClubSelectedList,
                (request, _) => ValueTask.FromResult(ReadClubSelected(request))),
            new ApplicationCallBinding<GiftNewUserOfferPageRequest, GiftNewUserOfferPage>(
                GiftApplicationDescriptors.NewUserOfferList,
                (request, _) => ValueTask.FromResult(ReadNewUserOffer(request))),
            new ApplicationCallBinding<GiftRefreshRequest, GiftRefreshResult>(
                GiftApplicationDescriptors.Refresh,
                Refresh),
            new ApplicationCallBinding<GiftPresentOpenRequest, GiftPresentOpenDispatchReceipt>(
                GiftApplicationDescriptors.PresentOpen,
                OpenPresent),
            new ApplicationCallBinding<GiftPurchaseRequest, GiftPurchaseDispatchReceipt>(
                GiftApplicationDescriptors.Purchase,
                PurchaseGift),
            new ApplicationCallBinding<GiftClubSelectRequest, GiftClubSelectDispatchReceipt>(
                GiftApplicationDescriptors.ClubSelect,
                SelectClubGift),
            new ApplicationCallBinding<
                GiftOfferGiftabilityRefreshRequest,
                GiftOfferGiftabilityRefreshResult>(
                GiftApplicationDescriptors.OfferGiftabilityRefresh,
                RefreshOfferGiftability),
            new ApplicationCallBinding<
                GiftNewUserSelectRequest,
                GiftNewUserSelectDispatchReceipt>(
                GiftApplicationDescriptors.NewUserSelect,
                SelectNewUserGifts),
            new ApplicationCallBinding<
                GiftNewUserAdvanceRequest,
                GiftNewUserAdvanceDispatchReceipt>(
                GiftApplicationDescriptors.NewUserAdvance,
                AdvanceNewUserFlow),
            new ApplicationEventBinding<GiftChanged>(
                GiftApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        gifts.StateCommitted += ObserveCommit;
        gifts.StateChanged += PublishChanged;
        try
        {
            gifts.BindOperations(this);
        }
        catch
        {
            gifts.StateCommitted -= ObserveCommit;
            gifts.StateChanged -= PublishChanged;
            changed.Dispose();
            wrapping_refresh_lane.Dispose();
            club_info_refresh_lane.Dispose();
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
            gifts.UnbindOperations(this);
            gifts.StateCommitted -= ObserveCommit;
            gifts.StateChanged -= PublishChanged;
            lifetime.Cancel();
            ClearRefreshState();
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
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(GiftApplication));
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
            throw new ObjectDisposedException(nameof(GiftApplication));
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
            wrapping_refresh_lane.Dispose();
            club_info_refresh_lane.Dispose();
            lifetime.Dispose();
            disposal.TrySetResult();
        }
    }

    private bool DisposalStarted() => Volatile.Read(ref dispose_started);

    private bool PublicationCurrent(GiftStateUpdate update) =>
        !DisposalStarted() && gifts.IsCurrentPublication(update);

    private GiftOperationScope CaptureScope(
        long? expected_session_generation,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        ValidateExpectedRevision(
            expected_session_generation,
            nameof(expected_session_generation));
        GiftState state = gifts.State;
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(state.Session, session))
        {
            throw new InvalidOperationException(
                "The gift state is not bound to the active hotel session.");
        }
        if (expected_session_generation is long expected &&
            expected != state.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The active gift session generation does not match the expected generation.");
        }
        return new GiftOperationScope(session, state.SessionGeneration);
    }

    private void RequireScope(GiftOperationScope scope)
    {
        ThrowIfDisposed();
        GiftState state = gifts.State;
        if (!ReferenceEquals(connection.Session, scope.Session) ||
            !ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The hotel session changed during the gift operation.");
        }
    }

    private GiftRevisionScope CaptureRevisionScope(
        long? expected_session_generation,
        long? expected_source_revision,
        Func<GiftState, long> revision,
        Func<GiftState, bool>? loaded,
        string source_name,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(source_name);
        ValidateExpectedRevision(
            expected_source_revision,
            nameof(expected_source_revision));
        GiftOperationScope scope = CaptureScope(
            expected_session_generation,
            cancellation_token);
        GiftState state = gifts.State;
        if (!ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The hotel session changed while the gift revision was captured.");
        }
        long source_revision = revision(state);
        if (expected_source_revision is long expected &&
            expected != source_revision)
        {
            throw new InvalidOperationException(
                $"The {source_name} revision does not match the expected revision.");
        }
        if (expected_source_revision is not null && loaded is not null && !loaded(state))
        {
            throw new InvalidOperationException(
                $"The {source_name} snapshot is not loaded.");
        }
        return new GiftRevisionScope(
            scope.Session,
            scope.SessionGeneration,
            source_revision,
            expected_source_revision is not null);
    }

    private void RequireRevisionScope(
        GiftRevisionScope scope,
        Func<GiftState, long> revision,
        string source_name)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(source_name);
        RequireScope(new GiftOperationScope(scope.Session, scope.SessionGeneration));
        if (scope.PinSourceRevision && revision(gifts.State) != scope.SourceRevision)
        {
            throw new InvalidOperationException(
                $"The {source_name} revision changed before gift dispatch.");
        }
    }

    private GiftRoomScope CaptureRoomScope(
        long? expected_session_generation,
        long? expected_room_generation,
        CancellationToken cancellation_token)
    {
        ValidateExpectedRevision(
            expected_room_generation,
            nameof(expected_room_generation));
        GiftOperationScope scope = CaptureScope(
            expected_session_generation,
            cancellation_token);
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            RequireScope(scope);
            if (!current_room.IsReady || current_room.RoomId == 0)
                throw new InvalidOperationException("A ready hotel room is required.");
            if (expected_room_generation is long expected &&
                expected != current_room.Generation)
            {
                throw new InvalidOperationException(
                    "The ready room generation does not match the expected generation.");
            }
            return new GiftRoomScope(
                scope.Session,
                scope.SessionGeneration,
                (Id)current_room.RoomId,
                current_room.Generation,
                current_room.Revision);
        });
    }

    private void RequireRoomScope(GiftRoomScope scope)
    {
        ThrowIfDisposed();
        room.Capture(current_room =>
        {
            RequireScope(new GiftOperationScope(scope.Session, scope.SessionGeneration));
            if (!current_room.IsReady ||
                current_room.RoomId != scope.RoomId ||
                current_room.Generation != scope.RoomGeneration ||
                current_room.Revision != scope.RoomRevision)
            {
                throw new InvalidOperationException(
                    "The ready room changed before gift dispatch.");
            }
            return true;
        });
    }

    private GiftPurchaseScope CapturePurchaseScope(
        long? expected_session_generation,
        long? expected_catalog_generation,
        CancellationToken cancellation_token)
    {
        ValidateExpectedRevision(
            expected_catalog_generation,
            nameof(expected_catalog_generation));
        GiftOperationScope gift_scope = CaptureScope(
            expected_session_generation,
            cancellation_token);
        CatalogManagerScope catalog_scope = catalog_manager.CaptureScope(
            expected_session_generation,
            expected_catalog_generation);
        if (!ReferenceEquals(catalog_scope.Session, gift_scope.Session) ||
            catalog_scope.SessionGeneration != gift_scope.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The catalog and gift state are not bound to the same hotel session.");
        }
        return new GiftPurchaseScope(
            gift_scope.Session,
            gift_scope.SessionGeneration,
            catalog_scope.CatalogGeneration);
    }

    private void RequirePurchaseScope(GiftPurchaseScope scope)
    {
        RequireScope(new GiftOperationScope(scope.Session, scope.SessionGeneration));
        CatalogManagerState state = catalog_manager.State;
        if (!ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration ||
            state.CatalogGeneration != scope.CatalogGeneration)
        {
            throw new InvalidOperationException(
                "The catalog generation changed before gift purchase dispatch.");
        }
    }

    private static void ValidateExpectedRevision(long? revision, string argument_name)
    {
        if (revision is <= 0)
            throw new ArgumentOutOfRangeException(argument_name);
    }

    private static bool UsesUnityGiftWire(ClientType client)
    {
        if (!ClientTypes.IsSupported(client))
            throw new NotSupportedException("The gift operation requires a supported client.");
        return client is ClientType.Unity;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(DisposalStarted(), this);

    private readonly record struct GiftOperationScope(
        Session Session,
        long SessionGeneration);

    private readonly record struct GiftRevisionScope(
        Session Session,
        long SessionGeneration,
        long SourceRevision,
        bool PinSourceRevision);

    private readonly record struct GiftRoomScope(
        Session Session,
        long SessionGeneration,
        Id RoomId,
        long RoomGeneration,
        long RoomRevision);

    private readonly record struct GiftPurchaseScope(
        Session Session,
        long SessionGeneration,
        long CatalogGeneration);

    private sealed class Invocation(GiftApplication owner) : IDisposable
    {
        private GiftApplication? current = owner;

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
                Action<T>? listener_value = Interlocked.Exchange(ref current_listener, null);
                if (source_value is not null && listener_value is not null)
                    source_value.Unsubscribe(listener_value);
            }
        }
    }
}
