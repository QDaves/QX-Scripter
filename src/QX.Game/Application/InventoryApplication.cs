using System.Text;
using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class InventoryApplication : IApplicationFeature
{
    private const int commit_history_limit = 32;
    private const int snapshot_lease_limit = 16;

    private readonly IConnection connection;
    private readonly InventoryManager inventory;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<InventoryFurniChanged> furni_changed;
    private readonly ApplicationEventSource<InventoryPetChanged> pets_changed;
    private readonly object updates_sync = new();
    private readonly List<InventoryStateUpdate> furni_fragments = [];
    private readonly List<InventoryStateUpdate> pet_fragments = [];
    private readonly object furni_leases_sync = new();
    private readonly Dictionary<long, FurniSnapshotLease> furni_leases = [];
    private readonly Queue<long> furni_lease_order = [];
    private readonly object pet_leases_sync = new();
    private readonly Dictionary<long, PetSnapshotLease> pet_leases = [];
    private readonly Queue<long> pet_lease_order = [];
    private long lease_revision;
    private int disposed;

    public InventoryApplication(
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
        inventory = game.Inventory;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        furni_changed = new ApplicationEventSource<InventoryFurniChanged>(observer_error);
        pets_changed = new ApplicationEventSource<InventoryPetChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<InventoryStateRequest, InventoryStateView>(
                InventoryApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<InventoryFurniPageRequest, InventoryFurniPage>(
                InventoryApplicationDescriptors.FurniList,
                (request, _) => ValueTask.FromResult(FurniList(request))),
            new ApplicationCallBinding<InventoryFurniRefreshRequest, InventoryFurniPage>(
                InventoryApplicationDescriptors.FurniRefresh,
                RefreshFurni),
            new ApplicationCallBinding<InventoryPetPageRequest, InventoryPetPage>(
                InventoryApplicationDescriptors.PetsList,
                (request, _) => ValueTask.FromResult(PetList(request))),
            new ApplicationCallBinding<InventoryPetRefreshRequest, InventoryPetPage>(
                InventoryApplicationDescriptors.PetsRefresh,
                RefreshPets),
            new ApplicationCallBinding<InventoryAvatarEffectRequest, InventoryDispatchResult>(
                InventoryApplicationDescriptors.AvatarEffectActivate,
                ActivateEffect),
            new ApplicationEventBinding<InventoryFurniChanged>(
                InventoryApplicationDescriptors.FurniChanged,
                furni_changed.Subscribe),
            new ApplicationEventBinding<InventoryPetChanged>(
                InventoryApplicationDescriptors.PetsChanged,
                pets_changed.Subscribe)
        ]);
        inventory.StateCommitted += OnStateCommitted;
        inventory.StateChanged += OnStateChanged;
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public InventoryStateView ReadState(InventoryStateRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        return StateView(inventory.State);
    }

    public InventoryFurniPage FurniList(InventoryFurniPageRequest request)
    {
        ThrowIfDisposed();
        ValidateFurniRequest(request);
        FurniSnapshotLease lease = request.SnapshotRevision is long revision
            ? FurniLease(revision, request.ItemId)
            : StoreFurniLease(inventory.State, request.ItemId);
        return FurniPage(lease, request.Offset, request.Limit);
    }

    public InventoryPetPage PetList(InventoryPetPageRequest request)
    {
        ThrowIfDisposed();
        ValidatePetRequest(request);
        PetSnapshotLease lease = request.SnapshotRevision is long revision
            ? PetLease(revision, request.PetId, request.Name)
            : StorePetLease(inventory.State, request.PetId, request.Name);
        return PetPage(lease, request.Offset, request.Limit);
    }

    public async ValueTask<InventoryFurniPage> RefreshFurni(
        InventoryFurniRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.ItemId, nameof(request.ItemId));
        ValidateLimit(request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        InventoryState loaded = await RefreshFurniState(
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        FurniSnapshotLease lease = StoreFurniLease(loaded, request.ItemId);
        return FurniPage(lease, 0, request.Limit);
    }

    public async ValueTask<InventoryPetPage> RefreshPets(
        InventoryPetRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.PetId, nameof(request.PetId));
        ValidateName(request.Name);
        ValidateLimit(request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        InventoryState loaded = await RefreshPetState(
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        PetSnapshotLease lease = StorePetLease(loaded, request.PetId, request.Name);
        return PetPage(lease, 0, request.Limit);
    }

    public ValueTask<InventoryDispatchResult> ActivateEffect(
        InventoryAvatarEffectRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        InventoryOperationScope scope = CaptureScope(cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.Inventory.AvatarEffects.ActivationRequest,
            new AvatarEffectActivationRequest(request.EffectId),
            scope.Session,
            cancellation_token,
            () => RequireScope(scope));
        RequireScope(scope);
        return ValueTask.FromResult(new InventoryDispatchResult(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.Generation,
            inventory.State.Revision,
            request.EffectId));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        inventory.StateCommitted -= OnStateCommitted;
        inventory.StateChanged -= OnStateChanged;
        lock (updates_sync)
        {
            furni_fragments.Clear();
            pet_fragments.Clear();
        }
        ClearLeases();
        furni_changed.Dispose();
        pets_changed.Dispose();
    }

    private async Task<InventoryState> RefreshFurniState(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        InventoryOperationScope scope = CaptureScope(cancellation_token);
        long started = time_provider.GetTimestamp();
        long attempt_baseline = -1;
        InventoryStateUpdate? accepted = null;
        await requests.RequestAsync(
            MessageContracts.Inventory.Furni.Request,
            new FurniInventoryRequest(),
            MessageContracts.Inventory.Furni.Snapshot,
            scope.Session,
            match: response =>
            {
                if (!ScopeActive(scope))
                    return false;
                InventoryStateUpdate? update = FindFurniCommit(
                    scope.Generation,
                    Volatile.Read(ref attempt_baseline),
                    response);
                if (update is null)
                    return false;
                Volatile.Write(ref accepted, update);
                return true;
            },
            timeout_ms: timeout_milliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () =>
            {
                RequireScope(scope);
                lock (updates_sync)
                {
                    Volatile.Write(ref attempt_baseline, inventory.State.Furni.SnapshotRevision);
                    Volatile.Write(ref accepted, null);
                }
            }).ConfigureAwait(false);
        RequireScope(scope);
        InventoryStateUpdate fragment = Volatile.Read(ref accepted)
            ?? throw new InvalidOperationException(
                "The accepted furni inventory response was not committed by the passive state owner.");
        InventoryState loaded = fragment.State.Furni.Loaded
            ? fragment.State
            : await WaitForFurniLoad(
                scope,
                ((FurniFragmentCommit)fragment.Value!).LoadGeneration,
                Remaining(started, timeout_milliseconds),
                timeout_milliseconds,
                cancellation_token).ConfigureAwait(false);
        RequireScope(scope);
        return loaded;
    }

    private async Task<InventoryState> RefreshPetState(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        InventoryOperationScope scope = CaptureScope(cancellation_token);
        long started = time_provider.GetTimestamp();
        long attempt_baseline = -1;
        InventoryStateUpdate? accepted = null;
        await requests.RequestAsync(
            MessageContracts.Inventory.Pets.Request,
            new PetInventoryRequest(),
            MessageContracts.Inventory.Pets.Snapshot,
            scope.Session,
            match: response =>
            {
                if (!ScopeActive(scope))
                    return false;
                InventoryStateUpdate? update = FindPetCommit(
                    scope.Generation,
                    Volatile.Read(ref attempt_baseline),
                    response);
                if (update is null)
                    return false;
                Volatile.Write(ref accepted, update);
                return true;
            },
            timeout_ms: timeout_milliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () =>
            {
                RequireScope(scope);
                lock (updates_sync)
                {
                    Volatile.Write(ref attempt_baseline, inventory.State.Pets.SnapshotRevision);
                    Volatile.Write(ref accepted, null);
                }
            }).ConfigureAwait(false);
        RequireScope(scope);
        InventoryStateUpdate fragment = Volatile.Read(ref accepted)
            ?? throw new InvalidOperationException(
                "The accepted pet inventory response was not committed by the passive state owner.");
        InventoryState loaded = fragment.State.Pets.Loaded
            ? fragment.State
            : await WaitForPetLoad(
                scope,
                ((PetFragmentCommit)fragment.Value!).LoadGeneration,
                Remaining(started, timeout_milliseconds),
                timeout_milliseconds,
                cancellation_token).ConfigureAwait(false);
        RequireScope(scope);
        return loaded;
    }

    private async Task<InventoryState> WaitForFurniLoad(
        InventoryOperationScope scope,
        long load_generation,
        TimeSpan timeout,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        if (timeout <= TimeSpan.Zero)
            throw new RequestTimeoutException(
                MessageKeys.Inventory.Furni.Request.Value,
                MessageKeys.Inventory.Furni.Snapshot.Value,
                timeout_milliseconds);
        var completion = new TaskCompletionSource<InventoryState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(InventoryStateUpdate update)
        {
            if (!ReferenceEquals(update.State.Session, scope.Session) ||
                update.State.Generation != scope.Generation)
            {
                completion.TrySetException(
                    new InvalidOperationException("The hotel session changed while loading the furni inventory."));
                return;
            }
            FurniInventoryState current = update.State.Furni;
            if (current.RecoveryPending)
            {
                completion.TrySetException(
                    new FragmentedLoadCorrelationException(
                        "inventory",
                        current.RecoveryRetiredRequestEpoch,
                        current.RecoveryActiveRequestEpoch));
                return;
            }
            if (current.LoadGeneration > load_generation)
            {
                completion.TrySetException(
                    new InvalidOperationException("The furni inventory load generation changed before completion."));
                return;
            }
            if (current.Loaded && current.LoadGeneration == load_generation)
                completion.TrySetResult(update.State);
        }
        inventory.StateCommitted += Observe;
        try
        {
            Observe(new InventoryStateUpdate(
                InventoryStateChangeKind.FurniFragment,
                inventory.State,
                null));
            try
            {
                return await completion.Task.WaitAsync(timeout, cancellation_token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new RequestTimeoutException(
                    MessageKeys.Inventory.Furni.Request.Value,
                    MessageKeys.Inventory.Furni.Snapshot.Value,
                    timeout_milliseconds);
            }
        }
        finally
        {
            inventory.StateCommitted -= Observe;
        }
    }

    private async Task<InventoryState> WaitForPetLoad(
        InventoryOperationScope scope,
        long load_generation,
        TimeSpan timeout,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        if (timeout <= TimeSpan.Zero)
            throw new RequestTimeoutException(
                MessageKeys.Inventory.Pets.Request.Value,
                MessageKeys.Inventory.Pets.Snapshot.Value,
                timeout_milliseconds);
        var completion = new TaskCompletionSource<InventoryState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(InventoryStateUpdate update)
        {
            if (!ReferenceEquals(update.State.Session, scope.Session) ||
                update.State.Generation != scope.Generation)
            {
                completion.TrySetException(
                    new InvalidOperationException("The hotel session changed while loading the pet inventory."));
                return;
            }
            PetInventoryState current = update.State.Pets;
            if (current.RecoveryPending)
            {
                completion.TrySetException(
                    new FragmentedLoadCorrelationException(
                        "pet inventory",
                        current.RecoveryRetiredRequestEpoch,
                        current.RecoveryActiveRequestEpoch));
                return;
            }
            if (current.LoadGeneration > load_generation)
            {
                completion.TrySetException(
                    new InvalidOperationException("The pet inventory load generation changed before completion."));
                return;
            }
            if (current.Loaded && current.LoadGeneration == load_generation)
                completion.TrySetResult(update.State);
        }
        inventory.StateCommitted += Observe;
        try
        {
            Observe(new InventoryStateUpdate(
                InventoryStateChangeKind.PetFragment,
                inventory.State,
                null));
            try
            {
                return await completion.Task.WaitAsync(timeout, cancellation_token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new RequestTimeoutException(
                    MessageKeys.Inventory.Pets.Request.Value,
                    MessageKeys.Inventory.Pets.Snapshot.Value,
                    timeout_milliseconds);
            }
        }
        finally
        {
            inventory.StateCommitted -= Observe;
        }
    }

    private InventoryStateUpdate? FindFurniCommit(
        long generation,
        long baseline_revision,
        FurniList response)
    {
        lock (updates_sync)
        {
            for (int index = furni_fragments.Count - 1; index >= 0; index--)
            {
                InventoryStateUpdate update = furni_fragments[index];
                if (update.State.Generation == generation &&
                    update.State.Furni.SnapshotRevision > baseline_revision &&
                    update.Value is FurniFragmentCommit fragment &&
                    InventoryManager.Equivalent(fragment, response))
                {
                    return update;
                }
            }
            return null;
        }
    }

    private InventoryStateUpdate? FindPetCommit(
        long generation,
        long baseline_revision,
        PetInventory response)
    {
        lock (updates_sync)
        {
            for (int index = pet_fragments.Count - 1; index >= 0; index--)
            {
                InventoryStateUpdate update = pet_fragments[index];
                if (update.State.Generation == generation &&
                    update.State.Pets.SnapshotRevision > baseline_revision &&
                    update.Value is PetFragmentCommit fragment &&
                    InventoryManager.Equivalent(fragment, response))
                {
                    return update;
                }
            }
            return null;
        }
    }

    private void OnStateCommitted(InventoryStateUpdate update)
    {
        lock (updates_sync)
        {
            if (update.Kind is InventoryStateChangeKind.Reset)
            {
                furni_fragments.Clear();
                pet_fragments.Clear();
            }
            else if (update.Kind is InventoryStateChangeKind.FurniFragment)
            {
                AddCommit(furni_fragments, update);
            }
            else if (update.Kind is InventoryStateChangeKind.PetFragment)
            {
                AddCommit(pet_fragments, update);
            }
        }
        if (update.Kind is InventoryStateChangeKind.Reset)
            ClearLeases();
    }

    private void OnStateChanged(InventoryStateUpdate update) => PublishEvents(update);

    private void PublishEvents(InventoryStateUpdate update)
    {
        DateTimeOffset now = time_provider.GetUtcNow();
        switch (update.Kind)
        {
            case InventoryStateChangeKind.FurniFragment
                when update.Value is FurniFragmentCommit && FurniLoadCurrent(update.State):
                PublishFurni(update, now, InventoryChangeKind.Loaded, null);
                break;
            case InventoryStateChangeKind.FurniAddedOrUpdated
                when update.Value is FurniItemsCommit items:
                foreach (InventoryItemSnapshot item in items.Added)
                    PublishFurni(update, now, InventoryChangeKind.Added, item);
                foreach (InventoryItemSnapshot item in items.Updated)
                    PublishFurni(update, now, InventoryChangeKind.Updated, item);
                break;
            case InventoryStateChangeKind.FurniRemoved
                when update.Value is FurniRemovedCommit removed:
                foreach (InventoryItemSnapshot item in removed.Items)
                    PublishFurni(update, now, InventoryChangeKind.Removed, item);
                break;
            case InventoryStateChangeKind.FurniInvalidated:
                PublishFurni(update, now, InventoryChangeKind.Invalidated, null);
                break;
            case InventoryStateChangeKind.PetFragment
                when update.Value is PetFragmentCommit && PetLoadCurrent(update.State):
                PublishPet(update, now, InventoryChangeKind.Loaded, null, null);
                break;
            case InventoryStateChangeKind.PetAddedOrUpdated
                when update.Value is PetItemCommit pet:
                PublishPet(
                    update,
                    now,
                    pet.Added ? InventoryChangeKind.Added : InventoryChangeKind.Updated,
                    pet.Pet,
                    pet.Added ? pet.OpenInventory : null);
                break;
            case InventoryStateChangeKind.PetRemoved
                when update.Value is PetRemovedCommit { Pet: not null } pet:
                PublishPet(update, now, InventoryChangeKind.Removed, pet.Pet, null);
                break;
            case InventoryStateChangeKind.Reset:
                PublishFurni(update, now, InventoryChangeKind.Reset, null);
                PublishPet(update, now, InventoryChangeKind.Reset, null, null);
                break;
        }
    }

    private bool FurniLoadCurrent(InventoryState published)
    {
        InventoryState current = inventory.State;
        return ReferenceEquals(current.Session, published.Session) &&
            current.Generation == published.Generation &&
            current.Furni.Loaded &&
            current.Furni.LoadGeneration == published.Furni.LoadGeneration;
    }

    private bool PetLoadCurrent(InventoryState published)
    {
        InventoryState current = inventory.State;
        return ReferenceEquals(current.Session, published.Session) &&
            current.Generation == published.Generation &&
            current.Pets.Loaded &&
            current.Pets.LoadGeneration == published.Pets.LoadGeneration;
    }

    private void PublishFurni(
        InventoryStateUpdate update,
        DateTimeOffset now,
        InventoryChangeKind kind,
        InventoryItemSnapshot? item) => furni_changed.Publish(new InventoryFurniChanged(
            kind,
            now,
            update.State.Generation,
            update.State.Revision,
            update.State.Furni.SnapshotRevision,
            update.State.Furni.LoadGeneration,
            item));

    private void PublishPet(
        InventoryStateUpdate update,
        DateTimeOffset now,
        InventoryChangeKind kind,
        InventoryPetSnapshot? pet,
        bool? open_inventory) => pets_changed.Publish(new InventoryPetChanged(
            kind,
            now,
            update.State.Generation,
            update.State.Revision,
            update.State.Pets.SnapshotRevision,
            update.State.Pets.LoadGeneration,
            pet,
            open_inventory));

    private FurniSnapshotLease StoreFurniLease(InventoryState state, Id? item_id)
    {
        InventoryItemSnapshot[] values = state.Furni.Items.Values
            .Where(item => item_id is null || item.ItemId == item_id)
            .OrderBy(item => (long)item.ItemId)
            .ToArray();
        long revision = Interlocked.Increment(ref lease_revision);
        var lease = new FurniSnapshotLease(
            revision,
            state.Session,
            state.Generation,
            state.Revision,
            state.Furni,
            item_id,
            Array.AsReadOnly(values));
        lock (furni_leases_sync)
        {
            furni_leases.Add(revision, lease);
            furni_lease_order.Enqueue(revision);
            TrimLeases(furni_leases, furni_lease_order);
        }
        return lease;
    }

    private PetSnapshotLease StorePetLease(InventoryState state, Id? pet_id, string? name)
    {
        InventoryPetSnapshot[] values = state.Pets.Pets.Values
            .Where(pet => pet_id is null || pet.Id == pet_id)
            .Where(pet => name is null || string.Equals(pet.Name, name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pet => (long)pet.Id)
            .ToArray();
        long revision = Interlocked.Increment(ref lease_revision);
        var lease = new PetSnapshotLease(
            revision,
            state.Session,
            state.Generation,
            state.Revision,
            state.Pets,
            pet_id,
            name,
            Array.AsReadOnly(values));
        lock (pet_leases_sync)
        {
            pet_leases.Add(revision, lease);
            pet_lease_order.Enqueue(revision);
            TrimLeases(pet_leases, pet_lease_order);
        }
        return lease;
    }

    private FurniSnapshotLease FurniLease(long revision, Id? item_id)
    {
        lock (furni_leases_sync)
        {
            if (!furni_leases.TryGetValue(revision, out FurniSnapshotLease? lease) ||
                lease.ItemId != item_id ||
                !LeaseActive(lease.Session, lease.SessionGeneration))
            {
                throw new InvalidOperationException("The furni inventory snapshot lease is unavailable or does not match the requested filter.");
            }
            return lease;
        }
    }

    private PetSnapshotLease PetLease(long revision, Id? pet_id, string? name)
    {
        lock (pet_leases_sync)
        {
            if (!pet_leases.TryGetValue(revision, out PetSnapshotLease? lease) ||
                lease.PetId != pet_id ||
                !string.Equals(lease.Name, name, StringComparison.OrdinalIgnoreCase) ||
                !LeaseActive(lease.Session, lease.SessionGeneration))
            {
                throw new InvalidOperationException("The pet inventory snapshot lease is unavailable or does not match the requested filters.");
            }
            return lease;
        }
    }

    private bool LeaseActive(Session? lease_session, long session_generation)
    {
        InventoryState current = inventory.State;
        return ReferenceEquals(current.Session, lease_session) &&
            ReferenceEquals(connection.Session, lease_session) &&
            current.Generation == session_generation;
    }

    private InventoryFurniPage FurniPage(FurniSnapshotLease lease, int offset, int limit)
    {
        IReadOnlyList<InventoryItemSnapshot> page = Slice(lease.Items, offset, limit);
        bool connected = lease.Session is not null && ReferenceEquals(connection.Session, lease.Session);
        return new InventoryFurniPage(
            connected,
            connected ? lease.Session!.Client : null,
            lease.SessionGeneration,
            lease.StateRevision,
            lease.Revision,
            lease.State.SnapshotRevision,
            lease.State.LoadGeneration,
            lease.State.Loaded,
            lease.State.Loading,
            lease.State.Stale,
            lease.State.RecoveryPending,
            lease.State.ExpectedFragments,
            lease.State.ReceivedFragments,
            lease.State.Items.Count,
            lease.Items.Count,
            offset,
            NextOffset(offset, page.Count, lease.Items.Count),
            page);
    }

    private InventoryPetPage PetPage(PetSnapshotLease lease, int offset, int limit)
    {
        IReadOnlyList<InventoryPetSnapshot> page = Slice(lease.Pets, offset, limit);
        bool connected = lease.Session is not null && ReferenceEquals(connection.Session, lease.Session);
        return new InventoryPetPage(
            connected,
            connected ? lease.Session!.Client : null,
            lease.SessionGeneration,
            lease.StateRevision,
            lease.Revision,
            lease.State.SnapshotRevision,
            lease.State.LoadGeneration,
            lease.State.Loaded,
            lease.State.Loading,
            lease.State.Stale,
            lease.State.RecoveryPending,
            lease.State.ExpectedFragments,
            lease.State.ReceivedFragments,
            lease.State.Pets.Count,
            lease.Pets.Count,
            offset,
            NextOffset(offset, page.Count, lease.Pets.Count),
            page);
    }

    private InventoryStateView StateView(InventoryState state)
    {
        bool connected = state.Session is not null && ReferenceEquals(connection.Session, state.Session);
        return new InventoryStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.Generation,
            state.Revision,
            Summary(state.Furni),
            Summary(state.Pets));
    }

    private static InventoryCollectionStateView Summary(FurniInventoryState state) => new(
        state.SnapshotRevision,
        state.LoadGeneration,
        state.Loaded,
        state.Loading,
        state.Stale,
        state.RecoveryPending,
        state.ExpectedFragments,
        state.ReceivedFragments,
        state.Items.Count);

    private static InventoryCollectionStateView Summary(PetInventoryState state) => new(
        state.SnapshotRevision,
        state.LoadGeneration,
        state.Loaded,
        state.Loading,
        state.Stale,
        state.RecoveryPending,
        state.ExpectedFragments,
        state.ReceivedFragments,
        state.Pets.Count);

    private InventoryOperationScope CaptureScope(CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        InventoryState current = inventory.State;
        Session active_session = current.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(connection.Session, active_session))
            throw new InvalidOperationException("The inventory state is not bound to the active hotel session.");
        return new InventoryOperationScope(active_session, current.Generation);
    }

    private bool ScopeActive(InventoryOperationScope scope)
    {
        InventoryState current = inventory.State;
        return ReferenceEquals(connection.Session, scope.Session) &&
            ReferenceEquals(current.Session, scope.Session) &&
            current.Generation == scope.Generation;
    }

    private void RequireScope(InventoryOperationScope scope)
    {
        ThrowIfDisposed();
        if (!ScopeActive(scope))
            throw new InvalidOperationException("The hotel session changed during the inventory operation.");
    }

    private TimeSpan Remaining(long started, int timeout_milliseconds)
    {
        TimeSpan remaining = TimeSpan.FromMilliseconds(timeout_milliseconds) -
            time_provider.GetElapsedTime(started);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void ClearLeases()
    {
        lock (furni_leases_sync)
        {
            furni_leases.Clear();
            furni_lease_order.Clear();
        }
        lock (pet_leases_sync)
        {
            pet_leases.Clear();
            pet_lease_order.Clear();
        }
    }

    private static void AddCommit(List<InventoryStateUpdate> updates, InventoryStateUpdate update)
    {
        updates.Add(update);
        if (updates.Count > commit_history_limit)
            updates.RemoveRange(0, updates.Count - commit_history_limit);
    }

    private static void TrimLeases<T>(Dictionary<long, T> leases, Queue<long> order)
    {
        while (leases.Count > snapshot_lease_limit && order.TryDequeue(out long revision))
            leases.Remove(revision);
    }

    private static IReadOnlyList<T> Slice<T>(IReadOnlyList<T> values, int offset, int limit)
    {
        if (offset >= values.Count)
            return Array.AsReadOnly(Array.Empty<T>());
        int count = Math.Min(limit, values.Count - offset);
        var page = new T[count];
        for (int index = 0; index < count; index++)
            page[index] = values[offset + index];
        return Array.AsReadOnly(page);
    }

    private static int? NextOffset(int offset, int count, int total)
    {
        int next = checked(offset + count);
        return next < total ? next : null;
    }

    private static void ValidateFurniRequest(InventoryFurniPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.ItemId, nameof(request.ItemId));
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
    }

    private static void ValidatePetRequest(InventoryPetPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.PetId, nameof(request.PetId));
        ValidateName(request.Name);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
    }

    private static void ValidatePage(int offset, int limit, long? snapshot_revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ValidateLimit(limit);
        if (snapshot_revision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot_revision));
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

    private static void ValidateId(Id? value, string name)
    {
        if (value is { } identifier && (long)identifier <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateName(string? value)
    {
        if (value is null)
            return;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The pet name cannot be empty.", nameof(value));
        if (Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private readonly record struct InventoryOperationScope(Session Session, long Generation);

    private sealed record FurniSnapshotLease(
        long Revision,
        Session? Session,
        long SessionGeneration,
        long StateRevision,
        FurniInventoryState State,
        Id? ItemId,
        IReadOnlyList<InventoryItemSnapshot> Items);

    private sealed record PetSnapshotLease(
        long Revision,
        Session? Session,
        long SessionGeneration,
        long StateRevision,
        PetInventoryState State,
        Id? PetId,
        string? Name,
        IReadOnlyList<InventoryPetSnapshot> Pets);
}
