using Qx;
using Qx.Model;

namespace Qx.Scripting;

/// <summary>
/// Represents a query over inventory pets.
/// </summary>
/// <remarks>
/// Every filter returns a new query. Text matching ignores case, <see langword="null"/> entries
/// in text lists are skipped, and a <see langword="null"/> argument throws
/// <see cref="ArgumentNullException"/>.
/// </remarks>
public sealed class InventoryPetQuery : QueryCollection<InventoryPet>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryPetQuery"/> class over the specified pets.
    /// </summary>
    /// <param name="pets">The inventory pets to query.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pets"/> is <see langword="null"/>.</exception>
    public InventoryPetQuery(IEnumerable<InventoryPet> pets) : base(pets)
    {
    }

    /// <summary>
    /// Filters the pets with a predicate.
    /// </summary>
    /// <param name="predicate">The condition a pet must meet to be kept.</param>
    /// <returns>A new query with the pets that match <paramref name="predicate"/>.</returns>
    public InventoryPetQuery Where(Func<InventoryPet, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    /// <summary>
    /// Filters the pets to those with any of the specified ids.
    /// </summary>
    /// <param name="ids">The pet ids to keep.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    /// <summary>
    /// Filters the pets to those with any of the specified ids.
    /// </summary>
    /// <param name="ids">The pet ids to keep.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(pet => values.Contains(pet.Id));
    }

    /// <summary>
    /// Filters the pets to those with any of the specified names, ignoring case.
    /// </summary>
    /// <param name="names">The names to keep.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    /// <summary>
    /// Filters the pets to those with any of the specified names, ignoring case.
    /// </summary>
    /// <param name="names">The names to keep.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(pet => values.Contains(pet.Name));
    }

    /// <summary>
    /// Filters out the pets with any of the specified names, ignoring case.
    /// </summary>
    /// <param name="names">The names to drop.</param>
    /// <returns>A new query without the matching pets.</returns>
    public InventoryPetQuery NotNamed(params string[] names) =>
        NotNamed((IEnumerable<string>)names);

    /// <summary>
    /// Filters out the pets with any of the specified names, ignoring case.
    /// </summary>
    /// <param name="names">The names to drop.</param>
    /// <returns>A new query without the matching pets.</returns>
    public InventoryPetQuery NotNamed(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(pet => !values.Contains(pet.Name));
    }

    /// <summary>
    /// Filters the pets to those whose name contains the specified text, ignoring case.
    /// </summary>
    /// <param name="value">The text to search for.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(pet => pet.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters the pets to those of any of the specified pet types.
    /// </summary>
    /// <param name="typeIds">The pet type ids to keep, compared with <see cref="InventoryPet.TypeId"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfType(params int[] typeIds) =>
        OfType((IEnumerable<int>)typeIds);

    /// <summary>
    /// Filters the pets to those of any of the specified pet types.
    /// </summary>
    /// <param name="typeIds">The pet type ids to keep, compared with <see cref="InventoryPet.TypeId"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfType(IEnumerable<int> typeIds)
    {
        HashSet<int> values = QueryValues.Set(typeIds);
        return Where(pet => values.Contains(pet.TypeId));
    }

    /// <summary>
    /// Filters the pets to those with any of the specified palettes.
    /// </summary>
    /// <param name="paletteIds">The palette ids to keep, compared with <see cref="InventoryPet.PaletteId"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfPalette(params int[] paletteIds) =>
        OfPalette((IEnumerable<int>)paletteIds);

    /// <summary>
    /// Filters the pets to those with any of the specified palettes.
    /// </summary>
    /// <param name="paletteIds">The palette ids to keep, compared with <see cref="InventoryPet.PaletteId"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfPalette(IEnumerable<int> paletteIds)
    {
        HashSet<int> values = QueryValues.Set(paletteIds);
        return Where(pet => values.Contains(pet.PaletteId));
    }

    /// <summary>
    /// Filters the pets to those of any of the specified breeds.
    /// </summary>
    /// <param name="breedIds">The breed ids to keep, compared with <see cref="InventoryPet.BreedId"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfBreed(params int[] breedIds) =>
        OfBreed((IEnumerable<int>)breedIds);

    /// <summary>
    /// Filters the pets to those of any of the specified breeds.
    /// </summary>
    /// <param name="breedIds">The breed ids to keep, compared with <see cref="InventoryPet.BreedId"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfBreed(IEnumerable<int> breedIds)
    {
        HashSet<int> values = QueryValues.Set(breedIds);
        return Where(pet => values.Contains(pet.BreedId));
    }

    /// <summary>
    /// Filters the pets to those with any of the specified colors, ignoring case.
    /// </summary>
    /// <param name="colors">The colors to keep, compared with <see cref="InventoryPet.Color"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfColor(params string[] colors) =>
        OfColor((IEnumerable<string>)colors);

    /// <summary>
    /// Filters the pets to those with any of the specified colors, ignoring case.
    /// </summary>
    /// <param name="colors">The colors to keep, compared with <see cref="InventoryPet.Color"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfColor(IEnumerable<string> colors)
    {
        HashSet<string> values = QueryValues.Strings(colors);
        return Where(pet => values.Contains(pet.Color));
    }

    /// <summary>
    /// Filters the pets to those at any of the specified levels.
    /// </summary>
    /// <param name="levels">The levels to keep.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery AtLevel(params int[] levels) =>
        AtLevel((IEnumerable<int>)levels);

    /// <summary>
    /// Filters the pets to those at any of the specified levels.
    /// </summary>
    /// <param name="levels">The levels to keep.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery AtLevel(IEnumerable<int> levels)
    {
        HashSet<int> values = QueryValues.Set(levels);
        return Where(pet => values.Contains(pet.Level));
    }

    /// <summary>
    /// Filters the pets to those with a level in the specified range.
    /// </summary>
    /// <param name="minimum">The lowest level to keep, inclusive.</param>
    /// <param name="maximum">The highest level to keep, inclusive.</param>
    /// <returns>A new query with the matching pets.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="minimum"/> is greater than <paramref name="maximum"/>.</exception>
    public InventoryPetQuery LevelBetween(int minimum, int maximum)
    {
        if (minimum > maximum)
            throw new ArgumentException("Minimum level cannot exceed maximum level.", nameof(minimum));
        return Where(pet => pet.Level >= minimum && pet.Level <= maximum);
    }

    /// <summary>
    /// Filters the pets to those at or above the specified level.
    /// </summary>
    /// <param name="minimum">The lowest level to keep, inclusive.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery AtLeastLevel(int minimum) =>
        Where(pet => pet.Level >= minimum);

    /// <summary>
    /// Filters the pets to those at or below the specified level.
    /// </summary>
    /// <param name="maximum">The highest level to keep, inclusive.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery AtMostLevel(int maximum) =>
        Where(pet => pet.Level <= maximum);

    /// <summary>
    /// Filters the pets to those with any of the specified rarity levels.
    /// </summary>
    /// <param name="rarityLevels">The rarity levels to keep, compared with <see cref="InventoryPet.RarityLevel"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfRarity(params int[] rarityLevels) =>
        OfRarity((IEnumerable<int>)rarityLevels);

    /// <summary>
    /// Filters the pets to those with any of the specified rarity levels.
    /// </summary>
    /// <param name="rarityLevels">The rarity levels to keep, compared with <see cref="InventoryPet.RarityLevel"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery OfRarity(IEnumerable<int> rarityLevels)
    {
        HashSet<int> values = QueryValues.Set(rarityLevels);
        return Where(pet => values.Contains(pet.RarityLevel));
    }

    /// <summary>
    /// Filters the pets by whether their rarity level is known.
    /// </summary>
    /// <remarks>
    /// A rarity level of zero or more counts as known; a negative level counts as unknown.
    /// </remarks>
    /// <param name="value">Whether to keep pets with a known rarity instead of the others.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery WithKnownRarity(bool value = true) =>
        Where(pet => (pet.RarityLevel >= 0) == value);

    /// <summary>
    /// Filters the pets by whether they have any custom parts.
    /// </summary>
    /// <param name="value">Whether to keep pets with custom parts instead of pets without any.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery WithCustomParts(bool value = true) =>
        Where(pet => (pet.CustomParts.Count > 0) == value);

    /// <summary>
    /// Filters the pets to those with a custom part on any of the specified layers.
    /// </summary>
    /// <param name="layerIds">The layer ids to match, compared with <see cref="PetCustomPart.LayerId"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery WithCustomLayer(params int[] layerIds)
    {
        HashSet<int> values = QueryValues.Set(layerIds);
        return Where(pet => pet.CustomParts.Any(part => values.Contains(part.LayerId)));
    }

    /// <summary>
    /// Filters the pets to those with any of the specified custom parts.
    /// </summary>
    /// <param name="partIds">The part ids to match, compared with <see cref="PetCustomPart.PartId"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery WithCustomPart(params int[] partIds)
    {
        HashSet<int> values = QueryValues.Set(partIds);
        return Where(pet => pet.CustomParts.Any(part => values.Contains(part.PartId)));
    }

    /// <summary>
    /// Filters the pets to those with a custom part in any of the specified palettes.
    /// </summary>
    /// <param name="paletteIds">The palette ids to match, compared with <see cref="PetCustomPart.PaletteId"/>.</param>
    /// <returns>A new query with the matching pets.</returns>
    public InventoryPetQuery WithCustomPalette(params int[] paletteIds)
    {
        HashSet<int> values = QueryValues.Set(paletteIds);
        return Where(pet => pet.CustomParts.Any(part => values.Contains(part.PaletteId)));
    }

    private InventoryPetQuery Next(IEnumerable<InventoryPet> pets) => new(pets);
}
