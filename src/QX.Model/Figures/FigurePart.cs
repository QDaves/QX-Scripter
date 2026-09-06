using System.Collections.ObjectModel;
using System.Globalization;

namespace Qx.Model.Figures;

public sealed class FigurePart : IEquatable<FigurePart>
{
    private readonly int[] _colorIds;
    private readonly ReadOnlyCollection<int> _colors;

    public FigurePartType Type { get; }
    public int SetId { get; }
    public IReadOnlyList<int> ColorIds => _colors;

    public FigurePart(FigurePartType type, int setId, IEnumerable<int>? colorIds = null)
    {
        if (type == default)
            throw new ArgumentException("A figure part type is required.", nameof(type));
        if (setId < 0)
            throw new ArgumentOutOfRangeException(nameof(setId));

        Type = type;
        SetId = setId;
        _colorIds = colorIds?.ToArray() ?? [];

        if (_colorIds.Any(static colorId => colorId < 0))
            throw new ArgumentOutOfRangeException(nameof(colorIds), "Figure color IDs cannot be negative.");

        _colors = Array.AsReadOnly(_colorIds);
    }

    public override string ToString()
    {
        string value = string.Concat(Type.Value, "-", SetId.ToString(CultureInfo.InvariantCulture));
        return _colorIds.Length == 0
            ? value
            : string.Concat(value, "-", string.Join("-", _colorIds));
    }

    public bool Equals(FigurePart? other) =>
        other is not null &&
        Type == other.Type &&
        SetId == other.SetId &&
        _colorIds.AsSpan().SequenceEqual(other._colorIds);

    public override bool Equals(object? obj) => obj is FigurePart other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Type);
        hash.Add(SetId);
        foreach (int colorId in _colorIds)
            hash.Add(colorId);
        return hash.ToHashCode();
    }
}
