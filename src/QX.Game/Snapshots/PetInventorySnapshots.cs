using Qx.Model;

namespace Qx.Game.Snapshots;

public sealed record InventoryPetPartSnapshot(
    int LayerId,
    int PartId,
    int PaletteId);

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
    Id RoomId,
    string RoomName,
    string RoomContext,
    bool HasRoomContext,
    bool IsInRoom,
    string FigureString);

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
            pet.RoomId,
            pet.RoomName,
            pet.RoomContext,
            pet.HasRoomContext,
            pet.IsInRoom,
            pet.FigureString);
    }
}
