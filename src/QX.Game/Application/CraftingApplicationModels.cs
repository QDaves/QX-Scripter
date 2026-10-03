using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>Represents a request for the crafting state view.</summary>
/// <remarks>
/// Used by the <c>crafting.state</c> query. The application keeps the four most recent crafting
/// snapshots of the active hotel session, and a retained snapshot can no longer be read once it is
/// dropped or the session changes.
/// </remarks>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.</param>
public sealed record CraftingStateRequest(
    long? SnapshotRevision = null);

/// <summary>Represents a summary of the craftable products of a crafting furniture.</summary>
/// <param name="ProductCount">The number of products that can be crafted.</param>
/// <param name="UsableInventoryFurnitureClassCount">The number of inventory furniture classes that can be used as ingredients.</param>
public sealed record CraftingProductsSummary(
    int ProductCount,
    int UsableInventoryFurnitureClassCount);

/// <summary>Represents a summary of a crafting recipe.</summary>
/// <param name="IngredientCount">The number of ingredients in the recipe.</param>
public sealed record CraftingRecipeSummary(
    int IngredientCount);

/// <summary>Represents the crafting state read from one snapshot.</summary>
/// <remarks>Returned by the <c>crafting.state</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="Revision">The crafting state revision, which increases with every change.</param>
/// <param name="ProductsRevision">The revision of the craftable products, which increases when they are received or cleared.</param>
/// <param name="RecipeRevision">The revision of the recipe, which increases when a recipe is received or cleared.</param>
/// <param name="ResultRevision">The revision of the crafting result, which increases when a result is received or cleared.</param>
/// <param name="AvailabilityRevision">The revision of the recipe availability, which increases when it is received or cleared.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the view was read from.</param>
/// <param name="Products">The summary of the last craftable products received, or <see langword="null"/> when none have been received.</param>
/// <param name="Recipe">The summary of the last recipe received, or <see langword="null"/> when none has been received.</param>
/// <param name="LastResult">The last crafting result received, or <see langword="null"/> when none has been received.</param>
/// <param name="AvailableRecipes">The last recipe availability received, or <see langword="null"/> when none has been received.</param>
public sealed record CraftingStateView(
    bool Connected,
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

/// <summary>Specifies which collection of the craftable products a page reads.</summary>
public enum CraftingProductsCollection
{
    /// <summary>The products that can be crafted.</summary>
    Products,
    /// <summary>The inventory furniture classes that can be used as ingredients.</summary>
    UsableInventoryFurnitureClasses
}

/// <summary>Represents a request for a page of the craftable products.</summary>
/// <remarks>Used by the <c>crafting.products.list</c> query.</remarks>
/// <param name="Collection">The collection to read.</param>
/// <param name="Offset">The zero-based index of the first row to return.</param>
/// <param name="Limit">The maximum number of rows to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record CraftingProductsPageRequest(
    CraftingProductsCollection Collection = CraftingProductsCollection.Products,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of the craftable products read from one snapshot.</summary>
/// <remarks>
/// Returned by the <c>crafting.products.list</c> query. Only the list selected by
/// <paramref name="Collection"/> holds rows, and the other one is empty.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The crafting state revision, which increases with every change.</param>
/// <param name="ProductsRevision">The revision of the craftable products, which increases when they are received or cleared.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Loaded">Whether craftable products have been received in the current session.</param>
/// <param name="ProductCount">The number of products that can be crafted.</param>
/// <param name="UsableInventoryFurnitureClassCount">The number of inventory furniture classes that can be used as ingredients.</param>
/// <param name="Collection">The collection the page was read from.</param>
/// <param name="Total">The number of rows in the selected collection.</param>
/// <param name="Offset">The zero-based index of the first row in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more rows.</param>
/// <param name="Products">The products in the page when <paramref name="Collection"/> is <see cref="CraftingProductsCollection.Products"/>.</param>
/// <param name="UsableInventoryFurnitureClasses">The furniture class names in the page when <paramref name="Collection"/> is <see cref="CraftingProductsCollection.UsableInventoryFurnitureClasses"/>.</param>
public sealed record CraftingProductsPage(
    bool Connected,
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

/// <summary>Represents a request for a page of the ingredients of the last recipe received.</summary>
/// <remarks>Used by the <c>crafting.recipe.list</c> query.</remarks>
/// <param name="Offset">The zero-based index of the first ingredient to return.</param>
/// <param name="Limit">The maximum number of ingredients to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record CraftingRecipePageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of the ingredients of the last recipe received, read from one snapshot.</summary>
/// <remarks>Returned by the <c>crafting.recipe.list</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The crafting state revision, which increases with every change.</param>
/// <param name="RecipeRevision">The revision of the recipe, which increases when a recipe is received or cleared.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Loaded">Whether a recipe has been received in the current session.</param>
/// <param name="Total">The number of ingredients in the recipe.</param>
/// <param name="Offset">The zero-based index of the first ingredient in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more ingredients.</param>
/// <param name="Ingredients">The ingredients in the page.</param>
public sealed record CraftingRecipePage(
    bool Connected,
    long SessionGeneration,
    long StateRevision,
    long RecipeRevision,
    long SnapshotRevision,
    bool Loaded,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<CraftingIngredient> Ingredients);

/// <summary>Represents a request for the products a crafting furniture can craft.</summary>
/// <remarks>
/// Used by the <c>crafting.products.refresh</c> operation, which requires a ready room and the Flash
/// client. Each call sends its own request and accepts the first matching response that arrives
/// after it, as long as no newer products request has been sent in the meantime.
/// </remarks>
/// <param name="CraftingFurnitureId">The id of the crafting furniture, a positive 32-bit value.</param>
/// <param name="Limit">The maximum number of products in the first page of the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedRoomGeneration">The room generation the call must run in, or <see langword="null"/> to use the ready room.</param>
public sealed record CraftingProductsRefreshRequest(
    Id CraftingFurnitureId,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>Represents the result of a craftable products refresh.</summary>
/// <remarks>Returned by the <c>crafting.products.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the response was observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="RoomId">The id of the room the refresh ran in.</param>
/// <param name="RoomGeneration">The generation of the room session the refresh ran in.</param>
/// <param name="RoomRevision">The room state revision when the request was prepared.</param>
/// <param name="StateRevision">The crafting state revision after the response was received.</param>
/// <param name="ProductsRevision">The revision of the craftable products that were received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading further pages.</param>
/// <param name="RequestedCraftingFurnitureId">The crafting furniture id that was sent. The response does not echo it.</param>
/// <param name="MessagesDispatched">The number of request messages the call sent.</param>
/// <param name="FirstPage">The first page of products from the refreshed snapshot.</param>
public sealed record CraftingProductsRefreshResult(
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

/// <summary>Represents a request for the ingredients of a crafting recipe.</summary>
/// <remarks>
/// Used by the <c>crafting.recipe.refresh</c> operation, which requires a ready room and the Flash
/// client. Each call sends its own request and accepts the first matching response that arrives
/// after it, as long as no newer recipe request has been sent in the meantime.
/// </remarks>
/// <param name="RecipeCode">The recipe code, which must not be blank and must fit in 65535 UTF-8 bytes.</param>
/// <param name="Limit">The maximum number of ingredients in the first page of the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedRoomGeneration">The room generation the call must run in, or <see langword="null"/> to use the ready room.</param>
public sealed record CraftingRecipeRefreshRequest(
    string RecipeCode,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>Represents the result of a crafting recipe refresh.</summary>
/// <remarks>Returned by the <c>crafting.recipe.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the response was observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="RoomId">The id of the room the refresh ran in.</param>
/// <param name="RoomGeneration">The generation of the room session the refresh ran in.</param>
/// <param name="RoomRevision">The room state revision when the request was prepared.</param>
/// <param name="StateRevision">The crafting state revision after the response was received.</param>
/// <param name="RecipeRevision">The revision of the recipe that was received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading further pages.</param>
/// <param name="RequestedRecipeCode">The recipe code that was sent. The response does not echo it.</param>
/// <param name="MessagesDispatched">The number of request messages the call sent.</param>
/// <param name="FirstPage">The first page of ingredients from the refreshed snapshot.</param>
public sealed record CraftingRecipeRefreshResult(
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

/// <summary>Represents a request for the recipes that a set of ingredient items can craft.</summary>
/// <remarks>
/// Used by the <c>crafting.availability.refresh</c> operation, which requires a ready room. Each call
/// sends its own request and accepts the first matching response that arrives after it, as long as no
/// newer availability request has been sent in the meantime.
/// </remarks>
/// <param name="CraftingFurnitureId">The id of the crafting furniture, a positive 32-bit value.</param>
/// <param name="IngredientItemIds">The ids of the inventory items to check, at most 65535 positive 32-bit values.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedRoomGeneration">The room generation the call must run in, or <see langword="null"/> to use the ready room.</param>
public sealed record CraftingAvailabilityRefreshRequest(
    Id CraftingFurnitureId,
    IReadOnlyList<Id> IngredientItemIds,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>Represents the result of a crafting availability refresh.</summary>
/// <remarks>Returned by the <c>crafting.availability.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the response was observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="RoomId">The id of the room the refresh ran in.</param>
/// <param name="RoomGeneration">The generation of the room session the refresh ran in.</param>
/// <param name="RoomRevision">The room state revision when the request was prepared.</param>
/// <param name="StateRevision">The crafting state revision after the response was received.</param>
/// <param name="AvailabilityRevision">The revision of the recipe availability that was received.</param>
/// <param name="RequestedCraftingFurnitureId">The crafting furniture id that was sent. The response does not echo it.</param>
/// <param name="RequestedIngredientCount">The number of ingredient item ids that were sent.</param>
/// <param name="MessagesDispatched">The number of request messages the call sent.</param>
/// <param name="AvailableRecipes">The recipe availability the server returned.</param>
public sealed record CraftingAvailabilityRefreshResult(
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

/// <summary>Represents a request to craft a known recipe.</summary>
/// <remarks>
/// Used by the <c>crafting.craft</c> operation, which requires a ready room.
/// The operation sends the request and returns without waiting for the crafting result, which
/// arrives later as a <see cref="CraftingChanged"/> of kind <see cref="CraftingChangeKind.Result"/>.
/// </remarks>
/// <param name="CraftingFurnitureId">The id of the crafting furniture, a positive 32-bit value.</param>
/// <param name="RecipeCode">The recipe code, which must not be blank and must fit in 65535 UTF-8 bytes.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedRoomGeneration">The room generation the call must run in, or <see langword="null"/> to use the ready room.</param>
public sealed record CraftingCraftRequest(
    Id CraftingFurnitureId,
    string RecipeCode,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>Represents the receipt for a craft request that was sent.</summary>
/// <remarks>Returned by the <c>crafting.craft</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the request was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the request was sent in.</param>
/// <param name="RoomId">The id of the room the request was sent in.</param>
/// <param name="RoomGeneration">The generation of the room session the request was sent in.</param>
/// <param name="RoomRevision">The room state revision when the request was prepared.</param>
/// <param name="CraftingFurnitureId">The id of the crafting furniture.</param>
/// <param name="RecipeCode">The recipe code that was sent.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record CraftingCraftDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    Id CraftingFurnitureId,
    string RecipeCode,
    int MessagesDispatched);

/// <summary>Represents a request to craft a secret recipe from a set of ingredient items.</summary>
/// <remarks>
/// Used by the <c>crafting.secret_craft</c> operation, which requires a ready room and the Flash
/// client. The operation sends the request and returns without waiting for the crafting result,
/// which arrives later as a <see cref="CraftingChanged"/> of kind <see cref="CraftingChangeKind.Result"/>.
/// </remarks>
/// <param name="CraftingFurnitureId">The id of the crafting furniture, a positive 32-bit value.</param>
/// <param name="IngredientItemIds">The ids of the inventory items to craft with, at most 65535 positive 32-bit values.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
/// <param name="ExpectedRoomGeneration">The room generation the call must run in, or <see langword="null"/> to use the ready room.</param>
public sealed record CraftingSecretCraftRequest(
    Id CraftingFurnitureId,
    IReadOnlyList<Id> IngredientItemIds,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>Represents the receipt for a secret craft request that was sent.</summary>
/// <remarks>Returned by the <c>crafting.secret_craft</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the request was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the request was sent in.</param>
/// <param name="RoomId">The id of the room the request was sent in.</param>
/// <param name="RoomGeneration">The generation of the room session the request was sent in.</param>
/// <param name="RoomRevision">The room state revision when the request was prepared.</param>
/// <param name="CraftingFurnitureId">The id of the crafting furniture.</param>
/// <param name="IngredientItemCount">The number of ingredient item ids that were sent.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record CraftingSecretCraftDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    Id CraftingFurnitureId,
    int IngredientItemCount,
    int MessagesDispatched);

/// <summary>Specifies the kind of a crafting change.</summary>
public enum CraftingChangeKind
{
    /// <summary>The craftable products of a crafting furniture were received.</summary>
    Products,
    /// <summary>The ingredients of a recipe were received.</summary>
    Recipe,
    /// <summary>A crafting result was received.</summary>
    Result,
    /// <summary>The recipe availability for a set of ingredients was received.</summary>
    Availability,
    /// <summary>The crafting state was cleared for a new or closed hotel session.</summary>
    Reset
}

/// <summary>Represents a change to the crafting state.</summary>
/// <remarks>
/// Published by the <c>crafting.changed</c> event. Crafting results are not matched to the craft
/// request that caused them.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session the change belongs to.</param>
/// <param name="Revision">The crafting state revision after the change.</param>
/// <param name="SourceRevision">The revision of the part that changed, or <paramref name="Revision"/> for <see cref="CraftingChangeKind.Reset"/>.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot for <see cref="CraftingChangeKind.Products"/> and <see cref="CraftingChangeKind.Recipe"/>; otherwise, <see langword="null"/>.</param>
/// <param name="Products">The summary of the received products for <see cref="CraftingChangeKind.Products"/>; otherwise, <see langword="null"/>.</param>
/// <param name="Recipe">The summary of the received recipe for <see cref="CraftingChangeKind.Recipe"/>; otherwise, <see langword="null"/>.</param>
/// <param name="Result">The crafting result for <see cref="CraftingChangeKind.Result"/>; otherwise, <see langword="null"/>.</param>
/// <param name="Availability">The recipe availability for <see cref="CraftingChangeKind.Availability"/>; otherwise, <see langword="null"/>.</param>
public sealed record CraftingChanged(
    CraftingChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
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
