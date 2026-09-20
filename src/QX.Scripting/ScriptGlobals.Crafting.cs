using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    public CraftableProducts? CraftableProducts => Crafting.Products;

    public CraftingRecipe? CurrentCraftingRecipe => Crafting.Recipe;

    public CraftingResult? LastCraftingResult => Crafting.LastResult;

    public CraftingRecipesAvailable? AvailableCraftingRecipes =>
        Crafting.AvailableRecipes;

    /// <param name="crafting_furniture_id">The floor item id of the crafting furniture in the room.</param>
    public void RequestCraftableProducts(Id crafting_furniture_id) =>
        Crafting.RequestProducts(crafting_furniture_id);

    /// <param name="recipe_code">The recipe code taken from a craftable-products entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recipe_code"/> is null.</exception>
    public void RequestCraftingRecipe(string recipe_code) =>
        Crafting.RequestRecipe(recipe_code);

    /// <param name="crafting_furniture_id">The floor item id of the crafting furniture.</param>
    /// <param name="recipe_code">The recipe code to craft.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recipe_code"/> is null.</exception>
    public void Craft(Id crafting_furniture_id, string recipe_code) =>
        Crafting.Craft(crafting_furniture_id, recipe_code);

    /// <param name="crafting_furniture_id">The floor item id of the crafting furniture.</param>
    /// <param name="ingredient_item_ids">The inventory item ids to consume.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ingredient_item_ids"/> is null.</exception>
    public void CraftSecret(
        Id crafting_furniture_id,
        params Id[] ingredient_item_ids) =>
        Crafting.CraftSecret(crafting_furniture_id, ingredient_item_ids);

    /// <param name="crafting_furniture_id">The floor item id of the crafting furniture.</param>
    /// <param name="ingredient_item_ids">The inventory item ids currently loaded.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ingredient_item_ids"/> is null.</exception>
    public void RequestCraftingRecipesAvailable(
        Id crafting_furniture_id,
        params Id[] ingredient_item_ids) =>
        Crafting.RequestAvailableRecipes(
            crafting_furniture_id,
            ingredient_item_ids);
}
