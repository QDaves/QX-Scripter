using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class GetOuterScopeIns : ASInstruction
{
    public int ScopeIndex { get; set; }

    public GetOuterScopeIns()
        : base(OPCode.GetOuterScope)
    {
    }

    public GetOuterScopeIns(int scope_index)
        : this()
    {
        ScopeIndex = scope_index;
    }

    public GetOuterScopeIns(ref SpanFlashReader input)
        : this()
    {
        ScopeIndex = input.ReadEncodedInt();
    }

    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine) => machine.Values.Push(null);

    protected override int GetBodySize() => SpanFlashWriter.GetEncodedIntSize(ScopeIndex);

    protected override void WriteValuesTo(ref SpanFlashWriter output) => output.WriteEncodedInt(ScopeIndex);
}
