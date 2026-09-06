namespace Flazzy.ABC.AVM2.Instructions;

public sealed class ConvertF4Ins : ASInstruction
{
    public ConvertF4Ins()
        : base(OPCode.Convert_f4)
    {
    }

    public override int GetPopCount() => 1;
    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine)
    {
        object? value = machine.Values.Pop();
        if (value is ASFloat4 float4)
        {
            machine.Values.Push(float4);
            return;
        }

        float scalar = Convert.ToSingle(value);
        machine.Values.Push(new ASFloat4(scalar, scalar, scalar, scalar));
    }
}
