namespace Flazzy.ABC.AVM2.Instructions;

public sealed class CoerceDIns : PrimitiveCoerceIns
{
    public CoerceDIns()
        : base(OPCode.Coerce_d)
    {
    }

    protected override object Coerce(object value) => Convert.ToDouble(value);
}
