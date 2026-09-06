using Qx.Model.Crafting;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record CraftingStateRequest(
    long? SnapshotRevision = null);

public sealed record CraftingProductsSummary(
    int ProductCount,
    int UsableInventoryFurnitureClassCount);

public sealed record CraftingRecipeSummary(
    int IngredientCount);

public sealed record CraftingStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long ProductsRevision,
    long RecipeRevision,
    long ResultRevision,
    long AvailabilityRevision,
    long SnapshotRevision,
    CraftingProductsSummary? Products,
    CraftingRecipeSummary? Recipe,
    CraftingResult? LastResult,
    CraftingRecipesAvailable? AvailableRecipes);

public enum CraftingProductsCollection
{
    Products,
    UsableInventoryFurnitureClasses
}

public sealed record CraftingProductsPageRequest(
    CraftingProductsCollection Collection = CraftingProductsCollection.Products,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record CraftingProductsPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long ProductsRevision,
    long SnapshotRevision,
    bool Loaded,
    int ProductCount,
    int UsableInventoryFurnitureClassCount,
    CraftingProductsCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<CraftingProduct> Products,
    IReadOnlyList<string> UsableInventoryFurnitureClasses);

public sealed record CraftingRecipePageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record CraftingRecipePage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long RecipeRevision,
    long SnapshotRevision,
    bool Loaded,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<CraftingIngredient> Ingredients);

public sealed record CraftingProductsRefreshRequest(
    Id CraftingFurnitureId,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record CraftingProductsRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    long StateRevision,
    long ProductsRevision,
    long SnapshotRevision,
    Id RequestedCraftingFurnitureId,
    int MessagesDispatched,
    CraftingProductsPage FirstPage);

public sealed record CraftingRecipeRefreshRequest(
    string RecipeCode,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record CraftingRecipeRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    long StateRevision,
    long RecipeRevision,
    long SnapshotRevision,
    string RequestedRecipeCode,
    int MessagesDispatched,
    CraftingRecipePage FirstPage);

public sealed record CraftingAvailabilityRefreshRequest(
    Id CraftingFurnitureId,
    IReadOnlyList<Id> IngredientItemIds,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record CraftingAvailabilityRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    long StateRevision,
    long AvailabilityRevision,
    Id RequestedCraftingFurnitureId,
    int RequestedIngredientCount,
    int MessagesDispatched,
    CraftingRecipesAvailable AvailableRecipes);

public sealed record CraftingCraftRequest(
    Id CraftingFurnitureId,
    string RecipeCode,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record CraftingCraftDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    Id CraftingFurnitureId,
    string RecipeCode,
    int MessagesDispatched);

public sealed record CraftingSecretCraftRequest(
    Id CraftingFurnitureId,
    IReadOnlyList<Id> IngredientItemIds,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record CraftingSecretCraftDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    Id CraftingFurnitureId,
    int IngredientItemCount,
    int MessagesDispatched);

public enum CraftingChangeKind
{
    Products,
    Recipe,
    Result,
    Availability,
    Reset
}

public sealed record CraftingChanged(
    CraftingChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    CraftingProductsSummary? Products,
    CraftingRecipeSummary? Recipe,
    CraftingResult? Result,
    CraftingRecipesAvailable? Availability);

internal interface ICraftingOperations
{
    void RequestProducts(Id crafting_furniture_id);
    void RequestRecipe(string recipe_code);
    void Craft(Id crafting_furniture_id, string recipe_code);
    void CraftSecret(
        Id crafting_furniture_id,
        IReadOnlyList<Id> ingredient_item_ids);
    void RequestAvailableRecipes(
        Id crafting_furniture_id,
        IReadOnlyList<Id> ingredient_item_ids);
}
