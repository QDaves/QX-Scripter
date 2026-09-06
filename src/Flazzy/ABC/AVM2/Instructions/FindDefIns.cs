using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class FindDefIns : ASInstruction
{
    public int DefinitionNameIndex { get; set; }
    public ASMultiname DefinitionName => GetMultiname(DefinitionNameIndex, nameof(DefinitionName));

    public FindDefIns(ABCFile abc)
        : base(OPCode.FindDef, abc)
    {
    }

    public FindDefIns(ABCFile abc, int definition_name_index)
        : this(abc)
    {
        DefinitionNameIndex = definition_name_index;
    }

    public FindDefIns(ABCFile abc, ref SpanFlashReader input)
        : this(abc)
    {
        DefinitionNameIndex = input.ReadEncodedInt();
    }

    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine) => machine.Values.Push(null);

    protected override int GetBodySize() => SpanFlashWriter.GetEncodedIntSize(DefinitionNameIndex);

    protected override void WriteValuesTo(ref SpanFlashWriter output) => output.WriteEncodedInt(DefinitionNameIndex);
}
