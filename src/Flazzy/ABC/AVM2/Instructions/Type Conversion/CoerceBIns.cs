namespace Flazzy.ABC.AVM2.Instructions;

public sealed class CoerceBIns : PrimitiveCoerceIns
{
    public CoerceBIns()
        : base(OPCode.Coerce_b)
    {
    }

    protected override object Coerce(object value) => value switch
    {
        bool boolean => boolean,
        string text => text.Length > 0,
        double number => number != 0 && !double.IsNaN(number),
        float number => number != 0 && !float.IsNaN(number),
        decimal number => number != 0,
        sbyte number => number != 0,
        byte number => number != 0,
        short number => number != 0,
        ushort number => number != 0,
        int number => number != 0,
        uint number => number != 0,
        long number => number != 0,
        ulong number => number != 0,
        _ => true
    };
}
