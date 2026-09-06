namespace Flazzy.ABC.AVM2.Instructions;

public sealed class Sxi16Ins : SignExtendIns
{
    public Sxi16Ins()
        : base(OPCode.Sxi16)
    {
    }

    protected override int Extend(int value) => unchecked((short)value);
}
