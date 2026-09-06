using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class PushShortIns : Primitive
{
    private uint _raw_value;
    public uint RawValue
    {
        get => _raw_value;
        set
        {
            _raw_value = value;
            base.Value = unchecked((short)value);
        }
    }
    new public short Value => unchecked((short)_raw_value);

    public PushShortIns()
        : base(OPCode.PushShort)
    { }
    public PushShortIns(short value)
        : this()
    {
        RawValue = unchecked((ushort)value);
    }
    public PushShortIns(int raw_value)
        : this()
    {
        RawValue = unchecked((uint)raw_value);
    }
    public PushShortIns(uint raw_value)
        : this()
    {
        RawValue = raw_value;
    }
    public PushShortIns(ref SpanFlashReader input)
        : this()
    {
        RawValue = input.ReadEncodedUInt();
    }

    protected override int GetBodySize()
    {
        return SpanFlashWriter.GetEncodedUIntSize(RawValue);
    }
    protected override void WriteValuesTo(ref SpanFlashWriter output)
    {
        output.WriteEncodedUInt(RawValue);
    }
}
