using System.Collections.ObjectModel;
using System.Globalization;

namespace Qx.Model.Figures;

public sealed class Figure : IEquatable<Figure>
{
    private readonly FigurePart[] _parts;
    private readonly ReadOnlyCollection<FigurePart> _readOnlyParts;

    public static Figure Empty { get; } = new([]);

    public IReadOnlyList<FigurePart> Parts => _readOnlyParts;

    public Figure(IEnumerable<FigurePart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        _parts = parts.ToArray();

        if (_parts.Any(static part => part is null))
            throw new ArgumentException("Figure parts cannot contain null values.", nameof(parts));

        _readOnlyParts = Array.AsReadOnly(_parts);
    }

    public static Figure Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!try_parse(value, out Figure? figure, out string? error))
            throw new FormatException(error);
        return figure;
    }

    public static bool TryParse(string? value, out Figure figure)
    {
        if (value is null || !try_parse(value, out Figure? parsed, out _))
        {
            figure = Empty;
            return false;
        }

        figure = parsed;
        return true;
    }

    /// <summary>The distinct part types in the figure, in first-occurrence order.</summary>
    public IReadOnlyList<FigurePartType> PartTypes
    {
        get
        {
            List<FigurePartType> types = [];
            HashSet<FigurePartType> seen = [];

            foreach (FigurePart part in _parts)
            {
                if (seen.Add(part.Type))
                    types.Add(part.Type);
            }

            return types.AsReadOnly();
        }
    }

    public bool HasPartType(FigurePartType type) => _parts.Any(part => part.Type == type);

    public IReadOnlyList<FigurePart> FindParts(FigurePartType type) =>
        Array.AsReadOnly(_parts.Where(part => part.Type == type).ToArray());

    public FigurePart? FindLastPart(FigurePartType type)
    {
        for (int index = _parts.Length - 1; index >= 0; index--)
        {
            if (_parts[index].Type == type)
                return _parts[index];
        }

        return null;
    }

    public Figure Add(FigurePart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return new Figure(_parts.Append(part));
    }

    public Figure SetPart(FigurePart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return new Figure(_parts.Where(existing => existing.Type != part.Type).Append(part));
    }

    public Figure RemoveParts(FigurePartType type) =>
        new(_parts.Where(part => part.Type != type));

    /// <summary>
    /// Collapses repeated part types the way the client's figure container does: the last
    /// occurrence of a type wins and takes the position of that last occurrence.
    /// </summary>
    public Figure Normalize()
    {
        List<FigurePart> parts = [];

        foreach (FigurePart part in _parts)
        {
            parts.RemoveAll(existing => existing.Type == part.Type);
            parts.Add(part);
        }

        return parts.Count == _parts.Length ? this : new Figure(parts);
    }

    public override string ToString() => string.Join(".", _parts.Select(static part => part.ToString()));

    public bool Equals(Figure? other) =>
        other is not null &&
        _parts.AsSpan().SequenceEqual(other._parts);

    public override bool Equals(object? obj) => obj is Figure other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (FigurePart part in _parts)
            hash.Add(part);
        return hash.ToHashCode();
    }

    private static bool try_parse(string value, out Figure figure, out string? error)
    {
        if (value.Length == 0)
        {
            figure = Empty;
            error = null;
            return true;
        }

        string[] rawParts = value.Split('.');
        FigurePart[] parts = new FigurePart[rawParts.Length];

        for (int partIndex = 0; partIndex < rawParts.Length; partIndex++)
        {
            string rawPart = rawParts[partIndex];
            string[] tokens = rawPart.Split('-');

            if (tokens.Length < 2)
            {
                figure = Empty;
                error = $"Figure part {partIndex} must contain a type and set ID.";
                return false;
            }

            if (!FigurePartType.TryParse(tokens[0], out FigurePartType type))
            {
                figure = Empty;
                error = $"Figure part {partIndex} has an invalid type.";
                return false;
            }

            if (!try_parse_id(tokens[1], out int setId))
            {
                figure = Empty;
                error = $"Figure part {partIndex} has an invalid set ID.";
                return false;
            }

            int[] colorIds = new int[tokens.Length - 2];
            for (int colorIndex = 0; colorIndex < colorIds.Length; colorIndex++)
            {
                if (!try_parse_id(tokens[colorIndex + 2], out colorIds[colorIndex]))
                {
                    figure = Empty;
                    error = $"Figure part {partIndex} has an invalid color ID at index {colorIndex}.";
                    return false;
                }
            }

            parts[partIndex] = new FigurePart(type, setId, colorIds);
        }

        figure = new Figure(parts);
        error = null;
        return true;
    }

    private static bool try_parse_id(string value, out int id)
    {
        if (value.Length > 0 &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            return true;
        }

        id = 0;
        return false;
    }
}
