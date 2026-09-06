using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class BkptLineIns : ASInstruction
{
    public int LineNumber { get; set; }

    public BkptLineIns()
        : base(OPCode.BkptLine)
    {
    }

    public BkptLineIns(int line_number)
        : this()
    {
        LineNumber = line_number;
    }

    public BkptLineIns(ref SpanFlashReader input)
        : this()
    {
        LineNumber = input.ReadEncodedInt();
    }

    protected override int GetBodySize() => SpanFlashWriter.GetEncodedIntSize(LineNumber);

    protected override void WriteValuesTo(ref SpanFlashWriter output) => output.WriteEncodedInt(LineNumber);
}
