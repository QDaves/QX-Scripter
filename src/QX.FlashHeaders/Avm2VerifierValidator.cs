using System.Reflection;
using Flazzy.ABC;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

public enum Avm2VerifierSeverity
{
    Warning,
    Error
}

public sealed class Avm2VerifierDiagnostic
{
    public required string Code { get; init; }
    public required Avm2VerifierSeverity Severity { get; init; }
    public required string Message { get; init; }
    public int? InstructionIndex { get; init; }
    public int? Offset { get; init; }
    public int? ExceptionIndex { get; init; }
    public string? ReferenceKind { get; init; }
    public int? ReferenceIndex { get; init; }
    public long? Actual { get; init; }
    public long? Limit { get; init; }
    public required bool Reachable { get; init; }
}

public sealed class Avm2VerifierValidation
{
    public required bool VerifierValid { get; init; }
    public required List<Avm2VerifierDiagnostic> Diagnostics { get; init; }
}

public static class Avm2VerifierValidator
{
    const int u30_max = 0x3fffffff;

    sealed class DecodedInstruction
    {
        public required int Index { get; init; }
        public required int Offset { get; init; }
        public required int Size { get; init; }
        public required ASInstruction Value { get; init; }
        public required Avm2InstructionInventory? Inventory { get; init; }
        public required bool Reachable { get; init; }
    }

    readonly record struct PoolOperand(string Kind, string Property);

    static readonly Dictionary<OPCode, PoolOperand> pool_operands = new()
    {
        [OPCode.AsType] = new("multiname", "TypeNameIndex"),
        [OPCode.CallProperty] = new("multiname", "PropertyNameIndex"),
        [OPCode.CallPropLex] = new("multiname", "PropertyNameIndex"),
        [OPCode.CallPropVoid] = new("multiname", "PropertyNameIndex"),
        [OPCode.CallSuper] = new("multiname", "MethodNameIndex"),
        [OPCode.CallSuperVoid] = new("multiname", "MethodNameIndex"),
        [OPCode.Coerce] = new("multiname", "TypeNameIndex"),
        [OPCode.ConstructProp] = new("multiname", "PropertyNameIndex"),
        [OPCode.Debug] = new("string", "NameIndex"),
        [OPCode.DebugFile] = new("string", "FileNameIndex"),
        [OPCode.DeleteProperty] = new("multiname", "PropertyNameIndex"),
        [OPCode.Dxns] = new("string", "UriIndex"),
        [OPCode.FindDef] = new("multiname", "DefinitionNameIndex"),
        [OPCode.FindProperty] = new("multiname", "PropertyNameIndex"),
        [OPCode.FindPropStrict] = new("multiname", "PropertyNameIndex"),
        [OPCode.GetDescendants] = new("multiname", "DescendantIndex"),
        [OPCode.GetLex] = new("multiname", "TypeNameIndex"),
        [OPCode.GetProperty] = new("multiname", "PropertyNameIndex"),
        [OPCode.GetSuper] = new("multiname", "PropertyNameIndex"),
        [OPCode.InitProperty] = new("multiname", "PropertyNameIndex"),
        [OPCode.IsType] = new("multiname", "TypeNameIndex"),
        [OPCode.PushDouble] = new("double", "ValueIndex"),
        [OPCode.PushFloat] = new("float", "ValueIndex"),
        [OPCode.PushFloat4] = new("float4", "ValueIndex"),
        [OPCode.PushInt] = new("int", "ValueIndex"),
        [OPCode.PushNamespace] = new("namespace", "NamespaceIndex"),
        [OPCode.PushString] = new("string", "ValueIndex"),
        [OPCode.PushUInt] = new("uint", "ValueIndex"),
        [OPCode.SetProperty] = new("multiname", "PropertyNameIndex"),
        [OPCode.SetSuper] = new("multiname", "PropertyNameIndex")
    };

    static readonly HashSet<OPCode> float_opcodes =
    [
        OPCode.Lf32x4,
        OPCode.Sf32x4,
        OPCode.PushFloat,
        OPCode.PushFloat4,
        OPCode.Convert_f,
        OPCode.UnPlus,
        OPCode.Convert_f4
    ];

    public static Avm2VerifierValidation Validate(
        ASMethodBody? body,
        Avm2MethodAnalysis? analysis)
    {
        var diagnostics = new List<Avm2VerifierDiagnostic>();
        if (body is null)
        {
            Add(diagnostics, "body-missing", "The AVM2 method body is missing.");
            return Result(diagnostics);
        }
        if (analysis is null)
        {
            Add(diagnostics, "analysis-missing", "The AVM2 method analysis is missing.");
            return Result(diagnostics);
        }

        ABCFile abc = body.ABC;
        AuditPhase(
            diagnostics,
            "frame-audit",
            () => ValidateFrameHeader(body, abc, diagnostics));
        List<DecodedInstruction> instructions = Decode(body, analysis, diagnostics);
        if (instructions.Count > 0)
        {
            AuditPhase(
                diagnostics,
                "flow-audit",
                () => ValidateFrameFlow(body, analysis, instructions, diagnostics));
            AuditPhase(
                diagnostics,
                "operand-audit",
                () => ValidateInstructionOperands(body, abc, instructions, diagnostics));
            AuditPhase(
                diagnostics,
                "branch-audit",
                () => ValidateBranches(body, analysis, instructions, diagnostics));
            AuditPhase(
                diagnostics,
                "termination-audit",
                () => ValidateFallthrough(body, instructions, diagnostics));
        }
        else if (body.Code.Length == 0)
        {
            Add(diagnostics, "empty-code", "A method body must contain a terminating instruction.");
        }
        AuditPhase(
            diagnostics,
            "exception-audit",
            () => ValidateExceptions(body, abc, analysis, instructions, diagnostics));
        return Result(diagnostics);
    }

    static void AuditPhase(
        List<Avm2VerifierDiagnostic> diagnostics,
        string code,
        Action audit)
    {
        try
        {
            audit();
        }
        catch (Exception exception)
        {
            Add(
                diagnostics,
                code,
                $"Verifier audit failed safely: {exception.GetType().Name}: {exception.Message}");
        }
    }

    static Avm2VerifierValidation Result(List<Avm2VerifierDiagnostic> diagnostics)
    {
        List<Avm2VerifierDiagnostic> distinct = diagnostics
            .DistinctBy(value => (
                value.Code,
                value.Severity,
                value.InstructionIndex,
                value.Offset,
                value.ExceptionIndex,
                value.ReferenceKind,
                value.ReferenceIndex,
                value.Actual,
                value.Limit))
            .ToList();
        return new Avm2VerifierValidation
        {
            VerifierValid = distinct.All(value => value.Severity != Avm2VerifierSeverity.Error),
            Diagnostics = distinct
        };
    }

    static void ValidateFrameHeader(
        ASMethodBody body,
        ABCFile abc,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        ValidateU30("max-stack", body.MaxStack, diagnostics);
        ValidateU30("local-count", body.LocalCount, diagnostics);
        ValidateU30("initial-scope-depth", body.InitialScopeDepth, diagnostics);
        ValidateU30("max-scope-depth", body.MaxScopeDepth, diagnostics);

        if (body.InitialScopeDepth > body.MaxScopeDepth)
        {
            Add(
                diagnostics,
                "scope-depth-order",
                $"Initial scope depth {body.InitialScopeDepth} exceeds maximum scope depth {body.MaxScopeDepth}.",
                actual: body.InitialScopeDepth,
                limit: body.MaxScopeDepth);
        }

        if (!ValidIndex(body.MethodIndex, abc.Methods.Count))
        {
            Add(
                diagnostics,
                "method-body-index",
                $"Method body index {body.MethodIndex} is outside the method table.",
                reference_kind: "method",
                reference_index: body.MethodIndex,
                actual: body.MethodIndex,
                limit: abc.Methods.Count);
            return;
        }

        ASMethod method = abc.Methods[body.MethodIndex];
        int required_locals = method.Parameters.Count + 1;
        if ((method.Flags & (MethodFlags.NeedArguments | MethodFlags.NeedRest)) != 0)
            required_locals++;
        if (body.LocalCount < required_locals)
        {
            Add(
                diagnostics,
                "local-count-parameters",
                $"Local count {body.LocalCount} cannot hold this, parameters, and the required rest or arguments value.",
                actual: body.LocalCount,
                limit: required_locals);
        }
    }

    static void ValidateU30(
        string field,
        int value,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        if (value >= 0 && value <= u30_max)
            return;
        Add(
            diagnostics,
            "frame-u30",
            $"Frame field {field} has non-U30 value {value}.",
            reference_kind: field,
            actual: value,
            limit: u30_max);
    }

    static List<DecodedInstruction> Decode(
        ASMethodBody body,
        Avm2MethodAnalysis analysis,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        var result = new List<DecodedInstruction>();
        try
        {
            var reachable_by_block = analysis.ControlFlow.Blocks
                .ToDictionary(value => value.Id, value => value.Reachable);
            IReadOnlyList<ASInstruction> code = analysis.DecodedCode;
            for (int index = 0; index < code.Count; index++)
            {
                ASInstruction value = code[index];
                int offset = value.DecodedOffset;
                int size = value.DecodedSize > 0 ? value.DecodedSize : value.GetSize();
                Avm2InstructionInventory? inventory =
                    index < analysis.Instructions.Count
                        ? analysis.Instructions[index]
                        : null;
                bool reachable = inventory is not null &&
                    inventory.Block >= 0 &&
                    reachable_by_block.GetValueOrDefault(
                        inventory.Block,
                        false);
                result.Add(new DecodedInstruction
                {
                    Index = index,
                    Offset = offset,
                    Size = size,
                    Value = value,
                    Inventory = inventory,
                    Reachable = reachable
                });
            }

            if (result.Count != analysis.Instructions.Count ||
                result.Any(value =>
                    value.Inventory is null ||
                    value.Offset != value.Inventory.Offset ||
                    value.Size <= 0 ||
                    value.Offset < 0 ||
                    value.Offset > body.Code.Length - value.Size))
            {
                Add(
                    diagnostics,
                    "analysis-code-mismatch",
                    "Decoded instructions do not match the supplied method analysis.");
            }
        }
        catch (Exception exception)
        {
            Add(
                diagnostics,
                "code-decode",
                $"AVM2 bytecode decoding failed: {exception.GetType().Name}: {exception.Message}");
        }
        return result;
    }

    static void ValidateFrameFlow(
        ASMethodBody body,
        Avm2MethodAnalysis analysis,
        List<DecodedInstruction> instructions,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        var decoded_by_offset = instructions.ToDictionary(value => value.Offset);
        var blocks = analysis.ControlFlow.Blocks.ToDictionary(value => value.Id);
        foreach (Avm2BasicBlockInventory block in analysis.ControlFlow.Blocks.Where(value => value.Reachable))
        {
            List<Avm2InstructionInventory> block_instructions = analysis.Instructions
                .Where(value => value.Block == block.Id)
                .OrderBy(value => value.Index)
                .ToList();
            if (!block.EntryStackDepth.HasValue || !block.EntryScopeDepth.HasValue)
            {
                Add(
                    diagnostics,
                    "frame-state-missing",
                    $"Reachable block {block.Id} has no complete entry frame state.",
                    offset: block.StartOffset);
                continue;
            }

            int stack_depth = block.EntryStackDepth.Value;
            int scope_depth = block.EntryScopeDepth.Value;
            ValidateStackLimit(stack_depth, body.MaxStack, block.StartOffset, null, diagnostics);
            ValidateScopeLimit(scope_depth, body, block.StartOffset, null, diagnostics);
            foreach (Avm2InstructionInventory inventory in block_instructions)
            {
                decoded_by_offset.TryGetValue(inventory.Offset, out DecodedInstruction? decoded);
                if (stack_depth < inventory.PopCount)
                {
                    AddInstruction(
                        diagnostics,
                        decoded,
                        "stack-underflow",
                        $"Instruction requires {inventory.PopCount} stack values but only {stack_depth} are available.",
                        actual: stack_depth,
                        limit: inventory.PopCount);
                    stack_depth = inventory.PopCount;
                }
                stack_depth += inventory.PushCount - inventory.PopCount;
                ValidateStackLimit(
                    stack_depth,
                    body.MaxStack,
                    inventory.Offset,
                    decoded,
                    diagnostics);

                if (decoded?.Value is GetScopeObjectIns scope_object)
                {
                    int local_scope_depth = scope_depth - body.InitialScopeDepth;
                    if (scope_object.ScopeIndex >= local_scope_depth)
                    {
                        AddInstruction(
                            diagnostics,
                            decoded,
                            "local-scope-index",
                            $"Local scope index {scope_object.ScopeIndex} is outside depth {Math.Max(0, local_scope_depth)}.",
                            reference_kind: "local-scope",
                            reference_index: scope_object.ScopeIndex,
                            actual: scope_object.ScopeIndex,
                            limit: Math.Max(0, local_scope_depth));
                    }
                }

                int scope_delta = ScopeDelta(decoded?.Value.OP ?? Enum.Parse<OPCode>(inventory.Opcode));
                if (scope_delta < 0 && scope_depth <= body.InitialScopeDepth)
                {
                    AddInstruction(
                        diagnostics,
                        decoded,
                        "scope-underflow",
                        "The local scope stack is empty.");
                    scope_depth = body.InitialScopeDepth;
                }
                else
                {
                    scope_depth += scope_delta;
                }
                ValidateScopeLimit(
                    scope_depth,
                    body,
                    inventory.Offset,
                    decoded,
                    diagnostics);
            }
        }

        foreach (Avm2ControlFlowEdgeInventory edge in analysis.ControlFlow.Edges)
        {
            if (!edge.ToBlock.HasValue ||
                !blocks.TryGetValue(edge.FromBlock, out Avm2BasicBlockInventory? source) ||
                !blocks.TryGetValue(edge.ToBlock.Value, out Avm2BasicBlockInventory? target) ||
                !source.Reachable ||
                !target.Reachable ||
                !source.ExitStackDepth.HasValue ||
                !source.ExitScopeDepth.HasValue ||
                !target.EntryStackDepth.HasValue ||
                !target.EntryScopeDepth.HasValue)
                continue;

            int expected_stack = edge.Kind == "Exception" ? 1 : source.ExitStackDepth.Value;
            int expected_scope = edge.Kind == "Exception"
                ? body.InitialScopeDepth
                : source.ExitScopeDepth.Value;
            if (target.EntryStackDepth.Value != expected_stack)
            {
                Add(
                    diagnostics,
                    "stack-depth-merge",
                    $"Control-flow edge enters block {target.Id} with stack depth {expected_stack}, not {target.EntryStackDepth.Value}.",
                    instruction_index: edge.SourceInstruction,
                    offset: edge.SourceOffset,
                    actual: expected_stack,
                    limit: target.EntryStackDepth.Value);
            }
            if (target.EntryScopeDepth.Value != expected_scope)
            {
                Add(
                    diagnostics,
                    "scope-depth-merge",
                    $"Control-flow edge enters block {target.Id} with scope depth {expected_scope}, not {target.EntryScopeDepth.Value}.",
                    instruction_index: edge.SourceInstruction,
                    offset: edge.SourceOffset,
                    actual: expected_scope,
                    limit: target.EntryScopeDepth.Value);
            }
        }
    }

    static void ValidateStackLimit(
        int stack_depth,
        int max_stack,
        int offset,
        DecodedInstruction? instruction,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        if (stack_depth <= max_stack)
            return;
        if (instruction is null)
        {
            Add(
                diagnostics,
                "stack-overflow",
                $"Stack depth {stack_depth} exceeds max_stack {max_stack}.",
                offset: offset,
                actual: stack_depth,
                limit: max_stack);
        }
        else
        {
            AddInstruction(
                diagnostics,
                instruction,
                "stack-overflow",
                $"Stack depth {stack_depth} exceeds max_stack {max_stack}.",
                actual: stack_depth,
                limit: max_stack);
        }
    }

    static void ValidateScopeLimit(
        int scope_depth,
        ASMethodBody body,
        int offset,
        DecodedInstruction? instruction,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        if (scope_depth >= body.InitialScopeDepth && scope_depth <= body.MaxScopeDepth)
            return;
        string message = $"Scope depth {scope_depth} is outside {body.InitialScopeDepth}..{body.MaxScopeDepth}.";
        if (instruction is null)
        {
            Add(
                diagnostics,
                "scope-overflow",
                message,
                offset: offset,
                actual: scope_depth,
                limit: body.MaxScopeDepth);
        }
        else
        {
            AddInstruction(
                diagnostics,
                instruction,
                "scope-overflow",
                message,
                actual: scope_depth,
                limit: body.MaxScopeDepth);
        }
    }

    static void ValidateInstructionOperands(
        ASMethodBody body,
        ABCFile abc,
        List<DecodedInstruction> instructions,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        MethodFlags flags = ValidIndex(body.MethodIndex, abc.Methods.Count)
            ? abc.Methods[body.MethodIndex].Flags
            : MethodFlags.None;
        foreach (DecodedInstruction instruction in instructions)
        {
            ASInstruction value = instruction.Value;
            if (value is Local local && !ValidIndex(local.Register, body.LocalCount))
            {
                AddInstruction(
                    diagnostics,
                    instruction,
                    "local-index",
                    $"Local register {local.Register} is outside local_count {body.LocalCount}.",
                    reference_kind: "local",
                    reference_index: local.Register,
                    actual: local.Register,
                    limit: body.LocalCount);
            }
            if (value is HasNext2Ins iteration)
            {
                ValidateLocalIndex(
                    iteration.ObjectIndex,
                    body.LocalCount,
                    instruction,
                    "hasnext2-object",
                    diagnostics);
                ValidateLocalIndex(
                    iteration.RegisterIndex,
                    body.LocalCount,
                    instruction,
                    "hasnext2-index",
                    diagnostics);
                if (iteration.ObjectIndex == iteration.RegisterIndex)
                {
                    AddInstruction(
                        diagnostics,
                        instruction,
                        "hasnext2-alias",
                        "hasnext2 requires two distinct local registers.",
                        reference_kind: "local",
                        reference_index: iteration.ObjectIndex);
                }
            }
            if (value is GetOuterScopeIns outer &&
                !ValidIndex(outer.ScopeIndex, body.InitialScopeDepth))
            {
                AddInstruction(
                    diagnostics,
                    instruction,
                    "outer-scope-index",
                    $"Outer scope index {outer.ScopeIndex} is outside initial_scope_depth {body.InitialScopeDepth}.",
                    reference_kind: "outer-scope",
                    reference_index: outer.ScopeIndex,
                    actual: outer.ScopeIndex,
                    limit: body.InitialScopeDepth);
            }
            if (value.OP == OPCode.NewActivation &&
                !flags.HasFlag(MethodFlags.NeedActivation))
            {
                AddInstruction(
                    diagnostics,
                    instruction,
                    "newactivation-flag",
                    "newactivation requires the NeedActivation method flag.");
            }
            if (value.OP is OPCode.Dxns or OPCode.DxnsLate &&
                !flags.HasFlag(MethodFlags.SetDxns))
            {
                AddInstruction(
                    diagnostics,
                    instruction,
                    "dxns-flag",
                    "dxns and dxnslate require the SetDxns method flag.");
            }
            if (float_opcodes.Contains(value.OP) && !abc.HasFloatSupport)
            {
                AddInstruction(
                    diagnostics,
                    instruction,
                    "float-opcode-version",
                    $"Opcode {value.OP} requires ABC version 47.16 or newer.");
            }

            ValidatePoolOperand(abc, instruction, diagnostics);
            switch (value)
            {
                case CallMethodIns call_method:
                    AddInstruction(
                        diagnostics,
                        instruction,
                        "callmethod-verifier-illegal",
                        call_method.MethodIndex == 0
                            ? "CallMethod uses the reserved zero dispatch id."
                            : $"CallMethod early binding with dispatch operand {call_method.MethodIndex} is verifier-illegal.",
                        "dispatch-id",
                        call_method.MethodIndex,
                        call_method.MethodIndex);
                    break;
                case CallStaticIns call_static:
                    ValidateTableIndex(
                        call_static.MethodIndex,
                        abc.Methods.Count,
                        instruction,
                        "method-index",
                        "method",
                        diagnostics);
                    break;
                case NewFunctionIns function:
                    ValidateTableIndex(
                        function.MethodIndex,
                        abc.Methods.Count,
                        instruction,
                        "method-index",
                        "method",
                        diagnostics);
                    break;
                case NewClassIns new_class:
                    ValidateTableIndex(
                        new_class.ClassIndex,
                        abc.Classes.Count,
                        instruction,
                        "class-index",
                        "class",
                        diagnostics);
                    break;
                case NewCatchIns new_catch:
                    ValidateTableIndex(
                        new_catch.ExceptionIndex,
                        body.Exceptions.Count,
                        instruction,
                        "newcatch-index",
                        "exception",
                        diagnostics);
                    break;
            }
        }
    }

    static void ValidateLocalIndex(
        int index,
        int count,
        DecodedInstruction instruction,
        string reference_kind,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        if (ValidIndex(index, count))
            return;
        AddInstruction(
            diagnostics,
            instruction,
            "local-index",
            $"Local register {index} is outside local_count {count}.",
            reference_kind: reference_kind,
            reference_index: index,
            actual: index,
            limit: count);
    }

    static void ValidatePoolOperand(
        ABCFile abc,
        DecodedInstruction instruction,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        if (!pool_operands.TryGetValue(instruction.Value.OP, out PoolOperand operand))
            return;
        PropertyInfo? property = instruction.Value.GetType().GetProperty(
            operand.Property,
            BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(instruction.Value) is not int index)
        {
            AddInstruction(
                diagnostics,
                instruction,
                "pool-operand-unreadable",
                $"Unable to read {operand.Property} from opcode {instruction.Value.OP}.",
                reference_kind: operand.Kind);
            return;
        }
        int count = PoolCount(abc, operand.Kind);
        if (index > 0 && index < count)
            return;
        AddInstruction(
            diagnostics,
            instruction,
            $"pool-{operand.Kind}-index",
            $"{operand.Kind} pool index {index} is outside 1..{Math.Max(0, count - 1)}.",
            reference_kind: operand.Kind,
            reference_index: index,
            actual: index,
            limit: count);
    }

    static int PoolCount(ABCFile abc, string kind) => kind switch
    {
        "int" => abc.Pool.Integers.Count,
        "uint" => abc.Pool.UIntegers.Count,
        "double" => abc.Pool.Doubles.Count,
        "float" => abc.Pool.Floats.Count,
        "float4" => abc.Pool.Float4s.Count,
        "string" => abc.Pool.Strings.Count,
        "namespace" => abc.Pool.Namespaces.Count,
        "multiname" => abc.Pool.Multinames.Count,
        _ => 0
    };

    static void ValidateTableIndex(
        int index,
        int count,
        DecodedInstruction instruction,
        string code,
        string kind,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        if (ValidIndex(index, count))
            return;
        AddInstruction(
            diagnostics,
            instruction,
            code,
            $"{kind} index {index} is outside 0..{count - 1}.",
            reference_kind: kind,
            reference_index: index,
            actual: index,
            limit: count);
    }

    static void ValidateBranches(
        ASMethodBody body,
        Avm2MethodAnalysis analysis,
        List<DecodedInstruction> instructions,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        var by_offset = instructions.ToDictionary(value => value.Offset);
        DecodedInstruction[] reachable = instructions
            .Where(value => value.Reachable)
            .OrderBy(value => value.Offset)
            .ToArray();
        DecodedInstruction? widest = reachable.FirstOrDefault();
        for (int index = 1; index < reachable.Length; index++)
        {
            DecodedInstruction current = reachable[index];
            if (widest is not null &&
                current.Offset < widest.Offset + widest.Size)
            {
                AddInstruction(
                    diagnostics,
                    current,
                    "reachable-instruction-overlap",
                    $"Reachable instruction at {current.Offset} overlaps " +
                    $"the reachable instruction at {widest.Offset}.");
            }
            if (widest is null ||
                current.Offset + current.Size >
                widest.Offset + widest.Size)
            {
                widest = current;
            }
        }
        var forward_targets = new HashSet<int>();
        foreach (DecodedInstruction instruction in reachable)
        {
            foreach (int target in BranchTargets(instruction))
            {
                if (target > instruction.Offset && by_offset.ContainsKey(target))
                    forward_targets.Add(target);
            }
        }
        foreach (Avm2ControlFlowEdgeInventory edge in analysis.ControlFlow.Edges.Where(value =>
                     value.Kind == "Exception" &&
                     value.TargetOffset > value.SourceOffset))
        {
            DecodedInstruction? source = instructions.FirstOrDefault(value =>
                value.Index == edge.SourceInstruction);
            if (source?.Reachable == true && by_offset.ContainsKey(edge.TargetOffset))
                forward_targets.Add(edge.TargetOffset);
        }

        foreach (DecodedInstruction instruction in reachable)
        {
            foreach (int target in BranchTargets(instruction).Distinct())
            {
                if (target < 0 || target >= body.Code.Length ||
                    !by_offset.TryGetValue(target, out DecodedInstruction? target_instruction))
                {
                    AddInstruction(
                        diagnostics,
                        instruction,
                        "branch-target",
                        $"Branch target {target} is outside the code or not an instruction boundary.",
                        reference_kind: "code-offset",
                        reference_index: target,
                        actual: target,
                        limit: body.Code.Length);
                    continue;
                }
                if (target <= instruction.Offset &&
                    target_instruction.Value.OP != OPCode.Label &&
                    !forward_targets.Contains(target))
                {
                    AddInstruction(
                        diagnostics,
                        instruction,
                        "backedge-target",
                        $"Backward branch target {target} is neither label nor an established forward target.",
                        reference_kind: "code-offset",
                        reference_index: target,
                        actual: target);
                }
            }
        }
    }

    static IEnumerable<int> BranchTargets(DecodedInstruction instruction)
    {
        if (instruction.Value is Jumper jumper)
        {
            yield return instruction.Offset + instruction.Size + Signed24(jumper.Offset);
            yield break;
        }
        if (instruction.Value is not LookUpSwitchIns lookup)
            yield break;
        yield return instruction.Offset + Signed24(lookup.DefaultOffset);
        foreach (uint raw_target in lookup.CaseOffsets)
            yield return instruction.Offset + Signed24(raw_target);
    }

    static void ValidateFallthrough(
        ASMethodBody body,
        List<DecodedInstruction> instructions,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        foreach (DecodedInstruction last in instructions.Where(value =>
            value.Reachable &&
            value.Offset + value.Size == body.Code.Length &&
            value.Value.OP is not (
                OPCode.Jump or
                OPCode.LookUpSwitch or
                OPCode.ReturnValue or
                OPCode.ReturnVoid or
                OPCode.Throw)))
        {
            AddInstruction(
                diagnostics,
                last,
                "code-fallthrough",
                "Reachable control flow falls off the end of the method body.");
        }
    }

    static void ValidateExceptions(
        ASMethodBody body,
        ABCFile abc,
        Avm2MethodAnalysis analysis,
        List<DecodedInstruction> instructions,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        var instruction_offsets = instructions.Select(value => value.Offset).ToHashSet();
        var handler_reachable = analysis.ControlFlow.Edges
            .Where(value => value.Kind == "Exception" && value.ExceptionIndex.HasValue)
            .Select(value => value.ExceptionIndex!.Value)
            .ToHashSet();
        for (int index = 0; index < body.Exceptions.Count; index++)
        {
            ASException exception = body.Exceptions[index];
            Avm2ExceptionNormalization normalized =
                analysis.Exceptions[index];
            if (normalized.From < 0 ||
                normalized.To < normalized.From ||
                normalized.Target < normalized.To ||
                normalized.Target >= body.Code.Length)
            {
                Add(
                    diagnostics,
                    "exception-range",
                    $"Exception {index} must satisfy 0 <= from <= to <= target < code_length.",
                    exception_index: index,
                    actual: normalized.Target,
                    limit: body.Code.Length);
            }
            if (!instruction_offsets.Contains(normalized.Target))
            {
                Add(
                    diagnostics,
                    "exception-target-boundary",
                    $"Exception {index} targets non-instruction offset {normalized.Target}.",
                    handler_reachable.Contains(index)
                        ? Avm2VerifierSeverity.Error
                        : Avm2VerifierSeverity.Warning,
                    offset: normalized.Target,
                    exception_index: index,
                    actual: normalized.Target);
            }
            ValidateExceptionPoolIndex(
                exception.ExceptionTypeIndex,
                abc.Pool.Multinames.Count,
                index,
                "exception-type",
                diagnostics);
            ValidateExceptionPoolIndex(
                exception.VariableNameIndex,
                abc.Pool.Multinames.Count,
                index,
                "exception-name",
                diagnostics);
        }
    }

    static void ValidateExceptionPoolIndex(
        int pool_index,
        int count,
        int exception_index,
        string kind,
        List<Avm2VerifierDiagnostic> diagnostics)
    {
        if (pool_index >= 0 && pool_index < count)
            return;
        Add(
            diagnostics,
            $"pool-{kind}-index",
            $"Exception {exception_index} has {kind} pool index {pool_index} outside 0..{count - 1}.",
            exception_index: exception_index,
            reference_kind: "multiname",
            reference_index: pool_index,
            actual: pool_index,
            limit: count);
    }

    static int ScopeDelta(OPCode opcode) => opcode switch
    {
        OPCode.PushScope or OPCode.PushWith => 1,
        OPCode.PopScope => -1,
        _ => 0
    };

    static int Signed24(uint value) =>
        (value & 0x00800000) == 0
            ? (int)value
            : unchecked((int)(value | 0xff000000));

    static bool ValidIndex(int index, int count) =>
        index >= 0 && index < count;

    static void AddInstruction(
        List<Avm2VerifierDiagnostic> diagnostics,
        DecodedInstruction? instruction,
        string code,
        string message,
        string? reference_kind = null,
        int? reference_index = null,
        long? actual = null,
        long? limit = null)
    {
        bool reachable = instruction?.Reachable != false;
        Add(
            diagnostics,
            code,
            message,
            reachable ? Avm2VerifierSeverity.Error : Avm2VerifierSeverity.Warning,
            instruction?.Index,
            instruction?.Offset,
            null,
            reference_kind,
            reference_index,
            actual,
            limit,
            reachable);
    }

    static void Add(
        List<Avm2VerifierDiagnostic> diagnostics,
        string code,
        string message,
        Avm2VerifierSeverity severity = Avm2VerifierSeverity.Error,
        int? instruction_index = null,
        int? offset = null,
        int? exception_index = null,
        string? reference_kind = null,
        int? reference_index = null,
        long? actual = null,
        long? limit = null,
        bool reachable = true)
    {
        diagnostics.Add(new Avm2VerifierDiagnostic
        {
            Code = code,
            Severity = severity,
            Message = message,
            InstructionIndex = instruction_index,
            Offset = offset,
            ExceptionIndex = exception_index,
            ReferenceKind = reference_kind,
            ReferenceIndex = reference_index,
            Actual = actual,
            Limit = limit,
            Reachable = reachable
        });
    }
}
