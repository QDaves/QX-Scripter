using Qx.Model;

namespace Qx.Game.Snapshots;

/// <summary>
/// Represents the JSON projection of one custom part of a pet's figure used by the MCP read tools
/// and the application-layer results; scripts that read live state use <see cref="PetCustomPart"/>.
/// </summary>
/// <param name="LayerId">The figure layer the part is drawn on.</param>
/// <param name="PartId">The part identifier.</param>
/// <param name="PaletteId">The palette the part is colored with.</param>
public sealed record InventoryPetPartSnapshot(
    int LayerId,
    int PartId,
    int PaletteId);

/// <summary>
/// Represents the JSON projection of a pet in the local user's inventory used by the MCP read tools
/// and the application-layer results; scripts that read live state use <see cref="InventoryPet"/>.
/// </summary>
/// <param name="Id">The pet identifier.</param>
/// <param name="Name">The pet name.</param>
/// <param name="TypeId">The pet type identifier.</param>
/// <param name="PaletteId">The palette of the pet's figure.</param>
/// <param name="Color">The figure color as the hotel sends it.</param>
/// <param name="BreedId">The breed identifier.</param>
/// <param name="CustomParts">The custom figure parts, in the order the hotel sent them.</param>
/// <param name="Level">The pet level.</param>
/// <param name="RarityLevel">The rarity level as the hotel sends it.</param>
/// <param name="FigureString">
/// The figure string built from <paramref name="TypeId"/>, <paramref name="PaletteId"/>,
/// <paramref name="Color"/>, the custom part count and each part's layer, part and palette,
/// separated by spaces.
/// </param>
public sealed record InventoryPetSnapshot(
    Id Id,
    string Name,
    int TypeId,
    int PaletteId,
    string Color,
    int BreedId,
    IReadOnlyList<InventoryPetPartSnapshot> CustomParts,
    int Level,
    int RarityLevel,
    string FigureString);

/// <summary>
/// Represents the JSON projection of the local user's pet inventory and its load state for the MCP
/// read tools; scripts use the <c>InventoryPets</c> global.
/// </summary>
/// <param name="IsLoading">Whether a load is in flight right now.</param>
/// <param name="IsStale">
/// Whether the listed pets are left over from a previous load that has been invalidated. They are
/// still returned, but a fresh load is needed before acting on them.
/// </param>
/// <param name="Generation">A counter bumped every time a new load begins.</param>
/// <param name="ExpectedFragments">How many fragments the current load consists of, or -1 while that is not yet known.</param>
/// <param name="ReceivedFragments">How many fragments of the current load have arrived.</param>
/// <param name="Total">The number of pets in the inventory.</param>
/// <param name="Returned">The number of pets in <paramref name="Pets"/>.</param>
/// <param name="MaxPets">The cap applied to the projection.</param>
/// <param name="Truncated">Whether pets were dropped to honor the cap.</param>
/// <param name="Pets">The pets, ordered ascending by identifier. When truncated these are the lowest identifiers.</param>
public sealed record PetInventorySnapshot(
    bool IsLoading,
    bool IsStale,
    long Generation,
    int ExpectedFragments,
    int ReceivedFragments,
    int Total,
    int Returned,
    int MaxPets,
    bool Truncated,
    IReadOnlyList<InventoryPetSnapshot> Pets);

public static partial class SnapshotFactory
{
    /// <summary>Projects inventory pets into a capped inventory snapshot.</summary>
    /// <param name="pets">The pets to project.</param>
    /// <param name="maxPets">The maximum number of pets to return.</param>
    /// <param name="isLoading">Whether a load is in flight.</param>
    /// <param name="isStale">Whether the pets are left over from an invalidated load.</param>
    /// <param name="generation">The load counter to stamp onto the snapshot.</param>
    /// <param name="expectedFragments">The number of fragments in the current load, or -1 when not yet known.</param>
    /// <param name="receivedFragments">The number of fragments received so far.</param>
    /// <param name="sourceItemLimit">The safety valve against an unbounded source; a source with more items throws.</param>
    /// <returns>The inventory, ordered ascending by pet identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxPets"/> or <paramref name="sourceItemLimit"/> is negative.
    /// </exception>
    /// <exception cref="SnapshotSourceLimitExceededException">
    /// Thrown when the sequence yields more than <paramref name="sourceItemLimit"/> items.
    /// </exception>
    public static PetInventorySnapshot PetInventory(
        IEnumerable<InventoryPet> pets,
        int maxPets = 200,
        bool isLoading = false,
        bool isStale = false,
        long generation = 0,
        int expectedFragments = -1,
        int receivedFragments = 0,
        int sourceItemLimit = DefaultSourceItemLimit)
    {
        CappedSource<InventoryPet> inventory = SelectCapped(
            pets,
            maxPets,
            sourceItemLimit,
            nameof(pets),
            Comparer<InventoryPet>.Create(
                (left, right) => ((long)left.Id).CompareTo((long)right.Id)));
        InventoryPetSnapshot[] projected = inventory.Items
            .Select(From)
            .ToArray();

        return new PetInventorySnapshot(
            isLoading,
            isStale,
            generation,
            expectedFragments,
            receivedFragments,
            inventory.Total,
            projected.Length,
            maxPets,
            projected.Length < inventory.Total,
            projected);
    }

    /// <summary>Projects one inventory pet.</summary>
    /// <param name="pet">The pet to project.</param>
    /// <returns>The pet snapshot.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pet"/> is <see langword="null"/>.</exception>
    public static InventoryPetSnapshot From(InventoryPet pet)
    {
        ArgumentNullException.ThrowIfNull(pet);

        return new InventoryPetSnapshot(
            pet.Id,
            pet.Name,
            pet.TypeId,
            pet.PaletteId,
            pet.Color,
            pet.BreedId,
            pet.CustomParts
                .Select(part => new InventoryPetPartSnapshot(
                    part.LayerId,
                    part.PartId,
                    part.PaletteId))
                .ToArray(),
            pet.Level,
            pet.RarityLevel,
            pet.FigureString);
    }
}
