namespace Flazzy.ABC;

public sealed class ASUndefined
{
    public static ASUndefined Value { get; } = new();

    private ASUndefined()
    {
    }

    public override string ToString() => "undefined";
}
