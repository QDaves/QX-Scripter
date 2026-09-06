using Flazzy.ABC;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

public enum Avm2FlowKind
{
    Next,
    Branch,
    Jump,
    Switch,
    Return,
    Throw
}

public readonly record struct Avm2InstructionEffect(
    int PopCount,
    int PushCount,
    int ScopeDelta,
    Avm2FlowKind Flow);

public static class Avm2InstructionSemantics
{
    public static Avm2InstructionEffect Read(ASInstruction instruction)
    {
        Avm2InstructionEffect effect = instruction.OP switch
        {
            OPCode.Add or OPCode.Add_i or
            OPCode.BitAnd or OPCode.BitOr or OPCode.BitXor or
            OPCode.Divide or OPCode.Equals or
            OPCode.GreaterEquals or OPCode.GreaterThan or
            OPCode.In or OPCode.InstanceOf or OPCode.IsTypeLate or
            OPCode.LessEquals or OPCode.LessThan or
            OPCode.LShift or OPCode.Modulo or
            OPCode.Multiply or OPCode.Multiply_i or
            OPCode.NextName or OPCode.NextValue or
            OPCode.RShift or OPCode.StrictEquals or
            OPCode.Subtract or OPCode.Subtract_i or
            OPCode.URShift => Effect(2, 1),

            OPCode.AsType or
            OPCode.BitNot or OPCode.CheckFilter or
            OPCode.Coerce or OPCode.Coerce_a or OPCode.Coerce_b or OPCode.Coerce_d or
            OPCode.Coerce_i or OPCode.Coerce_o or OPCode.Coerce_s or OPCode.Coerce_u or
            OPCode.Convert_b or OPCode.Convert_d or OPCode.Convert_i or
            OPCode.Convert_f or OPCode.Convert_f4 or
            OPCode.Convert_o or OPCode.Convert_s or OPCode.Convert_u or OPCode.UnPlus or
            OPCode.Decrement or OPCode.Decrement_i or
            OPCode.Esc_XAttr or OPCode.Esc_XElem or
            OPCode.Increment or OPCode.Increment_i or
            OPCode.IsType or OPCode.Negate or OPCode.Negate_i or
            OPCode.Lf32 or OPCode.Lf32x4 or OPCode.Lf64 or
            OPCode.Li8 or OPCode.Li16 or OPCode.Li32 or
            OPCode.Not or OPCode.Sxi1 or OPCode.Sxi8 or OPCode.Sxi16 or
            OPCode.TypeOf => Effect(1, 1),

            OPCode.ApplyType => Effect(((ApplyTypeIns)instruction).ParamCount + 1, 1),
            OPCode.AsTypeLate => Effect(2, 1),
            OPCode.Call => Effect(((CallIns)instruction).ArgCount + 2, 1),
            OPCode.CallMethod => Effect(((CallMethodIns)instruction).ArgCount + 1, 1),
            OPCode.CallProperty => PropertyCall((CallPropertyIns)instruction, true),
            OPCode.CallPropLex => PropertyCall((CallPropLexIns)instruction, true),
            OPCode.CallPropVoid => PropertyCall((CallPropVoidIns)instruction, false),
            OPCode.CallStatic => Effect(((CallStaticIns)instruction).ArgCount + 1, 1),
            OPCode.CallSuper => SuperCall((CallSuperIns)instruction, true),
            OPCode.CallSuperVoid => SuperCall((CallSuperVoidIns)instruction, false),
            OPCode.Construct => Effect(((ConstructIns)instruction).ArgCount + 1, 1),
            OPCode.ConstructProp => ConstructProperty((ConstructPropIns)instruction),
            OPCode.ConstructSuper => Effect(((ConstructSuperIns)instruction).ArgCount + 1, 0),

            OPCode.DeleteProperty => PropertyEffect(((DeletePropertyIns)instruction).PropertyName, 1, 1),
            OPCode.FindProperty => PropertyEffect(((FindPropertyIns)instruction).PropertyName, 0, 1),
            OPCode.FindPropStrict => PropertyEffect(((FindPropStrictIns)instruction).PropertyName, 0, 1),
            OPCode.GetDescendants => PropertyEffect(((GetDescendantsIns)instruction).Descendant, 1, 1),
            OPCode.GetLex => PropertyEffect(((GetLexIns)instruction).TypeName, 0, 1),
            OPCode.GetProperty => PropertyEffect(((GetPropertyIns)instruction).PropertyName, 1, 1),
            OPCode.GetSuper => PropertyEffect(((GetSuperIns)instruction).PropertyName, 1, 1),
            OPCode.InitProperty => PropertyEffect(((InitPropertyIns)instruction).PropertyName, 2, 0),
            OPCode.SetProperty => PropertyEffect(((SetPropertyIns)instruction).PropertyName, 2, 0),
            OPCode.SetSuper => PropertyEffect(((SetSuperIns)instruction).PropertyName, 2, 0),

            OPCode.DecLocal or OPCode.DecLocal_i or
            OPCode.IncLocal or OPCode.IncLocal_i or
            OPCode.Kill => Effect(0, 0),
            OPCode.GetLocal or OPCode.GetLocal_0 or OPCode.GetLocal_1 or
            OPCode.GetLocal_2 or OPCode.GetLocal_3 => Effect(0, 1),
            OPCode.SetLocal or OPCode.SetLocal_0 or OPCode.SetLocal_1 or
            OPCode.SetLocal_2 or OPCode.SetLocal_3 => Effect(1, 0),

            OPCode.Dup => Effect(1, 2),
            OPCode.Pop => Effect(1, 0),
            OPCode.Swap => Effect(2, 2),
            OPCode.PushScope or OPCode.PushWith => Effect(1, 0, 1),
            OPCode.PopScope => Effect(0, 0, -1),

            OPCode.PushByte or OPCode.PushDouble or OPCode.PushFloat or
            OPCode.PushFloat4 or OPCode.PushFalse or
            OPCode.PushInt or OPCode.PushNamespace or OPCode.PushNan or
            OPCode.PushNull or OPCode.PushShort or OPCode.PushString or
            OPCode.PushTrue or OPCode.PushUInt or OPCode.PushUndefined => Effect(0, 1),

            OPCode.FindDef or OPCode.GetGlobalScope or OPCode.GetGlobalSlot or
            OPCode.GetOuterScope or
            OPCode.GetScopeObject or OPCode.NewActivation or
            OPCode.NewCatch or OPCode.NewFunction => Effect(0, 1),
            OPCode.GetSlot => Effect(1, 1),
            OPCode.SetGlobalSlot => Effect(1, 0),
            OPCode.SetSlot => Effect(2, 0),
            OPCode.NewArray => Effect(((NewArrayIns)instruction).ArgCount, 1),
            OPCode.NewClass => Effect(1, 1),
            OPCode.NewObject => Effect(((NewObjectIns)instruction).ArgCount * 2, 1),

            OPCode.HasNext => Effect(2, 1),
            OPCode.HasNext2 => Effect(0, 1),
            OPCode.Sf32 or OPCode.Sf32x4 or OPCode.Sf64 or OPCode.Si8 or OPCode.Si16 or
            OPCode.Si32 => Effect(2, 0),

            OPCode.IfTrue or OPCode.IfFalse => Effect(1, 0, 0, Avm2FlowKind.Branch),
            OPCode.IfEq or OPCode.IfGe or OPCode.IfGt or OPCode.IfLe or
            OPCode.IfLt or OPCode.IfNe or OPCode.IfNGe or OPCode.IfNGt or
            OPCode.IfNLe or OPCode.IfNLt or OPCode.IfStrictEq or
            OPCode.IfStrictNE => Effect(2, 0, 0, Avm2FlowKind.Branch),
            OPCode.Jump => Effect(0, 0, 0, Avm2FlowKind.Jump),
            OPCode.LookUpSwitch => Effect(1, 0, 0, Avm2FlowKind.Switch),
            OPCode.ReturnValue => Effect(1, 0, 0, Avm2FlowKind.Return),
            OPCode.ReturnVoid => Effect(0, 0, 0, Avm2FlowKind.Return),
            OPCode.Throw => Effect(1, 0, 0, Avm2FlowKind.Throw),

            OPCode.Bkpt or OPCode.BkptLine or OPCode.Debug or OPCode.DebugFile or
            OPCode.DebugLine or OPCode.Dxns or OPCode.Label or OPCode.Nop or
            OPCode.Timestamp => Effect(0, 0),
            OPCode.DxnsLate => Effect(1, 0),

            _ => throw new InvalidDataException($"Missing AVM2 semantics for {instruction.OP}.")
        };
        if (effect.PopCount < 0 || effect.PushCount < 0)
            throw new InvalidDataException($"Invalid AVM2 stack effect for {instruction.OP}.");
        return effect;
    }

    public static void VerifyCoverage()
    {
        OPCode[] values = Enum.GetValues<OPCode>();
        var missing = new List<string>();
        foreach (OPCode value in values)
        {
            if (!HasStaticDefinition(value))
                missing.Add(value.ToString());
        }
        if (missing.Count > 0)
            throw new InvalidDataException($"Missing AVM2 opcode definitions: {string.Join(", ", missing)}.");
    }

    public static bool CanThrow(OPCode op) => op is not (
        OPCode.Bkpt or OPCode.BkptLine or OPCode.Dup or
        OPCode.GetGlobalScope or OPCode.GetGlobalSlot or OPCode.GetLocal or
        OPCode.GetLocal_0 or OPCode.GetLocal_1 or OPCode.GetLocal_2 or OPCode.GetLocal_3 or
        OPCode.GetOuterScope or OPCode.GetScopeObject or
        OPCode.IfFalse or OPCode.IfStrictEq or OPCode.IfStrictNE or OPCode.IfTrue or
        OPCode.Jump or OPCode.Kill or OPCode.Label or OPCode.LookUpSwitch or
        OPCode.Nop or OPCode.Not or OPCode.Pop or OPCode.PopScope or
        OPCode.PushByte or OPCode.PushDouble or OPCode.PushFalse or
        OPCode.PushFloat or OPCode.PushFloat4 or OPCode.PushInt or
        OPCode.PushNamespace or OPCode.PushNan or OPCode.PushNull or OPCode.PushShort or
        OPCode.PushString or OPCode.PushTrue or OPCode.PushUInt or OPCode.PushUndefined or
        OPCode.ReturnVoid or OPCode.SetLocal or OPCode.SetLocal_0 or OPCode.SetLocal_1 or
        OPCode.SetLocal_2 or OPCode.SetLocal_3 or OPCode.Swap or OPCode.Timestamp or
        OPCode.TypeOf);

    static bool HasStaticDefinition(OPCode op) => op switch
    {
        OPCode.Add or OPCode.Add_i or OPCode.ApplyType or OPCode.AsType or OPCode.AsTypeLate or
        OPCode.BitAnd or OPCode.BitNot or OPCode.BitOr or OPCode.BitXor or
        OPCode.Bkpt or OPCode.BkptLine or
        OPCode.Call or OPCode.CallMethod or OPCode.CallProperty or OPCode.CallPropLex or
        OPCode.CallPropVoid or OPCode.CallStatic or OPCode.CallSuper or OPCode.CallSuperVoid or
        OPCode.CheckFilter or OPCode.Coerce or OPCode.Coerce_a or OPCode.Coerce_b or
        OPCode.Coerce_d or OPCode.Coerce_i or OPCode.Coerce_o or OPCode.Coerce_s or
        OPCode.Coerce_u or
        OPCode.Construct or OPCode.ConstructProp or OPCode.ConstructSuper or
        OPCode.Convert_b or OPCode.Convert_d or OPCode.Convert_f or OPCode.Convert_f4 or
        OPCode.Convert_i or OPCode.Convert_o or OPCode.Convert_s or OPCode.Convert_u or
        OPCode.UnPlus or OPCode.Debug or OPCode.DebugFile or
        OPCode.DebugLine or OPCode.DecLocal or OPCode.DecLocal_i or OPCode.Decrement or
        OPCode.Decrement_i or OPCode.DeleteProperty or OPCode.Divide or OPCode.Dup or
        OPCode.Dxns or OPCode.DxnsLate or OPCode.Equals or OPCode.Esc_XAttr or
        OPCode.Esc_XElem or OPCode.FindDef or OPCode.FindProperty or OPCode.FindPropStrict or
        OPCode.GetDescendants or OPCode.GetGlobalScope or OPCode.GetGlobalSlot or
        OPCode.GetOuterScope or
        OPCode.GetLex or OPCode.GetLocal or OPCode.GetLocal_0 or OPCode.GetLocal_1 or
        OPCode.GetLocal_2 or OPCode.GetLocal_3 or OPCode.GetProperty or
        OPCode.GetScopeObject or OPCode.GetSlot or OPCode.GetSuper or
        OPCode.GreaterEquals or OPCode.GreaterThan or OPCode.HasNext or OPCode.HasNext2 or
        OPCode.IfEq or OPCode.IfFalse or OPCode.IfGe or OPCode.IfGt or OPCode.IfLe or
        OPCode.IfLt or OPCode.IfNe or OPCode.IfNGe or OPCode.IfNGt or OPCode.IfNLe or
        OPCode.IfNLt or OPCode.IfStrictEq or OPCode.IfStrictNE or OPCode.IfTrue or
        OPCode.In or OPCode.IncLocal or OPCode.IncLocal_i or OPCode.Increment or
        OPCode.Increment_i or OPCode.InitProperty or OPCode.InstanceOf or OPCode.IsType or
        OPCode.IsTypeLate or OPCode.Jump or OPCode.Kill or OPCode.Label or
        OPCode.LessEquals or OPCode.LessThan or OPCode.Lf32 or OPCode.Lf32x4 or OPCode.Lf64 or
        OPCode.Li8 or OPCode.Li16 or OPCode.Li32 or OPCode.LookUpSwitch or OPCode.LShift or
        OPCode.Modulo or OPCode.Multiply or OPCode.Multiply_i or OPCode.Negate or
        OPCode.Negate_i or OPCode.NewActivation or OPCode.NewArray or OPCode.NewCatch or
        OPCode.NewClass or OPCode.NewFunction or OPCode.NewObject or OPCode.NextName or
        OPCode.NextValue or OPCode.Nop or OPCode.Not or OPCode.Pop or OPCode.PopScope or
        OPCode.PushByte or OPCode.PushDouble or OPCode.PushFloat or OPCode.PushFloat4 or
        OPCode.PushFalse or OPCode.PushInt or
        OPCode.PushNamespace or OPCode.PushNan or OPCode.PushNull or OPCode.PushScope or
        OPCode.PushShort or OPCode.PushString or OPCode.PushTrue or OPCode.PushUInt or
        OPCode.PushUndefined or OPCode.PushWith or OPCode.ReturnValue or OPCode.ReturnVoid or
        OPCode.RShift or OPCode.SetGlobalSlot or OPCode.SetLocal or OPCode.SetLocal_0 or
        OPCode.SetLocal_1 or OPCode.SetLocal_2 or OPCode.SetLocal_3 or OPCode.SetProperty or
        OPCode.SetSlot or OPCode.SetSuper or OPCode.Sf32 or OPCode.Sf32x4 or OPCode.Sf64 or OPCode.Si8 or
        OPCode.Si16 or OPCode.Si32 or OPCode.StrictEquals or OPCode.Subtract or
        OPCode.Subtract_i or OPCode.Swap or OPCode.Sxi1 or OPCode.Sxi8 or OPCode.Sxi16 or
        OPCode.Throw or OPCode.Timestamp or OPCode.TypeOf or OPCode.URShift => true,
        _ => false
    };

    static Avm2InstructionEffect PropertyCall(CallPropertyIns instruction, bool pushes) =>
        PropertyEffect(instruction.PropertyName, instruction.ArgCount + 1, pushes ? 1 : 0);

    static Avm2InstructionEffect PropertyCall(CallPropLexIns instruction, bool pushes) =>
        PropertyEffect(instruction.PropertyName, instruction.ArgCount + 1, pushes ? 1 : 0);

    static Avm2InstructionEffect PropertyCall(CallPropVoidIns instruction, bool pushes) =>
        PropertyEffect(instruction.PropertyName, instruction.ArgCount + 1, pushes ? 1 : 0);

    static Avm2InstructionEffect SuperCall(CallSuperIns instruction, bool pushes) =>
        PropertyEffect(instruction.MethodName, instruction.ArgCount + 1, pushes ? 1 : 0);

    static Avm2InstructionEffect SuperCall(CallSuperVoidIns instruction, bool pushes) =>
        PropertyEffect(instruction.MethodName, instruction.ArgCount + 1, pushes ? 1 : 0);

    static Avm2InstructionEffect ConstructProperty(ConstructPropIns instruction) =>
        PropertyEffect(instruction.PropertyName, instruction.ArgCount + 1, 1);

    static Avm2InstructionEffect PropertyEffect(
        ASMultiname name,
        int fixed_pops,
        int pushes) =>
        Effect(fixed_pops + RuntimeNamePops(name), pushes);

    static int RuntimeNamePops(ASMultiname name) =>
        (name.IsNameNeeded ? 1 : 0) + (name.IsNamespaceNeeded ? 1 : 0);

    static Avm2InstructionEffect Effect(
        int pops,
        int pushes,
        int scope_delta = 0,
        Avm2FlowKind flow = Avm2FlowKind.Next) =>
        new(pops, pushes, scope_delta, flow);
}
