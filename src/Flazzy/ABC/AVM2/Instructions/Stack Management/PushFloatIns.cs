using Flazzy.IO;

namespace Flazzy.ABC.AVM2.Instructions;

public sealed class PushFloatIns : Primitive
{
    private float _value;
    new public float Value
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
            _value = ABC.Pool.Floats[value];
            base.Value = _value;
        }
    }

    public PushFloatIns(ABCFile abc)
        : base(OPCode.PushFloat, abc)
    {
    }

    public PushFloatIns(ABCFile abc, float value)
        : this(abc)
    {
        Value = value;
    }

    public PushFloatIns(ABCFile abc, ref SpanFlashReader input)
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
