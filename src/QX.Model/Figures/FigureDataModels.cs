using System.Collections.ObjectModel;

namespace Qx.Model.Figures;

/// <summary>Specifies the client format a figure data document was written for.</summary>
public enum FigureDataFormat
{
    /// <summary>The Flash client figure data format.</summary>
    Flash
}

/// <summary>Represents a color in a figure palette.</summary>
/// <param name="Id">The color id that figure parts reference.</param>
/// <param name="Index">The <c>index</c> value of the color in figure data.</param>
/// <param name="ClubLevel">The club level required to wear the color, 0 when no club is needed.</param>
/// <param name="IsSelectable">Whether the avatar editor offers the color.</param>
/// <param name="Rgb">The color as a 24 bit <c>0xRRGGBB</c> value.</param>
public sealed record FigureColor(
    int Id,
    int Index,
    int ClubLevel,
    bool IsSelectable,
    uint Rgb)
{
    /// <summary>Gets the red component, from 0 to 255.</summary>
    public byte Red => (byte)(Rgb >> 16);
    /// <summary>Gets the green component, from 0 to 255.</summary>
    public byte Green => (byte)(Rgb >> 8);
    /// <summary>Gets the blue component, from 0 to 255.</summary>
    public byte Blue => (byte)Rgb;
    /// <summary>Gets the red component scaled to the range 0 to 1.</summary>
    public double RedMultiplier => Red / 255d;
    /// <summary>Gets the green component scaled to the range 0 to 1.</summary>
    public double GreenMultiplier => Green / 255d;
    /// <summary>Gets the blue component scaled to the range 0 to 1.</summary>
    public double BlueMultiplier => Blue / 255d;
}

/// <summary>Represents a palette of colors that figure parts can be colored with.</summary>
public sealed class FigurePalette
{
    private readonly FigureColor[] _colors;
    private readonly ReadOnlyCollection<FigureColor> _readOnlyColors;
    private readonly Dictionary<int, FigureColor> _colorsById;

    /// <summary>Gets the palette id that set types reference.</summary>
    public int Id { get; }
    /// <summary>Gets the colors of the palette, in figure data order.</summary>
    public IReadOnlyList<FigureColor> Colors => _readOnlyColors;

    /// <summary>Initializes a new instance of the <see cref="FigurePalette"/> class.</summary>
    /// <param name="id">The palette id.</param>
    /// <param name="colors">The colors. A later color replaces an earlier one with the same id in lookups.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colors"/> is <see langword="null"/>.</exception>
    public FigurePalette(int id, IEnumerable<FigureColor> colors)
    {
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id));
        ArgumentNullException.ThrowIfNull(colors);

        Id = id;
        _colors = colors.ToArray();
        _readOnlyColors = Array.AsReadOnly(_colors);
        _colorsById = [];

        foreach (FigureColor color in _colors)
            _colorsById[color.Id] = color;
    }

    /// <summary>Gets the color with the specified id.</summary>
    /// <param name="id">The color id.</param>
    /// <returns>The color, or <see langword="null"/> when the palette has no color with the id.</returns>
    public FigureColor? GetColor(int id) => _colorsById.GetValueOrDefault(id);

    /// <summary>Tries to get the color with the specified id.</summary>
    /// <param name="id">The color id.</param>
    /// <param name="color">The color, or <see langword="null"/> when the palette has no color with the id.</param>
    /// <returns><see langword="true"/> if the color was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetColor(int id, out FigureColor color) =>
        _colorsById.TryGetValue(id, out color!);
}

/// <summary>Represents one part asset inside a figure part set.</summary>
/// <param name="Id">The part id, which selects the part asset.</param>
/// <param name="Type">The part type the asset is drawn as, which can differ from the set's type.</param>
/// <param name="Index">The <c>index</c> value of the part in figure data.</param>
/// <param name="ColorIndex">The <c>colorindex</c> value, which picks the figure part color the asset takes.</param>
/// <param name="PaletteMapId">The <c>palettemapid</c> value, or <see langword="null"/> when figure data omits it.</param>
/// <param name="Breed">The <c>breed</c> value, or <see langword="null"/> when figure data omits it.</param>
/// <param name="IsColorable">The <c>colorable</c> value, or <see langword="null"/> when figure data omits it.</param>
public sealed record FigureSetPart(
    int Id,
    FigurePartType Type,
    int Index,
    int ColorIndex,
    int? PaletteMapId,
    string? Breed,
    bool? IsColorable)
{
    /// <summary>Gets <see cref="Breed"/> as a number, or <see langword="null"/> when it is missing or not numeric.</summary>
    public int? BreedId => int.TryParse(Breed, out int value) ? value : null;
}

/// <summary>Represents a figure set, a selectable choice of parts for one figure part type.</summary>
public sealed class FigurePartSet
{
    private readonly FigureSetPart[] _parts;
    private readonly ReadOnlyCollection<FigureSetPart> _readOnlyParts;
    private readonly FigurePartType[] _hiddenLayers;
    private readonly ReadOnlyCollection<FigurePartType> _readOnlyHiddenLayers;

    /// <summary>Gets the part type the set belongs to.</summary>
    public FigurePartType Type { get; }
    /// <summary>Gets the set id that figure parts reference.</summary>
    public int Id { get; }
    /// <summary>Gets the gender that may wear the set, <see cref="FigureGender.Unisex"/> for every gender.</summary>
    public FigureGender Gender { get; }
    /// <summary>Gets the club level required to wear the set, 0 when no club is needed.</summary>
    public int ClubLevel { get; }
    /// <summary>Gets whether the set can be colored.</summary>
    public bool IsColorable { get; }
    /// <summary>Gets whether the avatar editor offers the set.</summary>
    public bool IsSelectable { get; }
    /// <summary>Gets whether figure data marks the set as preselectable.</summary>
    public bool IsPreSelectable { get; }
    /// <summary>Gets whether the set is sold, which the client requires to be owned before it can be worn.</summary>
    public bool IsSellable { get; }
    /// <summary>Gets the part assets the set draws.</summary>
    public IReadOnlyList<FigureSetPart> Parts => _readOnlyParts;
    /// <summary>Gets the part types the set hides while it is worn.</summary>
    public IReadOnlyList<FigurePartType> HiddenLayers => _readOnlyHiddenLayers;

    /// <summary>Initializes a new instance of the <see cref="FigurePartSet"/> class.</summary>
    /// <param name="type">The part type the set belongs to.</param>
    /// <param name="id">The set id.</param>
    /// <param name="gender">The gender that may wear the set. <see cref="FigureGender.Undefined"/> is not allowed.</param>
    /// <param name="clubLevel">The club level required to wear the set.</param>
    /// <param name="isColorable">Whether the set can be colored.</param>
    /// <param name="isSelectable">Whether the avatar editor offers the set.</param>
    /// <param name="isPreSelectable">Whether figure data marks the set as preselectable.</param>
    /// <param name="isSellable">Whether the set is sold.</param>
    /// <param name="parts">The part assets the set draws.</param>
    /// <param name="hiddenLayers">The part types the set hides while it is worn.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> is the default value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> or <paramref name="clubLevel"/> is negative, or <paramref name="gender"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parts"/> or <paramref name="hiddenLayers"/> is <see langword="null"/>.</exception>
    public FigurePartSet(
        FigurePartType type,
        int id,
        FigureGender gender,
        int clubLevel,
        bool isColorable,
        bool isSelectable,
        bool isPreSelectable,
        bool isSellable,
        IEnumerable<FigureSetPart> parts,
        IEnumerable<FigurePartType> hiddenLayers)
    {
        if (type == default)
            throw new ArgumentException("A figure part type is required.", nameof(type));
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id));
        if (gender is FigureGender.Undefined || !Enum.IsDefined(gender))
            throw new ArgumentOutOfRangeException(nameof(gender));
        if (clubLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(clubLevel));

        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(hiddenLayers);

        Type = type;
        Id = id;
        Gender = gender;
        ClubLevel = clubLevel;
        IsColorable = isColorable;
        IsSelectable = isSelectable;
        IsPreSelectable = isPreSelectable;
        IsSellable = isSellable;
        _parts = parts.ToArray();
        _readOnlyParts = Array.AsReadOnly(_parts);
        _hiddenLayers = hiddenLayers.ToArray();
        _readOnlyHiddenLayers = Array.AsReadOnly(_hiddenLayers);
    }

    /// <summary>Gets the part asset with the specified type and id.</summary>
    /// <param name="type">The part type of the asset.</param>
    /// <param name="id">The part id.</param>
    /// <returns>The first matching part, or <see langword="null"/> when the set has none.</returns>
    public FigureSetPart? GetPart(FigurePartType type, int id) =>
        _parts.FirstOrDefault(part => part.Type == type && part.Id == id);

    /// <summary>Gets whether the set may be worn by the given gender.</summary>
    /// <param name="gender">The gender to check.</param>
    /// <returns><see langword="true"/> if the set is unisex or matches <paramref name="gender"/>; otherwise, <see langword="false"/>.</returns>
    public bool IsValidForGender(FigureGender gender) =>
        Gender is FigureGender.Unisex || Gender == gender;
}

/// <summary>Represents the sets, palette and mandatory rules of one figure part type.</summary>
public sealed class FigureSetType
{
    private readonly FigurePartSet[] _sets;
    private readonly ReadOnlyCollection<FigurePartSet> _readOnlySets;
    private readonly Dictionary<int, FigurePartSet> _setsById;

    /// <summary>Gets the part type the set type describes.</summary>
    public FigurePartType Type { get; }
    /// <summary>Gets the id of the palette the part type is colored from.</summary>
    public int PaletteId { get; }
    /// <summary>Gets whether female figures without club must contain the part type.</summary>
    public bool IsMandatoryForFemaleWithoutClub { get; }
    /// <summary>Gets whether female figures with club must contain the part type.</summary>
    public bool IsMandatoryForFemaleWithClub { get; }
    /// <summary>Gets whether male figures without club must contain the part type.</summary>
    public bool IsMandatoryForMaleWithoutClub { get; }
    /// <summary>Gets whether male figures with club must contain the part type.</summary>
    public bool IsMandatoryForMaleWithClub { get; }
    /// <summary>Gets the sets of the part type, in figure data order.</summary>
    public IReadOnlyList<FigurePartSet> Sets => _readOnlySets;

    /// <summary>Initializes a new instance of the <see cref="FigureSetType"/> class.</summary>
    /// <param name="type">The part type the set type describes.</param>
    /// <param name="paletteId">The id of the palette the part type is colored from.</param>
    /// <param name="isMandatoryForFemaleWithoutClub">Whether female figures without club must contain the part type.</param>
    /// <param name="isMandatoryForFemaleWithClub">Whether female figures with club must contain the part type.</param>
    /// <param name="isMandatoryForMaleWithoutClub">Whether male figures without club must contain the part type.</param>
    /// <param name="isMandatoryForMaleWithClub">Whether male figures with club must contain the part type.</param>
    /// <param name="sets">The sets. A later set replaces an earlier one with the same id in lookups.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> is the default value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="paletteId"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sets"/> is <see langword="null"/>.</exception>
    public FigureSetType(
        FigurePartType type,
        int paletteId,
        bool isMandatoryForFemaleWithoutClub,
        bool isMandatoryForFemaleWithClub,
        bool isMandatoryForMaleWithoutClub,
        bool isMandatoryForMaleWithClub,
        IEnumerable<FigurePartSet> sets)
    {
        if (type == default)
            throw new ArgumentException("A figure part type is required.", nameof(type));
        if (paletteId < 0)
            throw new ArgumentOutOfRangeException(nameof(paletteId));
        ArgumentNullException.ThrowIfNull(sets);

        Type = type;
        PaletteId = paletteId;
        IsMandatoryForFemaleWithoutClub = isMandatoryForFemaleWithoutClub;
        IsMandatoryForFemaleWithClub = isMandatoryForFemaleWithClub;
        IsMandatoryForMaleWithoutClub = isMandatoryForMaleWithoutClub;
        IsMandatoryForMaleWithClub = isMandatoryForMaleWithClub;
        _sets = sets.ToArray();
        _readOnlySets = Array.AsReadOnly(_sets);
        _setsById = [];

        foreach (FigurePartSet set in _sets)
            _setsById[set.Id] = set;
    }

    /// <summary>Gets whether a figure must contain the part type for a gender at a club level.</summary>
    /// <param name="gender">The gender. Genders other than male and female never have mandatory types.</param>
    /// <param name="clubLevel">The club level. Any level of 1 or more counts as club, a negative level returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the part type is mandatory; otherwise, <see langword="false"/>.</returns>
    public bool IsMandatory(FigureGender gender, int clubLevel)
    {
        if (clubLevel < 0)
            return false;

        return (gender, Math.Min(clubLevel, 1)) switch
        {
            (FigureGender.Female, 0) => IsMandatoryForFemaleWithoutClub,
            (FigureGender.Female, 1) => IsMandatoryForFemaleWithClub,
            (FigureGender.Male, 0) => IsMandatoryForMaleWithoutClub,
            (FigureGender.Male, 1) => IsMandatoryForMaleWithClub,
            _ => false
        };
    }

    /// <summary>Gets the lowest club level at which a figure of a gender may omit the part type.</summary>
    /// <param name="gender">The gender.</param>
    /// <returns>
    /// 0 when the part type is optional without club, 1 when it is optional only with club, and -1 when
    /// it is always mandatory or <paramref name="gender"/> is neither male nor female.
    /// </returns>
    public int OptionalFromClubLevel(FigureGender gender)
    {
        bool first;
        bool second;

        switch (gender)
        {
            case FigureGender.Female:
                first = IsMandatoryForFemaleWithoutClub;
                second = IsMandatoryForFemaleWithClub;
                break;
            case FigureGender.Male:
                first = IsMandatoryForMaleWithoutClub;
                second = IsMandatoryForMaleWithClub;
                break;
            default:
                return -1;
        }

        if (!first)
            return 0;
        return !second ? 1 : -1;
    }

    /// <summary>Gets the set with the specified id.</summary>
    /// <param name="id">The set id.</param>
    /// <returns>The set, or <see langword="null"/> when the part type has no set with the id.</returns>
    public FigurePartSet? GetSet(int id) => _setsById.GetValueOrDefault(id);

    /// <summary>Gets the set the client falls back to for a gender.</summary>
    /// <param name="gender">The gender the set must be valid for.</param>
    /// <returns>The last set in figure data order that needs no club and is valid for <paramref name="gender"/>, or <see langword="null"/> when there is none.</returns>
    public FigurePartSet? GetDefaultSet(FigureGender gender)
    {
        for (int index = _sets.Length - 1; index >= 0; index--)
        {
            FigurePartSet set = _sets[index];
            if (set.ClubLevel == 0 && set.IsValidForGender(gender))
                return set;
        }

        return null;
    }

    /// <summary>Gets the sets the avatar editor offers for a gender and club level.</summary>
    /// <param name="gender">The gender the sets must be valid for.</param>
    /// <param name="clubLevel">The club level. Sets that need a higher level are excluded.</param>
    /// <returns>The selectable sets, in figure data order.</returns>
    /// <remarks>
    /// Sellable sets are included; the client additionally requires them to be owned in the inventory.
    /// </remarks>
    public IReadOnlyList<FigurePartSet> GetSelectableSets(FigureGender gender, int clubLevel) =>
        Array.AsReadOnly(_sets
            .Where(set => set.IsSelectable && set.IsValidForGender(gender) && set.ClubLevel <= clubLevel)
            .ToArray());
}

/// <summary>
/// Represents the outcome of completing a figure for a gender the way the client repairs figures
/// before rendering them.
/// </summary>
/// <param name="Figure">The completed figure.</param>
/// <param name="IsValid">Whether the figure needed no repair.</param>
/// <param name="RepairedTypes">The part types that were replaced with their default set, in figure data order.</param>
public sealed record FigureValidation(
    Figure Figure,
    bool IsValid,
    IReadOnlyList<FigurePartType> RepairedTypes);

/// <summary>Represents a figure part color id resolved against its palette.</summary>
/// <param name="Id">The color id from the figure part.</param>
/// <param name="Color">The palette color, or <see langword="null"/> when the palette or the color is unknown.</param>
public sealed record ResolvedFigureColor(int Id, FigureColor? Color);

/// <summary>Represents a figure part resolved against figure data.</summary>
/// <param name="Selection">The figure part as it appears in the figure.</param>
/// <param name="SetType">The set type of the part type, or <see langword="null"/> when figure data has none.</param>
/// <param name="Set">The selected set, or <see langword="null"/> when the set type or set id is unknown.</param>
/// <param name="Colors">The selected colors, one per color id, in figure part order.</param>
public sealed record ResolvedFigurePart(
    FigurePart Selection,
    FigureSetType? SetType,
    FigurePartSet? Set,
    IReadOnlyList<ResolvedFigureColor> Colors);
