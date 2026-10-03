using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Qx.Model.Figures;

/// <summary>
/// Represents figure data, the palettes and part sets that define which figures avatars can wear.
/// </summary>
/// <remarks>
/// Figure data is read from the hotel's <c>figuredata</c> XML with <see cref="ParseXml"/>.
/// Lookups by id or part type return <see langword="null"/> for unknown values.
/// </remarks>
public sealed class FigureData
{
    private const int ValidationClubLevel = 2;

    private readonly FigurePalette[] _palettes;
    private readonly ReadOnlyCollection<FigurePalette> _readOnlyPalettes;
    private readonly FigureSetType[] _setTypes;
    private readonly ReadOnlyCollection<FigureSetType> _readOnlySetTypes;
    private readonly Dictionary<int, FigurePalette> _palettesById;
    private readonly Dictionary<FigurePartType, FigureSetType> _setTypesByType;

    /// <summary>Gets the client format the figure data was read in.</summary>
    public FigureDataFormat Format { get; }
    /// <summary>Gets the color palettes, in figure data order.</summary>
    public IReadOnlyList<FigurePalette> Palettes => _readOnlyPalettes;
    /// <summary>Gets the set types, one per figure part type, in figure data order.</summary>
    public IReadOnlyList<FigureSetType> SetTypes => _readOnlySetTypes;

    /// <summary>Initializes a new instance of the <see cref="FigureData"/> class.</summary>
    /// <param name="format">The client format the figure data was read in.</param>
    /// <param name="palettes">The color palettes. A later palette replaces an earlier one with the same id in lookups.</param>
    /// <param name="setTypes">The set types. A later set type replaces an earlier one with the same part type in lookups.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="format"/> is not a defined value.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="palettes"/> or <paramref name="setTypes"/> is <see langword="null"/>.</exception>
    public FigureData(
        FigureDataFormat format,
        IEnumerable<FigurePalette> palettes,
        IEnumerable<FigureSetType> setTypes)
    {
        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(nameof(format));
        ArgumentNullException.ThrowIfNull(palettes);
        ArgumentNullException.ThrowIfNull(setTypes);

        Format = format;
        _palettes = palettes.ToArray();
        _setTypes = setTypes.ToArray();
        _readOnlyPalettes = Array.AsReadOnly(_palettes);
        _readOnlySetTypes = Array.AsReadOnly(_setTypes);
        _palettesById = [];
        _setTypesByType = [];

        foreach (FigurePalette palette in _palettes)
            _palettesById[palette.Id] = palette;
        foreach (FigureSetType setType in _setTypes)
            _setTypesByType[setType.Type] = setType;
    }

    /// <summary>Parses figure data from a <c>figuredata</c> XML document.</summary>
    /// <param name="xml">The XML text. DTDs are rejected and external resources are not resolved.</param>
    /// <param name="format">The client format of the document.</param>
    /// <returns>The parsed figure data.</returns>
    /// <remarks>
    /// For <see cref="FigureDataFormat.Flash"/>, parts of the same type inside a set are ordered by
    /// descending index, and every <c>breed</c> value must be numeric.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="xml"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the root is not <c>figuredata</c>, or a required attribute is missing or invalid.</exception>
    /// <exception cref="XmlException">Thrown when the text is not well formed XML.</exception>
    public static FigureData ParseXml(string xml, FigureDataFormat format)
    {
        ArgumentNullException.ThrowIfNull(xml);

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using StringReader textReader = new(xml);
        using XmlReader xmlReader = XmlReader.Create(textReader, settings);
        XDocument document = XDocument.Load(xmlReader, LoadOptions.None);
        XElement root = document.Root ?? throw new FormatException("Figure data has no root element.");

        if (root.Name.LocalName != "figuredata")
            throw new FormatException($"Expected figuredata root but found '{root.Name.LocalName}'.");

        List<FigurePalette> palettes = [];
        XElement? colors = child(root, "colors");
        if (colors is not null)
        {
            foreach (XElement paletteElement in children(colors, "palette"))
                palettes.Add(parse_palette(paletteElement));
        }

        List<FigureSetType> setTypes = [];
        XElement? sets = child(root, "sets");
        if (sets is not null)
        {
            foreach (XElement setTypeElement in children(sets, "settype"))
                setTypes.Add(parse_set_type(setTypeElement, format));
        }

        return new FigureData(format, palettes, setTypes);
    }

    /// <summary>Gets the palette with the specified id.</summary>
    /// <param name="id">The palette id.</param>
    /// <returns>The palette, or <see langword="null"/> when no palette has the id.</returns>
    public FigurePalette? GetPalette(int id) => _palettesById.GetValueOrDefault(id);

    /// <summary>Gets the set type for a part type.</summary>
    /// <param name="type">The part type.</param>
    /// <returns>The set type, or <see langword="null"/> when the part type is not in the figure data.</returns>
    public FigureSetType? GetSetType(FigurePartType type) => _setTypesByType.GetValueOrDefault(type);

    /// <summary>Gets the palette a part type is colored from, following its set type's palette id.</summary>
    /// <param name="type">The part type.</param>
    /// <returns>The palette, or <see langword="null"/> when the set type or its palette is unknown.</returns>
    public FigurePalette? GetPalette(FigurePartType type) =>
        GetSetType(type) is { } setType ? GetPalette(setType.PaletteId) : null;

    /// <summary>Gets the part set with the specified id, searching every set type in order.</summary>
    /// <param name="id">The set id.</param>
    /// <returns>The first matching set, or <see langword="null"/> when no set type has the id.</returns>
    public FigurePartSet? GetSet(int id)
    {
        foreach (FigureSetType setType in _setTypes)
        {
            FigurePartSet? set = setType.GetSet(id);
            if (set is not null)
                return set;
        }

        return null;
    }

    /// <summary>Gets the part set with the specified id and the set type that holds it.</summary>
    /// <param name="id">The set id.</param>
    /// <param name="setType">The set type that holds the set, or <see langword="null"/> when no set matches.</param>
    /// <returns>The first matching set, or <see langword="null"/> when no set type has the id.</returns>
    public FigurePartSet? GetSet(int id, out FigureSetType? setType)
    {
        foreach (FigureSetType candidate in _setTypes)
        {
            FigurePartSet? set = candidate.GetSet(id);
            if (set is not null)
            {
                setType = candidate;
                return set;
            }
        }

        setType = null;
        return null;
    }

    /// <summary>Gets the default set of a part type for a gender.</summary>
    /// <param name="type">The part type.</param>
    /// <param name="gender">The gender the set must be valid for.</param>
    /// <returns>The default set as chosen by <see cref="FigureSetType.GetDefaultSet"/>, or <see langword="null"/> when there is none.</returns>
    public FigurePartSet? GetDefaultSet(FigurePartType type, FigureGender gender) =>
        GetSetType(type)?.GetDefaultSet(gender);

    /// <summary>Gets whether a part set may be worn by the given gender.</summary>
    /// <param name="id">The set id.</param>
    /// <param name="gender">The gender to check.</param>
    /// <returns><see langword="true"/> if the set exists and is valid for <paramref name="gender"/>; otherwise, <see langword="false"/>.</returns>
    public bool IsValidSetForGender(int id, FigureGender gender) =>
        GetSet(id)?.IsValidForGender(gender) ?? false;

    /// <summary>Gets the part types a figure must contain for a gender at a club level.</summary>
    /// <param name="gender">The gender. Only <see cref="FigureGender.Male"/> and <see cref="FigureGender.Female"/> have mandatory types.</param>
    /// <param name="clubLevel">The club level. Any level of 1 or more counts as club, a negative level has no mandatory types.</param>
    /// <returns>The mandatory part types, in figure data order.</returns>
    public IReadOnlyList<FigurePartType> GetMandatorySetTypes(FigureGender gender, int clubLevel) =>
        Array.AsReadOnly(_setTypes
            .Where(setType => setType.IsMandatory(gender, clubLevel))
            .Select(setType => setType.Type)
            .ToArray());

    /// <summary>Infers the gender from the part sets the figure selects.</summary>
    /// <param name="figure">The figure to inspect.</param>
    /// <returns>
    /// The gender of the gendered sets, or <see cref="FigureGender.Undefined"/> when no selected set is
    /// gendered or when the selections contradict each other.
    /// </returns>
    /// <remarks>
    /// Figure strings do not carry a gender, so the result is only an inference from figure data.
    /// Unisex and unknown sets are ignored. The authoritative gender always arrives next to the
    /// figure on the wire.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="figure"/> is <see langword="null"/>.</exception>
    public FigureGender InferGender(Figure figure)
    {
        ArgumentNullException.ThrowIfNull(figure);
        FigureGender inferred = FigureGender.Undefined;

        foreach (FigurePart part in figure.Parts)
        {
            FigurePartSet? set = GetSetType(part.Type)?.GetSet(part.SetId);
            if (set is null || set.Gender is FigureGender.Unisex or FigureGender.Undefined)
                continue;

            if (inferred == FigureGender.Undefined)
                inferred = set.Gender;
            else if (inferred != set.Gender)
                return FigureGender.Undefined;
        }

        return inferred;
    }

    /// <summary>
    /// Completes a figure for a gender the way the client repairs a figure before rendering.
    /// </summary>
    /// <param name="figure">The figure to complete.</param>
    /// <param name="gender">The gender the figure is worn as.</param>
    /// <returns>The completed figure, whether it needed repair, and the part types that were replaced.</returns>
    /// <remarks>
    /// The figure is normalized with <see cref="Figure.Normalize"/> first. Every part type that is
    /// mandatory for the gender with club and is missing or references an unknown set is replaced by
    /// the default set for that gender with color id 0. Part types without a default set are left as they are.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="figure"/> is <see langword="null"/>.</exception>
    public FigureValidation Validate(Figure figure, FigureGender gender)
    {
        ArgumentNullException.ThrowIfNull(figure);

        Figure current = figure.Normalize();
        List<FigurePartType> repaired = [];

        foreach (FigureSetType setType in _setTypes)
        {
            if (!setType.IsMandatory(gender, ValidationClubLevel))
                continue;

            FigurePart? selected = current.FindLastPart(setType.Type);
            if (selected is not null && setType.GetSet(selected.SetId) is not null)
                continue;

            FigurePartSet? defaultSet = setType.GetDefaultSet(gender);
            if (defaultSet is null)
                continue;

            current = current.SetPart(new FigurePart(setType.Type, defaultSet.Id, [0]));
            repaired.Add(setType.Type);
        }

        return new FigureValidation(current, repaired.Count == 0, repaired.AsReadOnly());
    }

    /// <summary>Gets the club level required to wear a figure.</summary>
    /// <param name="figure">The figure to inspect.</param>
    /// <param name="gender">The gender the figure is worn as.</param>
    /// <param name="partTypes">The part types the figure may omit only at a sufficient club level, or <see langword="null"/> to skip that check.</param>
    /// <returns>The required club level, 0 when no club is needed.</returns>
    /// <remarks>
    /// The level is the highest club level across the selected sets and their colors, raised by
    /// the club level at which any of <paramref name="partTypes"/> that the figure omits becomes
    /// optional. Unknown sets, palettes and colors are ignored.
    /// The client passes the part types explicitly, for example the mannequin widget passes
    /// its clothing part types. Its own fallback reads body part ids out of the avatar
    /// geometry, which are not figure data set types, so no fallback is applied here.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="figure"/> is <see langword="null"/>.</exception>
    public int ResolveClubLevel(
        Figure figure,
        FigureGender gender,
        IEnumerable<FigurePartType>? partTypes = null)
    {
        ArgumentNullException.ThrowIfNull(figure);
        int level = 0;

        foreach (FigurePart part in figure.Parts)
        {
            FigureSetType? setType = GetSetType(part.Type);
            FigurePartSet? set = setType?.GetSet(part.SetId);
            if (setType is null || set is null)
                continue;

            level = Math.Max(level, set.ClubLevel);
            FigurePalette? palette = GetPalette(setType.PaletteId);
            if (palette is null)
                continue;

            foreach (int colorId in part.ColorIds)
            {
                FigureColor? color = palette.GetColor(colorId);
                if (color is not null)
                    level = Math.Max(level, color.ClubLevel);
            }
        }

        if (partTypes is null)
            return level;

        foreach (FigurePartType type in partTypes)
        {
            if (figure.HasPartType(type))
                continue;

            FigureSetType? setType = GetSetType(type);
            if (setType is not null)
                level = Math.Max(level, setType.OptionalFromClubLevel(gender));
        }

        return level;
    }

    /// <summary>Resolves every part of a figure to its set type, part set and palette colors.</summary>
    /// <param name="figure">The figure to resolve.</param>
    /// <returns>One resolved part per figure part, in figure order. Unknown set types, sets and colors resolve to <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="figure"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<ResolvedFigurePart> Resolve(Figure figure)
    {
        ArgumentNullException.ThrowIfNull(figure);
        ResolvedFigurePart[] resolved = new ResolvedFigurePart[figure.Parts.Count];

        for (int index = 0; index < figure.Parts.Count; index++)
        {
            FigurePart selection = figure.Parts[index];
            FigureSetType? setType = GetSetType(selection.Type);
            FigurePartSet? set = setType?.GetSet(selection.SetId);
            FigurePalette? palette = setType is null ? null : GetPalette(setType.PaletteId);
            ResolvedFigureColor[] resolvedColors = new ResolvedFigureColor[selection.ColorIds.Count];

            for (int colorIndex = 0; colorIndex < selection.ColorIds.Count; colorIndex++)
            {
                int colorId = selection.ColorIds[colorIndex];
                resolvedColors[colorIndex] = new ResolvedFigureColor(colorId, palette?.GetColor(colorId));
            }

            resolved[index] = new ResolvedFigurePart(
                selection,
                setType,
                set,
                Array.AsReadOnly(resolvedColors));
        }

        return Array.AsReadOnly(resolved);
    }

    /// <summary>Serializes the palettes and set types to a <c>figuredata</c> XML string without formatting.</summary>
    /// <returns>The XML text, which <see cref="ParseXml"/> can read back.</returns>
    public string ToXml()
    {
        XElement colors = new("colors",
            _palettes.Select(palette => new XElement("palette",
                new XAttribute("id", format(palette.Id)),
                palette.Colors.Select(color => new XElement("color",
                    new XAttribute("id", format(color.Id)),
                    new XAttribute("index", format(color.Index)),
                    new XAttribute("club", format(color.ClubLevel)),
                    new XAttribute("selectable", format(color.IsSelectable)),
                    color.Rgb.ToString("X6", CultureInfo.InvariantCulture))))));

        XElement sets = new("sets", _setTypes.Select(compose_set_type));
        return new XDocument(new XElement("figuredata", colors, sets))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static FigurePalette parse_palette(XElement element)
    {
        int id = required_non_negative_int(element, "id");
        List<FigureColor> colors = [];

        foreach (XElement colorElement in children(element, "color"))
        {
            string rgbValue = required_attribute(colorElement, null);
            if (rgbValue.Length is < 1 or > 6 ||
                !uint.TryParse(rgbValue, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint rgb))
            {
                throw new FormatException($"Invalid RGB value '{rgbValue}'.");
            }

            colors.Add(new FigureColor(
                required_non_negative_int(colorElement, "id"),
                required_non_negative_int(colorElement, "index"),
                required_non_negative_int(colorElement, "club"),
                required_bool(colorElement, "selectable"),
                rgb));
        }

        return new FigurePalette(id, colors);
    }

    private static FigureSetType parse_set_type(XElement element, FigureDataFormat format)
    {
        FigurePartType type = FigurePartType.Parse(required_attribute(element, "type"));
        List<FigurePartSet> sets = [];

        foreach (XElement setElement in children(element, "set"))
            sets.Add(parse_part_set(setElement, type, format));

        return new FigureSetType(
            type,
            required_non_negative_int(element, "paletteid"),
            required_bool(element, "mand_f_0"),
            required_bool(element, "mand_f_1"),
            required_bool(element, "mand_m_0"),
            required_bool(element, "mand_m_1"),
            sets);
    }

    private static FigurePartSet parse_part_set(
        XElement element,
        FigurePartType setType,
        FigureDataFormat format)
    {
        List<FigureSetPart> parts = [];
        foreach (XElement partElement in children(element, "part"))
        {
            FigureSetPart part = new(
                required_non_negative_int(partElement, "id"),
                FigurePartType.Parse(required_attribute(partElement, "type")),
                required_non_negative_int(partElement, "index"),
                required_non_negative_int(partElement, "colorindex"),
                optional_non_negative_int(partElement, "palettemapid"),
                optional_attribute(partElement, "breed"),
                optional_bool(partElement, "colorable"));

            if (format == FigureDataFormat.Flash)
                insert_flash_part(parts, part);
            else
                parts.Add(part);
        }

        List<FigurePartType> hiddenLayers = [];
        XElement? hiddenLayerElement = child(element, "hiddenlayers");
        if (hiddenLayerElement is not null)
        {
            foreach (XElement layer in children(hiddenLayerElement, "layer"))
                hiddenLayers.Add(FigurePartType.Parse(required_attribute(layer, "parttype")));
        }

        FigureGender gender = FigureGenderCode.Parse(required_attribute(element, "gender"));
        if (format == FigureDataFormat.Flash)
        {
            foreach (FigureSetPart part in parts)
            {
                if (part.Breed is not null && part.BreedId is null)
                    throw new FormatException($"Flash figure breed '{part.Breed}' is not numeric.");
            }
        }

        return new FigurePartSet(
            setType,
            required_non_negative_int(element, "id"),
            gender,
            required_non_negative_int(element, "club"),
            required_bool(element, "colorable"),
            required_bool(element, "selectable"),
            required_bool(element, "preselectable"),
            required_bool(element, "sellable"),
            parts,
            hiddenLayers);
    }

    private static void insert_flash_part(List<FigureSetPart> parts, FigureSetPart part)
    {
        for (int index = 0; index < parts.Count; index++)
        {
            FigureSetPart existing = parts[index];
            if (existing.Type == part.Type && existing.Index < part.Index)
            {
                parts.Insert(index, part);
                return;
            }
        }

        parts.Add(part);
    }

    private static XElement compose_set_type(FigureSetType setType) =>
        new("settype",
            new XAttribute("type", setType.Type.Value),
            new XAttribute("paletteid", format(setType.PaletteId)),
            new XAttribute("mand_f_0", format(setType.IsMandatoryForFemaleWithoutClub)),
            new XAttribute("mand_f_1", format(setType.IsMandatoryForFemaleWithClub)),
            new XAttribute("mand_m_0", format(setType.IsMandatoryForMaleWithoutClub)),
            new XAttribute("mand_m_1", format(setType.IsMandatoryForMaleWithClub)),
            setType.Sets.Select(compose_part_set));

    private static XElement compose_part_set(FigurePartSet set)
    {
        XElement element = new("set",
            new XAttribute("id", format(set.Id)),
            new XAttribute("gender", FigureGenderCode.Compose(set.Gender)),
            new XAttribute("club", format(set.ClubLevel)),
            new XAttribute("colorable", format(set.IsColorable)),
            new XAttribute("selectable", format(set.IsSelectable)),
            new XAttribute("preselectable", format(set.IsPreSelectable)),
            new XAttribute("sellable", format(set.IsSellable)));

        foreach (FigureSetPart part in set.Parts)
        {
            XElement partElement = new("part",
                new XAttribute("id", format(part.Id)),
                new XAttribute("type", part.Type.Value),
                new XAttribute("index", format(part.Index)),
                new XAttribute("colorindex", format(part.ColorIndex)));

            if (part.PaletteMapId is int paletteMapId)
                partElement.Add(new XAttribute("palettemapid", format(paletteMapId)));
            if (part.Breed is not null)
                partElement.Add(new XAttribute("breed", part.Breed));
            if (part.IsColorable is bool isColorable)
                partElement.Add(new XAttribute("colorable", format(isColorable)));

            element.Add(partElement);
        }

        if (set.HiddenLayers.Count > 0)
        {
            element.Add(new XElement("hiddenlayers",
                set.HiddenLayers.Select(type =>
                    new XElement("layer", new XAttribute("parttype", type.Value)))));
        }

        return element;
    }

    private static IEnumerable<XElement> children(XElement parent, string name) =>
        parent.Elements().Where(element => element.Name.LocalName == name);

    private static XElement? child(XElement parent, string name) =>
        children(parent, name).FirstOrDefault();

    private static string required_attribute(XElement element, string? name)
    {
        string? value = name is null
            ? element.Value
            : element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value;

        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException(name is null
                ? $"{element.Name.LocalName} requires a value."
                : $"{element.Name.LocalName} requires attribute '{name}'.");

        return value;
    }

    private static string? optional_attribute(XElement element, string name)
    {
        string? value = element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static int required_non_negative_int(XElement element, string name)
    {
        string value = required_attribute(element, name);
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result))
            throw new FormatException($"{element.Name.LocalName}.{name} has invalid integer '{value}'.");
        return result;
    }

    private static int? optional_non_negative_int(XElement element, string name)
    {
        string? value = optional_attribute(element, name);
        if (value is null)
            return null;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result))
            throw new FormatException($"{element.Name.LocalName}.{name} has invalid integer '{value}'.");
        return result;
    }

    private static bool required_bool(XElement element, string name)
    {
        string value = required_attribute(element, name);
        return parse_bool(element, name, value);
    }

    private static bool? optional_bool(XElement element, string name)
    {
        string? value = optional_attribute(element, name);
        return value is null ? null : parse_bool(element, name, value);
    }

    private static bool parse_bool(XElement element, string name, string value) =>
        value.ToLowerInvariant() switch
        {
            "0" or "false" => false,
            "1" or "true" => true,
            _ => throw new FormatException($"{element.Name.LocalName}.{name} has invalid boolean '{value}'.")
        };

    private static string format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string format(bool value) => value ? "1" : "0";
}
