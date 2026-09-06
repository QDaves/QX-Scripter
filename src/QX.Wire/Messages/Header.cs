namespace Qx.Messages;

public readonly record struct Header(Direction Direction, short Value)
{
    public static readonly Header Unknown = new();

    public static readonly Header All = new(Direction.Both, 0);

    public static implicit operator Header((Direction direction, short value) x) => new(x.direction, x.value);

    public static implicit operator ReadOnlySpan<Header>(in Header header) => new(in header);
}
