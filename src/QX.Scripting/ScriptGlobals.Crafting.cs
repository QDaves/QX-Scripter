using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the craftable products the server sent last, or <see langword="null"/> when none have
    /// been received in this session.
    /// </summary>
    public CraftableProducts? CraftableProducts => Crafting.Products;

    /// <summary>
    /// Gets the recipe ingredients the server sent last, or <see langword="null"/> when none have
    /// been received in this session.
    /// </summary>
    public CraftingRecipe? CurrentCraftingRecipe => Crafting.Recipe;

    /// <summary>
    /// Gets the result of the last craft, or <see langword="null"/> when none has been received in
    /// this session.
    /// </summary>
    public CraftingResult? LastCraftingResult => Crafting.LastResult;

    /// <summary>
    /// Gets the recipe availability the server sent last for a set of ingredients, or
    /// <see langword="null"/> when none has been received in this session.
    /// </summary>
    public CraftingRecipesAvailable? AvailableCraftingRecipes =>
        Crafting.AvailableRecipes;

    /// <summary>
    /// Requests the products that can be crafted with a crafting furniture.
    /// </summary>
    /// <remarks>
    /// It returns without waiting. The response updates <see cref="CraftableProducts"/> and runs
    /// the <see cref="OnCraftableProducts(Action{CraftableProducts})"/> handlers.
    /// </remarks>
    /// <param name="craftingFurnitureId">The floor item id of the crafting furniture in the room.</param>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    public void RequestCraftableProducts(Id craftingFurnitureId) =>
        Crafting.RequestProducts(craftingFurnitureId);

    /// <summary>
    /// Requests the ingredients of a recipe.
    /// </summary>
    /// <remarks>
    /// It returns without waiting. The response updates <see cref="CurrentCraftingRecipe"/> and
    /// runs the <see cref="OnCraftingRecipe(Action{CraftingRecipe})"/> handlers.
    /// </remarks>
    /// <param name="recipeCode">The recipe code taken from a craftable products entry.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="recipeCode"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    public void RequestCraftingRecipe(string recipeCode) =>
        Crafting.RequestRecipe(recipeCode);

    /// <summary>
    /// Crafts a recipe with a crafting furniture.
    /// </summary>
    /// <remarks>
    /// It returns without waiting. The response updates <see cref="LastCraftingResult"/> and runs
    /// the <see cref="OnCraftingResult(Action{CraftingResult})"/> handlers.
    /// </remarks>
    /// <param name="craftingFurnitureId">The floor item id of the crafting furniture.</param>
    /// <param name="recipeCode">The recipe code to craft.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="recipeCode"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    public void Craft(Id craftingFurnitureId, string recipeCode) =>
        Crafting.Craft(craftingFurnitureId, recipeCode);

    /// <summary>
    /// Crafts a secret recipe from a set of ingredient items.
    /// </summary>
    /// <remarks>
    /// It returns without waiting. The response updates <see cref="LastCraftingResult"/> and runs
    /// the <see cref="OnCraftingResult(Action{CraftingResult})"/> handlers.
    /// </remarks>
    /// <param name="craftingFurnitureId">The floor item id of the crafting furniture.</param>
    /// <param name="ingredientItemIds">The inventory item ids to consume.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ingredientItemIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    public void CraftSecret(
        Id craftingFurnitureId,
        params Id[] ingredientItemIds) =>
        Crafting.CraftSecret(craftingFurnitureId, ingredientItemIds);

    /// <summary>
    /// Requests the number of recipes that match a set of ingredient items.
    /// </summary>
    /// <remarks>
    /// It returns without waiting. The response updates <see cref="AvailableCraftingRecipes"/> and
    /// runs the <see cref="OnCraftingRecipesAvailable(Action{CraftingRecipesAvailable})"/> handlers.
    /// </remarks>
    /// <param name="craftingFurnitureId">The floor item id of the crafting furniture.</param>
    /// <param name="ingredientItemIds">The inventory item ids to check.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ingredientItemIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    public void RequestCraftingRecipesAvailable(
        Id craftingFurnitureId,
        params Id[] ingredientItemIds) =>
        Crafting.RequestAvailableRecipes(
            craftingFurnitureId,
            ingredientItemIds);
}
