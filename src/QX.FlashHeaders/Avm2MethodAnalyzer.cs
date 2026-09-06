using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

public sealed class Avm2MethodAnalysis
{
    public required List<Avm2InstructionInventory> Instructions { get; init; }
    public required Avm2ControlFlowInventory ControlFlow { get; init; }
    public required IReadOnlyList<Avm2ExceptionNormalization> Exceptions { get; init; }
    public required List<Avm2ReferenceInventory> References { get; init; }
    public required List<string> Diagnostics { get; init; }
    public required string StructuralSha256 { get; init; }
    public required string SemanticSha256 { get; init; }
    public required string ReadableCode { get; init; }
    internal ASMethodBody? SourceBody { get; init; }
    internal IReadOnlyList<ASInstruction> DecodedCode { get; init; } = [];
    internal string IntegrityFingerprint { get; set; } = "";

    internal bool MatchesSource(ASMethodBody body)
    {
        if (!ReferenceEquals(SourceBody, body))
            return false;
        try
        {
            return string.Equals(
                Avm2MethodAnalyzer.IntegrityFingerprint(this),
                IntegrityFingerprint,
                StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}

public static class Avm2MethodAnalyzer
{
    sealed class InstructionNode
    {
        public int Index { get; set; }
        public required int Offset { get; init; }
        public required int Size { get; init; }
        public required ASInstruction Value { get; init; }
        public required bool Storage { get; init; }
    }

    sealed class BlockNode
    {
        public required int Id { get; init; }
        public required int First { get; init; }
        public required int Last { get; init; }
        public required int StartOffset { get; init; }
        public required int EndOffset { get; init; }
        public int? EntryStack { get; set; }
        public int? ExitStack { get; set; }
        public int? EntryScope { get; set; }
        public int? ExitScope { get; set; }
        public bool Reachable { get; set; }
    }

    sealed class DecodedEdge
    {
        public required InstructionNode Source { get; init; }
        public required int Target { get; init; }
        public required string Kind { get; init; }
        public int? CaseIndex { get; init; }
        public int? ExceptionIndex { get; init; }
        public string? ExceptionType { get; init; }
        public bool Resolved { get; set; }
    }

    sealed class DecodedGraph
    {
        public required List<InstructionNode> Nodes { get; init; }
        public required List<DecodedEdge> Edges { get; init; }
        public required bool Complete { get; init; }
    }

    sealed class GraphResult
    {
        public required List<BlockNode> Blocks { get; init; }
        public required List<Avm2ControlFlowEdgeInventory> Edges { get; init; }
        public required Dictionary<int, int> BlockByInstruction { get; init; }
        public required Dictionary<int, int> BlockByOffset { get; init; }
        public required bool Complete { get; init; }
    }

    sealed class IdentityContext
    {
        public HashSet<ASClass> Classes { get; } =
            new(ReferenceEqualityComparer.Instance);
        public Dictionary<ASMethod, string> MethodCache { get; } =
            new(ReferenceEqualityComparer.Instance);
        public Dictionary<ASClass, string> ClassCache { get; } =
            new(ReferenceEqualityComparer.Instance);
        public int TargetDepth { get; set; }
    }

    sealed class RuntimeIdentityCache
    {
        sealed class NamespacePool
        {
            public required string[] Runtime { get; init; }
            public required string[] Normalized { get; init; }
        }

        readonly Dictionary<ASMultiname, string> encoding_symbols =
            new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<ASMultiname, string> runtime_symbols =
            new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<ASMultiname, string> normalized_symbols =
            new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<ASNamespace, string> runtime_namespaces =
            new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<ASNamespace, string> normalized_namespaces =
            new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<ASConstantPool, NamespacePool> namespace_pools =
            new(ReferenceEqualityComparer.Instance);

        public bool TryGetSymbol(
            ASMultiname value,
            SymbolIdentityMode mode,
            out string identity) =>
            Symbols(mode).TryGetValue(value, out identity!);

        public void AddSymbol(
            ASMultiname value,
            SymbolIdentityMode mode,
            string identity) =>
            Symbols(mode).TryAdd(value, identity);

        public bool TryGetNamespace(
            ASNamespace value,
            SymbolIdentityMode mode,
            out string identity)
        {
            IndexPool(value.Pool);
            return Namespaces(mode).TryGetValue(value, out identity!);
        }

        public string NamespaceIdentity(
            ASConstantPool pool,
            int index,
            SymbolIdentityMode mode)
        {
            if (index < 0 || index >= pool.Namespaces.Count)
                return "invalid";
            NamespacePool identities = IndexPool(pool);
            return mode == SymbolIdentityMode.Runtime
                ? identities.Runtime[index]
                : identities.Normalized[index];
        }

        Dictionary<ASMultiname, string> Symbols(
            SymbolIdentityMode mode) =>
            mode switch
            {
                SymbolIdentityMode.Encoding => encoding_symbols,
                SymbolIdentityMode.Runtime => runtime_symbols,
                _ => normalized_symbols
            };

        Dictionary<ASNamespace, string> Namespaces(
            SymbolIdentityMode mode) =>
            mode == SymbolIdentityMode.Runtime
                ? runtime_namespaces
                : normalized_namespaces;

        NamespacePool IndexPool(ASConstantPool pool)
        {
            if (namespace_pools.TryGetValue(
                    pool,
                    out NamespacePool? cached))
            {
                return cached;
            }

            var runtime = new string[pool.Namespaces.Count];
            var normalized = new string[pool.Namespaces.Count];
            var private_ordinals = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (int index = 0; index < pool.Namespaces.Count; index++)
            {
                ASNamespace? value = pool.Namespaces[index];
                int private_ordinal = 0;
                if (value is not null && value.Kind == NamespaceKind.Private)
                {
                    string uri = value.NameIndex >= 0 &&
                        value.NameIndex < pool.Strings.Count
                            ? value.RuntimeName
                            : string.Empty;
                    private_ordinal = private_ordinals.GetValueOrDefault(uri);
                    private_ordinals[uri] = private_ordinal + 1;
                }
                runtime[index] = RuntimeNamespaceIdentity(
                    pool,
                    index,
                    SymbolIdentityMode.Runtime,
                    private_ordinal);
                normalized[index] = RuntimeNamespaceIdentity(
                    pool,
                    index,
                    SymbolIdentityMode.Normalized,
                    private_ordinal);
                if (value is null)
                    continue;
                runtime_namespaces.TryAdd(value, runtime[index]);
                normalized_namespaces.TryAdd(value, normalized[index]);
            }
            var created = new NamespacePool
            {
                Runtime = runtime,
                Normalized = normalized
            };
            namespace_pools.Add(pool, created);
            return created;
        }
    }

    sealed class RuntimeIdentityLease : IDisposable
    {
        bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (runtime_identity_depth > 0)
                runtime_identity_depth--;
            if (runtime_identity_depth == 0)
                runtime_identity_cache = null;
        }
    }

    [ThreadStatic]
    static RuntimeIdentityCache? runtime_identity_cache;

    [ThreadStatic]
    static int runtime_identity_depth;

    readonly record struct MethodIdentityStamp(
        string? CodeSha256,
        string Shape,
        int StringCount,
        int NamespaceCount,
        int NamespaceSetCount,
        int MultinameCount,
        int MethodCount,
        int ClassCount);

    enum SymbolIdentityMode
    {
        Encoding,
        Runtime,
        Normalized
    }

    internal static IDisposable CacheRuntimeIdentities()
    {
        runtime_identity_cache ??= new RuntimeIdentityCache();
        runtime_identity_depth++;
        return new RuntimeIdentityLease();
    }

    public static Avm2MethodAnalysis Analyze(ASMethodBody body)
    {
        using IDisposable identities = CacheRuntimeIdentities();
        ASCode code = body.ParseCode();
        List<InstructionNode> nodes = ReadInstructions(code, body.Code.Length);
        IReadOnlyList<Avm2ExceptionNormalization> exceptions =
            Avm2ExceptionNormalizer.Normalize(body, code);
        var diagnostics = new List<string>();
        GraphResult graph = BuildGraph(
            body,
            nodes,
            exceptions,
            diagnostics);
        AnalyzeDepths(body, nodes, graph, diagnostics);
        List<Avm2ReferenceInventory> references = ReadReferences(nodes);
        List<Avm2InstructionInventory> instructions = nodes.Select(node => new Avm2InstructionInventory
        {
            Index = node.Index,
            Offset = node.Offset,
            Opcode = node.Value.OP.ToString(),
            PopCount = PopCount(node.Value),
            PushCount = PushCount(node.Value),
            CanThrow = Avm2InstructionSemantics.CanThrow(node.Value.OP),
            Block = graph.BlockByInstruction.GetValueOrDefault(node.Index, -1),
            Operands = ReadOperands(node.Value)
        }).ToList();
        int entry_block = graph.Blocks.Count == 0 ? -1 : 0;
        List<Avm2BasicBlockInventory> blocks = graph.Blocks.Select(block => new Avm2BasicBlockInventory
        {
            Id = block.Id,
            FirstInstruction = block.First,
            LastInstruction = block.Last,
            StartOffset = block.StartOffset,
            EndOffset = block.EndOffset,
            Reachable = block.Reachable,
            EntryStackDepth = block.EntryStack,
            ExitStackDepth = block.ExitStack,
            EntryScopeDepth = block.EntryScope,
            ExitScopeDepth = block.ExitScope
        }).ToList();
        Avm2LoopAnalysis loop_analysis = Avm2LoopAnalyzer.Analyze(
            entry_block,
            blocks,
            graph.Edges);
        var control_flow = new Avm2ControlFlowInventory
        {
            EntryBlock = entry_block,
            Blocks = blocks,
            Edges = graph.Edges,
            HasLoop = loop_analysis.HasLoop,
            Complete = graph.Complete,
            Dominators = loop_analysis.Dominators,
            NaturalLoops = loop_analysis.NaturalLoops,
            IrreducibleCycles = loop_analysis.IrreducibleCycles
        };
        string structural = Fingerprint(body.Method, nodes, graph, false);
        string semantic = Fingerprint(body.Method, nodes, graph, true);
        string readable = Avm2ReadableCode.Render(body.Method, nodes.Select(node => node.Value).ToList(), instructions, control_flow);
        var result = new Avm2MethodAnalysis
        {
            Instructions = instructions,
            ControlFlow = control_flow,
            Exceptions = exceptions,
            References = references,
            Diagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToList(),
            StructuralSha256 = structural,
            SemanticSha256 = semantic,
            ReadableCode = readable,
            SourceBody = body,
            DecodedCode = nodes.Select(node => node.Value).ToArray()
        };
        result.IntegrityFingerprint =
            IntegrityFingerprint(result);
        return result;
    }

    internal static string SignatureFingerprint(ASMethod method, bool semantic)
    {
        using IDisposable identities = CacheRuntimeIdentities();
        var canonical = new StringBuilder();
        AppendSignature(canonical, method, semantic);
        return Hash(canonical);
    }

    internal static string IntegrityFingerprint(
        Avm2MethodAnalysis analysis)
    {
        using IDisposable identities = CacheRuntimeIdentities();
        ArgumentNullException.ThrowIfNull(analysis);
        var canonical = new StringBuilder();
        void Add(object? value)
        {
            string item = Convert.ToString(
                value,
                CultureInfo.InvariantCulture) ?? "";
            canonical.Append(item.Length)
                .Append(':')
                .Append(item)
                .Append(';');
        }
        void AddIntegers(IEnumerable<int> values)
        {
            foreach (int value in values)
                Add(value);
            Add("\u001f");
        }
        void AddOperands(
            IEnumerable<KeyValuePair<string, string?>>
                operands)
        {
            foreach ((string name, string? value) in
                operands.OrderBy(
                    value => value.Key,
                    StringComparer.Ordinal))
            {
                Add(name);
                Add(value);
            }
            Add("\u001e");
        }

        ASMethodBody? body = analysis.SourceBody;
        Add(body is not null);
        if (body is not null)
        {
            MethodIdentityStamp stamp =
                MethodStamp(body.Method);
            Add(stamp.CodeSha256);
            Add(stamp.Shape);
            Add(stamp.StringCount);
            Add(stamp.NamespaceCount);
            Add(stamp.NamespaceSetCount);
            Add(stamp.MultinameCount);
            Add(stamp.MethodCount);
            Add(stamp.ClassCount);
            Add(SignatureFingerprint(
                body.Method,
                false));
            Add(SignatureFingerprint(
                body.Method,
                true));
            Add(body.MethodIndex);
            Add(body.MaxStack);
            Add(body.LocalCount);
            Add(body.InitialScopeDepth);
            Add(body.MaxScopeDepth);
            foreach (ASException exception in
                body.Exceptions)
            {
                Add(exception.From);
                Add(exception.To);
                Add(exception.Target);
                Add(exception.ExceptionTypeIndex);
                Add(exception.VariableNameIndex);
            }
            Add("\u001d");
            foreach (ASTrait trait in body.Traits)
            {
                Add(trait.Kind);
                Add(trait.Attributes);
                Add(trait.Id);
                Add(trait.QNameIndex);
                Add(SafeValue(
                    () => RuntimeSymbolIdentity(
                        trait.QName),
                    ""));
                switch (trait.Kind)
                {
                    case TraitKind.Slot:
                    case TraitKind.Constant:
                        Add(trait.TypeIndex);
                        Add(SafeValue(
                            () => RuntimeSymbolIdentity(
                                trait.Type),
                            ""));
                        Add(trait.ValueIndex);
                        if (trait.ValueIndex != 0)
                            Add(trait.ValueKind);
                        break;
                    case TraitKind.Method:
                    case TraitKind.Getter:
                    case TraitKind.Setter:
                        Add(trait.MethodIndex);
                        break;
                    case TraitKind.Function:
                        Add(trait.FunctionIndex);
                        break;
                    case TraitKind.Class:
                        Add(trait.ClassIndex);
                        break;
                }
                if (trait.Attributes.HasFlag(
                    TraitAttributes.Metadata))
                {
                    AddIntegers(trait.MetadataIndices);
                }
            }
            Add("\u001c");
        }
        foreach (ASInstruction instruction in
            analysis.DecodedCode)
        {
            Add(instruction.OP);
            Add(instruction.DecodedOffset);
            Add(instruction.DecodedSize);
            AddOperands(ReadOperands(instruction));
        }
        Add("\u001b");
        foreach (Avm2InstructionInventory instruction in
            analysis.Instructions)
        {
            Add(instruction.Index);
            Add(instruction.Offset);
            Add(instruction.Opcode);
            Add(instruction.PopCount);
            Add(instruction.PushCount);
            Add(instruction.CanThrow);
            Add(instruction.Block);
            AddOperands(instruction.Operands);
        }
        Add("\u001a");
        Avm2ControlFlowInventory flow =
            analysis.ControlFlow;
        Add(flow.EntryBlock);
        Add(flow.HasLoop);
        Add(flow.Complete);
        foreach (Avm2BasicBlockInventory block in
            flow.Blocks)
        {
            Add(block.Id);
            Add(block.FirstInstruction);
            Add(block.LastInstruction);
            Add(block.StartOffset);
            Add(block.EndOffset);
            Add(block.Reachable);
            Add(block.EntryStackDepth);
            Add(block.ExitStackDepth);
            Add(block.EntryScopeDepth);
            Add(block.ExitScopeDepth);
        }
        Add("\u0019");
        foreach (Avm2ControlFlowEdgeInventory edge in
            flow.Edges)
        {
            Add(edge.FromBlock);
            Add(edge.ToBlock);
            Add(edge.SourceInstruction);
            Add(edge.SourceOffset);
            Add(edge.TargetOffset);
            Add(edge.Kind);
            Add(edge.CaseIndex);
            Add(edge.ExceptionIndex);
            Add(edge.ExceptionType);
        }
        Add("\u0018");
        foreach (Avm2DominatorInventory dominator in
            flow.Dominators)
        {
            Add(dominator.Block);
            Add(dominator.ImmediateDominator);
            AddIntegers(dominator.Dominators);
        }
        Add("\u0017");
        foreach (Avm2NaturalLoopInventory loop in
            flow.NaturalLoops)
        {
            Add(loop.Id);
            Add(loop.HeaderBlock);
            AddIntegers(loop.LatchBlocks);
            AddIntegers(loop.Blocks);
            AddIntegers(loop.ExitingBlocks);
            AddIntegers(loop.ExitBlocks);
            Add(loop.ParentLoop);
            Add(loop.Depth);
            AddIntegers(loop.Ancestors);
        }
        Add("\u0016");
        foreach (Avm2IrreducibleCycleInventory cycle in
            flow.IrreducibleCycles)
        {
            Add(cycle.Id);
            AddIntegers(cycle.Blocks);
            AddIntegers(cycle.EntryBlocks);
        }
        Add("\u0015");
        foreach (Avm2ExceptionNormalization exception in
            analysis.Exceptions)
        {
            Add(exception.ExceptionIndex);
            Add(exception.RawFrom);
            Add(exception.RawTo);
            Add(exception.RawTarget);
            Add(exception.From);
            Add(exception.To);
            Add(exception.Target);
            Add(exception.Status);
            Add(exception.Shift);
            Add(exception.JumpOffset);
            Add(exception.JumpTarget);
            Add(exception.NewCatchOffset);
            Add(exception.NewCatchExceptionIndex);
            AddIntegers(exception.FromCandidates);
            Add(exception.FromResolution);
            foreach (Avm2ExceptionNormalizationCandidate
                candidate in exception.Candidates)
            {
                Add(candidate.Shift);
                Add(candidate.From);
                Add(candidate.To);
                Add(candidate.Target);
                Add(candidate.JumpOffset);
                Add(candidate.JumpTarget);
                Add(candidate.NewCatchOffset);
                Add(candidate.NewCatchExceptionIndex);
                AddIntegers(candidate.FromCandidates);
                Add(candidate.FromResolution);
            }
            Add("\u0014");
        }
        Add("\u0013");
        foreach (Avm2ReferenceInventory reference in
            analysis.References)
        {
            Add(reference.Instruction);
            Add(reference.Offset);
            Add(reference.Kind);
            Add(reference.Target);
            Add(reference.SymbolIdentity);
            Add(reference.EncodingSymbolIdentity);
            Add(reference.RuntimeSymbolIdentity);
            Add(reference.NormalizedSymbolIdentity);
            Add(reference.ArgumentCount);
            Add(reference.MethodIndex);
            Add(reference.ClassIndex);
        }
        Add("\u0012");
        foreach (string diagnostic in
            analysis.Diagnostics)
        {
            Add(diagnostic);
        }
        Add(analysis.StructuralSha256);
        Add(analysis.SemanticSha256);
        Add(analysis.ReadableCode);
        return Hash(canonical);
    }

    static List<InstructionNode> ReadInstructions(ASCode code, int code_length)
    {
        var nodes = new List<InstructionNode>(code.Count);
        int next_offset = 0;
        for (int index = 0; index < code.Count; index++)
        {
            ASInstruction instruction = code[index];
            int offset = instruction.DecodedOffset >= 0
                ? instruction.DecodedOffset
                : next_offset;
            int size = instruction.DecodedSize > 0
                ? instruction.DecodedSize
                : instruction.GetSize();
            if (offset != next_offset)
                throw new InvalidDataException(
                    $"Non-contiguous AVM2 instruction at {offset}; expected {next_offset}.");
            if (size <= 0 || offset > code_length - size)
                throw new InvalidDataException(
                    $"Invalid AVM2 instruction span {offset}+{size}/{code_length}.");
            nodes.Add(new InstructionNode
            {
                Index = index,
                Offset = offset,
                Size = size,
                Value = instruction,
                Storage = true
            });
            next_offset = offset + size;
        }
        if (next_offset != code_length)
            throw new InvalidDataException(
                $"Decoded AVM2 length {next_offset} does not match code length {code_length}.");
        return nodes;
    }

    static GraphResult BuildGraph(
        ASMethodBody body,
        List<InstructionNode> nodes,
        IReadOnlyList<Avm2ExceptionNormalization> exceptions,
        List<string> diagnostics)
    {
        if (nodes.Count == 0)
        {
            return new GraphResult
            {
                Blocks = [],
                Edges = [],
                BlockByInstruction = [],
                BlockByOffset = [],
                Complete = true
            };
        }

        int code_length = body.Code.Length;
        DecodedGraph decoded = DecodeGraph(
            body,
            nodes,
            exceptions,
            diagnostics);
        Dictionary<int, InstructionNode> node_by_offset =
            decoded.Nodes.ToDictionary(node => node.Offset);
        HashSet<int> leaders = FindLeaders(decoded.Nodes, decoded.Edges);
        List<List<InstructionNode>> block_nodes =
            PartitionBlocks(decoded.Nodes, node_by_offset, leaders);
        nodes.Clear();
        var blocks = new List<BlockNode>(block_nodes.Count);
        var block_by_instruction = new Dictionary<int, int>(
            decoded.Nodes.Count);
        var block_by_offset = new Dictionary<int, int>(block_nodes.Count);
        for (int block_index = 0; block_index < block_nodes.Count; block_index++)
        {
            List<InstructionNode> values = block_nodes[block_index];
            int first = nodes.Count;
            foreach (InstructionNode node in values)
            {
                node.Index = nodes.Count;
                nodes.Add(node);
            }
            int last = nodes.Count - 1;
            var block = new BlockNode
            {
                Id = block_index,
                First = first,
                Last = last,
                StartOffset = values[0].Offset,
                EndOffset = values[^1].Offset + values[^1].Size
            };
            blocks.Add(block);
            block_by_offset.Add(block.StartOffset, block.Id);
            for (int instruction_index = first;
                instruction_index <= last;
                instruction_index++)
            {
                block_by_instruction.Add(instruction_index, block.Id);
            }
        }

        var edges = new List<Avm2ControlFlowEdgeInventory>();
        foreach (DecodedEdge edge in decoded.Edges)
        {
            BlockNode source =
                blocks[block_by_instruction[edge.Source.Index]];
            int? target_block = edge.Resolved &&
                node_by_offset.TryGetValue(
                    edge.Target,
                    out InstructionNode? target_node)
                ? block_by_instruction[target_node.Index]
                : null;
            if (edge.Kind == "Next" &&
                target_block == source.Id)
            {
                continue;
            }
            AddEdge(
                edges,
                source,
                edge.Source,
                edge.Target,
                edge.Kind == "Next" ? "Fallthrough" : edge.Kind,
                edge.CaseIndex,
                edge.ExceptionIndex,
                edge.ExceptionType,
                block_by_offset,
                code_length);
        }
        MarkReachable(blocks, edges);
        return new GraphResult
        {
            Blocks = blocks,
            Edges = edges,
            BlockByInstruction = block_by_instruction,
            BlockByOffset = block_by_offset,
            Complete = decoded.Complete
        };
    }

    static DecodedGraph DecodeGraph(
        ASMethodBody body,
        List<InstructionNode> storage,
        IReadOnlyList<Avm2ExceptionNormalization> exceptions,
        List<string> diagnostics)
    {
        var nodes = storage.ToDictionary(node => node.Offset);
        var edges = new List<DecodedEdge>();
        var expanded = new HashSet<int>();
        var queue = new Queue<InstructionNode>(storage);
        bool complete = true;

        while (queue.Count > 0)
        {
            InstructionNode source = queue.Dequeue();
            if (!expanded.Add(source.Offset))
                continue;
            AddNormalEdges(
                body,
                source,
                nodes,
                edges,
                queue,
                diagnostics,
                ref complete);
        }

        HashSet<int> reachable = ReachableOffsets(edges);
        var exception_edges = new HashSet<(int Source, int Exception)>();
        while (true)
        {
            HashSet<int> loop_headers = edges
                .Where(edge =>
                    edge.Resolved &&
                    reachable.Contains(edge.Source.Offset) &&
                    edge.Kind is "Jump" or "Taken" or "Case" or "Default" &&
                    edge.Target <= edge.Source.Offset)
                .Select(edge => edge.Target)
                .ToHashSet();
            bool changed = false;
            for (int exception_index = 0;
                exception_index < exceptions.Count;
                exception_index++)
            {
                ASException exception = body.Exceptions[exception_index];
                Avm2ExceptionNormalization normalized =
                    exceptions[exception_index];
                string type = Qualified(exception.ExceptionType);
                foreach (InstructionNode source in nodes.Values
                    .Where(node =>
                        reachable.Contains(node.Offset) &&
                        node.Offset >= normalized.From &&
                        node.Offset < normalized.To &&
                        (Avm2InstructionSemantics.CanThrow(node.Value.OP) ||
                         loop_headers.Contains(node.Offset)))
                    .OrderBy(node => node.Offset))
                {
                    if (!exception_edges.Add(
                        (source.Offset, exception_index)))
                    {
                        continue;
                    }
                    var edge = new DecodedEdge
                    {
                        Source = source,
                        Target = normalized.Target,
                        Kind = "Exception",
                        ExceptionIndex = exception_index,
                        ExceptionType = type
                    };
                    edge.Resolved = ResolveTarget(
                        body,
                        edge,
                        nodes,
                        queue,
                        diagnostics,
                        ref complete);
                    edges.Add(edge);
                    changed = true;
                }
            }
            while (queue.Count > 0)
            {
                InstructionNode source = queue.Dequeue();
                if (!expanded.Add(source.Offset))
                    continue;
                AddNormalEdges(
                    body,
                    source,
                    nodes,
                    edges,
                    queue,
                    diagnostics,
                    ref complete);
                changed = true;
            }
            HashSet<int> next_reachable = ReachableOffsets(edges);
            changed |= !next_reachable.SetEquals(reachable);
            reachable = next_reachable;
            if (!changed)
                break;
        }

        return new DecodedGraph
        {
            Nodes = nodes.Values.OrderBy(node => node.Offset).ToList(),
            Edges = edges,
            Complete = complete
        };
    }

    static void AddNormalEdges(
        ASMethodBody body,
        InstructionNode source,
        Dictionary<int, InstructionNode> nodes,
        List<DecodedEdge> edges,
        Queue<InstructionNode> queue,
        List<string> diagnostics,
        ref bool complete)
    {
        if (source.Value is Jumper jumper)
        {
            AddDecodedEdge(
                body,
                source,
                source.Offset + source.Size + SignedOffset(jumper.Offset),
                jumper.OP == OPCode.Jump ? "Jump" : "Taken",
                null,
                nodes,
                edges,
                queue,
                diagnostics,
                ref complete);
            if (jumper.OP != OPCode.Jump)
            {
                AddDecodedEdge(
                    body,
                    source,
                    source.Offset + source.Size,
                    "Fallthrough",
                    null,
                    nodes,
                    edges,
                    queue,
                    diagnostics,
                    ref complete);
            }
            return;
        }
        if (source.Value is LookUpSwitchIns lookup)
        {
            for (int case_index = 0;
                case_index < lookup.CaseOffsets.Count;
                case_index++)
            {
                AddDecodedEdge(
                    body,
                    source,
                    source.Offset +
                        SignedOffset(lookup.CaseOffsets[case_index]),
                    "Case",
                    case_index,
                    nodes,
                    edges,
                    queue,
                    diagnostics,
                    ref complete);
            }
            AddDecodedEdge(
                body,
                source,
                source.Offset + SignedOffset(lookup.DefaultOffset),
                "Default",
                null,
                nodes,
                edges,
                queue,
                diagnostics,
                ref complete);
            return;
        }
        if (IsTerminal(source.Value.OP))
            return;
        int next = source.Offset + source.Size;
        if (next == body.Code.Length)
            return;
        AddDecodedEdge(
            body,
            source,
            next,
            "Next",
            null,
            nodes,
            edges,
            queue,
            diagnostics,
            ref complete);
    }

    static void AddDecodedEdge(
        ASMethodBody body,
        InstructionNode source,
        int target,
        string kind,
        int? case_index,
        Dictionary<int, InstructionNode> nodes,
        List<DecodedEdge> edges,
        Queue<InstructionNode> queue,
        List<string> diagnostics,
        ref bool complete)
    {
        var edge = new DecodedEdge
        {
            Source = source,
            Target = target,
            Kind = kind,
            CaseIndex = case_index
        };
        edge.Resolved = ResolveTarget(
            body,
            edge,
            nodes,
            queue,
            diagnostics,
            ref complete);
        edges.Add(edge);
    }

    static bool ResolveTarget(
        ASMethodBody body,
        DecodedEdge edge,
        Dictionary<int, InstructionNode> nodes,
        Queue<InstructionNode> queue,
        List<string> diagnostics,
        ref bool complete)
    {
        if (nodes.ContainsKey(edge.Target))
            return true;
        if ((uint)edge.Target >= (uint)body.Code.Length)
        {
            diagnostics.Add(
                $"{edge.Kind.ToLowerInvariant()} at {edge.Source.Offset} " +
                $"targets non-instruction offset {edge.Target}");
            complete = false;
            return false;
        }
        try
        {
            ASInstruction instruction = ASCode.DecodeAt(
                body.ABC,
                body.Code,
                edge.Target);
            var node = new InstructionNode
            {
                Offset = edge.Target,
                Size = instruction.DecodedSize,
                Value = instruction,
                Storage = false
            };
            nodes.Add(node.Offset, node);
            queue.Enqueue(node);
            return true;
        }
        catch (Exception error)
        {
            diagnostics.Add(
                $"{edge.Kind.ToLowerInvariant()} at {edge.Source.Offset} " +
                $"targets undecodable offset {edge.Target}: " +
                $"{error.GetType().Name}: {error.Message}");
            complete = false;
            return false;
        }
    }

    static HashSet<int> ReachableOffsets(
        IReadOnlyList<DecodedEdge> edges)
    {
        var reachable = new HashSet<int> { 0 };
        var outgoing = edges
            .Where(edge => edge.Resolved)
            .GroupBy(edge => edge.Source.Offset)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.Target).Distinct().ToArray());
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int source = queue.Dequeue();
            foreach (int target in outgoing.GetValueOrDefault(source) ?? [])
            {
                if (reachable.Add(target))
                    queue.Enqueue(target);
            }
        }
        return reachable;
    }

    static HashSet<int> FindLeaders(
        IReadOnlyList<InstructionNode> nodes,
        IReadOnlyList<DecodedEdge> edges)
    {
        var leaders = new HashSet<int> { 0 };
        foreach (DecodedEdge edge in edges.Where(edge =>
            edge.Resolved && edge.Kind != "Next"))
        {
            leaders.Add(edge.Target);
        }
        foreach (IGrouping<int, DecodedEdge> incoming in edges
            .Where(edge => edge.Resolved)
            .GroupBy(edge => edge.Target)
            .Where(group => group
                .Select(edge => edge.Source.Offset)
                .Distinct()
                .Skip(1)
                .Any()))
        {
            leaders.Add(incoming.Key);
        }
        InstructionNode[] storage = nodes
            .Where(node => node.Storage)
            .OrderBy(node => node.Offset)
            .ToArray();
        for (int index = 0; index + 1 < storage.Length; index++)
        {
            if (storage[index].Value is Jumper or LookUpSwitchIns ||
                IsTerminal(storage[index].Value.OP))
            {
                leaders.Add(storage[index + 1].Offset);
            }
        }
        return leaders;
    }

    static List<List<InstructionNode>> PartitionBlocks(
        IReadOnlyList<InstructionNode> nodes,
        IReadOnlyDictionary<int, InstructionNode> node_by_offset,
        HashSet<int> leaders)
    {
        var result = new List<List<InstructionNode>>();
        var assigned = new HashSet<int>();
        foreach (int leader in leaders.Order())
            AddBlock(leader);
        foreach (InstructionNode node in nodes.OrderBy(node => node.Offset))
        {
            if (!assigned.Contains(node.Offset))
                AddBlock(node.Offset);
        }
        result.Sort((left, right) =>
            left[0].Offset.CompareTo(right[0].Offset));
        return result;

        void AddBlock(int start)
        {
            if (assigned.Contains(start) ||
                !node_by_offset.TryGetValue(start, out InstructionNode? node))
            {
                return;
            }
            var block = new List<InstructionNode>();
            while (assigned.Add(node.Offset))
            {
                block.Add(node);
                if (node.Value is Jumper or LookUpSwitchIns ||
                    IsTerminal(node.Value.OP))
                {
                    break;
                }
                int next = node.Offset + node.Size;
                if (leaders.Contains(next) ||
                    !node_by_offset.TryGetValue(
                        next,
                        out InstructionNode? next_node))
                {
                    break;
                }
                node = next_node;
            }
            if (block.Count > 0)
                result.Add(block);
        }
    }

    static void AddEdge(
        List<Avm2ControlFlowEdgeInventory> edges,
        BlockNode source,
        InstructionNode instruction,
        int target,
        string kind,
        int? case_index,
        int? exception_index,
        string? exception_type,
        Dictionary<int, int> block_by_offset,
        int code_length)
    {
        edges.Add(new Avm2ControlFlowEdgeInventory
        {
            FromBlock = source.Id,
            ToBlock = target == code_length ? null : block_by_offset.GetValueOrDefault(target, -1) is int block && block >= 0 ? block : null,
            SourceInstruction = instruction.Index,
            SourceOffset = instruction.Offset,
            TargetOffset = target,
            Kind = kind,
            CaseIndex = case_index,
            ExceptionIndex = exception_index,
            ExceptionType = exception_type
        });
    }

    static void MarkReachable(
        List<BlockNode> blocks,
        List<Avm2ControlFlowEdgeInventory> edges)
    {
        if (blocks.Count == 0)
            return;
        foreach (BlockNode block in blocks)
            block.Reachable = false;
        var outgoing = edges
            .Where(edge => edge.ToBlock.HasValue)
            .GroupBy(edge => edge.FromBlock)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ToBlock!.Value).Distinct().ToArray());
        var queue = new Queue<int>();
        queue.Enqueue(0);
        blocks[0].Reachable = true;
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int target in outgoing.GetValueOrDefault(current) ?? [])
            {
                if (blocks[target].Reachable)
                    continue;
                blocks[target].Reachable = true;
                queue.Enqueue(target);
            }
        }
    }

    static void AnalyzeDepths(
        ASMethodBody body,
        List<InstructionNode> nodes,
        GraphResult graph,
        List<string> diagnostics)
    {
        if (graph.Blocks.Count == 0)
            return;

        graph.Blocks[0].EntryStack = 0;
        graph.Blocks[0].EntryScope = body.InitialScopeDepth;
        var outgoing = graph.Edges
            .GroupBy(edge => edge.FromBlock)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var queued = new bool[graph.Blocks.Count];
        var queue = new Queue<int>();
        queue.Enqueue(0);
        queued[0] = true;

        while (queue.Count > 0)
        {
            int block_index = queue.Dequeue();
            queued[block_index] = false;
            BlockNode block = graph.Blocks[block_index];
            int stack = block.EntryStack ?? 0;
            int scope = block.EntryScope ?? body.InitialScopeDepth;
            for (int instruction_index = block.First; instruction_index <= block.Last; instruction_index++)
            {
                ASInstruction instruction = nodes[instruction_index].Value;
                int pop = PopCount(instruction);
                if (stack < pop)
                {
                    diagnostics.Add($"operand stack underflow in block {block.Id} at {nodes[instruction_index].Offset}: need {pop}, have {stack}");
                    stack = pop;
                }
                stack += PushCount(instruction) - pop;
                int scope_delta = ScopeDelta(instruction);
                if (scope + scope_delta < body.InitialScopeDepth)
                    diagnostics.Add($"scope stack underflow in block {block.Id} at {nodes[instruction_index].Offset}");
                scope += scope_delta;
            }
            block.ExitStack = stack;
            block.ExitScope = scope;

            foreach (Avm2ControlFlowEdgeInventory edge in outgoing.GetValueOrDefault(block.Id) ?? [])
            {
                if (!edge.ToBlock.HasValue)
                    continue;
                BlockNode target = graph.Blocks[edge.ToBlock.Value];
                int next_stack = edge.Kind == "Exception" ? 1 : stack;
                int next_scope = edge.Kind == "Exception" ? body.InitialScopeDepth : scope;
                bool changed = MergeDepth(target, next_stack, next_scope, diagnostics, edge);
                if (changed && !queued[target.Id])
                {
                    queued[target.Id] = true;
                    queue.Enqueue(target.Id);
                }
            }
        }
    }

    static bool MergeDepth(
        BlockNode target,
        int stack,
        int scope,
        List<string> diagnostics,
        Avm2ControlFlowEdgeInventory edge)
    {
        bool changed = false;
        if (!target.EntryStack.HasValue)
        {
            target.EntryStack = stack;
            changed = true;
        }
        else if (target.EntryStack.Value != stack)
        {
            diagnostics.Add(
                $"operand stack depth mismatch at block {target.Id}: {target.EntryStack.Value} and {stack} from block {edge.FromBlock}");
        }
        if (!target.EntryScope.HasValue)
        {
            target.EntryScope = scope;
            changed = true;
        }
        else if (target.EntryScope.Value != scope)
        {
            diagnostics.Add(
                $"scope stack depth mismatch at block {target.Id}: {target.EntryScope.Value} and {scope} from block {edge.FromBlock}");
        }
        return changed;
    }

    static List<Avm2ReferenceInventory> ReadReferences(List<InstructionNode> nodes)
    {
        var references = new List<Avm2ReferenceInventory>();
        foreach (InstructionNode node in nodes)
        {
            ASInstruction instruction = node.Value;
            ASMultiname? symbol = ReadSymbol(instruction);
            int? method_index = ReadIntegerProperty(instruction, "MethodIndex");
            int? class_index = ReadIntegerProperty(instruction, "ClassIndex");
            int? argument_count = ReadIntegerProperty(instruction, "ArgCount");
            string kind = ReferenceKind(instruction.OP);
            string target = symbol is null ? "" : Qualified(symbol);
            string? symbol_identity = symbol is null ? null : ExactSymbolIdentity(symbol);
            string? runtime_symbol_identity = symbol is null
                ? null
                : RuntimeSymbolIdentity(symbol);
            string? normalized_symbol_identity = symbol is null
                ? null
                : NormalizedSymbolIdentity(symbol);

            if (instruction is CallStaticIns call_static)
            {
                target = ResolvedMethodTarget(call_static.MethodIndex, () => call_static.Method);
                method_index = call_static.MethodIndex;
            }
            else if (instruction is NewFunctionIns function)
            {
                target = ResolvedMethodTarget(function.MethodIndex, () => function.Method);
                method_index = function.MethodIndex;
            }
            else if (instruction is CallMethodIns dispatch)
            {
                target = $"dispatch-slot:{dispatch.MethodIndex}";
                method_index = null;
            }
            else if (instruction is NewClassIns new_class)
            {
                target = ResolvedClassTarget(new_class);
                class_index = new_class.ClassIndex;
            }
            else if (instruction is GetGlobalSlotIns global_read)
            {
                target = $"global-slot:{global_read.SlotIndex}";
            }
            else if (instruction is SetGlobalSlotIns global_write)
            {
                target = $"global-slot:{global_write.SlotIndex}";
            }
            else if (instruction is GetSlotIns slot_read)
            {
                target = $"slot:{slot_read.SlotIndex}";
            }
            else if (instruction is SetSlotIns slot_write)
            {
                target = $"slot:{slot_write.SlotIndex}";
            }
            else if (instruction is GetScopeObjectIns scope)
            {
                target = $"scope:{scope.ScopeIndex}";
            }
            else if (instruction is NewCatchIns exception)
            {
                target = $"exception:{exception.ExceptionIndex}";
            }
            else if (instruction is HasNext2Ins iteration)
            {
                target = $"registers:{iteration.ObjectIndex},{iteration.RegisterIndex}";
            }
            else if (instruction is Primitive primitive)
            {
                kind = LiteralKind(primitive.Value);
                target = LiteralText(primitive.Value);
            }
            else if (instruction.OP == OPCode.PushUndefined)
            {
                kind = "UndefinedLiteral";
                target = "undefined";
            }

            if (kind.Length == 0)
                continue;
            if (target.Length == 0)
                target = method_index.HasValue ? $"method:{method_index.Value}" :
                    class_index.HasValue ? $"class:{class_index.Value}" :
                    instruction.OP.ToString();
            references.Add(new Avm2ReferenceInventory
            {
                Instruction = node.Index,
                Offset = node.Offset,
                Kind = kind,
                Target = target,
                SymbolIdentity = symbol_identity,
                EncodingSymbolIdentity = symbol_identity,
                RuntimeSymbolIdentity = runtime_symbol_identity,
                NormalizedSymbolIdentity = normalized_symbol_identity,
                ArgumentCount = argument_count,
                MethodIndex = method_index,
                ClassIndex = class_index
            });
        }
        return references;
    }

    static string ReferenceKind(OPCode op) => op switch
    {
        OPCode.Call or OPCode.CallMethod or OPCode.CallProperty or OPCode.CallPropLex or
        OPCode.CallPropVoid or OPCode.CallStatic => "Call",
        OPCode.CallSuper or OPCode.CallSuperVoid => "SuperCall",
        OPCode.Construct or OPCode.ConstructProp or OPCode.ConstructSuper => "Construct",
        OPCode.GetProperty or OPCode.GetSuper => "PropertyRead",
        OPCode.SetProperty or OPCode.SetSuper or OPCode.InitProperty => "PropertyWrite",
        OPCode.DeleteProperty => "PropertyDelete",
        OPCode.FindDef or OPCode.FindProperty or OPCode.FindPropStrict => "PropertyLookup",
        OPCode.GetLex => "LexicalRead",
        OPCode.GetDescendants => "DescendantRead",
        OPCode.Coerce or OPCode.AsType => "Type",
        OPCode.IsType => "TypeCheck",
        OPCode.AsTypeLate => "DynamicTypeCast",
        OPCode.IsTypeLate => "DynamicTypeCheck",
        OPCode.GetGlobalSlot or OPCode.GetSlot => "SlotRead",
        OPCode.SetGlobalSlot or OPCode.SetSlot => "SlotWrite",
        OPCode.GetOuterScope or OPCode.GetScopeObject => "ScopeRead",
        OPCode.NewCatch => "ExceptionScope",
        OPCode.HasNext2 => "IterationState",
        OPCode.NewClass => "Class",
        OPCode.NewFunction => "Function",
        _ => ""
    };

    static string Fingerprint(
        ASMethod method,
        List<InstructionNode> nodes,
        GraphResult graph,
        bool semantic)
    {
        var context = new IdentityContext();
        var canonical = new StringBuilder();
        AppendMethodFingerprint(canonical, method, nodes, graph, semantic, context);
        return Hash(canonical);
    }

    static void AppendMethodFingerprint(
        StringBuilder canonical,
        ASMethod method,
        List<InstructionNode> nodes,
        GraphResult graph,
        bool semantic,
        IdentityContext context)
    {
        AppendSignature(canonical, method, semantic);
        if (method.Body is not null)
        {
            canonical.Append("|frame:")
                .Append(method.Body.MaxStack)
                .Append(':')
                .Append(method.Body.LocalCount)
                .Append(':')
                .Append(method.Body.InitialScopeDepth)
                .Append(':')
                .Append(method.Body.MaxScopeDepth);
            AppendTraitIdentities(
                canonical,
                "activation",
                method.Body.Traits,
                semantic,
                context);
        }
        foreach (InstructionNode node in nodes)
        {
            ASInstruction instruction = node.Value;
            canonical.Append('|').Append(((byte)instruction.OP).ToString("x2", CultureInfo.InvariantCulture));
            canonical.Append(':').Append(PopCount(instruction)).Append('>').Append(PushCount(instruction));
            if (instruction is Jumper jumper)
            {
                int target = node.Offset + node.Size + SignedOffset(jumper.Offset);
                canonical.Append("@b").Append(graph.BlockByOffset.GetValueOrDefault(target, -1));
            }
            else if (instruction is LookUpSwitchIns lookup)
            {
                foreach (uint raw_target in lookup.CaseOffsets.Append(lookup.DefaultOffset))
                {
                    int target = node.Offset + SignedOffset(raw_target);
                    canonical.Append("@b").Append(graph.BlockByOffset.GetValueOrDefault(target, -1));
                }
            }
            ASMultiname? symbol = ReadSymbol(instruction);
            if (symbol is not null)
                canonical.Append(':').Append(SymbolIdentity(symbol, semantic));
            if (instruction is Primitive primitive)
            {
                canonical.Append(':').Append(LiteralKind(primitive.Value));
                if (semantic)
                    canonical.Append('=').Append(LiteralText(primitive.Value));
            }
            if (instruction is Local local)
                canonical.Append(":l").Append(local.Register);
            int? arguments = ReadIntegerProperty(instruction, "ArgCount");
            if (arguments.HasValue)
                canonical.Append(":a").Append(arguments.Value);
            int? slot = ReadIntegerProperty(instruction, "SlotIndex");
            if (slot.HasValue)
                canonical.Append(":s").Append(slot.Value);
            AppendFingerprintOperands(canonical, instruction, semantic, context);
        }
        if (method.Body is not null)
        {
            foreach (ASException exception in method.Body.Exceptions)
            {
                canonical.Append("|exception:")
                    .Append(exception.From)
                    .Append(':')
                    .Append(exception.To)
                    .Append(':')
                    .Append(exception.Target)
                    .Append(':')
                    .Append(SafeValue(
                        () => SymbolIdentity(exception.ExceptionType, semantic),
                        $"pool:{exception.ExceptionTypeIndex}"))
                    .Append(':')
                    .Append(SafeValue(
                        () => SymbolIdentity(exception.VariableName, semantic),
                        $"pool:{exception.VariableNameIndex}"));
            }
        }
    }

    static void AppendSignature(StringBuilder canonical, ASMethod method, bool semantic)
    {
        canonical.Append("flags:")
            .Append((int)method.Flags)
            .Append("|params:")
            .Append(method.Parameters.Count);
        foreach (ASParameter parameter in method.Parameters)
        {
            canonical.Append(':').Append(SafeValue(
                () => SymbolIdentity(parameter.Type, semantic),
                $"invalid-type:{parameter.TypeIndex}"));
            if (parameter.IsOptional)
            {
                canonical.Append("=optional:");
                if (semantic)
                {
                    canonical.Append("effective:")
                        .Append(SafeValue(
                            () => ConstantValueIdentity(
                                parameter.Value,
                                parameter.Type),
                            $"pool:{parameter.ValueIndex}"));
                }
                else
                {
                    canonical.Append(parameter.ValueIndex == 0
                            ? "implicit:"
                            : "explicit:")
                        .Append((int)parameter.ValueKind)
                        .Append(':')
                        .Append(SafeValue(
                            () => LiteralKind(parameter.Value),
                            $"pool:{parameter.ValueIndex}"));
                }
            }
            else
            {
                canonical.Append("=required");
            }
        }
        canonical.Append('>').Append(SafeValue(
            () => SymbolIdentity(method.ReturnType, semantic),
            $"invalid-type:{method.ReturnTypeIndex}"));
        if (semantic)
        {
            string name = SafeValue(
                () => method.Name ?? "",
                $"invalid-name:{method.NameIndex}");
            canonical.Append('#').Append(Token(name));
        }
    }

    static void AppendFingerprintOperands(
        StringBuilder canonical,
        ASInstruction instruction,
        bool semantic,
        IdentityContext context)
    {
        switch (instruction)
        {
            case CallStaticIns call:
                canonical.Append(":method=")
                    .Append(ResolvedMethodIdentity(
                        call.MethodIndex,
                        () => call.Method,
                        semantic,
                        context));
                break;
            case NewFunctionIns function:
                canonical.Append(":method=")
                    .Append(ResolvedMethodIdentity(
                        function.MethodIndex,
                        () => function.Method,
                        semantic,
                        context));
                break;
            case CallMethodIns dispatch:
                canonical.Append(":dispatch=").Append(dispatch.MethodIndex);
                break;
            case NewClassIns value:
                canonical.Append(":class=")
                    .Append(ResolvedClassIdentity(value, semantic, context));
                break;
            case GetScopeObjectIns scope:
                canonical.Append(":scope=").Append(scope.ScopeIndex);
                break;
            case GetOuterScopeIns scope:
                canonical.Append(":outer-scope=").Append(scope.ScopeIndex);
                break;
            case BkptLineIns line:
                canonical.Append(":breakpoint-line=").Append(line.LineNumber);
                break;
            case NewCatchIns exception:
                canonical.Append(":exception=").Append(exception.ExceptionIndex);
                break;
            case HasNext2Ins iteration:
                canonical.Append(":iterator=")
                    .Append(iteration.ObjectIndex)
                    .Append(',')
                    .Append(iteration.RegisterIndex);
                break;
            case DebugIns debug when semantic:
                canonical.Append(":debug=")
                    .Append(debug.DebugType)
                    .Append(',')
                    .Append(debug.RegisterIndex)
                    .Append(',')
                    .Append(debug.Extra)
                    .Append(',')
                    .Append(SafeValue(() => debug.Name, $"pool:{debug.NameIndex}"));
                break;
            case DebugFileIns file when semantic:
                canonical.Append(":file=")
                    .Append(SafeValue(() => file.FileName, $"pool:{file.FileNameIndex}"));
                break;
            case DebugLineIns line when semantic:
                canonical.Append(":line=").Append(line.LineNumber);
                break;
            case DxnsIns ns when semantic:
                canonical.Append(":dxns=")
                    .Append(SafeValue(() => ns.Uri, $"pool:{ns.UriIndex}"));
                break;
            case PushNamespaceIns ns:
                canonical.Append(":namespace=")
                    .Append(SafeValue(
                        () => RuntimeNamespaceIdentity(
                            ns.Namespace.Pool,
                            ns.NamespaceIndex,
                            semantic
                                ? SymbolIdentityMode.Runtime
                                : SymbolIdentityMode.Normalized),
                        $"invalid:{ns.NamespaceIndex}"));
                break;
        }
    }

    internal static Dictionary<string, string?> ReadOperands(ASInstruction instruction)
    {
        var operands = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (PropertyInfo property in instruction.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            if (!property.CanRead ||
                property.GetIndexParameters().Length != 0 ||
                property.Name is nameof(ASInstruction.OP) or
                    nameof(ASInstruction.DecodedOffset) or
                    nameof(ASInstruction.DecodedSize))
                continue;
            object? value;
            try
            {
                value = property.GetValue(instruction);
            }
            catch
            {
                continue;
            }
            string? text = OperandText(value);
            if (text is not null)
                operands.TryAdd(property.Name, text);
        }
        return operands;
    }

    static string? OperandText(object? value)
    {
        if (value is null)
            return "null";
        if (value is ASMultiname multiname)
            return Qualified(multiname);
        if (value is ASNamespace ns)
            return $"{ns.Kind}:{ns.RuntimeName}";
        if (value is ASFloat4 float4)
            return Float4Text(float4);
        if (value is string text)
            return text;
        if (value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or Enum)
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        if (value is IEnumerable sequence)
        {
            var values = new List<string?>();
            foreach (object? item in sequence)
            {
                string? converted = OperandText(item);
                if (converted is null)
                    return null;
                values.Add(converted);
            }
            return string.Join(",", values);
        }
        return null;
    }

    internal static int PopCount(ASInstruction instruction) =>
        Avm2InstructionSemantics.Read(instruction).PopCount;

    internal static int PushCount(ASInstruction instruction) =>
        Avm2InstructionSemantics.Read(instruction).PushCount;

    static int ScopeDelta(ASInstruction instruction) =>
        Avm2InstructionSemantics.Read(instruction).ScopeDelta;

    static bool IsTerminal(OPCode op) =>
        op is OPCode.ReturnValue or OPCode.ReturnVoid or OPCode.Throw;

    static int SignedOffset(uint value)
    {
        int result = (int)(value & 0xFFFFFF);
        return (result & 0x800000) == 0 ? result : result | unchecked((int)0xFF000000);
    }

    static int? ReadIntegerProperty(ASInstruction instruction, string name)
    {
        PropertyInfo? property = instruction.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property is null || property.PropertyType != typeof(int))
            return null;
        try
        {
            return (int?)property.GetValue(instruction);
        }
        catch
        {
            return null;
        }
    }

    internal static ASMultiname? ReadSymbol(ASInstruction instruction)
    {
        foreach (PropertyInfo property in instruction.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            if (!typeof(ASMultiname).IsAssignableFrom(property.PropertyType) || !property.CanRead)
                continue;
            try
            {
                return property.GetValue(instruction) as ASMultiname;
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    internal static string Qualified(ASMultiname? name)
    {
        if (name is null)
            return "";
        try
        {
            if (name.Kind == MultinameKind.TypeName)
            {
                string root = Qualified(name.QName);
                string types = string.Join(",", name.TypeIndices.Select(index =>
                    index == 0
                        ? "*"
                        : index > 0 && index < name.Pool.Multinames.Count
                            ? Qualified(name.Pool.Multinames[index])
                            : "<invalid>"));
                return $"{root}.<{types}>";
            }
            string local = name.RuntimeName;
            string ns = name.Namespace?.RuntimeName ?? "";
            return string.IsNullOrEmpty(ns) ? local : $"{ns}.{local}";
        }
        catch
        {
            return "";
        }
    }

    public static string ExactSymbolIdentity(ASMultiname? name) =>
        EncodingSymbolIdentity(name);

    public static string EncodingSymbolIdentity(ASMultiname? name)
    {
        var visited = new HashSet<ASMultiname>(ReferenceEqualityComparer.Instance);
        return SymbolIdentity(name, SymbolIdentityMode.Encoding, visited);
    }

    public static string RuntimeSymbolIdentity(ASMultiname? name)
    {
        var visited = new HashSet<ASMultiname>(ReferenceEqualityComparer.Instance);
        return SymbolIdentity(name, SymbolIdentityMode.Runtime, visited);
    }

    public static string NormalizedSymbolIdentity(ASMultiname? name)
    {
        var visited = new HashSet<ASMultiname>(ReferenceEqualityComparer.Instance);
        return SymbolIdentity(name, SymbolIdentityMode.Normalized, visited);
    }

    public static bool TryGetStaticName(
        ASMultiname? name,
        out string value)
    {
        value = string.Empty;
        if (name is null ||
            name.IsNameNeeded ||
            name.IsAnyName ||
            name.Kind is not (
                MultinameKind.QName or
                MultinameKind.QNameA or
                MultinameKind.RTQName or
                MultinameKind.RTQNameA or
                MultinameKind.Multiname or
                MultinameKind.MultinameA) ||
            name.NameIndex < 0 ||
            name.NameIndex >= name.Pool.Strings.Count)
        {
            return false;
        }
        string? runtime_name = name.Pool.Strings[name.NameIndex];
        if (runtime_name is null)
            return false;
        value = runtime_name;
        return true;
    }

    public static string EncodingNamespaceIdentity(ASNamespace? value)
    {
        if (value is null)
            return "null";
        int index = ReferenceIndex(value.Pool.Namespaces, value);
        return index < 0
            ? $"unpooled:k:{(byte)value.Kind:x2}|ni:{value.NameIndex}|n:{Token(SafeValue(() => value.Name ?? "", ""))}"
            : EncodingNamespaceIdentity(value.Pool, index);
    }

    public static string RuntimeNamespaceIdentity(ASNamespace? value)
    {
        if (value is null)
            return "null";
        if (runtime_identity_cache is not null &&
            runtime_identity_cache.TryGetNamespace(
                value,
                SymbolIdentityMode.Runtime,
                out string cached))
        {
            return cached;
        }
        int index = ReferenceIndex(value.Pool.Namespaces, value);
        return index < 0
            ? $"unpooled:{RuntimeNamespaceKind(value.Kind)}:{Token(SafeValue(() => value.RuntimeName, ""))}"
            : RuntimeNamespaceIdentity(value.Pool, index, SymbolIdentityMode.Runtime);
    }

    public static string NormalizedNamespaceIdentity(ASNamespace? value)
    {
        if (value is null)
            return "null";
        if (runtime_identity_cache is not null &&
            runtime_identity_cache.TryGetNamespace(
                value,
                SymbolIdentityMode.Normalized,
                out string cached))
        {
            return cached;
        }
        int index = ReferenceIndex(value.Pool.Namespaces, value);
        return index < 0
            ? $"unpooled:{RuntimeNamespaceKind(value.Kind)}:{Token(NormalizeSymbol(SafeValue(() => value.RuntimeName, ""), true))}"
            : RuntimeNamespaceIdentity(value.Pool, index, SymbolIdentityMode.Normalized);
    }

    public static string EncodingNamespaceSetIdentity(ASNamespaceSet? value)
    {
        if (value is null)
            return "null";
        int index = ReferenceIndex(value.Pool.NamespaceSets, value);
        return index < 0
            ? $"unpooled:[{string.Join(",", value.NamespaceIndices.Select((namespace_index, position) =>
                $"{position}:{namespace_index}:{EncodingNamespaceIdentity(value.Pool, namespace_index)}"))}]"
            : EncodingNamespaceSetIdentity(value.Pool, index);
    }

    public static string RuntimeNamespaceSetIdentity(ASNamespaceSet? value)
    {
        if (value is null)
            return "null";
        int index = ReferenceIndex(value.Pool.NamespaceSets, value);
        return index < 0
            ? $"unpooled:[{string.Join(",", value.NamespaceIndices.Select((namespace_index, position) =>
                $"{position}:{RuntimeNamespaceIdentity(value.Pool, namespace_index, SymbolIdentityMode.Runtime)}"))}]"
            : RuntimeNamespaceSetIdentity(value.Pool, index, SymbolIdentityMode.Runtime);
    }

    public static string NormalizedNamespaceSetIdentity(ASNamespaceSet? value)
    {
        if (value is null)
            return "null";
        int index = ReferenceIndex(value.Pool.NamespaceSets, value);
        return index < 0
            ? $"unpooled:[{string.Join(",", value.NamespaceIndices.Select((namespace_index, position) =>
                $"{position}:{RuntimeNamespaceIdentity(value.Pool, namespace_index, SymbolIdentityMode.Normalized)}"))}]"
            : RuntimeNamespaceSetIdentity(value.Pool, index, SymbolIdentityMode.Normalized);
    }

    public static string SymbolIdentity(ASMultiname? name, bool semantic) =>
        semantic
            ? RuntimeSymbolIdentity(name)
            : NormalizedSymbolIdentity(name);

    static string SymbolIdentity(
        ASMultiname? name,
        SymbolIdentityMode mode,
        HashSet<ASMultiname> visited)
    {
        if (name is null)
            return "null";
        bool cacheable = visited.Count == 0 &&
            runtime_identity_cache is not null;
        if (cacheable &&
            runtime_identity_cache!.TryGetSymbol(
                name,
                mode,
                out string cached))
        {
            return cached;
        }
        if (!visited.Add(name))
            return $"cycle:k:{(byte)name.Kind:x2}";
        try
        {
            string identity = mode == SymbolIdentityMode.Encoding
                ? EncodingMultinameIdentity(name, visited)
                : RuntimeMultinameIdentity(name, mode, visited);
            if (cacheable)
            {
                runtime_identity_cache!.AddSymbol(
                    name,
                    mode,
                    identity);
            }
            return identity;
        }
        catch
        {
            return $"k:{(byte)name.Kind:x2}|invalid";
        }
        finally
        {
            visited.Remove(name);
        }
    }

    static string EncodingMultinameIdentity(
        ASMultiname name,
        HashSet<ASMultiname> visited) =>
        name.Kind switch
        {
            MultinameKind.QName or MultinameKind.QNameA =>
                $"k:{(byte)name.Kind:x2}|nsi:{name.NamespaceIndex}|ns:" +
                EncodingNamespaceIdentity(name.Pool, name.NamespaceIndex) +
                $"|ni:{name.NameIndex}|n:{EncodingStringIdentity(name.Pool, name.NameIndex)}",
            MultinameKind.RTQName or MultinameKind.RTQNameA =>
                $"k:{(byte)name.Kind:x2}|ni:{name.NameIndex}|n:" +
                EncodingStringIdentity(name.Pool, name.NameIndex),
            MultinameKind.RTQNameL or MultinameKind.RTQNameLA =>
                $"k:{(byte)name.Kind:x2}",
            MultinameKind.Multiname or MultinameKind.MultinameA =>
                $"k:{(byte)name.Kind:x2}|ni:{name.NameIndex}|n:" +
                EncodingStringIdentity(name.Pool, name.NameIndex) +
                $"|seti:{name.NamespaceSetIndex}|set:" +
                EncodingNamespaceSetIdentity(name.Pool, name.NamespaceSetIndex),
            MultinameKind.MultinameL or MultinameKind.MultinameLA =>
                $"k:{(byte)name.Kind:x2}|seti:{name.NamespaceSetIndex}|set:" +
                EncodingNamespaceSetIdentity(name.Pool, name.NamespaceSetIndex),
            MultinameKind.TypeName =>
                $"k:{(byte)name.Kind:x2}|qi:{name.QNameIndex}|q:" +
                EncodingMultinameReference(name.Pool, name.QNameIndex, visited) +
                $"|types:[{string.Join(",", name.TypeIndices.Select((index, position) =>
                    $"{position}:{index}:{EncodingMultinameReference(name.Pool, index, visited)}"))}]",
            _ => $"k:{(byte)name.Kind:x2}|invalid"
        };

    static string RuntimeMultinameIdentity(
        ASMultiname name,
        SymbolIdentityMode mode,
        HashSet<ASMultiname> visited) =>
        name.Kind switch
        {
            MultinameKind.QName or MultinameKind.QNameA =>
                $"k:{(byte)name.Kind:x2}|ns:" +
                RuntimeNamespaceIdentity(name.Pool, name.NamespaceIndex, mode) +
                $"|n:{RuntimeStringIdentity(name.Pool, name.NameIndex, mode)}",
            MultinameKind.RTQName or MultinameKind.RTQNameA =>
                $"k:{(byte)name.Kind:x2}|n:" +
                RuntimeStringIdentity(name.Pool, name.NameIndex, mode),
            MultinameKind.RTQNameL or MultinameKind.RTQNameLA =>
                $"k:{(byte)name.Kind:x2}",
            MultinameKind.Multiname or MultinameKind.MultinameA =>
                $"k:{(byte)name.Kind:x2}|n:" +
                RuntimeStringIdentity(name.Pool, name.NameIndex, mode) +
                $"|set:{RuntimeNamespaceSetIdentity(name.Pool, name.NamespaceSetIndex, mode)}",
            MultinameKind.MultinameL or MultinameKind.MultinameLA =>
                $"k:{(byte)name.Kind:x2}|set:" +
                RuntimeNamespaceSetIdentity(name.Pool, name.NamespaceSetIndex, mode),
            MultinameKind.TypeName =>
                $"k:{(byte)name.Kind:x2}|q:" +
                RuntimeMultinameReference(name.Pool, name.QNameIndex, mode, visited) +
                $"|types:[{string.Join(",", name.TypeIndices.Select((index, position) =>
                    $"{position}:{RuntimeMultinameReference(name.Pool, index, mode, visited)}"))}]",
            _ => $"k:{(byte)name.Kind:x2}|invalid"
        };

    static string EncodingMultinameReference(
        ASConstantPool pool,
        int index,
        HashSet<ASMultiname> visited)
    {
        if (index < 0 || index >= pool.Multinames.Count)
            return $"invalid:{index}";
        return SymbolIdentity(pool.Multinames[index], SymbolIdentityMode.Encoding, visited);
    }

    static string RuntimeMultinameReference(
        ASConstantPool pool,
        int index,
        SymbolIdentityMode mode,
        HashSet<ASMultiname> visited)
    {
        if (index < 0 || index >= pool.Multinames.Count)
            return "invalid";
        return SymbolIdentity(pool.Multinames[index], mode, visited);
    }

    static string EncodingStringIdentity(ASConstantPool pool, int index)
    {
        if (index < 0 || index >= pool.Strings.Count)
            return $"invalid:{index}";
        return $"{index}:{Token(pool.Strings[index] ?? "")}";
    }

    static string RuntimeStringIdentity(
        ASConstantPool pool,
        int index,
        SymbolIdentityMode mode)
    {
        if (index == 0)
            return "any-name";
        if (index < 0 || index >= pool.Strings.Count)
            return "invalid";
        string value = pool.Strings[index] ?? "";
        return Token(mode == SymbolIdentityMode.Runtime
            ? value
            : NormalizeSymbol(value, true));
    }

    static string EncodingNamespaceIdentity(ASConstantPool pool, int index)
    {
        if (index < 0 || index >= pool.Namespaces.Count)
            return $"invalid:{index}";
        ASNamespace? value = pool.Namespaces[index];
        if (value is null)
            return $"{index}:null";
        return $"{index}:k:{(byte)value.Kind:x2}|ni:{value.NameIndex}|n:" +
            EncodingStringIdentity(pool, value.NameIndex);
    }

    static string EncodingNamespaceSetIdentity(ASConstantPool pool, int index)
    {
        if (index < 0 || index >= pool.NamespaceSets.Count)
            return $"invalid:{index}";
        ASNamespaceSet? value = pool.NamespaceSets[index];
        if (value is null)
            return $"{index}:null";
        return $"{index}:[{string.Join(",", value.NamespaceIndices.Select((namespace_index, position) =>
            $"{position}:{namespace_index}:{EncodingNamespaceIdentity(pool, namespace_index)}"))}]";
    }

    static string RuntimeNamespaceIdentity(
        ASConstantPool pool,
        int index,
        SymbolIdentityMode mode,
        int? private_ordinal = null)
    {
        if (!private_ordinal.HasValue &&
            runtime_identity_cache is not null)
        {
            return runtime_identity_cache.NamespaceIdentity(
                pool,
                index,
                mode);
        }
        if (index < 0 || index >= pool.Namespaces.Count)
            return "invalid";
        ASNamespace? value = pool.Namespaces[index];
        if (value is null)
            return "null";
        if (value.NameIndex < 0 ||
            value.NameIndex >= pool.Strings.Count)
        {
            return "invalid";
        }
        string uri = value.RuntimeName;
        uri = Token(mode == SymbolIdentityMode.Runtime
            ? uri
            : NormalizeSymbol(uri, true));
        if (value.Kind == NamespaceKind.Private)
        {
            int ordinal = private_ordinal ??
                PrivateNamespaceOrdinal(
                    pool,
                    index,
                    value);
            return $"private:{uri}:instance:{ordinal}";
        }
        return $"{RuntimeNamespaceKind(value.Kind)}:{uri}";
    }

    internal static string RuntimeNamespaceIdentity(
        ASConstantPool pool,
        int index,
        int private_ordinal) =>
        RuntimeNamespaceIdentity(
            pool,
            index,
            SymbolIdentityMode.Runtime,
            private_ordinal);

    static string RuntimeNamespaceSetIdentity(
        ASConstantPool pool,
        int index,
        SymbolIdentityMode mode)
    {
        if (index < 0 || index >= pool.NamespaceSets.Count)
            return "invalid";
        ASNamespaceSet? value = pool.NamespaceSets[index];
        if (value is null)
            return "null";
        return $"[{string.Join(",", value.NamespaceIndices.Select((namespace_index, position) =>
            $"{position}:{RuntimeNamespaceIdentity(pool, namespace_index, mode)}"))}]";
    }

    static int PrivateNamespaceOrdinal(
        ASConstantPool pool,
        int index,
        ASNamespace value)
    {
        string uri = value.NameIndex >= 0 &&
            value.NameIndex < pool.Strings.Count
                ? value.RuntimeName
                : "";
        int ordinal = 0;
        for (int current = 0; current < index && current < pool.Namespaces.Count; current++)
        {
            ASNamespace? candidate = pool.Namespaces[current];
            if (candidate is null || candidate.Kind != NamespaceKind.Private)
                continue;
            string candidate_uri =
                candidate.NameIndex >= 0 && candidate.NameIndex < pool.Strings.Count
                    ? candidate.RuntimeName
                    : "";
            if (string.Equals(candidate_uri, uri, StringComparison.Ordinal))
                ordinal++;
        }
        return ordinal;
    }

    static string RuntimeNamespaceKind(NamespaceKind kind) => kind switch
    {
        NamespaceKind.Namespace or NamespaceKind.Package => "public",
        NamespaceKind.Protected => "protected",
        NamespaceKind.PackageInternal => "package-internal",
        NamespaceKind.Explicit => "explicit",
        NamespaceKind.StaticProtected => "static-protected",
        NamespaceKind.Private => "private",
        _ => $"invalid-{(byte)kind:x2}"
    };

    static string Token(string value) => $"{value.Length}:{value}";

    internal static string NormalizeSymbol(string value, bool preserve_readable)
    {
        if (value.Length == 0)
            return "?";
        var result = new StringBuilder(value.Length);
        int segment_start = 0;
        for (int index = 0; index <= value.Length; index++)
        {
            if (index < value.Length && value[index] is not '.' and not ':' and not '<' and not '>' and not ',')
                continue;
            string segment = value[segment_start..index];
            result.Append(preserve_readable && IsReadable(segment) ? segment : "_");
            if (index < value.Length)
                result.Append(value[index]);
            segment_start = index + 1;
        }
        return result.ToString();
    }

    static bool IsReadable(string value)
    {
        if (value.Length < 2 || value.StartsWith("_-", StringComparison.Ordinal) || value.Contains('§'))
            return false;
        return value.All(character => char.IsLetterOrDigit(character) || character is '_' or '$');
    }

    static string MethodTarget(int index, ASMethod method)
    {
        string name = SafeValue(() => method.Name ?? "", "");
        return name.Length == 0
            ? $"method#{index}"
            : $"{name}#{index}";
    }

    static string MethodIdentity(
        ASMethod method,
        bool semantic,
        IdentityContext context)
    {
        if (context.MethodCache.TryGetValue(method, out string? cached))
            return cached;
        (string structural, string semantic_identity) =
            ComputeMethodIdentities(method);
        string identity = semantic
            ? semantic_identity
            : structural;
        context.MethodCache[method] = identity;
        return identity;
    }

    static (string Structural, string Semantic) ComputeMethodIdentities(
        ASMethod method)
    {
        List<InstructionNode>? nodes = null;
        GraphResult? graph = null;
        ASMethodBody? body = method.Body;
        if (body is not null)
        {
            try
            {
                ASCode code = body.ParseCode();
                nodes = ReadInstructions(code, body.Code.Length);
                IReadOnlyList<Avm2ExceptionNormalization> exceptions =
                    Avm2ExceptionNormalizer.Normalize(body, code);
                var diagnostics = new List<string>();
                graph = BuildGraph(
                    body,
                    nodes,
                    exceptions,
                    diagnostics);
            }
            catch
            {
                nodes = null;
                graph = null;
            }
        }
        return (
            ComputeMethodIdentity(method, false, body, nodes, graph),
            ComputeMethodIdentity(method, true, body, nodes, graph));
    }

    static string ComputeMethodIdentity(
        ASMethod method,
        bool semantic,
        ASMethodBody? body,
        List<InstructionNode>? nodes,
        GraphResult? graph)
    {
        var context = new IdentityContext { TargetDepth = 1 };
        var canonical = new StringBuilder(ShallowMethodIdentity(method, semantic));
        canonical.Append("|definition:");
        if (body is null)
        {
            AppendSignature(canonical, method, semantic);
            canonical.Append("|body:none");
        }
        else if (nodes is not null && graph is not null)
        {
            AppendMethodFingerprint(
                canonical,
                method,
                nodes,
                graph,
                semantic,
                context);
        }
        else
        {
            AppendSignature(canonical, method, semantic);
            canonical.Append("|frame:")
                .Append(body.MaxStack)
                .Append(':')
                .Append(body.LocalCount)
                .Append(':')
                .Append(body.InitialScopeDepth)
                .Append(':')
                .Append(body.MaxScopeDepth);
            AppendTraitIdentities(
                canonical,
                "activation",
                body.Traits,
                semantic,
                context);
            canonical.Append("|undecoded:").Append(ByteHash(body.Code ?? []));
        }
        return Hash(canonical);
    }

    static MethodIdentityStamp MethodStamp(ASMethod method)
    {
        var shape = new StringBuilder();
        shape.Append((int)method.Flags)
            .Append(':')
            .Append(method.NameIndex)
            .Append(':')
            .Append(method.ReturnTypeIndex)
            .Append(':')
            .Append(method.Parameters.Count);
        foreach (ASParameter parameter in method.Parameters)
        {
            shape.Append('|')
                .Append(parameter.TypeIndex);
            if (method.Flags.HasFlag(
                MethodFlags.HasParamNames))
            {
                shape.Append(':')
                    .Append(parameter.NameIndex);
            }
            shape.Append(':')
                .Append(parameter.IsOptional ? '1' : '0');
            if (parameter.IsOptional)
            {
                shape.Append(':')
                    .Append(parameter.ValueIndex)
                    .Append(':')
                    .Append((int)parameter.ValueKind);
            }
        }
        ASMethodBody? body = method.Body;
        if (body is not null)
        {
            shape.Append("|frame:")
                .Append(body.MaxStack)
                .Append(':')
                .Append(body.LocalCount)
                .Append(':')
                .Append(body.InitialScopeDepth)
                .Append(':')
                .Append(body.MaxScopeDepth);
            foreach (ASException exception in body.Exceptions)
            {
                shape.Append("|exception:")
                    .Append(exception.From)
                    .Append(':')
                    .Append(exception.To)
                    .Append(':')
                    .Append(exception.Target)
                    .Append(':')
                    .Append(exception.ExceptionTypeIndex)
                    .Append(':')
                    .Append(exception.VariableNameIndex);
            }
            foreach (ASTrait trait in body.Traits)
            {
                shape.Append("|activation:")
                    .Append((int)trait.Kind)
                    .Append(':')
                    .Append((int)trait.Attributes)
                    .Append(':')
                    .Append(trait.Id)
                    .Append(':')
                    .Append(trait.QNameIndex);
                switch (trait.Kind)
                {
                    case TraitKind.Slot:
                    case TraitKind.Constant:
                        shape.Append(':')
                            .Append(trait.TypeIndex)
                            .Append(':')
                            .Append(trait.ValueIndex);
                        if (trait.ValueIndex != 0)
                        {
                            shape.Append(':')
                                .Append((int)trait.ValueKind);
                        }
                        break;
                    case TraitKind.Method:
                    case TraitKind.Getter:
                    case TraitKind.Setter:
                        shape.Append(':')
                            .Append(trait.MethodIndex);
                        break;
                    case TraitKind.Function:
                        shape.Append(':')
                            .Append(trait.FunctionIndex);
                        break;
                    case TraitKind.Class:
                        shape.Append(':')
                            .Append(trait.ClassIndex);
                        break;
                }
                if (trait.Attributes.HasFlag(
                    TraitAttributes.Metadata))
                {
                    shape.Append(":metadata:")
                        .Append(trait.MetadataIndices.Count);
                    foreach (int metadata_index in
                        trait.MetadataIndices)
                    {
                        shape.Append(':')
                            .Append(metadata_index);
                    }
                }
            }
        }
        ABCFile abc = method.ABC;
        return new MethodIdentityStamp(
            body is null ? null : ByteHash(body.Code ?? []),
            shape.ToString(),
            abc.Pool.Strings.Count,
            abc.Pool.Namespaces.Count,
            abc.Pool.NamespaceSets.Count,
            abc.Pool.Multinames.Count,
            abc.Methods.Count,
            abc.Classes.Count);
    }

    static string ShallowMethodIdentity(ASMethod method, bool semantic)
    {
        var canonical = new StringBuilder();
        canonical.Append("method-info|");
        AppendSignature(canonical, method, semantic);
        return canonical.ToString();
    }

    static string ResolvedMethodTarget(int index, Func<ASMethod> resolve)
    {
        try
        {
            return MethodTarget(index, resolve());
        }
        catch
        {
            return $"invalid-method:{index}";
        }
    }

    static string ResolvedMethodIdentity(
        int index,
        Func<ASMethod?> resolve,
        bool semantic,
        IdentityContext context)
    {
        try
        {
            ASMethod? method = resolve();
            if (method is null)
                return "invalid-method";
            return context.TargetDepth > 0
                ? $"method:{Hash(new StringBuilder(ShallowMethodIdentity(method, semantic)))}"
                : MethodIdentity(method, semantic, context);
        }
        catch
        {
            return "invalid-method";
        }
    }

    static string ResolvedClassTarget(NewClassIns instruction)
    {
        try
        {
            return Qualified(instruction.Class.Instance.QName);
        }
        catch
        {
            return $"invalid-class:{instruction.ClassIndex}";
        }
    }

    static string ResolvedClassIdentity(
        NewClassIns instruction,
        bool semantic,
        IdentityContext context)
    {
        try
        {
            ASClass? value = instruction.Class;
            return value is null
                ? "invalid-class"
                : ClassTargetIdentity(value, semantic, context);
        }
        catch
        {
            return "invalid-class";
        }
    }

    static string ClassIdentity(
        ASClass value,
        bool semantic,
        IdentityContext context)
    {
        if (context.ClassCache.TryGetValue(value, out string? cached))
            return cached;
        string shallow = ShallowClassIdentity(value, semantic);
        if (!context.Classes.Add(value))
            return $"class-cycle:{Hash(new StringBuilder(shallow))}";
        context.TargetDepth++;
        try
        {
            ASInstance instance = value.Instance;
            var canonical = new StringBuilder(shallow);
            canonical.Append("|flags:")
                .Append((int)instance.Flags);
            if (instance.Flags.HasFlag(ClassFlags.ProtectedNamespace))
            {
                canonical.Append("|protected:")
                    .Append(SafeValue(
                        () => RuntimeNamespaceIdentity(
                            instance.ABC.Pool,
                            instance.ProtectedNamespaceIndex,
                            semantic
                                ? SymbolIdentityMode.Runtime
                                : SymbolIdentityMode.Normalized),
                        "invalid"));
            }
            foreach (int interface_index in instance.InterfaceIndices)
            {
                canonical.Append("|interface:")
                    .Append(interface_index >= 0 &&
                        interface_index < instance.ABC.Pool.Multinames.Count
                            ? SymbolIdentity(
                                instance.ABC.Pool.Multinames[interface_index],
                                semantic)
                            : "invalid");
            }
            canonical.Append("|instance-constructor:")
                .Append(ResolvedMethodIdentity(
                    instance.ConstructorIndex,
                    () => instance.Constructor,
                    semantic,
                    context));
            AppendTraitIdentities(
                canonical,
                "instance",
                instance.Traits,
                semantic,
                context);
            canonical.Append("|static-constructor:")
                .Append(ResolvedMethodIdentity(
                    value.ConstructorIndex,
                    () => value.Constructor,
                    semantic,
                    context));
            AppendTraitIdentities(
                canonical,
                "static",
                value.Traits,
                semantic,
                context);
            string identity = Hash(canonical);
            context.ClassCache[value] = identity;
            return identity;
        }
        catch
        {
            return $"invalid-class:{Hash(new StringBuilder(shallow))}";
        }
        finally
        {
            context.TargetDepth--;
            context.Classes.Remove(value);
        }
    }

    static string ClassTargetIdentity(
        ASClass value,
        bool semantic,
        IdentityContext context) =>
        context.TargetDepth > 0
            ? $"class:{Hash(new StringBuilder(ShallowClassIdentity(value, semantic)))}"
            : ClassIdentity(value, semantic, context);

    static string ShallowClassIdentity(ASClass value, bool semantic)
    {
        var canonical = new StringBuilder();
        canonical.Append("name:")
            .Append(SafeValue(
                () => SymbolIdentity(value.Instance.QName, semantic),
                "invalid"))
            .Append("|super:")
            .Append(SafeValue(
                () => SymbolIdentity(value.Instance.Super, semantic),
                "invalid"));
        return canonical.ToString();
    }

    static void AppendTraitIdentities(
        StringBuilder canonical,
        string scope,
        IEnumerable<ASTrait> traits,
        bool semantic,
        IdentityContext context)
    {
        foreach (ASTrait trait in traits)
        {
            canonical.Append('|')
                .Append(scope)
                .Append(':')
                .Append((int)trait.Kind)
                .Append(':')
                .Append((int)trait.Attributes)
                .Append(':')
                .Append(trait.Id)
                .Append(':')
                .Append(SafeValue(
                    () => SymbolIdentity(trait.QName, semantic),
                    $"invalid-symbol:{trait.QNameIndex}"));
            if (trait.Kind is TraitKind.Slot or TraitKind.Constant)
            {
                canonical.Append(":type:")
                    .Append(SafeValue(
                        () => SymbolIdentity(
                            trait.Type,
                            semantic),
                        $"invalid-type:{trait.TypeIndex}"))
                    .Append(":value-kind:");
                if (semantic)
                {
                    canonical.Append("effective:value:")
                        .Append(SafeValue(
                            () => ConstantValueIdentity(
                                trait.Value,
                                trait.Type),
                            "invalid"));
                }
                else
                {
                    if (trait.ValueIndex == 0)
                        canonical.Append("absent");
                    else
                        canonical.Append((int)trait.ValueKind);
                    canonical.Append(":value:")
                        .Append(trait.ValueIndex == 0
                            ? "none"
                            : "present");
                }
            }
            else if (trait.Kind is TraitKind.Method or TraitKind.Getter or TraitKind.Setter)
            {
                canonical.Append(":method:")
                    .Append(ResolvedMethodIdentity(
                        trait.MethodIndex,
                        () => trait.Method,
                        semantic,
                        context));
            }
            else if (trait.Kind == TraitKind.Function)
            {
                canonical.Append(":function:")
                    .Append(ResolvedMethodIdentity(
                        trait.FunctionIndex,
                        () => trait.Function,
                        semantic,
                        context));
            }
            else if (trait.Kind == TraitKind.Class)
            {
                canonical.Append(":class:")
                    .Append(SafeValue(
                        () => trait.Class is ASClass value
                            ? ClassTargetIdentity(value, semantic, context)
                            : "invalid-class",
                        "invalid-class"));
            }
        }
    }

    static T SafeValue<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch
        {
            return fallback;
        }
    }

    internal static string ConstantValueIdentity(
        object? value,
        ASMultiname? declared_type = null)
    {
        string? builtin =
            ASConstantPool.GetPublicBuiltinName(
                declared_type);
        if (builtin == "int")
        {
            int number = value switch
            {
                int item => item,
                uint item => checked((int)item),
                double item => checked((int)item),
                _ => throw new InvalidDataException(
                    "The AVM2 int default does not contain a numeric atom.")
            };
            return $"int:{number.ToString(CultureInfo.InvariantCulture)}";
        }
        if (builtin == "uint")
        {
            uint number = value switch
            {
                int item => checked((uint)item),
                uint item => item,
                double item => checked((uint)item),
                _ => throw new InvalidDataException(
                    "The AVM2 uint default does not contain a numeric atom.")
            };
            return $"uint:{number.ToString(CultureInfo.InvariantCulture)}";
        }
        if (builtin == "Number")
        {
            double number = value switch
            {
                int item => item,
                uint item => item,
                double item => item,
                _ => throw new InvalidDataException(
                    "The AVM2 Number default does not contain a numeric atom.")
            };
            return NumberIdentity(number);
        }
        if (value is not ASNamespace ns)
        {
            return value switch
            {
                ASUndefined => "undefined",
                null => "null",
                string text => $"string:{Token(text)}",
                bool boolean => boolean
                    ? "boolean:true"
                    : "boolean:false",
                int number =>
                    $"integer:{number.ToString(CultureInfo.InvariantCulture)}",
                uint number =>
                    $"integer:{number.ToString(CultureInfo.InvariantCulture)}",
                double number => NumberIdentity(number),
                float number => FloatIdentity(number),
                ASFloat4 float4 =>
                    $"float4:{FloatIdentity(float4.X)}:{FloatIdentity(float4.Y)}:{FloatIdentity(float4.Z)}:{FloatIdentity(float4.W)}",
                IFormattable formattable =>
                    $"{value.GetType().FullName}:{formattable.ToString(null, CultureInfo.InvariantCulture)}",
                _ =>
                    $"{value.GetType().FullName}:{value}"
            };
        }
        int index = ReferenceIndex(ns.Pool.Namespaces, ns);
        return index < 0
            ? $"namespace:{(byte)ns.Kind:x2}:{Token(SafeValue(() => ns.RuntimeName, ""))}"
            : RuntimeNamespaceIdentity(ns.Pool, index, SymbolIdentityMode.Runtime);
    }

    static string NumberIdentity(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(
            value);
        return $"number:{unchecked((ulong)bits):x16}";
    }

    static string FloatIdentity(float value)
    {
        int bits = BitConverter.SingleToInt32Bits(
            value);
        return $"float:{unchecked((uint)bits):x8}";
    }

    static int ReferenceIndex<T>(IReadOnlyList<T?> values, T target)
        where T : class
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (ReferenceEquals(values[index], target))
                return index;
        }
        return -1;
    }

    static string ByteHash(ReadOnlySpan<byte> data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    internal static string LiteralText(object? value) =>
        ASLiteralFormatter.Format(value);

    static string LiteralKind(object? value) => value switch
    {
        ASUndefined => "UndefinedLiteral",
        null => "NullLiteral",
        string => "StringLiteral",
        bool => "BooleanLiteral",
        byte or sbyte or short or ushort or int or uint or long or ulong => "IntegerLiteral",
        float => "FloatLiteral",
        ASFloat4 => "Float4Literal",
        double or decimal => "NumberLiteral",
        _ => "Literal"
    };

    static string Float4Text(ASFloat4 value) =>
        $"float4({FloatText(value.X)}, {FloatText(value.Y)}, {FloatText(value.Z)}, {FloatText(value.W)})";

    static string FloatText(float value) =>
        ASLiteralFormatter.FormatFloat(value);

    static string Hash(StringBuilder canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
}
