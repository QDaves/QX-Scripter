using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class PushByteIns : Primitive
{
    private byte _raw_value;
    public byte RawValue
    {
        get => _raw_value;
        set
        {
            _raw_value = value;
            base.Value = unchecked((sbyte)value);
        }
    }
    new public sbyte Value => unchecked((sbyte)_raw_value);

    public PushByteIns()
        : base(OPCode.PushByte)
    { }
    public PushByteIns(sbyte value)
        : this()
    {
        RawValue = unchecked((byte)value);
    }
    public PushByteIns(byte raw_value)
        : this()
    {
        RawValue = raw_value;
    }
    public PushByteIns(ref SpanFlashReader input)
        : this()
    {
        RawValue = input.ReadByte();
    }

    protected override int GetBodySize() => sizeof(byte);
    protected override void WriteValuesTo(ref SpanFlashWriter output)
    {
        output.Write(RawValue);
    }
}
