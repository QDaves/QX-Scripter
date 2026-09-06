namespace Flazzy.ABC.AVM2.Instructions;

public abstract class SignExtendIns : ASInstruction
{
    protected SignExtendIns(OPCode op)
        : base(op)
    {
    }

    public override int GetPopCount() => 1;
    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine)
    {
        object? value = machine.Values.Pop();
        if (value is null)
        {
            machine.Values.Push(null);
            return;
        }

        try
        {
            machine.Values.Push(Extend(Convert.ToInt32(value)));
        }
        catch
        {
            machine.Values.Push(null);
        }
    }

    protected abstract int Extend(int value);
}
