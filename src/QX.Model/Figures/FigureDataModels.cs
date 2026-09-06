using System.Collections.ObjectModel;

namespace Qx.Model.Figures;

public enum FigureDataFormat
{
    Flash,
    Unity
}

public sealed record FigureColor(
    int Id,
    int Index,
    int ClubLevel,
    bool IsSelectable,
    uint Rgb)
{
    public byte Red => (byte)(Rgb >> 16);
    public byte Green => (byte)(Rgb >> 8);
    public byte Blue => (byte)Rgb;
    public double RedMultiplier => Red / 255d;
    public double GreenMultiplier => Green / 255d;
    public double BlueMultiplier => Blue / 255d;
}

public sealed class FigurePalette
{
    private readonly FigureColor[] _colors;
    private readonly ReadOnlyCollection<FigureColor> _readOnlyColors;
    private readonly Dictionary<int, FigureColor> _colorsById;

    public int Id { get; }
    public IReadOnlyList<FigureColor> Colors => _readOnlyColors;

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

    public FigureColor? GetColor(int id) => _colorsById.GetValueOrDefault(id);

    public bool TryGetColor(int id, out FigureColor color) =>
        _colorsById.TryGetValue(id, out color!);
}

public sealed record FigureSetPart(
    int Id,
    FigurePartType Type,
    int Index,
    int ColorIndex,
    int? PaletteMapId,
    string? Breed,
    bool? IsColorable)
{
    public int? BreedId => int.TryParse(Breed, out int value) ? value : null;
}

public sealed class FigurePartSet
{
    private readonly FigureSetPart[] _parts;
    private readonly ReadOnlyCollection<FigureSetPart> _readOnlyParts;
    private readonly FigurePartType[] _hiddenLayers;
    private readonly ReadOnlyCollection<FigurePartType> _readOnlyHiddenLayers;

    public FigurePartType Type { get; }
    public int Id { get; }
    public FigureGender Gender { get; }
    public int ClubLevel { get; }
    public bool IsColorable { get; }
    public bool IsSelectable { get; }
    public bool IsPreSelectable { get; }
    public bool IsSellable { get; }
    public IReadOnlyList<FigureSetPart> Parts => _readOnlyParts;
    public IReadOnlyList<FigurePartType> HiddenLayers => _readOnlyHiddenLayers;

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

    public FigureSetPart? GetPart(FigurePartType type, int id) =>
        _parts.FirstOrDefault(part => part.Type == type && part.Id == id);

    /// <summary>
    /// Whether the set may be worn by the given gender. Unisex sets are valid for everyone.
    /// </summary>
    public bool IsValidForGender(FigureGender gender) =>
        Gender is FigureGender.Unisex || Gender == gender;
}

public sealed class FigureSetType
{
    private readonly FigurePartSet[] _sets;
    private readonly ReadOnlyCollection<FigurePartSet> _readOnlySets;
    private readonly Dictionary<int, FigurePartSet> _setsById;

    public FigurePartType Type { get; }
    public int PaletteId { get; }
    public bool IsMandatoryForFemaleWithoutClub { get; }
    public bool IsMandatoryForFemaleWithClub { get; }
    public bool IsMandatoryForMaleWithoutClub { get; }
    public bool IsMandatoryForMaleWithClub { get; }
    public IReadOnlyList<FigurePartSet> Sets => _readOnlySets;

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

    public FigurePartSet? GetSet(int id) => _setsById.GetValueOrDefault(id);

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

    /// <summary>
    /// The sets the avatar editor offers for a gender and club level. Sellable sets are
    /// included; the client additionally requires them to be owned in the inventory.
    /// </summary>
    public IReadOnlyList<FigurePartSet> GetSelectableSets(FigureGender gender, int club_level) =>
        Array.AsReadOnly(_sets
            .Where(set => set.IsSelectable && set.IsValidForGender(gender) && set.ClubLevel <= club_level)
            .ToArray());
}

/// <summary>
/// The outcome of completing a figure for a gender the way the client repairs figures
/// before rendering them.
/// </summary>
public sealed record FigureValidation(
    Figure Figure,
    bool IsValid,
    IReadOnlyList<FigurePartType> RepairedTypes);

public sealed record ResolvedFigureColor(int Id, FigureColor? Color);

public sealed record ResolvedFigurePart(
    FigurePart Selection,
    FigureSetType? SetType,
    FigurePartSet? Set,
    IReadOnlyList<ResolvedFigureColor> Colors);
