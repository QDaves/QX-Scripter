namespace Flazzy.ABC.AVM2.Instructions;

public abstract class MemoryLoadIns : ASInstruction
{
    protected MemoryLoadIns(OPCode op)
        : base(op)
    {
    }

    public override int GetPopCount() => 1;
    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine)
    {
        machine.Values.Pop();
        machine.Values.Push(null);
    }
}
