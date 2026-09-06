using Qx;
using Qx.Model;

namespace Qx.Scripting;

public sealed class InventoryPetQuery : QueryCollection<InventoryPet>
{
    public InventoryPetQuery(IEnumerable<InventoryPet> pets) : base(pets)
    {
    }

    public InventoryPetQuery Where(Func<InventoryPet, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public InventoryPetQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    public InventoryPetQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(pet => values.Contains(pet.Id));
    }

    public InventoryPetQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    public InventoryPetQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(pet => values.Contains(pet.Name));
    }

    public InventoryPetQuery NotNamed(params string[] names) =>
        NotNamed((IEnumerable<string>)names);

    public InventoryPetQuery NotNamed(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(pet => !values.Contains(pet.Name));
    }

    public InventoryPetQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(pet => pet.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public InventoryPetQuery OfType(params int[] type_ids) =>
        OfType((IEnumerable<int>)type_ids);

    public InventoryPetQuery OfType(IEnumerable<int> type_ids)
    {
        HashSet<int> values = QueryValues.Set(type_ids);
        return Where(pet => values.Contains(pet.TypeId));
    }

    public InventoryPetQuery OfPalette(params int[] palette_ids) =>
        OfPalette((IEnumerable<int>)palette_ids);

    public InventoryPetQuery OfPalette(IEnumerable<int> palette_ids)
    {
        HashSet<int> values = QueryValues.Set(palette_ids);
        return Where(pet => values.Contains(pet.PaletteId));
    }

    public InventoryPetQuery OfBreed(params int[] breed_ids) =>
        OfBreed((IEnumerable<int>)breed_ids);

    public InventoryPetQuery OfBreed(IEnumerable<int> breed_ids)
    {
        HashSet<int> values = QueryValues.Set(breed_ids);
        return Where(pet => values.Contains(pet.BreedId));
    }

    public InventoryPetQuery OfColor(params string[] colors) =>
        OfColor((IEnumerable<string>)colors);

    public InventoryPetQuery OfColor(IEnumerable<string> colors)
    {
        HashSet<string> values = QueryValues.Strings(colors);
        return Where(pet => values.Contains(pet.Color));
    }

    public InventoryPetQuery AtLevel(params int[] levels) =>
        AtLevel((IEnumerable<int>)levels);

    public InventoryPetQuery AtLevel(IEnumerable<int> levels)
    {
        HashSet<int> values = QueryValues.Set(levels);
        return Where(pet => values.Contains(pet.Level));
    }

    public InventoryPetQuery LevelBetween(int minimum, int maximum)
    {
        if (minimum > maximum)
            throw new ArgumentException("Minimum level cannot exceed maximum level.", nameof(minimum));
        return Where(pet => pet.Level >= minimum && pet.Level <= maximum);
    }

    public InventoryPetQuery AtLeastLevel(int minimum) =>
        Where(pet => pet.Level >= minimum);

    public InventoryPetQuery AtMostLevel(int maximum) =>
        Where(pet => pet.Level <= maximum);

    public InventoryPetQuery OfRarity(params int[] rarity_levels) =>
        OfRarity((IEnumerable<int>)rarity_levels);

    public InventoryPetQuery OfRarity(IEnumerable<int> rarity_levels)
    {
        HashSet<int> values = QueryValues.Set(rarity_levels);
        return Where(pet => values.Contains(pet.RarityLevel));
    }

    public InventoryPetQuery WithKnownRarity(bool value = true) =>
        Where(pet => (pet.RarityLevel >= 0) == value);

    public InventoryPetQuery InRoom(bool value = true) =>
        Where(pet => pet.IsInRoom == value);

    public InventoryPetQuery InRoom(Id room_id) =>
        Where(pet => pet.RoomId == room_id);

    public InventoryPetQuery WithCustomParts(bool value = true) =>
        Where(pet => (pet.CustomParts.Count > 0) == value);

    public InventoryPetQuery WithCustomLayer(params int[] layer_ids)
    {
        HashSet<int> values = QueryValues.Set(layer_ids);
        return Where(pet => pet.CustomParts.Any(part => values.Contains(part.LayerId)));
    }

    public InventoryPetQuery WithCustomPart(params int[] part_ids)
    {
        HashSet<int> values = QueryValues.Set(part_ids);
        return Where(pet => pet.CustomParts.Any(part => values.Contains(part.PartId)));
    }

    public InventoryPetQuery WithCustomPalette(params int[] palette_ids)
    {
        HashSet<int> values = QueryValues.Set(palette_ids);
        return Where(pet => pet.CustomParts.Any(part => values.Contains(part.PaletteId)));
    }

    private InventoryPetQuery Next(IEnumerable<InventoryPet> pets) => new(pets);
}
