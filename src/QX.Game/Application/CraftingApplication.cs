using System.Text;
using Qx.Interception;

namespace Qx.Game.Application;

internal sealed partial class CraftingApplication : IApplicationFeature, ICraftingOperations
{
    private readonly IConnection connection;
    private readonly CraftingManager crafting;
    private readonly RoomManager room;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly GuardedEventSource<CraftingChanged> changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object lifecycle_sync = new();
    private readonly TaskCompletionSource disposal = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<int> invocation_depth = new();
    private int active_invocations;
    private bool dispose_started;
    private bool cleanup_finished;
    private bool disposal_finished;

    public CraftingApplication(
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
        crafting = game.Crafting;
        room = game.Room;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new GuardedEventSource<CraftingChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<CraftingStateRequest, CraftingStateView>(
                CraftingApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<CraftingProductsPageRequest, CraftingProductsPage>(
                CraftingApplicationDescriptors.ProductsList,
                (request, _) => ValueTask.FromResult(ReadProducts(request))),
            new ApplicationCallBinding<CraftingRecipePageRequest, CraftingRecipePage>(
                CraftingApplicationDescriptors.RecipeList,
                (request, _) => ValueTask.FromResult(ReadRecipe(request))),
            new ApplicationCallBinding<
                CraftingProductsRefreshRequest,
                CraftingProductsRefreshResult>(
                CraftingApplicationDescriptors.ProductsRefresh,
                RefreshProducts),
            new ApplicationCallBinding<
                CraftingRecipeRefreshRequest,
                CraftingRecipeRefreshResult>(
                CraftingApplicationDescriptors.RecipeRefresh,
                RefreshRecipe),
            new ApplicationCallBinding<
                CraftingAvailabilityRefreshRequest,
                CraftingAvailabilityRefreshResult>(
                CraftingApplicationDescriptors.AvailabilityRefresh,
                RefreshAvailability),
            new ApplicationCallBinding<
                CraftingCraftRequest,
                CraftingCraftDispatchReceipt>(
                CraftingApplicationDescriptors.Craft,
                CraftRecipe),
            new ApplicationCallBinding<
                CraftingSecretCraftRequest,
                CraftingSecretCraftDispatchReceipt>(
                CraftingApplicationDescriptors.SecretCraft,
                CraftSecret),
            new ApplicationEventBinding<CraftingChanged>(
                CraftingApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        crafting.StateCommitted += ObserveCommit;
        crafting.StateChanged += PublishChanged;
        try
        {
            crafting.BindOperations(this);
        }
        catch
        {
            crafting.StateCommitted -= ObserveCommit;
            crafting.StateChanged -= PublishChanged;
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
            crafting.UnbindOperations(this);
            crafting.StateCommitted -= ObserveCommit;
            crafting.StateChanged -= PublishChanged;
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
                throw new ObjectDisposedException(nameof(CraftingApplication));
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
            throw new ObjectDisposedException(nameof(CraftingApplication));
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
            lifetime.Dispose();
            disposal.TrySetResult();
        }
    }

    private bool DisposalStarted() => Volatile.Read(ref dispose_started);

    private bool PublicationCurrent(CraftingStateUpdate update) =>
        !DisposalStarted() && crafting.IsCurrentPublication(update);

    private CraftingRoomScope CaptureRoomScope(
        long? expected_session_generation,
        long? expected_room_generation,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        ValidateExpectedRevision(
            expected_session_generation,
            nameof(expected_session_generation));
        ValidateExpectedRevision(
            expected_room_generation,
            nameof(expected_room_generation));
        CraftingState state = crafting.State;
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(state.Session, session))
        {
            throw new InvalidOperationException(
                "The crafting state is not bound to the active hotel session.");
        }
        if (expected_session_generation is long expected_session &&
            expected_session != state.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The active crafting session generation does not match the expected generation.");
        }
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(connection.Session, session) ||
                !ReferenceEquals(crafting.State.Session, session))
            {
                throw new InvalidOperationException(
                    "The hotel session changed while the crafting room was captured.");
            }
            if (!current_room.IsReady || current_room.RoomId == 0)
                throw new InvalidOperationException("A ready hotel room is required.");
            if (expected_room_generation is long expected_room &&
                expected_room != current_room.Generation)
            {
                throw new InvalidOperationException(
                    "The ready room generation does not match the expected generation.");
            }
            return new CraftingRoomScope(
                session,
                state.SessionGeneration,
                (Id)current_room.RoomId,
                current_room.Generation,
                current_room.Revision);
        });
    }

    private void RequireDispatchScope(CraftingRoomScope scope)
    {
        ThrowIfDisposed();
        room.Capture(current_room =>
        {
            RequireSessionScope(scope);
            if (!current_room.IsReady ||
                current_room.RoomId != scope.RoomId ||
                current_room.Generation != scope.RoomGeneration ||
                current_room.Revision != scope.RoomRevision)
            {
                throw new InvalidOperationException(
                    "The ready room changed before the crafting request could be dispatched.");
            }
            return true;
        });
    }

    private void RequireResponseScope(CraftingRoomScope scope)
    {
        ThrowIfDisposed();
        room.Capture(current_room =>
        {
            RequireSessionScope(scope);
            if (current_room.RoomId != scope.RoomId ||
                current_room.Generation != scope.RoomGeneration)
            {
                throw new InvalidOperationException(
                    "The ready room changed while the crafting response was pending.");
            }
            return true;
        });
    }

    private bool ResponseScopeActive(CraftingRoomScope scope)
    {
        if (DisposalStarted())
            return false;
        CraftingState state = crafting.State;
        if (!ReferenceEquals(connection.Session, scope.Session) ||
            !ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration)
        {
            return false;
        }
        return room.Capture(current_room =>
            current_room.RoomId == scope.RoomId &&
            current_room.Generation == scope.RoomGeneration);
    }

    private void RequireSessionScope(CraftingRoomScope scope)
    {
        CraftingState state = crafting.State;
        if (!ReferenceEquals(connection.Session, scope.Session) ||
            !ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The hotel session changed during the crafting operation.");
        }
    }

    private long CaptureRequestEpoch(
        CraftingRequestRoute route,
        CraftingRoomScope scope) => crafting.CaptureRequestEpoch(
        route,
        scope.Session,
        scope.SessionGeneration);

    private long AdvanceRequestEpoch(
        CraftingRequestRoute route,
        long baseline,
        CraftingRoomScope scope)
    {
        RequireDispatchScope(scope);
        return crafting.AdvanceRequestEpoch(
            route,
            baseline,
            scope.Session,
            scope.SessionGeneration);
    }

    private static void ValidateExpectedRevision(long? revision, string argument_name)
    {
        if (revision is <= 0)
            throw new ArgumentOutOfRangeException(argument_name);
    }

    private static void ValidatePage(int offset, int limit, long? snapshot_revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ValidatePageLimit(limit);
        if (snapshot_revision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot_revision));
        if (offset != 0 && snapshot_revision is null)
        {
            throw new ArgumentException(
                "Continuation pages require a snapshot revision.",
                nameof(snapshot_revision));
        }
    }

    private static void ValidatePageLimit(int limit)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidateTypedId(Id value, ClientType client, string argument_name)
    {
        long id = value;
        bool valid = ClientTypes.IsFlash(client)
            ? id is > 0 and <= int.MaxValue
            : ClientTypes.IsUnity(client) && id > 0;
        if (!valid)
            throw new ArgumentOutOfRangeException(argument_name);
    }

    private static void ValidateTypedItems(
        IReadOnlyList<Id> item_ids,
        ClientType client,
        string argument_name)
    {
        ArgumentNullException.ThrowIfNull(item_ids, argument_name);
        if (item_ids.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(argument_name);
        foreach (Id item_id in item_ids)
            ValidateTypedId(item_id, client, argument_name);
    }

    private static void ValidateWireString(string value, string argument_name)
    {
        ArgumentNullException.ThrowIfNull(value, argument_name);
        if (Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(argument_name);
    }

    private static void ValidateTypedRecipeCode(string value, string argument_name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, argument_name);
        ValidateWireString(value, argument_name);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(DisposalStarted(), this);

    private readonly record struct CraftingRoomScope(
        Session Session,
        long SessionGeneration,
        Id RoomId,
        long RoomGeneration,
        long RoomRevision);

    private sealed class Invocation(CraftingApplication owner) : IDisposable
    {
        private CraftingApplication? current = owner;

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
