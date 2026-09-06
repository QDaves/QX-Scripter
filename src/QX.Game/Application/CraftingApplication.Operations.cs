using Qx.Game.Protocol;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class CraftingApplication
{
    private const int heavy_commit_history_limit = 2;
    private const int scalar_commit_history_limit = 32;
    private readonly object refresh_sync = new();
    private readonly List<ObservedCraftingCommit> products_commits = [];
    private readonly List<ObservedCraftingCommit> recipe_commits = [];
    private readonly List<ObservedCraftingCommit> result_commits = [];
    private readonly List<ObservedCraftingCommit> availability_commits = [];

    public ValueTask<CraftingProductsRefreshResult> RefreshProducts(
        CraftingProductsRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => RefreshProductsCore(request, token));

    private async ValueTask<CraftingProductsRefreshResult> RefreshProductsCore(
        CraftingProductsRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        CraftingRoomScope scope = CaptureRoomScope(
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        ValidateTypedId(
            request.CraftingFurnitureId,
            scope.Session.Client,
            nameof(request.CraftingFurnitureId));
        var await_state = new RouteAwaitState();
        await requests.RequestAsync(
            MessageContracts.Crafting.ProductsRequest,
            new GetCraftableProducts(request.CraftingFurnitureId),
            MessageContracts.Crafting.ProductsSnapshot,
            scope.Session,
            match: response => MatchProducts(await_state, scope, response),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => Arm(
                await_state,
                scope,
                CraftingRequestRoute.Products,
                static state => state.ProductsRevision),
            attempt_start: () => Prepare(
                await_state,
                scope,
                CraftingRequestRoute.Products)).ConfigureAwait(false);
        RequireResponseScope(scope);
        ObservedCraftingCommit observed = Accepted(await_state, "craftable-products");
        CraftingSnapshotLease lease = StoreStateLease(observed.Update.State);
        try
        {
            CraftingProductsPage first_page = ProductsPage(
                lease,
                CraftingProductsCollection.Products,
                0,
                request.Limit);
            var result = new CraftingProductsRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                scope.RoomId,
                scope.RoomGeneration,
                scope.RoomRevision,
                observed.Update.State.Revision,
                observed.Update.State.ProductsRevision,
                lease.Revision,
                request.CraftingFurnitureId,
                1,
                first_page);
            RequireResponseScope(scope);
            return result;
        }
        catch
        {
            RemoveLease(lease.Revision);
            throw;
        }
    }

    public ValueTask<CraftingRecipeRefreshResult> RefreshRecipe(
        CraftingRecipeRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => RefreshRecipeCore(request, token));

    private async ValueTask<CraftingRecipeRefreshResult> RefreshRecipeCore(
        CraftingRecipeRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTypedRecipeCode(request.RecipeCode, nameof(request.RecipeCode));
        ValidatePageLimit(request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        CraftingRoomScope scope = CaptureRoomScope(
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        var await_state = new RouteAwaitState();
        await requests.RequestAsync(
            MessageContracts.Crafting.RecipeRequest,
            new GetCraftingRecipe(request.RecipeCode),
            MessageContracts.Crafting.RecipeSnapshot,
            scope.Session,
            match: response => MatchRecipe(await_state, scope, response),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => Arm(
                await_state,
                scope,
                CraftingRequestRoute.Recipe,
                static state => state.RecipeRevision),
            attempt_start: () => Prepare(
                await_state,
                scope,
                CraftingRequestRoute.Recipe)).ConfigureAwait(false);
        RequireResponseScope(scope);
        ObservedCraftingCommit observed = Accepted(await_state, "crafting-recipe");
        CraftingSnapshotLease lease = StoreStateLease(observed.Update.State);
        try
        {
            CraftingRecipePage first_page = RecipePage(lease, 0, request.Limit);
            var result = new CraftingRecipeRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                scope.RoomId,
                scope.RoomGeneration,
                scope.RoomRevision,
                observed.Update.State.Revision,
                observed.Update.State.RecipeRevision,
                lease.Revision,
                request.RecipeCode,
                1,
                first_page);
            RequireResponseScope(scope);
            return result;
        }
        catch
        {
            RemoveLease(lease.Revision);
            throw;
        }
    }

    public ValueTask<CraftingAvailabilityRefreshResult> RefreshAvailability(
        CraftingAvailabilityRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => RefreshAvailabilityCore(request, token));

    private async ValueTask<CraftingAvailabilityRefreshResult>
        RefreshAvailabilityCore(
            CraftingAvailabilityRefreshRequest request,
            CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        CraftingRoomScope scope = CaptureRoomScope(
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        ValidateTypedId(
            request.CraftingFurnitureId,
            scope.Session.Client,
            nameof(request.CraftingFurnitureId));
        ArgumentNullException.ThrowIfNull(request.IngredientItemIds);
        Id[] ingredient_item_ids = request.IngredientItemIds.ToArray();
        ValidateTypedItems(
            ingredient_item_ids,
            scope.Session.Client,
            nameof(request.IngredientItemIds));
        var await_state = new RouteAwaitState();
        await requests.RequestAsync(
            MessageContracts.Crafting.AvailabilityRequest,
            new GetCraftingRecipesAvailable(
                request.CraftingFurnitureId,
                ingredient_item_ids),
            MessageContracts.Crafting.AvailabilitySnapshot,
            scope.Session,
            match: response => MatchAvailability(await_state, scope, response),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => Arm(
                await_state,
                scope,
                CraftingRequestRoute.Availability,
                static state => state.AvailabilityRevision),
            attempt_start: () => Prepare(
                await_state,
                scope,
                CraftingRequestRoute.Availability)).ConfigureAwait(false);
        RequireResponseScope(scope);
        ObservedCraftingCommit observed = Accepted(
            await_state,
            "crafting-availability");
        var result = new CraftingAvailabilityRefreshResult(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            observed.ObservedAtUtc,
            scope.SessionGeneration,
            scope.RoomId,
            scope.RoomGeneration,
            scope.RoomRevision,
            observed.Update.State.Revision,
            observed.Update.State.AvailabilityRevision,
            request.CraftingFurnitureId,
            ingredient_item_ids.Length,
            1,
            (CraftingRecipesAvailable)observed.Update.Value!);
        RequireResponseScope(scope);
        return result;
    }

    public ValueTask<CraftingCraftDispatchReceipt> CraftRecipe(
        CraftingCraftRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(CraftRecipeCore(request, token)));

    private CraftingCraftDispatchReceipt CraftRecipeCore(
        CraftingCraftRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTypedRecipeCode(request.RecipeCode, nameof(request.RecipeCode));
        CraftingRoomScope scope = CaptureRoomScope(
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        ValidateTypedId(
            request.CraftingFurnitureId,
            scope.Session.Client,
            nameof(request.CraftingFurnitureId));
        long request_baseline = CaptureRequestEpoch(
            CraftingRequestRoute.Result,
            scope);
        message_dispatcher.Dispatch(
            MessageContracts.Crafting.Craft,
            new Qx.Model.Messages.Incoming.Craft(
                request.CraftingFurnitureId,
                request.RecipeCode),
            scope.Session,
            cancellation_token,
            () => AdvanceRequestEpoch(
                CraftingRequestRoute.Result,
                request_baseline,
                scope));
        return new CraftingCraftDispatchReceipt(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            scope.RoomId,
            scope.RoomGeneration,
            scope.RoomRevision,
            request.CraftingFurnitureId,
            request.RecipeCode,
            1);
    }

    public ValueTask<CraftingSecretCraftDispatchReceipt> CraftSecret(
        CraftingSecretCraftRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(CraftSecretCore(request, token)));

    private CraftingSecretCraftDispatchReceipt CraftSecretCore(
        CraftingSecretCraftRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        CraftingRoomScope scope = CaptureRoomScope(
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        ValidateTypedId(
            request.CraftingFurnitureId,
            scope.Session.Client,
            nameof(request.CraftingFurnitureId));
        ArgumentNullException.ThrowIfNull(request.IngredientItemIds);
        Id[] ingredient_item_ids = request.IngredientItemIds.ToArray();
        ValidateTypedItems(
            ingredient_item_ids,
            scope.Session.Client,
            nameof(request.IngredientItemIds));
        long request_baseline = CaptureRequestEpoch(
            CraftingRequestRoute.Result,
            scope);
        message_dispatcher.Dispatch(
            MessageContracts.Crafting.SecretCraft,
            new Qx.Model.Messages.Incoming.CraftSecret(
                request.CraftingFurnitureId,
                ingredient_item_ids),
            scope.Session,
            cancellation_token,
            () => AdvanceRequestEpoch(
                CraftingRequestRoute.Result,
                request_baseline,
                scope));
        return new CraftingSecretCraftDispatchReceipt(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            scope.RoomId,
            scope.RoomGeneration,
            scope.RoomRevision,
            request.CraftingFurnitureId,
            ingredient_item_ids.Length,
            1);
    }

    void ICraftingOperations.RequestProducts(Id crafting_furniture_id)
    {
        InvokeLegacy(cancellation_token =>
        {
            CraftingRoomScope scope = CaptureRoomScope(
                null,
                null,
                cancellation_token);
            long request_baseline = CaptureRequestEpoch(
                CraftingRequestRoute.Products,
                scope);
            message_dispatcher.Dispatch(
                MessageContracts.Crafting.ProductsRequest,
                new GetCraftableProducts(crafting_furniture_id),
                scope.Session,
                cancellation_token,
                () => AdvanceRequestEpoch(
                    CraftingRequestRoute.Products,
                    request_baseline,
                    scope));
        });
    }

    void ICraftingOperations.RequestRecipe(string recipe_code)
    {
        InvokeLegacy(cancellation_token =>
        {
            ValidateWireString(recipe_code, nameof(recipe_code));
            CraftingRoomScope scope = CaptureRoomScope(
                null,
                null,
                cancellation_token);
            long request_baseline = CaptureRequestEpoch(
                CraftingRequestRoute.Recipe,
                scope);
            message_dispatcher.Dispatch(
                MessageContracts.Crafting.RecipeRequest,
                new GetCraftingRecipe(recipe_code),
                scope.Session,
                cancellation_token,
                () => AdvanceRequestEpoch(
                    CraftingRequestRoute.Recipe,
                    request_baseline,
                    scope));
        });
    }

    void ICraftingOperations.Craft(Id crafting_furniture_id, string recipe_code)
    {
        InvokeLegacy(cancellation_token =>
        {
            ValidateWireString(recipe_code, nameof(recipe_code));
            CraftingRoomScope scope = CaptureRoomScope(
                null,
                null,
                cancellation_token);
            long request_baseline = CaptureRequestEpoch(
                CraftingRequestRoute.Result,
                scope);
            message_dispatcher.Dispatch(
                MessageContracts.Crafting.Craft,
                new Qx.Model.Messages.Incoming.Craft(
                    crafting_furniture_id,
                    recipe_code),
                scope.Session,
                cancellation_token,
                () => AdvanceRequestEpoch(
                    CraftingRequestRoute.Result,
                    request_baseline,
                    scope));
        });
    }

    void ICraftingOperations.CraftSecret(
        Id crafting_furniture_id,
        IReadOnlyList<Id> ingredient_item_ids)
    {
        InvokeLegacy(cancellation_token =>
        {
            ArgumentNullException.ThrowIfNull(ingredient_item_ids);
            Id[] item_ids = ingredient_item_ids.ToArray();
            CraftingRoomScope scope = CaptureRoomScope(
                null,
                null,
                cancellation_token);
            long request_baseline = CaptureRequestEpoch(
                CraftingRequestRoute.Result,
                scope);
            message_dispatcher.Dispatch(
                MessageContracts.Crafting.SecretCraft,
                new Qx.Model.Messages.Incoming.CraftSecret(
                    crafting_furniture_id,
                    item_ids),
                scope.Session,
                cancellation_token,
                () => AdvanceRequestEpoch(
                    CraftingRequestRoute.Result,
                    request_baseline,
                    scope));
        });
    }

    void ICraftingOperations.RequestAvailableRecipes(
        Id crafting_furniture_id,
        IReadOnlyList<Id> ingredient_item_ids)
    {
        InvokeLegacy(cancellation_token =>
        {
            ArgumentNullException.ThrowIfNull(ingredient_item_ids);
            Id[] item_ids = ingredient_item_ids.ToArray();
            CraftingRoomScope scope = CaptureRoomScope(
                null,
                null,
                cancellation_token);
            long request_baseline = CaptureRequestEpoch(
                CraftingRequestRoute.Availability,
                scope);
            message_dispatcher.Dispatch(
                MessageContracts.Crafting.AvailabilityRequest,
                new GetCraftingRecipesAvailable(
                    crafting_furniture_id,
                    item_ids),
                scope.Session,
                cancellation_token,
                () => AdvanceRequestEpoch(
                    CraftingRequestRoute.Availability,
                    request_baseline,
                    scope));
        });
    }

    private void Arm(
        RouteAwaitState await_state,
        CraftingRoomScope scope,
        CraftingRequestRoute route,
        Func<CraftingState, long> source_revision)
    {
        long request_baseline;
        lock (refresh_sync)
            request_baseline = await_state.RequestBaseline;
        if (request_baseline < 0)
        {
            throw new InvalidOperationException(
                "The crafting request epoch was not prepared before dispatch.");
        }
        long expected_request_epoch = AdvanceRequestEpoch(
            route,
            request_baseline,
            scope);
        lock (refresh_sync)
        {
            CraftingState state = crafting.State;
            if (!ReferenceEquals(state.Session, scope.Session) ||
                state.SessionGeneration != scope.SessionGeneration)
            {
                throw new InvalidOperationException(
                    "The hotel session changed while the crafting response was armed.");
            }
            await_state.SourceBaseline = source_revision(state);
            await_state.ExpectedRequestEpoch = expected_request_epoch;
            await_state.Accepted = null;
            await_state.Armed = true;
        }
    }

    private void Prepare(
        RouteAwaitState await_state,
        CraftingRoomScope scope,
        CraftingRequestRoute route)
    {
        long request_baseline = CaptureRequestEpoch(route, scope);
        lock (refresh_sync)
        {
            await_state.RequestBaseline = request_baseline;
            await_state.SourceBaseline = -1;
            await_state.ExpectedRequestEpoch = -1;
            await_state.Accepted = null;
            await_state.Armed = false;
        }
    }

    private bool MatchProducts(
        RouteAwaitState await_state,
        CraftingRoomScope scope,
        CraftableProducts response)
    {
        lock (refresh_sync)
        {
            if (!AwaitStateCurrent(
                    await_state,
                    scope,
                    CraftingRequestRoute.Products))
            {
                return false;
            }
            ObservedCraftingCommit? accepted = FindCommit(
                products_commits,
                await_state,
                scope,
                static update => update.State.ProductsRevision,
                update => update.Value is CraftableProducts value &&
                    ProductsEqual(value, response));
            if (accepted is null)
                return false;
            await_state.Accepted = accepted;
            await_state.Armed = false;
            return true;
        }
    }

    private bool MatchRecipe(
        RouteAwaitState await_state,
        CraftingRoomScope scope,
        CraftingRecipe response)
    {
        lock (refresh_sync)
        {
            if (!AwaitStateCurrent(
                    await_state,
                    scope,
                    CraftingRequestRoute.Recipe))
            {
                return false;
            }
            ObservedCraftingCommit? accepted = FindCommit(
                recipe_commits,
                await_state,
                scope,
                static update => update.State.RecipeRevision,
                update => update.Value is CraftingRecipe value &&
                    value.Ingredients.SequenceEqual(response.Ingredients));
            if (accepted is null)
                return false;
            await_state.Accepted = accepted;
            await_state.Armed = false;
            return true;
        }
    }

    private bool MatchAvailability(
        RouteAwaitState await_state,
        CraftingRoomScope scope,
        CraftingRecipesAvailable response)
    {
        lock (refresh_sync)
        {
            if (!AwaitStateCurrent(
                    await_state,
                    scope,
                    CraftingRequestRoute.Availability))
            {
                return false;
            }
            ObservedCraftingCommit? accepted = FindCommit(
                availability_commits,
                await_state,
                scope,
                static update => update.State.AvailabilityRevision,
                update => update.Value is CraftingRecipesAvailable value &&
                    value == response);
            if (accepted is null)
                return false;
            await_state.Accepted = accepted;
            await_state.Armed = false;
            return true;
        }
    }

    private bool AwaitStateCurrent(
        RouteAwaitState await_state,
        CraftingRoomScope scope,
        CraftingRequestRoute route) =>
        await_state.Armed &&
        await_state.Accepted is null &&
        ResponseScopeActive(scope) &&
        crafting.RequestEpochIsCurrent(
            route,
            await_state.ExpectedRequestEpoch,
            scope.Session,
            scope.SessionGeneration);

    private static ObservedCraftingCommit? FindCommit(
        IReadOnlyList<ObservedCraftingCommit> commits,
        RouteAwaitState await_state,
        CraftingRoomScope scope,
        Func<CraftingStateUpdate, long> source_revision,
        Func<CraftingStateUpdate, bool> matches)
    {
        for (int index = 0; index < commits.Count; index++)
        {
            ObservedCraftingCommit commit = commits[index];
            CraftingStateUpdate update = commit.Update;
            if (update.RequestEpoch == await_state.ExpectedRequestEpoch &&
                source_revision(update) > await_state.SourceBaseline &&
                ReferenceEquals(update.State.Session, scope.Session) &&
                update.State.SessionGeneration == scope.SessionGeneration &&
                matches(update))
            {
                return commit;
            }
        }
        return null;
    }

    private ObservedCraftingCommit Accepted(
        RouteAwaitState await_state,
        string route_name)
    {
        lock (refresh_sync)
        {
            return await_state.Accepted ??
                throw new InvalidOperationException(
                    $"The accepted {route_name} response was not committed by the crafting state owner.");
        }
    }

    private void ObserveCommit(CraftingStateUpdate update)
    {
        Invocation invocation;
        try
        {
            invocation = EnterInvocation();
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        using (invocation)
        {
            DateTimeOffset observed_at = time_provider.GetUtcNow();
            lock (refresh_sync)
            {
                if (DisposalStarted())
                    return;
                switch (update.Kind)
                {
                    case CraftingStateChangeKind.Products:
                        AddCommit(
                            products_commits,
                            new ObservedCraftingCommit(
                                Sanitize(update, CraftingStateChangeKind.Products),
                                observed_at),
                            heavy_commit_history_limit);
                        break;
                    case CraftingStateChangeKind.Recipe:
                        AddCommit(
                            recipe_commits,
                            new ObservedCraftingCommit(
                                Sanitize(update, CraftingStateChangeKind.Recipe),
                                observed_at),
                            heavy_commit_history_limit);
                        break;
                    case CraftingStateChangeKind.Result:
                        AddCommit(
                            result_commits,
                            new ObservedCraftingCommit(Sanitize(update, null), observed_at),
                            scalar_commit_history_limit);
                        break;
                    case CraftingStateChangeKind.Availability:
                        AddCommit(
                            availability_commits,
                            new ObservedCraftingCommit(Sanitize(update, null), observed_at),
                            scalar_commit_history_limit);
                        break;
                    case CraftingStateChangeKind.Reset:
                        products_commits.Clear();
                        recipe_commits.Clear();
                        result_commits.Clear();
                        availability_commits.Clear();
                        break;
                }
            }
            if (update.Kind is CraftingStateChangeKind.Reset)
                ClearLeases();
        }
    }

    private void PublishChanged(CraftingStateUpdate update)
    {
        Invocation invocation;
        try
        {
            invocation = EnterInvocation();
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        using (invocation)
        {
            if (!PublicationCurrent(update))
                return;
            long? snapshot_revision = update.Kind is
                CraftingStateChangeKind.Products or
                CraftingStateChangeKind.Recipe
                    ? StoreStateLease(update.State).Revision
                    : null;
            changed.Publish(
                new CraftingChanged(
                    ChangeKind(update.Kind),
                    time_provider.GetUtcNow(),
                    update.State.Session?.Client,
                    update.State.SessionGeneration,
                    update.State.Revision,
                    SourceRevision(update),
                    snapshot_revision,
                    update.Value is CraftableProducts products
                        ? ProductsSummary(products)
                        : null,
                    update.Value is CraftingRecipe recipe
                        ? RecipeSummary(recipe)
                        : null,
                    update.Value as CraftingResult,
                    update.Value as CraftingRecipesAvailable),
                () => PublicationCurrent(update));
        }
    }

    private void ClearRefreshState()
    {
        lock (refresh_sync)
        {
            products_commits.Clear();
            recipe_commits.Clear();
            result_commits.Clear();
            availability_commits.Clear();
        }
    }

    private static CraftingStateUpdate Sanitize(
        CraftingStateUpdate update,
        CraftingStateChangeKind? preserve) => update with
        {
            State = update.State with
            {
                Products = preserve is CraftingStateChangeKind.Products
                ? update.State.Products
                : null,
                Recipe = preserve is CraftingStateChangeKind.Recipe
                ? update.State.Recipe
                : null
            }
        };

    private static void AddCommit(
        List<ObservedCraftingCommit> commits,
        ObservedCraftingCommit commit,
        int limit)
    {
        commits.Add(commit);
        if (commits.Count > limit)
            commits.RemoveAt(0);
    }

    private static CraftingChangeKind ChangeKind(
        CraftingStateChangeKind kind) => kind switch
        {
            CraftingStateChangeKind.Products => CraftingChangeKind.Products,
            CraftingStateChangeKind.Recipe => CraftingChangeKind.Recipe,
            CraftingStateChangeKind.Result => CraftingChangeKind.Result,
            CraftingStateChangeKind.Availability => CraftingChangeKind.Availability,
            CraftingStateChangeKind.Reset => CraftingChangeKind.Reset,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static long SourceRevision(CraftingStateUpdate update) =>
        update.Kind switch
        {
            CraftingStateChangeKind.Products => update.State.ProductsRevision,
            CraftingStateChangeKind.Recipe => update.State.RecipeRevision,
            CraftingStateChangeKind.Result => update.State.ResultRevision,
            CraftingStateChangeKind.Availability =>
                update.State.AvailabilityRevision,
            CraftingStateChangeKind.Reset => update.State.Revision,
            _ => throw new ArgumentOutOfRangeException(nameof(update))
        };

    private static bool ProductsEqual(
        CraftableProducts left,
        CraftableProducts right) =>
        left.Products.SequenceEqual(right.Products) &&
        left.UsableInventoryFurnitureClasses.SequenceEqual(
            right.UsableInventoryFurnitureClasses);

    private sealed class RouteAwaitState
    {
        public long RequestBaseline = -1;
        public long SourceBaseline = -1;
        public long ExpectedRequestEpoch = -1;
        public ObservedCraftingCommit? Accepted;
        public bool Armed;
    }

    private sealed record ObservedCraftingCommit(
        CraftingStateUpdate Update,
        DateTimeOffset ObservedAtUtc);
}
