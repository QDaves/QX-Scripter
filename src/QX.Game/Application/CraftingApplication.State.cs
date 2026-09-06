using Qx.Model.Crafting;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class CraftingApplication
{
    public CraftingStateView ReadState(CraftingStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        if (request.SnapshotRevision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.SnapshotRevision));
        CraftingSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        CraftingStateView result = StateView(lease);
        RequireLeaseActive(lease);
        return result;
    }

    public CraftingProductsPage ReadProducts(CraftingProductsPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        if (!Enum.IsDefined(request.Collection))
            throw new ArgumentOutOfRangeException(nameof(request.Collection));
        CraftingSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        CraftingProductsPage result = ProductsPage(
            lease,
            request.Collection,
            request.Offset,
            request.Limit);
        RequireLeaseActive(lease);
        return result;
    }

    public CraftingRecipePage ReadRecipe(CraftingRecipePageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        CraftingSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        CraftingRecipePage result = RecipePage(
            lease,
            request.Offset,
            request.Limit);
        RequireLeaseActive(lease);
        return result;
    }

    private CraftingStateView StateView(CraftingSnapshotLease lease)
    {
        CraftingState state = lease.State;
        bool connected = Connected(state);
        return new CraftingStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.ProductsRevision,
            state.RecipeRevision,
            state.ResultRevision,
            state.AvailabilityRevision,
            lease.Revision,
            state.Products is { } products ? ProductsSummary(products) : null,
            state.Recipe is { } recipe ? RecipeSummary(recipe) : null,
            state.LastResult,
            state.AvailableRecipes);
    }

    private CraftingProductsPage ProductsPage(
        CraftingSnapshotLease lease,
        CraftingProductsCollection collection,
        int offset,
        int limit)
    {
        CraftingState state = lease.State;
        CraftableProducts? snapshot = state.Products;
        IReadOnlyList<CraftingProduct> products = Array.Empty<CraftingProduct>();
        IReadOnlyList<string> furniture_classes = Array.Empty<string>();
        int product_count = snapshot?.Products.Count ?? 0;
        int furniture_class_count =
            snapshot?.UsableInventoryFurnitureClasses.Count ?? 0;
        int total;
        int returned;
        switch (collection)
        {
            case CraftingProductsCollection.Products:
                products = snapshot is null
                    ? Array.Empty<CraftingProduct>()
                    : Slice(snapshot.Products, offset, limit);
                total = product_count;
                returned = products.Count;
                break;
            case CraftingProductsCollection.UsableInventoryFurnitureClasses:
                furniture_classes = snapshot is null
                    ? Array.Empty<string>()
                    : Slice(
                        snapshot.UsableInventoryFurnitureClasses,
                        offset,
                        limit);
                total = furniture_class_count;
                returned = furniture_classes.Count;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(collection));
        }
        bool connected = Connected(state);
        return new CraftingProductsPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.ProductsRevision,
            lease.Revision,
            snapshot is not null,
            product_count,
            furniture_class_count,
            collection,
            total,
            offset,
            NextOffset(offset, returned, total),
            products,
            furniture_classes);
    }

    private CraftingRecipePage RecipePage(
        CraftingSnapshotLease lease,
        int offset,
        int limit)
    {
        CraftingState state = lease.State;
        CraftingRecipe? snapshot = state.Recipe;
        int total = snapshot?.Ingredients.Count ?? 0;
        IReadOnlyList<CraftingIngredient> ingredients = snapshot is null
            ? Array.Empty<CraftingIngredient>()
            : Slice(snapshot.Ingredients, offset, limit);
        bool connected = Connected(state);
        return new CraftingRecipePage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.RecipeRevision,
            lease.Revision,
            snapshot is not null,
            total,
            offset,
            NextOffset(offset, ingredients.Count, total),
            ingredients);
    }

    private bool Connected(CraftingState state) =>
        state.Session is not null &&
        ReferenceEquals(connection.Session, state.Session);

    private void RequireLeaseActive(CraftingSnapshotLease lease)
    {
        if (!LeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the crafting snapshot was being read.");
        }
    }

    private static CraftingProductsSummary ProductsSummary(
        CraftableProducts value) => new(
        value.Products.Count,
        value.UsableInventoryFurnitureClasses.Count);

    private static CraftingRecipeSummary RecipeSummary(CraftingRecipe value) => new(
        value.Ingredients.Count);

    private static IReadOnlyList<T> Slice<T>(
        IReadOnlyList<T> values,
        int offset,
        int limit)
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
}
