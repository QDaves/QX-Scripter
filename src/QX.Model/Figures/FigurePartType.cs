namespace Qx.Model.Figures;

/// <summary>
/// A figure part type as serialized in a figure string.
/// The wire code is the only serialized form; <see cref="Name"/> is a readable alias.
/// </summary>
public readonly record struct FigurePartType
{
    public static readonly FigurePartType Body = new("bd");
    public static readonly FigurePartType Shoes = new("sh");
    public static readonly FigurePartType Legs = new("lg");
    public static readonly FigurePartType Chest = new("ch");
    public static readonly FigurePartType Waist = new("wa");
    public static readonly FigurePartType ChestAccessory = new("ca");
    public static readonly FigurePartType Head = new("hd");
    public static readonly FigurePartType Hair = new("hr");
    public static readonly FigurePartType FaceAccessory = new("fa");
    public static readonly FigurePartType EyeAccessory = new("ea");
    public static readonly FigurePartType HeadAccessory = new("ha");
    public static readonly FigurePartType HeadEquipment = new("he");
    public static readonly FigurePartType CoatChest = new("cc");
    public static readonly FigurePartType ChestPrint = new("cp");
    public static readonly FigurePartType Misc = new("mc");
    public static readonly FigurePartType MiscRight = new("mcr");
    public static readonly FigurePartType MiscLeft = new("mcl");
    public static readonly FigurePartType Pet = new("pt");
    public static readonly FigurePartType PetRight = new("ptr");
    public static readonly FigurePartType PetLeft = new("ptl");
    public static readonly FigurePartType LeftItem = new("li");
    public static readonly FigurePartType LeftHand = new("lh");
    public static readonly FigurePartType LeftSleeve = new("ls");
    public static readonly FigurePartType RightHand = new("rh");
    public static readonly FigurePartType RightSleeve = new("rs");
    public static readonly FigurePartType Face = new("fc");
    public static readonly FigurePartType Eyes = new("ey");
    public static readonly FigurePartType HairBack = new("hrb");
    public static readonly FigurePartType RightItem = new("ri");
    public static readonly FigurePartType LeftCoatSleeve = new("lc");
    public static readonly FigurePartType RightCoatSleeve = new("rc");

    private static readonly FigurePartType[] _all =
    [
        Body, Shoes, Legs, Chest, Waist, ChestAccessory, Head, Hair, FaceAccessory,
        EyeAccessory, HeadAccessory, HeadEquipment, CoatChest, ChestPrint, Misc,
        MiscRight, MiscLeft, Pet, PetRight, PetLeft, LeftItem, LeftHand, LeftSleeve,
        RightHand, RightSleeve, Face, Eyes, HairBack, RightItem, LeftCoatSleeve,
        RightCoatSleeve
    ];

    private static readonly string[] _names =
    [
        "Body", "Shoes", "Legs", "Chest", "Waist", "ChestAccessory", "Head", "Hair",
        "FaceAccessory", "EyeAccessory", "HeadAccessory", "HeadEquipment", "CoatChest",
        "ChestPrint", "Misc", "MiscRight", "MiscLeft", "Pet", "PetRight", "PetLeft",
        "LeftItem", "LeftHand", "LeftSleeve", "RightHand", "RightSleeve", "Face", "Eyes",
        "HairBack", "RightItem", "LeftCoatSleeve", "RightCoatSleeve"
    ];

    private static readonly FigurePartType[] _figureSets =
    [
        Shoes, Legs, Chest, Waist, ChestAccessory, Head, Hair, FaceAccessory,
        EyeAccessory, HeadAccessory, HeadEquipment, CoatChest, ChestPrint, Pet, Misc
    ];

    private static readonly FigurePartType[] _flashOnly = [MiscRight, MiscLeft, PetRight, PetLeft];

    private static readonly Dictionary<string, string> _nameByCode = build_name_by_code();
    private static readonly Dictionary<string, FigurePartType> _byName = build_by_name();
    private static readonly HashSet<string> _figureSetCodes = [.. _figureSets.Select(type => type.Value)];
    private static readonly HashSet<string> _flashOnlyCodes = [.. _flashOnly.Select(type => type.Value)];

    /// <summary>Every part type declared by the Flash client, in client declaration order.</summary>
    public static IReadOnlyList<FigurePartType> All { get; } = Array.AsReadOnly(_all);

    /// <summary>
    /// The part types the avatar editor treats as selectable figure sets, in client order.
    /// </summary>
    public static IReadOnlyList<FigurePartType> FigureSets { get; } = Array.AsReadOnly(_figureSets);

    /// <summary>Part types the Flash client declares but the Unity client does not.</summary>
    public static IReadOnlyList<FigurePartType> FlashOnly { get; } = Array.AsReadOnly(_flashOnly);

    /// <summary>The serialized wire code, e.g. <c>hr</c>.</summary>
    public string Value { get; }

    /// <summary>
    /// A readable alias for the wire code, e.g. <c>Hair</c>.
    /// Unknown codes report themselves so future server-side types stay usable.
    /// </summary>
    public string Name => Value is not null && _nameByCode.TryGetValue(Value, out string? name)
        ? name
        : Value ?? string.Empty;

    /// <summary>Whether the wire code is one of the part types declared by the clients.</summary>
    public bool IsKnown => Value is not null && _nameByCode.ContainsKey(Value);

    /// <summary>Whether the avatar editor exposes this part type as a selectable figure set.</summary>
    public bool IsEditorSet => Value is not null && _figureSetCodes.Contains(Value);

    /// <summary>Whether only the Flash client declares this part type.</summary>
    public bool IsFlashOnly => Value is not null && _flashOnlyCodes.Contains(Value);

    public FigurePartType(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        foreach (char character in value)
        {
            if (character is '.' or '-' || char.IsWhiteSpace(character))
                throw new ArgumentException("Figure part types cannot contain separators or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Parses a wire code such as <c>hr</c>.</summary>
    public static FigurePartType Parse(string value) => new(value);

    /// <summary>Parses a wire code such as <c>hr</c>.</summary>
    public static bool TryParse(string? value, out FigurePartType type)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            type = default;
            return false;
        }

        foreach (char character in value)
        {
            if (character is '.' or '-' || char.IsWhiteSpace(character))
            {
                type = default;
                return false;
            }
        }

        type = new FigurePartType(value);
        return true;
    }

    /// <summary>Resolves a readable alias such as <c>Hair</c> to its wire code.</summary>
    public static FigurePartType FromName(string name)
    {
        if (!TryFromName(name, out FigurePartType type))
            throw new FormatException($"Unknown figure part type name '{name}'.");
        return type;
    }

    /// <summary>Resolves a readable alias such as <c>Hair</c> to its wire code.</summary>
    public static bool TryFromName(string? name, out FigurePartType type)
    {
        if (name is not null && _byName.TryGetValue(name, out type))
            return true;

        type = default;
        return false;
    }

    public override string ToString() => Value ?? string.Empty;

    private static Dictionary<string, string> build_name_by_code()
    {
        Dictionary<string, string> map = new(_all.Length, StringComparer.Ordinal);
        for (int index = 0; index < _all.Length; index++)
            map[_all[index].Value] = _names[index];
        return map;
    }

    private static Dictionary<string, FigurePartType> build_by_name()
    {
        Dictionary<string, FigurePartType> map = new(_all.Length, StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < _all.Length; index++)
            map[_names[index]] = _all[index];
        return map;
    }
}
