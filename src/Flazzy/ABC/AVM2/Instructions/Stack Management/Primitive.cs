namespace Flazzy.ABC.AVM2.Instructions;

public abstract class Primitive : ASInstruction
{
    public virtual object? Value { get; set; }

    public Primitive(OPCode op)
        : base(op)
    { }
    public Primitive(OPCode op, ABCFile abc)
        : base(op, abc)
    { }

    public override int GetPopCount() => 0;
    public override int GetPushCount() => 1;
    public override void Execute(ASMachine machine)
    {
        machine.Values.Push(Value);
    }

    public static bool IsValid(OPCode op)
    {
        return op switch
        {
            OPCode.PushNan or
            OPCode.PushNull or
            OPCode.PushByte or
            OPCode.PushShort or
            OPCode.PushInt or
            OPCode.PushUInt or
            OPCode.PushDouble or
            OPCode.PushFloat or
            OPCode.PushFloat4 or
            OPCode.PushString or
            OPCode.PushTrue or
            OPCode.PushFalse => true,

            _ => false
        };
    }
    public static Primitive Create(ABCFile abc, object? value)
    {
        return value switch
        {
            sbyte @sbyte => new PushByteIns(@sbyte),
            byte @byte when @byte <= sbyte.MaxValue => new PushByteIns((sbyte)@byte),
            byte @byte => new PushShortIns((short)@byte),
            short @short => new PushShortIns(@short),
            int @int => new PushIntIns(abc, @int),
            uint @uint => new PushUIntIns(abc, @uint),
            float @float => new PushFloatIns(abc, @float),
            double @double => new PushDoubleIns(abc, @double),
            ASFloat4 float4 => new PushFloat4Ins(abc, float4),
            string @string => new PushStringIns(abc, @string),

            bool @bool when @bool => new PushTrueIns(),
            bool @bool when !@bool => new PushFalseIns(),

            null => new PushNullIns(),
            _ => new PushNaNIns()
        };
    }
}
