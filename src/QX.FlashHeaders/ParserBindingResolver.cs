using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

internal static class ParserBindingResolver
{
    sealed class CandidateSet
    {
        public HashSet<ASInstance> Values { get; } =
            new(ReferenceEqualityComparer.Instance);
        public bool Ambiguous { get; set; }
    }

    public static ASInstance? Resolve(
        ASInstance message,
        Avm2CallTargetResolver types)
    {
        CandidateSet declared = DeclaredCandidates(message, types);
        if (declared.Ambiguous)
            return null;
        if (declared.Values.Count != 0)
            return declared.Values.Count == 1 ? declared.Values.Single() : null;

        CandidateSet constructor = ConstructorCandidates(message, types);
        return !constructor.Ambiguous && constructor.Values.Count == 1
            ? constructor.Values.Single()
            : null;
    }

    static CandidateSet DeclaredCandidates(
        ASInstance message,
        Avm2CallTargetResolver types)
    {
        var candidates = new CandidateSet();

        try
        {
            foreach (ASTrait trait in message.Traits)
                AddCandidate(candidates, trait.Type, message.ABC, types);
        }
        catch
        {
            candidates.Ambiguous = true;
        }

        try
        {
            foreach (ASMethod method in message.GetMethods())
            {
                if (method.Parameters.Count == 0)
                    AddCandidate(candidates, method.ReturnType, message.ABC, types);
            }
        }
        catch
        {
            candidates.Ambiguous = true;
        }

        return candidates;
    }

    static CandidateSet ConstructorCandidates(
        ASInstance message,
        Avm2CallTargetResolver types)
    {
        var candidates = new CandidateSet();
        if (message.Constructor?.Body is not ASMethodBody body)
            return candidates;

        try
        {
            Avm2MethodBinding[] bindings = types
                .ResolveMethodBindings(message.Constructor)
                .Where(binding =>
                    binding.Resolved &&
                    binding.Role ==
                        Avm2MethodBindingRole.InstanceConstructor &&
                    ReferenceEquals(binding.Owner, message))
                .Take(2)
                .ToArray();
            if (bindings.Length != 1)
            {
                candidates.Ambiguous = true;
                return candidates;
            }

            Avm2MethodAnalysis analysis =
                Avm2MethodAnalyzer.Analyze(body);
            var receiver = new Avm2ExactReceiver(
                message,
                false);
            Avm2DataFlowAnalysis flow =
                types.DeclaringScopes.Analyze(
                    body,
                    analysis,
                    bindings[0],
                    receiver);
            if (!analysis.ControlFlow.Complete ||
                !flow.Complete)
            {
                candidates.Ambiguous = true;
                return candidates;
            }

            IReadOnlyList<ASInstruction> code =
                analysis.DecodedCode;
            foreach (string value in RelevantConstructorValues(
                code,
                flow))
            {
                Avm2ResolvedValueSet resolution =
                    types.ResolveValueTypes(
                        bindings[0],
                        receiver,
                        value);
                ASInstance[] parsers = resolution.Types
                    .Select(type => type.RuntimeType)
                    .Where(StructuralSignature.ImplementsParser)
                    .Distinct(
                        (IEqualityComparer<ASInstance>)
                            ReferenceEqualityComparer.Instance)
                    .ToArray();
                if (parsers.Length == 0)
                {
                    if (!resolution.Exhaustive &&
                        MayRepresentType(
                            flow,
                            value))
                    {
                        candidates.Ambiguous = true;
                    }
                    continue;
                }
                if (!resolution.Exhaustive ||
                    resolution.Types.Count != 1 ||
                    parsers.Length != 1)
                {
                    candidates.Ambiguous = true;
                    continue;
                }
                candidates.Values.Add(parsers[0]);
            }
        }
        catch
        {
            candidates.Ambiguous = true;
        }

        return candidates;
    }

    internal static IReadOnlyList<string> RelevantConstructorValues(
        IReadOnlyList<ASInstruction> code,
        Avm2DataFlowAnalysis flow)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(flow);
        Dictionary<string, Avm2DataFlowOperation> producers =
            Producers(flow);
        Dictionary<string, Avm2DataFlowPhi> phis =
            flow.Phis.ToDictionary(
                phi => phi.Value,
                StringComparer.Ordinal);
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (Avm2DataFlowOperation operation in
            flow.Operations.Where(operation =>
                !operation.Unreachable &&
                operation.Instruction >= 0 &&
                operation.Instruction < code.Count))
        {
            ASInstruction instruction =
                code[operation.Instruction];
            if (instruction is ConstructSuperIns construct &&
                operation.Inputs.Count == construct.ArgCount + 1 &&
                AliasesOnly(
                    operation.Inputs[0],
                    "v_entry_local_0",
                    producers,
                    phis,
                    new HashSet<string>(StringComparer.Ordinal)))
            {
                foreach (string value in operation.Inputs.Skip(1))
                    values.Add(value);
                continue;
            }
            if (instruction is
                    SetPropertyIns or
                    InitPropertyIns or
                    SetSlotIns &&
                operation.Inputs.Count == 2 &&
                AliasesOnly(
                    operation.Inputs[0],
                    "v_entry_local_0",
                    producers,
                    phis,
                    new HashSet<string>(StringComparer.Ordinal)))
            {
                values.Add(operation.Inputs[^1]);
            }
        }
        return values.Order(StringComparer.Ordinal).ToArray();
    }

    static bool MayRepresentType(
        Avm2DataFlowAnalysis flow,
        string value)
    {
        Dictionary<string, Avm2DataFlowOperation> producers =
            Producers(flow);
        Dictionary<string, Avm2DataFlowPhi> phis =
            flow.Phis.ToDictionary(
                phi => phi.Value,
                StringComparer.Ordinal);
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(value);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            if (!visited.Add(current))
                continue;
            if (phis.TryGetValue(
                    current,
                    out Avm2DataFlowPhi? phi))
            {
                foreach (Avm2DataFlowPhiInput input in phi.Inputs)
                    pending.Push(input.Value);
                continue;
            }
            if (!producers.TryGetValue(
                    current,
                    out Avm2DataFlowOperation? producer))
            {
                continue;
            }
            if (producer.Opcode is
                nameof(OPCode.GetLex) or
                nameof(OPCode.Construct) or
                nameof(OPCode.ConstructProp))
            {
                return true;
            }
            if (!Transparent(producer))
                continue;
            foreach (string input in producer.Inputs)
                pending.Push(input);
        }
        return false;
    }

    static bool AliasesOnly(
        string value,
        string source,
        IReadOnlyDictionary<string, Avm2DataFlowOperation> producers,
        IReadOnlyDictionary<string, Avm2DataFlowPhi> phis,
        HashSet<string> visited)
    {
        if (value == source)
            return true;
        if (!visited.Add(value))
            return false;
        if (phis.TryGetValue(
                value,
                out Avm2DataFlowPhi? phi))
        {
            return phi.Inputs.Count > 0 &&
                phi.Inputs.All(input => AliasesOnly(
                    input.Value,
                    source,
                    producers,
                    phis,
                    new HashSet<string>(
                        visited,
                        StringComparer.Ordinal)));
        }
        return producers.TryGetValue(
                value,
                out Avm2DataFlowOperation? producer) &&
            Transparent(producer) &&
            producer.Inputs.Count > 0 &&
            producer.Inputs.All(input => AliasesOnly(
                input,
                source,
                producers,
                phis,
                new HashSet<string>(
                    visited,
                    StringComparer.Ordinal)));
    }

    static bool Transparent(
        Avm2DataFlowOperation operation) =>
        operation.Opcode is
            nameof(OPCode.GetLocal) or
            nameof(OPCode.GetLocal_0) or
            nameof(OPCode.GetLocal_1) or
            nameof(OPCode.GetLocal_2) or
            nameof(OPCode.GetLocal_3) or
            nameof(OPCode.SetLocal) or
            nameof(OPCode.SetLocal_0) or
            nameof(OPCode.SetLocal_1) or
            nameof(OPCode.SetLocal_2) or
            nameof(OPCode.SetLocal_3) or
            nameof(OPCode.Dup) or
            nameof(OPCode.Coerce) or
            nameof(OPCode.Coerce_a) or
            nameof(OPCode.Coerce_o) or
            nameof(OPCode.AsType) or
            nameof(OPCode.AsTypeLate) or
            nameof(OPCode.CheckFilter) or
            nameof(OPCode.Convert_o);

    static Dictionary<string, Avm2DataFlowOperation> Producers(
        Avm2DataFlowAnalysis flow)
    {
        var producers =
            new Dictionary<string, Avm2DataFlowOperation>(
                StringComparer.Ordinal);
        foreach (Avm2DataFlowOperation operation in
            flow.Operations)
        {
            foreach (string definition in
                operation.Definitions)
            {
                producers.TryAdd(
                    definition,
                    operation);
            }
        }
        return producers;
    }

    static void AddCandidate(
        CandidateSet candidates,
        ASMultiname? type,
        ABCFile requester,
        Avm2CallTargetResolver types)
    {
        if (type is null)
            return;

        IReadOnlyList<Avm2TypeDefinition> definitions =
            types.ResolveTypes(type, requester);
        List<Avm2TypeDefinition> parsers = definitions
            .Where(definition =>
                StructuralSignature.ImplementsParser(definition.Instance))
            .ToList();
        if (parsers.Count == 0)
            return;
        if (definitions.Count != 1 || parsers.Count != 1)
        {
            candidates.Ambiguous = true;
            return;
        }
        candidates.Values.Add(parsers[0].Instance);
    }
}
