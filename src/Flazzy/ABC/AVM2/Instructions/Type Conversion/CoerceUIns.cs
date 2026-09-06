namespace Flazzy.ABC.AVM2.Instructions;

public sealed class CoerceUIns : PrimitiveCoerceIns
{
    public CoerceUIns()
        : base(OPCode.Coerce_u)
    {
    }

    protected override object Coerce(object value) => unchecked((uint)Convert.ToDouble(value));
}
