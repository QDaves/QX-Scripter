namespace Qx.Model.Figures;

/// <summary>
/// Represents a figure part type as serialized in a figure string.
/// </summary>
/// <remarks>
/// The wire code in <see cref="Value"/> is the only serialized form; <see cref="Name"/> is a readable alias.
/// </remarks>
public readonly record struct FigurePartType
{
    /// <summary>The body part type, code <c>bd</c>.</summary>
    public static readonly FigurePartType Body = new("bd");
    /// <summary>The shoes part type, code <c>sh</c>.</summary>
    public static readonly FigurePartType Shoes = new("sh");
    /// <summary>The legs part type, code <c>lg</c>.</summary>
    public static readonly FigurePartType Legs = new("lg");
    /// <summary>The chest part type, code <c>ch</c>.</summary>
    public static readonly FigurePartType Chest = new("ch");
    /// <summary>The waist part type, code <c>wa</c>.</summary>
    public static readonly FigurePartType Waist = new("wa");
    /// <summary>The chest accessory part type, code <c>ca</c>.</summary>
    public static readonly FigurePartType ChestAccessory = new("ca");
    /// <summary>The head part type, code <c>hd</c>.</summary>
    public static readonly FigurePartType Head = new("hd");
    /// <summary>The hair part type, code <c>hr</c>.</summary>
    public static readonly FigurePartType Hair = new("hr");
    /// <summary>The face accessory part type, code <c>fa</c>.</summary>
    public static readonly FigurePartType FaceAccessory = new("fa");
    /// <summary>The eye accessory part type, code <c>ea</c>.</summary>
    public static readonly FigurePartType EyeAccessory = new("ea");
    /// <summary>The head accessory part type, code <c>ha</c>.</summary>
    public static readonly FigurePartType HeadAccessory = new("ha");
    /// <summary>The head equipment part type, code <c>he</c>.</summary>
    public static readonly FigurePartType HeadEquipment = new("he");
    /// <summary>The coat part type, code <c>cc</c>.</summary>
    public static readonly FigurePartType CoatChest = new("cc");
    /// <summary>The chest print part type, code <c>cp</c>.</summary>
    public static readonly FigurePartType ChestPrint = new("cp");
    /// <summary>The miscellaneous part type, code <c>mc</c>.</summary>
    public static readonly FigurePartType Misc = new("mc");
    /// <summary>The right miscellaneous part type, code <c>mcr</c>.</summary>
    public static readonly FigurePartType MiscRight = new("mcr");
    /// <summary>The left miscellaneous part type, code <c>mcl</c>.</summary>
    public static readonly FigurePartType MiscLeft = new("mcl");
    /// <summary>The pet part type, code <c>pt</c>.</summary>
    public static readonly FigurePartType Pet = new("pt");
    /// <summary>The right pet part type, code <c>ptr</c>.</summary>
    public static readonly FigurePartType PetRight = new("ptr");
    /// <summary>The left pet part type, code <c>ptl</c>.</summary>
    public static readonly FigurePartType PetLeft = new("ptl");
    /// <summary>The left hand item part type, code <c>li</c>.</summary>
    public static readonly FigurePartType LeftItem = new("li");
    /// <summary>The left hand part type, code <c>lh</c>.</summary>
    public static readonly FigurePartType LeftHand = new("lh");
    /// <summary>The left sleeve part type, code <c>ls</c>.</summary>
    public static readonly FigurePartType LeftSleeve = new("ls");
    /// <summary>The right hand part type, code <c>rh</c>.</summary>
    public static readonly FigurePartType RightHand = new("rh");
    /// <summary>The right sleeve part type, code <c>rs</c>.</summary>
    public static readonly FigurePartType RightSleeve = new("rs");
    /// <summary>The face part type, code <c>fc</c>.</summary>
    public static readonly FigurePartType Face = new("fc");
    /// <summary>The eyes part type, code <c>ey</c>.</summary>
    public static readonly FigurePartType Eyes = new("ey");
    /// <summary>The back hair part type, code <c>hrb</c>.</summary>
    public static readonly FigurePartType HairBack = new("hrb");
    /// <summary>The right hand item part type, code <c>ri</c>.</summary>
    public static readonly FigurePartType RightItem = new("ri");
    /// <summary>The left coat sleeve part type, code <c>lc</c>.</summary>
    public static readonly FigurePartType LeftCoatSleeve = new("lc");
    /// <summary>The right coat sleeve part type, code <c>rc</c>.</summary>
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

    private static readonly Dictionary<string, string> _nameByCode = build_name_by_code();
    private static readonly Dictionary<string, FigurePartType> _byName = build_by_name();
    private static readonly HashSet<string> _figureSetCodes = [.. _figureSets.Select(type => type.Value)];

    /// <summary>Gets every part type declared by the Flash client, in client declaration order.</summary>
    public static IReadOnlyList<FigurePartType> All { get; } = Array.AsReadOnly(_all);

    /// <summary>
    /// Gets the part types the avatar editor treats as selectable figure sets, in client order.
    /// </summary>
    public static IReadOnlyList<FigurePartType> FigureSets { get; } = Array.AsReadOnly(_figureSets);

    /// <summary>Gets the serialized wire code, such as <c>hr</c>.</summary>
    /// <remarks>The value is <see langword="null"/> for a default instance.</remarks>
    public string Value { get; }

    /// <summary>
    /// Gets a readable alias for the wire code, such as <c>Hair</c>.
    /// </summary>
    /// <remarks>
    /// Unknown codes return the wire code itself so future server side types stay usable.
    /// A default instance returns an empty string.
    /// </remarks>
    public string Name => Value is not null && _nameByCode.TryGetValue(Value, out string? name)
        ? name
        : Value ?? string.Empty;

    /// <summary>Gets whether the wire code is one of the part types declared by the client.</summary>
    public bool IsKnown => Value is not null && _nameByCode.ContainsKey(Value);

    /// <summary>Gets whether the avatar editor exposes the part type as a selectable figure set.</summary>
    public bool IsEditorSet => Value is not null && _figureSetCodes.Contains(Value);

    /// <summary>Initializes a new instance of the <see cref="FigurePartType"/> struct from a wire code.</summary>
    /// <param name="value">The wire code, such as <c>hr</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is empty, whitespace, or contains <c>.</c>, <c>-</c> or whitespace.</exception>
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
    /// <param name="value">The wire code. Unknown codes are accepted.</param>
    /// <returns>The part type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is empty, whitespace, or contains <c>.</c>, <c>-</c> or whitespace.</exception>
    public static FigurePartType Parse(string value) => new(value);

    /// <summary>Tries to parse a wire code such as <c>hr</c>.</summary>
    /// <param name="value">The wire code. Unknown codes are accepted.</param>
    /// <param name="type">The part type, or the default value when parsing fails.</param>
    /// <returns><see langword="true"/> if the code was parsed; otherwise, <see langword="false"/>.</returns>
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
    /// <param name="name">The alias, case insensitive.</param>
    /// <returns>The part type.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="name"/> is not a known alias.</exception>
    public static FigurePartType FromName(string name)
    {
        if (!TryFromName(name, out FigurePartType type))
            throw new FormatException($"Unknown figure part type name '{name}'.");
        return type;
    }

    /// <summary>Tries to resolve a readable alias such as <c>Hair</c> to its wire code.</summary>
    /// <param name="name">The alias, case insensitive.</param>
    /// <param name="type">The part type, or the default value when the alias is unknown.</param>
    /// <returns><see langword="true"/> if the alias is known; otherwise, <see langword="false"/>.</returns>
    public static bool TryFromName(string? name, out FigurePartType type)
    {
        if (name is not null && _byName.TryGetValue(name, out type))
            return true;

        type = default;
        return false;
    }

    /// <summary>Returns the wire code, or an empty string for a default instance.</summary>
    /// <returns>The wire code.</returns>
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
