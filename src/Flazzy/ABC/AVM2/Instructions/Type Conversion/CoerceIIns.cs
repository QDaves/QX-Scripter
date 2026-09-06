namespace Flazzy.ABC.AVM2.Instructions;

public sealed class CoerceIIns : PrimitiveCoerceIns
{
    public CoerceIIns()
        : base(OPCode.Coerce_i)
    {
    }

    protected override object Coerce(object value) => Convert.ToInt32(value);
}
