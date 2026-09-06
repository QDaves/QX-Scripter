using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class PushFloat4Ins : Primitive
{
    private ASFloat4 _value;
    new public ASFloat4 Value
    {
        get => _value;
        set
        {
            _value = value;
            _valueIndex = ABC.Pool.AddConstant(value);
            base.Value = value;
        }
    }

    private int _valueIndex;
    public int ValueIndex
    {
        get => _valueIndex;
        set
        {
            _valueIndex = value;
            _value = ABC.Pool.Float4s[value];
            base.Value = _value;
        }
    }

    public PushFloat4Ins(ABCFile abc)
        : base(OPCode.PushFloat4, abc)
    {
    }

    public PushFloat4Ins(ABCFile abc, ASFloat4 value)
        : this(abc)
    {
        Value = value;
    }

    public PushFloat4Ins(ABCFile abc, ref SpanFlashReader input)
        : this(abc)
    {
        ValueIndex = input.ReadEncodedInt();
    }

    protected override int GetBodySize()
    {
        return SpanFlashWriter.GetEncodedIntSize(ValueIndex);
    }

    protected override void WriteValuesTo(ref SpanFlashWriter output)
    {
        output.WriteEncodedInt(ValueIndex);
    }
}
