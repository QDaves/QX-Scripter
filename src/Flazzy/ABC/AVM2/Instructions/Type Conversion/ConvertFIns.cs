namespace Flazzy.ABC.AVM2.Instructions;

public sealed class ConvertFIns : ASInstruction
{
    public ConvertFIns()
        : base(OPCode.Convert_f)
    {
    }

    public override int GetPopCount() => 1;
    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine)
    {
        object? value = machine.Values.Pop();
        machine.Values.Push(Convert.ToSingle(value));
    }
}
