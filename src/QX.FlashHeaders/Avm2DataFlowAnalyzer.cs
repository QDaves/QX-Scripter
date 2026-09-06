using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

public enum Avm2VerifierTypeKind
{
    Unknown,
    Any,
    Null,
    Void,
    Known
}

public sealed record Avm2VerifierType
{
    [JsonConstructor]
    public Avm2VerifierType(
        Avm2VerifierTypeKind kind,
        string? identity)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (kind == Avm2VerifierTypeKind.Known)
            ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        else if (identity is not null)
            throw new ArgumentException(
                "Only known verifier types may have an identity.",
                nameof(identity));
        Kind = kind;
        Identity = identity;
    }

    public Avm2VerifierTypeKind Kind { get; }
    public string? Identity { get; }

    public static Avm2VerifierType Unknown { get; } =
        new(Avm2VerifierTypeKind.Unknown, null);
    public static Avm2VerifierType Any { get; } =
        new(Avm2VerifierTypeKind.Any, null);
    public static Avm2VerifierType Null { get; } =
        new(Avm2VerifierTypeKind.Null, null);
    public static Avm2VerifierType Void { get; } =
        new(Avm2VerifierTypeKind.Void, null);

    public static Avm2VerifierType Known(string identity) =>
        new(
            Avm2VerifierTypeKind.Known,
            identity);
}

internal sealed class Avm2ExactReceiver : IEquatable<Avm2ExactReceiver>
{
    public Avm2ExactReceiver(
        ASInstance runtime_type,
        bool @static)
    {
        RuntimeType = runtime_type;
        Static = @static;
    }

    public ASInstance RuntimeType { get; }
    public bool Static { get; }

    public bool Equals(Avm2ExactReceiver? other) =>
        other is not null &&
        ReferenceEquals(RuntimeType, other.RuntimeType) &&
        Static == other.Static;

    public override bool Equals(object? value) =>
        value is Avm2ExactReceiver other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(RuntimeType),
            Static);
}

public sealed class Avm2DataFlowAnalysis
{
    public int FormatVersion { get; init; } = 2;
    public required List<Avm2DataFlowValue> Values { get; init; }
    public required List<Avm2DataFlowOperation> Operations { get; init; }
    public required List<Avm2DataFlowBlock> Blocks { get; init; }
    public required List<Avm2DataFlowPhi> Phis { get; init; }
    public required List<Avm2DataFlowDiagnostic> Diagnostics { get; init; }
    public required bool Complete { get; init; }
    public bool DeclaringScopeKnown { get; init; }
    public IReadOnlyDictionary<int, IReadOnlyList<bool?>>
        ScopeWithBefore { get; init; } =
            new Dictionary<int, IReadOnlyList<bool?>>();
    public IReadOnlyList<Avm2DataFlowScopeValue>? DeclaringScopeValues
    {
        get;
        init;
    }
    public int? CapturedScopeSize =>
        DeclaringScopeKnown
            ? DeclaringScopeValues?.Count ?? 0
            : null;
    internal Avm2ExactReceiver? ExactReceiver { get; init; }
    internal ASMethodBody? SourceBody { get; init; }
    internal Avm2MethodBinding? SourceBinding { get; init; }
    internal Avm2MethodAnalysis? SourceMethodAnalysis
    {
        get;
        init;
    }
    internal Avm2DataFlowScopeContext? DeclaringScopeContext
    {
        get;
        init;
    }
    internal string IntegrityFingerprint { get; set; } = "";

    internal bool MatchesSource(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver,
        Avm2DataFlowScopeContext? scope_context)
    {
        if (!ReferenceEquals(SourceBody, body) ||
            !ReferenceEquals(
                SourceMethodAnalysis,
                method_analysis) ||
            !ReferenceEquals(SourceBinding, binding) ||
            !Equals(ExactReceiver, exact_receiver) ||
            !method_analysis.MatchesSource(body))
        {
            return false;
        }
        try
        {
            return Avm2DataFlowAnalyzer.ScopeContextsEqual(
                    DeclaringScopeContext,
                    scope_context) &&
                string.Equals(
                    Avm2DataFlowAnalyzer.IntegrityFingerprint(
                        this),
                    IntegrityFingerprint,
                    StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    internal bool MatchesSource(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2DataFlowScopeContext? scope_context) =>
        MatchesSource(
            body,
            method_analysis,
            null,
            null,
            scope_context);
}

public sealed class Avm2DataFlowValue
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public required string Definition { get; init; }
    public string? TypeHint { get; init; }
    public Avm2VerifierType VerifierType { get; init; } =
        Avm2VerifierType.Unknown;
    public string? ExactRuntimeTypeIdentity { get; init; }
    public string? Literal { get; init; }
    public int? Block { get; init; }
    public int? Instruction { get; init; }
    public required List<string> Sources { get; init; }
}

public sealed class Avm2DataFlowOperation
{
    public required int Instruction { get; init; }
    public required int Offset { get; init; }
    public required int Block { get; init; }
    public required string Opcode { get; init; }
    public required bool Unreachable { get; init; }
    public required List<string> Inputs { get; init; }
    public required List<string> Outputs { get; init; }
    public required List<string> Definitions { get; init; }
    public required List<string> StackBefore { get; init; }
    public required List<string> StackAfter { get; init; }
    public required List<string> ScopeBefore { get; init; }
    public required List<string> ScopeAfter { get; init; }
    public required SortedDictionary<int, string> LocalWrites { get; init; }
    public required SortedDictionary<string, string?> Operands { get; init; }
}

public sealed class Avm2DataFlowBlock
{
    public required int Id { get; init; }
    public required bool Unreachable { get; init; }
    public required List<int> Instructions { get; init; }
    public required Avm2DataFlowState Entry { get; init; }
    public required Avm2DataFlowState Exit { get; init; }
}

public sealed class Avm2DataFlowState
{
    public required List<string> Stack { get; init; }
    public required List<string> Scope { get; init; }
    public required List<string> Locals { get; init; }
}

public sealed class Avm2DataFlowPhi
{
    public required string Value { get; init; }
    public required int Block { get; init; }
    public required string State { get; init; }
    public required int Index { get; init; }
    public required List<Avm2DataFlowPhiInput> Inputs { get; init; }
}

public sealed class Avm2DataFlowPhiInput
{
    public int? FromBlock { get; init; }
    public required string EdgeKind { get; init; }
    public int? SourceInstruction { get; init; }
    public required string Value { get; init; }
}

public sealed class Avm2DataFlowDiagnostic
{
    public required string Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public int? Block { get; init; }
    public int? Instruction { get; init; }
}

public sealed class Avm2DataFlowScopeValue
{
    public required string Provenance { get; init; }
    public string? TypeHint { get; init; }
    public Avm2VerifierType VerifierType { get; init; } =
        Avm2VerifierType.Unknown;
    public string? ExactRuntimeTypeIdentity { get; init; }
    public string? Literal { get; init; }
    public bool IsWith { get; init; }
}

public sealed class Avm2DataFlowScopeContext
{
    public required IReadOnlyList<Avm2DataFlowScopeValue> DeclaringScope { get; init; }
    public bool HasExtraVerifierType { get; init; }
    public Avm2VerifierType ExtraVerifierType { get; init; } =
        Avm2VerifierType.Unknown;
    public int CapturedScopeSize => DeclaringScope.Count;
    public int FullScopeSize =>
        DeclaringScope.Count + (HasExtraVerifierType ? 1 : 0);

    public static Avm2DataFlowScopeContext Empty { get; } = new()
    {
        DeclaringScope = Array.Empty<Avm2DataFlowScopeValue>(),
        ExtraVerifierType = Avm2VerifierType.Unknown
    };

    public static Avm2DataFlowScopeContext Capture(
        Avm2DataFlowAnalysis analysis,
        Avm2DataFlowOperation operation)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(operation);
        if (analysis.SourceBody is null ||
            analysis.SourceMethodAnalysis is null ||
            !analysis.MatchesSource(
                analysis.SourceBody,
                analysis.SourceMethodAnalysis,
                analysis.SourceBinding,
                analysis.ExactReceiver,
                analysis.DeclaringScopeContext))
        {
            throw new InvalidOperationException(
                "The source analysis provenance or integrity is invalid.");
        }
        if (!analysis.Operations.Contains(operation))
            throw new ArgumentException("The operation does not belong to the analysis.", nameof(operation));
        if (!analysis.DeclaringScopeKnown)
        {
            throw new InvalidOperationException(
                "The source analysis has no proven declaring scope.");
        }
        if (!analysis.Complete)
        {
            throw new InvalidOperationException(
                "The source analysis is incomplete.");
        }
        if (operation.Unreachable)
        {
            throw new InvalidOperationException(
                "An unreachable operation cannot capture a declaring scope.");
        }
        if (operation.Opcode is not (nameof(OPCode.NewFunction) or nameof(OPCode.NewClass)))
        {
            throw new ArgumentException(
                "Only newfunction and newclass operations capture a declaring scope.",
                nameof(operation));
        }

        Dictionary<string, Avm2DataFlowValue> values = analysis.Values
            .ToDictionary(value => value.Id, StringComparer.Ordinal);
        var declaring_scope = new List<Avm2DataFlowScopeValue>(operation.ScopeBefore.Count);
        if (!analysis.ScopeWithBefore.TryGetValue(
                operation.Instruction,
                out IReadOnlyList<bool?>? scope_with) ||
            scope_with.Count != operation.ScopeBefore.Count)
        {
            throw new InvalidOperationException(
                "The captured scope metadata is unavailable.");
        }
        for (int index = 0; index < operation.ScopeBefore.Count; index++)
        {
            string value_id = operation.ScopeBefore[index];
            if (!values.TryGetValue(value_id, out Avm2DataFlowValue? value))
            {
                throw new InvalidOperationException(
                    $"Captured scope value {value_id} is absent from the source analysis.");
            }
            if (value.Kind is
                "Unknown" or
                "UnknownDeclaringScope" or
                "Missing" or
                "Unreachable")
            {
                throw new InvalidOperationException(
                    $"Captured scope value {value_id} is not proven.");
            }
            if (scope_with[index] is not bool is_with)
            {
                throw new InvalidOperationException(
                    $"Captured scope value {value_id} has ambiguous with semantics.");
            }
            declaring_scope.Add(new Avm2DataFlowScopeValue
            {
                Provenance = string.Create(
                    CultureInfo.InvariantCulture,
                    $"capture:instruction:{operation.Instruction}:scope:{index}:{value.Definition}"),
                TypeHint = value.TypeHint,
                VerifierType =
                    value.VerifierType.Kind !=
                        Avm2VerifierTypeKind.Unknown
                    ? value.VerifierType
                    : index < (
                        analysis.DeclaringScopeValues?.Count ?? 0)
                        ? analysis.DeclaringScopeValues![index]
                            .VerifierType
                        : Avm2VerifierType.Unknown,
                ExactRuntimeTypeIdentity =
                    value.ExactRuntimeTypeIdentity ?? (index < (
                    analysis.DeclaringScopeValues?.Count ?? 0)
                    ? analysis.DeclaringScopeValues![index]
                        .ExactRuntimeTypeIdentity
                    : null),
                Literal = value.Literal,
                IsWith = is_with
            });
        }
        return new Avm2DataFlowScopeContext
        {
            DeclaringScope = declaring_scope.AsReadOnly(),
            ExtraVerifierType =
                Avm2VerifierType.Unknown
        };
    }
}

public static class Avm2DataFlowAnalyzer
{
    const string UnknownDeclaringScope = "v_unknown_declaring_scope";

    sealed class MutableValue
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }
        public required string Definition { get; init; }
        public string? TypeHint { get; set; }
        public Avm2VerifierType VerifierType { get; set; } =
            Avm2VerifierType.Unknown;
        public string? ExactRuntimeTypeIdentity { get; set; }
        public string? Literal { get; init; }
        public int? Block { get; init; }
        public int? Instruction { get; init; }
        public SortedSet<string> Sources { get; } = new(StringComparer.Ordinal);
    }

    sealed class FlowState
    {
        public required List<string> Stack { get; init; }
        public required List<string> Scope { get; init; }
        public required List<bool?> ScopeWith { get; init; }
        public required List<string> Locals { get; init; }

        public FlowState Copy() => new()
        {
            Stack = [.. Stack],
            Scope = [.. Scope],
            ScopeWith = [.. ScopeWith],
            Locals = [.. Locals]
        };
    }

    sealed class BlockTransfer
    {
        public required FlowState Exit { get; init; }
        public required Dictionary<int, FlowState> Before { get; init; }
        public required List<Avm2DataFlowOperation> Operations { get; init; }
    }

    readonly record struct IndexedEdge(int Index, Avm2ControlFlowEdgeInventory Edge);

    readonly record struct TypeProof(
        Avm2VerifierType? Verifier,
        string? ExactRuntime);

    readonly record struct IncomingState(
        int? FromBlock,
        string EdgeKind,
        int? SourceInstruction,
        FlowState State);

    sealed class AnalysisContext
    {
        public required ASMethodBody Body { get; init; }
        public required Avm2MethodAnalysis Method { get; init; }
        public required List<ASInstruction?> Code { get; init; }
        public required Dictionary<string, MutableValue> Values { get; init; }
        public required Dictionary<string, Avm2DataFlowDiagnostic> Diagnostics { get; init; }
        public required HashSet<string> ForcedPhis { get; init; }
        public required FlowState EntryState { get; init; }
        public required Dictionary<int, List<IndexedEdge>> IncomingEdges { get; init; }
        public required List<IndexedEdge> Edges { get; init; }
        public required Dictionary<int, Avm2BasicBlockInventory> BlocksById { get; init; }
        public required HashSet<int> ExceptionSources { get; init; }
        public required IReadOnlyList<string>? DeclaringScope { get; init; }
        public required IReadOnlyList<bool>? DeclaringScopeWith { get; init; }
        public required Avm2MethodBinding? Binding { get; init; }
        public required Avm2VerifierTypeRegistry VerifierTypes { get; init; }
        public required Dictionary<int, IReadOnlyList<bool?>> ScopeWithBefore
        {
            get;
            init;
        }
    }

    public static Avm2DataFlowAnalysis Analyze(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis) =>
        Analyze(body, method_analysis, null, null, null);

    public static Avm2DataFlowAnalysis Analyze(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2DataFlowScopeContext scope_context) =>
        Analyze(body, method_analysis, null, null, scope_context);

    public static Avm2DataFlowAnalysis Analyze(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2MethodBinding? binding) =>
        Analyze(body, method_analysis, binding, null, null);

    public static Avm2DataFlowAnalysis Analyze(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2MethodBinding? binding,
        Avm2DataFlowScopeContext scope_context) =>
        Analyze(body, method_analysis, binding, null, scope_context);

    internal static Avm2DataFlowAnalysis Analyze(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver)
        => Analyze(body, method_analysis, binding, exact_receiver, null);

    internal static Avm2DataFlowAnalysis Analyze(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver,
        Avm2DataFlowScopeContext? scope_context,
        Avm2VerifierTypeRegistry? verifier_types = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(method_analysis);
        if (!method_analysis.MatchesSource(body))
        {
            throw new ArgumentException(
                "The method analysis does not reference the analyzed method body.",
                nameof(method_analysis));
        }
        if (binding is not null &&
            (!binding.Resolved || !ReferenceEquals(binding.Method, body.Method)))
        {
            throw new ArgumentException(
                "The method binding does not reference the analyzed method body.",
                nameof(binding));
        }
        if (exact_receiver is not null &&
            binding is not null &&
            (binding.Scope is not (
                    Avm2MethodBindingScope.ClassInstance or
                    Avm2MethodBindingScope.ClassStatic) ||
                exact_receiver.Static !=
                    (binding.Scope == Avm2MethodBindingScope.ClassStatic)))
        {
            throw new ArgumentException(
                "Exact receiver provenance does not match the method binding.",
                nameof(exact_receiver));
        }
        verifier_types ??=
            Avm2VerifierTypeRegistry.For(body.ABC);
        ValidateScopeContext(
            scope_context,
            verifier_types);
        scope_context = ImmutableScopeContext(scope_context);

        var values = new Dictionary<string, MutableValue>(StringComparer.Ordinal);
        var diagnostics = new Dictionary<string, Avm2DataFlowDiagnostic>(StringComparer.Ordinal);
        List<ASInstruction?> code = ReadCode(body, method_analysis, diagnostics);
        IReadOnlyList<string>? declaring_scope = CreateDeclaringScope(
            scope_context,
            values);
        IReadOnlyList<bool>? declaring_scope_with = scope_context is null
            ? null
            : Array.AsReadOnly(
                scope_context.DeclaringScope
                    .Select(value => value.IsWith)
                    .ToArray());
        FlowState entry_state = CreateEntryState(
            body,
            binding,
            exact_receiver,
            verifier_types,
            values,
            diagnostics);
        List<IndexedEdge> edges = method_analysis.ControlFlow.Edges
            .Select((edge, index) => new IndexedEdge(index, edge))
            .ToList();
        Dictionary<int, List<IndexedEdge>> incoming_edges = edges
            .Where(edge => edge.Edge.ToBlock.HasValue)
            .GroupBy(edge => edge.Edge.ToBlock!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(edge => edge.Edge.FromBlock)
                    .ThenBy(edge => edge.Edge.SourceInstruction)
                    .ThenBy(edge => edge.Edge.Kind, StringComparer.Ordinal)
                    .ThenBy(edge => edge.Index)
                    .ToList());
        var context = new AnalysisContext
        {
            Body = body,
            Method = method_analysis,
            Code = code,
            Values = values,
            Diagnostics = diagnostics,
            ForcedPhis = new HashSet<string>(StringComparer.Ordinal),
            EntryState = entry_state,
            IncomingEdges = incoming_edges,
            Edges = edges,
            BlocksById = method_analysis.ControlFlow.Blocks.ToDictionary(block => block.Id),
            ExceptionSources = edges
                .Where(edge => edge.Edge.Kind == "Exception")
                .Select(edge => edge.Edge.SourceInstruction)
                .ToHashSet(),
            DeclaringScope = declaring_scope,
            DeclaringScopeWith = declaring_scope_with,
            Binding = binding,
            VerifierTypes = verifier_types,
            ScopeWithBefore = []
        };

        ValidateCoverage(context);
        ValidateGraph(context);

        var entries = new Dictionary<int, FlowState>();
        var transfers = new Dictionary<int, BlockTransfer>();
        List<Avm2BasicBlockInventory> reachable_blocks = method_analysis.ControlFlow.Blocks
            .Where(block => block.Reachable)
            .OrderBy(block => block.Id)
            .ToList();
        int iteration_limit = Math.Max(32, reachable_blocks.Count * 4 + 16);
        bool converged = false;

        for (int iteration = 0; iteration < iteration_limit; iteration++)
        {
            bool changed = false;
            foreach (Avm2BasicBlockInventory block in reachable_blocks)
            {
                FlowState? entry = MergeEntry(context, block, transfers);
                if (entry is null)
                    continue;
                bool entry_changed = !entries.TryGetValue(block.Id, out FlowState? current_entry) ||
                    !SameState(current_entry, entry);
                if (entry_changed)
                {
                    entries[block.Id] = entry;
                    changed = true;
                }
                if (entry_changed || !transfers.ContainsKey(block.Id))
                {
                    BlockTransfer transfer = TransferBlock(context, block, entry, false);
                    if (!transfers.TryGetValue(block.Id, out BlockTransfer? current_transfer) ||
                        !SameState(current_transfer.Exit, transfer.Exit))
                    {
                        changed = true;
                    }
                    transfers[block.Id] = transfer;
                }
            }
            if (!changed)
            {
                converged = true;
                break;
            }
        }

        if (!converged)
        {
            AddDiagnostic(
                context,
                "Error",
                "fixpoint-limit",
                $"Data-flow fixpoint did not converge after {iteration_limit} iterations.");
        }

        foreach (Avm2BasicBlockInventory block in reachable_blocks)
        {
            if (entries.ContainsKey(block.Id))
                continue;
            AddDiagnostic(
                context,
                "Error",
                "missing-entry-state",
                $"Reachable block {block.Id} has no entry state.",
                block.Id);
        }

        foreach (Avm2BasicBlockInventory block in method_analysis.ControlFlow.Blocks
            .Where(block => !block.Reachable)
            .OrderBy(block => block.Id))
        {
            FlowState entry = CreateUnreachableState(context, block);
            entries[block.Id] = entry;
            transfers[block.Id] = TransferBlock(context, block, entry, true);
        }

        List<Avm2DataFlowPhi> phis = BuildPhis(context, entries, transfers);
        InferPhiTypes(values, phis);

        List<Avm2DataFlowBlock> blocks = method_analysis.ControlFlow.Blocks
            .OrderBy(block => block.Id)
            .Select(block => BuildBlock(context, block, entries, transfers))
            .ToList();
        List<Avm2DataFlowOperation> operations = transfers
            .OrderBy(pair => pair.Key)
            .SelectMany(pair => pair.Value.Operations)
            .OrderBy(operation => operation.Instruction)
            .ToList();
        List<Avm2DataFlowValue> value_models = values.Values
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .Select(value => new Avm2DataFlowValue
            {
                Id = value.Id,
                Kind = value.Kind,
                Definition = value.Definition,
                TypeHint = value.TypeHint,
                VerifierType = value.VerifierType,
                ExactRuntimeTypeIdentity =
                    value.ExactRuntimeTypeIdentity,
                Literal = value.Literal,
                Block = value.Block,
                Instruction = value.Instruction,
                Sources = [.. value.Sources]
            })
            .ToList();
        List<Avm2DataFlowDiagnostic> diagnostic_models = diagnostics.Values
            .OrderByDescending(diagnostic => diagnostic.Severity == "Error")
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Block)
            .ThenBy(diagnostic => diagnostic.Instruction)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToList();
        bool complete = method_analysis.ControlFlow.Complete &&
            diagnostic_models.All(diagnostic => diagnostic.Severity != "Error");

        var result = new Avm2DataFlowAnalysis
        {
            Values = value_models,
            Operations = operations,
            Blocks = blocks,
            Phis = phis,
            Diagnostics = diagnostic_models,
            Complete = complete,
            ExactReceiver = exact_receiver,
            SourceBody = body,
            SourceBinding = binding,
            SourceMethodAnalysis = method_analysis,
            DeclaringScopeKnown = declaring_scope is not null,
            DeclaringScopeValues = scope_context?.DeclaringScope,
            DeclaringScopeContext = scope_context,
            ScopeWithBefore = context.ScopeWithBefore
                .OrderBy(value => value.Key)
                .ToDictionary(
                    value => value.Key,
                    value => value.Value)
        };
        result.IntegrityFingerprint =
            IntegrityFingerprint(result);
        return result;
    }

    static void ValidateScopeContext(
        Avm2DataFlowScopeContext? context,
        Avm2VerifierTypeRegistry verifier_types)
    {
        if (context is null)
            return;
        ArgumentNullException.ThrowIfNull(
            context.DeclaringScope);
        for (int index = 0;
            index < context.DeclaringScope.Count;
            index++)
        {
            Avm2DataFlowScopeValue value =
                context.DeclaringScope[index] ??
                throw new ArgumentException(
                    $"Declaring scope value {index} is null.",
                    nameof(context));
            if (string.IsNullOrWhiteSpace(value.Provenance))
            {
                throw new ArgumentException(
                    $"Declaring scope value {index} has no provenance.",
                    nameof(context));
            }
            if (value.VerifierType.Kind ==
                Avm2VerifierTypeKind.Unknown)
            {
                throw new ArgumentException(
                    $"Declaring scope value {index} has an unproven verifier type.",
                    nameof(context));
            }
            if (value.VerifierType.Kind is
                Avm2VerifierTypeKind.Null or
                Avm2VerifierTypeKind.Void)
            {
                throw new ArgumentException(
                    $"Declaring scope value {index} is not an object verifier type.",
                    nameof(context));
            }
            if (value.ExactRuntimeTypeIdentity is not null &&
                !ExactScopeTypeMatches(
                    value.VerifierType,
                    value.ExactRuntimeTypeIdentity,
                    verifier_types))
            {
                throw new ArgumentException(
                    $"Declaring scope value {index} has an exact runtime type inconsistent with its verifier type.",
                    nameof(context));
            }
        }
        if (!context.HasExtraVerifierType)
        {
            if (context.ExtraVerifierType.Kind !=
                Avm2VerifierTypeKind.Unknown)
            {
                throw new ArgumentException(
                    "Declaring scope has an extra verifier type without an extra local scope requirement.",
                    nameof(context));
            }
            return;
        }
        if (context.ExtraVerifierType.Kind is
            Avm2VerifierTypeKind.Unknown or
            Avm2VerifierTypeKind.Null or
            Avm2VerifierTypeKind.Void)
        {
            throw new ArgumentException(
                "Declaring scope has an invalid extra verifier type.",
                nameof(context));
        }
    }

    static bool ExactScopeTypeMatches(
        Avm2VerifierType verifier,
        string exact_runtime,
        Avm2VerifierTypeRegistry verifier_types)
    {
        if (string.IsNullOrWhiteSpace(exact_runtime))
            return false;
        return verifier.Kind switch
        {
            Avm2VerifierTypeKind.Any => true,
            Avm2VerifierTypeKind.Known =>
                verifier_types.IsAssignable(
                    verifier.Identity,
                    exact_runtime),
            _ => false
        };
    }

    static Avm2DataFlowScopeContext? ImmutableScopeContext(
        Avm2DataFlowScopeContext? context)
    {
        if (context is null)
            return null;
        ArgumentNullException.ThrowIfNull(
            context.DeclaringScope);
        Avm2DataFlowScopeValue[] values = context
            .DeclaringScope
            .Select(value =>
            {
                ArgumentNullException.ThrowIfNull(value);
                return new Avm2DataFlowScopeValue
                {
                    Provenance = value.Provenance,
                    TypeHint = value.TypeHint,
                    VerifierType = value.VerifierType,
                    ExactRuntimeTypeIdentity =
                        value.ExactRuntimeTypeIdentity,
                    Literal = value.Literal,
                    IsWith = value.IsWith
                };
            })
            .ToArray();
        return new Avm2DataFlowScopeContext
        {
            DeclaringScope = Array.AsReadOnly(values),
            HasExtraVerifierType =
                context.HasExtraVerifierType,
            ExtraVerifierType =
                context.ExtraVerifierType
        };
    }

    internal static bool ScopeContextsEqual(
        Avm2DataFlowScopeContext? left,
        Avm2DataFlowScopeContext? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null ||
            right is null ||
            left.CapturedScopeSize !=
                right.CapturedScopeSize ||
            left.FullScopeSize != right.FullScopeSize ||
            left.HasExtraVerifierType !=
                right.HasExtraVerifierType ||
            left.ExtraVerifierType !=
                right.ExtraVerifierType)
        {
            return false;
        }
        for (int index = 0;
            index < left.DeclaringScope.Count;
            index++)
        {
            Avm2DataFlowScopeValue left_value =
                left.DeclaringScope[index];
            Avm2DataFlowScopeValue right_value =
                right.DeclaringScope[index];
            if (!string.Equals(
                    left_value.Provenance,
                    right_value.Provenance,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    left_value.TypeHint,
                    right_value.TypeHint,
                    StringComparison.Ordinal) ||
                left_value.VerifierType !=
                    right_value.VerifierType ||
                !string.Equals(
                    left_value.ExactRuntimeTypeIdentity,
                    right_value.ExactRuntimeTypeIdentity,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    left_value.Literal,
                    right_value.Literal,
                    StringComparison.Ordinal) ||
                left_value.IsWith != right_value.IsWith)
            {
                return false;
            }
        }
        return true;
    }

    internal static string IntegrityFingerprint(
        Avm2DataFlowAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var text = new StringBuilder();
        void Add(object? value)
        {
            string item = Convert.ToString(
                value,
                CultureInfo.InvariantCulture) ?? "";
            text.Append(item.Length)
                .Append(':')
                .Append(item)
                .Append(';');
        }
        void AddType(Avm2VerifierType type)
        {
            Add(type.Kind);
            Add(type.Identity);
        }
        void AddVector(IEnumerable<string> values)
        {
            foreach (string value in values)
                Add(value);
            Add("\u001e");
        }
        void AddState(Avm2DataFlowState state)
        {
            AddVector(state.Stack);
            AddVector(state.Scope);
            AddVector(state.Locals);
        }

        Add(analysis.FormatVersion);
        Add(analysis.Complete);
        Avm2DataFlowScopeContext? scope =
            analysis.DeclaringScopeContext;
        Add(scope is not null);
        if (scope is not null)
        {
            Add(scope.CapturedScopeSize);
            Add(scope.FullScopeSize);
            Add(scope.HasExtraVerifierType);
            AddType(scope.ExtraVerifierType);
            foreach (Avm2DataFlowScopeValue value in
                scope.DeclaringScope)
            {
                Add(value.Provenance);
                Add(value.TypeHint);
                AddType(value.VerifierType);
                Add(value.ExactRuntimeTypeIdentity);
                Add(value.Literal);
                Add(value.IsWith);
            }
        }
        foreach (Avm2DataFlowValue value in
            analysis.Values)
        {
            Add(value.Id);
            Add(value.Kind);
            Add(value.Definition);
            Add(value.TypeHint);
            AddType(value.VerifierType);
            Add(value.ExactRuntimeTypeIdentity);
            Add(value.Literal);
            Add(value.Block);
            Add(value.Instruction);
            AddVector(value.Sources);
        }
        Add("\u001d");
        foreach (Avm2DataFlowOperation operation in
            analysis.Operations)
        {
            Add(operation.Instruction);
            Add(operation.Offset);
            Add(operation.Block);
            Add(operation.Opcode);
            Add(operation.Unreachable);
            AddVector(operation.Inputs);
            AddVector(operation.Outputs);
            AddVector(operation.Definitions);
            AddVector(operation.StackBefore);
            AddVector(operation.StackAfter);
            AddVector(operation.ScopeBefore);
            AddVector(operation.ScopeAfter);
            foreach ((int register, string value) in
                operation.LocalWrites)
            {
                Add(register);
                Add(value);
            }
            Add("\u001c");
            foreach ((string name, string? value) in
                operation.Operands)
            {
                Add(name);
                Add(value);
            }
            Add("\u001b");
        }
        Add("\u001a");
        foreach (Avm2DataFlowBlock block in
            analysis.Blocks)
        {
            Add(block.Id);
            Add(block.Unreachable);
            foreach (int instruction in block.Instructions)
                Add(instruction);
            Add("\u0019");
            AddState(block.Entry);
            AddState(block.Exit);
        }
        Add("\u0018");
        foreach (Avm2DataFlowPhi phi in
            analysis.Phis)
        {
            Add(phi.Value);
            Add(phi.Block);
            Add(phi.State);
            Add(phi.Index);
            foreach (Avm2DataFlowPhiInput input in
                phi.Inputs)
            {
                Add(input.FromBlock);
                Add(input.EdgeKind);
                Add(input.SourceInstruction);
                Add(input.Value);
            }
            Add("\u0017");
        }
        foreach (Avm2DataFlowDiagnostic diagnostic in
            analysis.Diagnostics)
        {
            Add(diagnostic.Severity);
            Add(diagnostic.Code);
            Add(diagnostic.Message);
            Add(diagnostic.Block);
            Add(diagnostic.Instruction);
        }
        foreach ((int instruction, IReadOnlyList<bool?> values) in
            analysis.ScopeWithBefore
                .OrderBy(value => value.Key))
        {
            Add(instruction);
            foreach (bool? value in values)
                Add(value);
            Add("\u0016");
        }
        byte[] hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }

    static List<ASInstruction?> ReadCode(
        ASMethodBody body,
        Avm2MethodAnalysis method,
        Dictionary<string, Avm2DataFlowDiagnostic> diagnostics)
    {
        List<ASInstruction?> code =
            method.DecodedCode.Cast<ASInstruction?>().ToList();
        if (code.Count != method.Instructions.Count)
        {
            diagnostics[$"instruction-count|||{code.Count}:{method.Instructions.Count}"] =
                new Avm2DataFlowDiagnostic
                {
                    Severity = "Error",
                    Code = "instruction-count",
                    Message = $"Decoded {code.Count} instructions but analysis contains {method.Instructions.Count}."
                };
        }
        if (code.Count < method.Instructions.Count)
            code.AddRange(Enumerable.Repeat<ASInstruction?>(null, method.Instructions.Count - code.Count));
        if (code.Count > method.Instructions.Count)
            code.RemoveRange(method.Instructions.Count, code.Count - method.Instructions.Count);
        return code;
    }

    static IReadOnlyList<string>? CreateDeclaringScope(
        Avm2DataFlowScopeContext? scope_context,
        Dictionary<string, MutableValue> values)
    {
        if (scope_context is null)
        {
            RegisterValue(
                values,
                UnknownDeclaringScope,
                "UnknownDeclaringScope",
                "declaring-scope:unknown",
                "*",
                null);
            return null;
        }
        ArgumentNullException.ThrowIfNull(scope_context.DeclaringScope);

        var declaring_scope = new List<string>(scope_context.DeclaringScope.Count);
        for (int index = 0; index < scope_context.DeclaringScope.Count; index++)
        {
            Avm2DataFlowScopeValue source = scope_context.DeclaringScope[index] ??
                throw new ArgumentException(
                    $"Declaring scope value {index} is null.",
                    nameof(scope_context));
            ArgumentException.ThrowIfNullOrWhiteSpace(source.Provenance);
            string id = $"v_declaring_scope_{index}";
            RegisterValue(
                values,
                id,
                "DeclaringScope",
                source.Provenance,
                source.TypeHint ?? "Object",
                source.Literal,
                verifier_type: source.VerifierType,
                exact_runtime_type_identity:
                    source.ExactRuntimeTypeIdentity);
            declaring_scope.Add(id);
        }
        return declaring_scope.AsReadOnly();
    }

    static FlowState CreateEntryState(
        ASMethodBody body,
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver,
        Avm2VerifierTypeRegistry verifier_types,
        Dictionary<string, MutableValue> values,
        Dictionary<string, Avm2DataFlowDiagnostic> diagnostics)
    {
        var locals = new List<string>(Math.Max(body.LocalCount, 0));
        for (int index = 0; index < body.LocalCount; index++)
        {
            string id = $"v_entry_local_{index}";
            string kind;
            string? type;
            TypeProof type_proof = default;
            string? literal = null;
            if (index == 0)
            {
                kind = "This";
                type_proof = ReceiverTypeProof(
                    binding,
                    exact_receiver);
                type = exact_receiver is not null
                    ? SafeType(
                        () => Avm2MethodAnalyzer.Qualified(
                            exact_receiver.RuntimeType.QName),
                        "*")
                    : binding?.Scope is
                    Avm2MethodBindingScope.ClassInstance or
                    Avm2MethodBindingScope.ClassStatic
                        ? SafeType(
                            () => Avm2MethodAnalyzer.Qualified(binding.Owner.QName),
                            "*")
                        : "*";
            }
            else if (index <= body.Method.Parameters.Count)
            {
                kind = "Parameter";
                type_proof = new TypeProof(
                    SymbolVerifierType(
                        body.ABC,
                        verifier_types,
                        body.Method.Parameters[index - 1].Type),
                    null);
                type = SafeType(
                    () => Avm2MethodAnalyzer.Qualified(body.Method.Parameters[index - 1].Type),
                    "*");
            }
            else if (index == body.Method.Parameters.Count + 1 &&
                body.Method.Flags.HasFlag(MethodFlags.NeedArguments))
            {
                kind = "Arguments";
                type = "Arguments";
                type_proof = KnownType(
                    "builtin:arguments",
                    false);
            }
            else if (index == body.Method.Parameters.Count + 1 &&
                body.Method.Flags.HasFlag(MethodFlags.NeedRest))
            {
                kind = "Rest";
                type = "Array";
                type_proof = KnownType(
                    "builtin:array",
                    false);
            }
            else
            {
                kind = "Undefined";
                type = "undefined";
                literal = "undefined";
                type_proof = new TypeProof(
                    Avm2VerifierType.Void,
                    "builtin:void");
            }
            RegisterValue(
                values,
                id,
                kind,
                $"entry:local:{index}",
                type,
                literal,
                verifier_type: type_proof.Verifier,
                exact_runtime_type_identity:
                    type_proof.ExactRuntime);
            locals.Add(id);
        }
        int required_locals = body.Method.Parameters.Count + 1;
        if (body.Method.Flags.HasFlag(MethodFlags.NeedArguments) ||
            body.Method.Flags.HasFlag(MethodFlags.NeedRest))
        {
            required_locals++;
        }
        if (body.LocalCount < required_locals)
        {
            diagnostics[$"local-count|||{body.LocalCount}:{required_locals}"] =
                new Avm2DataFlowDiagnostic
                {
                    Severity = "Error",
                    Code = "local-count",
                    Message = $"Method declares {body.LocalCount} locals but requires at least {required_locals}."
                };
        }

        return new FlowState
        {
            Stack = [],
            Scope = [],
            ScopeWith = [],
            Locals = locals
        };
    }

    static void ValidateCoverage(AnalysisContext context)
    {
        try
        {
            Avm2InstructionSemantics.VerifyCoverage();
        }
        catch (Exception exception)
        {
            AddDiagnostic(context, "Error", "opcode-coverage", exception.Message);
        }

        foreach (Avm2InstructionInventory instruction in context.Method.Instructions)
        {
            if (instruction.PopCount < 0 || instruction.PushCount < 0)
            {
                AddDiagnostic(
                    context,
                    "Error",
                    "invalid-stack-effect",
                    $"{instruction.Opcode} has stack effect {instruction.PopCount}/{instruction.PushCount}.",
                    instruction.Block,
                    instruction.Index);
            }
            if (instruction.Index < 0 || instruction.Index >= context.Code.Count)
                continue;
            ASInstruction? decoded = context.Code[instruction.Index];
            if (decoded is null)
                continue;
            if (!string.Equals(decoded.OP.ToString(), instruction.Opcode, StringComparison.Ordinal))
            {
                AddDiagnostic(
                    context,
                    "Error",
                    "opcode-mismatch",
                    $"Decoded {decoded.OP} but analysis records {instruction.Opcode}.",
                    instruction.Block,
                    instruction.Index);
            }
        }
    }

    static void ValidateGraph(AnalysisContext context)
    {
        HashSet<int> block_ids = context.Method.ControlFlow.Blocks
            .Select(block => block.Id)
            .ToHashSet();
        foreach (IndexedEdge indexed in context.Edges)
        {
            Avm2ControlFlowEdgeInventory edge = indexed.Edge;
            if (!block_ids.Contains(edge.FromBlock))
            {
                AddDiagnostic(
                    context,
                    "Error",
                    "edge-source",
                    $"Edge {indexed.Index} references missing source block {edge.FromBlock}.");
            }
            if (edge.ToBlock.HasValue && !block_ids.Contains(edge.ToBlock.Value))
            {
                AddDiagnostic(
                    context,
                    "Error",
                    "edge-target",
                    $"Edge {indexed.Index} references missing target block {edge.ToBlock.Value}.");
            }
        }
        foreach (string diagnostic in context.Method.Diagnostics)
        {
            bool error = diagnostic.Contains("underflow", StringComparison.OrdinalIgnoreCase) ||
                diagnostic.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
                diagnostic.Contains("unresolved", StringComparison.OrdinalIgnoreCase) ||
                diagnostic.Contains("invalid", StringComparison.OrdinalIgnoreCase);
            AddDiagnostic(
                context,
                error ? "Error" : "Warning",
                "control-flow",
                diagnostic);
        }
        if (!context.Method.ControlFlow.Complete)
        {
            AddDiagnostic(
                context,
                "Error",
                "control-flow-incomplete",
                "The input control-flow graph is incomplete.");
        }
    }

    static FlowState? MergeEntry(
        AnalysisContext context,
        Avm2BasicBlockInventory block,
        Dictionary<int, BlockTransfer> transfers)
    {
        List<IncomingState> incoming = IncomingStates(context, block, transfers);
        if (incoming.Count == 0)
            return null;
        int expected_stack = block.EntryStackDepth ?? incoming[0].State.Stack.Count;
        int expected_scope = block.EntryScopeDepth.HasValue
            ? LocalScopeDepth(context.Body, block.EntryScopeDepth.Value)
            : incoming[0].State.Scope.Count;
        int local_count = context.Body.LocalCount;
        ValidateIncomingDepths(context, block, incoming, expected_stack, expected_scope, local_count);
        return new FlowState
        {
            Stack = MergeVector(context, block.Id, "Stack", expected_stack, incoming),
            Scope = MergeVector(context, block.Id, "Scope", expected_scope, incoming),
            ScopeWith = MergeScopeWith(
                context,
                block.Id,
                expected_scope,
                incoming),
            Locals = MergeVector(context, block.Id, "Local", local_count, incoming)
        };
    }

    static List<IncomingState> IncomingStates(
        AnalysisContext context,
        Avm2BasicBlockInventory block,
        Dictionary<int, BlockTransfer> transfers)
    {
        var incoming = new List<IncomingState>();
        if (block.Id == context.Method.ControlFlow.EntryBlock)
            incoming.Add(new IncomingState(null, "Entry", null, context.EntryState));
        foreach (IndexedEdge indexed in context.IncomingEdges.GetValueOrDefault(block.Id) ?? [])
        {
            Avm2ControlFlowEdgeInventory edge = indexed.Edge;
            if (!context.BlocksById.TryGetValue(
                    edge.FromBlock,
                    out Avm2BasicBlockInventory? source_block) ||
                !source_block.Reachable)
                continue;
            if (!transfers.TryGetValue(edge.FromBlock, out BlockTransfer? transfer))
                continue;
            FlowState state;
            if (edge.Kind == "Exception")
            {
                FlowState source = transfer.Before.GetValueOrDefault(edge.SourceInstruction) ??
                    transfer.Exit;
                string exception_id = $"v_exception_edge_{indexed.Index}";
                string type = string.IsNullOrWhiteSpace(edge.ExceptionType) ? "*" : edge.ExceptionType;
                RegisterValue(
                    context.Values,
                    exception_id,
                    "Exception",
                    $"edge:{indexed.Index}:exception",
                    type,
                    null,
                    edge.ToBlock,
                    edge.SourceInstruction);
                state = new FlowState
                {
                    Stack = [exception_id],
                    Scope = [.. context.EntryState.Scope],
                    ScopeWith = [.. context.EntryState.ScopeWith],
                    Locals = [.. source.Locals]
                };
            }
            else
            {
                state = transfer.Exit;
            }
            incoming.Add(new IncomingState(
                edge.FromBlock,
                edge.Kind,
                edge.SourceInstruction,
                state));
        }
        return incoming;
    }

    static void ValidateIncomingDepths(
        AnalysisContext context,
        Avm2BasicBlockInventory block,
        List<IncomingState> incoming,
        int expected_stack,
        int expected_scope,
        int expected_locals)
    {
        foreach (IncomingState source in incoming)
        {
            if (source.State.Stack.Count != expected_stack)
            {
                AddDiagnostic(
                    context,
                    "Error",
                    "stack-depth",
                    $"Block {block.Id} expects stack depth {expected_stack} but {SourceName(source)} provides {source.State.Stack.Count}.",
                    block.Id,
                    source.SourceInstruction);
            }
            if (source.State.Scope.Count != expected_scope)
            {
                AddDiagnostic(
                    context,
                    "Error",
                    "scope-depth",
                    $"Block {block.Id} expects scope depth {expected_scope} but {SourceName(source)} provides {source.State.Scope.Count}.",
                    block.Id,
                    source.SourceInstruction);
            }
            if (source.State.Locals.Count != expected_locals)
            {
                AddDiagnostic(
                    context,
                    "Error",
                    "local-depth",
                    $"Block {block.Id} expects {expected_locals} locals but {SourceName(source)} provides {source.State.Locals.Count}.",
                    block.Id,
                    source.SourceInstruction);
            }
        }
    }

    static string SourceName(IncomingState source) =>
        source.FromBlock.HasValue ? $"block {source.FromBlock.Value}" : "method entry";

    static List<string> MergeVector(
        AnalysisContext context,
        int block,
        string state,
        int count,
        List<IncomingState> incoming)
    {
        var result = new List<string>(Math.Max(count, 0));
        for (int index = 0; index < count; index++)
        {
            List<string> candidates = incoming
                .Select(source => ReadStateValue(source.State, state, index))
                .Where(value => value is not null)
                .Cast<string>()
                .ToList();
            if (candidates.Count == 0)
            {
                string missing = $"v_missing_b{block}_{state.ToLowerInvariant()}_{index}";
                RegisterValue(
                    context.Values,
                    missing,
                    "Unknown",
                    $"block:{block}:{state.ToLowerInvariant()}:{index}:missing",
                    "*",
                    null,
                    block);
                result.Add(missing);
                AddDiagnostic(
                    context,
                    "Error",
                    "missing-state-value",
                    $"Block {block} has no value for {state.ToLowerInvariant()} {index}.",
                    block);
                continue;
            }
            string phi_id = $"v_phi_b{block}_{state.ToLowerInvariant()}_{index}";
            bool different = candidates.Distinct(StringComparer.Ordinal).Skip(1).Any();
            if (different)
                context.ForcedPhis.Add(phi_id);
            if (context.ForcedPhis.Contains(phi_id))
            {
                RegisterValue(
                    context.Values,
                    phi_id,
                    "Phi",
                    $"block:{block}:{state.ToLowerInvariant()}:{index}:phi",
                    null,
                    null,
                    block);
                result.Add(phi_id);
            }
            else
            {
                result.Add(candidates[0]);
            }
        }
        return result;
    }

    static string? ReadStateValue(FlowState state, string kind, int index)
    {
        List<string> values = kind switch
        {
            "Stack" => state.Stack,
            "Scope" => state.Scope,
            _ => state.Locals
        };
        return index >= 0 && index < values.Count ? values[index] : null;
    }

    static List<bool?> MergeScopeWith(
        AnalysisContext context,
        int block,
        int count,
        List<IncomingState> incoming)
    {
        var result = new List<bool?>(Math.Max(count, 0));
        for (int index = 0; index < count; index++)
        {
            bool?[] candidates = incoming
                .Where(source => index < source.State.ScopeWith.Count)
                .Select(source => source.State.ScopeWith[index])
                .Distinct()
                .ToArray();
            if (candidates.Length == 1)
            {
                result.Add(candidates[0]);
                continue;
            }
            result.Add(null);
            AddDiagnostic(
                context,
                "Error",
                "scope-with-merge",
                $"Block {block} merges incompatible scope kinds at index {index}.",
                block);
        }
        return result;
    }

    static BlockTransfer TransferBlock(
        AnalysisContext context,
        Avm2BasicBlockInventory block,
        FlowState entry,
        bool unreachable)
    {
        FlowState state = entry.Copy();
        var before = new Dictionary<int, FlowState>();
        var operations = new List<Avm2DataFlowOperation>();
        for (int index = block.FirstInstruction; index <= block.LastInstruction; index++)
        {
            if (index < 0 || index >= context.Method.Instructions.Count)
            {
                AddDiagnostic(
                    context,
                    "Error",
                    "instruction-range",
                    $"Block {block.Id} references instruction {index} outside the method.",
                    block.Id,
                    index);
                continue;
            }
            if (context.ExceptionSources.Contains(index))
                before[index] = state.Copy();
            Avm2InstructionInventory inventory = context.Method.Instructions[index];
            ASInstruction? instruction = index < context.Code.Count ? context.Code[index] : null;
            operations.Add(TransferInstruction(
                context,
                block.Id,
                inventory,
                instruction,
                state,
                unreachable));
        }
        return new BlockTransfer
        {
            Exit = state,
            Before = before,
            Operations = operations
        };
    }

    static Avm2DataFlowOperation TransferInstruction(
        AnalysisContext context,
        int block,
        Avm2InstructionInventory inventory,
        ASInstruction? instruction,
        FlowState state,
        bool unreachable)
    {
        List<string> stack_before = [.. state.Stack];
        List<string> scope_before = [.. state.Scope];
        List<bool?> scope_with_before = [.. state.ScopeWith];
        var inputs = new List<string>();
        var outputs = new List<string>();
        var definitions = new List<string>();
        var local_writes = new SortedDictionary<int, string>();
        OPCode? op = instruction?.OP;

        if (op is OPCode.GetLocal or OPCode.GetLocal_0 or OPCode.GetLocal_1 or
            OPCode.GetLocal_2 or OPCode.GetLocal_3)
        {
            int register = instruction is Local local ? local.Register : -1;
            string value = ReadLocal(
                context,
                state,
                register,
                block,
                inventory.Index,
                !unreachable);
            inputs.Add(value);
            outputs.Add(value);
            state.Stack.Add(value);
        }
        else if (op is OPCode.SetLocal or OPCode.SetLocal_0 or OPCode.SetLocal_1 or
            OPCode.SetLocal_2 or OPCode.SetLocal_3)
        {
            List<string> popped = PopStack(
                context,
                state,
                inventory.PopCount,
                block,
                inventory.Index,
                !unreachable);
            inputs.AddRange(popped);
            string value = popped.Count == 0
                ? MissingValue(context, block, inventory.Index, "setlocal")
                : popped[^1];
            int register = instruction is Local local ? local.Register : -1;
            WriteLocal(
                context,
                state,
                register,
                value,
                block,
                inventory.Index,
                local_writes,
                !unreachable);
            outputs.Add(value);
        }
        else if (op is OPCode.IncLocal or OPCode.IncLocal_i or
            OPCode.DecLocal or OPCode.DecLocal_i)
        {
            int register = instruction is Local local ? local.Register : -1;
            string input = ReadLocal(
                context,
                state,
                register,
                block,
                inventory.Index,
                !unreachable);
            inputs.Add(input);
            string output = $"v_i{inventory.Index}_local_{register}";
            string type = op is OPCode.IncLocal_i or OPCode.DecLocal_i ? "int" : "Number";
            RegisterValue(
                context.Values,
                output,
                "Instruction",
                $"instruction:{inventory.Index}:local:{register}",
                type,
                null,
                block,
                inventory.Index,
                [input]);
            WriteLocal(
                context,
                state,
                register,
                output,
                block,
                inventory.Index,
                local_writes,
                !unreachable);
            outputs.Add(output);
            definitions.Add(output);
        }
        else if (op == OPCode.Kill)
        {
            int register = instruction is Local local ? local.Register : -1;
            string output = $"v_i{inventory.Index}_local_{register}";
            RegisterValue(
                context.Values,
                output,
                "Undefined",
                $"instruction:{inventory.Index}:local:{register}",
                "undefined",
                "undefined",
                block,
                inventory.Index);
            WriteLocal(
                context,
                state,
                register,
                output,
                block,
                inventory.Index,
                local_writes,
                !unreachable);
            outputs.Add(output);
            definitions.Add(output);
        }
        else if (op == OPCode.HasNext2 && instruction is HasNext2Ins has_next)
        {
            string object_input = ReadLocal(
                context,
                state,
                has_next.ObjectIndex,
                block,
                inventory.Index,
                !unreachable);
            string index_input = ReadLocal(
                context,
                state,
                has_next.RegisterIndex,
                block,
                inventory.Index,
                !unreachable);
            inputs.Add(object_input);
            inputs.Add(index_input);
            string object_output = $"v_i{inventory.Index}_local_{has_next.ObjectIndex}";
            string index_output = $"v_i{inventory.Index}_local_{has_next.RegisterIndex}";
            string result = $"v_i{inventory.Index}_out_0";
            RegisterValue(
                context.Values,
                object_output,
                "Instruction",
                $"instruction:{inventory.Index}:local:{has_next.ObjectIndex}",
                "Object",
                null,
                block,
                inventory.Index,
                [object_input, index_input]);
            RegisterValue(
                context.Values,
                index_output,
                "Instruction",
                $"instruction:{inventory.Index}:local:{has_next.RegisterIndex}",
                "int",
                null,
                block,
                inventory.Index,
                [object_input, index_input]);
            RegisterValue(
                context.Values,
                result,
                "Instruction",
                $"instruction:{inventory.Index}:output:0",
                "Boolean",
                null,
                block,
                inventory.Index,
                [object_input, index_input]);
            WriteLocal(
                context,
                state,
                has_next.ObjectIndex,
                object_output,
                block,
                inventory.Index,
                local_writes,
                !unreachable);
            WriteLocal(
                context,
                state,
                has_next.RegisterIndex,
                index_output,
                block,
                inventory.Index,
                local_writes,
                !unreachable);
            state.Stack.Add(result);
            outputs.Add(object_output);
            outputs.Add(index_output);
            outputs.Add(result);
            definitions.Add(object_output);
            definitions.Add(index_output);
            definitions.Add(result);
        }
        else if (op == OPCode.Dup)
        {
            List<string> popped = PopStack(
                context,
                state,
                1,
                block,
                inventory.Index,
                !unreachable);
            inputs.AddRange(popped);
            string value = popped.Count == 0
                ? MissingValue(context, block, inventory.Index, "dup")
                : popped[^1];
            state.Stack.Add(value);
            state.Stack.Add(value);
            outputs.Add(value);
            outputs.Add(value);
        }
        else if (op == OPCode.Swap)
        {
            List<string> popped = PopStack(
                context,
                state,
                2,
                block,
                inventory.Index,
                !unreachable);
            inputs.AddRange(popped);
            if (popped.Count == 2)
            {
                state.Stack.Add(popped[1]);
                state.Stack.Add(popped[0]);
                outputs.Add(popped[1]);
                outputs.Add(popped[0]);
            }
        }
        else if (op is OPCode.PushScope or OPCode.PushWith)
        {
            List<string> popped = PopStack(
                context,
                state,
                1,
                block,
                inventory.Index,
                !unreachable);
            inputs.AddRange(popped);
            string value = popped.Count == 0
                ? MissingValue(context, block, inventory.Index, "scope")
                : popped[^1];
            state.Scope.Add(value);
            state.ScopeWith.Add(op == OPCode.PushWith);
            outputs.Add(value);
        }
        else if (op == OPCode.PopScope)
        {
            if (state.Scope.Count == 0)
            {
                AddDiagnostic(
                    context,
                    unreachable ? "Warning" : "Error",
                    "scope-underflow",
                    $"Instruction {inventory.Index} pops an empty local scope stack.",
                    block,
                    inventory.Index);
            }
            else
            {
                string value = state.Scope[^1];
                state.Scope.RemoveAt(state.Scope.Count - 1);
                state.ScopeWith.RemoveAt(state.ScopeWith.Count - 1);
                inputs.Add(value);
            }
        }
        else if (op == OPCode.GetScopeObject)
        {
            int index = instruction is GetScopeObjectIns scope
                ? scope.ScopeIndex
                : -1;
            string value = ReadScope(
                context,
                state,
                index,
                block,
                inventory.Index,
                !unreachable);
            inputs.Add(value);
            outputs.Add(value);
            state.Stack.Add(value);
        }
        else if (op == OPCode.GetOuterScope)
        {
            int index = instruction is GetOuterScopeIns outer
                ? outer.ScopeIndex
                : -1;
            string value = ReadDeclaringScope(
                context,
                index,
                block,
                inventory.Index,
                !unreachable);
            inputs.Add(value);
            outputs.Add(value);
            state.Stack.Add(value);
        }
        else if (op == OPCode.GetGlobalScope)
        {
            string value = ReadGlobalScope(
                context,
                state,
                block,
                inventory.Index,
                !unreachable);
            inputs.Add(value);
            outputs.Add(value);
            state.Stack.Add(value);
        }
        else
        {
            List<string> popped = PopStack(
                context,
                state,
                inventory.PopCount,
                block,
                inventory.Index,
                !unreachable);
            inputs.AddRange(popped);
            if (op is OPCode.FindProperty or OPCode.FindPropStrict or OPCode.GetLex)
            {
                bool exact_top =
                    ExactTopLocalScopeBindsProperty(context, state, instruction);
                if (!exact_top)
                {
                    RequireDeclaringScope(
                        context,
                        block,
                        inventory.Index,
                        !unreachable);
                }
                inputs.AddRange(exact_top
                    ? state.Scope
                    : EffectiveScope(context, state));
            }
            if (op is OPCode.NewFunction or OPCode.NewClass)
            {
                RequireDeclaringScope(
                    context,
                    block,
                    inventory.Index,
                    !unreachable);
                inputs.AddRange(EffectiveScope(context, state));
            }
            if (op is OPCode.GetGlobalSlot or OPCode.SetGlobalSlot)
                inputs.Add(ReadGlobalScope(
                    context,
                    state,
                    block,
                    inventory.Index,
                    !unreachable));
            string? type = ResultType(instruction);
            TypeProof type_proof =
                ResultTypeProof(context, instruction, popped);
            string? literal = Literal(instruction);
            for (int output_index = 0; output_index < inventory.PushCount; output_index++)
            {
                string output = $"v_i{inventory.Index}_out_{output_index}";
                RegisterValue(
                    context.Values,
                    output,
                    "Instruction",
                    $"instruction:{inventory.Index}:output:{output_index}",
                    type,
                    literal,
                    block,
                    inventory.Index,
                    inputs,
                    type_proof.Verifier,
                    type_proof.ExactRuntime);
                state.Stack.Add(output);
                outputs.Add(output);
                definitions.Add(output);
            }
        }

        int expected_stack = stack_before.Count - inventory.PopCount + inventory.PushCount;
        if (expected_stack >= 0 && state.Stack.Count != expected_stack)
        {
            AddDiagnostic(
                context,
                unreachable ? "Warning" : "Error",
                "stack-transfer",
                $"{inventory.Opcode} produces stack depth {state.Stack.Count}, expected {expected_stack}.",
                block,
                inventory.Index);
        }
        int scope_delta = op is OPCode.PushScope or OPCode.PushWith
            ? 1
            : op == OPCode.PopScope ? -1 : 0;
        int expected_scope = scope_before.Count + scope_delta;
        if (expected_scope >= 0 && state.Scope.Count != expected_scope)
        {
            AddDiagnostic(
                context,
                unreachable ? "Warning" : "Error",
                "scope-transfer",
                $"{inventory.Opcode} produces scope depth {state.Scope.Count}, expected {expected_scope}.",
                block,
                inventory.Index);
        }
        context.ScopeWithBefore[inventory.Index] =
            EffectiveScopeWith(context, scope_with_before).AsReadOnly();

        return new Avm2DataFlowOperation
        {
            Instruction = inventory.Index,
            Offset = inventory.Offset,
            Block = block,
            Opcode = inventory.Opcode,
            Unreachable = unreachable,
            Inputs = inputs,
            Outputs = outputs,
            Definitions = definitions,
            StackBefore = stack_before,
            StackAfter = [.. state.Stack],
            ScopeBefore = EffectiveScope(context, scope_before),
            ScopeAfter = EffectiveScope(context, state),
            LocalWrites = local_writes,
            Operands = new SortedDictionary<string, string?>(
                inventory.Operands,
                StringComparer.Ordinal)
        };
    }

    static List<string> PopStack(
        AnalysisContext context,
        FlowState state,
        int count,
        int block,
        int instruction,
        bool reachable)
    {
        if (count <= 0)
            return [];
        var values = new List<string>(count);
        int missing = Math.Max(0, count - state.Stack.Count);
        for (int index = 0; index < missing; index++)
        {
            string id = $"v_i{instruction}_missing_stack_{index}";
            RegisterValue(
                context.Values,
                id,
                "Unknown",
                $"instruction:{instruction}:missing-stack:{index}",
                "*",
                null,
                block,
                instruction);
            values.Add(id);
        }
        if (missing > 0)
        {
            AddDiagnostic(
                context,
                reachable ? "Error" : "Warning",
                "stack-underflow",
                $"Instruction {instruction} needs {count} stack values but has {state.Stack.Count}.",
                block,
                instruction);
        }
        int available = Math.Min(count, state.Stack.Count);
        int start = state.Stack.Count - available;
        values.AddRange(state.Stack.GetRange(start, available));
        state.Stack.RemoveRange(start, available);
        return values;
    }

    static string ReadLocal(
        AnalysisContext context,
        FlowState state,
        int register,
        int block,
        int instruction,
        bool reachable)
    {
        if (register >= 0 && register < state.Locals.Count)
            return state.Locals[register];
        AddDiagnostic(
            context,
            reachable ? "Error" : "Warning",
            "local-index",
            $"Instruction {instruction} reads invalid local {register}.",
            block,
            instruction);
        return MissingValue(context, block, instruction, $"local-{register}");
    }

    static void WriteLocal(
        AnalysisContext context,
        FlowState state,
        int register,
        string value,
        int block,
        int instruction,
        SortedDictionary<int, string> writes,
        bool reachable)
    {
        if (register < 0 || register >= state.Locals.Count)
        {
            AddDiagnostic(
                context,
                reachable ? "Error" : "Warning",
                "local-index",
                $"Instruction {instruction} writes invalid local {register}.",
                block,
                instruction);
            return;
        }
        state.Locals[register] = value;
        writes[register] = value;
    }

    static string ReadScope(
        AnalysisContext context,
        FlowState state,
        int index,
        int block,
        int instruction,
        bool reachable)
    {
        if (index >= 0 && index < state.Scope.Count)
            return state.Scope[index];
        AddDiagnostic(
            context,
            reachable ? "Error" : "Warning",
            "scope-index",
            $"Instruction {instruction} reads invalid scope {index}.",
            block,
            instruction);
        return MissingValue(context, block, instruction, $"scope-{index}");
    }

    static string ReadDeclaringScope(
        AnalysisContext context,
        int index,
        int block,
        int instruction,
        bool reachable)
    {
        if (context.DeclaringScope is null)
        {
            AddDiagnostic(
                context,
                reachable ? "Error" : "Warning",
                "declaring-scope-unavailable",
                $"Instruction {instruction} requires a proven declaring scope.",
                block,
                instruction);
            return MissingValue(
                context,
                block,
                instruction,
                $"declaring-scope-{index}");
        }
        if (index >= 0 && index < context.DeclaringScope.Count)
            return context.DeclaringScope[index];
        AddDiagnostic(
            context,
            reachable ? "Error" : "Warning",
            "declaring-scope-index",
            $"Instruction {instruction} reads invalid declaring scope {index}.",
            block,
            instruction);
        return MissingValue(
            context,
            block,
            instruction,
            $"declaring-scope-{index}");
    }

    static bool ExactTopLocalScopeBindsProperty(
        AnalysisContext context,
        FlowState state,
        ASInstruction? instruction)
    {
        if (context.DeclaringScope is not null ||
            state.Scope.Count == 0 ||
            state.Scope[^1] != "v_entry_local_0" ||
            context.Binding is null ||
            context.Binding.Scope is not (
                Avm2MethodBindingScope.ClassInstance or
                Avm2MethodBindingScope.ClassStatic))
        {
            return false;
        }

        ASMultiname? property = instruction switch
        {
            FindPropertyIns find => find.PropertyName,
            FindPropStrictIns strict => strict.PropertyName,
            GetLexIns lexical => lexical.TypeName,
            _ => null
        };
        if (property is null ||
            property.Kind is not (MultinameKind.QName or MultinameKind.QNameA) ||
            property.Namespace?.Kind != NamespaceKind.Private)
        {
            return false;
        }

        string identity = Avm2MethodAnalyzer.ExactSymbolIdentity(property);
        return context.Binding.Owner.Traits.Any(trait =>
        {
            try
            {
                ASMultiname? name = trait.QName;
                return name is not null &&
                    name.Kind is MultinameKind.QName or MultinameKind.QNameA &&
                    name.Namespace?.Kind == NamespaceKind.Private &&
                    Avm2MethodAnalyzer.ExactSymbolIdentity(name) == identity;
            }
            catch
            {
                return false;
            }
        });
    }

    static bool RequireDeclaringScope(
        AnalysisContext context,
        int block,
        int instruction,
        bool reachable)
    {
        if (context.DeclaringScope is not null)
            return true;
        AddDiagnostic(
            context,
            reachable ? "Error" : "Warning",
            "declaring-scope-unavailable",
            $"Instruction {instruction} requires a proven declaring scope.",
            block,
            instruction);
        return false;
    }

    static string ReadGlobalScope(
        AnalysisContext context,
        FlowState state,
        int block,
        int instruction,
        bool reachable)
    {
        if (context.DeclaringScope is null)
        {
            RequireDeclaringScope(
                context,
                block,
                instruction,
                reachable);
            return MissingValue(context, block, instruction, "global-scope");
        }
        if (context.DeclaringScope.Count > 0)
            return context.DeclaringScope[0];
        return ReadScope(
            context,
            state,
            0,
            block,
            instruction,
            reachable);
    }

    static List<string> EffectiveScope(
        AnalysisContext context,
        FlowState state) =>
        EffectiveScope(context, state.Scope);

    static List<string> EffectiveScope(
        AnalysisContext context,
        IReadOnlyList<string> local_scope)
    {
        int declaring_count = context.DeclaringScope?.Count ?? 0;
        int unknown_count = context.DeclaringScope is null ? 1 : 0;
        var scope = new List<string>(
            declaring_count + unknown_count + local_scope.Count);
        if (context.DeclaringScope is not null)
            scope.AddRange(context.DeclaringScope);
        else
            scope.Add(UnknownDeclaringScope);
        scope.AddRange(local_scope);
        return scope;
    }

    static List<bool?> EffectiveScopeWith(
        AnalysisContext context,
        IReadOnlyList<bool?> local_scope)
    {
        int declaring_count = context.DeclaringScopeWith?.Count ?? 0;
        int unknown_count = context.DeclaringScopeWith is null ? 1 : 0;
        var scope = new List<bool?>(
            declaring_count + unknown_count + local_scope.Count);
        if (context.DeclaringScopeWith is not null)
            scope.AddRange(context.DeclaringScopeWith.Select(value => (bool?)value));
        else
            scope.Add(null);
        scope.AddRange(local_scope);
        return scope;
    }

    static int LocalScopeDepth(ASMethodBody body, int encoded_depth) =>
        Math.Max(0, encoded_depth - body.InitialScopeDepth);

    static string MissingValue(
        AnalysisContext context,
        int block,
        int instruction,
        string role)
    {
        string id = $"v_i{instruction}_missing_{NormalizeId(role)}";
        RegisterValue(
            context.Values,
            id,
            "Unknown",
            $"instruction:{instruction}:missing:{role}",
            "*",
            null,
            block,
            instruction);
        return id;
    }

    static string NormalizeId(string value)
    {
        var result = new char[value.Length];
        for (int index = 0; index < value.Length; index++)
        {
            char character = char.ToLowerInvariant(value[index]);
            result[index] = char.IsLetterOrDigit(character) ? character : '_';
        }
        return new string(result);
    }

    static FlowState CreateUnreachableState(
        AnalysisContext context,
        Avm2BasicBlockInventory block)
    {
        int stack_count = Math.Max(block.EntryStackDepth ?? 0, 0);
        int scope_count = block.EntryScopeDepth.HasValue
            ? LocalScopeDepth(context.Body, block.EntryScopeDepth.Value)
            : 0;
        var stack = new List<string>(stack_count);
        var scope = new List<string>(scope_count);
        var locals = new List<string>(Math.Max(context.Body.LocalCount, 0));
        for (int index = 0; index < stack_count; index++)
        {
            string id = $"v_unreachable_b{block.Id}_stack_{index}";
            RegisterValue(
                context.Values,
                id,
                "Unreachable",
                $"block:{block.Id}:unreachable:stack:{index}",
                "*",
                null,
                block.Id);
            stack.Add(id);
        }
        for (int index = 0; index < scope_count; index++)
        {
            string id = $"v_unreachable_b{block.Id}_scope_{index}";
            RegisterValue(
                context.Values,
                id,
                "Unreachable",
                $"block:{block.Id}:unreachable:scope:{index}",
                "Object",
                null,
                block.Id);
            scope.Add(id);
        }
        for (int index = 0; index < context.Body.LocalCount; index++)
        {
            string id = $"v_unreachable_b{block.Id}_local_{index}";
            RegisterValue(
                context.Values,
                id,
                "Unreachable",
                $"block:{block.Id}:unreachable:local:{index}",
                "*",
                null,
                block.Id);
            locals.Add(id);
        }
        return new FlowState
        {
            Stack = stack,
            Scope = scope,
            ScopeWith = Enumerable
                .Repeat<bool?>(null, scope_count)
                .ToList(),
            Locals = locals
        };
    }

    static List<Avm2DataFlowPhi> BuildPhis(
        AnalysisContext context,
        Dictionary<int, FlowState> entries,
        Dictionary<int, BlockTransfer> transfers)
    {
        var phis = new List<Avm2DataFlowPhi>();
        foreach (Avm2BasicBlockInventory block in context.Method.ControlFlow.Blocks
            .Where(block => block.Reachable)
            .OrderBy(block => block.Id))
        {
            if (!entries.TryGetValue(block.Id, out FlowState? entry))
                continue;
            List<IncomingState> incoming = IncomingStates(context, block, transfers);
            AddPhisForVector(context, phis, block.Id, "Stack", entry.Stack, incoming, 0);
            AddPhisForVector(
                context,
                phis,
                block.Id,
                "Scope",
                entry.Scope,
                incoming,
                context.DeclaringScope?.Count ??
                    (context.DeclaringScope is null ? 1 : 0));
            AddPhisForVector(context, phis, block.Id, "Local", entry.Locals, incoming, 0);
        }
        return phis
            .OrderBy(phi => phi.Block)
            .ThenBy(phi => StateOrder(phi.State))
            .ThenBy(phi => phi.Index)
            .ToList();
    }

    static void AddPhisForVector(
        AnalysisContext context,
        List<Avm2DataFlowPhi> phis,
        int block,
        string state,
        List<string> entry,
        List<IncomingState> incoming,
        int index_offset)
    {
        for (int index = 0; index < entry.Count; index++)
        {
            string value = entry[index];
            if (!value.StartsWith($"v_phi_b{block}_", StringComparison.Ordinal))
                continue;
            List<Avm2DataFlowPhiInput> inputs = incoming
                .Select(source => new
                {
                    Source = source,
                    Value = ReadStateValue(source.State, state, index)
                })
                .Where(item => item.Value is not null)
                .OrderBy(item => item.Source.FromBlock.HasValue ? 1 : 0)
                .ThenBy(item => item.Source.FromBlock)
                .ThenBy(item => item.Source.SourceInstruction)
                .ThenBy(item => item.Source.EdgeKind, StringComparer.Ordinal)
                .Select(item => new Avm2DataFlowPhiInput
                {
                    FromBlock = item.Source.FromBlock,
                    EdgeKind = item.Source.EdgeKind,
                    SourceInstruction = item.Source.SourceInstruction,
                    Value = item.Value!
                })
                .ToList();
            MutableValue phi = context.Values[value];
            foreach (string source in inputs.Select(input => input.Value))
                phi.Sources.Add(source);
            phis.Add(new Avm2DataFlowPhi
            {
                Value = value,
                Block = block,
                State = state,
                Index = index + index_offset,
                Inputs = inputs
            });
        }
    }

    static int StateOrder(string state) => state switch
    {
        "Stack" => 0,
        "Scope" => 1,
        _ => 2
    };

    static void InferPhiTypes(
        Dictionary<string, MutableValue> values,
        List<Avm2DataFlowPhi> phis)
    {
        int limit = Math.Max(4, phis.Count + 1);
        for (int iteration = 0; iteration < limit; iteration++)
        {
            bool changed = false;
            foreach (Avm2DataFlowPhi phi in phis)
            {
                MutableValue target = values[phi.Value];
                List<string> types = phi.Inputs
                    .Select(input => values.GetValueOrDefault(input.Value)?.TypeHint)
                    .Where(type => !string.IsNullOrWhiteSpace(type))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                string? inferred = types.Count switch
                {
                    0 => null,
                    1 => types[0],
                    _ => "*"
                };
                Avm2VerifierType[] verifier_types = phi.Inputs
                    .Select(input => values
                        .GetValueOrDefault(input.Value)?
                        .VerifierType ??
                        Avm2VerifierType.Unknown)
                    .Distinct()
                    .ToArray();
                Avm2VerifierType verifier_type =
                    verifier_types.Length == 1
                        ? verifier_types[0]
                        : Avm2VerifierType.Unknown;
                string?[] exact_types = phi.Inputs
                    .Select(input => values
                        .GetValueOrDefault(input.Value)?
                        .ExactRuntimeTypeIdentity)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                string? exact_type =
                    exact_types.Length == 1
                        ? exact_types[0]
                        : null;
                if (target.TypeHint != inferred)
                {
                    target.TypeHint = inferred;
                    changed = true;
                }
                if (target.VerifierType != verifier_type)
                {
                    target.VerifierType = verifier_type;
                    changed = true;
                }
                if (target.ExactRuntimeTypeIdentity != exact_type)
                {
                    target.ExactRuntimeTypeIdentity = exact_type;
                    changed = true;
                }
            }
            if (!changed)
                break;
        }
    }

    static Avm2DataFlowBlock BuildBlock(
        AnalysisContext context,
        Avm2BasicBlockInventory block,
        Dictionary<int, FlowState> entries,
        Dictionary<int, BlockTransfer> transfers)
    {
        FlowState entry = entries.GetValueOrDefault(block.Id) ?? new FlowState
        {
            Stack = [],
            Scope = [],
            ScopeWith = [],
            Locals = []
        };
        FlowState exit = transfers.GetValueOrDefault(block.Id)?.Exit ?? entry;
        var instructions = new List<int>();
        for (int index = block.FirstInstruction; index <= block.LastInstruction; index++)
        {
            if (index >= 0 && index < context.Method.Instructions.Count)
                instructions.Add(index);
        }
        return new Avm2DataFlowBlock
        {
            Id = block.Id,
            Unreachable = !block.Reachable,
            Instructions = instructions,
            Entry = StateModel(context, entry),
            Exit = StateModel(context, exit)
        };
    }

    static Avm2DataFlowState StateModel(
        AnalysisContext context,
        FlowState state) => new()
    {
        Stack = [.. state.Stack],
        Scope = EffectiveScope(context, state),
        Locals = [.. state.Locals]
    };

    static string? ResultType(ASInstruction? instruction)
    {
        if (instruction is null)
            return "*";
        return instruction.OP switch
        {
            OPCode.PushTrue or OPCode.PushFalse or OPCode.Convert_b or OPCode.Coerce_b or
            OPCode.Not or OPCode.Equals or OPCode.StrictEquals or OPCode.LessThan or
            OPCode.LessEquals or OPCode.GreaterThan or OPCode.GreaterEquals or
            OPCode.InstanceOf or OPCode.In or OPCode.IsType or OPCode.IsTypeLate or
            OPCode.DeleteProperty or OPCode.HasNext or OPCode.HasNext2 => "Boolean",
            OPCode.PushByte or OPCode.PushShort or OPCode.PushInt or OPCode.Convert_i or
            OPCode.Coerce_i or OPCode.Increment_i or OPCode.Decrement_i or OPCode.Negate_i or
            OPCode.Add_i or OPCode.Subtract_i or OPCode.Multiply_i or OPCode.BitAnd or
            OPCode.BitOr or OPCode.BitXor or OPCode.LShift or OPCode.RShift or
            OPCode.Sxi1 or OPCode.Sxi8 or OPCode.Sxi16 or OPCode.Li8 or OPCode.Li16 or
            OPCode.Li32 => "int",
            OPCode.PushUInt or OPCode.Convert_u or OPCode.Coerce_u or OPCode.URShift => "uint",
            OPCode.PushDouble or OPCode.PushNan or OPCode.Convert_d or OPCode.Coerce_d or
            OPCode.Increment or OPCode.Decrement or OPCode.Negate or OPCode.Divide or
            OPCode.Modulo or OPCode.Multiply or OPCode.Subtract or OPCode.Lf32 or
            OPCode.Lf64 => "Number",
            OPCode.PushFloat or OPCode.Convert_f => "float",
            OPCode.PushFloat4 or OPCode.Convert_f4 or OPCode.Lf32x4 => "float4",
            OPCode.UnPlus => "numeric",
            OPCode.PushString or OPCode.Convert_s or OPCode.Coerce_s or OPCode.TypeOf or
            OPCode.Esc_XAttr or OPCode.Esc_XElem => "String",
            OPCode.PushNull => "null",
            OPCode.PushUndefined => "undefined",
            OPCode.PushNamespace => "Namespace",
            OPCode.NewArray => "Array",
            OPCode.NewObject or OPCode.NewActivation or OPCode.NewCatch or
            OPCode.Convert_o or OPCode.Coerce_o => "Object",
            OPCode.NewFunction => "Function",
            OPCode.NewClass => "Class",
            OPCode.AsType or OPCode.Coerce => SafeType(
                () => Avm2MethodAnalyzer.Qualified(Avm2MethodAnalyzer.ReadSymbol(instruction)),
                "*"),
            _ => "*"
        };
    }

    static TypeProof ResultTypeProof(
        AnalysisContext context,
        ASInstruction? instruction,
        IReadOnlyList<string> inputs)
    {
        if (instruction is null)
            return default;
        MutableValue? input = inputs.Count == 0
            ? null
            : context.Values.GetValueOrDefault(inputs[^1]);
        if (instruction is NewClassIns new_class &&
            context.Binding is not null &&
            new_class.ClassIndex >= 0 &&
            new_class.ClassIndex < context.Binding.Abc.Classes.Count)
        {
            string identity = ClassTypeIdentity(
                context.Binding.AbcIndex,
                new_class.ClassIndex,
                true);
            return new TypeProof(
                Avm2VerifierType.Known(identity),
                identity);
        }
        if (instruction.OP == OPCode.NewFunction)
        {
            return new TypeProof(
                Avm2VerifierType.Known("builtin:function"),
                "builtin:function");
        }
        if (instruction is CoerceIns)
        {
            Avm2VerifierType target =
                SymbolVerifierType(context, instruction);
            string? exact = input?.ExactRuntimeTypeIdentity;
            if (exact is not null &&
                !CanAssign(
                    context,
                    target,
                    Avm2VerifierType.Known(exact)))
            {
                exact = null;
            }
            return new TypeProof(target, exact);
        }
        if (instruction is AsTypeIns)
        {
            Avm2VerifierType target =
                AsTypeTarget(SymbolVerifierType(context, instruction));
            Avm2VerifierType source =
                input?.VerifierType ?? Avm2VerifierType.Unknown;
            Avm2VerifierType verifier =
                CanAssign(context, target, source)
                    ? source
                    : target;
            string? exact = input?.ExactRuntimeTypeIdentity;
            if (exact is not null &&
                !CanAssign(
                    context,
                    target,
                    Avm2VerifierType.Known(exact)))
            {
                exact = null;
            }
            return new TypeProof(verifier, exact);
        }
        return instruction.OP switch
        {
            OPCode.PushString or OPCode.Convert_s or OPCode.Coerce_s =>
                KnownType("builtin:string", true),
            OPCode.PushTrue or OPCode.PushFalse or OPCode.Convert_b or
                OPCode.Coerce_b => KnownType("builtin:boolean", true),
            OPCode.PushInt or OPCode.PushByte or OPCode.PushShort or
                OPCode.Convert_i or OPCode.Coerce_i =>
                KnownType("builtin:int", true),
            OPCode.PushUInt or OPCode.Convert_u or OPCode.Coerce_u =>
                KnownType("builtin:uint", true),
            OPCode.PushDouble or OPCode.PushNan or OPCode.Convert_d or
                OPCode.Coerce_d => KnownType("builtin:number", true),
            OPCode.PushNamespace =>
                KnownType("builtin:namespace", true),
            OPCode.NewArray => KnownType("builtin:array", true),
            OPCode.NewObject => KnownType("builtin:object", true),
            OPCode.PushNull => new TypeProof(
                Avm2VerifierType.Null,
                "builtin:null"),
            OPCode.PushUndefined => new TypeProof(
                Avm2VerifierType.Void,
                "builtin:void"),
            OPCode.Coerce_a => new TypeProof(
                Avm2VerifierType.Any,
                input?.ExactRuntimeTypeIdentity),
            OPCode.Convert_o or OPCode.Coerce_o =>
                new TypeProof(
                    Avm2VerifierType.Known("builtin:object"),
                    input?.ExactRuntimeTypeIdentity),
            _ => default
        };
    }

    static TypeProof KnownType(
        string identity,
        bool exact) =>
        new(
            Avm2VerifierType.Known(identity),
            exact ? identity : null);

    static Avm2VerifierType SymbolVerifierType(
        AnalysisContext context,
        ASInstruction instruction)
    {
        ASMultiname? symbol;
        try
        {
            symbol = Avm2MethodAnalyzer.ReadSymbol(instruction);
        }
        catch
        {
            return Avm2VerifierType.Unknown;
        }
        return SymbolVerifierType(
            context.Body.ABC,
            context.VerifierTypes,
            symbol);
    }

    static Avm2VerifierType SymbolVerifierType(
        ABCFile abc,
        Avm2VerifierTypeRegistry verifier_types,
        ASMultiname? symbol)
    {
        if (symbol is null || ReferenceEquals(
                symbol,
                abc.Pool.Multinames
                    .FirstOrDefault()))
        {
            return Avm2VerifierType.Any;
        }
        string? identity =
            verifier_types.ResolveVerifierReferenceIdentity(
                symbol,
                abc);
        return identity is null
            ? Avm2VerifierType.Unknown
            : Avm2VerifierType.Known(identity);
    }

    static Avm2VerifierType AsTypeTarget(
        Avm2VerifierType target)
    {
        if (target.Kind != Avm2VerifierTypeKind.Known)
            return target;
        return target.Identity is
            "builtin:boolean" or
            "builtin:int" or
            "builtin:uint" or
            "builtin:number"
                ? Avm2VerifierType.Known("builtin:object")
                : target;
    }

    static bool CanAssign(
        AnalysisContext context,
        Avm2VerifierType target,
        Avm2VerifierType source)
    {
        if (target.Kind == Avm2VerifierTypeKind.Any)
            return source.Kind != Avm2VerifierTypeKind.Unknown;
        if (target.Kind != Avm2VerifierTypeKind.Known)
            return target == source;
        if (source.Kind == Avm2VerifierTypeKind.Null)
        {
            return target.Identity is not (
                "builtin:boolean" or
                "builtin:int" or
                "builtin:uint" or
                "builtin:number");
        }
        if (source.Kind != Avm2VerifierTypeKind.Known)
            return false;
        if (target.Identity == source.Identity)
            return true;
        return context.VerifierTypes.IsAssignable(
            target.Identity,
            source.Identity);
    }

    static string? Literal(ASInstruction? instruction)
    {
        if (instruction is Primitive primitive)
        {
            try
            {
                return Avm2MethodAnalyzer.LiteralText(primitive.Value);
            }
            catch
            {
                return null;
            }
        }
        if (instruction?.OP == OPCode.PushUndefined)
            return "undefined";
        if (instruction is PushNamespaceIns pushed_namespace)
        {
            try
            {
                return Avm2MethodAnalyzer.LiteralText(
                    $"{pushed_namespace.Namespace.Kind}:{pushed_namespace.Namespace.RuntimeName}");
            }
            catch
            {
                return $"pool:{pushed_namespace.NamespaceIndex}";
            }
        }
        return null;
    }

    static TypeProof ReceiverTypeProof(
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver)
    {
        Avm2VerifierType verifier;
        if (binding is null)
        {
            verifier =
                Avm2VerifierType.Known("builtin:object");
        }
        else
        {
            verifier = binding.Scope switch
            {
                Avm2MethodBindingScope.Script =>
                    Avm2VerifierType.Known(string.Create(
                        CultureInfo.InvariantCulture,
                        $"abc:{binding.AbcIndex}:script:{binding.ContainerIndex}")),
                Avm2MethodBindingScope.ClassStatic =>
                    Avm2VerifierType.Known(ClassTypeIdentity(
                        binding.AbcIndex,
                        binding.ContainerIndex,
                        true)),
                Avm2MethodBindingScope.ClassInstance =>
                    Avm2VerifierType.Known(ClassTypeIdentity(
                        binding.AbcIndex,
                        binding.ContainerIndex,
                        false)),
                _ => Avm2VerifierType.Unknown
            };
        }
        if (exact_receiver is null)
        {
            string? exact_script =
                binding?.Scope == Avm2MethodBindingScope.Script
                    ? verifier.Identity
                    : null;
            return new TypeProof(verifier, exact_script);
        }
        if (binding is not null)
        {
            int class_index = binding.Abc.Instances.IndexOf(
                exact_receiver.RuntimeType);
            if (class_index >= 0)
            {
                return new TypeProof(
                    verifier,
                    ClassTypeIdentity(
                        binding.AbcIndex,
                        class_index,
                        exact_receiver.Static));
            }
        }
        try
        {
            return new TypeProof(
                verifier,
                $"symbol:{Avm2MethodAnalyzer.RuntimeSymbolIdentity(
                    exact_receiver.RuntimeType.QName)}:{(exact_receiver.Static ? "static" : "instance")}");
        }
        catch
        {
            return new TypeProof(verifier, null);
        }
    }

    static string ClassTypeIdentity(
        int abc_index,
        int class_index,
        bool @static) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"abc:{abc_index}:class:{class_index}:{(@static ? "static" : "instance")}");

    static string SafeType(Func<string> read, string fallback)
    {
        try
        {
            string value = read();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        catch
        {
            return fallback;
        }
    }

    static MutableValue RegisterValue(
        Dictionary<string, MutableValue> values,
        string id,
        string kind,
        string definition,
        string? type,
        string? literal,
        int? block = null,
        int? instruction = null,
        IEnumerable<string>? sources = null,
        Avm2VerifierType? verifier_type = null,
        string? exact_runtime_type_identity = null)
    {
        if (!values.TryGetValue(id, out MutableValue? value))
        {
            value = new MutableValue
            {
                Id = id,
                Kind = kind,
                Definition = definition,
                TypeHint = type,
                VerifierType =
                    verifier_type ??
                    Avm2VerifierType.Unknown,
                ExactRuntimeTypeIdentity =
                    exact_runtime_type_identity,
                Literal = literal,
                Block = block,
                Instruction = instruction
            };
            values.Add(id, value);
        }
        else
        {
            if (value.VerifierType.Kind ==
                    Avm2VerifierTypeKind.Unknown &&
                verifier_type is not null)
            {
                value.VerifierType = verifier_type;
            }
            if (value.ExactRuntimeTypeIdentity is null &&
                exact_runtime_type_identity is not null)
            {
                value.ExactRuntimeTypeIdentity =
                    exact_runtime_type_identity;
            }
        }
        if (kind == "Instruction")
            value.Sources.Clear();
        if (sources is not null)
        {
            foreach (string source in sources)
                value.Sources.Add(source);
        }
        return value;
    }

    static void AddDiagnostic(
        AnalysisContext context,
        string severity,
        string code,
        string message,
        int? block = null,
        int? instruction = null)
    {
        string key = string.Join(
            "|",
            severity,
            code,
            block?.ToString(CultureInfo.InvariantCulture) ?? "",
            instruction?.ToString(CultureInfo.InvariantCulture) ?? "",
            message);
        context.Diagnostics.TryAdd(key, new Avm2DataFlowDiagnostic
        {
            Severity = severity,
            Code = code,
            Message = message,
            Block = block,
            Instruction = instruction
        });
    }

    static bool SameState(FlowState left, FlowState right) =>
        left.Stack.SequenceEqual(right.Stack, StringComparer.Ordinal) &&
        left.Scope.SequenceEqual(right.Scope, StringComparer.Ordinal) &&
        left.ScopeWith.SequenceEqual(right.ScopeWith) &&
        left.Locals.SequenceEqual(right.Locals, StringComparer.Ordinal);
}
