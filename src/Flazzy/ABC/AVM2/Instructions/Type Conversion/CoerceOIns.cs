namespace Flazzy.ABC.AVM2.Instructions;

public sealed class CoerceOIns : PrimitiveCoerceIns
{
    public CoerceOIns()
        : base(OPCode.Coerce_o)
    {
    }

    protected override object Coerce(object value) => value;
}
