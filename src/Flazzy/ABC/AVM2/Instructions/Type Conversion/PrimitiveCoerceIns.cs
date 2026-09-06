namespace Flazzy.ABC.AVM2.Instructions;

public abstract class PrimitiveCoerceIns : ASInstruction
{
    protected PrimitiveCoerceIns(OPCode op)
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
            machine.Values.Push(Coerce(value));
        }
        catch
        {
            machine.Values.Push(null);
        }
    }

    protected abstract object Coerce(object value);
}
