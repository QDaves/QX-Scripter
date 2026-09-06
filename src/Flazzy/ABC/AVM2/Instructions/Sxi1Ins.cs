namespace Flazzy.ABC.AVM2.Instructions;

public sealed class Sxi1Ins : SignExtendIns
{
    public Sxi1Ins()
        : base(OPCode.Sxi1)
    {
    }

    protected override int Extend(int value) => (value << 31) >> 31;
}
