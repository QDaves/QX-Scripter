using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class DxnsIns : ASInstruction
{
    public int UriIndex { get; set; }
    public string Uri => GetString(UriIndex, nameof(Uri));

    public DxnsIns(ABCFile abc)
        : base(OPCode.Dxns, abc)
    { }
    public DxnsIns(ABCFile abc, ref SpanFlashReader input)
        : this(abc)
    {
        UriIndex = input.ReadEncodedInt();
    }

    protected override int GetBodySize()
    {
        return SpanFlashWriter.GetEncodedIntSize(UriIndex);
    }
    protected override void WriteValuesTo(ref SpanFlashWriter output)
    {
        output.WriteEncodedInt(UriIndex);
    }
}