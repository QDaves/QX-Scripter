using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class IsTypeIns : ASInstruction
{
    public int TypeNameIndex { get; set; }
    public ASMultiname TypeName => GetMultiname(TypeNameIndex, nameof(TypeName));

    public IsTypeIns(ABCFile abc)
        : base(OPCode.IsType, abc)
    {
    }

    public IsTypeIns(ABCFile abc, ref SpanFlashReader input)
        : this(abc)
    {
        TypeNameIndex = input.ReadEncodedInt();
    }

    public IsTypeIns(ABCFile abc, int type_name_index)
        : this(abc)
    {
        TypeNameIndex = type_name_index;
    }

    public override int GetPopCount() => 1;
    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine)
    {
        machine.Values.Pop();
        machine.Values.Push(null);
    }

    protected override int GetBodySize() => SpanFlashWriter.GetEncodedIntSize(TypeNameIndex);

    protected override void WriteValuesTo(ref SpanFlashWriter output) => output.WriteEncodedInt(TypeNameIndex);
}
