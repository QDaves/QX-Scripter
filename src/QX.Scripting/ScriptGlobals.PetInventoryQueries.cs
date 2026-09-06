using Qx.Model;

namespace Qx.Scripting;

/// <content>Query entry points for the pet inventory.</content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Starts a filter/sort/projection query over the pets currently cached in the inventory.
    /// Nothing is requested: query before the pet inventory has been loaded and the result is
    /// empty.
    /// </summary>
    /// <returns>A query over a snapshot of the cached pets.</returns>
    public InventoryPetQuery QueryInventoryPets() => Queries.InventoryPets;

    /// <summary>
    /// Starts a pet query over a caller-supplied sequence instead of the inventory cache.
    /// </summary>
    /// <param name="pets">The pets to query.</param>
    /// <returns>A query over the given pets.</returns>
    public InventoryPetQuery QueryInventoryPets(IEnumerable<InventoryPet> pets) => Queries.From(pets);
}
