using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <param name="handler">Receives the product list.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnCraftableProducts(Action<CraftableProducts> handler)
        => Subscribe(handler, value => Crafting.ProductsReceived += value,
            value => Crafting.ProductsReceived -= value);

    /// <param name="handler">Receives the ingredient list.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnCraftingRecipe(Action<CraftingRecipe> handler)
        => Subscribe(handler, value => Crafting.RecipeReceived += value,
            value => Crafting.RecipeReceived -= value);

    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnCraftingResult(Action<CraftingResult> handler)
        => Subscribe(handler, value => Crafting.ResultReceived += value,
            value => Crafting.ResultReceived -= value);

    /// <param name="handler">Receives the match count and the completeness flag.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnCraftingRecipesAvailable(
        Action<CraftingRecipesAvailable> handler)
        => Subscribe(handler, value => Crafting.AvailableRecipesReceived += value,
            value => Crafting.AvailableRecipesReceived -= value);

    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnCraftingReset(Action handler)
        => Subscribe(handler, value => Crafting.ResetCompleted += value,
            value => Crafting.ResetCompleted -= value);
}
