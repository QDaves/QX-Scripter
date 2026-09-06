using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Crafting;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

internal enum CraftingRequestRoute
{
    Products,
    Recipe,
    Result,
    Availability
}

internal sealed record CraftingState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long ProductsRevision,
    CraftableProducts? Products,
    long RecipeRevision,
    CraftingRecipe? Recipe,
    long ResultRevision,
    CraftingResult? LastResult,
    long AvailabilityRevision,
    CraftingRecipesAvailable? AvailableRecipes);

internal enum CraftingStateChangeKind
{
    Products,
    Recipe,
    Result,
    Availability,
    Reset
}

internal sealed record CraftingStateUpdate(
    CraftingStateChangeKind Kind,
    CraftingState State,
    object? Value,
    long RequestEpoch,
    long PublicationEpoch,
    bool PublishLegacyReset);

public sealed class CraftingManager : GameStateManager
{
    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<CraftingStateUpdate> publications = [];
    private CraftingState state = InitialState();
    private ICraftingOperations? operations;
    private long products_request_epoch;
    private long recipe_request_epoch;
    private long result_request_epoch;
    private long availability_request_epoch;
    private long committed_generation;
    private long reset_generation = -1;
    private long publication_epoch;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    public CraftableProducts? Products => State.Products;
    public CraftingRecipe? Recipe => State.Recipe;
    public CraftingResult? LastResult => State.LastResult;
    public CraftingRecipesAvailable? AvailableRecipes => State.AvailableRecipes;

    internal CraftingState State => Volatile.Read(ref state);

    public event Action<CraftableProducts>? ProductsReceived;
    public event Action<CraftingRecipe>? RecipeReceived;
    public event Action<CraftingResult>? ResultReceived;
    public event Action<CraftingRecipesAvailable>? AvailableRecipesReceived;
    public event Action? ResetCompleted;
    internal event Action<CraftingStateUpdate>? StateCommitted;
    internal event Action<CraftingStateUpdate>? StateChanged;

    protected override void OnAttach()
    {
        CommitReset(CurrentSession, false);
        OnConnected(BindSession);
        OnOutgoing(
            MessageContracts.Crafting.ProductsRequest,
            (_, generation) => ObserveRequest(CraftingRequestRoute.Products, generation));
        OnOutgoing(
            MessageContracts.Crafting.RecipeRequest,
            (_, generation) => ObserveRequest(CraftingRequestRoute.Recipe, generation));
        OnOutgoing(
            MessageContracts.Crafting.Craft,
            (_, generation) => ObserveRequest(CraftingRequestRoute.Result, generation));
        OnOutgoing(
            MessageContracts.Crafting.SecretCraft,
            (_, generation) => ObserveRequest(CraftingRequestRoute.Result, generation));
        OnOutgoing(
            MessageContracts.Crafting.AvailabilityRequest,
            (_, generation) => ObserveRequest(CraftingRequestRoute.Availability, generation));
        OnIncoming(MessageContracts.Crafting.ProductsSnapshot, ApplyProducts);
        OnIncoming(MessageContracts.Crafting.RecipeSnapshot, ApplyRecipe);
        OnIncoming(MessageContracts.Crafting.Result, ApplyResult);
        OnIncoming(MessageContracts.Crafting.AvailabilitySnapshot, ApplyAvailability);
    }

    public void RequestProducts(Id crafting_furniture_id) =>
        Operations().RequestProducts(crafting_furniture_id);

    public void RequestRecipe(string recipe_code)
    {
        ArgumentNullException.ThrowIfNull(recipe_code);
        Operations().RequestRecipe(recipe_code);
    }

    public void Craft(Id crafting_furniture_id, string recipe_code)
    {
        ArgumentNullException.ThrowIfNull(recipe_code);
        Operations().Craft(crafting_furniture_id, recipe_code);
    }

    public void CraftSecret(
        Id crafting_furniture_id,
        IReadOnlyList<Id> ingredient_item_ids)
    {
        ArgumentNullException.ThrowIfNull(ingredient_item_ids);
        Operations().CraftSecret(crafting_furniture_id, ingredient_item_ids);
    }

    public void RequestAvailableRecipes(
        Id crafting_furniture_id,
        IReadOnlyList<Id> ingredient_item_ids)
    {
        ArgumentNullException.ThrowIfNull(ingredient_item_ids);
        Operations().RequestAvailableRecipes(
            crafting_furniture_id,
            ingredient_item_ids);
    }

    internal void BindOperations(ICraftingOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
            {
                throw new InvalidOperationException(
                    "Crafting operations are already bound.");
            }
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(ICraftingOperations value)
    {
        lock (operations_sync)
        {
            if (ReferenceEquals(operations, value))
                Volatile.Write(ref operations, null);
        }
    }

    internal long CaptureRequestEpoch(
        CraftingRequestRoute route,
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        lock (state_sync)
        {
            RequireRequestScope(
                expected_session,
                expected_session_generation,
                "captured");
            return RequestEpoch(route);
        }
    }

    internal long AdvanceRequestEpoch(
        CraftingRequestRoute route,
        long baseline,
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentNullException.ThrowIfNull(expected_session);
        lock (state_sync)
        {
            RequireRequestScope(
                expected_session,
                expected_session_generation,
                "advanced");
            if (RequestEpoch(route) != baseline)
            {
                throw new InvalidOperationException(
                    "Another crafting request was dispatched before the operation could send.");
            }
            long next = checked(baseline + 1);
            if (!ApplyIfCurrent(
                    expected_session_generation,
                    expected_session,
                    () => SetRequestEpoch(route, next)))
            {
                throw new InvalidOperationException(
                    "The hotel session changed before the crafting request could be dispatched.");
            }
            return next;
        }
    }

    internal bool RequestEpochIsCurrent(
        CraftingRequestRoute route,
        long expected_epoch,
        Session expected_session,
        long expected_session_generation)
    {
        lock (state_sync)
        {
            CraftingState current = state;
            if (!ReferenceEquals(current.Session, expected_session) ||
                current.SessionGeneration != expected_session_generation ||
                RequestEpoch(route) != expected_epoch)
            {
                return false;
            }
        }
        long before = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        long after = CurrentStateGeneration;
        return before == expected_session_generation &&
            after == expected_session_generation &&
            ReferenceEquals(active_session, expected_session);
    }

    internal bool IsCurrentPublication(CraftingStateUpdate update) =>
        UpdateCurrent(update);

    protected override void Reset() => CommitReset(CurrentSession, true);

    private void BindSession(Session session) => CommitReset(session, false);

    private void ApplyProducts(
        CraftableProducts message,
        long state_generation)
    {
        CraftableProducts snapshot = FreezeProducts(message);
        Store(
            state_generation,
            CraftingStateChangeKind.Products,
            CraftingRequestRoute.Products,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                ProductsRevision = checked(current.ProductsRevision + 1),
                Products = snapshot
            });
    }

    private void ApplyRecipe(
        CraftingRecipe message,
        long state_generation)
    {
        CraftingRecipe snapshot = FreezeRecipe(message);
        Store(
            state_generation,
            CraftingStateChangeKind.Recipe,
            CraftingRequestRoute.Recipe,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                RecipeRevision = checked(current.RecipeRevision + 1),
                Recipe = snapshot
            });
    }

    private void ApplyResult(
        CraftingResult message,
        long state_generation)
    {
        CraftingResult snapshot = FreezeResult(message);
        Store(
            state_generation,
            CraftingStateChangeKind.Result,
            CraftingRequestRoute.Result,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                ResultRevision = checked(current.ResultRevision + 1),
                LastResult = snapshot
            });
    }

    private void ApplyAvailability(
        CraftingRecipesAvailable message,
        long state_generation)
    {
        CraftingRecipesAvailable snapshot = message with { };
        Store(
            state_generation,
            CraftingStateChangeKind.Availability,
            CraftingRequestRoute.Availability,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                AvailabilityRevision = checked(current.AvailabilityRevision + 1),
                AvailableRecipes = snapshot
            });
    }

    private void ObserveRequest(
        CraftingRequestRoute route,
        long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        lock (state_sync)
        {
            CraftingState current = state;
            if (state_generation != committed_generation ||
                current.SessionGeneration != state_generation ||
                !ReferenceEquals(current.Session, active_session))
            {
                return;
            }
            long next = checked(RequestEpoch(route) + 1);
            ApplyIfCurrent(
                state_generation,
                active_session,
                () => SetRequestEpoch(route, next));
        }
    }

    private void Store(
        long state_generation,
        CraftingStateChangeKind kind,
        CraftingRequestRoute route,
        object value,
        Func<CraftingState, CraftingState> mutation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            CraftingStateUpdate update;
            lock (state_sync)
            {
                CraftingState current = state;
                if (state_generation != committed_generation ||
                    current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                CraftingState updated = mutation(current);
                long request_epoch = RequestEpoch(route);
                update = null!;
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new CraftingStateUpdate(
                            kind,
                            updated,
                            value,
                            request_epoch,
                            publication_epoch,
                            false);
                    }))
                {
                    return;
                }
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = null;
        if (drain)
        {
            try
            {
                DrainPublications();
            }
            catch (Exception error)
            {
                publication_failure = error;
            }
        }
        ThrowFailures(committed_failure, publication_failure);
    }

    private void CommitReset(Session? active_session, bool publish_legacy_reset)
    {
        long state_generation = CurrentStateGeneration;
        int thread_id = Environment.CurrentManagedThreadId;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            while (delivering && delivery_thread_id != thread_id)
                Monitor.Wait(publication_sync);
            CraftingStateUpdate update;
            lock (state_sync)
            {
                CraftingState current = state;
                if (state_generation < committed_generation ||
                    state_generation == reset_generation &&
                    ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                var updated = new CraftingState(
                    active_session,
                    state_generation,
                    checked(current.Revision + 1),
                    checked(current.ProductsRevision + 1),
                    null,
                    checked(current.RecipeRevision + 1),
                    null,
                    checked(current.ResultRevision + 1),
                    null,
                    checked(current.AvailabilityRevision + 1),
                    null);
                Volatile.Write(ref state, updated);
                products_request_epoch = 0;
                recipe_request_epoch = 0;
                result_request_epoch = 0;
                availability_request_epoch = 0;
                committed_generation = state_generation;
                reset_generation = state_generation;
                publication_epoch = checked(publication_epoch + 1);
                update = new CraftingStateUpdate(
                    CraftingStateChangeKind.Reset,
                    updated,
                    null,
                    0,
                    publication_epoch,
                    publish_legacy_reset);
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = null;
        if (drain)
        {
            try
            {
                DrainPublications();
            }
            catch (Exception error)
            {
                publication_failure = error;
            }
        }
        ThrowFailures(committed_failure, publication_failure);
    }

    private void DrainPublications()
    {
        Exception? failure = null;
        while (true)
        {
            CraftingStateUpdate update;
            lock (publication_sync)
            {
                if (!publications.TryDequeue(out update!))
                {
                    publishing = false;
                    break;
                }
                delivering = true;
                delivery_thread_id = Environment.CurrentManagedThreadId;
            }
            try
            {
                if (!UpdateCurrent(update))
                    continue;
                failure = Notify(StateChanged, update, update, failure);
                if (!UpdateCurrent(update))
                    continue;
                failure = NotifyLegacy(update, failure);
            }
            finally
            {
                lock (publication_sync)
                {
                    delivering = false;
                    delivery_thread_id = 0;
                    Monitor.PulseAll(publication_sync);
                }
            }
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private bool UpdateCurrent(CraftingStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            CraftingState current = State;
            if (current.SessionGeneration != update.State.SessionGeneration ||
                !ReferenceEquals(current.Session, update.State.Session))
            {
                return false;
            }
        }
        long before = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        long after = CurrentStateGeneration;
        return before == update.State.SessionGeneration &&
            after == update.State.SessionGeneration &&
            ReferenceEquals(active_session, update.State.Session);
    }

    private Exception? NotifyCommitted(CraftingStateUpdate update)
    {
        Exception? failure = null;
        Action<CraftingStateUpdate>? listeners = StateCommitted;
        if (listeners is null)
            return null;
        foreach (Action<CraftingStateUpdate> listener in listeners
            .GetInvocationList()
            .Cast<Action<CraftingStateUpdate>>())
        {
            try
            {
                listener(update);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private Exception? NotifyLegacy(
        CraftingStateUpdate update,
        Exception? failure) => update.Kind switch
        {
            CraftingStateChangeKind.Products => Notify(
                ProductsReceived,
                (CraftableProducts)update.Value!,
                update,
                failure),
            CraftingStateChangeKind.Recipe => Notify(
                RecipeReceived,
                (CraftingRecipe)update.Value!,
                update,
                failure),
            CraftingStateChangeKind.Result => Notify(
                ResultReceived,
                (CraftingResult)update.Value!,
                update,
                failure),
            CraftingStateChangeKind.Availability => Notify(
                AvailableRecipesReceived,
                (CraftingRecipesAvailable)update.Value!,
                update,
                failure),
            CraftingStateChangeKind.Reset when update.PublishLegacyReset => Notify(
                ResetCompleted,
                update,
                failure),
            CraftingStateChangeKind.Reset => failure,
            _ => throw new ArgumentOutOfRangeException(nameof(update))
        };

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        CraftingStateUpdate update,
        Exception? failure)
    {
        if (listeners is null)
            return failure;
        foreach (Action<T> listener in listeners.GetInvocationList().Cast<Action<T>>())
        {
            if (!UpdateCurrent(update))
                break;
            try
            {
                listener(value);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private Exception? Notify(
        Action? listeners,
        CraftingStateUpdate update,
        Exception? failure)
    {
        if (listeners is null)
            return failure;
        foreach (Action listener in listeners.GetInvocationList().Cast<Action>())
        {
            if (!UpdateCurrent(update))
                break;
            try
            {
                listener();
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private void RequireRequestScope(
        Session expected_session,
        long expected_session_generation,
        string operation)
    {
        CraftingState current = state;
        if (!ReferenceEquals(current.Session, expected_session) ||
            current.SessionGeneration != expected_session_generation ||
            committed_generation != expected_session_generation)
        {
            throw new InvalidOperationException(
                $"The crafting request epoch cannot be {operation} for a stale hotel session.");
        }
    }

    private long RequestEpoch(CraftingRequestRoute route) => route switch
    {
        CraftingRequestRoute.Products => products_request_epoch,
        CraftingRequestRoute.Recipe => recipe_request_epoch,
        CraftingRequestRoute.Result => result_request_epoch,
        CraftingRequestRoute.Availability => availability_request_epoch,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private void SetRequestEpoch(CraftingRequestRoute route, long value)
    {
        switch (route)
        {
            case CraftingRequestRoute.Products:
                products_request_epoch = value;
                break;
            case CraftingRequestRoute.Recipe:
                recipe_request_epoch = value;
                break;
            case CraftingRequestRoute.Result:
                result_request_epoch = value;
                break;
            case CraftingRequestRoute.Availability:
                availability_request_epoch = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    private ICraftingOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Crafting operations are unavailable until the application runtime is active.");

    private static CraftingState InitialState() => new(
        null,
        0,
        0,
        0,
        null,
        0,
        null,
        0,
        null,
        0,
        null);

    private static CraftableProducts FreezeProducts(CraftableProducts value) => new(
        ReadOnly(value.Products.Select(product => product with { })),
        ReadOnly(value.UsableInventoryFurnitureClasses));

    private static CraftingRecipe FreezeRecipe(CraftingRecipe value) => new(
        ReadOnly(value.Ingredients.Select(ingredient => ingredient with { })));

    private static CraftingResult FreezeResult(CraftingResult value) => new(
        value.Success,
        value.Product is { } product ? product with { } : null);

    private static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private static void ThrowFailures(Exception? first, Exception? second)
    {
        if (first is not null && second is not null)
            throw new AggregateException(first, second);
        if (first is not null)
            ExceptionDispatchInfo.Capture(first).Throw();
        if (second is not null)
            ExceptionDispatchInfo.Capture(second).Throw();
    }
}
