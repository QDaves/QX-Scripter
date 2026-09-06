namespace Qx;

public readonly record struct Id : IComparable<Id>, IComparable
{
    public const long MinValue = long.MinValue;
    public const long MaxValue = long.MaxValue;

    private readonly long _value;
    private Id(long value) => _value = value;

    public static implicit operator long(Id id) => id._value;
    public static implicit operator Id(long value) => new(value);

    public static explicit operator Id(string s)
    {
        if (!long.TryParse(s, out long value))
            throw new Exception($"Invalid ID: {s}");
        return new Id(value);
    }

    public static bool TryParse(string? s, out Id id)
    {
        if (!long.TryParse(s, out long value))
        {
            id = 0;
            return false;
        }
        id = value;
        return true;
    }

    public override string ToString() => _value.ToString();

    public int CompareTo(Id other) => _value.CompareTo(other._value);

    public int CompareTo(object? obj)
    {
        if (obj is not Id other)
            throw new ArgumentException($"Object must be of type {typeof(Id).FullName}.", nameof(obj));
        return CompareTo(other);
    }
}
