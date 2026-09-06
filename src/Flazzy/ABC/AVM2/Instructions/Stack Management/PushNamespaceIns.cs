using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class PushNamespaceIns : ASInstruction
{
    public int NamespaceIndex { get; set; }
    public ASNamespace Namespace => GetNamespace(NamespaceIndex, nameof(Namespace));

    public PushNamespaceIns(ABCFile abc)
        : base(OPCode.PushNamespace, abc)
    {
    }

    public PushNamespaceIns(ABCFile abc, ref SpanFlashReader input)
        : this(abc)
    {
        NamespaceIndex = input.ReadEncodedInt();
    }

    public PushNamespaceIns(ABCFile abc, int namespace_index)
        : this(abc)
    {
        NamespaceIndex = namespace_index;
    }

    public override int GetPushCount() => 1;

    public override void Execute(ASMachine machine) => machine.Values.Push(Namespace);

    protected override int GetBodySize() => SpanFlashWriter.GetEncodedIntSize(NamespaceIndex);

    protected override void WriteValuesTo(ref SpanFlashWriter output) => output.WriteEncodedInt(NamespaceIndex);
}
