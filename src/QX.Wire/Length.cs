namespace Qx;

public readonly record struct Length : IComparable<Length>, IComparable
{
    private readonly ushort _value;
    private Length(ushort value) => _value = value;

    public static implicit operator ushort(Length length) => length._value;
    public static implicit operator Length(ushort value) => new(value);
    public static explicit operator Length(int value) => new(checked((ushort)value));

    public override string ToString() => _value.ToString();

    public int CompareTo(Length other) => _value.CompareTo(other._value);

    public int CompareTo(object? obj)
    {
        if (obj is not Length other)
            throw new ArgumentException($"Object must be of type {typeof(Length).FullName}.", nameof(obj));
        return CompareTo(other);
    }
}
