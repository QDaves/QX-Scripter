namespace Flazzy.ABC.AVM2.Instructions;

public abstract class MemoryStoreIns : ASInstruction
{
    protected MemoryStoreIns(OPCode op)
        : base(op)
    {
    }

    public override int GetPopCount() => 2;

    public override void Execute(ASMachine machine)
    {
        machine.Values.Pop();
        machine.Values.Pop();
    }
}
