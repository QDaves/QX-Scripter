namespace Flazzy.ABC.AVM2.Instructions;

public sealed class UnPlusIns : ASInstruction
{
    public UnPlusIns()
        : base(OPCode.UnPlus)
    {
    }

    public override int GetPopCount() => 1;
    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine)
    {
        object? value = machine.Values.Pop();
        machine.Values.Push(value is float or ASFloat4 ? value : Convert.ToDouble(value));
    }
}
