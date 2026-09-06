using System.Collections.ObjectModel;
using System.Globalization;
using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

public enum Avm2DeclaringScopeStatus
{
    Proven,
    MissingCreator,
    AmbiguousCreator,
    RecursiveDependency,
    InvalidCreator,
    UnsupportedBinding
}

public sealed class Avm2DeclaringScopeResolution
{
    public required Avm2DeclaringScopeStatus Status { get; init; }
    public Avm2DataFlowScopeContext? Context { get; init; }
    public required IReadOnlyList<string> Provenance { get; init; }
    public bool Proven =>
        Status == Avm2DeclaringScopeStatus.Proven &&
        Context is not null;
}

public sealed class Avm2DeclaringScopeDiagnostic
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public int? AbcIndex { get; init; }
    public int? MethodIndex { get; init; }
    public int? Instruction { get; init; }
}

public sealed class Avm2DeclaringScopeIndex
{
    sealed record CreatorSite(
        int AbcIndex,
        int BodyIndex,
        ASMethodBody Body,
        int Instruction,
        int? ClassIndex,
        ASMethod? Function);

    sealed record SourceContext(
        ASMethod Method,
        Avm2MethodBinding? Binding,
        Avm2DataFlowScopeContext Context,
        string Provenance);

    sealed record ScriptTraitSite(
        int AbcIndex,
        int ScriptIndex,
        int TraitIndex,
        ASTrait Trait);

    readonly record struct ClassKey(int AbcIndex, int ClassIndex, bool Instance);

    enum CreatorNodeKind
    {
        Function,
        ClassStatic,
        ClassInstance
    }

    enum CreatorSiteState
    {
        Reachable,
        Unreachable,
        Invalid
    }

    enum CreatorValueKind
    {
        Unresolved,
        Concrete,
        Missing,
        Invalid,
        Ambiguous,
        Recursive
    }

    readonly record struct CreatorNode(
        CreatorNodeKind Kind,
        int AbcIndex,
        int Index);

    sealed record CreatorSource(
        ASMethod Method,
        Avm2MethodBinding? Binding,
        CreatorNode? Dependency,
        Avm2DataFlowScopeContext? Seed,
        string Provenance,
        bool Invalid);

    sealed class CreatorValue
    {
        public CreatorValueKind Kind { get; set; }
        public Avm2DataFlowScopeContext? Context { get; set; }
        public HashSet<string> Provenance { get; } =
            new(StringComparer.Ordinal);
    }

    sealed class FlowOperationIndex
    {
        public required Avm2DataFlowOperation?[] Operations { get; init; }
        public required bool[] Ambiguous { get; init; }
    }

    sealed class MethodInstructionIndex
    {
        public required Avm2InstructionInventory?[] Instructions { get; init; }
        public required bool[] Ambiguous { get; init; }
    }

    readonly record struct FlowKey(
        ASMethodBody Body,
        ASMethod Method,
        string Binding,
        string Context);

    readonly record struct ScopeTypeProof(
        Avm2VerifierType Verifier,
        string? ExactRuntime);

    readonly object sync = new();
    readonly IReadOnlyList<ABCFile> abcs;
    readonly IReadOnlyDictionary<int, ABCFile> abcs_by_index;
    readonly Avm2VerifierTypeRegistry verifier_types;
    readonly Avm2MethodBindingIndex method_bindings;
    readonly Dictionary<ABCFile, int> abc_indices =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASMethod, int> method_abc_indices =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASMethod, int> method_indices =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASMethodBody, int> body_abc_indices =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<string, Avm2DeclaringScopeResolution> binding_resolutions =
        new(StringComparer.Ordinal);
    readonly Dictionary<ASMethod, Avm2DeclaringScopeResolution> method_resolutions =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASMethod, Avm2DeclaringScopeResolution> function_resolutions =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ClassKey, Avm2DeclaringScopeResolution> class_resolutions = [];
    readonly Dictionary<ASMethod, List<CreatorSite>> function_creators =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<(int AbcIndex, int ClassIndex), List<CreatorSite>>
        class_creators = [];
    readonly Dictionary<CreatorNode, IReadOnlyList<CreatorSite>>
        creator_sites = [];
    readonly Dictionary<CreatorSite, CreatorSiteState> creator_states = [];
    readonly Dictionary<ASMethod, IReadOnlyList<CreatorSource>>
        creator_sources =
            new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASMethodBody, Avm2MethodAnalysis> method_analyses =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASMethodBody, Avm2VerifierValidation>
        verifier_validations =
            new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<FlowKey, Avm2DataFlowAnalysis> flows = [];
    readonly Dictionary<Avm2DataFlowAnalysis, FlowOperationIndex>
        flow_operation_indices =
            new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASMethodBody, MethodInstructionIndex>
        method_instruction_indices =
            new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<string, List<ScriptTraitSite>>
        script_traits = new(StringComparer.Ordinal);
    readonly HashSet<string> active_bindings = new(StringComparer.Ordinal);
    readonly HashSet<ASMethod> active_methods =
        new(ReferenceEqualityComparer.Instance);
    readonly List<Avm2DeclaringScopeDiagnostic> diagnostics = [];
    readonly HashSet<string> diagnostic_keys = new(StringComparer.Ordinal);
    readonly HashSet<int> incomplete_creator_abcs = [];
    bool creators_indexed;

    Avm2DeclaringScopeIndex(
        IReadOnlyList<ABCFile> abcs,
        Avm2MethodBindingIndex method_bindings)
    {
        this.abcs = abcs;
        this.method_bindings = method_bindings;
        verifier_types =
            Avm2VerifierTypeRegistry.Create(
                method_bindings.AbcsByIndex);
        abcs_by_index = method_bindings.AbcsByIndex;
        foreach ((int abc_index, ABCFile abc) in abcs_by_index
            .OrderBy(value => value.Key)
            .Select(value => (value.Key, value.Value)))
        {
            abc_indices.Add(abc, abc_index);
            for (int method_index = 0;
                method_index < abc.Methods.Count;
                method_index++)
            {
                ASMethod method = abc.Methods[method_index];
                method_abc_indices.Add(method, abc_index);
                method_indices.Add(method, method_index);
            }
            foreach (ASMethodBody body in abc.MethodBodies)
                body_abc_indices.Add(body, abc_index);
            for (int script_index = 0;
                script_index < abc.Scripts.Count;
                script_index++)
            {
                ASScript script = abc.Scripts[script_index];
                for (int trait_index = 0;
                    trait_index < script.Traits.Count;
                    trait_index++)
                {
                    ASTrait trait = script.Traits[trait_index];
                    string symbol = RuntimeSymbolKey(trait.QName);
                    if (symbol.Length == 0)
                        continue;
                    if (!script_traits.TryGetValue(
                            symbol,
                            out List<ScriptTraitSite>? traits))
                    {
                        traits = [];
                        script_traits.Add(symbol, traits);
                    }
                    traits.Add(new ScriptTraitSite(
                        abc_index,
                        script_index,
                        trait_index,
                        trait));
                }
            }
        }
    }

    public Avm2MethodBindingIndex MethodBindings => method_bindings;

    public IReadOnlyList<Avm2DeclaringScopeDiagnostic> Diagnostics
    {
        get
        {
            lock (sync)
                return Array.AsReadOnly(diagnostics.ToArray());
        }
    }

    public static Avm2DeclaringScopeIndex Create(
        IReadOnlyList<ABCFile> abcs)
    {
        ArgumentNullException.ThrowIfNull(abcs);
        ABCFile[] files = abcs.ToArray();
        for (int index = 0; index < files.Length; index++)
            ArgumentNullException.ThrowIfNull(files[index]);
        IReadOnlyList<ABCFile> immutable = Array.AsReadOnly(files);
        return new Avm2DeclaringScopeIndex(
            immutable,
            Avm2MethodBindingIndex.Create(immutable));
    }

    public static Avm2DeclaringScopeIndex Create(
        ABCFile abc)
    {
        ArgumentNullException.ThrowIfNull(abc);
        Avm2MethodBindingIndex bindings =
            Avm2MethodBindingIndex.Create(abc);
        return new Avm2DeclaringScopeIndex(
            bindings.Abcs,
            bindings);
    }

    public static Avm2DeclaringScopeIndex Create(
        Avm2MethodBindingIndex method_bindings)
    {
        ArgumentNullException.ThrowIfNull(method_bindings);
        return new Avm2DeclaringScopeIndex(
            method_bindings.Abcs,
            method_bindings);
    }

    public Avm2DeclaringScopeResolution Resolve(
        Avm2MethodBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        lock (sync)
        {
            ValidateBinding(binding);
            return ResolveBinding(binding);
        }
    }

    public Avm2DeclaringScopeResolution Resolve(ASMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        lock (sync)
        {
            ValidateMethod(method);
            return ResolveMethod(method);
        }
    }

    public bool TryGetContext(
        Avm2MethodBinding binding,
        out Avm2DataFlowScopeContext context)
    {
        Avm2DeclaringScopeResolution resolution = Resolve(binding);
        context = resolution.Context!;
        return resolution.Proven;
    }

    public bool TryGetContext(
        ASMethod method,
        out Avm2DataFlowScopeContext context)
    {
        Avm2DeclaringScopeResolution resolution = Resolve(method);
        context = resolution.Context!;
        return resolution.Proven;
    }

    public Avm2DataFlowAnalysis Analyze(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2MethodBinding? binding = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(method_analysis);
        ValidateMethodAnalysis(
            body,
            method_analysis);
        Avm2DataFlowScopeContext? context;
        Avm2MethodBinding? analysis_binding;
        lock (sync)
        {
            ValidateAnalysisInput(body, binding);
            context = ResolveContext(
                body.Method,
                binding);
            analysis_binding = binding ??
                UniqueAnalysisBinding(body.Method);
        }
        Avm2DataFlowAnalysis flow =
            Avm2DataFlowAnalyzer.Analyze(
            body,
            method_analysis,
            analysis_binding,
            null,
            context,
            verifier_types);
        ValidateTargetExtraScope(
            body,
            context,
            flow);
        return flow;
    }

    internal Avm2DataFlowAnalysis Analyze(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(method_analysis);
        ValidateMethodAnalysis(
            body,
            method_analysis);
        Avm2DataFlowScopeContext? context;
        Avm2MethodBinding? analysis_binding;
        lock (sync)
        {
            ValidateAnalysisInput(body, binding);
            context = ResolveContext(
                body.Method,
                binding);
            analysis_binding = binding ??
                UniqueAnalysisBinding(body.Method);
        }
        Avm2DataFlowAnalysis flow =
            Avm2DataFlowAnalyzer.Analyze(
            body,
            method_analysis,
            analysis_binding,
            exact_receiver,
            context,
            verifier_types);
        ValidateTargetExtraScope(
            body,
            context,
            flow);
        return flow;
    }

    internal Avm2DataFlowAnalysis Analyze(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis,
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver,
        Avm2DataFlowScopeContext scope_context)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(method_analysis);
        ArgumentNullException.ThrowIfNull(scope_context);
        ValidateMethodAnalysis(
            body,
            method_analysis);
        Avm2MethodBinding? analysis_binding;
        lock (sync)
        {
            ValidateAnalysisInput(body, binding);
            analysis_binding = binding ??
                UniqueAnalysisBinding(body.Method);
        }
        Avm2DataFlowAnalysis flow =
            Avm2DataFlowAnalyzer.Analyze(
            body,
            method_analysis,
            analysis_binding,
            exact_receiver,
            scope_context,
            verifier_types);
        ValidateTargetExtraScope(
            body,
            scope_context,
            flow);
        return flow;
    }

    Avm2MethodBinding? UniqueAnalysisBinding(
        ASMethod method)
    {
        Avm2MethodBinding[] bindings = method_bindings
            .GetBindings(method)
            .Where(value =>
                value.Resolved &&
                value.Role !=
                    Avm2MethodBindingRole.FunctionTrait)
            .ToArray();
        return bindings.Length == 1 &&
            !HasFunctionCreator(method)
                ? bindings[0]
                : null;
    }

    static void ValidateMethodAnalysis(
        ASMethodBody body,
        Avm2MethodAnalysis method_analysis)
    {
        if (!method_analysis.MatchesSource(body))
        {
            throw new ArgumentException(
                "The method analysis does not reference the analyzed method body.",
                nameof(method_analysis));
        }
    }

    void ValidateTargetExtraScope(
        ASMethodBody body,
        Avm2DataFlowScopeContext? context,
        Avm2DataFlowAnalysis flow)
    {
        if (context is null ||
            !context.HasExtraVerifierType)
        {
            return;
        }
        if (!body_abc_indices.ContainsKey(body))
        {
            throw new ArgumentException(
                "The method body does not belong to the indexed ABC corpus.",
                nameof(body));
        }
        Avm2VerifierType required =
            context.ExtraVerifierType;
        if (required.Kind ==
            Avm2VerifierTypeKind.Unknown)
        {
            throw new InvalidDataException(
                "The declaring scope has an unknown extra verifier type.");
        }
        IReadOnlyDictionary<string, Avm2DataFlowValue> values =
            flow.Values.ToDictionary(
                value => value.Id,
                StringComparer.Ordinal);
        foreach (Avm2DataFlowOperation operation in
            flow.Operations.Where(value =>
                !value.Unreachable &&
                value.Opcode is
                    nameof(OPCode.PushScope) or
                    nameof(OPCode.PushWith) &&
                value.ScopeBefore.Count ==
                    context.CapturedScopeSize))
        {
            if (operation.Inputs.Count != 1 ||
                operation.ScopeAfter.Count !=
                    context.FullScopeSize ||
                !values.TryGetValue(
                    operation.Inputs[0],
                    out Avm2DataFlowValue? input) ||
                !VerifierAssignable(
                    required,
                    input.VerifierType))
            {
                throw new InvalidDataException(
                    $"First local scope push does not satisfy {VerifierTypeText(required)}.");
            }
        }
    }

    Avm2DataFlowScopeContext? ResolveContext(
        ASMethod method,
        Avm2MethodBinding? binding)
    {
        Avm2DeclaringScopeResolution resolution = binding is null
            ? ResolveMethod(method)
            : ResolveBinding(binding);
        return resolution.Proven ? resolution.Context : null;
    }

    Avm2DeclaringScopeResolution ResolveBinding(
        Avm2MethodBinding binding)
    {
        if (binding_resolutions.TryGetValue(
                binding.Identity,
                out Avm2DeclaringScopeResolution? cached))
        {
            return cached;
        }
        if (!active_bindings.Add(binding.Identity))
        {
            return Unknown(
                Avm2DeclaringScopeStatus.RecursiveDependency,
                binding.Identity);
        }

        Avm2DeclaringScopeResolution resolution;
        try
        {
            resolution = binding.Role ==
                Avm2MethodBindingRole.FunctionTrait
                ? Unknown(
                    Avm2DeclaringScopeStatus.UnsupportedBinding,
                    binding.Identity)
                : binding.Scope switch
            {
                Avm2MethodBindingScope.Script => Proven(
                    ScriptContext(binding),
                    binding.Identity),
                Avm2MethodBindingScope.ClassStatic => ResolveClass(
                    binding.AbcIndex,
                    binding.ContainerIndex,
                    false),
                Avm2MethodBindingScope.ClassInstance => ResolveClass(
                    binding.AbcIndex,
                    binding.ContainerIndex,
                    true),
                _ => Unknown(
                    Avm2DeclaringScopeStatus.UnsupportedBinding,
                    binding.Identity)
            };
        }
        finally
        {
            active_bindings.Remove(binding.Identity);
        }
        binding_resolutions[binding.Identity] = resolution;
        return resolution;
    }

    Avm2DeclaringScopeResolution ResolveMethod(ASMethod method)
    {
        if (method_resolutions.TryGetValue(
                method,
                out Avm2DeclaringScopeResolution? cached))
        {
            return cached;
        }
        if (!active_methods.Add(method))
            return Unknown(Avm2DeclaringScopeStatus.RecursiveDependency, "method");

        Avm2DeclaringScopeResolution resolution;
        try
        {
            IReadOnlyList<Avm2MethodBinding> bindings =
                method_bindings.GetBindings(method)
                    .Where(value => value.Resolved)
                    .ToArray();
            EnsureCreators();
            bool has_creator =
                HasFunctionCreator(method);
            var candidates =
                new List<Avm2DeclaringScopeResolution>();
            var provenance = new List<string>();
            foreach (Avm2MethodBinding binding in bindings)
            {
                candidates.Add(ResolveBinding(binding));
                provenance.Add(binding.Identity);
            }
            if (has_creator || bindings.Count == 0)
            {
                candidates.Add(ResolveFunction(method));
                provenance.Add("newfunction");
            }
            if (candidates.Count > 1)
            {
                resolution = Merge(
                    candidates,
                    provenance);
            }
            else
            {
                resolution = candidates[0];
            }
        }
        finally
        {
            active_methods.Remove(method);
        }
        method_resolutions[method] = resolution;
        return resolution;
    }

    Avm2DeclaringScopeResolution ResolveFunction(ASMethod method)
    {
        if (function_resolutions.TryGetValue(
                method,
                out Avm2DeclaringScopeResolution? cached))
        {
            return cached;
        }
        if (TryFunctionNode(
                method,
                out CreatorNode node))
        {
            EnsureCreatorResolution(node);
        }
        if (function_resolutions.TryGetValue(
                method,
                out cached))
        {
            return cached;
        }
        return Unknown(
            method_abc_indices.ContainsKey(method)
                ? Avm2DeclaringScopeStatus.MissingCreator
                : Avm2DeclaringScopeStatus.InvalidCreator,
            "newfunction");
    }

    Avm2DeclaringScopeResolution ResolveClass(
        int abc_index,
        int class_index,
        bool instance)
    {
        var key = new ClassKey(abc_index, class_index, instance);
        if (class_resolutions.TryGetValue(
                key,
                out Avm2DeclaringScopeResolution? cached))
        {
            return cached;
        }
        EnsureCreatorResolution(new CreatorNode(
            instance
                ? CreatorNodeKind.ClassInstance
                : CreatorNodeKind.ClassStatic,
            abc_index,
            class_index));
        if (class_resolutions.TryGetValue(
                key,
                out cached))
        {
            return cached;
        }
        return Unknown(
            ValidClass(abc_index, class_index)
                ? Avm2DeclaringScopeStatus.MissingCreator
                : Avm2DeclaringScopeStatus.InvalidCreator,
            ClassProvenance(key));
    }

    Avm2DeclaringScopeResolution? CaptureAt(
        CreatorSite site,
        bool instance,
        SourceContext source)
    {
        try
        {
            Avm2MethodAnalysis analysis = MethodAnalysis(site.Body);
            Avm2DataFlowAnalysis flow = Flow(source, site.Body, analysis);
            Avm2DataFlowOperation? operation =
                FlowOperation(
                    flow,
                    site.Instruction);
            if (operation?.Unreachable == true)
                return null;
            if (operation is null ||
                operation.Opcode is not (
                    nameof(OPCode.NewClass) or
                    nameof(OPCode.NewFunction)))
            {
                return Unknown(
                    Avm2DeclaringScopeStatus.InvalidCreator,
                    SiteProvenance(site));
            }
            Avm2VerifierValidation verifier =
                VerifierValidation(site.Body, analysis);
            if (!analysis.ControlFlow.Complete ||
                !flow.Complete ||
                !verifier.VerifierValid)
            {
                return Unknown(
                    Avm2DeclaringScopeStatus.InvalidCreator,
                    SiteProvenance(site));
            }
            ValidateExtraScope(
                site,
                source,
                flow,
                analysis.DecodedCode);
            Avm2DataFlowScopeContext context = CaptureContext(
                site,
                source,
                flow,
                operation);
            if (instance)
                context = InstanceContext(site, context);
            return Proven(
                context,
                SiteProvenance(site),
                source.Provenance);
        }
        catch (Exception exception)
        {
            AddDiagnostic(
                "creator-analysis",
                $"{SiteProvenance(site)}: {exception.GetType().Name}: {exception.Message}",
                site.AbcIndex,
                site.Body.MethodIndex,
                site.Instruction);
            return Unknown(
                Avm2DeclaringScopeStatus.InvalidCreator,
                SiteProvenance(site));
        }
    }

    void EnsureCreatorResolution(CreatorNode root)
    {
        if (TryCreatorResolution(
                root,
                out _))
        {
            return;
        }
        EnsureCreators();
        if (TryCreatorResolution(
                root,
                out _))
        {
            return;
        }

        var nodes = new HashSet<CreatorNode>();
        var sites_by_node =
            new Dictionary<CreatorNode, IReadOnlyList<CreatorSite>>();
        var sources_by_site =
            new Dictionary<CreatorSite, IReadOnlyList<CreatorSource>>();
        var dependencies =
            new Dictionary<CreatorNode, HashSet<CreatorNode>>();
        var resolved =
            new Dictionary<CreatorNode, CreatorValue>();
        var pending = new Queue<CreatorNode>();
        pending.Enqueue(root);
        while (pending.Count > 0)
        {
            CreatorNode node = pending.Dequeue();
            if (TryCreatorResolution(
                    node,
                    out Avm2DeclaringScopeResolution? cached))
            {
                resolved.TryAdd(
                    node,
                    CreatorValueFromResolution(cached));
                continue;
            }
            if (!nodes.Add(node))
                continue;

            IReadOnlyList<CreatorSite> sites =
                CreatorSites(node);
            sites_by_node.Add(node, sites);
            var node_dependencies =
                new HashSet<CreatorNode>();
            dependencies.Add(
                node,
                node_dependencies);
            foreach (CreatorSite site in sites)
            {
                if (CreatorState(site) !=
                    CreatorSiteState.Reachable)
                {
                    continue;
                }
                IReadOnlyList<CreatorSource> sources =
                    CreatorSources(site.Body.Method);
                sources_by_site[site] = sources;
                foreach (CreatorNode dependency in sources
                    .Where(value =>
                        value.Dependency is not null)
                    .Select(value =>
                        value.Dependency!.Value))
                {
                    if (TryCreatorResolution(
                            dependency,
                            out cached))
                    {
                        resolved.TryAdd(
                            dependency,
                            CreatorValueFromResolution(
                                cached));
                        continue;
                    }
                    node_dependencies.Add(dependency);
                    pending.Enqueue(dependency);
                }
            }
        }

        IReadOnlyList<IReadOnlyList<CreatorNode>> components =
            CreatorComponents(nodes, dependencies);
        IReadOnlyList<int> component_order =
            CreatorComponentOrder(
                components,
                dependencies);
        foreach (int component_index in component_order)
        {
            IReadOnlyList<CreatorNode> component =
                components[component_index];
            var component_set =
                component.ToHashSet();
            var local = component.ToDictionary(
                value => value,
                _ => new CreatorValue
                {
                    Kind = CreatorValueKind.Unresolved
                });
            int maximum_rounds = Math.Max(
                8,
                component.Count * 4 + 4);
            for (int round = 0;
                round < maximum_rounds;
                round++)
            {
                bool changed = false;
                foreach (CreatorNode node in component)
                {
                    CreatorValue candidate =
                        EvaluateCreatorNode(
                            node,
                            sites_by_node[node],
                            sources_by_site,
                            component_set,
                            local,
                            resolved);
                    changed |= JoinCreatorValue(
                        local[node],
                        candidate);
                }
                if (!changed)
                    break;
            }
            foreach (CreatorValue value in local.Values)
            {
                if (value.Kind ==
                    CreatorValueKind.Unresolved)
                {
                    value.Kind =
                        CreatorValueKind.Recursive;
                }
            }
            foreach (CreatorNode node in component)
            {
                CreatorValue value = local[node];
                resolved.Add(node, value);
                CacheCreatorResolution(
                    node,
                    value);
            }
        }
    }

    bool TryCreatorResolution(
        CreatorNode node,
        out Avm2DeclaringScopeResolution resolution)
    {
        if (node.Kind == CreatorNodeKind.Function)
        {
            ASMethod? method = FunctionMethod(node);
            if (method is not null &&
                function_resolutions.TryGetValue(
                    method,
                    out resolution!))
            {
                return true;
            }
            resolution = null!;
            return false;
        }
        return class_resolutions.TryGetValue(
            new ClassKey(
                node.AbcIndex,
                node.Index,
                node.Kind ==
                    CreatorNodeKind.ClassInstance),
            out resolution!);
    }

    void CacheCreatorResolution(
        CreatorNode node,
        CreatorValue value)
    {
        Avm2DeclaringScopeResolution resolution =
            CreatorResolution(node, value);
        if (node.Kind == CreatorNodeKind.Function)
        {
            ASMethod? method = FunctionMethod(node);
            if (method is not null)
                function_resolutions[method] = resolution;
            return;
        }
        class_resolutions[new ClassKey(
            node.AbcIndex,
            node.Index,
            node.Kind ==
                CreatorNodeKind.ClassInstance)] =
            resolution;
    }

    static CreatorValue CreatorValueFromResolution(
        Avm2DeclaringScopeResolution resolution)
    {
        var value = new CreatorValue
        {
            Kind = resolution.Status switch
            {
                Avm2DeclaringScopeStatus.Proven
                    when resolution.Context is not null =>
                    CreatorValueKind.Concrete,
                Avm2DeclaringScopeStatus.MissingCreator =>
                    CreatorValueKind.Missing,
                Avm2DeclaringScopeStatus.AmbiguousCreator =>
                    CreatorValueKind.Ambiguous,
                Avm2DeclaringScopeStatus.RecursiveDependency =>
                    CreatorValueKind.Recursive,
                _ => CreatorValueKind.Invalid
            },
            Context = resolution.Context
        };
        foreach (string provenance in resolution.Provenance)
            value.Provenance.Add(provenance);
        return value;
    }

    CreatorValue EvaluateCreatorNode(
        CreatorNode node,
        IReadOnlyList<CreatorSite> sites,
        IReadOnlyDictionary<CreatorSite,
            IReadOnlyList<CreatorSource>> sources_by_site,
        IReadOnlySet<CreatorNode> component,
        IReadOnlyDictionary<CreatorNode, CreatorValue> local,
        IReadOnlyDictionary<CreatorNode, CreatorValue> resolved)
    {
        var result = new CreatorValue
        {
            Kind = CreatorValueKind.Unresolved
        };
        result.Provenance.Add(CreatorNodeProvenance(node));
        if (incomplete_creator_abcs.Contains(node.AbcIndex))
        {
            result.Kind = CreatorValueKind.Invalid;
            result.Provenance.Add("creator-scan-incomplete");
            return result;
        }
        if (sites.Count == 0)
        {
            result.Kind = CreatorValueKind.Missing;
            return result;
        }

        var contexts = new List<Avm2DataFlowScopeContext>();
        bool pending = false;
        bool active = false;
        foreach (CreatorSite site in sites)
        {
            CreatorSiteState site_state =
                CreatorState(site);
            if (site_state == CreatorSiteState.Unreachable)
                continue;
            result.Provenance.Add(SiteProvenance(site));
            if (site_state == CreatorSiteState.Invalid)
            {
                result.Kind = CreatorValueKind.Invalid;
                return result;
            }
            if (!sources_by_site.TryGetValue(
                    site,
                    out IReadOnlyList<CreatorSource>? sources) ||
                sources.Count == 0)
            {
                continue;
            }
            active = true;
            foreach (CreatorSource source in sources)
            {
                result.Provenance.Add(source.Provenance);
                if (source.Invalid)
                {
                    result.Kind = CreatorValueKind.Invalid;
                    return result;
                }
                Avm2DataFlowScopeContext? context = source.Seed;
                if (source.Dependency is CreatorNode dependency)
                {
                    CreatorValue dependency_value =
                        component.Contains(dependency)
                            ? local[dependency]
                            : resolved[dependency];
                    if (dependency_value.Kind ==
                        CreatorValueKind.Unresolved)
                    {
                        pending = true;
                        continue;
                    }
                    if (dependency_value.Kind !=
                            CreatorValueKind.Concrete ||
                        dependency_value.Context is null)
                    {
                        result.Kind = CreatorValueKind.Invalid;
                        return result;
                    }
                    context = dependency_value.Context;
                    foreach (string provenance in
                        dependency_value.Provenance)
                    {
                        result.Provenance.Add(provenance);
                    }
                }
                if (context is null)
                {
                    result.Kind = CreatorValueKind.Invalid;
                    return result;
                }
                var source_context = new SourceContext(
                    source.Method,
                    source.Binding,
                    context,
                    source.Provenance);
                Avm2DeclaringScopeResolution? capture =
                    CaptureAt(
                        site,
                        node.Kind ==
                            CreatorNodeKind.ClassInstance,
                        source_context);
                if (capture is null)
                    continue;
                foreach (string provenance in capture.Provenance)
                    result.Provenance.Add(provenance);
                if (!capture.Proven)
                {
                    result.Kind = CreatorValueKind.Invalid;
                    return result;
                }
                contexts.Add(capture.Context!);
            }
        }

        if (contexts.Count == 0)
        {
            result.Kind = pending
                ? CreatorValueKind.Unresolved
                : CreatorValueKind.Missing;
            return result;
        }
        Avm2DataFlowScopeContext first = contexts[0];
        if (contexts.Skip(1).Any(value =>
                !Equivalent(first, value)))
        {
            result.Kind = CreatorValueKind.Ambiguous;
            return result;
        }
        result.Kind = CreatorValueKind.Concrete;
        result.Context = MergeContexts(contexts);
        if (!active && pending)
            result.Kind = CreatorValueKind.Unresolved;
        return result;
    }

    static bool JoinCreatorValue(
        CreatorValue current,
        CreatorValue candidate)
    {
        foreach (string provenance in candidate.Provenance)
            current.Provenance.Add(provenance);
        if (candidate.Kind == CreatorValueKind.Unresolved)
            return false;
        if (candidate.Kind == CreatorValueKind.Invalid)
        {
            if (current.Kind == CreatorValueKind.Invalid)
                return false;
            current.Kind = CreatorValueKind.Invalid;
            current.Context = null;
            return true;
        }
        if (current.Kind == CreatorValueKind.Invalid)
            return false;
        if (candidate.Kind == CreatorValueKind.Ambiguous)
        {
            if (current.Kind == CreatorValueKind.Ambiguous)
                return false;
            current.Kind = CreatorValueKind.Ambiguous;
            current.Context = null;
            return true;
        }
        if (current.Kind == CreatorValueKind.Ambiguous)
            return false;
        if (candidate.Kind == CreatorValueKind.Concrete)
        {
            if (current.Kind == CreatorValueKind.Concrete)
            {
                if (current.Context is not null &&
                    candidate.Context is not null &&
                    Equivalent(
                        current.Context,
                        candidate.Context))
                {
                    return false;
                }
                current.Kind = CreatorValueKind.Ambiguous;
                current.Context = null;
                return true;
            }
            current.Kind = CreatorValueKind.Concrete;
            current.Context = candidate.Context;
            return true;
        }
        if (current.Kind == candidate.Kind)
            return false;
        if (current.Kind == CreatorValueKind.Concrete)
            return false;
        current.Kind = candidate.Kind;
        current.Context = null;
        return true;
    }

    IReadOnlyList<CreatorSource> CreatorSources(
        ASMethod method)
    {
        if (creator_sources.TryGetValue(
                method,
                out IReadOnlyList<CreatorSource>? cached))
        {
            return cached;
        }
        var sources = new List<CreatorSource>();
        foreach (Avm2MethodBinding binding in method_bindings
            .GetBindings(method)
            .Where(value => value.Resolved)
            .OrderBy(value => value.Identity, StringComparer.Ordinal))
        {
            if (binding.Role ==
                Avm2MethodBindingRole.FunctionTrait)
            {
                sources.Add(new CreatorSource(
                    method,
                    binding,
                    null,
                    null,
                    binding.Identity,
                    true));
                continue;
            }
            switch (binding.Scope)
            {
                case Avm2MethodBindingScope.Script:
                    sources.Add(new CreatorSource(
                        method,
                        binding,
                        null,
                        ScriptContext(binding),
                        binding.Identity,
                        false));
                    break;
                case Avm2MethodBindingScope.ClassStatic:
                    sources.Add(new CreatorSource(
                        method,
                        binding,
                        new CreatorNode(
                            CreatorNodeKind.ClassStatic,
                            binding.AbcIndex,
                            binding.ContainerIndex),
                        null,
                        binding.Identity,
                        false));
                    break;
                case Avm2MethodBindingScope.ClassInstance:
                    sources.Add(new CreatorSource(
                        method,
                        binding,
                        new CreatorNode(
                            CreatorNodeKind.ClassInstance,
                            binding.AbcIndex,
                            binding.ContainerIndex),
                        null,
                        binding.Identity,
                        false));
                    break;
                default:
                    sources.Add(new CreatorSource(
                        method,
                        binding,
                        null,
                        null,
                        binding.Identity,
                        true));
                    break;
            }
        }
        if (HasFunctionCreator(method) &&
            TryFunctionNode(method, out CreatorNode function))
        {
            sources.Add(new CreatorSource(
                method,
                null,
                function,
                null,
                "newfunction",
                false));
        }
        IReadOnlyList<CreatorSource> result =
            Array.AsReadOnly(sources.ToArray());
        creator_sources.Add(method, result);
        return result;
    }

    CreatorSiteState CreatorState(CreatorSite site)
    {
        if (creator_states.TryGetValue(
                site,
                out CreatorSiteState cached))
        {
            return cached;
        }
        CreatorSiteState state;
        try
        {
            Avm2MethodAnalysis analysis =
                MethodAnalysis(site.Body);
            Avm2InstructionInventory? instruction =
                MethodInstruction(
                    site.Body,
                    analysis,
                    site.Instruction);
            if (instruction is null ||
                instruction.Block < 0 ||
                instruction.Block >=
                    analysis.ControlFlow.Blocks.Count)
            {
                state = CreatorSiteState.Invalid;
                creator_states.Add(site, state);
                return state;
            }
            bool expected = site.Function is not null
                ? instruction.Opcode ==
                    nameof(OPCode.NewFunction)
                : instruction.Opcode ==
                    nameof(OPCode.NewClass);
            if (!expected)
            {
                state = CreatorSiteState.Invalid;
                creator_states.Add(site, state);
                return state;
            }
            state = analysis.ControlFlow
                    .Blocks[instruction.Block]
                    .Reachable
                ? CreatorSiteState.Reachable
                : CreatorSiteState.Unreachable;
        }
        catch (Exception exception)
        {
            AddDiagnostic(
                "creator-analysis",
                $"{SiteProvenance(site)}: {exception.GetType().Name}: {exception.Message}",
                site.AbcIndex,
                site.Body.MethodIndex,
                site.Instruction);
            state = CreatorSiteState.Invalid;
        }
        creator_states.Add(site, state);
        return state;
    }

    bool HasFunctionCreator(ASMethod method) =>
        function_creators.TryGetValue(
            method,
            out List<CreatorSite>? sites) &&
        sites.Any(value =>
            CreatorState(value) !=
                CreatorSiteState.Unreachable);

    IReadOnlyList<CreatorSite> CreatorSites(
        CreatorNode node)
    {
        if (creator_sites.TryGetValue(
                node,
                out IReadOnlyList<CreatorSite>? cached))
        {
            return cached;
        }
        IEnumerable<CreatorSite> sites;
        if (node.Kind == CreatorNodeKind.Function)
        {
            ASMethod? method = FunctionMethod(node);
            sites = method is not null &&
                function_creators.TryGetValue(
                    method,
                    out List<CreatorSite>? functions)
                ? functions
                : [];
        }
        else
        {
            sites = class_creators.TryGetValue(
                    (node.AbcIndex, node.Index),
                    out List<CreatorSite>? classes)
                ? classes
                : [];
        }
        IReadOnlyList<CreatorSite> result = Array.AsReadOnly(
            sites
                .OrderBy(value => value.AbcIndex)
                .ThenBy(value => value.BodyIndex)
                .ThenBy(value => value.Instruction)
                .ToArray());
        creator_sites.Add(node, result);
        return result;
    }

    bool TryFunctionNode(
        ASMethod method,
        out CreatorNode node)
    {
        node = default;
        if (!method_abc_indices.TryGetValue(
                method,
                out int abc_index) ||
            !method_indices.TryGetValue(
                method,
                out int method_index) ||
            !abcs_by_index.TryGetValue(
                abc_index,
                out ABCFile? abc))
        {
            return false;
        }
        if (method_index < 0 ||
            method_index >= abc.Methods.Count ||
            !ReferenceEquals(
                abc.Methods[method_index],
                method))
        {
            return false;
        }
        node = new CreatorNode(
            CreatorNodeKind.Function,
            abc_index,
            method_index);
        return true;
    }

    ASMethod? FunctionMethod(CreatorNode node)
    {
        if (node.Kind != CreatorNodeKind.Function ||
            !abcs_by_index.TryGetValue(
                node.AbcIndex,
                out ABCFile? abc) ||
            node.Index < 0 ||
            node.Index >= abc.Methods.Count)
        {
            return null;
        }
        return abc.Methods[node.Index];
    }

    static IReadOnlyList<CreatorNode> OrderedNodes(
        IEnumerable<CreatorNode> nodes) =>
        nodes
            .OrderBy(value => value.AbcIndex)
            .ThenBy(value => value.Kind)
            .ThenBy(value => value.Index)
            .ToArray();

    static IReadOnlyList<IReadOnlyList<CreatorNode>>
        CreatorComponents(
            IReadOnlySet<CreatorNode> nodes,
            IReadOnlyDictionary<CreatorNode,
                HashSet<CreatorNode>> dependencies)
    {
        var visited = new HashSet<CreatorNode>();
        var finished = new List<CreatorNode>();
        foreach (CreatorNode root in OrderedNodes(nodes))
        {
            if (!visited.Add(root))
                continue;
            var stack =
                new Stack<(CreatorNode Node, bool Exit)>();
            stack.Push((root, false));
            while (stack.Count > 0)
            {
                (CreatorNode node, bool exit) =
                    stack.Pop();
                if (exit)
                {
                    finished.Add(node);
                    continue;
                }
                stack.Push((node, true));
                foreach (CreatorNode dependency in OrderedNodes(
                    dependencies[node]).Reverse())
                {
                    if (visited.Add(dependency))
                        stack.Push((dependency, false));
                }
            }
        }

        var reverse = nodes.ToDictionary(
            value => value,
            _ => new HashSet<CreatorNode>());
        foreach ((CreatorNode node,
            HashSet<CreatorNode> node_dependencies) in
            dependencies)
        {
            foreach (CreatorNode dependency in
                node_dependencies)
            {
                reverse[dependency].Add(node);
            }
        }
        visited.Clear();
        var components =
            new List<IReadOnlyList<CreatorNode>>();
        for (int index = finished.Count - 1;
            index >= 0;
            index--)
        {
            CreatorNode root = finished[index];
            if (!visited.Add(root))
                continue;
            var component = new List<CreatorNode>();
            var stack = new Stack<CreatorNode>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                CreatorNode node = stack.Pop();
                component.Add(node);
                foreach (CreatorNode dependent in OrderedNodes(
                    reverse[node]).Reverse())
                {
                    if (visited.Add(dependent))
                        stack.Push(dependent);
                }
            }
            components.Add(OrderedNodes(component));
        }
        return components;
    }

    static IReadOnlyList<int> CreatorComponentOrder(
        IReadOnlyList<IReadOnlyList<CreatorNode>> components,
        IReadOnlyDictionary<CreatorNode,
            HashSet<CreatorNode>> dependencies)
    {
        var component_by_node =
            new Dictionary<CreatorNode, int>();
        for (int index = 0;
            index < components.Count;
            index++)
        {
            foreach (CreatorNode node in components[index])
                component_by_node.Add(node, index);
        }
        var required = Enumerable.Range(
                0,
                components.Count)
            .ToDictionary(
                value => value,
                _ => new HashSet<int>());
        var dependents = Enumerable.Range(
                0,
                components.Count)
            .ToDictionary(
                value => value,
                _ => new HashSet<int>());
        foreach ((CreatorNode node,
            HashSet<CreatorNode> node_dependencies) in
            dependencies)
        {
            int target = component_by_node[node];
            foreach (CreatorNode dependency in
                node_dependencies)
            {
                int source =
                    component_by_node[dependency];
                if (source == target ||
                    !required[target].Add(source))
                {
                    continue;
                }
                dependents[source].Add(target);
            }
        }
        var comparer = Comparer<int>.Create(
            (left, right) =>
            {
                if (left == right)
                    return 0;
                CreatorNode left_node =
                    components[left][0];
                CreatorNode right_node =
                    components[right][0];
                int comparison = left_node.AbcIndex
                    .CompareTo(right_node.AbcIndex);
                if (comparison == 0)
                {
                    comparison = left_node.Kind
                        .CompareTo(right_node.Kind);
                }
                if (comparison == 0)
                {
                    comparison = left_node.Index
                        .CompareTo(right_node.Index);
                }
                return comparison != 0
                    ? comparison
                    : left.CompareTo(right);
            });
        var ready = new SortedSet<int>(comparer);
        foreach (int component in required.Keys)
        {
            if (required[component].Count == 0)
                ready.Add(component);
        }
        var order = new List<int>();
        while (ready.Count > 0)
        {
            int component = ready.Min;
            ready.Remove(component);
            order.Add(component);
            foreach (int dependent in
                dependents[component])
            {
                required[dependent].Remove(component);
                if (required[dependent].Count == 0)
                    ready.Add(dependent);
            }
        }
        if (order.Count != components.Count)
            throw new InvalidDataException(
                "The creator component graph is cyclic.");
        return order;
    }

    static Avm2DeclaringScopeResolution CreatorResolution(
        CreatorNode node,
        CreatorValue value)
    {
        string[] provenance = value.Provenance
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        return value.Kind switch
        {
            CreatorValueKind.Concrete
                when value.Context is not null =>
                Proven(value.Context, provenance),
            CreatorValueKind.Missing =>
                Unknown(
                    Avm2DeclaringScopeStatus.MissingCreator,
                    provenance),
            CreatorValueKind.Ambiguous =>
                Unknown(
                    Avm2DeclaringScopeStatus.AmbiguousCreator,
                    provenance),
            CreatorValueKind.Recursive =>
                Unknown(
                    Avm2DeclaringScopeStatus.RecursiveDependency,
                    provenance),
            _ => Unknown(
                Avm2DeclaringScopeStatus.InvalidCreator,
                provenance.Length == 0
                    ? [CreatorNodeProvenance(node)]
                    : provenance)
        };
    }

    static string CreatorNodeProvenance(
        CreatorNode node) =>
        node.Kind == CreatorNodeKind.Function
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"abc:{node.AbcIndex}:method:{node.Index}:newfunction")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"abc:{node.AbcIndex}:class:{node.Index}:{(node.Kind == CreatorNodeKind.ClassInstance ? "instance" : "static")}");

    Avm2DataFlowAnalysis Flow(
        SourceContext source,
        ASMethodBody body,
        Avm2MethodAnalysis analysis)
    {
        var key = new FlowKey(
            body,
            source.Method,
            source.Binding?.Identity ?? "-",
            ContextFingerprint(source.Context));
        if (flows.TryGetValue(key, out Avm2DataFlowAnalysis? cached))
            return cached;
        Avm2DataFlowAnalysis flow = Avm2DataFlowAnalyzer.Analyze(
            body,
            analysis,
            source.Binding,
            null,
            source.Context,
            verifier_types);
        flows.Add(key, flow);
        return flow;
    }

    Avm2DataFlowOperation? FlowOperation(
        Avm2DataFlowAnalysis flow,
        int instruction)
    {
        if (!flow_operation_indices.TryGetValue(
                flow,
                out FlowOperationIndex? index))
        {
            int length = flow.Operations
                .Where(value => value.Instruction >= 0)
                .Select(value => value.Instruction + 1)
                .DefaultIfEmpty()
                .Max();
            var operations =
                new Avm2DataFlowOperation?[length];
            var ambiguous = new bool[length];
            foreach (Avm2DataFlowOperation operation in
                flow.Operations.Where(value =>
                    value.Instruction >= 0))
            {
                int position = operation.Instruction;
                if (operations[position] is null)
                    operations[position] = operation;
                else
                    ambiguous[position] = true;
            }
            index = new FlowOperationIndex
            {
                Operations = operations,
                Ambiguous = ambiguous
            };
            flow_operation_indices.Add(flow, index);
        }
        if (instruction < 0 ||
            instruction >= index.Operations.Length)
        {
            return null;
        }
        if (index.Ambiguous[instruction])
        {
            throw new InvalidOperationException(
                "Sequence contains more than one matching element");
        }
        return index.Operations[instruction];
    }

    Avm2InstructionInventory? MethodInstruction(
        ASMethodBody body,
        Avm2MethodAnalysis analysis,
        int instruction)
    {
        if (!method_instruction_indices.TryGetValue(
                body,
                out MethodInstructionIndex? index))
        {
            int length = analysis.Instructions
                .Where(value => value.Index >= 0)
                .Select(value => value.Index + 1)
                .DefaultIfEmpty()
                .Max();
            var instructions =
                new Avm2InstructionInventory?[length];
            var ambiguous = new bool[length];
            foreach (Avm2InstructionInventory value in
                analysis.Instructions.Where(value =>
                    value.Index >= 0))
            {
                int position = value.Index;
                if (instructions[position] is null)
                    instructions[position] = value;
                else
                    ambiguous[position] = true;
            }
            index = new MethodInstructionIndex
            {
                Instructions = instructions,
                Ambiguous = ambiguous
            };
            method_instruction_indices.Add(body, index);
        }
        if (instruction < 0 ||
            instruction >= index.Instructions.Length)
        {
            return null;
        }
        if (index.Ambiguous[instruction])
        {
            throw new InvalidOperationException(
                "Sequence contains more than one matching element");
        }
        return index.Instructions[instruction];
    }

    Avm2MethodAnalysis MethodAnalysis(ASMethodBody body)
    {
        if (method_analyses.TryGetValue(
                body,
                out Avm2MethodAnalysis? cached))
        {
            return cached;
        }
        Avm2MethodAnalysis analysis = Avm2MethodAnalyzer.Analyze(body);
        method_analyses.Add(body, analysis);
        return analysis;
    }

    Avm2VerifierValidation VerifierValidation(
        ASMethodBody body,
        Avm2MethodAnalysis analysis)
    {
        if (verifier_validations.TryGetValue(
                body,
                out Avm2VerifierValidation? cached))
        {
            return cached;
        }
        Avm2VerifierValidation validation =
            Avm2VerifierValidator.Validate(body, analysis);
        verifier_validations.Add(body, validation);
        return validation;
    }

    void ValidateExtraScope(
        CreatorSite site,
        SourceContext source,
        Avm2DataFlowAnalysis flow,
        IReadOnlyList<ASInstruction> code)
    {
        if (!source.Context.HasExtraVerifierType)
            return;
        Avm2VerifierType required =
            source.Context.ExtraVerifierType;
        if (required.Kind == Avm2VerifierTypeKind.Unknown)
        {
            throw new InvalidDataException(
                $"{source.Provenance} has an unknown extra verifier type.");
        }
        IReadOnlyDictionary<string, Avm2DataFlowValue> values =
            flow.Values.ToDictionary(
                value => value.Id,
                StringComparer.Ordinal);
        foreach (Avm2DataFlowOperation operation in flow.Operations
            .Where(value =>
                !value.Unreachable &&
                value.Opcode is
                    nameof(OPCode.PushScope) or
                    nameof(OPCode.PushWith) &&
                value.ScopeBefore.Count ==
                    source.Context.CapturedScopeSize))
        {
            if (operation.Inputs.Count != 1)
            {
                throw new InvalidDataException(
                    $"{source.Provenance} has an invalid first local scope push.");
            }
            ScopeTypeProof? actual = ScopeType(
                site,
                source,
                flow,
                code,
                values,
                operation.Inputs[0],
                []);
            if (actual is null ||
                !VerifierAssignable(
                    required,
                    actual.Value.Verifier))
            {
                throw new InvalidDataException(
                    $"{source.Provenance} first local scope type {VerifierTypeText(actual?.Verifier ?? Avm2VerifierType.Unknown)} does not satisfy {VerifierTypeText(required)}.");
            }
        }
    }

    Avm2DataFlowScopeContext CaptureContext(
        CreatorSite site,
        SourceContext source,
        Avm2DataFlowAnalysis flow,
        Avm2DataFlowOperation operation)
    {
        Avm2DataFlowScopeContext captured =
            Avm2DataFlowScopeContext.Capture(flow, operation);
        Dictionary<string, Avm2DataFlowValue> values = flow.Values
            .ToDictionary(value => value.Id, StringComparer.Ordinal);
        Avm2MethodAnalysis analysis = MethodAnalysis(site.Body);
        IReadOnlyList<ASInstruction> code = analysis.DecodedCode;
        var result = new Avm2DataFlowScopeValue[
            captured.DeclaringScope.Count];
        for (int index = 0; index < result.Length; index++)
        {
            Avm2DataFlowScopeValue value = captured.DeclaringScope[index];
            string value_id = operation.ScopeBefore[index];
            ScopeTypeProof? type_proof = ScopeType(
                site,
                source,
                flow,
                code,
                values,
                value_id,
                []);
            if (type_proof is null ||
                type_proof.Value.Verifier.Kind ==
                    Avm2VerifierTypeKind.Unknown)
            {
                throw new InvalidDataException(
                    $"{SiteProvenance(site)} cannot prove the exact scope type for {value_id}.");
            }
            result[index] = new Avm2DataFlowScopeValue
            {
                Provenance = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{SiteProvenance(site)}:scope:{index}:{value.Provenance}"),
                TypeHint = value.TypeHint,
                VerifierType = type_proof.Value.Verifier,
                ExactRuntimeTypeIdentity =
                    type_proof.Value.ExactRuntime,
                Literal = value.Literal,
                IsWith = value.IsWith
            };
        }
        ValidateNewClassBase(
            site,
            source,
            flow,
            operation,
            code,
            values,
            result);
        return new Avm2DataFlowScopeContext
        {
            DeclaringScope = Array.AsReadOnly(result),
            HasExtraVerifierType = true,
            ExtraVerifierType = CreatorExtraVerifierType(site)
        };
    }

    void ValidateNewClassBase(
        CreatorSite site,
        SourceContext source,
        Avm2DataFlowAnalysis flow,
        Avm2DataFlowOperation operation,
        IReadOnlyList<ASInstruction> code,
        IReadOnlyDictionary<string, Avm2DataFlowValue> values,
        IReadOnlyList<Avm2DataFlowScopeValue> scope)
    {
        if (site.ClassIndex is not int class_index)
            return;
        if (!ValidClass(site.AbcIndex, class_index) ||
            scope.Count <= source.Context.DeclaringScope.Count)
        {
            throw new InvalidDataException(
                $"{SiteProvenance(site)} has no local base-class scope.");
        }
        ASInstance instance =
            abcs_by_index[site.AbcIndex].Instances[class_index];
        bool no_base = instance.SuperIndex == 0;
        if (no_base && !instance.IsInterface)
        {
            throw new InvalidDataException(
                $"{SiteProvenance(site)} non-interface class has no base class.");
        }
        string? expected_static = null;
        string? expected_instance = null;
        if (!no_base)
        {
            expected_static = ResolveClassName(
                site.AbcIndex,
                instance.Super,
                false);
            expected_instance = ResolveClassName(
                site.AbcIndex,
                instance.Super,
                true);
            if (expected_static is null ||
                expected_instance is null)
            {
                throw new InvalidDataException(
                    $"{SiteProvenance(site)} cannot resolve base class {RuntimeSymbolKey(instance.Super)}.");
            }
        }
        ScopeTypeProof? operand = operation.StackBefore.Count == 0
            ? null
            : ScopeType(
                site,
                source,
                flow,
                code,
                values,
                operation.StackBefore[^1],
                []);
        if (!ValidNewClassOperand(
                operand,
                no_base,
                expected_static))
        {
            throw new InvalidDataException(
                $"{SiteProvenance(site)} base-class operand is {ScopeTypeText(operand)}, expected {(no_base ? "null" : expected_static)}.");
        }
        Avm2DataFlowScopeValue local_scope = scope[^1];
        var scope_type = new ScopeTypeProof(
            local_scope.VerifierType,
            local_scope.ExactRuntimeTypeIdentity);
        bool projection_proven =
            TryInstanceTraitsProjection(
                scope_type,
                expected_static,
                expected_instance,
                out string? actual_projection);
        if (!projection_proven ||
            !string.Equals(
                expected_instance,
                actual_projection,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{SiteProvenance(site)} base-class scope is {ScopeTypeText(scope_type)} with instance-traits projection {(projection_proven ? actual_projection ?? "none" : "unknown")}, expected {expected_instance ?? "none"}.");
        }
    }

    static bool ValidNewClassOperand(
        ScopeTypeProof? operand,
        bool no_base,
        string? expected_static)
    {
        if (operand is not ScopeTypeProof actual)
            return false;
        if (no_base)
        {
            return actual.Verifier.Kind ==
                    Avm2VerifierTypeKind.Null &&
                string.Equals(
                    actual.ExactRuntime,
                    "builtin:null",
                    StringComparison.Ordinal);
        }
        return expected_static is not null &&
            actual.Verifier.Kind ==
                Avm2VerifierTypeKind.Known &&
            string.Equals(
                actual.Verifier.Identity,
                expected_static,
                StringComparison.Ordinal) &&
            string.Equals(
                actual.ExactRuntime,
                expected_static,
                StringComparison.Ordinal);
    }

    static bool TryInstanceTraitsProjection(
        ScopeTypeProof type,
        string? expected_static,
        string? expected_instance,
        out string? projection)
    {
        projection = null;
        if (type.Verifier.Kind !=
                Avm2VerifierTypeKind.Known ||
            type.ExactRuntime is not string exact ||
            !string.Equals(
                type.Verifier.Identity,
                exact,
                StringComparison.Ordinal))
        {
            return false;
        }
        if (expected_static is not null &&
            expected_instance is not null &&
            string.Equals(
                exact,
                expected_static,
                StringComparison.Ordinal))
        {
            projection = expected_instance;
            return true;
        }
        if (TryClassTypeIdentity(
                exact,
                out int abc_index,
                out int class_index,
                out bool instance))
        {
            projection = instance
                ? null
                : TypeIdentity(
                    abc_index,
                    class_index,
                    true);
            return true;
        }
        if (exact.StartsWith(
                "builtin-class:",
                StringComparison.Ordinal) ||
            exact.StartsWith(
                "external-class:",
                StringComparison.Ordinal) ||
            exact.StartsWith(
                "symbol:",
                StringComparison.Ordinal) &&
            exact.EndsWith(
                ":static",
                StringComparison.Ordinal))
        {
            projection = exact;
            return true;
        }
        if (exact is
            "builtin:class" or
            "builtin:null" or
            "builtin:void")
        {
            return false;
        }
        if (ScriptScopeIdentity(exact) ||
            exact.StartsWith(
                "external-type:",
                StringComparison.Ordinal) ||
            exact.StartsWith(
                "builtin:",
                StringComparison.Ordinal) ||
            exact.StartsWith(
                "symbol:",
                StringComparison.Ordinal) &&
            exact.EndsWith(
                ":instance",
                StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }

    static bool TryClassTypeIdentity(
        string identity,
        out int abc_index,
        out int class_index,
        out bool instance)
    {
        abc_index = -1;
        class_index = -1;
        instance = false;
        string[] parts = identity.Split(':');
        if (parts.Length != 5 ||
            parts[0] != "abc" ||
            parts[2] != "class" ||
            !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out abc_index) ||
            !int.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out class_index) ||
            parts[4] is not (
                "instance" or
                "static"))
        {
            return false;
        }
        instance = parts[4] == "instance";
        return true;
    }

    static string ScopeTypeText(
        ScopeTypeProof? type) =>
        type is ScopeTypeProof value
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{VerifierTypeText(value.Verifier)} exact {value.ExactRuntime ?? "unknown"}")
            : "unknown";

    ScopeTypeProof? ScopeType(
        CreatorSite site,
        SourceContext source,
        Avm2DataFlowAnalysis flow,
        IReadOnlyList<ASInstruction> code,
        IReadOnlyDictionary<string, Avm2DataFlowValue> values,
        string value_id,
        HashSet<string> active)
    {
        if (!active.Add(value_id))
            return null;
        try
        {
            if (value_id.StartsWith(
                    "v_declaring_scope_",
                    StringComparison.Ordinal) &&
                int.TryParse(
                    value_id.AsSpan("v_declaring_scope_".Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int declaring_index) &&
                declaring_index >= 0 &&
                declaring_index < source.Context.DeclaringScope.Count)
            {
                Avm2DataFlowScopeValue declaring =
                    source.Context.DeclaringScope[declaring_index];
                return new ScopeTypeProof(
                    declaring.VerifierType,
                    declaring.ExactRuntimeTypeIdentity);
            }
            if (value_id == "v_entry_local_0")
                return ReceiverType(site, source.Binding);
            if (!values.TryGetValue(value_id, out Avm2DataFlowValue? value))
                return null;
            if (value.VerifierType.Kind !=
                Avm2VerifierTypeKind.Unknown)
            {
                return new ScopeTypeProof(
                    value.VerifierType,
                    value.ExactRuntimeTypeIdentity);
            }
            if (value.Instruction is int instruction_index &&
                instruction_index >= 0 &&
                instruction_index < code.Count)
            {
                ScopeTypeProof? instruction_type =
                    code[instruction_index] is GetLexIns get_lex
                        ? GetLexType(
                            site,
                            source,
                            flow,
                            code,
                            values,
                            instruction_index,
                            get_lex,
                            active)
                        : InstructionType(
                            site,
                            code[instruction_index]);
                if (instruction_type is not null)
                    return instruction_type;
            }
            if (value.Kind != "Phi")
                return null;
            ScopeTypeProof?[] sources = value.Sources
                .Select(value_source => ScopeType(
                    site,
                    source,
                    flow,
                    code,
                    values,
                    value_source,
                    active))
                .ToArray();
            if (sources.Any(value => value is null))
                return null;
            ScopeTypeProof[] proven = sources
                .Select(value => value!.Value)
                .ToArray();
            Avm2VerifierType[] verifier_types = proven
                .Select(value => value.Verifier)
                .Distinct()
                .ToArray();
            if (verifier_types.Length != 1)
                return null;
            string?[] exact_types = proven
                .Select(value => value.ExactRuntime)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return new ScopeTypeProof(
                verifier_types[0],
                exact_types.Length == 1
                    ? exact_types[0]
                    : null);
        }
        finally
        {
            active.Remove(value_id);
        }
    }

    ScopeTypeProof? GetLexType(
        CreatorSite site,
        SourceContext source,
        Avm2DataFlowAnalysis flow,
        IReadOnlyList<ASInstruction> code,
        IReadOnlyDictionary<string, Avm2DataFlowValue> values,
        int instruction,
        GetLexIns get_lex,
        HashSet<string> active)
    {
        Avm2DataFlowOperation? operation =
            FlowOperation(
                flow,
                instruction);
        if (operation is null ||
            !flow.ScopeWithBefore.TryGetValue(
                instruction,
                out IReadOnlyList<bool?>? scope_with) ||
            scope_with.Count != operation.ScopeBefore.Count)
        {
            return null;
        }
        string property = RuntimeSymbolKey(get_lex.TypeName);
        if (property.Length == 0)
            return null;
        var searched = new List<string>();
        for (int index = operation.ScopeBefore.Count - 1;
            index >= 1;
            index--)
        {
            if (scope_with[index] != false)
                return null;
            ScopeTypeProof? scope_type = ScopeType(
                site,
                source,
                flow,
                code,
                values,
                operation.ScopeBefore[index],
                active);
            if (scope_type is null)
                return null;
            if (scope_type.Value.Verifier.Kind ==
                Avm2VerifierTypeKind.Any)
            {
                continue;
            }
            if (scope_type.Value.Verifier.Kind !=
                Avm2VerifierTypeKind.Known)
                return null;
            string scope_identity =
                scope_type.Value.Verifier.Identity!;
            if (ScriptScopeIdentity(scope_identity))
                return null;
            searched.Add(scope_identity);
            (bool found, ASTrait? trait) =
                FindScopeTrait(
                    scope_identity,
                    property);
            if (found)
            {
                ScopeTypeProof? trait_type =
                    trait is null
                        ? null
                        : TraitValueType(trait);
                if (trait_type is null)
                {
                    AddDiagnostic(
                        "lexical-type",
                        $"{SiteProvenance(site)} cannot derive {property} from {scope_identity}.",
                        site.AbcIndex,
                        site.Body.MethodIndex,
                        instruction);
                }
                return trait_type;
            }
        }
        if (operation.ScopeBefore.Count == 0)
        {
            if (!IsUniqueScriptInitializer(source))
                return null;
        }
        else
        {
            if (scope_with[0] != false)
                return null;
            ScopeTypeProof? root = ScopeType(
                site,
                source,
                flow,
                code,
                values,
                operation.ScopeBefore[0],
                active);
            if (root is null ||
                !IsExactScriptGlobal(root.Value))
            {
                return null;
            }
            searched.Add(root.Value.Verifier.Identity!);
        }
        ScriptTraitSite? domain =
            ResolveDomainTrait(site.AbcIndex, property);
        if (domain is not null)
            return TraitValueType(domain.Trait);
        string? builtin = ResolveClassName(
            site.AbcIndex,
            get_lex.TypeName,
            false);
        if (builtin is null)
        {
            AddDiagnostic(
                "lexical-type",
                $"{SiteProvenance(site)} cannot resolve {property} after scopes {string.Join(",", searched)}.",
                site.AbcIndex,
                site.Body.MethodIndex,
                instruction);
        }
        return builtin is null
            ? null
            : new ScopeTypeProof(
                Avm2VerifierType.Known(builtin),
                builtin);
    }

    bool IsUniqueScriptInitializer(
        SourceContext source)
    {
        if (source.Binding is not Avm2MethodBinding binding ||
            binding.Scope !=
                Avm2MethodBindingScope.Script ||
            binding.Role !=
                Avm2MethodBindingRole.ScriptInitializer)
        {
            return false;
        }
        Avm2MethodBinding[] bindings = method_bindings
            .GetBindings(source.Method)
            .Where(value => value.Resolved)
            .Take(2)
            .ToArray();
        return bindings.Length == 1 &&
            bindings[0].Identity == binding.Identity;
    }

    bool IsExactScriptGlobal(
        ScopeTypeProof scope)
    {
        if (scope.Verifier.Kind !=
                Avm2VerifierTypeKind.Known ||
            !string.Equals(
                scope.ExactRuntime,
                scope.Verifier.Identity,
                StringComparison.Ordinal))
        {
            return false;
        }
        string[] parts = scope.Verifier.Identity!.Split(':');
        if (parts.Length != 4 ||
            parts[0] != "abc" ||
            parts[2] != "script" ||
            !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int abc_index) ||
            !int.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int script_index) ||
            !abcs_by_index.TryGetValue(
                abc_index,
                out ABCFile? abc))
        {
            return false;
        }
        return script_index >= 0 &&
            script_index < abc.Scripts.Count;
    }

    (bool Found, ASTrait? Trait) FindScopeTrait(
        string type_identity,
        string property)
    {
        foreach (ASContainer container in
            ScopeContainers(type_identity))
        {
            ASTrait[] matches = container.Traits
                .Where(trait =>
                    RuntimeSymbolKey(trait.QName) ==
                        property)
                .ToArray();
            if (matches.Length == 0)
                continue;
            return (
                true,
                matches.Length == 1
                    ? matches[0]
                    : null);
        }
        return (false, null);
    }

    IReadOnlyList<ASContainer> ScopeContainers(
        string type_identity)
    {
        string[] parts = type_identity.Split(':');
        if (parts.Length == 4 &&
            parts[0] == "abc" &&
            int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int script_abc) &&
            parts[2] == "script" &&
            int.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int script_index) &&
            abcs_by_index.TryGetValue(
                script_abc,
                out ABCFile? script_file) &&
            script_index >= 0 &&
            script_index < script_file.Scripts.Count)
        {
            return
            [
                script_file.Scripts[script_index]
            ];
        }
        if (parts.Length == 5 &&
            parts[0] == "abc" &&
            int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int class_abc) &&
            parts[2] == "class" &&
            int.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int class_index) &&
            abcs_by_index.TryGetValue(
                class_abc,
                out ABCFile? class_file) &&
            ValidClassIn(class_file, class_index))
        {
            bool instance = parts[4] == "instance";
            if (!instance && parts[4] != "static")
                return [];
            var containers = new List<ASContainer>();
            var visited =
                new HashSet<(int AbcIndex, int ClassIndex)>();
            int current_abc = class_abc;
            int current_class = class_index;
            while (visited.Add(
                (current_abc, current_class)) &&
                ValidClass(current_abc, current_class))
            {
                ABCFile current_file =
                    abcs_by_index[current_abc];
                containers.Add(instance
                    ? current_file.Instances[current_class]
                    : current_file.Classes[current_class]);
                ASInstance current =
                    current_file.Instances[current_class];
                (int AbcIndex, int ClassIndex)? parent =
                    ResolveClassTarget(
                        current_abc,
                        current.Super);
                if (parent is null)
                    break;
                current_abc = parent.Value.AbcIndex;
                current_class = parent.Value.ClassIndex;
            }
            return containers.AsReadOnly();
        }
        return [];
    }

    ScopeTypeProof? TraitValueType(ASTrait trait)
    {
        if (!abc_indices.TryGetValue(
                trait.ABC,
                out int trait_abc))
        {
            return null;
        }
        if (trait.Kind == TraitKind.Getter)
        {
            if (trait.Method is not ASMethod method)
                return null;
            return DeclaredType(
                trait_abc,
                method.ReturnType);
        }
        if (trait.Kind is
            TraitKind.Slot or
            TraitKind.Constant)
        {
            return DeclaredType(
                trait_abc,
                trait.Type);
        }
        string? verifier = trait.Kind switch
        {
            TraitKind.Class when ValidClass(
                trait_abc,
                trait.ClassIndex) => TypeIdentity(
                    trait_abc,
                    trait.ClassIndex,
                    false),
            TraitKind.Method => "builtin:function",
            _ => null
        };
        if (verifier is null)
            return null;
        string? exact = trait.Kind == TraitKind.Class
            ? verifier
            : null;
        return new ScopeTypeProof(
            Avm2VerifierType.Known(verifier),
            exact);
    }

    ScopeTypeProof? DeclaredType(
        int requester_abc,
        ASMultiname? name)
    {
        if (name is null ||
            ReferenceEquals(
                name,
                abcs_by_index[requester_abc]
                    .Pool.Multinames.FirstOrDefault()))
        {
            return new ScopeTypeProof(
                Avm2VerifierType.Any,
                null);
        }
        string? verifier = ResolveClassName(
            requester_abc,
            name,
            true);
        return verifier is null
            ? null
            : new ScopeTypeProof(
                Avm2VerifierType.Known(verifier),
                null);
    }

    ScriptTraitSite? ResolveDomainTrait(
        int requester_abc,
        string property)
    {
        if (!script_traits.TryGetValue(
                property,
                out List<ScriptTraitSite>? candidates))
        {
            return null;
        }
        ScriptTraitSite[] loaded = candidates
            .Where(value =>
                value.AbcIndex <= requester_abc)
            .OrderBy(value => value.AbcIndex)
            .ThenBy(value => value.ScriptIndex)
            .ThenBy(value => value.TraitIndex)
            .ToArray();
        if (loaded.Length == 0)
            return null;
        ScriptTraitSite first = loaded[0];
        return loaded.Count(value =>
            value.AbcIndex == first.AbcIndex &&
            value.ScriptIndex == first.ScriptIndex) == 1
                ? first
                : null;
    }

    string? ResolveClassName(
        int requester_abc,
        ASMultiname? name,
        bool instance)
    {
        string symbol = RuntimeSymbolKey(name);
        if (symbol.Length == 0)
            return null;
        if (HasLoadedDomainDefinition(
                requester_abc,
                symbol))
        {
            ScriptTraitSite? definition =
                ResolveDomainTrait(
                    requester_abc,
                    symbol);
            if (definition is null ||
                definition.Trait.Kind !=
                    TraitKind.Class ||
                !ValidClass(
                    definition.AbcIndex,
                    definition.Trait.ClassIndex))
            {
                return null;
            }
            return TypeIdentity(
                definition.AbcIndex,
                definition.Trait.ClassIndex,
                instance);
        }
        string? identity =
            verifier_types.ResolveInstanceIdentity(
                name,
                abcs_by_index[requester_abc]);
        if (identity is null ||
            identity.StartsWith(
                "abc:",
                StringComparison.Ordinal))
        {
            return null;
        }
        if (instance)
            return identity;
        return identity.StartsWith(
                "external-type:",
                StringComparison.Ordinal)
            ? $"external-class:{symbol}"
            : $"builtin-class:{symbol}";
    }

    (int AbcIndex, int ClassIndex)? ResolveClassTarget(
        int requester_abc,
        ASMultiname? name)
    {
        string symbol = RuntimeSymbolKey(name);
        if (symbol.Length == 0 ||
            !HasLoadedDomainDefinition(
                requester_abc,
                symbol))
        {
            return null;
        }
        ScriptTraitSite? definition =
            ResolveDomainTrait(
                requester_abc,
                symbol);
        return definition is not null &&
            definition.Trait.Kind ==
                TraitKind.Class &&
            ValidClass(
                definition.AbcIndex,
                definition.Trait.ClassIndex)
                ? (
                    definition.AbcIndex,
                    definition.Trait.ClassIndex)
                : null;
    }

    bool HasLoadedDomainDefinition(
        int requester_abc,
        string symbol)
    {
        return script_traits.TryGetValue(
                symbol,
                out List<ScriptTraitSite>? candidates) &&
            candidates.Any(value =>
                value.AbcIndex <= requester_abc);
    }

    static bool ScriptScopeIdentity(
        string identity)
    {
        string[] parts = identity.Split(':');
        return parts.Length == 4 &&
            parts[0] == "abc" &&
            parts[2] == "script";
    }

    bool VerifierAssignable(
        Avm2VerifierType expected,
        Avm2VerifierType actual)
    {
        if (expected.Kind == Avm2VerifierTypeKind.Any)
            return actual.Kind != Avm2VerifierTypeKind.Unknown;
        if (expected.Kind != Avm2VerifierTypeKind.Known)
            return expected == actual;
        if (actual.Kind != Avm2VerifierTypeKind.Known)
            return false;
        if (expected.Identity == actual.Identity)
            return true;
        return verifier_types.IsAssignable(
            expected.Identity,
            actual.Identity);
    }

    ScopeTypeProof? InstructionType(
        CreatorSite site,
        ASInstruction instruction)
    {
        if (instruction is NewClassIns new_class &&
            ValidClass(site.AbcIndex, new_class.ClassIndex))
        {
            string identity = TypeIdentity(
                site.AbcIndex,
                new_class.ClassIndex,
                false);
            return new ScopeTypeProof(
                Avm2VerifierType.Known(identity),
                identity);
        }
        if (instruction.OP == OPCode.NewFunction)
        {
            return new ScopeTypeProof(
                Avm2VerifierType.Known("builtin:function"),
                "builtin:function");
        }
        string? known = instruction.OP switch
        {
            OPCode.PushString or OPCode.Convert_s or OPCode.Coerce_s =>
                "builtin:string",
            OPCode.PushTrue or OPCode.PushFalse or OPCode.Convert_b or
                OPCode.Coerce_b => "builtin:boolean",
            OPCode.PushInt or OPCode.PushByte or OPCode.PushShort or
                OPCode.Convert_i or OPCode.Coerce_i => "builtin:int",
            OPCode.PushUInt or OPCode.Convert_u or OPCode.Coerce_u =>
                "builtin:uint",
            OPCode.PushDouble or OPCode.PushNan or OPCode.Convert_d or
                OPCode.Coerce_d => "builtin:number",
            OPCode.PushNamespace => "builtin:namespace",
            OPCode.NewArray => "builtin:array",
            _ => null
        };
        if (known is not null)
        {
            return new ScopeTypeProof(
                Avm2VerifierType.Known(known),
                known);
        }
        return instruction.OP switch
        {
            OPCode.PushNull => new ScopeTypeProof(
                Avm2VerifierType.Null,
                "builtin:null"),
            OPCode.PushUndefined => new ScopeTypeProof(
                Avm2VerifierType.Void,
                "builtin:void"),
            OPCode.Coerce_a => new ScopeTypeProof(
                Avm2VerifierType.Any,
                null),
            _ => null
        };
    }

    ScopeTypeProof? ReceiverType(
        CreatorSite site,
        Avm2MethodBinding? binding)
    {
        if (binding is null)
        {
            return new ScopeTypeProof(
                Avm2VerifierType.Known("builtin:object"),
                null);
        }
        string? verifier = binding.Scope switch
        {
            Avm2MethodBindingScope.Script => string.Create(
                CultureInfo.InvariantCulture,
                $"abc:{binding.AbcIndex}:script:{binding.ContainerIndex}"),
            Avm2MethodBindingScope.ClassStatic => TypeIdentity(
                binding.AbcIndex,
                binding.ContainerIndex,
                false),
            Avm2MethodBindingScope.ClassInstance => TypeIdentity(
                binding.AbcIndex,
                binding.ContainerIndex,
                true),
            _ => null
        };
        return verifier is null
            ? null
            : new ScopeTypeProof(
                Avm2VerifierType.Known(verifier),
                binding.Scope == Avm2MethodBindingScope.Script
                    ? verifier
                    : null);
    }

    static Avm2DataFlowScopeContext ScriptContext(
        Avm2MethodBinding binding) =>
        new()
        {
            DeclaringScope =
                Array.Empty<Avm2DataFlowScopeValue>(),
            HasExtraVerifierType = true,
            ExtraVerifierType = Avm2VerifierType.Known(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"abc:{binding.AbcIndex}:script:{binding.ContainerIndex}"))
        };

    Avm2VerifierType CreatorExtraVerifierType(
        CreatorSite site)
    {
        if (site.ClassIndex is int class_index &&
            ValidClass(site.AbcIndex, class_index))
        {
            return Avm2VerifierType.Known(
                TypeIdentity(
                    site.AbcIndex,
                    class_index,
                    false));
        }
        if (site.Function is not null)
        {
            return Avm2VerifierType.Known(
                "builtin:object");
        }
        return Avm2VerifierType.Unknown;
    }

    Avm2DataFlowScopeContext InstanceContext(
        CreatorSite site,
        Avm2DataFlowScopeContext static_context)
    {
        if (site.ClassIndex is not int class_index ||
            !ValidClass(site.AbcIndex, class_index))
        {
            throw new InvalidDataException(
                $"{SiteProvenance(site)} has no valid class target.");
        }
        ABCFile abc = abcs_by_index[site.AbcIndex];
        string type_hint = SafeQualified(abc.Classes[class_index].QName);
        var values = new Avm2DataFlowScopeValue[
            static_context.DeclaringScope.Count + 1];
        for (int index = 0;
            index < static_context.DeclaringScope.Count;
            index++)
        {
            values[index] = static_context.DeclaringScope[index];
        }
        values[^1] = new Avm2DataFlowScopeValue
        {
            Provenance = string.Create(
                CultureInfo.InvariantCulture,
                $"{SiteProvenance(site)}:class-closure"),
            TypeHint = type_hint,
            VerifierType =
                static_context.ExtraVerifierType,
            ExactRuntimeTypeIdentity =
                static_context.ExtraVerifierType.Identity,
            IsWith = false
        };
        return new Avm2DataFlowScopeContext
        {
            DeclaringScope = Array.AsReadOnly(values),
            HasExtraVerifierType = true,
            ExtraVerifierType = Avm2VerifierType.Known(
                TypeIdentity(
                    site.AbcIndex,
                    class_index,
                    true))
        };
    }

    Avm2DeclaringScopeResolution Merge(
        IReadOnlyList<Avm2DeclaringScopeResolution> candidates,
        IReadOnlyList<string> provenance)
    {
        if (candidates.Count == 0)
        {
            return Unknown(
                Avm2DeclaringScopeStatus.MissingCreator,
                provenance.ToArray());
        }
        if (candidates.Any(value => !value.Proven))
        {
            return Unknown(
                Avm2DeclaringScopeStatus.InvalidCreator,
                provenance.ToArray());
        }
        Avm2DataFlowScopeContext first = candidates[0].Context!;
        if (candidates.Skip(1).Any(value =>
                !Equivalent(first, value.Context!)))
        {
            return Unknown(
                Avm2DeclaringScopeStatus.AmbiguousCreator,
                provenance.ToArray());
        }
        Avm2DataFlowScopeContext merged = MergeContexts(
            candidates.Select(value => value.Context!).ToArray());
        return Proven(
            merged,
            provenance
                .Concat(candidates.SelectMany(value => value.Provenance))
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    static bool Equivalent(
        Avm2DataFlowScopeContext left,
        Avm2DataFlowScopeContext right)
    {
        if (left.DeclaringScope.Count != right.DeclaringScope.Count ||
            left.HasExtraVerifierType !=
                right.HasExtraVerifierType ||
            left.ExtraVerifierType != right.ExtraVerifierType ||
            left.ExtraVerifierType.Kind ==
                Avm2VerifierTypeKind.Unknown)
        {
            return false;
        }
        for (int index = 0; index < left.DeclaringScope.Count; index++)
        {
            Avm2DataFlowScopeValue left_value = left.DeclaringScope[index];
            Avm2DataFlowScopeValue right_value = right.DeclaringScope[index];
            if (left_value.IsWith != right_value.IsWith)
                return false;
            if (left_value.VerifierType.Kind ==
                    Avm2VerifierTypeKind.Unknown ||
                right_value.VerifierType.Kind ==
                    Avm2VerifierTypeKind.Unknown ||
                left_value.VerifierType !=
                    right_value.VerifierType ||
                !string.Equals(
                    left_value.ExactRuntimeTypeIdentity,
                    right_value.ExactRuntimeTypeIdentity,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    left_value.TypeHint,
                    right_value.TypeHint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    left_value.Literal,
                    right_value.Literal,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    static Avm2DataFlowScopeContext MergeContexts(
        IReadOnlyList<Avm2DataFlowScopeContext> contexts)
    {
        if (contexts.Count == 1)
            return contexts[0];
        int count = contexts[0].DeclaringScope.Count;
        var values = new Avm2DataFlowScopeValue[count];
        for (int index = 0; index < count; index++)
        {
            Avm2DataFlowScopeValue[] sources = contexts
                .Select(context => context.DeclaringScope[index])
                .ToArray();
            string[] origins = sources
                .Select(value => value.Provenance)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string?[] hints = sources
                .Select(value => value.TypeHint)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string?[] literals = sources
                .Select(value => value.Literal)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            values[index] = new Avm2DataFlowScopeValue
            {
                Provenance = origins.Length == 1
                    ? origins[0]
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"merge:scope:{index}:{string.Join("|", origins)}"),
                TypeHint = hints.Length == 1 ? hints[0] : "*",
                VerifierType = sources[0].VerifierType,
                ExactRuntimeTypeIdentity = sources
                    .Select(value =>
                        value.ExactRuntimeTypeIdentity)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == 1
                        ? sources[0].ExactRuntimeTypeIdentity
                        : null,
                Literal = literals.Length == 1 ? literals[0] : null,
                IsWith = sources[0].IsWith
            };
        }
        return new Avm2DataFlowScopeContext
        {
            DeclaringScope = Array.AsReadOnly(values),
            HasExtraVerifierType =
                contexts[0].HasExtraVerifierType,
            ExtraVerifierType =
                contexts[0].ExtraVerifierType
        };
    }

    static Avm2DeclaringScopeResolution Proven(
        Avm2DataFlowScopeContext context,
        params string[] provenance) =>
        Proven(context, (IReadOnlyList<string>)provenance);

    static Avm2DeclaringScopeResolution Proven(
        Avm2DataFlowScopeContext context,
        IReadOnlyList<string> provenance) =>
        new()
        {
            Status = Avm2DeclaringScopeStatus.Proven,
            Context = context,
            Provenance = Array.AsReadOnly(
                provenance
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray())
        };

    static Avm2DeclaringScopeResolution Unknown(
        Avm2DeclaringScopeStatus status,
        params string[] provenance) =>
        new()
        {
            Status = status,
            Provenance = Array.AsReadOnly(
                provenance
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray())
        };

    void EnsureCreators()
    {
        if (creators_indexed)
            return;
        foreach ((int abc_index, ABCFile abc) in abcs_by_index
            .OrderBy(value => value.Key)
            .Select(value => (value.Key, value.Value)))
        {
            for (int body_index = 0;
                body_index < abc.MethodBodies.Count;
                body_index++)
            {
                ASMethodBody body = abc.MethodBodies[body_index];
                try
                {
                    IReadOnlyList<ASInstruction> code =
                        body.ParseCode().ToList();
                    for (int instruction_index = 0;
                        instruction_index < code.Count;
                        instruction_index++)
                    {
                        if (code[instruction_index] is NewClassIns new_class &&
                            ValidClass(abc_index, new_class.ClassIndex))
                        {
                            var site = new CreatorSite(
                                abc_index,
                                body_index,
                                body,
                                instruction_index,
                                new_class.ClassIndex,
                                null);
                            if (!class_creators.TryGetValue(
                                    (abc_index, new_class.ClassIndex),
                                    out List<CreatorSite>? sites))
                            {
                                sites = [];
                                class_creators.Add(
                                    (abc_index, new_class.ClassIndex),
                                    sites);
                            }
                            sites.Add(site);
                        }
                        else if (code[instruction_index] is
                            NewFunctionIns new_function &&
                            new_function.MethodIndex >= 0 &&
                            new_function.MethodIndex < abc.Methods.Count)
                        {
                            ASMethod function =
                                abc.Methods[new_function.MethodIndex];
                            var site = new CreatorSite(
                                abc_index,
                                body_index,
                                body,
                                instruction_index,
                                null,
                                function);
                            if (!function_creators.TryGetValue(
                                    function,
                                    out List<CreatorSite>? sites))
                            {
                                sites = [];
                                function_creators.Add(function, sites);
                            }
                            sites.Add(site);
                        }
                    }
                }
                catch (Exception exception)
                {
                    incomplete_creator_abcs.Add(abc_index);
                    AddDiagnostic(
                        "creator-decode",
                        $"abc:{abc_index}:body:{body_index}: {exception.GetType().Name}: {exception.Message}",
                        abc_index,
                        body.MethodIndex,
                        null);
                }
            }
        }
        creators_indexed = true;
    }

    void ValidateBinding(Avm2MethodBinding binding)
    {
        if (!abc_indices.TryGetValue(
                binding.Abc,
                out int abc_index) ||
            abc_index != binding.AbcIndex)
        {
            throw new ArgumentException(
                "The binding does not belong to this declaring-scope index.",
                nameof(binding));
        }
    }

    void ValidateAnalysisInput(
        ASMethodBody body,
        Avm2MethodBinding? binding)
    {
        if (!body_abc_indices.ContainsKey(body))
        {
            throw new ArgumentException(
                "The method body does not belong to this declaring-scope index.",
                nameof(body));
        }
        if (binding is null)
            return;
        ValidateBinding(binding);
        if (!ReferenceEquals(
                binding.Method,
                body.Method))
        {
            throw new ArgumentException(
                "The binding does not reference the analyzed method body.",
                nameof(binding));
        }
    }

    void ValidateMethod(ASMethod method)
    {
        if (!method_bindings.GetBindings(method).Any() &&
            !abcs.Any(abc => abc.Methods.Any(value =>
                ReferenceEquals(value, method))))
        {
            throw new ArgumentException(
                "The method does not belong to this declaring-scope index.",
                nameof(method));
        }
    }

    bool ValidClass(int abc_index, int class_index) =>
        abcs_by_index.TryGetValue(
            abc_index,
            out ABCFile? abc) &&
        ValidClassIn(abc, class_index);

    static bool ValidClassIn(ABCFile abc, int class_index) =>
        class_index >= 0 &&
        class_index < abc.Classes.Count &&
        class_index < abc.Instances.Count;

    static string ContextFingerprint(
        Avm2DataFlowScopeContext context) =>
        string.Join(
            "\u001f",
            context.DeclaringScope.Select(value => string.Join(
                "\u001e",
                value.Provenance,
                value.VerifierType.Kind,
                value.VerifierType.Identity ?? "",
                value.ExactRuntimeTypeIdentity ?? "",
                value.TypeHint ?? "",
                value.Literal ?? "",
                value.IsWith ? "with" : "scope")))
            + string.Create(
                CultureInfo.InvariantCulture,
                $"\u001dextra:{context.HasExtraVerifierType}:{context.ExtraVerifierType.Kind}:{context.ExtraVerifierType.Identity ?? ""}");

    static string TypeIdentity(
        int abc_index,
        int class_index,
        bool instance) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"abc:{abc_index}:class:{class_index}:{(instance ? "instance" : "static")}");

    static string ClassProvenance(ClassKey key) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"abc:{key.AbcIndex}:class:{key.ClassIndex}:{(key.Instance ? "instance" : "static")}");

    static string SiteProvenance(CreatorSite site) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"abc:{site.AbcIndex}:body:{site.BodyIndex}:method:{site.Body.MethodIndex}:instruction:{site.Instruction}");

    static string VerifierTypeText(
        Avm2VerifierType type) =>
        type.Kind == Avm2VerifierTypeKind.Known
            ? type.Identity ?? "unknown"
            : type.Kind.ToString();

    static string SafeQualified(ASMultiname? name)
    {
        try
        {
            return name is null
                ? "*"
                : Avm2MethodAnalyzer.Qualified(name);
        }
        catch
        {
            return "*";
        }
    }

    string RuntimeSymbolKey(ASMultiname? name)
    {
        try
        {
            if (name is null)
                return "";
            string identity =
                Avm2MethodAnalyzer.RuntimeSymbolIdentity(name);
            if (name.Kind is not (
                    MultinameKind.QName or
                    MultinameKind.QNameA) ||
                name.Namespace?.Kind != NamespaceKind.Private)
            {
                return identity;
            }
            if (name.Pool.ABC is not ABCFile abc)
                return "";
            return abc_indices.TryGetValue(
                abc,
                out int abc_index)
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{identity}|private-abc:{abc_index}")
                : "";
        }
        catch
        {
            return "";
        }
    }

    void AddDiagnostic(
        string code,
        string message,
        int? abc_index,
        int? method_index,
        int? instruction)
    {
        string key = string.Join(
            "|",
            code,
            abc_index?.ToString(CultureInfo.InvariantCulture) ?? "",
            method_index?.ToString(CultureInfo.InvariantCulture) ?? "",
            instruction?.ToString(CultureInfo.InvariantCulture) ?? "",
            message);
        if (!diagnostic_keys.Add(key))
            return;
        diagnostics.Add(new Avm2DeclaringScopeDiagnostic
        {
            Code = code,
            Message = message,
            AbcIndex = abc_index,
            MethodIndex = method_index,
            Instruction = instruction
        });
    }
}
