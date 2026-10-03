using Qx.Game.Application;

namespace Qx.Game.Snapshots;

public sealed partial class GameQueryService
{
    /// <summary>Gets the pets in the local user's inventory, ordered ascending by identifier.</summary>
    /// <param name="maxPets">The maximum number of pets to return.</param>
    /// <returns>The <c>pet_inventory</c> query envelope.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxPets"/> is negative.</exception>
    public QueryEnvelope<PetInventorySnapshot> PetInventory(int maxPets = 200)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxPets);
        InventoryPetPage page = InventoryApplicationPages.ReadPets(
            application,
            maxPets: maxPets);
        var snapshot = new PetInventorySnapshot(
            page.Loading,
            page.Stale,
            page.LoadGeneration,
            page.ExpectedFragments,
            page.ReceivedFragments,
            page.Total,
            page.Pets.Count,
            maxPets,
            page.Pets.Count < page.Total,
            page.Pets);
        return Result(
            "pet_inventory",
            snapshot,
            page.Connected && page.Loaded,
            page.Loaded,
            page.Stale || !page.Connected && page.Total > 0,
            snapshot.Truncated,
            page.Loaded ? [] : ["petInventory"]);
    }

}
