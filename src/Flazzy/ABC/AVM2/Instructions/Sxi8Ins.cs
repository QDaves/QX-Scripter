namespace Flazzy.ABC.AVM2.Instructions;

public sealed class Sxi8Ins : SignExtendIns
{
    public Sxi8Ins()
        : base(OPCode.Sxi8)
    {
    }

    protected override int Extend(int value) => unchecked((sbyte)value);
}
