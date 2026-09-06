using System.Globalization;
using System.Text.Json.Serialization;
using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;
using Flazzy.ABC.AVM2.Instructions.Containers;

namespace Qx.Headers.Flash;

public sealed class Avm2ResolvedCall
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required bool Exhaustive { get; init; }
    public bool ControlFlowExhaustive { get; init; }
    public bool TargetExhaustive { get; init; }
    public bool Nullable { get; init; }
    public List<Avm2CallCondition> CallConditions { get; init; } = [];
    public required List<Avm2ResolvedCallTarget> Targets { get; init; }
    public List<Avm2CallTerminalOutcome> TerminalOutcomes { get; init; } = [];
    public required List<string> Diagnostics { get; init; }
}

public sealed class Avm2ResolvedCallTarget
{
    public required ASMethod Method { get; init; }
    [JsonIgnore]
    public Avm2MethodBinding? Binding { get; init; }
    [JsonIgnore]
    internal Avm2ExactReceiver? ExactReceiver { get; init; }
    [JsonIgnore]
    internal Avm2DataFlowScopeContext? ClosureScope { get; init; }
    [JsonIgnore]
    internal bool RequiresClosureScope { get; init; }
    public required string RuntimeType { get; init; }
    public int DefinitionAbc { get; init; } = -1;
    public required string SelectionKind { get; init; }
    public string? SelectorExpression { get; init; }
    public required List<Avm2CallCondition> Conditions { get; init; }
    public required List<Avm2CallTargetEvidence> Evidence { get; init; }
}

public sealed class Avm2CallCondition
{
    public required int Instruction { get; init; }
    public required int Offset { get; init; }
    public required string Edge { get; init; }
    public required string Expression { get; init; }
}

public sealed class Avm2CallTargetEvidence
{
    public required string Kind { get; init; }
    public required string Certainty { get; init; }
    public required string SourceClass { get; init; }
    public required string SourceMethod { get; init; }
    public int SourceAbc { get; init; } = -1;
    public int TargetAbc { get; init; } = -1;
    public required int Instruction { get; init; }
    public required int Offset { get; init; }
    public string? Symbol { get; init; }
}

public sealed class Avm2CallTerminalOutcome
{
    public required string Kind { get; init; }
    public string? Expression { get; init; }
    public required List<Avm2CallCondition> Conditions { get; init; }
    public required List<Avm2CallTargetEvidence> Evidence { get; init; }
}

internal readonly record struct Avm2TypeDefinition(
    ABCFile Abc,
    int AbcIndex,
    int ClassIndex,
    ASInstance Instance,
    ASClass Class,
    string Qualified,
    string RuntimeIdentity);

internal readonly record struct Avm2ResolvedValueType(
    ASInstance RuntimeType,
    bool Static);

internal sealed class Avm2ResolvedValueSet
{
    public required IReadOnlyList<Avm2ResolvedValueType> Types { get; init; }
    public required bool Exhaustive { get; init; }
}

public sealed class Avm2CallTargetResolver
{
    const int MaximumDepth = 24;
    const int MaximumValues = 4096;

    enum ReceiverKind
    {
        Instance,
        Static
    }

    enum LexicalDomainCertainty
    {
        Exact,
        Blocked
    }

    enum InterfaceBindingStatus
    {
        NotApplicable,
        Resolved,
        MissingImplementation,
        Invalid
    }

    enum PublicBindingStatus
    {
        Missing,
        Resolved,
        Invalid
    }

    sealed class TypeBinding
    {
        public required ABCFile Abc { get; init; }
        public required int AbcIndex { get; init; }
        public required int ClassIndex { get; init; }
        public required string Qualified { get; init; }
        public required string RuntimeIdentity { get; init; }
        public required bool PrivateNamespace { get; init; }
        public required ASInstance Instance { get; init; }
        public required ASClass Class { get; init; }
    }

    sealed class TraitBinding
    {
        public required int AbcIndex { get; init; }
        public required ASContainer Container { get; init; }
        public required ASTrait Trait { get; init; }
        public bool InterfaceBinding { get; init; }
        public ASTrait? InterfaceContract { get; init; }
    }

    readonly record struct InterfaceContractResolution(
        InterfaceBindingStatus Status,
        TraitBinding? Contract,
        IReadOnlyList<TraitBinding> Contracts);

    readonly record struct InterfaceMethodResolution(
        InterfaceBindingStatus Status,
        List<Avm2MethodBinding> Methods,
        ASTrait? Contract);

    readonly record struct InterfaceTraitResolution(
        InterfaceBindingStatus Status,
        TraitBinding? Trait);

    readonly record struct PublicBindingResolution(
        PublicBindingStatus Status,
        IReadOnlyList<TraitBinding> Traits);

    sealed class MethodContext
    {
        public required ASMethod Method { get; init; }
        public Avm2MethodBinding? Binding { get; init; }
        public Avm2ExactReceiver? ExactReceiver { get; init; }
        public required List<ASInstruction> Code { get; init; }
        public required Avm2MethodAnalysis Analysis { get; init; }
        public required Avm2DataFlowAnalysis Flow { get; init; }
        public required bool VerifierValid { get; init; }
        public required Dictionary<int, Avm2DataFlowOperation> Operations { get; init; }
        public required Dictionary<string, Avm2DataFlowValue> Values { get; init; }
        public required Dictionary<string, Avm2DataFlowOperation> Producers { get; init; }
        public required Dictionary<string, Avm2DataFlowPhi> Phis { get; init; }
        public required Dictionary<int, List<Avm2CallCondition>> Conditions { get; init; }
    }

    sealed class PointsTo
    {
        public required TypeBinding Binding { get; init; }
        public required ReceiverKind Receiver { get; init; }
        public required string SelectionKind { get; init; }
        public string? SelectorExpression { get; init; }
        public required List<Avm2CallCondition> Conditions { get; init; }
        public required List<Avm2CallTargetEvidence> Evidence { get; init; }
        public required bool Exhaustive { get; init; }
    }

    sealed class PointsToResult
    {
        public required List<PointsTo> Types { get; init; }
        public required List<Avm2CallTerminalOutcome> Outcomes { get; init; }
        public required bool ControlFlowExhaustive { get; init; }
        public required bool TargetExhaustive { get; init; }
        public bool Exhaustive => ControlFlowExhaustive && TargetExhaustive;
    }

    sealed class CallableResult
    {
        public required List<Avm2ResolvedCallTarget> Targets { get; init; }
        public required bool ControlFlowExhaustive { get; init; }
        public required bool TargetExhaustive { get; init; }
        public bool Exhaustive => ControlFlowExhaustive && TargetExhaustive;
    }

    readonly record struct CallSite(
        string Name,
        ASMultiname? Property,
        int ArgumentCount,
        string Receiver,
        IReadOnlyList<string> Arguments,
        string? Callable);

    readonly record struct MethodContextKey(
        ASMethod Method,
        Avm2MethodBinding? Binding,
        Avm2ExactReceiver? ExactReceiver = null);

    readonly record struct ResolvedValueKey(
        Avm2MethodBinding Binding,
        Avm2ExactReceiver? ExactReceiver,
        string Value);

    readonly record struct ExternalContextKey(
        ASMethod Method,
        Avm2MethodBinding? Binding,
        Avm2ExactReceiver? ExactReceiver,
        Avm2DataFlowAnalysis Flow);

    readonly record struct ScopedMethodContextKey(
        ASMethod Method,
        Avm2MethodBinding? Binding,
        Avm2ExactReceiver? ExactReceiver,
        Avm2DataFlowScopeContext Scope);

    sealed class ScopedMethodContextKeyComparer :
        IEqualityComparer<ScopedMethodContextKey>
    {
        public bool Equals(
            ScopedMethodContextKey left,
            ScopedMethodContextKey right) =>
            ReferenceEquals(left.Method, right.Method) &&
            ReferenceEquals(left.Binding, right.Binding) &&
            Equals(left.ExactReceiver, right.ExactReceiver) &&
            Avm2DataFlowAnalyzer.ScopeContextsEqual(
                left.Scope,
                right.Scope);

        public int GetHashCode(
            ScopedMethodContextKey key)
        {
            var hash = new HashCode();
            hash.Add(
                key.Method,
                ReferenceEqualityComparer.Instance);
            hash.Add(
                key.Binding,
                ReferenceEqualityComparer.Instance);
            hash.Add(key.ExactReceiver);
            AddScopeHash(
                ref hash,
                key.Scope);
            return hash.ToHashCode();
        }
    }

    readonly record struct TraitSourceIdentity(
        ASContainer Container,
        int QNameIndex,
        TraitKind Kind,
        ABCFile? SourceAbc,
        ASInstance? SourceInstance);

    readonly record struct RuntimeBindingIdentity(
        ASContainer Container,
        ASTrait Trait);

    readonly record struct PointsToIdentity(
        ABCFile Abc,
        ASInstance Instance,
        ReceiverKind Receiver,
        string SelectionKind,
        string? SelectorExpression,
        string Conditions);

    readonly record struct OutcomeIdentity(
        string Kind,
        string? Expression,
        string Conditions);

    readonly record struct TargetIdentity(
        ASMethod Method,
        string? BindingIdentity,
        Avm2ExactReceiver? ExactReceiver,
        string ClosureScope,
        int DefinitionAbc,
        string RuntimeType,
        string SelectionKind,
        string? SelectorExpression,
        string Conditions);

    readonly record struct ConditionIdentity(
        int Instruction,
        int Offset,
        string Edge,
        string Expression);

    readonly record struct EvidenceIdentity(
        string Kind,
        string Certainty,
        int SourceAbc,
        string SourceClass,
        string SourceMethod,
        int TargetAbc,
        int Instruction,
        int Offset,
        string? Symbol);

    readonly record struct ConstructorPrivateWriteKey(
        ASInstance Owner,
        ASTrait Trait,
        Avm2ExactReceiver Receiver);

    readonly record struct ClosedSlotValueSetKey(
        ASInstance Owner,
        ASInstance Runtime,
        ASTrait Trait,
        Avm2MethodBinding Writer,
        int Instruction);

    readonly record struct ClosedSlotInventoryKey(
        ASInstance Owner,
        ASTrait Trait,
        int SlotIndex);

    readonly record struct GlobalInstruction(
        ASMethod Method,
        int Instruction,
        ASInstruction Value);

    sealed class ClosedSlotWriteInventory
    {
        public required IReadOnlyList<GlobalInstruction>
            PropertyWrites { get; init; }
        public required IReadOnlyList<GlobalInstruction>
            SetSlotWrites { get; init; }
    }

    sealed class EffectiveSlotLayout
    {
        public required Dictionary<int, ASTrait> Slots { get; init; }
        public required int HighestSlot { get; init; }
    }

    readonly Dictionary<string, List<TypeBinding>> types = new(StringComparer.Ordinal);
    readonly List<TypeBinding> all_types = [];
    readonly Dictionary<string, TypeBinding[]> qualified_types =
        new(StringComparer.Ordinal);
    readonly Dictionary<string, TypeBinding[]> static_name_types =
        new(StringComparer.Ordinal);
    readonly Dictionary<ASInstance, TypeBinding> types_by_instance =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASNamespace, string>
        runtime_namespace_identities =
            new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASMultiname, string>
        runtime_symbol_identities =
            new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<string, List<TraitBinding>> traits = new(StringComparer.Ordinal);
    readonly Dictionary<ABCFile, Dictionary<string, List<TraitBinding>>>
        exact_private_traits = new(
            ReferenceEqualityComparer.Instance);
    readonly Avm2DeclaringScopeIndex declaring_scopes;
    readonly Avm2MethodBindingIndex method_bindings;
    readonly bool harman_method_aliases;
    readonly Dictionary<ABCFile, int> abc_indices =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<MethodContextKey, MethodContext?> contexts = [];
    readonly Dictionary<ResolvedValueKey, Avm2ResolvedValueSet>
        resolved_value_types = [];
    readonly Dictionary<ExternalContextKey, MethodContext>
        external_contexts = [];
    readonly Dictionary<ScopedMethodContextKey, MethodContext?>
        scoped_contexts = new(
            new ScopedMethodContextKeyComparer());
    readonly Dictionary<MethodContext, PointsToResult>
        returns = new(
            ReferenceEqualityComparer.Instance);
    readonly HashSet<MethodContext>
        active_returns = new(
            ReferenceEqualityComparer.Instance);
    readonly Dictionary<(ASContainer Container, bool Inherit), EffectiveSlotLayout?>
        slot_layouts = [];
    readonly HashSet<(ASContainer Container, bool Inherit)> active_slot_layouts = [];
    readonly Dictionary<ConstructorPrivateWriteKey, PointsToResult?>
        constructor_private_writes = [];
    readonly HashSet<ConstructorPrivateWriteKey> active_constructor_private_writes = [];
    readonly Dictionary<ClosedSlotValueSetKey, bool>
        closed_slot_value_sets = [];
    readonly Dictionary<ClosedSlotInventoryKey, ClosedSlotWriteInventory>
        closed_slot_inventories = [];
    readonly Dictionary<ASMethod, bool>
        reachable_alternate_method_references =
            new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<ASInstance, TypeBinding?> parents =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<(TypeBinding Candidate, TypeBinding Target), bool>
        strict_subtypes = [];
    List<GlobalInstruction>? global_instructions;
    bool global_instructions_complete;
    int constructor_private_write_suppression;

    public Avm2CallTargetResolver(
        IEnumerable<ABCFile> abc_files,
        bool harman_method_aliases = false)
        : this(
            CreateDeclaringScopes(abc_files),
            harman_method_aliases)
    {
    }

    internal Avm2CallTargetResolver(
        Avm2DeclaringScopeIndex declaring_scopes,
        bool harman_method_aliases = false)
    {
        this.declaring_scopes = declaring_scopes ??
            throw new ArgumentNullException(nameof(declaring_scopes));
        this.harman_method_aliases = harman_method_aliases;
        method_bindings = declaring_scopes.MethodBindings;
        var qualified_lookup = new Dictionary<string, List<TypeBinding>>(
            StringComparer.Ordinal);
        var static_names = new Dictionary<string, List<TypeBinding>>(
            StringComparer.Ordinal);
        IReadOnlyList<ABCFile> files = method_bindings.Abcs;
        for (int abc_index = 0; abc_index < files.Count; abc_index++)
        {
            ABCFile abc = files[abc_index];
            abc_indices.TryAdd(abc, abc_index);
            IndexRuntimeNamespaces(abc.Pool);
            int type_count = Math.Min(abc.Instances.Count, abc.Classes.Count);
            for (int index = 0; index < type_count; index++)
            {
                ASInstance instance = abc.Instances[index];
                string qualified = Qualified(instance.QName);
                if (qualified.Length == 0)
                    continue;
                var binding = new TypeBinding
                {
                    Abc = abc,
                    AbcIndex = abc_index,
                    ClassIndex = index,
                    Qualified = qualified,
                    RuntimeIdentity = RuntimeSymbolIdentity(instance.QName),
                    PrivateNamespace = IsPrivate(instance.QName),
                    Instance = instance,
                    Class = abc.Classes[index]
                };
                if (!types.TryGetValue(binding.RuntimeIdentity, out List<TypeBinding>? values))
                {
                    values = [];
                    types.Add(binding.RuntimeIdentity, values);
                }
                values.Add(binding);
                all_types.Add(binding);
                AddTypeLookup(qualified_lookup, binding.Qualified, binding);
                if (Avm2MethodAnalyzer.TryGetStaticName(
                        instance.QName,
                        out string static_name))
                {
                    AddTypeLookup(static_names, static_name, binding);
                }
                types_by_instance.Add(instance, binding);
                IndexTraits(binding.Instance, abc_index);
                IndexTraits(binding.Class, abc_index);
            }
        }
        foreach ((string name, List<TypeBinding> values) in qualified_lookup)
            qualified_types.Add(name, [.. values]);
        foreach ((string name, List<TypeBinding> values) in static_names)
            static_name_types.Add(name, [.. values]);
    }

    void IndexRuntimeNamespaces(ASConstantPool pool)
    {
        var private_ordinals = new Dictionary<string, int>(
            StringComparer.Ordinal);
        for (int index = 0; index < pool.Namespaces.Count; index++)
        {
            ASNamespace? value = pool.Namespaces[index];
            int private_ordinal = 0;
            if (value is not null &&
                value.Kind == NamespaceKind.Private)
            {
                string uri = value.NameIndex >= 0 &&
                    value.NameIndex < pool.Strings.Count
                        ? value.RuntimeName
                        : string.Empty;
                private_ordinal =
                    private_ordinals.GetValueOrDefault(uri);
                private_ordinals[uri] = private_ordinal + 1;
            }
            if (value is null)
                continue;
            runtime_namespace_identities.TryAdd(
                value,
                Avm2MethodAnalyzer.RuntimeNamespaceIdentity(
                    pool,
                    index,
                    private_ordinal));
        }
    }

    string RuntimeNamespaceIdentity(ASNamespace? value) =>
        value is not null &&
        runtime_namespace_identities.TryGetValue(
            value,
            out string? identity)
                ? identity
                : Avm2MethodAnalyzer.RuntimeNamespaceIdentity(value);

    string RuntimeSymbolIdentity(ASMultiname? value)
    {
        if (value is null)
            return Avm2MethodAnalyzer.RuntimeSymbolIdentity(null);
        if (runtime_symbol_identities.TryGetValue(
                value,
                out string? identity))
        {
            return identity;
        }
        identity = Avm2MethodAnalyzer.RuntimeSymbolIdentity(value);
        runtime_symbol_identities.Add(value, identity);
        return identity;
    }

    internal Avm2DeclaringScopeIndex DeclaringScopes =>
        declaring_scopes;

    static Avm2DeclaringScopeIndex CreateDeclaringScopes(
        IEnumerable<ABCFile> abc_files)
    {
        ArgumentNullException.ThrowIfNull(abc_files);
        return Avm2DeclaringScopeIndex.Create(abc_files.ToArray());
    }

    internal IReadOnlyList<Avm2TypeDefinition> ResolveTypes(
        ASMultiname? name,
        ABCFile requester) =>
        FindTypes(name, requester, true)
            .Select(Definition)
            .ToArray();

    internal Avm2TypeDefinition? ResolveUniqueType(
        ASMultiname? name,
        ABCFile requester)
    {
        List<TypeBinding> matches = FindTypes(name, requester, true);
        return matches.Count == 1 ? Definition(matches[0]) : null;
    }

    internal Avm2TypeDefinition? ResolveType(ASInstance instance)
    {
        return types_by_instance.TryGetValue(
            instance,
            out TypeBinding? binding)
                ? Definition(binding)
                : null;
    }

    internal IReadOnlyList<Avm2MethodBinding> ResolveMethodBindings(
        ASMethod method) =>
        method_bindings.GetBindings(method);

    internal IReadOnlyList<Avm2MethodBinding> ResolveNamedBindings(
        string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return method_bindings.Bindings
            .Where(binding =>
                binding.Resolved &&
                NameMatches(binding.Trait?.QName, name))
            .ToArray();
    }

    internal Avm2ResolvedValueSet ResolveValueTypes(
        Avm2MethodBinding binding,
        Avm2ExactReceiver? exact_receiver,
        string value)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrEmpty(value);
        if (!binding.Resolved ||
            binding.Method is null ||
            binding.Method.Body is null)
        {
            throw new ArgumentException(
                "The method binding does not reference an analyzable method.",
                nameof(binding));
        }
        var key = new ResolvedValueKey(
            binding,
            exact_receiver,
            value);
        if (resolved_value_types.TryGetValue(
                key,
                out Avm2ResolvedValueSet? cached))
        {
            return cached;
        }
        MethodContext context = Context(
            binding.Method,
            binding,
            exact_receiver) ?? throw new ArgumentException(
                "The method binding and exact receiver do not form a valid analysis context.",
                nameof(binding));
        PointsToResult result = ResolveValue(
            context,
            value,
            new HashSet<string>(StringComparer.Ordinal),
            0);
        var types = new List<Avm2ResolvedValueType>();
        foreach (PointsTo type in result.Types)
        {
            bool @static = type.Receiver == ReceiverKind.Static;
            if (types.Any(candidate =>
                ReferenceEquals(
                    candidate.RuntimeType,
                    type.Binding.Instance) &&
                candidate.Static == @static))
            {
                continue;
            }
            types.Add(new Avm2ResolvedValueType(
                type.Binding.Instance,
                @static));
        }
        var resolved = new Avm2ResolvedValueSet
        {
            Types = types,
            Exhaustive = result.Exhaustive &&
                result.Outcomes.Count == 0 &&
                result.Types.All(type => type.Exhaustive)
        };
        resolved_value_types.Add(key, resolved);
        return resolved;
    }

    internal bool LexicalBuiltinClassReceiver(
        Avm2MethodBinding binding,
        Avm2ExactReceiver? exact_receiver,
        string value)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrEmpty(value);
        if (!binding.Resolved ||
            binding.Method?.Body is null)
        {
            return false;
        }
        MethodContext? context = Context(
            binding.Method,
            binding,
            exact_receiver);
        if (context is null ||
            !context.VerifierValid ||
            !context.Flow.Complete ||
            !context.Flow.DeclaringScopeKnown ||
            !context.Analysis.ControlFlow.Complete ||
            !context.Producers.TryGetValue(
                value,
                out Avm2DataFlowOperation? operation) ||
            operation.Unreachable ||
            operation.Instruction < 0 ||
            operation.Instruction >= context.Code.Count ||
            operation.Outputs.Count != 1 ||
            !string.Equals(
                operation.Outputs[0],
                value,
                StringComparison.Ordinal) ||
            context.Code[operation.Instruction] is not
                GetLexIns lexical)
        {
            return false;
        }
        return LexicalBuiltinClassReceiver(
            context,
            operation,
            lexical.TypeName);
    }

    internal bool RuntimeSatisfies(
        ASInstance runtime,
        ASInstance contract)
    {
        return types_by_instance.TryGetValue(
                runtime,
                out TypeBinding? runtime_binding) &&
            types_by_instance.TryGetValue(
                contract,
                out TypeBinding? contract_binding) &&
            (ReferenceEquals(runtime, contract) ||
                IsStrictSubtype(runtime_binding, contract_binding));
    }

    internal bool RuntimeCouldSatisfy(
        ASInstance runtime,
        ASInstance contract) =>
        !types_by_instance.TryGetValue(
            runtime,
            out TypeBinding? runtime_binding) ||
        !types_by_instance.TryGetValue(
            contract,
            out TypeBinding? contract_binding) ||
        CouldBeSubtypeOrSame(
            runtime_binding,
            contract_binding);

    internal bool RuntimeIsDisjointFromBuiltin(
        ASInstance runtime,
        string builtin) =>
        types_by_instance.TryGetValue(
            runtime,
            out TypeBinding? runtime_binding) &&
        OwnerIsDisjointFromBuiltin(
            runtime_binding,
            builtin);

    internal bool RuntimeIsDisjointFromClass(
        ASInstance runtime,
        ASMultiname declared,
        ABCFile requester)
    {
        if (!types_by_instance.TryGetValue(
                runtime,
                out TypeBinding? current))
        {
            return false;
        }
        List<TypeBinding> loaded = FindTypes(
            declared,
            requester,
            true);
        if (loaded.Count > 0)
        {
            return loaded.All(candidate =>
                DeclaredTypeIsDisjointFromOwner(
                    candidate,
                    current));
        }

        var visited = new HashSet<ASInstance>(
            ReferenceEqualityComparer.Instance);
        while (visited.Add(current.Instance))
        {
            if (PropertiesMatchIndexed(
                    current.Instance.QName,
                    current.Abc,
                    declared,
                    requester) ||
                PropertiesMatchIndexed(
                    current.Instance.Super,
                    current.Abc,
                    declared,
                    requester))
            {
                return false;
            }
            if (IsBuiltinObject(current.Instance.Super))
                return true;
            List<TypeBinding> parents = FindTypes(
                current.Instance.Super,
                current.Abc,
                true);
            if (parents.Count != 1)
                return false;
            current = parents[0];
        }
        return false;
    }

    internal bool ProvesClosedSlotValueSet(
        ASInstance owner,
        ASMultiname property,
        Avm2MethodBinding writer,
        int instruction) =>
        ProvesClosedSlotValueSet(
            owner,
            owner,
            property,
            writer,
            instruction);

    internal bool ProvesClosedSlotValueSet(
        ASInstance owner,
        ASInstance runtime,
        ASMultiname property,
        Avm2MethodBinding writer,
        int instruction)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(writer);
        if (instruction < 0 ||
            property.Kind is not (
                MultinameKind.QName or
                MultinameKind.QNameA) ||
            property.Namespace is not ASNamespace property_namespace ||
            property_namespace.Kind is not (
                NamespaceKind.Private or
                NamespaceKind.Protected) ||
            !ReferenceEquals(property.Pool.ABC, owner.ABC) ||
            writer.Role != Avm2MethodBindingRole.InstanceConstructor ||
            !ReferenceEquals(writer.Owner, owner) ||
            !ReferenceEquals(writer.Method, owner.Constructor) ||
            writer.Method?.Body is null ||
            !types_by_instance.TryGetValue(
                owner,
                out TypeBinding? owner_binding) ||
            !types_by_instance.TryGetValue(
                runtime,
                out TypeBinding? runtime_binding) ||
            !RuntimeSatisfies(
                runtime,
                owner))
        {
            return false;
        }

        List<ASTrait> matches = owner.Traits
            .Where(trait =>
                trait.Kind is TraitKind.Slot or TraitKind.Constant &&
                PropertiesMatchIndexed(
                    trait.QName,
                    owner.ABC,
                    property,
                    property.Pool.ABC))
            .Take(2)
            .ToList();
        if (matches.Count != 1)
            return false;

        ASTrait slot = matches[0];
        EffectiveSlotLayout? layout = SlotLayout(
            owner_binding,
            ReceiverKind.Instance);
        if (layout is null)
            return false;
        List<int> indices = layout.Slots
            .Where(value => ReferenceEquals(value.Value, slot))
            .Select(value => value.Key)
            .Take(2)
            .ToList();
        if (indices.Count != 1 ||
            !EnsureGlobalInstructionIndex())
        {
            return false;
        }

        var key = new ClosedSlotValueSetKey(
            owner,
            runtime,
            slot,
            writer,
            instruction);
        if (closed_slot_value_sets.TryGetValue(
                key,
                out bool cached))
        {
            return cached;
        }
        bool proven =
            !HasReachableAlternateMethodReference(
                writer.Method) &&
            ProvesClosedSlotValueSet(
                owner_binding,
                runtime_binding,
                slot,
                writer,
                instruction,
                indices[0]);
        closed_slot_value_sets.Add(key, proven);
        return proven;
    }

    internal Avm2ExactReceiver? ResolveExactReceiver(
        Avm2MethodBinding source,
        Avm2ExactReceiver? source_receiver,
        string value,
        Avm2MethodBinding target)
    {
        Avm2ResolvedValueSet resolved = ResolveValueTypes(
            source,
            source_receiver,
            value);
        if (!resolved.Exhaustive || resolved.Types.Count != 1)
            return null;
        Avm2ResolvedValueType type = resolved.Types[0];
        var exact_receiver = new Avm2ExactReceiver(
            type.RuntimeType,
            type.Static);
        return ExactReceiverMatches(target, exact_receiver)
            ? exact_receiver
            : null;
    }

    internal IReadOnlyList<Avm2MethodBinding> ResolveMethods(
        ASContainer container,
        ASMultiname property,
        ABCFile requester) =>
        ResolveContainerMethods(
            container,
            trait => PropertiesMatchIndexed(
                property,
                requester,
                trait.QName,
                trait.ABC));

    internal IReadOnlyList<Avm2MethodBinding> ResolvePublicMethods(
        ASContainer container,
        string name) =>
        ResolveContainerMethods(
            container,
            trait => NameMatches(trait.QName, name) &&
                IsPublicImplementation(trait.QName));

    internal static bool PropertiesMatch(
        ASMultiname? left,
        ABCFile? left_requester,
        ASMultiname? right,
        ABCFile? right_requester)
    {
        if (left is null || right is null || !NamesMatch(left, right))
            return false;
        bool left_private = IsPrivate(left);
        bool right_private = IsPrivate(right);
        if (left_private || right_private)
        {
            return left_private &&
                right_private &&
                left_requester is not null &&
                right_requester is not null &&
                ReferenceEquals(left_requester, right_requester) &&
                ReferenceEquals(left.Pool.ABC, left_requester) &&
                ReferenceEquals(right.Pool.ABC, right_requester) &&
                Avm2MethodAnalyzer.RuntimeSymbolIdentity(left) ==
                    Avm2MethodAnalyzer.RuntimeSymbolIdentity(right);
        }
        if (Avm2MethodAnalyzer.RuntimeSymbolIdentity(left) ==
            Avm2MethodAnalyzer.RuntimeSymbolIdentity(right))
        {
            return true;
        }
        return NamespaceSetContains(left, right) ||
            NamespaceSetContains(right, left);
    }

    bool PropertiesMatchIndexed(
        ASMultiname? left,
        ABCFile? left_requester,
        ASMultiname? right,
        ABCFile? right_requester)
    {
        if (left is null || right is null || !NamesMatch(left, right))
            return false;
        bool left_private = IsPrivate(left);
        bool right_private = IsPrivate(right);
        if (left_private || right_private)
        {
            return left_private &&
                right_private &&
                left_requester is not null &&
                right_requester is not null &&
                ReferenceEquals(left_requester, right_requester) &&
                ReferenceEquals(left.Pool.ABC, left_requester) &&
                ReferenceEquals(right.Pool.ABC, right_requester) &&
                RuntimeSymbolIdentity(left) == RuntimeSymbolIdentity(right);
        }
        if (RuntimeSymbolIdentity(left) == RuntimeSymbolIdentity(right))
            return true;
        return NamespaceSetContainsIndexed(left, right) ||
            NamespaceSetContainsIndexed(right, left);
    }

    static Avm2TypeDefinition Definition(TypeBinding binding) =>
        new(
            binding.Abc,
            binding.AbcIndex,
            binding.ClassIndex,
            binding.Instance,
            binding.Class,
            binding.Qualified,
            binding.RuntimeIdentity);

    IReadOnlyList<Avm2MethodBinding> ResolveContainerMethods(
        ASContainer container,
        Func<ASTrait, bool> predicate)
    {
        ASContainer? current = container;
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        while (current is not null)
        {
            List<Avm2MethodBinding> matches = MethodBindings(current)
                .Where(binding =>
                    binding.Trait is not null &&
                    predicate(binding.Trait))
                .ToList();
            if (matches.Count > 0)
                return matches;
            if (container is ASClass)
                break;

            ASInstance? instance = current switch
            {
                ASInstance value => value,
                ASClass value => value.Instance,
                _ => null
            };
            if (instance is null || !visited.Add(instance))
                break;
            TypeBinding? parent = Parent(instance);
            current = parent is null
                ? null
                : container is ASClass
                    ? parent.Class
                    : parent.Instance;
        }
        return [];
    }

    public void ClearTransientContexts()
    {
        contexts.Clear();
        resolved_value_types.Clear();
        external_contexts.Clear();
        scoped_contexts.Clear();
        returns.Clear();
        active_returns.Clear();
        constructor_private_writes.Clear();
        active_constructor_private_writes.Clear();
        constructor_private_write_suppression = 0;
    }

    public Avm2ResolvedCall Resolve(
        ASMethod caller,
        IReadOnlyList<ASInstruction> code,
        Avm2DataFlowAnalysis flow,
        Avm2DataFlowOperation operation)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(operation);
        ASMethodBody body = caller.Body ??
            throw new InvalidOperationException(
                "Call target resolution requires a method body.");
        Avm2MethodAnalysis? source_analysis =
            flow.SourceMethodAnalysis;
        Avm2MethodAnalysis analysis =
            source_analysis?.MatchesSource(body) == true
                ? source_analysis
                : Avm2MethodAnalyzer.Analyze(body);
        return Resolve(caller, null, code, analysis, flow, operation);
    }

    public Avm2ResolvedCall Resolve(
        Avm2MethodBinding caller_binding,
        IReadOnlyList<ASInstruction> code,
        Avm2DataFlowAnalysis flow,
        Avm2DataFlowOperation operation)
    {
        ArgumentNullException.ThrowIfNull(caller_binding);
        ASMethod caller = caller_binding.Method ??
            throw new ArgumentException(
                "Caller binding does not resolve to a method.",
                nameof(caller_binding));
        ASMethodBody body = caller.Body ??
            throw new InvalidOperationException(
                "Call target resolution requires a method body.");
        Avm2MethodAnalysis? source_analysis =
            flow.SourceMethodAnalysis;
        Avm2MethodAnalysis analysis =
            source_analysis?.MatchesSource(body) == true
                ? source_analysis
                : Avm2MethodAnalyzer.Analyze(body);
        return Resolve(
            caller,
            caller_binding,
            code,
            analysis,
            flow,
            operation);
    }

    public Avm2ResolvedCall Resolve(
        ASMethod caller,
        IReadOnlyList<ASInstruction> code,
        Avm2MethodAnalysis analysis,
        Avm2DataFlowAnalysis flow,
        Avm2DataFlowOperation operation)
    {
        return Resolve(caller, null, code, analysis, flow, operation);
    }

    public Avm2ResolvedCall Resolve(
        Avm2MethodBinding caller_binding,
        IReadOnlyList<ASInstruction> code,
        Avm2MethodAnalysis analysis,
        Avm2DataFlowAnalysis flow,
        Avm2DataFlowOperation operation)
    {
        ArgumentNullException.ThrowIfNull(caller_binding);
        ASMethod caller = caller_binding.Method ??
            throw new ArgumentException(
                "Caller binding does not resolve to a method.",
                nameof(caller_binding));
        return Resolve(
            caller,
            caller_binding,
            code,
            analysis,
            flow,
            operation);
    }

    Avm2ResolvedCall Resolve(
        ASMethod caller,
        Avm2MethodBinding? caller_binding,
        IReadOnlyList<ASInstruction> code,
        Avm2MethodAnalysis analysis,
        Avm2DataFlowAnalysis flow,
        Avm2DataFlowOperation operation)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(operation);
        if (!flow.Operations.Contains(operation))
        {
            throw new ArgumentException(
                "The operation does not belong to the supplied data-flow analysis.",
                nameof(operation));
        }
        if (operation.Instruction < 0 ||
            operation.Instruction >= code.Count ||
            operation.Opcode !=
                code[operation.Instruction].OP.ToString() ||
            operation.Offset !=
                code[operation.Instruction].DecodedOffset)
        {
            return Empty("", "NotCall");
        }

        if (caller_binding is not null &&
            !ReferenceEquals(caller_binding.Method, caller))
        {
            throw new ArgumentException(
                "Caller binding resolves to a different method.",
                nameof(caller_binding));
        }
        MethodContext context = ExternalContext(
            caller,
            caller_binding,
            code,
            analysis,
            flow);
        if (!context.Operations.TryGetValue(
                operation.Instruction,
                out Avm2DataFlowOperation? canonical_operation) ||
            canonical_operation.Instruction < 0 ||
            canonical_operation.Instruction >= context.Code.Count ||
            !TryCall(
                caller,
                canonical_operation,
                context.Code[canonical_operation.Instruction],
                out CallSite call))
        {
            return Empty("", "InvalidFlow", "call-operation");
        }
        return Resolve(
            context,
            canonical_operation,
            context.Code[canonical_operation.Instruction],
            call,
            0);
    }

    Avm2ResolvedCall Resolve(
        MethodContext context,
        Avm2DataFlowOperation operation,
        ASInstruction instruction,
        CallSite call,
        int depth)
    {
        if (depth > MaximumDepth)
            return WithCallConditions(
                Empty(call.Name, "DepthLimited", "call-target-depth"),
                context,
                operation);

        Avm2ResolvedCall? direct = DirectTarget(context, operation, instruction, call);
        if (direct is not null)
            return WithCallConditions(direct, context, operation);

        PointsToResult receiver_value = ResolveValue(
            context,
            call.Receiver,
            new HashSet<string>(StringComparer.Ordinal),
            depth + 1);
        PointsToResult receivers = Result(
            receiver_value.Types,
            DereferenceOutcomes(
                context,
                operation,
                receiver_value.Outcomes),
            receiver_value.ControlFlowExhaustive,
            receiver_value.TargetExhaustive);
        var targets = new List<Avm2ResolvedCallTarget>();
        foreach (PointsTo receiver in receivers.Types)
        {
            foreach (Avm2MethodBinding binding in ResolveMethods(receiver, call))
            {
                List<Avm2CallTargetEvidence> evidence = [.. receiver.Evidence];
                if (UsesInterfaceBinding(
                    receiver.Binding,
                    receiver.Receiver,
                    binding,
                    call,
                    out ASTrait? contract))
                {
                    evidence.Add(Evidence(
                        "InterfaceBinding",
                        "VmExact",
                        context,
                        operation.Instruction,
                        operation.Offset,
                        $"{Qualified(contract!.QName)}->" +
                        Qualified(binding.Trait!.QName),
                        binding.AbcIndex));
                }
                else if (UsesMethodInfoAlias(binding, call))
                {
                    evidence.Add(Evidence(
                        "HarmanMethodInfoAlias",
                        "AuthenticatedHarmanTransform",
                        context,
                        operation.Instruction,
                        operation.Offset,
                        $"{DisplayName(binding.Trait!.QName)}->{binding.Method!.Name}",
                        binding.AbcIndex));
                }
                targets.Add(new Avm2ResolvedCallTarget
                {
                    Method = binding.Method!,
                    Binding = binding,
                    ExactReceiver = ExactReceiver(receiver),
                    RuntimeType = receiver.Binding.Qualified,
                    DefinitionAbc = receiver.Binding.AbcIndex,
                    SelectionKind = receiver.SelectionKind,
                    SelectorExpression = receiver.SelectorExpression,
                    Conditions = receiver.Conditions,
                    Evidence = evidence
                });
            }
        }

        bool control_flow_exhaustive = receivers.ControlFlowExhaustive;
        bool target_exhaustive = receivers.TargetExhaustive &&
            (receivers.Types.Count > 0 || receivers.Outcomes.Count > 0) &&
            receivers.Types.All(receiver => ResolveMethods(receiver, call)
                .Any(binding =>
                    harman_method_aliases ||
                    !UsesMethodInfoAlias(binding, call) ||
                    UsesInterfaceBinding(
                        receiver.Binding,
                        receiver.Receiver,
                        binding,
                        call,
                        out _)));

        targets = Deduplicate(targets);
        Avm2ResolvedCall resolved = new()
        {
            Name = call.Name,
            Kind = targets.Count switch
            {
                0 => "Unresolved",
                1 => targets[0].SelectionKind,
                _ when targets.All(target => target.SelectionKind == "FactoryBranch") =>
                    "FactoryDispatch",
                _ => "TypeUnion"
            },
            Exhaustive = control_flow_exhaustive && target_exhaustive,
            ControlFlowExhaustive = control_flow_exhaustive,
            TargetExhaustive = target_exhaustive,
            Nullable = receivers.Outcomes.Any(outcome => outcome.Kind == "Null"),
            Targets = targets,
            TerminalOutcomes = receivers.Outcomes,
            Diagnostics = targets.Count == 0 && receivers.Outcomes.Count == 0
                ? call.Property?.IsRuntime == true
                    ? ["runtime-multiname-unresolved"]
                    : ["call-target-unresolved"]
                : []
        };
        return WithCallConditions(resolved, context, operation);
    }

    Avm2ResolvedCall? DirectTarget(
        MethodContext context,
        Avm2DataFlowOperation operation,
        ASInstruction instruction,
        CallSite call)
    {
        ASMethod? method = instruction switch
        {
            CallStaticIns value => value.Method,
            _ => null
        };
        if (method is not null)
        {
            Avm2MethodBinding? binding = UniqueBinding(method);
            return Exact(
                call.Name,
                method,
                "ExactIndex",
                operation,
                context,
                binding: binding,
                exact_receiver: ExactInvocationReceiver(
                    context,
                    call,
                    1));
        }

        if (instruction is CallIns)
        {
            CallableResult callable = call.Callable is null
                ? EmptyCallable()
                : ResolveCallable(
                    context,
                    call.Callable,
                    new HashSet<string>(StringComparer.Ordinal),
                    1);
            Avm2ExactReceiver? exact_receiver = ExactInvocationReceiver(
                context,
                call,
                1);
            List<Avm2ResolvedCallTarget> targets = Deduplicate(
                callable.Targets.Select(target =>
                    WithExactReceiver(target, exact_receiver)));
            return new Avm2ResolvedCall
            {
                Name = call.Name,
                Kind = targets.Count switch
                {
                    0 => "Unresolved",
                    1 => targets[0].SelectionKind,
                    _ => "CallableUnion"
                },
                Exhaustive = callable.Exhaustive && targets.Count > 0,
                ControlFlowExhaustive =
                    callable.ControlFlowExhaustive && targets.Count > 0,
                TargetExhaustive =
                    callable.TargetExhaustive && targets.Count > 0,
                Targets = targets,
                Diagnostics = targets.Count == 0
                    ? ["callins-callable-unresolved"]
                    : []
            };
        }

        if (instruction is CallMethodIns dispatch)
        {
            return Empty(
                call.Name,
                "Unresolved",
                "callmethod-verifier-illegal",
                $"callmethod-disp-id-{dispatch.MethodIndex}-unresolved");
        }

        if (instruction is ConstructPropIns construct)
        {
            PointsToResult constructors = ResolveConstructProperty(
                context,
                operation,
                construct.PropertyName,
                call.Receiver,
                new HashSet<string>(StringComparer.Ordinal),
                1);
            if (constructors.Types.Count > 0)
            {
                return ConstructorTargets(
                    call.Name,
                    constructors,
                    operation,
                    context);
            }
        }

        if (instruction is ConstructIns)
        {
            PointsToResult constructors = ConstructorClosures(ResolveValue(
                context,
                call.Receiver,
                new HashSet<string>(StringComparer.Ordinal),
                1));
            if (constructors.Types.Count > 0)
            {
                return ConstructorTargets(
                    call.Name,
                    constructors,
                    operation,
                    context);
            }
        }

        if (instruction.OP == OPCode.ConstructSuper)
        {
            TypeBinding? parent = MethodContainer(context) is ASInstance instance
                ? Parent(instance)
                : null;
            if (parent is not null)
            {
                return Exact(
                    call.Name,
                    parent.Instance.Constructor,
                    "ConstructedType",
                    operation,
                    context,
                    parent.Qualified,
                    ConstructorBinding(parent),
                    CompatibleInvocationReceiver(
                        context,
                        call,
                        parent,
                        1));
            }
        }

        if (instruction is CallSuperIns or CallSuperVoidIns)
        {
            TypeBinding? parent = MethodContainer(context) is ASInstance instance
                ? Parent(instance)
                : null;
            List<Avm2MethodBinding> matches = parent is null
                ? []
                : FindMethods(parent, ReceiverKind.Instance, call)
                    .Take(2)
                    .ToList();
            Avm2MethodBinding? target =
                matches.Count == 1 ? matches[0] : null;
            if (target is not null)
            {
                return Exact(
                    call.Name,
                    target.Method!,
                    "DeclaredType",
                    operation,
                    context,
                    parent!.Qualified,
                    target,
                    CompatibleInvocationReceiver(
                        context,
                        call,
                        parent,
                        1));
            }
        }

        return null;
    }

    PointsToResult ResolveValue(
        MethodContext context,
        string value,
        HashSet<string> visited,
        int depth)
    {
        if (depth > MaximumDepth || visited.Count > MaximumValues || !visited.Add(value))
            return None();

        if (value == "v_entry_local_0")
        {
            if (context.ExactReceiver is Avm2ExactReceiver exact_receiver)
            {
                return !types_by_instance.TryGetValue(
                        exact_receiver.RuntimeType,
                        out TypeBinding? exact)
                    ? None()
                    : One(
                        exact,
                        exact_receiver.Static
                            ? ReceiverKind.Static
                            : ReceiverKind.Instance,
                        "ExactReceiver",
                        context,
                        -1,
                        -1,
                        true,
                        Qualified(exact_receiver.RuntimeType.QName));
            }
            ASContainer? container = MethodContainer(context);
            TypeBinding? owner = Owner(container);
            if (owner is null)
                return None();
            if (container is ASClass)
            {
                return One(
                    owner,
                    ReceiverKind.Static,
                    "DeclaredType",
                    context,
                    -1,
                    -1,
                    true);
            }
            bool closed = owner.Instance.Flags.HasFlag(ClassFlags.Final);
            return Many(
                all_types.Where(candidate =>
                    ReferenceEquals(candidate.Abc, owner.Abc) &&
                    ReferenceEquals(candidate.Instance, owner.Instance) ||
                    IsStrictSubtype(candidate, owner)),
                ReceiverKind.Instance,
                "DeclaredType",
                context,
                -1,
                -1,
                closed);
        }

        if (value.StartsWith("v_entry_local_", StringComparison.Ordinal) &&
            int.TryParse(value.AsSpan("v_entry_local_".Length), out int local) &&
            local > 0 && local <= context.Method.Parameters.Count)
        {
            return Many(
                FindTypes(
                    context.Method.Parameters[local - 1].Type,
                    context.Method.ABC),
                ReceiverKind.Instance,
                "DeclaredType",
                context,
                -1,
                -1,
                false);
        }

        if (context.Phis.TryGetValue(value, out Avm2DataFlowPhi? phi))
        {
            var types = new List<PointsTo>();
            var outcomes = new List<Avm2CallTerminalOutcome>();
            bool control_flow_exhaustive = true;
            bool target_exhaustive = true;
            foreach (Avm2DataFlowPhiInput input in phi.Inputs)
            {
                List<Avm2CallCondition> input_conditions =
                    PhiInputConditions(context, phi, input);
                PointsToResult branch = ResolveValue(
                    context,
                    input.Value,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1);
                types.AddRange(branch.Types.Select(type =>
                    WithConditions(
                        WithSelection(type, "PhiUnion"),
                        input_conditions)));
                outcomes.AddRange(branch.Outcomes.Select(outcome =>
                    WithConditions(outcome, input_conditions)));
                control_flow_exhaustive &= branch.ControlFlowExhaustive;
                target_exhaustive &= branch.TargetExhaustive;
            }
            return Result(
                types,
                outcomes,
                control_flow_exhaustive,
                target_exhaustive);
        }

        if (!context.Producers.TryGetValue(value, out Avm2DataFlowOperation? producer) ||
            producer.Instruction < 0 || producer.Instruction >= context.Code.Count)
        {
            return TypeHint(context, value);
        }

        ASInstruction instruction = context.Code[producer.Instruction];
        if (instruction.OP is OPCode.PushNull or OPCode.PushUndefined)
        {
            return Outcome(
                instruction.OP == OPCode.PushNull ? "Null" : "Undefined",
                instruction.OP == OPCode.PushNull ? "null" : "undefined",
                context,
                producer,
                true);
        }
        if (instruction is ConstructPropIns construct_property)
        {
            int receiver_index =
                producer.Inputs.Count - construct_property.ArgCount -
                (construct_property.PropertyName.IsNamespaceNeeded ? 1 : 0) -
                (construct_property.PropertyName.IsNameNeeded ? 1 : 0) - 1;
            if (receiver_index >= 0 && receiver_index < producer.Inputs.Count)
            {
                PointsToResult constructors = ResolveConstructProperty(
                    context,
                    producer,
                    construct_property.PropertyName,
                    producer.Inputs[receiver_index],
                    visited,
                    depth + 1);
                return Result(
                    constructors.Types.Select(type => new PointsTo
                    {
                        Binding = type.Binding,
                        Receiver = ReceiverKind.Instance,
                        SelectionKind = "ConstructedType",
                        SelectorExpression = type.SelectorExpression,
                        Conditions = type.Conditions,
                        Evidence =
                        [
                            .. type.Evidence,
                            Evidence(
                                "ConstructedType",
                                type.Exhaustive ? "Exact" : "Partial",
                                context,
                                producer.Instruction,
                                producer.Offset,
                                type.Binding.Qualified,
                                type.Binding.AbcIndex)
                        ],
                        Exhaustive = type.Exhaustive
                    }),
                    constructors.Outcomes,
                    constructors.ControlFlowExhaustive,
                    constructors.TargetExhaustive);
            }
            return None();
        }

        if (instruction is ConstructIns construct)
        {
            int receiver_index = producer.Inputs.Count - construct.ArgCount - 1;
            if (receiver_index >= 0 && receiver_index < producer.Inputs.Count)
            {
                PointsToResult constructors = ConstructorClosures(ResolveValue(
                    context,
                    producer.Inputs[receiver_index],
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1));
                return Result(
                    constructors.Types.Select(type => new PointsTo
                    {
                        Binding = type.Binding,
                        Receiver = ReceiverKind.Instance,
                        SelectionKind = "ConstructedType",
                        SelectorExpression = type.SelectorExpression,
                        Conditions = type.Conditions,
                        Evidence =
                        [
                            .. type.Evidence,
                            Evidence(
                                "ConstructedType",
                                "Exact",
                                context,
                                producer.Instruction,
                                producer.Offset,
                                type.Binding.Qualified)
                        ],
                        Exhaustive = type.Exhaustive
                    }),
                    constructors.Exhaustive);
            }
        }

        if (instruction is CoerceIns coerce)
        {
            PointsToResult runtime = producer.Inputs.Count == 0
                ? None()
                : ResolveValue(
                    context,
                    producer.Inputs[^1],
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1);
            if (runtime.Types.Count > 0 || runtime.Outcomes.Count > 0)
            {
                return NarrowToType(
                    context,
                    producer,
                    runtime,
                    FindTypes(coerce.TypeName, context.Method.ABC),
                    false);
            }
            return Many(
                FindTypes(coerce.TypeName, context.Method.ABC),
                ReceiverKind.Instance,
                "DeclaredType",
                context,
                producer.Instruction,
                producer.Offset,
                false);
        }

        if (instruction is AsTypeIns as_type)
        {
            PointsToResult runtime = producer.Inputs.Count == 0
                ? None()
                : ResolveValue(
                    context,
                    producer.Inputs[^1],
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1);
            if (runtime.Types.Count > 0 || runtime.Outcomes.Count > 0)
            {
                return NarrowToType(
                    context,
                    producer,
                    runtime,
                    FindTypes(as_type.TypeName, context.Method.ABC),
                    true);
            }
            return Many(
                FindTypes(as_type.TypeName, context.Method.ABC),
                ReceiverKind.Instance,
                "DeclaredType",
                context,
                producer.Instruction,
                producer.Offset,
                false);
        }

        if (instruction.OP == OPCode.AsTypeLate)
        {
            if (producer.Inputs.Count < 2)
                return None();
            PointsToResult runtime = ResolveValue(
                context,
                producer.Inputs[0],
                new HashSet<string>(visited, StringComparer.Ordinal),
                depth + 1);
            PointsToResult target = ResolveValue(
                context,
                producer.Inputs[^1],
                new HashSet<string>(visited, StringComparer.Ordinal),
                depth + 1);
            List<TypeBinding> target_types = target.Types
                .Where(value => value.Receiver == ReceiverKind.Static)
                .Select(value => value.Binding)
                .ToList();
            if (target_types.Count == 0)
                return None();
            PointsToResult narrowed = NarrowToType(
                context,
                producer,
                runtime,
                target_types,
                true);
            return Result(
                narrowed.Types,
                narrowed.Outcomes,
                narrowed.ControlFlowExhaustive && target.Exhaustive,
                narrowed.TargetExhaustive && target.Exhaustive);
        }

        if (instruction.OP == OPCode.CheckFilter)
        {
            if (producer.Inputs.Count == 0)
                return None();
            return FilterXmlValue(
                context,
                producer,
                ResolveValue(
                    context,
                    producer.Inputs[^1],
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1));
        }

        if (instruction.OP == OPCode.Coerce_a)
        {
            List<PointsTo> aliases = [];
            List<Avm2CallTerminalOutcome> outcomes = [];
            bool control_flow_exhaustive = producer.Inputs.Count > 0;
            bool target_exhaustive = producer.Inputs.Count > 0;
            foreach (string input in producer.Inputs)
            {
                PointsToResult resolved = ResolveValue(
                    context,
                    input,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1);
                aliases.AddRange(resolved.Types);
                outcomes.AddRange(resolved.Outcomes);
                control_flow_exhaustive &= resolved.ControlFlowExhaustive;
                target_exhaustive &= resolved.TargetExhaustive;
            }
            if (aliases.Count > 0 || outcomes.Count > 0)
            {
                return Result(
                    aliases,
                    outcomes,
                    control_flow_exhaustive,
                    target_exhaustive);
            }
        }

        if (instruction is GetLexIns lexical)
        {
            PointsToResult? assigned = ResolveDominatingPrivateWrite(
                context,
                lexical.TypeName,
                producer,
                null,
                visited,
                depth);
            if (assigned is not null)
                return assigned;
            PointsToResult lexical_scope = ResolveLexicalScope(
                context,
                producer,
                lexical.TypeName,
                visited,
                depth);
            if (lexical_scope.Types.Count > 0)
            {
                PointsToResult scoped_value = ResolveTraitValue(
                    context,
                    lexical.TypeName,
                    lexical_scope,
                    producer,
                    visited,
                    depth);
                if (scoped_value.Types.Count > 0 ||
                    scoped_value.Outcomes.Count > 0)
                {
                    return scoped_value;
                }
            }
            LexicalDomainCertainty domain = InspectLexicalDomain(
                context,
                producer,
                lexical.TypeName,
                visited,
                depth);
            if (domain == LexicalDomainCertainty.Blocked)
                return None();
            PointsToResult definitions = Many(
                FindTypes(lexical.TypeName, context.Method.ABC),
                ReceiverKind.Static,
                "DeclaredType",
                context,
                producer.Instruction,
                producer.Offset,
                domain == LexicalDomainCertainty.Exact);
            if (definitions.Types.Count > 0)
                return definitions;
        }

        ASMultiname? lexical_property = instruction switch
        {
            FindPropertyIns find => find.PropertyName,
            FindPropStrictIns strict => strict.PropertyName,
            _ => null
        };
        if (lexical_property is not null)
        {
            PointsToResult lexical_scope = ResolveLexicalScope(
                context,
                producer,
                lexical_property,
                visited,
                depth);
            if (lexical_scope.Types.Count > 0)
                return lexical_scope;
        }

        if (instruction is GetPropertyIns property)
        {
            string? receiver_value = producer.Inputs.FirstOrDefault();
            PointsToResult? assigned = ResolveDominatingPrivateWrite(
                context,
                property.PropertyName,
                producer,
                receiver_value,
                visited,
                depth);
            if (assigned is not null)
                return assigned;
            PointsToResult receiver = receiver_value is null
                ? None()
                : ResolveValue(
                    context,
                    receiver_value,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1);
            PointsToResult trait = ResolveTraitValue(
                context,
                property.PropertyName,
                receiver,
                producer,
                visited,
                depth);
            if (trait.Types.Count > 0 || trait.Outcomes.Count > 0)
                return trait;
        }

        if (instruction is GetSlotIns slot)
        {
            string? receiver_value = producer.Inputs.FirstOrDefault();
            PointsToResult receiver = receiver_value is null
                ? None()
                : ResolveValue(
                    context,
                    receiver_value,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1);
            PointsToResult slot_type = ResolveSlotValue(
                context,
                slot.SlotIndex,
                receiver,
                producer);
            if (slot_type.Types.Count > 0)
                return slot_type;
        }

        if (TryCall(context.Method, producer, instruction, out CallSite call))
        {
            Avm2ResolvedCall targets = Resolve(context, producer, instruction, call, depth + 1);
            PointsToResult returned = ResolveReturns(
                context,
                producer,
                call,
                targets,
                depth + 1);
            if (returned.Types.Count > 0 || returned.Outcomes.Count > 0)
                return returned;
        }

        return TypeHint(context, value);
    }

    PointsToResult? ResolveDominatingPrivateWrite(
        MethodContext context,
        ASMultiname property,
        Avm2DataFlowOperation load,
        string? receiver,
        HashSet<string> visited,
        int depth)
    {
        List<TraitBinding> property_traits = ExactPropertyTraits(property);
        if (depth >= MaximumDepth ||
            !IsExactPrivateProperty(property, context.Method.ABC) ||
            property_traits.Count != 1 ||
            property_traits[0].Trait.Kind is not (
                TraitKind.Slot or TraitKind.Constant))
        {
            return null;
        }

        return ResolveSameBlockPrivateWrite(
                context,
                property_traits[0],
                property,
                load,
                receiver,
                visited,
                depth) ??
            ResolveConstructorPrivateWrite(
                context,
                property,
                load,
                receiver,
                visited,
                depth);
    }

    PointsToResult? ResolveSameBlockPrivateWrite(
        MethodContext context,
        TraitBinding property_trait,
        ASMultiname property,
        Avm2DataFlowOperation load,
        string? receiver,
        HashSet<string> visited,
        int depth)
    {
        if (!SameBlockContextIsComplete(context) ||
            property_trait.Container is not ASInstance trait_owner ||
            MethodContainer(context) is not ASInstance method_owner ||
            !ReferenceEquals(method_owner, trait_owner) ||
            !LoadReadsOwnerThis(context, property, load, receiver))
        {
            return null;
        }

        List<Avm2DataFlowOperation> stores = context.Flow.Operations
            .Where(operation =>
                operation.Block == load.Block &&
                operation.Instruction >= 0 &&
                operation.Instruction < load.Instruction &&
                operation.Instruction < context.Code.Count &&
                context.Code[operation.Instruction] is
                    SetPropertyIns or InitPropertyIns &&
                IsSameExactPrivateProperty(
                    property,
                    PropertyMultiname(context.Code[operation.Instruction]),
                    context.Method.ABC))
            .OrderBy(operation => operation.Instruction)
            .ToList();
        if (stores.Count == 0)
            return null;

        Avm2DataFlowOperation store = stores[^1];
        if (store.Inputs.Count != 2 ||
            !ValueIsExactlyThis(
                context,
                store.Inputs[0],
                property,
                new HashSet<string>(StringComparer.Ordinal)))
        {
            return null;
        }

        foreach (Avm2DataFlowOperation operation in context.Flow.Operations.Where(operation =>
            operation.Block == load.Block &&
            operation.Instruction > store.Instruction &&
            operation.Instruction < load.Instruction))
        {
            if (operation.Instruction < 0 ||
                operation.Instruction >= context.Code.Count)
            {
                return null;
            }
            ASInstruction instruction = context.Code[operation.Instruction];
            if (!IsPrivateWriteProofTransparent(instruction))
                return null;
        }

        PointsToResult stored = ResolveValue(
            context,
            store.Inputs[^1],
            new HashSet<string>(visited, StringComparer.Ordinal),
            depth + 1);
        if (!stored.Exhaustive ||
            stored.Types.Count == 0 ||
            stored.Outcomes.Count > 0 ||
            stored.Types.Any(value => !value.Exhaustive))
        {
            return null;
        }

        return Result(
            stored.Types.Select(value => new PointsTo
            {
                Binding = value.Binding,
                Receiver = value.Receiver,
                SelectionKind = value.SelectionKind,
                SelectorExpression = value.SelectorExpression,
                Conditions = value.Conditions,
                Evidence =
                [
                    .. value.Evidence,
                    Evidence(
                        "DominatingPrivateWrite",
                        "Exact",
                        context,
                        store.Instruction,
                        store.Offset,
                        Qualified(property),
                        value.Binding.AbcIndex)
                ],
                Exhaustive = true
            }),
            [],
            true,
            true);
    }

    PointsToResult? ResolveConstructorPrivateWrite(
        MethodContext context,
        ASMultiname property,
        Avm2DataFlowOperation load,
        string? receiver,
        HashSet<string> visited,
        int depth)
    {
        if (constructor_private_write_suppression > 0 ||
            MethodContainer(context) is not ASInstance load_owner ||
            Owner(load_owner) is not TypeBinding owner ||
            !LoadReadsOwnerThis(context, property, load, receiver))
        {
            return null;
        }

        List<TraitBinding> property_traits = ExactPropertyTraits(property);
        if (property_traits.Count != 1 ||
            !ReferenceEquals(property_traits[0].Container, owner.Instance) ||
            property_traits[0].Trait.Kind is not (
                TraitKind.Slot or TraitKind.Constant))
        {
            return null;
        }

        Avm2ExactReceiver? owner_receiver = context.ExactReceiver is
            Avm2ExactReceiver exact_receiver &&
            !exact_receiver.Static &&
            ReferenceEquals(exact_receiver.RuntimeType, owner.Instance)
                ? exact_receiver
                : owner.Instance.Flags.HasFlag(ClassFlags.Final)
                    ? new Avm2ExactReceiver(owner.Instance, false)
                    : null;
        if (owner_receiver is null)
            return null;

        var key = new ConstructorPrivateWriteKey(
            owner.Instance,
            property_traits[0].Trait,
            owner_receiver);
        if (constructor_private_writes.TryGetValue(key, out PointsToResult? cached))
            return cached;
        if (!active_constructor_private_writes.Add(key))
            return null;

        try
        {
            PointsToResult? result = BuildConstructorPrivateWrite(
                owner,
                property_traits[0].Trait,
                property,
                owner_receiver,
                visited,
                depth);
            constructor_private_writes[key] = result;
            return result;
        }
        finally
        {
            active_constructor_private_writes.Remove(key);
        }
    }

    PointsToResult? BuildConstructorPrivateWrite(
        TypeBinding owner,
        ASTrait slot,
        ASMultiname property,
        Avm2ExactReceiver owner_receiver,
        HashSet<string> visited,
        int depth)
    {
        Avm2MethodBinding? constructor_binding = ConstructorBinding(owner);
        if (constructor_binding is null ||
            ResolveMethodBindings(owner.Instance.Constructor).Count != 1 ||
            Context(
                owner.Instance.Constructor,
                constructor_binding,
                owner_receiver) is not MethodContext constructor ||
            !ConstructorContextIsComplete(constructor))
        {
            return null;
        }

        Avm2MethodBinding? class_constructor_binding = method_bindings
            .GetBindings(owner.Class)
            .SingleOrDefault(binding =>
                binding.Role == Avm2MethodBindingRole.StaticConstructor &&
                ReferenceEquals(binding.Method, owner.Class.Constructor));
        if (class_constructor_binding is null ||
            ResolveMethodBindings(owner.Class.Constructor).Count != 1 ||
            Context(owner.Class.Constructor, class_constructor_binding) is not MethodContext class_constructor ||
            !InitializerContextIsComplete(class_constructor) ||
            !EnsureGlobalInstructionIndex())
        {
            return null;
        }

        List<GlobalInstruction> possible_writes = global_instructions!
            .Where(candidate =>
                IsPropertyWrite(candidate.Value) &&
                PropertyCouldMatchPrivateSlot(
                    PropertyMultiname(candidate.Value),
                    property))
            .ToList();
        if (possible_writes.Count != 1 ||
            !ReferenceEquals(possible_writes[0].Method, constructor.Method) ||
            !constructor.Operations.TryGetValue(
                possible_writes[0].Instruction,
                out Avm2DataFlowOperation? store) ||
            store.Unreachable ||
            constructor.Code[store.Instruction] is not
                (SetPropertyIns or InitPropertyIns) ||
            !IsSameExactPrivateProperty(
                property,
                PropertyMultiname(constructor.Code[store.Instruction]),
                property.Pool.ABC) ||
            store.Inputs.Count != 2 ||
            !ValueIsExactlyThis(
                constructor,
                store.Inputs[0],
                property,
                new HashSet<string>(StringComparer.Ordinal)) ||
            !StoreDominatesConstructorReturns(constructor, store) ||
            !ConstructorBodyIsSafe(
                constructor,
                owner,
                property,
                store))
        {
            return null;
        }

        PointsToResult stored = ResolveValue(
            constructor,
            store.Inputs[^1],
            new HashSet<string>(visited, StringComparer.Ordinal),
            depth + 1);
        if (!stored.Exhaustive ||
            stored.Types.Count != 1 ||
            stored.Outcomes.Count != 0 ||
            stored.Types[0].Receiver != ReceiverKind.Instance ||
            stored.Types[0].SelectionKind != "ConstructedType" ||
            !stored.Types[0].Exhaustive ||
            stored.Types[0].Conditions.Count != 0)
        {
            return null;
        }

        EffectiveSlotLayout? layout = SlotLayout(owner, ReceiverKind.Instance);
        if (layout is null)
            return null;
        KeyValuePair<int, ASTrait> slot_entry = layout.Slots
            .SingleOrDefault(value => ReferenceEquals(value.Value, slot));
        if (slot_entry.Key <= 0 || slot_entry.Value is null)
            return null;
        int slot_index = slot_entry.Key;

        constructor_private_write_suppression++;
        try
        {
            if (HasPossibleSetSlotAlias(owner, slot_index))
                return null;
        }
        finally
        {
            constructor_private_write_suppression--;
        }

        PointsTo value = stored.Types[0];
        return Result(
            [
                new PointsTo
                {
                    Binding = value.Binding,
                    Receiver = value.Receiver,
                    SelectionKind = value.SelectionKind,
                    SelectorExpression = value.SelectorExpression,
                    Conditions = value.Conditions,
                    Evidence =
                    [
                        .. value.Evidence,
                        Evidence(
                            "ConstructorPrivateWrite",
                            "Exact",
                            constructor,
                            store.Instruction,
                            store.Offset,
                            Qualified(property),
                            value.Binding.AbcIndex)
                    ],
                    Exhaustive = true
                }
            ],
            [],
            true,
            true);
    }

    static bool ConstructorContextIsComplete(MethodContext context) =>
        InitializerContextIsComplete(context) &&
        !context.Analysis.ControlFlow.HasLoop;

    static bool InitializerContextIsComplete(MethodContext context) =>
        context.Method.Body is not null &&
        context.Method.Body.Exceptions.Count == 0 &&
        context.Analysis.ControlFlow.Complete &&
        context.Flow.Complete &&
        context.Code.Count == context.Flow.Operations.Count &&
        context.Flow.Operations.All(operation => operation.Instruction >= 0);

    static bool SameBlockContextIsComplete(MethodContext context) =>
        context.Analysis.ControlFlow.Complete &&
        context.Flow.Complete &&
        context.Code.Count == context.Flow.Operations.Count &&
        context.Flow.Operations.All(operation =>
            operation.Instruction >= 0 &&
            operation.Instruction < context.Code.Count) &&
        context.Flow.Operations
            .Select(operation => operation.Instruction)
            .Distinct()
            .Count() == context.Code.Count;

    bool LoadReadsOwnerThis(
        MethodContext context,
        ASMultiname property,
        Avm2DataFlowOperation load,
        string? receiver)
    {
        if (receiver is not null)
        {
            return ValueIsExactlyThis(
                context,
                receiver,
                property,
                new HashSet<string>(StringComparer.Ordinal));
        }
        return load.Instruction >= 0 &&
            load.Instruction < context.Code.Count &&
            context.Code[load.Instruction] is GetLexIns &&
            load.ScopeBefore.Count > 0 &&
            ValueIsExactlyThis(
                context,
                load.ScopeBefore[^1],
                property,
                new HashSet<string>(StringComparer.Ordinal));
    }

    bool ValueIsExactlyThis(
        MethodContext context,
        string value,
        ASMultiname property,
        HashSet<string> visited)
    {
        if (value == "v_entry_local_0")
            return true;
        if (!visited.Add(value))
            return false;
        if (context.Phis.TryGetValue(value, out Avm2DataFlowPhi? phi))
        {
            return phi.Inputs.Count > 0 &&
                phi.Inputs.All(input => ValueIsExactlyThis(
                    context,
                    input.Value,
                    property,
                    new HashSet<string>(visited, StringComparer.Ordinal)));
        }
        if (!context.Producers.TryGetValue(value, out Avm2DataFlowOperation? producer) ||
            producer.Instruction < 0 ||
            producer.Instruction >= context.Code.Count)
        {
            return false;
        }

        ASInstruction instruction = context.Code[producer.Instruction];
        if (instruction is FindPropertyIns or FindPropStrictIns)
        {
            return IsSameExactPrivateProperty(
                    property,
                    PropertyMultiname(instruction),
                    property.Pool.ABC) &&
                producer.ScopeBefore.Count > 0 &&
                ValueIsExactlyThis(
                    context,
                    producer.ScopeBefore[^1],
                    property,
                    new HashSet<string>(visited, StringComparer.Ordinal));
        }
        if (instruction.OP is
            OPCode.Coerce_a or
            OPCode.CheckFilter or
            OPCode.Convert_o)
        {
            return producer.Inputs.Count == 1 &&
                ValueIsExactlyThis(
                    context,
                    producer.Inputs[0],
                    property,
                    new HashSet<string>(visited, StringComparer.Ordinal));
        }
        return false;
    }

    bool ValueCouldBeThis(
        MethodContext context,
        string value,
        HashSet<string> visited)
    {
        const string entry_local = "v_entry_local_";
        if (value.StartsWith(entry_local, StringComparison.Ordinal) &&
            int.TryParse(
                value.AsSpan(entry_local.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int register))
        {
            if (register == 0)
                return true;
            if (register > context.Method.Parameters.Count)
                return false;
            return MethodContainer(context) is not ASInstance instance ||
                Owner(instance) is not TypeBinding owner ||
                !owner.Instance.Flags.HasFlag(ClassFlags.Final);
        }
        if (value.StartsWith("v_entry_scope_", StringComparison.Ordinal))
            return false;
        TypeBinding? scope_owner = null;
        if (context.ExactReceiver is Avm2ExactReceiver exact_receiver)
        {
            types_by_instance.TryGetValue(
                exact_receiver.RuntimeType,
                out scope_owner);
        }
        scope_owner ??= Owner(MethodContainer(context));
        if (context.Values.TryGetValue(
                value,
                out Avm2DataFlowValue? model) &&
            model.Kind == "DeclaringScope" &&
            scope_owner is not null &&
            ExactScopeValueIsDisjoint(
                context,
                value,
                scope_owner))
        {
            return false;
        }
        if (!visited.Add(value))
            return true;
        if (context.Phis.TryGetValue(value, out Avm2DataFlowPhi? phi))
        {
            return phi.Inputs.Any(input => ValueCouldBeThis(
                context,
                input.Value,
                new HashSet<string>(visited, StringComparer.Ordinal)));
        }
        if (!context.Producers.TryGetValue(value, out Avm2DataFlowOperation? producer) ||
            producer.Instruction < 0 ||
            producer.Instruction >= context.Code.Count)
        {
            return true;
        }

        ASInstruction instruction = context.Code[producer.Instruction];
        if (instruction is FindPropertyIns or FindPropStrictIns)
            return ScopeLookupCouldReturnThis(context, instruction, producer, visited);
        if (instruction is GetLexIns)
            return ScopeLookupCouldReturnThis(context, instruction, producer, visited);
        if (instruction is GetPropertyIns or GetSuperIns or GetSlotIns)
            return producer.Inputs.Count > 0 &&
                ValueCouldBeThis(
                    context,
                    producer.Inputs[0],
                    new HashSet<string>(visited, StringComparer.Ordinal));
        if (instruction.OP is OPCode.GetScopeObject or OPCode.GetOuterScope)
        {
            return producer.Inputs.Count != 1 ||
                ValueCouldBeThis(
                context,
                producer.Inputs[0],
                new HashSet<string>(visited, StringComparer.Ordinal));
        }
        if (instruction.OP is
            OPCode.Coerce or
            OPCode.Coerce_a or
            OPCode.Coerce_o or
            OPCode.AsType or
            OPCode.AsTypeLate or
            OPCode.CheckFilter or
            OPCode.Convert_o)
        {
            return producer.Inputs.Any(input => ValueCouldBeThis(
                context,
                input,
                new HashSet<string>(visited, StringComparer.Ordinal)));
        }
        return producer.Inputs.Any(input => ValueCouldBeThis(
            context,
            input,
            new HashSet<string>(visited, StringComparer.Ordinal)));
    }

    bool ScopeLookupCouldReturnThis(
        MethodContext context,
        ASInstruction instruction,
        Avm2DataFlowOperation operation,
        HashSet<string> visited)
    {
        if (!operation.ScopeBefore.Any(value => ValueCouldBeThis(
                context,
                value,
                new HashSet<string>(visited, StringComparer.Ordinal))))
        {
            return false;
        }

        ASMultiname? property = PropertyMultiname(instruction);
        if (property is null ||
            property.IsRuntime ||
            MethodContainer(context) is not ASInstance instance ||
            Owner(instance) is not TypeBinding owner)
        {
            return true;
        }

        bool private_property = IsPrivate(property);
        TypeBinding? exact_runtime = null;
        if (context.ExactReceiver is Avm2ExactReceiver exact_receiver &&
            !exact_receiver.Static)
        {
            types_by_instance.TryGetValue(
                exact_receiver.RuntimeType,
                out exact_runtime);
        }
        if (exact_runtime is null &&
            !private_property &&
            !owner.Instance.Flags.HasFlag(ClassFlags.Final))
        {
            return true;
        }
        return all_types
            .Where(candidate =>
                exact_runtime is not null
                    ? ReferenceEquals(candidate.Instance, exact_runtime.Instance)
                    : ReferenceEquals(candidate.Abc, owner.Abc) &&
                        ReferenceEquals(candidate.Instance, owner.Instance) ||
                        IsStrictSubtype(candidate, owner))
            .Any(candidate =>
                !private_property &&
                    !candidate.Instance.Flags.HasFlag(ClassFlags.Sealed) &&
                    IsStandardPublicImplementation(property) ||
                FindTraits(
                        candidate,
                        ReceiverKind.Instance,
                        property)
                    .Any());
    }

    static bool StoreDominatesConstructorReturns(
        MethodContext context,
        Avm2DataFlowOperation store)
    {
        List<Avm2DataFlowOperation> returns = context.Flow.Operations
            .Where(operation =>
                !operation.Unreachable &&
                operation.Opcode is
                    nameof(OPCode.ReturnVoid) or
                    nameof(OPCode.ReturnValue))
            .ToList();
        if (returns.Count == 0)
            return false;

        Dictionary<int, Avm2DominatorInventory> dominators =
            context.Analysis.ControlFlow.Dominators
                .ToDictionary(value => value.Block);
        foreach (Avm2DataFlowOperation return_operation in returns)
        {
            if (return_operation.Block == store.Block)
            {
                if (store.Instruction >= return_operation.Instruction)
                    return false;
                continue;
            }
            if (!dominators.TryGetValue(
                    return_operation.Block,
                    out Avm2DominatorInventory? return_dominators) ||
                !return_dominators.Dominators.Contains(store.Block))
            {
                return false;
            }
        }

        HashSet<int> outgoing = context.Analysis.ControlFlow.Edges
            .Where(edge => edge.ToBlock.HasValue)
            .Select(edge => edge.FromBlock)
            .ToHashSet();
        return context.Analysis.ControlFlow.Blocks
            .Where(block => block.Reachable && !outgoing.Contains(block.Id))
            .All(block =>
                block.LastInstruction >= 0 &&
                block.LastInstruction < context.Code.Count &&
                context.Code[block.LastInstruction].OP is
                    OPCode.ReturnVoid or
                    OPCode.ReturnValue or
                    OPCode.Throw);
    }

    bool ConstructorBodyIsSafe(
        MethodContext context,
        TypeBinding owner,
        ASMultiname target_property,
        Avm2DataFlowOperation store)
    {
        foreach (Avm2DataFlowOperation operation in context.Flow.Operations)
        {
            if (operation.Unreachable ||
                operation.Instruction == store.Instruction ||
                operation.Instruction < 0 ||
                operation.Instruction >= context.Code.Count)
            {
                continue;
            }

            ASInstruction instruction = context.Code[operation.Instruction];
            bool captures_this_scope =
                instruction.OP is OPCode.NewFunction or OPCode.NewClass &&
                operation.ScopeBefore.Any(value => ValueCouldBeThis(
                    context,
                    value,
                    new HashSet<string>(StringComparer.Ordinal)));
            if (captures_this_scope)
                return false;

            if (instruction.OP == OPCode.ConstructSuper)
            {
                if (operation.Inputs.Count == 0 ||
                    !ValueIsExactlyThis(
                        context,
                        operation.Inputs[0],
                        target_property,
                        new HashSet<string>(StringComparer.Ordinal)))
                {
                    return false;
                }
                if (operation.Inputs.Skip(1).Any(value => ValueCouldBeThis(
                    context,
                    value,
                    new HashSet<string>(StringComparer.Ordinal))))
                {
                    return false;
                }
                continue;
            }

            if (instruction is SetPropertyIns or InitPropertyIns)
            {
                if (operation.Inputs.Count != 2)
                    return false;
                bool receiver_could_be_this = ValueCouldBeThis(
                    context,
                    operation.Inputs[0],
                    new HashSet<string>(StringComparer.Ordinal));
                bool value_could_be_this = ValueCouldBeThis(
                    context,
                    operation.Inputs[^1],
                    new HashSet<string>(StringComparer.Ordinal));
                if (value_could_be_this)
                {
                    return false;
                }
                ASMultiname? written_property = PropertyMultiname(instruction);
                if (receiver_could_be_this &&
                    (written_property is null ||
                        !ValueIsExactlyThis(
                            context,
                            operation.Inputs[0],
                            written_property,
                            new HashSet<string>(StringComparer.Ordinal)) ||
                        written_property.Kind is not (
                            MultinameKind.QName or MultinameKind.QNameA) ||
                        !IsDirectDataSlot(owner, written_property)))
                {
                    return false;
                }
                continue;
            }

            if (instruction is FindPropertyIns or FindPropStrictIns)
            {
                bool scope_could_select_this = ScopeLookupCouldReturnThis(
                    context,
                    instruction,
                    operation,
                    new HashSet<string>(StringComparer.Ordinal));
                string? top_scope = operation.ScopeBefore.LastOrDefault();
                if (scope_could_select_this &&
                    (PropertyMultiname(instruction) is not ASMultiname property ||
                        property.Kind is not (
                            MultinameKind.QName or MultinameKind.QNameA) ||
                        top_scope is null ||
                        ScopeWasPushedWith(
                            context,
                            operation,
                            operation.ScopeBefore.Count - 1,
                            top_scope) ||
                        !ValueIsExactlyThis(
                            context,
                            top_scope,
                            property,
                            new HashSet<string>(StringComparer.Ordinal)) ||
                        !IsDirectDataSlot(owner, property)))
                {
                    return false;
                }
                continue;
            }

            if (instruction is GetLexIns)
            {
                string? top_scope = operation.ScopeBefore.LastOrDefault();
                if (ScopeLookupCouldReturnThis(
                        context,
                        instruction,
                        operation,
                        new HashSet<string>(StringComparer.Ordinal)) &&
                    (PropertyMultiname(instruction) is not ASMultiname property ||
                        property.Kind is not (
                            MultinameKind.QName or MultinameKind.QNameA) ||
                        top_scope is null ||
                        ScopeWasPushedWith(
                            context,
                            operation,
                            operation.ScopeBefore.Count - 1,
                            top_scope) ||
                        !ValueIsExactlyThis(
                            context,
                            top_scope,
                            property,
                            new HashSet<string>(StringComparer.Ordinal)) ||
                        !IsDirectDataSlot(owner, property)))
                {
                    return false;
                }
                continue;
            }

            if (instruction is GetPropertyIns)
            {
                bool receiver_could_be_this =
                    operation.Inputs.Count > 0 &&
                    ValueCouldBeThis(
                        context,
                        operation.Inputs[0],
                        new HashSet<string>(StringComparer.Ordinal));
                if (receiver_could_be_this &&
                    (PropertyMultiname(instruction) is not ASMultiname property ||
                        property.Kind is not (
                            MultinameKind.QName or MultinameKind.QNameA) ||
                        !ValueIsExactlyThis(
                            context,
                            operation.Inputs[0],
                            property,
                            new HashSet<string>(StringComparer.Ordinal)) ||
                        !IsDirectDataSlot(owner, property)))
                {
                    return false;
                }
                continue;
            }

            bool consumes_this = operation.Inputs.Any(value => ValueCouldBeThis(
                context,
                value,
                new HashSet<string>(StringComparer.Ordinal)));
            if (consumes_this &&
                !ConstructorThisUseIsTransparent(instruction))
            {
                return false;
            }
        }
        return true;
    }

    static bool ConstructorThisUseIsTransparent(ASInstruction instruction) =>
        instruction.OP is
            OPCode.GetLocal or
            OPCode.GetLocal_0 or
            OPCode.GetLocal_1 or
            OPCode.GetLocal_2 or
            OPCode.GetLocal_3 or
            OPCode.SetLocal or
            OPCode.SetLocal_0 or
            OPCode.SetLocal_1 or
            OPCode.SetLocal_2 or
            OPCode.SetLocal_3 or
            OPCode.Kill or
            OPCode.Dup or
            OPCode.Swap or
            OPCode.Pop or
            OPCode.PushScope or
            OPCode.PopScope or
            OPCode.GetSlot or
            OPCode.GetScopeObject or
            OPCode.GetOuterScope or
            OPCode.Coerce or
            OPCode.Coerce_a or
            OPCode.Coerce_o or
            OPCode.AsType or
            OPCode.AsTypeLate or
            OPCode.CheckFilter or
            OPCode.Convert_o;

    bool IsDirectDataSlot(
        TypeBinding owner,
        ASMultiname property)
    {
        List<TraitBinding> traits = FindTraits(
                owner,
                ReceiverKind.Instance,
                property)
            .ToList();
        return traits.Count == 1 &&
            traits[0].Trait.Kind is TraitKind.Slot or TraitKind.Constant;
    }

    bool EnsureGlobalInstructionIndex()
    {
        if (global_instructions is not null)
            return global_instructions_complete;

        var instructions = new List<GlobalInstruction>();
        bool complete = true;
        foreach (ABCFile abc in method_bindings.Abcs)
        {
            foreach (ASMethod method in abc.Methods)
            {
                if (method.Body is null)
                    continue;
                try
                {
                    List<ASInstruction> code = method.Body.ParseCode().ToList();
                    for (int index = 0; index < code.Count; index++)
                    {
                        ASInstruction instruction = code[index];
                        if (IsPropertyWrite(instruction) ||
                            instruction is
                                SetSlotIns or
                                PushNamespaceIns or
                                NewFunctionIns or
                                CallStaticIns)
                        {
                            instructions.Add(new GlobalInstruction(
                                method,
                                index,
                                instruction));
                        }
                    }
                }
                catch
                {
                    complete = false;
                }
            }
        }
        global_instructions = instructions;
        global_instructions_complete = complete;
        return complete;
    }

    internal bool HasReachableAlternateMethodReference(
        ASMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (!EnsureGlobalInstructionIndex())
            return true;
        if (reachable_alternate_method_references.TryGetValue(
                method,
                out bool cached))
        {
            return cached;
        }

        int method_index = method.ABC.Methods.IndexOf(
            method);
        if (method_index < 0)
            return true;
        foreach (GlobalInstruction candidate in global_instructions!)
        {
            int referenced = candidate.Value switch
            {
                NewFunctionIns function =>
                    function.MethodIndex,
                CallStaticIns call =>
                    call.MethodIndex,
                _ => -1
            };
            if (!ReferenceEquals(
                    candidate.Method.ABC,
                    method.ABC) ||
                referenced != method_index)
            {
                continue;
            }
            if (ReferenceSiteReachable(candidate))
            {
                reachable_alternate_method_references.Add(
                    method,
                    true);
                return true;
            }
        }
        reachable_alternate_method_references.Add(
            method,
            false);
        return false;
    }

    bool ReferenceSiteReachable(
        GlobalInstruction candidate)
    {
        List<Avm2MethodBinding?> bindings = method_bindings
            .GetBindings(candidate.Method)
            .Where(binding => binding.Resolved)
            .Cast<Avm2MethodBinding?>()
            .ToList();
        bindings.Add(null);
        foreach (Avm2MethodBinding? binding in bindings)
        {
            MethodContext? context = Context(
                candidate.Method,
                binding);
            if (context is null ||
                !context.Analysis.ControlFlow.Complete ||
                !context.Flow.Complete ||
                !context.Operations.TryGetValue(
                    candidate.Instruction,
                    out Avm2DataFlowOperation? operation))
            {
                return true;
            }
            if (!operation.Unreachable)
                return true;
        }
        return false;
    }

    bool ProvesClosedSlotValueSet(
        TypeBinding slot_owner,
        TypeBinding runtime,
        ASTrait slot,
        Avm2MethodBinding writer,
        int writer_instruction,
        int slot_index)
    {
        ClosedSlotWriteInventory inventory =
            ClosedSlotInventory(
                slot_owner,
                slot,
                slot_index);
        int allowed_writes = 0;
        foreach (GlobalInstruction candidate in
            inventory.PropertyWrites)
        {
            if (AllowedSlotWrite(
                    candidate,
                    slot.QName,
                    writer,
                    writer_instruction))
            {
                allowed_writes++;
                continue;
            }
            if (!PropertyWritePreservesSlotValueSet(
                    candidate,
                    runtime))
            {
                return false;
            }
        }
        foreach (GlobalInstruction candidate in
            inventory.SetSlotWrites)
        {
            if (!SetSlotWritePreservesSlotValueSet(
                    candidate,
                    runtime,
                    slot,
                    slot_index))
            {
                return false;
            }
        }
        return allowed_writes == 1;
    }

    ClosedSlotWriteInventory ClosedSlotInventory(
        TypeBinding slot_owner,
        ASTrait slot,
        int slot_index)
    {
        var key = new ClosedSlotInventoryKey(
            slot_owner.Instance,
            slot,
            slot_index);
        if (closed_slot_inventories.TryGetValue(
                key,
                out ClosedSlotWriteInventory? cached))
        {
            return cached;
        }

        var property_writes = new List<GlobalInstruction>();
        var set_slot_writes = new List<GlobalInstruction>();
        foreach (GlobalInstruction candidate in global_instructions!)
        {
            if (IsPropertyWrite(candidate.Value) &&
                PropertyCouldMatchStorageSlot(
                    PropertyMultiname(candidate.Value),
                    slot.QName,
                    slot_owner))
            {
                property_writes.Add(candidate);
                continue;
            }
            if (candidate.Value is SetSlotIns set_slot &&
                set_slot.SlotIndex == slot_index &&
                SetSlotCouldWriteStorageFamily(
                    candidate,
                    slot_owner,
                    slot,
                    slot_index))
            {
                set_slot_writes.Add(candidate);
            }
        }

        var inventory = new ClosedSlotWriteInventory
        {
            PropertyWrites = property_writes,
            SetSlotWrites = set_slot_writes
        };
        closed_slot_inventories.Add(key, inventory);
        return inventory;
    }

    bool SetSlotCouldWriteStorageFamily(
        GlobalInstruction candidate,
        TypeBinding slot_owner,
        ASTrait slot,
        int slot_index)
    {
        List<Avm2MethodBinding?> bindings = method_bindings
            .GetBindings(candidate.Method)
            .Where(binding => binding.Resolved)
            .Cast<Avm2MethodBinding?>()
            .ToList();
        bindings.Add(null);

        foreach (Avm2MethodBinding? binding in bindings)
        {
            MethodContext? context = Context(
                candidate.Method,
                binding);
            if (context is null ||
                !context.Analysis.ControlFlow.Complete ||
                !context.Operations.TryGetValue(
                    candidate.Instruction,
                    out Avm2DataFlowOperation? operation))
            {
                return true;
            }
            if (operation.Unreachable)
                continue;
            if (operation.Inputs.Count != 2)
                return true;
            string receiver = operation.Inputs[0];
            if (!context.Flow.Complete)
            {
                if (ReceiverOriginIsDisjoint(
                        context,
                        receiver,
                        slot_owner))
                {
                    continue;
                }
                return true;
            }
            if (ExactSlotReceiverCouldAlias(
                    context,
                    receiver,
                    slot_owner,
                    slot,
                    slot_index))
            {
                return true;
            }
        }
        return false;
    }

    bool AllowedSlotWrite(
        GlobalInstruction candidate,
        ASMultiname property,
        Avm2MethodBinding writer,
        int writer_instruction)
    {
        if (!ReferenceEquals(candidate.Method, writer.Method) ||
            candidate.Instruction != writer_instruction ||
            candidate.Value is not (SetPropertyIns or InitPropertyIns) ||
            PropertyMultiname(candidate.Value) is not
                ASMultiname written ||
            written.Kind is not (
                MultinameKind.QName or
                MultinameKind.QNameA) ||
            !PropertiesMatchIndexed(
                property,
                property.Pool.ABC,
                written,
                written.Pool.ABC))
        {
            return false;
        }
        List<Avm2MethodBinding> bindings = method_bindings
            .GetBindings(candidate.Method)
            .Where(binding => binding.Resolved)
            .Take(2)
            .ToList();
        return bindings.Count == 1 &&
            ReferenceEquals(bindings[0], writer);
    }

    bool PropertyWritePreservesSlotValueSet(
        GlobalInstruction candidate,
        TypeBinding owner)
    {
        List<Avm2MethodBinding?> bindings = method_bindings
            .GetBindings(candidate.Method)
            .Where(binding => binding.Resolved)
            .Cast<Avm2MethodBinding?>()
            .ToList();
        bool alternate =
            HasReachableAlternateMethodReference(candidate.Method);
        if (bindings.Count == 0 && !alternate)
            return true;
        if (alternate)
            bindings.Add(null);

        foreach (Avm2MethodBinding? binding in bindings)
        {
            MethodContext? context = Context(
                candidate.Method,
                binding);
            if (context is null ||
                !context.Analysis.ControlFlow.Complete ||
                !context.Operations.TryGetValue(
                    candidate.Instruction,
                    out Avm2DataFlowOperation? operation))
            {
                return false;
            }
            if (operation.Unreachable)
                continue;
            if (operation.Inputs.Count < 2)
                return false;
            string receiver = operation.Inputs[0];
            if (!context.Flow.Complete)
            {
                if (ReceiverOriginIsDisjoint(
                        context,
                        receiver,
                        owner))
                {
                    continue;
                }
                return false;
            }
            if (StorageReceiverCouldAlias(
                    context,
                    receiver,
                    owner) &&
                !ValueIsOnlyNull(
                    context,
                    operation.Inputs[^1]))
            {
                return false;
            }
        }
        return true;
    }

    bool SetSlotWritePreservesSlotValueSet(
        GlobalInstruction candidate,
        TypeBinding owner,
        ASTrait slot,
        int slot_index)
    {
        List<Avm2MethodBinding?> bindings = method_bindings
            .GetBindings(candidate.Method)
            .Where(binding => binding.Resolved)
            .Cast<Avm2MethodBinding?>()
            .ToList();
        bool alternate =
            HasReachableAlternateMethodReference(candidate.Method);
        if (bindings.Count == 0 && !alternate)
            return true;
        if (alternate)
            bindings.Add(null);

        foreach (Avm2MethodBinding? binding in bindings)
        {
            MethodContext? context = Context(
                candidate.Method,
                binding);
            if (context is null ||
                !context.Analysis.ControlFlow.Complete ||
                !context.Operations.TryGetValue(
                    candidate.Instruction,
                    out Avm2DataFlowOperation? operation))
            {
                return false;
            }
            if (operation.Unreachable)
                continue;
            if (operation.Inputs.Count != 2)
                return false;
            string receiver = operation.Inputs[0];
            if (!context.Flow.Complete)
            {
                if (ReceiverOriginIsDisjoint(
                        context,
                        receiver,
                        owner))
                {
                    continue;
                }
                return false;
            }
            if (ExactSlotReceiverCouldAlias(
                    context,
                    receiver,
                    owner,
                    slot,
                    slot_index) &&
                !ValueIsOnlyNull(
                    context,
                    operation.Inputs[^1]))
            {
                return false;
            }
        }
        return true;
    }

    bool ValueIsOnlyNull(
        MethodContext context,
        string value)
    {
        if (!context.VerifierValid)
        {
            return false;
        }
        PointsToResult resolved = ResolveValue(
            context,
            value,
            new HashSet<string>(StringComparer.Ordinal),
            0);
        return resolved.Exhaustive &&
            resolved.Types.Count == 0 &&
            resolved.Outcomes.Count > 0 &&
            resolved.Outcomes.All(outcome =>
                outcome.Kind == "Null");
    }

    bool StorageReceiverCouldAlias(
        MethodContext context,
        string receiver,
        TypeBinding owner)
    {
        if (!context.VerifierValid)
            return true;
        if (context.Binding is null &&
            ValueCouldBeThis(
                context,
                receiver,
                new HashSet<string>(StringComparer.Ordinal)))
        {
            return true;
        }
        PointsToResult resolved = ResolveValue(
            context,
            receiver,
            new HashSet<string>(StringComparer.Ordinal),
            0);
        if (resolved.Types.Count > 0)
        {
            bool possible = resolved.Types.Any(value =>
                value.Receiver == ReceiverKind.Instance &&
                TypeCouldCarryOwnerSlot(value, owner));
            if (possible)
                return true;
            if (resolved.Exhaustive)
                return false;
        }
        else if (resolved.Exhaustive &&
            resolved.Outcomes.Count > 0)
        {
            return false;
        }
        return !ReceiverOriginIsDisjoint(
            context,
            receiver,
            owner);
    }

    bool ExactSlotReceiverCouldAlias(
        MethodContext context,
        string receiver,
        TypeBinding owner,
        ASTrait slot,
        int slot_index)
    {
        if (!context.VerifierValid)
            return true;
        PointsToResult resolved = ResolveValue(
            context,
            receiver,
            new HashSet<string>(StringComparer.Ordinal),
            0);
        foreach (PointsTo value in resolved.Types)
        {
            if (value.Receiver != ReceiverKind.Instance)
                continue;
            if (!value.Exhaustive)
            {
                if (!DeclaredTypeIsDisjointFromOwner(
                        value.Binding,
                        owner))
                {
                    return true;
                }
                continue;
            }
            if (!CouldBeSubtypeOrSame(
                    value.Binding,
                    owner))
            {
                continue;
            }
            EffectiveSlotLayout? layout = SlotLayout(
                value.Binding,
                ReceiverKind.Instance);
            if (layout is null ||
                !layout.Slots.TryGetValue(
                    slot_index,
                    out ASTrait? runtime_slot) ||
                !ReferenceEquals(runtime_slot, slot))
            {
                return true;
            }
            return true;
        }
        if (resolved.Exhaustive)
            return false;
        return !ReceiverOriginIsDisjoint(
            context,
            receiver,
            owner);
    }

    bool PropertyCouldMatchStorageSlot(
        ASMultiname? candidate,
        ASMultiname property,
        TypeBinding owner)
    {
        if (candidate is null ||
            property.Kind is not (
                MultinameKind.QName or
                MultinameKind.QNameA))
        {
            return false;
        }
        switch (candidate.Kind)
        {
            case MultinameKind.QName:
            case MultinameKind.QNameA:
                return PropertiesMatchIndexed(
                    candidate,
                    candidate.Pool.ABC,
                    property,
                    property.Pool.ABC) ||
                    NamesMatch(candidate, property) &&
                        ProtectedNamespaceCouldAlias(
                            candidate.Namespace,
                            property,
                            owner);
            case MultinameKind.Multiname:
            case MultinameKind.MultinameA:
                return NamesMatch(candidate, property) &&
                    NamespaceSetContainsStorageSlot(
                        candidate,
                        property,
                        owner);
            case MultinameKind.MultinameL:
            case MultinameKind.MultinameLA:
                return NamespaceSetContainsStorageSlot(
                    candidate,
                    property,
                    owner);
            case MultinameKind.RTQName:
            case MultinameKind.RTQNameA:
                return NamesMatch(candidate, property) &&
                    RuntimeNamespaceCouldMatch(property);
            case MultinameKind.RTQNameL:
            case MultinameKind.RTQNameLA:
                return RuntimeNamespaceCouldMatch(property);
            default:
                return false;
        }
    }

    internal bool PropertyCouldMatchStorageSlot(
        ASMultiname? candidate,
        ASMultiname property,
        ASInstance owner)
    {
        TypeBinding? binding = Owner(owner);
        return binding is null ||
            PropertyCouldMatchStorageSlot(
                candidate,
                property,
                binding);
    }

    internal bool StorageAccessPropertyNameIsDisjoint(
        ASMethod method,
        int instruction_index,
        ASMultiname candidate,
        ASMultiname property)
    {
        if (!Avm2MethodAnalyzer.TryGetStaticName(
                property,
                out string property_name))
        {
            return false;
        }
        List<Avm2MethodBinding?> bindings = method_bindings
            .GetBindings(method)
            .Where(binding => binding.Resolved)
            .Cast<Avm2MethodBinding?>()
            .ToList();
        bool alternate =
            HasReachableAlternateMethodReference(method);
        if (bindings.Count == 0 && !alternate)
            return true;
        if (alternate)
            bindings.Add(null);
        foreach (Avm2MethodBinding? binding in bindings)
        {
            MethodContext? context = Context(
                method,
                binding);
            if (context is null ||
                !context.VerifierValid ||
                !context.Analysis.ControlFlow.Complete ||
                !context.Flow.Complete ||
                !context.Operations.TryGetValue(
                    instruction_index,
                    out Avm2DataFlowOperation? operation))
            {
                return false;
            }
            if (operation.Unreachable)
                continue;
            if (!candidate.IsNameNeeded)
            {
                if (!Avm2MethodAnalyzer.TryGetStaticName(
                        candidate,
                        out string candidate_name) ||
                    string.Equals(
                        candidate_name,
                        property_name,
                        StringComparison.Ordinal))
                {
                    return false;
                }
                continue;
            }
            int receiver_values = context.Code[instruction_index].OP is
                OPCode.FindProperty or
                OPCode.FindPropStrict or
                OPCode.GetLex
                    ? 0
                    : 1;
            int name_index = receiver_values +
                (candidate.IsNamespaceNeeded ? 1 : 0);
            if (name_index >= operation.Inputs.Count ||
                !RuntimePropertyNameIsDisjoint(
                    context,
                    operation.Inputs[name_index],
                    property_name,
                    new HashSet<string>(
                        StringComparer.Ordinal)))
            {
                return false;
            }
        }
        return true;
    }

    static bool RuntimePropertyNameIsDisjoint(
        MethodContext context,
        string value,
        string property_name,
        HashSet<string> visited)
    {
        if (!visited.Add(value))
            return false;
        if (context.Producers.TryGetValue(
                value,
                out Avm2DataFlowOperation? producer) &&
            producer.Instruction >= 0 &&
            producer.Instruction < context.Code.Count &&
            context.Code[producer.Instruction] is
                PushStringIns pushed)
        {
            return !string.Equals(
                pushed.Value,
                property_name,
                StringComparison.Ordinal);
        }
        if (context.Phis.TryGetValue(
                value,
                out Avm2DataFlowPhi? phi))
        {
            return phi.Inputs.Count > 0 &&
                phi.Inputs.All(input =>
                    RuntimePropertyNameIsDisjoint(
                        context,
                        input.Value,
                        property_name,
                        new HashSet<string>(
                            visited,
                            StringComparer.Ordinal)));
        }
        if (!context.Values.TryGetValue(
                value,
                out Avm2DataFlowValue? resolved))
        {
            return false;
        }
        if (resolved.VerifierType.Kind ==
            Avm2VerifierTypeKind.Null)
        {
            return property_name != "null";
        }
        if (resolved.VerifierType.Kind ==
            Avm2VerifierTypeKind.Void)
        {
            return property_name != "undefined";
        }
        if (resolved.VerifierType.Kind !=
            Avm2VerifierTypeKind.Known)
        {
            if (producer is null ||
                producer.Instruction < 0 ||
                producer.Instruction >= context.Code.Count)
            {
                return false;
            }
            OPCode opcode =
                context.Code[producer.Instruction].OP;
            if (opcode is
                OPCode.PushByte or
                OPCode.PushShort or
                OPCode.PushInt or
                OPCode.PushUInt or
                OPCode.PushDouble or
                OPCode.PushNan or
                OPCode.Convert_i or
                OPCode.Convert_u or
                OPCode.Convert_d or
                OPCode.Coerce_i or
                OPCode.Coerce_u or
                OPCode.Coerce_d or
                OPCode.Increment or
                OPCode.Decrement or
                OPCode.Negate or
                OPCode.IncLocal or
                OPCode.DecLocal or
                OPCode.Divide or
                OPCode.Modulo or
                OPCode.Multiply or
                OPCode.Subtract or
                OPCode.Increment_i or
                OPCode.Decrement_i or
                OPCode.Negate_i or
                OPCode.IncLocal_i or
                OPCode.DecLocal_i or
                OPCode.Add_i or
                OPCode.Subtract_i or
                OPCode.Multiply_i or
                OPCode.BitAnd or
                OPCode.BitOr or
                OPCode.BitXor or
                OPCode.LShift or
                OPCode.RShift or
                OPCode.URShift or
                OPCode.Sxi1 or
                OPCode.Sxi8 or
                OPCode.Sxi16 or
                OPCode.Li8 or
                OPCode.Li16 or
                OPCode.Li32 or
                OPCode.Lf32 or
                OPCode.Lf64)
            {
                return NonNumericPropertyName(property_name);
            }
            if (opcode is
                OPCode.PushTrue or
                OPCode.PushFalse or
                OPCode.Convert_b or
                OPCode.Coerce_b or
                OPCode.Not)
            {
                return property_name is not (
                    "true" or
                    "false");
            }
            if (producer.Inputs.Count == 1 &&
                opcode is
                    OPCode.Dup or
                    OPCode.GetLocal or
                    OPCode.SetLocal or
                    OPCode.Coerce_a or
                    OPCode.Coerce_s or
                    OPCode.Convert_s or
                    OPCode.CheckFilter)
            {
                return RuntimePropertyNameIsDisjoint(
                    context,
                    producer.Inputs[0],
                    property_name,
                    visited);
            }
            return false;
        }
        return resolved.VerifierType.Identity switch
        {
            "builtin:boolean" =>
                property_name is not ("true" or "false"),
            "builtin:int" or
            "builtin:uint" or
            "builtin:number" or
            "builtin:float" =>
                NonNumericPropertyName(property_name),
            _ => false
        };
    }

    static bool NonNumericPropertyName(string value) =>
        !double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out _) &&
        value is not (
            "NaN" or
            "Infinity" or
            "-Infinity");

    internal bool StorageAccessReceiverIsDisjoint(
        ASMethod method,
        int instruction_index,
        ASInstance owner)
    {
        TypeBinding? owner_binding = Owner(owner);
        if (owner_binding is null)
            return false;
        List<Avm2MethodBinding?> bindings = method_bindings
            .GetBindings(method)
            .Where(binding => binding.Resolved)
            .Cast<Avm2MethodBinding?>()
            .ToList();
        bool alternate =
            HasReachableAlternateMethodReference(method);
        if (bindings.Count == 0 && !alternate)
            return true;
        if (alternate)
            bindings.Add(null);
        foreach (Avm2MethodBinding? binding in bindings)
        {
            MethodContext? context = Context(
                method,
                binding);
            if (context is null ||
                !context.VerifierValid ||
                !context.Analysis.ControlFlow.Complete ||
                !context.Operations.TryGetValue(
                    instruction_index,
                    out Avm2DataFlowOperation? operation))
            {
                return false;
            }
            if (operation.Unreachable)
                continue;
            ASInstruction instruction =
                context.Code[instruction_index];
            if (instruction.OP is not (
                    OPCode.GetSlot or
                    OPCode.SetSlot or
                    OPCode.GetProperty or
                    OPCode.SetProperty or
                    OPCode.InitProperty or
                    OPCode.DeleteProperty or
                    OPCode.GetSuper or
                    OPCode.SetSuper or
                    OPCode.GetDescendants or
                    OPCode.CallProperty or
                    OPCode.CallPropVoid or
                    OPCode.CallPropLex or
                    OPCode.CallSuper or
                    OPCode.CallSuperVoid or
                    OPCode.ConstructProp) ||
                operation.Inputs.Count == 0)
            {
                return false;
            }
            string receiver = operation.Inputs[0];
            if (ReceiverOriginIsDisjoint(
                    context,
                    receiver,
                    owner_binding))
            {
                continue;
            }
            if (!context.Flow.Complete ||
                StorageReceiverCouldAlias(
                    context,
                    receiver,
                    owner_binding))
            {
                return false;
            }
        }
        return true;
    }

    internal bool StorageValueReceiverIsDisjoint(
        Avm2MethodBinding binding,
        Avm2ExactReceiver? exact_receiver,
        string value,
        ASInstance owner)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentNullException.ThrowIfNull(owner);
        TypeBinding? owner_binding = Owner(owner);
        MethodContext? context = Context(
            binding.Method!,
            binding,
            exact_receiver);
        return owner_binding is not null &&
            context is not null &&
            context.Flow.Complete &&
            context.Analysis.ControlFlow.Complete &&
            context.VerifierValid &&
            !StorageReceiverCouldAlias(
                context,
                value,
                owner_binding);
    }

    internal bool ConstructedBuiltinIsDisjointFromRuntime(
        Avm2MethodBinding binding,
        Avm2ExactReceiver? exact_receiver,
        string value,
        ASInstance runtime)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentNullException.ThrowIfNull(runtime);
        MethodContext? context = Context(
            binding.Method!,
            binding,
            exact_receiver);
        return types_by_instance.TryGetValue(
                runtime,
                out TypeBinding? runtime_binding) &&
            context is not null &&
            context.VerifierValid &&
            context.Flow.Complete &&
            context.Analysis.ControlFlow.Complete &&
            ConstructedBuiltinIsDisjointFromRuntime(
                context,
                value,
                runtime_binding);
    }

    bool ConstructedBuiltinIsDisjointFromRuntime(
        MethodContext context,
        string value,
        TypeBinding runtime)
    {
        if (!context.Producers.TryGetValue(
                value,
                out Avm2DataFlowOperation? producer) ||
            producer.Instruction < 0 ||
            producer.Instruction >= context.Code.Count ||
            context.Code[producer.Instruction] is not
                ConstructPropIns construct)
        {
            return false;
        }
        int receiver_index =
            producer.Inputs.Count - construct.ArgCount -
            (construct.PropertyName.IsNamespaceNeeded ? 1 : 0) -
            (construct.PropertyName.IsNameNeeded ? 1 : 0) - 1;
        if (receiver_index < 0 ||
            receiver_index >= producer.Inputs.Count ||
            !context.Producers.TryGetValue(
                producer.Inputs[receiver_index],
                out Avm2DataFlowOperation? lexical) ||
            lexical.Instruction < 0 ||
            lexical.Instruction >= context.Code.Count ||
            context.Code[lexical.Instruction] is not (
                FindPropertyIns or FindPropStrictIns) ||
            PropertyMultiname(context.Code[lexical.Instruction]) is not
                ASMultiname lexical_property ||
            !PropertiesMatchIndexed(
                lexical_property,
                context.Method.ABC,
                construct.PropertyName,
                context.Method.ABC) ||
            !LexicalBuiltinClassReceiver(
                context,
                lexical,
                construct.PropertyName))
        {
            return false;
        }
        string? builtin =
            Avm2VerifierTypeRegistry.CoreInstanceIdentity(
                construct.PropertyName);
        return builtin is not null &&
            OwnerIsDisjointFromBuiltin(runtime, builtin);
    }

    bool NamespaceSetContainsStorageSlot(
        ASMultiname candidate,
        ASMultiname property,
        TypeBinding owner)
    {
        try
        {
            ASNamespace? property_namespace = property.Namespace;
            ASNamespaceSet? candidate_namespaces = candidate.NamespaceSet;
            if (property_namespace is null || candidate_namespaces is null)
                return true;
            bool private_namespace =
                property_namespace.Kind == NamespaceKind.Private;
            string identity = RuntimeNamespaceIdentity(
                property_namespace);
            return candidate_namespaces.NamespaceIndices.Any(index =>
                index > 0 &&
                index < candidate.Pool.Namespaces.Count &&
                candidate.Pool.Namespaces[index] is ASNamespace candidate_namespace &&
                (private_namespace
                    ? ReferenceEquals(
                            candidate.Pool.ABC,
                            property.Pool.ABC) &&
                        ReferenceEquals(
                            candidate_namespace,
                            property_namespace)
                    : RuntimeNamespaceIdentity(
                            candidate_namespace) ==
                        identity ||
                        ProtectedNamespaceCouldAlias(
                            candidate_namespace,
                            property,
                            owner)));
        }
        catch
        {
            return true;
        }
    }

    bool RuntimeNamespaceCouldMatch(
        ASMultiname property)
    {
        ASNamespace? property_namespace = property.Namespace;
        return property_namespace is null ||
            property_namespace.Kind != NamespaceKind.Private ||
            PrivateNamespaceAvailableAtRuntime(property);
    }

    bool ProtectedNamespaceCouldAlias(
        ASNamespace? candidate,
        ASMultiname property,
        TypeBinding owner)
    {
        if (candidate is null ||
            property.Namespace is not ASNamespace property_namespace ||
            property_namespace.Kind != NamespaceKind.Protected ||
            candidate.Kind != NamespaceKind.Protected ||
            !owner.Instance.Flags.HasFlag(
                ClassFlags.ProtectedNamespace) ||
            RuntimeNamespaceIdentity(
                property_namespace) !=
                RuntimeNamespaceIdentity(
                    owner.Instance.ProtectedNamespace))
        {
            return false;
        }
        string candidate_identity = RuntimeNamespaceIdentity(
            candidate);
        return all_types.Any(value =>
            value.Instance.Flags.HasFlag(
                ClassFlags.ProtectedNamespace) &&
            RuntimeNamespaceIdentity(
                value.Instance.ProtectedNamespace) ==
                    candidate_identity &&
            RuntimeCouldSatisfy(
                value.Instance,
                owner.Instance));
    }

    static bool IsPropertyWrite(ASInstruction instruction) =>
        instruction.OP is
            OPCode.SetProperty or
            OPCode.InitProperty or
            OPCode.SetSuper;

    bool PropertyCouldMatchPrivateSlot(
        ASMultiname? candidate,
        ASMultiname property)
    {
        if (candidate is null ||
            !IsExactPrivateProperty(property, property.Pool.ABC))
        {
            return false;
        }

        switch (candidate.Kind)
        {
            case MultinameKind.QName:
            case MultinameKind.QNameA:
                return IsSameExactPrivateProperty(
                    property,
                    candidate,
                    property.Pool.ABC);
            case MultinameKind.Multiname:
            case MultinameKind.MultinameA:
                return NamesMatch(candidate, property) &&
                    NamespaceSetContainsExactPrivate(candidate, property);
            case MultinameKind.MultinameL:
            case MultinameKind.MultinameLA:
                return NamespaceSetContainsExactPrivate(candidate, property);
            case MultinameKind.RTQName:
            case MultinameKind.RTQNameA:
                return NamesMatch(candidate, property) &&
                    PrivateNamespaceAvailableAtRuntime(property);
            case MultinameKind.RTQNameL:
            case MultinameKind.RTQNameLA:
                return PrivateNamespaceAvailableAtRuntime(property);
            default:
                return false;
        }
    }

    static bool NamespaceSetContainsExactPrivate(
        ASMultiname candidate,
        ASMultiname property)
    {
        if (!ReferenceEquals(candidate.Pool.ABC, property.Pool.ABC))
            return false;
        try
        {
            ASNamespaceSet? candidate_namespaces = candidate.NamespaceSet;
            ASNamespace? property_namespace = property.Namespace;
            if (candidate_namespaces is null || property_namespace is null)
                return false;
            return candidate_namespaces.NamespaceIndices.Any(index =>
                index > 0 &&
                index < candidate.Pool.Namespaces.Count &&
                ReferenceEquals(
                    candidate.Pool.Namespaces[index],
                    property_namespace));
        }
        catch
        {
            return false;
        }
    }

    bool PrivateNamespaceAvailableAtRuntime(ASMultiname property)
    {
        ASNamespace? property_namespace = property.Namespace;
        if (property_namespace is null ||
            property.Pool.ABC is not ABCFile abc)
        {
            return false;
        }
        if (global_instructions!.Any(value =>
            value.Value is PushNamespaceIns pushed &&
            ReferenceEquals(pushed.Namespace, property_namespace)))
        {
            return true;
        }

        IEnumerable<ASTrait> traits_with_defaults =
            abc.Instances.Cast<ASContainer>()
                .Concat(abc.Classes)
                .Concat(abc.Scripts)
                .Concat(abc.MethodBodies)
                .SelectMany(container => container.Traits);
        if (traits_with_defaults.Any(trait =>
            IsNamespaceConstant(trait.ValueKind) &&
            trait.ValueIndex > 0 &&
            trait.ValueIndex < abc.Pool.Namespaces.Count &&
            ReferenceEquals(
                abc.Pool.Namespaces[trait.ValueIndex],
                property_namespace)))
        {
            return true;
        }

        return abc.Methods.SelectMany(method => method.Parameters).Any(parameter =>
            parameter.IsOptional &&
            IsNamespaceConstant(parameter.ValueKind) &&
            parameter.ValueIndex > 0 &&
            parameter.ValueIndex < abc.Pool.Namespaces.Count &&
            ReferenceEquals(
                abc.Pool.Namespaces[parameter.ValueIndex],
                property_namespace));
    }

    static bool IsNamespaceConstant(ConstantKind kind) =>
        kind is
            ConstantKind.Namespace or
            ConstantKind.PackageNamespace or
            ConstantKind.PackageInternalNs or
            ConstantKind.ProtectedNs or
            ConstantKind.ExplicitNamespace or
            ConstantKind.StaticProtectedNs or
            ConstantKind.PrivateNs;

    List<TraitBinding> ExactPropertyTraits(ASMultiname property)
    {
        if (property.Pool.ABC is not ABCFile abc)
            return [];
        if (!exact_private_traits.TryGetValue(
                abc,
                out Dictionary<string, List<TraitBinding>>? indexed))
        {
            indexed = IndexExactPrivateTraits(abc);
            exact_private_traits.Add(abc, indexed);
        }
        string identity =
            Avm2MethodAnalyzer.ExactSymbolIdentity(property);
        return indexed.TryGetValue(
                identity,
                out List<TraitBinding>? matches)
            ? [.. matches]
            : [];
    }

    Dictionary<string, List<TraitBinding>>
        IndexExactPrivateTraits(ABCFile abc)
    {
        int abc_index = AbcIndex(abc);
        var indexed =
            new Dictionary<string, List<TraitBinding>>(
                StringComparer.Ordinal);
        IEnumerable<ASContainer> containers =
            abc.Instances.Cast<ASContainer>()
                .Concat(abc.Classes)
                .Concat(abc.Scripts)
                .Concat(abc.MethodBodies);
        foreach (ASContainer container in containers)
        {
            foreach (ASTrait trait in container.Traits)
            {
                if (!IsExactPrivateProperty(
                        trait.QName,
                        abc))
                {
                    continue;
                }
                string identity =
                    Avm2MethodAnalyzer.ExactSymbolIdentity(
                        trait.QName);
                if (!indexed.TryGetValue(
                        identity,
                        out List<TraitBinding>? values))
                {
                    values = [];
                    indexed.Add(identity, values);
                }
                values.Add(new TraitBinding
                {
                    AbcIndex = abc_index,
                    Container = container,
                    Trait = trait
                });
            }
        }
        return indexed;
    }

    EffectiveSlotLayout? SlotLayout(
        TypeBinding binding,
        ReceiverKind receiver) =>
        BuildSlotLayout(
            receiver == ReceiverKind.Instance
                ? binding.Instance
                : binding.Class,
            receiver == ReceiverKind.Instance);

    EffectiveSlotLayout? BuildSlotLayout(
        ASContainer container,
        bool inherit)
    {
        var key = (container, inherit);
        if (slot_layouts.TryGetValue(key, out EffectiveSlotLayout? cached))
            return cached;
        if (!active_slot_layouts.Add(key))
            return null;

        try
        {
            var slots = new Dictionary<int, ASTrait>();
            int highest = 0;
            int inherited_highest = 0;
            ASInstance? owning_instance = container switch
            {
                ASInstance value => value,
                ASClass value => value.Instance,
                _ => null
            };
            if (owning_instance?.Flags.HasFlag(ClassFlags.Interface) == true &&
                container.Traits.Any(trait => trait.Kind is
                    TraitKind.Slot or
                    TraitKind.Constant or
                    TraitKind.Class))
            {
                slot_layouts[key] = null;
                return null;
            }
            if (inherit && container is ASInstance instance)
            {
                bool builtin_object =
                    IsBuiltinObject(instance.Super) &&
                    FindTypes(instance.Super, instance.ABC).Count == 0;
                List<TypeBinding> parents = builtin_object
                    ? []
                    : FindTypes(instance.Super, instance.ABC)
                        .DistinctBy(value => (value.Abc, value.Instance))
                        .Take(2)
                        .ToList();
                if (!builtin_object && parents.Count != 1)
                {
                    slot_layouts[key] = null;
                    return null;
                }
                if (parents.Count == 1)
                {
                    EffectiveSlotLayout? inherited =
                        BuildSlotLayout(parents[0].Instance, true);
                    if (inherited is null)
                    {
                        slot_layouts[key] = null;
                        return null;
                    }
                    foreach ((int index, ASTrait trait) in inherited.Slots)
                        slots.Add(index, trait);
                    highest = inherited.HighestSlot;
                    if (!ReferenceEquals(parents[0].Abc, instance.ABC) &&
                        highest > 0)
                    {
                        slot_layouts[key] = null;
                        return null;
                    }
                    inherited_highest = highest;
                }
            }

            foreach (ASTrait trait in container.Traits)
            {
                if (trait.Kind == TraitKind.Function)
                {
                    slot_layouts[key] = null;
                    return null;
                }
                if (trait.Kind is not (
                    TraitKind.Slot or
                    TraitKind.Constant or
                    TraitKind.Class))
                {
                    continue;
                }

                if (trait.Id > 0 && trait.Id <= inherited_highest)
                {
                    slot_layouts[key] = null;
                    return null;
                }
                if (trait.Id > container.Traits.Count)
                {
                    slot_layouts[key] = null;
                    return null;
                }
                int slot = trait.Id > 0
                    ? trait.Id
                    : checked(highest + 1);
                if (slot <= 0 || slots.ContainsKey(slot))
                {
                    slot_layouts[key] = null;
                    return null;
                }
                slots.Add(slot, trait);
                highest = Math.Max(highest, slot);
            }

            var layout = new EffectiveSlotLayout
            {
                Slots = slots,
                HighestSlot = highest
            };
            slot_layouts[key] = layout;
            return layout;
        }
        finally
        {
            active_slot_layouts.Remove(key);
        }
    }

    bool HasPossibleSetSlotAlias(
        TypeBinding owner,
        int slot_index)
    {
        foreach (GlobalInstruction candidate in global_instructions!)
        {
            if (candidate.Value is not SetSlotIns set_slot ||
                set_slot.SlotIndex != slot_index)
            {
                continue;
            }

            List<Avm2MethodBinding?> bindings = method_bindings
                .GetBindings(candidate.Method)
                .Where(binding => binding.Resolved)
                .Cast<Avm2MethodBinding?>()
                .ToList();
            if (bindings.Count == 0)
                bindings.Add(null);

            foreach (Avm2MethodBinding? binding in bindings)
            {
                MethodContext? context = Context(candidate.Method, binding);
                if (context is null ||
                    !context.Analysis.ControlFlow.Complete ||
                    !context.Operations.TryGetValue(
                        candidate.Instruction,
                        out Avm2DataFlowOperation? operation))
                {
                    return true;
                }
                if (operation.Unreachable)
                    continue;
                if (operation.Inputs.Count != 2)
                    return true;
                string receiver = operation.Inputs[0];
                if (!context.Flow.Complete)
                {
                    if (ReceiverOriginIsDisjoint(
                            context,
                            receiver,
                            owner))
                    {
                        continue;
                    }
                    return true;
                }
                if (SetSlotReceiverCouldAlias(
                        context,
                        receiver,
                        owner))
                {
                    return true;
                }
            }
        }
        return false;
    }

    bool SetSlotReceiverCouldAlias(
        MethodContext context,
        string receiver,
        TypeBinding owner)
    {
        PointsToResult resolved = ResolveValue(
            context,
            receiver,
            new HashSet<string>(StringComparer.Ordinal),
            0);
        if (resolved.Types.Count > 0)
        {
            bool possible = resolved.Types.Any(value =>
                value.Receiver == ReceiverKind.Instance &&
                TypeCouldCarryOwnerSlot(value, owner));
            if (possible)
                return true;
            if (resolved.Exhaustive)
                return false;
        }
        else if (resolved.Exhaustive && resolved.Outcomes.Count > 0)
        {
            return false;
        }

        return !ReceiverOriginIsDisjoint(context, receiver, owner);
    }

    bool TypeCouldCarryOwnerSlot(
        PointsTo receiver,
        TypeBinding owner)
    {
        if (receiver.Exhaustive)
            return CouldBeSubtypeOrSame(receiver.Binding, owner);
        return !DeclaredTypeIsDisjointFromOwner(receiver.Binding, owner);
    }

    bool DeclaredTypeIsDisjointFromOwner(
        TypeBinding declared,
        TypeBinding owner)
    {
        if (CouldBeSubtypeOrSame(declared, owner) ||
            CouldBeSubtypeOrSame(owner, declared) ||
            declared.Instance.Flags.HasFlag(ClassFlags.Interface) ||
            owner.Instance.Flags.HasFlag(ClassFlags.Interface))
        {
            return false;
        }
        return true;
    }

    bool CouldBeSubtypeOrSame(
        TypeBinding candidate,
        TypeBinding target)
    {
        bool target_interface =
            target.Instance.Flags.HasFlag(ClassFlags.Interface);
        var pending = new Stack<TypeBinding>();
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        pending.Push(candidate);
        while (pending.Count > 0)
        {
            TypeBinding current = pending.Pop();
            if (!visited.Add(current.Instance))
                continue;
            if (SameType(current, target))
                return true;

            bool no_class_parent =
                current.Instance.IsInterface &&
                current.Instance.SuperIndex == 0;
            bool builtin_object =
                IsBuiltinObject(current.Instance.Super) &&
                FindTypes(current.Instance.Super, current.Abc).Count == 0;
            if (!no_class_parent && !builtin_object)
            {
                List<TypeBinding> parents =
                    FindTypes(current.Instance.Super, current.Abc);
                if (parents.Count == 0)
                {
                    if (!ExternalPlatformParentIsDisjoint(
                            current.Instance.Super,
                            target))
                    {
                        return true;
                    }
                }
                foreach (TypeBinding parent in parents)
                    pending.Push(parent);
            }
            if (!target_interface)
                continue;
            foreach (ASMultiname interface_name in current.Instance.GetInterfaces())
            {
                List<TypeBinding> contracts =
                    FindTypes(interface_name, current.Abc);
                if (contracts.Count == 0)
                    return true;
                foreach (TypeBinding contract in contracts)
                    pending.Push(contract);
            }
        }
        return false;
    }

    static bool ExternalPlatformParentIsDisjoint(
        ASMultiname? parent,
        TypeBinding target) =>
        parent is not null &&
        Qualified(parent).StartsWith(
            "flash.",
            StringComparison.Ordinal) &&
        !target.Qualified.StartsWith(
            "flash.",
            StringComparison.Ordinal);

    static bool SameType(TypeBinding left, TypeBinding right) =>
        ReferenceEquals(left.Instance, right.Instance) ||
        !left.PrivateNamespace &&
        !right.PrivateNamespace &&
        left.RuntimeIdentity == right.RuntimeIdentity;

    bool ReceiverOriginIsDisjoint(
        MethodContext context,
        string receiver,
        TypeBinding owner) =>
        ReceiverOriginIsDisjoint(
            context,
            receiver,
            owner,
            new HashSet<string>(StringComparer.Ordinal));

    bool ReceiverOriginIsDisjoint(
        MethodContext context,
        string receiver,
        TypeBinding owner,
        HashSet<string> visited)
    {
        if (!context.VerifierValid)
            return false;
        if (!visited.Add(receiver))
            return false;
        if (receiver == "v_entry_local_0")
        {
            if (context.Binding is null)
                return false;
            if (context.Binding.Scope != Avm2MethodBindingScope.ClassInstance)
                return true;
            TypeBinding? binding_owner = Owner(context.Binding.Owner);
            return binding_owner is not null &&
                DeclaredTypeIsDisjointFromOwner(binding_owner, owner);
        }
        if (ReceiverVerifierTypeIsDisjoint(
                context,
                receiver,
                owner))
        {
            return true;
        }
        if (!context.Producers.TryGetValue(
                receiver,
                out Avm2DataFlowOperation? producer) ||
            producer.Instruction < 0 ||
            producer.Instruction >= context.Code.Count)
        {
            return ExactScopeValueIsDisjoint(
                context,
                receiver,
                owner);
        }

        ASInstruction instruction =
            context.Code[producer.Instruction];
        if (instruction is GetLexIns lexical &&
            (LexicalBuiltinClassReceiver(
                    context,
                    producer,
                    lexical.TypeName) ||
                LexicalValueDeclaredTypeIsDisjoint(
                    context,
                    producer,
                    lexical.TypeName,
                    owner)))
        {
            return true;
        }

        if (instruction is
                FindPropertyIns or FindPropStrictIns)
        {
            if (FindPropertySelectsActivation(
                    context,
                    instruction,
                    producer))
            {
                return true;
            }
            TypeBinding? context_owner = Owner(MethodContainer(context));
            if (context_owner is not null &&
                SameType(context_owner, owner) &&
                MethodContainer(context) is ASInstance)
            {
                bool could_return = ScopeLookupCouldReturnThis(
                    context,
                    instruction,
                    producer,
                    new HashSet<string>(
                        visited,
                        StringComparer.Ordinal));
                return !could_return;
            }
            return producer.ScopeBefore.All(value => ReceiverOriginIsDisjoint(
                context,
                value,
                owner,
                new HashSet<string>(visited, StringComparer.Ordinal)));
        }

        return instruction.OP is
            OPCode.PushNull or
            OPCode.PushUndefined or
            OPCode.PushTrue or
            OPCode.PushFalse or
            OPCode.PushByte or
            OPCode.PushShort or
            OPCode.PushInt or
            OPCode.PushUInt or
            OPCode.PushDouble or
            OPCode.PushNan or
            OPCode.PushString or
            OPCode.PushNamespace or
            OPCode.NewArray or
            OPCode.NewObject or
            OPCode.NewActivation or
            OPCode.NewCatch or
            OPCode.NewFunction or
            OPCode.NewClass;
    }

    bool LexicalBuiltinClassReceiver(
        MethodContext context,
        Avm2DataFlowOperation operation,
        ASMultiname property)
    {
        if (Avm2VerifierTypeRegistry.CoreInstanceIdentity(
                property) is null ||
            FindTypes(
                property,
                context.Method.ABC,
                true).Count > 0 ||
            method_bindings.Abcs.Any(abc =>
                abc.Scripts.Any(script =>
                    script.Traits.Any(trait =>
                        PropertiesMatchIndexed(
                            property,
                            context.Method.ABC,
                            trait.QName,
                            abc)))))
        {
            return false;
        }
        return InspectLexicalDomain(
                context,
                operation,
                property,
                new HashSet<string>(StringComparer.Ordinal),
                0) == LexicalDomainCertainty.Exact;
    }

    bool LexicalValueDeclaredTypeIsDisjoint(
        MethodContext context,
        Avm2DataFlowOperation operation,
        ASMultiname property,
        TypeBinding owner)
    {
        PointsToResult scopes = ResolveLexicalScope(
            context,
            operation,
            property,
            new HashSet<string>(StringComparer.Ordinal),
            0);
        if (!scopes.Exhaustive ||
            scopes.Types.Count == 0 ||
            scopes.Outcomes.Count > 0)
        {
            return false;
        }
        bool matched = false;
        foreach (PointsTo scope in scopes.Types)
        {
            List<TraitBinding> candidates = FindTraits(
                    scope.Binding,
                    scope.Receiver,
                    property)
                .Where(candidate =>
                    candidate.Trait.Kind != TraitKind.Setter)
                .ToList();
            if (candidates.Count == 0)
                return false;
            foreach (TraitBinding candidate in candidates)
            {
                matched = true;
                if (candidate.Trait.Kind is
                    TraitKind.Class or
                    TraitKind.Method or
                    TraitKind.Function)
                {
                    continue;
                }
                ASMultiname? declared = candidate.Trait.Kind switch
                {
                    TraitKind.Slot or TraitKind.Constant =>
                        candidate.Trait.Type,
                    TraitKind.Getter =>
                        candidate.Trait.Method?.ReturnType,
                    _ => null
                };
                if (!DeclaredTypeIsDisjointFromOwner(
                        declared,
                        candidate.Container.ABC,
                        owner))
                {
                    return false;
                }
            }
        }
        return matched;
    }

    bool DeclaredTypeIsDisjointFromOwner(
        ASMultiname? declared,
        ABCFile requester,
        TypeBinding owner)
    {
        string? builtin =
            Avm2VerifierTypeRegistry.CoreInstanceIdentity(
                declared);
        if (builtin is not null)
            return OwnerIsDisjointFromBuiltin(owner, builtin);
        List<TypeBinding> candidates = FindTypes(
            declared,
            requester,
            true);
        return candidates.Count > 0 &&
            candidates.All(candidate =>
                DeclaredTypeIsDisjointFromOwner(
                    candidate,
                    owner));
    }

    bool OwnerIsDisjointFromBuiltin(
        TypeBinding owner,
        string builtin)
    {
        if (builtin == "builtin:object")
            return false;
        TypeBinding? current = owner;
        var visited = new HashSet<ASInstance>(
            ReferenceEqualityComparer.Instance);
        while (current is not null &&
            visited.Add(current.Instance))
        {
            string? parent_builtin =
                Avm2VerifierTypeRegistry.CoreInstanceIdentity(
                    current.Instance.Super);
            if (parent_builtin is not null)
                return parent_builtin != builtin;
            List<TypeBinding> parents = FindTypes(
                current.Instance.Super,
                current.Abc,
                true);
            if (parents.Count != 1)
                return false;
            current = parents[0];
        }
        return false;
    }

    bool ReceiverVerifierTypeIsDisjoint(
        MethodContext context,
        string receiver,
        TypeBinding owner)
    {
        if (!context.VerifierValid ||
            !context.Values.TryGetValue(
                receiver,
                out Avm2DataFlowValue? value))
        {
            return false;
        }
        string? identity =
            value.ExactRuntimeTypeIdentity ??
            (value.VerifierType.Kind ==
                Avm2VerifierTypeKind.Known
                    ? value.VerifierType.Identity
                    : null);
        if (identity is null)
            return false;
        if (TryExactRuntimeType(
                identity,
                out TypeBinding? declared,
                out ReceiverKind receiver_kind))
        {
            return receiver_kind == ReceiverKind.Static ||
                DeclaredTypeIsDisjointFromOwner(
                    declared,
                    owner);
        }
        if (identity == "builtin:object")
            return false;
        if (identity.StartsWith(
                "builtin-class:",
                StringComparison.Ordinal))
        {
            return true;
        }
        if (identity.StartsWith(
                "builtin:",
                StringComparison.Ordinal) ||
            identity.StartsWith(
                "builtin-vector:",
                StringComparison.Ordinal))
        {
            return OwnerIsDisjointFromBuiltin(owner, identity);
        }
        if (identity.StartsWith(
                "external-class:",
                StringComparison.Ordinal))
        {
            return true;
        }
        if (identity.StartsWith(
                "external-type:",
                StringComparison.Ordinal))
        {
            return false;
        }
        return identity.StartsWith(
            "abc:",
            StringComparison.Ordinal) &&
            identity.Contains(
                ":script:",
                StringComparison.Ordinal);
    }

    static bool FindPropertySelectsActivation(
        MethodContext context,
        ASInstruction instruction,
        Avm2DataFlowOperation operation)
    {
        if (context.Method.Body is not ASMethodBody body ||
            PropertyMultiname(instruction) is not ASMultiname property ||
            property.IsRuntime ||
            property.Kind is not (
                MultinameKind.QName or
                MultinameKind.QNameA) ||
            body.Traits.Count(trait =>
                trait.Kind is TraitKind.Slot or TraitKind.Constant &&
                Avm2MethodAnalyzer.RuntimeSymbolIdentity(trait.QName) ==
                    Avm2MethodAnalyzer.RuntimeSymbolIdentity(property)) != 1 ||
            operation.ScopeBefore.Count == 0)
        {
            return false;
        }

        int scope_index = operation.ScopeBefore.Count - 1;
        string scope = operation.ScopeBefore[scope_index];
        return !ScopeWasPushedWith(
                context,
                operation,
                scope_index,
                scope) &&
            context.Producers.TryGetValue(
                scope,
                out Avm2DataFlowOperation? producer) &&
            producer.Instruction >= 0 &&
            producer.Instruction < context.Code.Count &&
            context.Code[producer.Instruction].OP == OPCode.NewActivation;
    }

    static bool ActivationScope(
        MethodContext context,
        string value) =>
        context.Producers.TryGetValue(
            value,
            out Avm2DataFlowOperation? producer) &&
        producer.Instruction >= 0 &&
        producer.Instruction < context.Code.Count &&
        context.Code[producer.Instruction].OP == OPCode.NewActivation;

    bool ExactScopeValueIsDisjoint(
        MethodContext context,
        string value,
        TypeBinding owner)
    {
        if (!context.Values.TryGetValue(
                value,
                out Avm2DataFlowValue? scope) ||
            scope.ExactRuntimeTypeIdentity is not string identity)
        {
            return false;
        }
        if (TryExactRuntimeType(
                identity,
                out TypeBinding exact,
                out ReceiverKind receiver))
        {
            return receiver == ReceiverKind.Static ||
                DeclaredTypeIsDisjointFromOwner(
                    exact,
                    owner);
        }
        string[] parts = identity.Split(':');
        if (parts.Length == 4 &&
            parts[0] == "abc" &&
            parts[2] == "script" &&
            int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _) &&
            int.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _))
        {
            return true;
        }
        return identity.StartsWith(
            "builtin-class:",
            StringComparison.Ordinal);
    }

    static bool StoreReceiverMatches(
        MethodContext context,
        string stored_receiver,
        ASMultiname property,
        string? loaded_receiver)
    {
        if (loaded_receiver is not null)
            return stored_receiver == loaded_receiver;
        return ProducerInstruction(context, stored_receiver) switch
        {
            FindPropertyIns find => IsSameExactPrivateProperty(
                property,
                find.PropertyName,
                context.Method.ABC),
            FindPropStrictIns strict => IsSameExactPrivateProperty(
                property,
                strict.PropertyName,
                context.Method.ABC),
            _ => false
        };
    }

    static bool IsPrivateWriteProofTransparent(ASInstruction instruction) =>
        instruction.OP is
            OPCode.Nop or
            OPCode.Label or
            OPCode.Debug or
            OPCode.DebugFile or
            OPCode.DebugLine or
            OPCode.Bkpt or
            OPCode.BkptLine or
            OPCode.Pop or
            OPCode.Dup or
            OPCode.Swap;

    static bool IsExactPrivateProperty(ASMultiname? property, ABCFile? abc) =>
        property is not null &&
        abc is not null &&
        property.Kind is MultinameKind.QName or MultinameKind.QNameA &&
        Avm2MethodAnalyzer.TryGetStaticName(property, out _) &&
        ReferenceEquals(property.Pool.ABC, abc) &&
        property.Namespace?.Kind == NamespaceKind.Private;

    static bool IsSameExactPrivateProperty(
        ASMultiname property,
        ASMultiname? candidate,
        ABCFile? abc) =>
        IsExactPrivateProperty(property, abc) &&
        IsExactPrivateProperty(candidate, abc) &&
        Avm2MethodAnalyzer.ExactSymbolIdentity(property) ==
            Avm2MethodAnalyzer.ExactSymbolIdentity(candidate);

    PointsToResult ResolveLexicalScope(
        MethodContext context,
        Avm2DataFlowOperation operation,
        ASMultiname property,
        HashSet<string> visited,
        int depth)
    {
        bool entry_scope_unknown = false;
        for (int scope_index = operation.ScopeBefore.Count - 1;
            scope_index >= 1;
            scope_index--)
        {
            string value = operation.ScopeBefore[scope_index];
            if (ScopeIsUnknown(context, value))
            {
                entry_scope_unknown = true;
                continue;
            }
            if (ScopeWasPushedWith(
                    context,
                    operation,
                    scope_index,
                    value))
                return None();
            if (ActivationScope(
                    context,
                    value))
            {
                if (property.IsRuntime ||
                    context.Method.Body is not ASMethodBody body ||
                    body.Traits.Any(trait =>
                        PropertiesMatchIndexed(
                            property,
                            context.Method.ABC,
                            trait.QName,
                            trait.ABC)))
                {
                    return None();
                }
                continue;
            }
            if (IsExactPrivateProperty(property, context.Method.ABC) &&
                ValueIsExactlyThis(
                    context,
                    value,
                    property,
                    new HashSet<string>(StringComparer.Ordinal)) &&
                Owner(MethodContainer(context)) is TypeBinding exact_owner)
            {
                if (FindTraits(
                        exact_owner,
                        ReceiverKind.Instance,
                        property).Any())
                {
                    return One(
                        exact_owner,
                        ReceiverKind.Instance,
                        "LexicalScope",
                        context,
                        operation.Instruction,
                        operation.Offset,
                        true);
                }
                continue;
            }

            PointsToResult scope = ResolveValue(
                context,
                value,
                new HashSet<string>(visited, StringComparer.Ordinal),
                depth + 1);
            if (scope.Types.Count == 0 &&
                ScopeIsKnownBuiltin(context, value))
            {
                if (property.IsRuntime)
                    return None();
                continue;
            }
            if (!scope.Exhaustive ||
                scope.Types.Count == 0 ||
                scope.Outcomes.Count > 0)
            {
                return None();
            }
            List<PointsTo> matching = scope.Types
                .Where(candidate => FindTraits(
                    candidate.Binding,
                    candidate.Receiver,
                    property).Any())
                .ToList();
            if (matching.Count > 0)
            {
                bool exact =
                    !entry_scope_unknown &&
                    matching.Count == scope.Types.Count;
                return Result(
                    matching.Select(candidate => new PointsTo
                    {
                        Binding = candidate.Binding,
                        Receiver = candidate.Receiver,
                        SelectionKind = "LexicalScope",
                        SelectorExpression = candidate.SelectorExpression,
                        Conditions = candidate.Conditions,
                        Evidence =
                        [
                            .. candidate.Evidence,
                            Evidence(
                                "LexicalScope",
                                exact ? "Exact" : "Partial",
                                context,
                                operation.Instruction,
                                operation.Offset,
                                $"{candidate.Binding.Qualified}.{Qualified(property)}",
                                candidate.Binding.AbcIndex)
                        ],
                        Exhaustive = exact
                    }),
                    exact);
            }
            if (scope.Types.Count > 0 &&
                ScopeCouldSupplyUnknownProperty(property))
            {
                return None();
            }
        }
        if (entry_scope_unknown)
            return None();

        TypeBinding? owner = Owner(MethodContainer(context));
        if (owner is null ||
            !IsPrivate(property) ||
            !FindTraits(owner, ReceiverKind.Static, property).Any())
        {
            return None();
        }
        return One(
            owner,
            ReceiverKind.Static,
            "PrivateLexicalScope",
            context,
            operation.Instruction,
            operation.Offset,
            true);
    }

    LexicalDomainCertainty InspectLexicalDomain(
        MethodContext context,
        Avm2DataFlowOperation operation,
        ASMultiname property,
        HashSet<string> visited,
        int depth)
    {
        for (int scope_index = operation.ScopeBefore.Count - 1;
            scope_index >= 1;
            scope_index--)
        {
            string value = operation.ScopeBefore[scope_index];
            if (ScopeIsUnknown(context, value))
                return LexicalDomainCertainty.Blocked;
            if (ScopeWasPushedWith(
                    context,
                    operation,
                    scope_index,
                    value))
                return LexicalDomainCertainty.Blocked;
            if (ActivationScope(
                    context,
                    value))
            {
                if (property.IsRuntime ||
                    context.Method.Body is not ASMethodBody body ||
                    body.Traits.Any(trait =>
                        PropertiesMatchIndexed(
                            property,
                            context.Method.ABC,
                            trait.QName,
                            trait.ABC)))
                {
                    return LexicalDomainCertainty.Blocked;
                }
                continue;
            }
            if (ScopeHasScriptIdentity(context, value))
                return LexicalDomainCertainty.Blocked;
            if (ScopeHasAnyVerifier(context, value))
            {
                if (property.IsRuntime)
                    return LexicalDomainCertainty.Blocked;
                continue;
            }
            if (IsExactPrivateProperty(property, context.Method.ABC) &&
                ValueIsExactlyThis(
                    context,
                    value,
                    property,
                    new HashSet<string>(StringComparer.Ordinal)) &&
                Owner(MethodContainer(context)) is TypeBinding exact_owner)
            {
                if (FindTraits(
                        exact_owner,
                        ReceiverKind.Instance,
                        property).Any())
                {
                    return LexicalDomainCertainty.Blocked;
                }
                continue;
            }

            PointsToResult scope = ResolveValue(
                context,
                value,
                new HashSet<string>(visited, StringComparer.Ordinal),
                depth + 1);
            if (scope.Types.Count == 0 &&
                ScopeIsKnownBuiltin(context, value))
            {
                if (property.IsRuntime)
                    return LexicalDomainCertainty.Blocked;
                continue;
            }
            if (!scope.Exhaustive ||
                scope.Types.Count == 0 ||
                scope.Outcomes.Count > 0 ||
                scope.Types.Any(candidate =>
                    FindTraits(
                        candidate.Binding,
                        candidate.Receiver,
                        property).Any() ||
                    ScopeCouldSupplyUnknownProperty(property)))
            {
                return LexicalDomainCertainty.Blocked;
            }
        }
        return DomainRootIsProven(context, operation)
            ? LexicalDomainCertainty.Exact
            : LexicalDomainCertainty.Blocked;
    }

    PointsToResult ResolveConstructProperty(
        MethodContext context,
        Avm2DataFlowOperation operation,
        ASMultiname property,
        string receiver_value,
        HashSet<string> visited,
        int depth)
    {
        if (context.Producers.TryGetValue(
                receiver_value,
                out Avm2DataFlowOperation? receiver_producer) &&
            receiver_producer.Instruction >= 0 &&
            receiver_producer.Instruction < context.Code.Count &&
            context.Code[receiver_producer.Instruction] is
                FindPropertyIns or FindPropStrictIns &&
            PropertyMultiname(context.Code[receiver_producer.Instruction]) is
                ASMultiname lexical_property &&
            PropertiesMatchIndexed(
                property,
                context.Method.ABC,
                lexical_property,
                context.Method.ABC))
        {
            PointsToResult lexical_scope = ResolveLexicalScope(
                context,
                receiver_producer,
                property,
                visited,
                depth + 1);
            if (lexical_scope.Types.Count > 0)
            {
                PointsToResult scoped_value = ResolveTraitValue(
                    context,
                    property,
                    lexical_scope,
                    operation,
                    visited,
                    depth + 1);
                PointsToResult scoped_constructors =
                    ConstructorClosures(scoped_value);
                if (scoped_constructors.Types.Count > 0)
                    return scoped_constructors;
                return None();
            }

            LexicalDomainCertainty domain = InspectLexicalDomain(
                context,
                receiver_producer,
                property,
                visited,
                depth + 1);
            if (domain == LexicalDomainCertainty.Blocked)
                return None();
            return Many(
                FindTypes(property, context.Method.ABC),
                ReceiverKind.Static,
                "DeclaredType",
                context,
                operation.Instruction,
                operation.Offset,
                domain == LexicalDomainCertainty.Exact);
        }

        PointsToResult receiver = ResolveValue(
            context,
            receiver_value,
            new HashSet<string>(visited, StringComparer.Ordinal),
            depth + 1);
        if (receiver.Types.Count == 0)
            return None();
        return ConstructorClosures(ResolveTraitValue(
            context,
            property,
            receiver,
            operation,
            visited,
            depth + 1));
    }

    PointsToResult ConstructorClosures(PointsToResult value)
    {
        List<PointsTo> constructors = value.Types
            .Where(candidate => candidate.Receiver == ReceiverKind.Static)
            .ToList();
        bool complete = constructors.Count == value.Types.Count &&
            constructors.Count > 0;
        return Result(
            constructors,
            value.Outcomes,
            value.ControlFlowExhaustive && complete,
            value.TargetExhaustive && complete);
    }

    PointsToResult NarrowToType(
        MethodContext context,
        Avm2DataFlowOperation operation,
        PointsToResult runtime,
        IReadOnlyList<TypeBinding> targets,
        bool nullable_failure)
    {
        if (targets.Count == 0)
            return None();

        var narrowed = new List<PointsTo>();
        var outcomes = runtime.Outcomes.ToList();
        bool classification_complete = runtime.Types.Count > 0 ||
            runtime.Outcomes.Count > 0;
        foreach (PointsTo source in runtime.Types)
        {
            List<TypeBinding> compatible = source.Receiver == ReceiverKind.Instance
                ? targets.Where(target =>
                    SameType(source.Binding, target) ||
                    IsStrictSubtype(source.Binding, target)).ToList()
                : [];
            if (compatible.Count > 0)
            {
                narrowed.Add(new PointsTo
                {
                    Binding = source.Binding,
                    Receiver = source.Receiver,
                    SelectionKind = source.SelectionKind,
                    SelectorExpression = source.SelectorExpression,
                    Conditions = source.Conditions,
                    Evidence =
                    [
                        .. source.Evidence,
                        Evidence(
                            "TypeNarrowing",
                            source.Exhaustive ? "Exact" : "Partial",
                            context,
                            operation.Instruction,
                            operation.Offset,
                            string.Join("|", compatible.Select(value => value.Qualified)),
                            source.Binding.AbcIndex)
                    ],
                    Exhaustive = source.Exhaustive
                });
                continue;
            }

            bool exact_failure = source.Exhaustive &&
                source.Receiver == ReceiverKind.Instance &&
                AncestryIsComplete(source.Binding);
            if (!exact_failure)
            {
                foreach (TypeBinding target in targets)
                {
                    narrowed.Add(new PointsTo
                    {
                        Binding = target,
                        Receiver = ReceiverKind.Instance,
                        SelectionKind = "TypeNarrowing",
                        SelectorExpression = source.SelectorExpression,
                        Conditions = source.Conditions,
                        Evidence =
                        [
                            .. source.Evidence,
                            Evidence(
                                "TypeNarrowing",
                                "Partial",
                                context,
                                operation.Instruction,
                                operation.Offset,
                                target.Qualified,
                                target.AbcIndex)
                        ],
                        Exhaustive = false
                    });
                }
                classification_complete = false;
            }

            outcomes.Add(new Avm2CallTerminalOutcome
            {
                Kind = nullable_failure ? "Null" : "Throw",
                Expression = nullable_failure ? "null" : "TypeError",
                Conditions = source.Conditions,
                Evidence =
                [
                    .. source.Evidence,
                    Evidence(
                        "TypeNarrowing",
                        exact_failure ? "Exact" : "Partial",
                        context,
                        operation.Instruction,
                        operation.Offset,
                        nullable_failure ? "null" : "TypeError",
                        source.Binding.AbcIndex)
                ]
            });
        }
        return Result(
            narrowed,
            outcomes,
            runtime.ControlFlowExhaustive && classification_complete,
            runtime.TargetExhaustive && classification_complete);
    }

    PointsToResult FilterXmlValue(
        MethodContext context,
        Avm2DataFlowOperation operation,
        PointsToResult runtime)
    {
        var filtered = new List<PointsTo>();
        var outcomes = runtime.Outcomes.Select(outcome =>
            outcome.Kind is "Null" or "Undefined"
                ? new Avm2CallTerminalOutcome
                {
                    Kind = "Throw",
                    Expression = "TypeError",
                    Conditions = outcome.Conditions,
                    Evidence =
                    [
                        .. outcome.Evidence,
                        Evidence(
                            "CheckFilter",
                            "Exact",
                            context,
                            operation.Instruction,
                            operation.Offset,
                            "TypeError")
                    ]
                }
                : outcome).ToList();
        bool classification_complete = runtime.Types.Count > 0 ||
            runtime.Outcomes.Count > 0;
        foreach (PointsTo source in runtime.Types)
        {
            if (source.Receiver == ReceiverKind.Instance &&
                IsXmlType(source.Binding))
            {
                filtered.Add(source);
                continue;
            }

            bool exact_failure = source.Exhaustive &&
                AncestryIsComplete(source.Binding);
            if (!exact_failure)
            {
                outcomes.Add(new Avm2CallTerminalOutcome
                {
                    Kind = "Unknown",
                    Expression = "XML|XMLList",
                    Conditions = source.Conditions,
                    Evidence =
                    [
                        .. source.Evidence,
                        Evidence(
                            "CheckFilter",
                            "Partial",
                            context,
                            operation.Instruction,
                            operation.Offset,
                            "XML|XMLList",
                            source.Binding.AbcIndex)
                    ]
                });
                classification_complete = false;
            }
            outcomes.Add(new Avm2CallTerminalOutcome
            {
                Kind = "Throw",
                Expression = "TypeError",
                Conditions = source.Conditions,
                Evidence =
                [
                    .. source.Evidence,
                    Evidence(
                        "CheckFilter",
                        exact_failure ? "Exact" : "Partial",
                        context,
                        operation.Instruction,
                        operation.Offset,
                        "TypeError",
                        source.Binding.AbcIndex)
                ]
            });
        }
        return Result(
            filtered,
            outcomes,
            runtime.ControlFlowExhaustive && classification_complete,
            runtime.TargetExhaustive && classification_complete);
    }

    bool IsXmlType(TypeBinding binding)
    {
        TypeBinding? current = binding;
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        while (current is not null && visited.Add(current.Instance))
        {
            List<TypeBinding> parents =
                FindTypes(current.Instance.Super, current.Abc);
            if (IsBuiltinType(current.Instance.Super, "XML", "XMLList") &&
                parents.Count == 0)
            {
                return true;
            }

            bool builtin_object =
                IsBuiltinObject(current.Instance.Super) &&
                parents.Count == 0;
            if (builtin_object || parents.Count != 1)
                return false;
            current = parents[0];
        }
        return false;
    }

    bool AncestryIsComplete(TypeBinding binding)
    {
        var pending = new Stack<TypeBinding>();
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        pending.Push(binding);
        while (pending.Count > 0)
        {
            TypeBinding current = pending.Pop();
            if (!visited.Add(current.Instance))
                continue;
            List<TypeBinding> parents =
                FindTypes(current.Instance.Super, current.Abc);
            bool builtin_object =
                IsBuiltinObject(current.Instance.Super) &&
                parents.Count == 0;
            if (!builtin_object)
            {
                if (parents.Count != 1)
                    return false;
                pending.Push(parents[0]);
            }
            foreach (ASMultiname interface_name in current.Instance.GetInterfaces())
            {
                List<TypeBinding> contracts =
                    FindTypes(interface_name, current.Abc);
                if (contracts.Count != 1)
                    return false;
                pending.Push(contracts[0]);
            }
        }
        return true;
    }

    static bool ScopeCouldSupplyUnknownProperty(
        ASMultiname property) =>
        property.IsRuntime;

    static bool ScopeWasPushedWith(
        MethodContext context,
        Avm2DataFlowOperation operation,
        int scope_index,
        string value)
    {
        if (context.Flow.ScopeWithBefore.TryGetValue(
                operation.Instruction,
                out IReadOnlyList<bool?>? scope_with) &&
            scope_with.Count == operation.ScopeBefore.Count)
        {
            return scope_index < 0 ||
                scope_index >= operation.ScopeBefore.Count ||
                operation.ScopeBefore[scope_index] != value ||
                scope_with[scope_index] != false;
        }
        return context.Flow.Operations.Any(candidate =>
            !candidate.Unreachable &&
            candidate.Instruction >= 0 &&
            candidate.Instruction < operation.Instruction &&
            candidate.Instruction < context.Code.Count &&
            context.Code[candidate.Instruction].OP == OPCode.PushWith &&
            candidate.Inputs.Contains(value, StringComparer.Ordinal));
    }

    static bool ScopeIsUnknown(
        MethodContext context,
        string value) =>
        value.StartsWith("v_entry_scope_", StringComparison.Ordinal) ||
        !context.Values.TryGetValue(
            value,
            out Avm2DataFlowValue? scope) ||
        scope.Kind is
            "Unknown" or
            "UnknownDeclaringScope" or
            "Missing" or
            "Unreachable";

    bool DomainRootIsProven(
        MethodContext context,
        Avm2DataFlowOperation operation)
    {
        if (operation.ScopeBefore.Count == 0)
            return IsUniqueScriptInitializer(context);
        string value = operation.ScopeBefore[0];
        return !ScopeWasPushedWith(
                context,
                operation,
                0,
                value) &&
            TryScriptGlobalScope(context, value);
    }

    bool IsUniqueScriptInitializer(MethodContext context)
    {
        Avm2MethodBinding[] bindings = ResolveMethodBindings(
                context.Method)
            .Where(value => value.Resolved)
            .Take(2)
            .ToArray();
        return bindings.Length == 1 &&
            bindings[0].Scope ==
                Avm2MethodBindingScope.Script &&
            bindings[0].Role ==
                Avm2MethodBindingRole.ScriptInitializer &&
            (context.Binding is null ||
                context.Binding.Identity ==
                    bindings[0].Identity);
    }

    bool TryScriptGlobalScope(
        MethodContext context,
        string value)
    {
        if (!context.Values.TryGetValue(
                value,
                out Avm2DataFlowValue? scope) ||
            scope.VerifierType.Kind !=
                Avm2VerifierTypeKind.Known ||
            !string.Equals(
                scope.ExactRuntimeTypeIdentity,
                scope.VerifierType.Identity,
                StringComparison.Ordinal) ||
            !TryScriptIdentity(
                scope.VerifierType.Identity,
                out int abc_index,
                out int script_index) ||
            !method_bindings.AbcsByIndex.TryGetValue(
                abc_index,
                out ABCFile? abc))
        {
            return false;
        }
        return script_index >= 0 &&
            script_index < abc.Scripts.Count;
    }

    static bool ScopeHasScriptIdentity(
        MethodContext context,
        string value)
    {
        if (!context.Values.TryGetValue(
                value,
                out Avm2DataFlowValue? scope))
        {
            return false;
        }
        return TryScriptIdentity(
                scope.VerifierType.Identity,
                out _,
                out _) ||
            TryScriptIdentity(
                scope.ExactRuntimeTypeIdentity,
                out _,
                out _);
    }

    static bool ScopeHasAnyVerifier(
        MethodContext context,
        string value) =>
        context.Values.TryGetValue(
            value,
            out Avm2DataFlowValue? scope) &&
        scope.VerifierType.Kind ==
            Avm2VerifierTypeKind.Any;

    static bool TryScriptIdentity(
        string? identity,
        out int abc_index,
        out int script_index)
    {
        abc_index = -1;
        script_index = -1;
        if (identity is null)
            return false;
        string[] parts = identity.Split(':');
        return parts.Length == 4 &&
            parts[0] == "abc" &&
            parts[2] == "script" &&
            int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out abc_index) &&
            int.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out script_index);
    }

    CallableResult ResolveCallable(
        MethodContext context,
        string value,
        HashSet<string> visited,
        int depth)
    {
        if (depth > MaximumDepth || visited.Count > MaximumValues || !visited.Add(value))
            return EmptyCallable();

        if (context.Phis.TryGetValue(value, out Avm2DataFlowPhi? phi))
        {
            var targets = new List<Avm2ResolvedCallTarget>();
            bool control_flow_exhaustive = phi.Inputs.Count > 0;
            bool target_exhaustive = phi.Inputs.Count > 0;
            foreach (Avm2DataFlowPhiInput input in phi.Inputs)
            {
                CallableResult branch = ResolveCallable(
                    context,
                    input.Value,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1);
                List<Avm2CallCondition> conditions =
                    PhiInputConditions(context, phi, input);
                foreach (Avm2ResolvedCallTarget target in branch.Targets)
                {
                    List<Avm2CallCondition> combined =
                        DistinctConditions([.. target.Conditions, .. conditions]);
                    targets.Add(new Avm2ResolvedCallTarget
                    {
                        Method = target.Method,
                        Binding = target.Binding,
                        ExactReceiver = target.ExactReceiver,
                        ClosureScope = target.ClosureScope,
                        RequiresClosureScope =
                            target.RequiresClosureScope,
                        RuntimeType = target.RuntimeType,
                        DefinitionAbc = target.DefinitionAbc,
                        SelectionKind = "PhiCallable",
                        SelectorExpression = combined.Count == 0
                            ? target.SelectorExpression
                            : string.Join(
                                " && ",
                                combined.Select(condition => condition.Expression)),
                        Conditions = combined,
                        Evidence =
                        [
                            .. target.Evidence,
                            Evidence(
                                "PhiCallable",
                                branch.Exhaustive ? "Exact" : "Partial",
                                context,
                                input.SourceInstruction ?? -1,
                                input.SourceInstruction.HasValue
                                    ? context.Operations.GetValueOrDefault(
                                        input.SourceInstruction.Value)?.Offset ?? -1
                                    : -1,
                                MethodName(target.Method),
                                target.DefinitionAbc)
                        ]
                    });
                }
                control_flow_exhaustive &= branch.ControlFlowExhaustive;
                target_exhaustive &= branch.TargetExhaustive;
            }
            targets = Deduplicate(targets);
            return new CallableResult
            {
                Targets = targets,
                ControlFlowExhaustive =
                    control_flow_exhaustive && targets.Count > 0,
                TargetExhaustive =
                    target_exhaustive && targets.Count > 0
            };
        }

        if (!context.Producers.TryGetValue(value, out Avm2DataFlowOperation? producer) ||
            producer.Instruction < 0 ||
            producer.Instruction >= context.Code.Count)
        {
            return EmptyCallable();
        }
        ASInstruction instruction = context.Code[producer.Instruction];
        if (instruction is NewFunctionIns function)
        {
            ASMethod method = function.Method;
            bool scope_captured = TryCaptureClosureScope(
                context,
                producer,
                out Avm2DataFlowScopeContext? closure_scope);
            return new CallableResult
            {
                Targets =
                [
                    new Avm2ResolvedCallTarget
                    {
                        Method = method,
                        ClosureScope = closure_scope,
                        RequiresClosureScope = true,
                        RuntimeType = MethodOwner(method),
                        DefinitionAbc = AbcIndex(method.ABC),
                        SelectionKind = "NewFunction",
                        Conditions = [],
                        Evidence =
                        [
                            Evidence(
                                "NewFunction",
                                "Exact",
                                context,
                                producer.Instruction,
                                producer.Offset,
                                MethodName(method),
                                AbcIndex(method.ABC))
                        ]
                    }
                ],
                ControlFlowExhaustive = true,
                TargetExhaustive = scope_captured
            };
        }
        string? source = instruction switch
        {
            CoerceIns coerce when CallableTypeIsCompatible(
                coerce.TypeName,
                context.Method.ABC) =>
                producer.Inputs.LastOrDefault(),
            AsTypeIns as_type when CallableTypeIsCompatible(
                as_type.TypeName,
                context.Method.ABC) =>
                producer.Inputs.LastOrDefault(),
            _ when instruction.OP == OPCode.Coerce_a =>
                producer.Inputs.LastOrDefault(),
            _ => null
        };
        return source is null
            ? EmptyCallable()
            : ResolveCallable(
                context,
                source,
                new HashSet<string>(visited, StringComparer.Ordinal),
                depth + 1);
    }

    static bool TryCaptureClosureScope(
        MethodContext context,
        Avm2DataFlowOperation producer,
        out Avm2DataFlowScopeContext? scope)
    {
        try
        {
            scope = Avm2DataFlowScopeContext.Capture(
                context.Flow,
                producer);
            return true;
        }
        catch (ArgumentException)
        {
            scope = null;
            return false;
        }
        catch (InvalidOperationException)
        {
            scope = null;
            return false;
        }
    }

    bool CallableTypeIsCompatible(ASMultiname? type, ABCFile requester) =>
        IsBuiltinType(type, "Function", "Object") &&
        FindTypes(type, requester).Count == 0;

    PointsToResult ResolveReturns(
        MethodContext caller,
        Avm2DataFlowOperation call_operation,
        CallSite call,
        Avm2ResolvedCall targets,
        int depth)
    {
        var values = new List<PointsTo>();
        var outcomes = new List<Avm2CallTerminalOutcome>();
        bool control_flow_exhaustive = targets.ControlFlowExhaustive &&
            targets.Targets.Count > 0;
        bool target_exhaustive = targets.TargetExhaustive &&
            targets.Targets.Count > 0;
        foreach (Avm2ResolvedCallTarget target in targets.Targets)
        {
            PointsToResult summary = ReturnSummary(
                target.Method,
                target.Binding,
                target.ExactReceiver,
                target.ClosureScope,
                target.RequiresClosureScope,
                depth + 1);
            if (summary.Types.Count == 0 && summary.Outcomes.Count == 0)
            {
                List<TypeBinding> declared_types = FindTypes(
                    target.Method.ReturnType,
                    target.Method.ABC);
                if (declared_types.Count > 0)
                {
                    foreach (TypeBinding declared in declared_types)
                    {
                        bool exact = declared.Instance.Flags.HasFlag(ClassFlags.Final);
                        values.Add(new PointsTo
                        {
                            Binding = declared,
                            Receiver = ReceiverKind.Instance,
                            SelectionKind = "ReturnType",
                            SelectorExpression = null,
                            Conditions = target.Conditions,
                            Evidence =
                            [
                                .. target.Evidence,
                                Evidence(
                                    "ReturnType",
                                    exact ? "ExactFinal" : "Declared",
                                    caller,
                                    call_operation.Instruction,
                                    call_operation.Offset,
                                    declared.Qualified,
                                    declared.AbcIndex)
                            ],
                            Exhaustive = exact
                        });
                        target_exhaustive &= exact;
                    }
                }
                else
                {
                    target_exhaustive = false;
                }
                control_flow_exhaustive = false;
                continue;
            }

            foreach (PointsTo returned in summary.Types)
            {
                List<Avm2CallCondition> conditions = returned.Conditions
                    .Select(condition => Substitute(condition, target.Method, call, caller))
                    .ToList();
                List<Avm2CallCondition> combined_conditions =
                    DistinctConditions([.. target.Conditions, .. conditions]);
                values.Add(new PointsTo
                {
                    Binding = returned.Binding,
                    Receiver = returned.Receiver,
                    SelectionKind = conditions.Count > 0
                        ? "FactoryBranch"
                        : returned.SelectionKind,
                    SelectorExpression = conditions.Count == 0
                        ? returned.SelectorExpression
                        : string.Join(" && ", conditions.Select(condition => condition.Expression)),
                    Conditions = combined_conditions,
                    Evidence =
                    [
                        .. target.Evidence,
                        .. returned.Evidence,
                        Evidence(
                            "ReturnType",
                            "Exact",
                            caller,
                            call_operation.Instruction,
                            call_operation.Offset,
                            returned.Binding.Qualified,
                            returned.Binding.AbcIndex)
                    ],
                    Exhaustive = summary.TargetExhaustive && returned.Exhaustive
                });
            }
            foreach (Avm2CallTerminalOutcome outcome in summary.Outcomes)
            {
                List<Avm2CallCondition> conditions = outcome.Conditions
                    .Select(condition => Substitute(condition, target.Method, call, caller))
                    .ToList();
                outcomes.Add(new Avm2CallTerminalOutcome
                {
                    Kind = outcome.Kind,
                    Expression = outcome.Expression,
                    Conditions = DistinctConditions([.. target.Conditions, .. conditions]),
                    Evidence =
                    [
                        .. target.Evidence,
                        .. outcome.Evidence,
                        Evidence(
                            "TerminalReturn",
                            outcome.Kind == "Unknown" ? "Unknown" : "Exact",
                            caller,
                            call_operation.Instruction,
                            call_operation.Offset,
                            outcome.Expression)
                    ]
                });
            }
            control_flow_exhaustive &= summary.ControlFlowExhaustive;
            target_exhaustive &= summary.TargetExhaustive;
        }
        return Result(
            values,
            outcomes,
            control_flow_exhaustive,
            target_exhaustive);
    }

    PointsToResult ReturnSummary(
        ASMethod method,
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver,
        int depth)
        => ReturnSummary(
            method,
            binding,
            exact_receiver,
            null,
            false,
            depth);

    PointsToResult ReturnSummary(
        ASMethod method,
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver,
        Avm2DataFlowScopeContext? closure_scope,
        bool requires_closure_scope,
        int depth)
    {
        if (depth > MaximumDepth)
            return None();
        MethodContext? context = requires_closure_scope
            ? closure_scope is null
                ? null
                : ScopedContext(
                    method,
                    binding,
                    exact_receiver,
                    closure_scope)
            : Context(
                method,
                binding,
                exact_receiver);
        if (context is null)
            return None();
        if (returns.TryGetValue(context, out PointsToResult? cached))
            return cached;
        if (!active_returns.Add(context))
            return None();
        try
        {
            var values = new List<PointsTo>();
            var outcomes = new List<Avm2CallTerminalOutcome>();
            bool control_flow_exhaustive = context.Flow.Complete &&
                context.Analysis.ControlFlow.Complete;
            bool target_exhaustive = control_flow_exhaustive;
            HashSet<int> outgoing_blocks = context.Analysis.ControlFlow.Edges
                .Where(edge => edge.Kind != "Exception" && edge.ToBlock.HasValue)
                .Select(edge => edge.FromBlock)
                .ToHashSet();
            List<Avm2BasicBlockInventory> terminal_blocks =
                context.Analysis.ControlFlow.Blocks
                .Where(block => block.Reachable && !outgoing_blocks.Contains(block.Id))
                .ToList();
            control_flow_exhaustive &= terminal_blocks.Count > 0 &&
                terminal_blocks.All(block =>
                    block.LastInstruction >= 0 &&
                    block.LastInstruction < context.Code.Count &&
                    context.Code[block.LastInstruction].OP is
                        OPCode.ReturnValue or OPCode.Throw);
            List<Avm2DataFlowOperation> return_operations = context.Flow.Operations
                .Where(operation => !operation.Unreachable &&
                    operation.Opcode == nameof(OPCode.ReturnValue))
                .ToList();
            foreach (Avm2DataFlowOperation operation in return_operations)
            {
                List<Avm2CallCondition> conditions =
                    context.Conditions.GetValueOrDefault(operation.Block) ?? [];
                if (operation.Inputs.Count == 0)
                {
                    outcomes.Add(Terminal(
                        "Unknown",
                        "missing-return-value",
                        conditions,
                        context,
                        operation,
                        "Unknown"));
                    target_exhaustive = false;
                    continue;
                }
                PointsToResult returned = ResolveValue(
                    context,
                    operation.Inputs[^1],
                    new HashSet<string>(StringComparer.Ordinal),
                    depth + 1);
                if (returned.Types.Count == 0 && returned.Outcomes.Count == 0)
                {
                    outcomes.Add(Terminal(
                        "Unknown",
                        Expression(
                            context,
                            operation.Inputs[^1],
                            new HashSet<string>(StringComparer.Ordinal),
                            0),
                        conditions,
                        context,
                        operation,
                        "Unknown"));
                    target_exhaustive = false;
                    continue;
                }
                foreach (PointsTo type in returned.Types)
                {
                    List<Avm2CallCondition> combined_conditions =
                        DistinctConditions([.. type.Conditions, .. conditions]);
                    values.Add(new PointsTo
                    {
                        Binding = type.Binding,
                        Receiver = type.Receiver,
                        SelectionKind = conditions.Count > 0
                            ? "FactoryBranch"
                            : type.SelectionKind,
                        SelectorExpression = conditions.Count == 0
                            ? type.SelectorExpression
                            : string.Join(" && ", conditions.Select(condition => condition.Expression)),
                        Conditions = combined_conditions,
                        Evidence =
                        [
                            .. type.Evidence,
                            Evidence(
                                "FactoryBranch",
                                "Exact",
                                context,
                                operation.Instruction,
                                operation.Offset,
                                type.Binding.Qualified,
                                type.Binding.AbcIndex)
                        ],
                        Exhaustive = type.Exhaustive
                    });
                }
                foreach (Avm2CallTerminalOutcome outcome in returned.Outcomes)
                {
                    outcomes.Add(new Avm2CallTerminalOutcome
                    {
                        Kind = outcome.Kind,
                        Expression = outcome.Expression,
                        Conditions = DistinctConditions(
                            [.. outcome.Conditions, .. conditions]),
                        Evidence = outcome.Evidence
                    });
                }
                control_flow_exhaustive &= returned.ControlFlowExhaustive;
                target_exhaustive &= returned.TargetExhaustive;
            }
            HashSet<int> caught_throws = context.Analysis.ControlFlow.Edges
                .Where(edge => edge.Kind == "Exception" && edge.ToBlock.HasValue)
                .Select(edge => edge.SourceInstruction)
                .ToHashSet();
            foreach (Avm2DataFlowOperation operation in context.Flow.Operations.Where(
                operation => !operation.Unreachable &&
                    operation.Opcode == nameof(OPCode.Throw) &&
                    !caught_throws.Contains(operation.Instruction)))
            {
                List<Avm2CallCondition> conditions =
                    context.Conditions.GetValueOrDefault(operation.Block) ?? [];
                string expression = operation.Inputs.Count == 0
                    ? "throw"
                    : Expression(
                        context,
                        operation.Inputs[^1],
                        new HashSet<string>(StringComparer.Ordinal),
                        0);
                outcomes.Add(Terminal(
                    "Throw",
                    expression,
                    conditions,
                    context,
                    operation,
                    "Exact"));
            }
            var summary = Result(
                values,
                outcomes,
                control_flow_exhaustive,
                target_exhaustive);
            returns[context] = summary;
            return summary;
        }
        finally
        {
            active_returns.Remove(context);
        }
    }

    List<Avm2CallTerminalOutcome> DereferenceOutcomes(
        MethodContext context,
        Avm2DataFlowOperation operation,
        IEnumerable<Avm2CallTerminalOutcome> outcomes) =>
        outcomes.Select(outcome =>
            outcome.Kind is "Null" or "Undefined"
                ? new Avm2CallTerminalOutcome
                {
                    Kind = "Throw",
                    Expression = "TypeError",
                    Conditions = outcome.Conditions,
                    Evidence =
                    [
                        .. outcome.Evidence,
                        Evidence(
                            "Dereference",
                            "Exact",
                            context,
                            operation.Instruction,
                            operation.Offset,
                            outcome.Kind)
                    ]
                }
                : outcome).ToList();

    PointsToResult ResolveTraitValue(
        MethodContext context,
        ASMultiname property,
        PointsToResult? receiver,
        Avm2DataFlowOperation operation,
        HashSet<string> visited,
        int depth)
    {
        var candidates = new List<(TraitBinding Trait, PointsTo? Source)>();
        bool coverage_complete = true;
        if (receiver is not null)
        {
            foreach (PointsTo type in receiver.Types)
            {
                List<TraitBinding> matched =
                    FindTraits(type.Binding, type.Receiver, property).ToList();
                matched = matched.Where(candidate =>
                    candidate.Trait.Kind != TraitKind.Setter ||
                    matched.All(value =>
                        value.Trait.Kind != TraitKind.Getter ||
                        EffectiveTraitIdentity(
                            Owner(value.Container),
                            value.Trait) !=
                        EffectiveTraitIdentity(
                            Owner(candidate.Container),
                            candidate.Trait))).ToList();
                if (matched.Count == 0)
                    coverage_complete = false;
                candidates.AddRange(matched.Select(trait =>
                    (trait, (PointsTo?)type)));
            }
        }
        else
        {
            string identity = RuntimeSymbolIdentity(property);
            candidates.AddRange((traits.GetValueOrDefault(identity) ?? [])
                .Where(candidate =>
                    !IsPrivate(property) ||
                    ReferenceEquals(candidate.Container.ABC, context.Method.ABC))
                .Select(candidate => (candidate, (PointsTo?)null)));
            coverage_complete = candidates.Count > 0;
        }

        var types_found = new List<PointsTo>();
        var terminal_outcomes = receiver is null
            ? []
            : DereferenceOutcomes(context, operation, receiver.Outcomes);
        bool control_flow_exhaustive = receiver?.ControlFlowExhaustive ?? true;
        bool target_exhaustive = receiver?.TargetExhaustive ?? true;
        foreach ((TraitBinding candidate, PointsTo? source) in candidates.DistinctBy(value =>
            new TraitSourceIdentity(
                value.Trait.Container,
                value.Trait.Trait.QNameIndex,
                value.Trait.Trait.Kind,
                value.Source?.Binding.Abc,
                value.Source?.Binding.Instance)))
        {
            bool candidate_resolved = false;
            List<Avm2CallTargetEvidence> interface_evidence =
                candidate.InterfaceBinding &&
                candidate.InterfaceContract is not null
                    ?
                    [
                        Evidence(
                            "InterfaceBinding",
                            "VmExact",
                            context,
                            operation.Instruction,
                            operation.Offset,
                            $"{Qualified(candidate.InterfaceContract.QName)}->" +
                            Qualified(candidate.Trait.QName),
                            candidate.AbcIndex)
                    ]
                    : [];
            if (candidate.Trait.Kind == TraitKind.Class)
            {
                if (candidate.Trait.ClassIndex >= 0 &&
                    candidate.Trait.ClassIndex < candidate.Trait.ABC.Classes.Count)
                {
                    ASInstance class_instance =
                        candidate.Trait.ABC.Classes[candidate.Trait.ClassIndex].Instance;
                    if (types_by_instance.TryGetValue(
                            class_instance,
                            out TypeBinding? class_binding))
                    {
                        types_found.Add(new PointsTo
                        {
                            Binding = class_binding,
                            Receiver = ReceiverKind.Static,
                            SelectionKind = "DeclaredType",
                            SelectorExpression = source?.SelectorExpression,
                            Conditions = source?.Conditions ?? [],
                            Evidence =
                            [
                                .. (source?.Evidence ?? []),
                                .. interface_evidence,
                                Evidence(
                                    "DeclaredType",
                                    "ExactClassTrait",
                                    context,
                                    operation.Instruction,
                                    operation.Offset,
                                    $"{ContainerName(candidate.Container)}." +
                                    $"{Qualified(candidate.Trait.QName)}:{class_binding.Qualified}",
                                    class_binding.AbcIndex)
                            ],
                            Exhaustive = source?.Exhaustive ?? true
                        });
                        candidate_resolved = true;
                    }
                }
                coverage_complete &= candidate_resolved;
                continue;
            }
            if (candidate.Trait.Kind == TraitKind.Getter &&
                candidate.Trait.Method is ASMethod getter)
            {
                PointsToResult returned = ReturnSummary(
                    getter,
                    Binding(candidate.Trait),
                    source is null ? null : ExactReceiver(source),
                    depth + 1);
                foreach (PointsTo value in returned.Types)
                {
                    List<Avm2CallCondition> conditions = DistinctConditions(
                        [.. (source?.Conditions ?? []), .. value.Conditions]);
                    types_found.Add(new PointsTo
                    {
                        Binding = value.Binding,
                        Receiver = value.Receiver,
                        SelectionKind = value.SelectionKind == "FactoryBranch"
                            ? "FactoryBranch"
                            : "GetterReturn",
                        SelectorExpression = value.SelectorExpression,
                        Conditions = conditions,
                        Evidence =
                        [
                            .. (source?.Evidence ?? []),
                            .. interface_evidence,
                            .. value.Evidence,
                            Evidence(
                                "GetterReturn",
                                returned.Exhaustive ? "Exact" : "Partial",
                                context,
                                operation.Instruction,
                                operation.Offset,
                                $"{ContainerName(candidate.Container)}." +
                                $"{Qualified(candidate.Trait.QName)}:{value.Binding.Qualified}",
                                value.Binding.AbcIndex)
                        ],
                        Exhaustive = (source?.Exhaustive ?? true) &&
                            returned.Exhaustive &&
                            value.Exhaustive
                    });
                }
                foreach (Avm2CallTerminalOutcome outcome in returned.Outcomes)
                {
                    terminal_outcomes.Add(new Avm2CallTerminalOutcome
                    {
                        Kind = outcome.Kind,
                        Expression = outcome.Expression,
                        Conditions = DistinctConditions(
                            [.. (source?.Conditions ?? []), .. outcome.Conditions]),
                        Evidence =
                        [
                            .. (source?.Evidence ?? []),
                            .. interface_evidence,
                            .. outcome.Evidence
                        ]
                    });
                }
                control_flow_exhaustive &= returned.ControlFlowExhaustive;
                target_exhaustive &= returned.TargetExhaustive;
                if (returned.Types.Count > 0 || returned.Outcomes.Count > 0)
                {
                    candidate_resolved = true;
                    continue;
                }
            }

            ASMultiname? declared = candidate.Trait.Kind switch
            {
                TraitKind.Slot or TraitKind.Constant => candidate.Trait.Type,
                TraitKind.Getter => candidate.Trait.Method?.ReturnType,
                _ => null
            };
            List<TypeBinding> declared_types =
                FindTypes(declared, candidate.Container.ABC).ToList();
            if (declared_types.Count > 0)
                candidate_resolved = true;
            foreach (TypeBinding binding in declared_types)
            {
                bool stored_value = candidate.Trait.Kind is
                    TraitKind.Slot or TraitKind.Constant;
                bool exact = stored_value &&
                    binding.Instance.Flags.HasFlag(ClassFlags.Final);
                types_found.Add(new PointsTo
                {
                    Binding = binding,
                    Receiver = ReceiverKind.Instance,
                    SelectionKind = "DeclaredType",
                    SelectorExpression = source?.SelectorExpression,
                    Conditions = source?.Conditions ?? [],
                    Evidence =
                    [
                        .. (source?.Evidence ?? []),
                        .. interface_evidence,
                        Evidence(
                            "DeclaredType",
                            exact ? "ExactFinalTrait" : "DeclaredTrait",
                            context,
                            operation.Instruction,
                            operation.Offset,
                            $"{ContainerName(candidate.Container)}." +
                            $"{Qualified(candidate.Trait.QName)}:{binding.Qualified}",
                            binding.AbcIndex)
                    ],
                    Exhaustive = (source?.Exhaustive ?? true) && exact
                });
            }
            if (candidate_resolved &&
                candidate.Trait.Kind is TraitKind.Slot or TraitKind.Constant)
            {
                terminal_outcomes.Add(new Avm2CallTerminalOutcome
                {
                    Kind = "Null",
                    Expression = "null",
                    Conditions = source?.Conditions ?? [],
                    Evidence =
                    [
                        .. (source?.Evidence ?? []),
                        .. interface_evidence,
                        Evidence(
                            "DeclaredType",
                            "NullableSlot",
                            context,
                            operation.Instruction,
                            operation.Offset,
                            $"{ContainerName(candidate.Container)}." +
                            Qualified(candidate.Trait.QName),
                            candidate.AbcIndex)
                    ]
                });
            }
            coverage_complete &= candidate_resolved;
        }
        return Result(
            types_found,
            terminal_outcomes,
            control_flow_exhaustive &&
                coverage_complete &&
                (types_found.Count > 0 || terminal_outcomes.Count > 0),
            target_exhaustive &&
                coverage_complete &&
                (types_found.Count > 0 || terminal_outcomes.Count > 0) &&
                types_found.All(value => value.Exhaustive));
    }

    PointsToResult ResolveSlotValue(
        MethodContext context,
        int slot,
        PointsToResult receiver,
        Avm2DataFlowOperation operation)
    {
        var found = new List<PointsTo>();
        var outcomes = DereferenceOutcomes(
            context,
            operation,
            receiver.Outcomes);
        bool coverage_complete = true;
        foreach (PointsTo type in receiver.Types)
        {
            EffectiveSlotLayout? layout = SlotLayout(
                type.Binding,
                type.Receiver);
            if (layout is null ||
                !layout.Slots.TryGetValue(slot, out ASTrait? trait))
            {
                coverage_complete = false;
                continue;
            }

            if (trait.Kind == TraitKind.Class)
            {
                if (trait.ClassIndex < 0 ||
                    trait.ClassIndex >= trait.ABC.Classes.Count)
                {
                    coverage_complete = false;
                    continue;
                }
                ASInstance class_instance = trait.ABC.Classes[trait.ClassIndex].Instance;
                if (types_by_instance.TryGetValue(
                        class_instance,
                        out TypeBinding? class_binding))
                {
                    found.Add(new PointsTo
                    {
                        Binding = class_binding,
                        Receiver = ReceiverKind.Static,
                        SelectionKind = "DeclaredType",
                        SelectorExpression = type.SelectorExpression,
                        Conditions = type.Conditions,
                        Evidence =
                        [
                            .. type.Evidence,
                            Evidence(
                                "DeclaredType",
                                "ExactClassSlot",
                                context,
                                operation.Instruction,
                                operation.Offset,
                                $"{type.Binding.Qualified}.slot[{slot}]:{class_binding.Qualified}",
                                class_binding.AbcIndex)
                        ],
                        Exhaustive = type.Exhaustive
                    });
                }
                else
                {
                    coverage_complete = false;
                }
                continue;
            }

            List<TypeBinding> declared_types =
                FindTypes(trait.Type, trait.ABC).ToList();
            if (declared_types.Count == 0)
                coverage_complete = false;
            foreach (TypeBinding binding in declared_types)
            {
                bool exact = binding.Instance.Flags.HasFlag(ClassFlags.Final);
                found.Add(new PointsTo
                {
                    Binding = binding,
                    Receiver = ReceiverKind.Instance,
                    SelectionKind = "DeclaredType",
                    SelectorExpression = type.SelectorExpression,
                    Conditions = type.Conditions,
                    Evidence =
                    [
                        .. type.Evidence,
                        Evidence(
                            "DeclaredType",
                            exact ? "ExactFinalSlot" : "DeclaredSlot",
                            context,
                            operation.Instruction,
                            operation.Offset,
                            $"{type.Binding.Qualified}.slot[{slot}]:{binding.Qualified}",
                            binding.AbcIndex)
                    ],
                    Exhaustive = type.Exhaustive && exact
                });
            }
            if (declared_types.Count > 0 &&
                trait.Kind is TraitKind.Slot or TraitKind.Constant)
            {
                outcomes.Add(new Avm2CallTerminalOutcome
                {
                    Kind = "Null",
                    Expression = "null",
                    Conditions = type.Conditions,
                    Evidence =
                    [
                        .. type.Evidence,
                        Evidence(
                            "DeclaredType",
                            "NullableSlot",
                            context,
                            operation.Instruction,
                            operation.Offset,
                            $"{type.Binding.Qualified}.slot[{slot}]",
                            type.Binding.AbcIndex)
                    ]
                });
            }
        }
        return Result(
            found,
            outcomes,
            receiver.ControlFlowExhaustive &&
                coverage_complete &&
                (found.Count > 0 || receiver.Outcomes.Count > 0),
            receiver.TargetExhaustive &&
                coverage_complete &&
                (found.Count > 0 || receiver.Outcomes.Count > 0) &&
                found.All(value => value.Exhaustive));
    }

    IEnumerable<Avm2MethodBinding> ResolveMethods(
        PointsTo receiver,
        CallSite call) =>
        FindMethods(receiver.Binding, receiver.Receiver, call)
            .Where(binding => binding.Method?.Body is not null);

    Dictionary<string, List<TraitBinding>> VisibleTraitBindings(
        TypeBinding binding,
        ReceiverKind receiver,
        ASMultiname property)
    {
        var effective = new Dictionary<string, List<TraitBinding>>(
            StringComparer.Ordinal);
        foreach (ASContainer container in Containers(binding, receiver))
        {
            foreach (IGrouping<string, ASTrait> group in container.Traits
                .Where(trait =>
                    SameProperty(property, trait.QName, container.ABC) ||
                    ProtectedOverrideMatches(
                        Owner(container),
                        property,
                        trait))
                .GroupBy(
                    trait => EffectiveTraitIdentity(
                        Owner(container),
                        trait),
                    StringComparer.Ordinal))
            {
                AddEffectiveTraitGroup(
                    effective,
                    group.Key,
                    group.Select(trait => new TraitBinding
                    {
                        AbcIndex = AbcIndex(container.ABC),
                        Container = container,
                        Trait = trait
                    }).ToList());
            }
        }
        return effective;
    }

    Dictionary<RuntimeBindingIdentity, List<TraitBinding>> CollapseTraitBindings(
        Dictionary<string, List<TraitBinding>> visible)
    {
        var collapsed =
            new Dictionary<RuntimeBindingIdentity, List<TraitBinding>>();
        foreach (List<TraitBinding> traits in visible.Values)
        {
            RuntimeBindingIdentity identity = TargetBindingIdentity(traits);
            if (!collapsed.TryGetValue(
                identity,
                out List<TraitBinding>? existing))
            {
                collapsed.Add(identity, traits);
                continue;
            }
            if (traits.Any(value => value.InterfaceBinding) &&
                existing.All(value => !value.InterfaceBinding))
            {
                collapsed[identity] = traits;
            }
        }
        return collapsed;
    }

    static RuntimeBindingIdentity TargetBindingIdentity(
        IReadOnlyList<TraitBinding> traits)
    {
        TraitBinding selected = traits.FirstOrDefault(value =>
            value.Trait.Kind == TraitKind.Getter) ?? traits[0];
        return new RuntimeBindingIdentity(
            selected.Container,
            selected.Trait);
    }

    static void AddEffectiveTraitGroup(
        Dictionary<string, List<TraitBinding>> effective,
        string identity,
        List<TraitBinding> traits)
    {
        if (!effective.TryGetValue(identity, out List<TraitBinding>? existing))
        {
            effective.Add(identity, traits);
            return;
        }
        if (!existing.All(value =>
                value.Trait.Kind is TraitKind.Getter or TraitKind.Setter) ||
            !traits.All(value =>
                value.Trait.Kind is TraitKind.Getter or TraitKind.Setter))
        {
            return;
        }
        existing.AddRange(traits.Where(candidate =>
            existing.All(value =>
                value.Trait.Kind != candidate.Trait.Kind)));
    }

    IEnumerable<Avm2MethodBinding> FindMethods(
        TypeBinding binding,
        ReceiverKind receiver,
        CallSite call)
    {
        if (call.Property?.IsRuntime == true)
            yield break;
        InterfaceMethodResolution interface_binding =
            receiver == ReceiverKind.Instance && call.Property is not null
                ? ResolveInterfaceMethod(binding, call)
                : new InterfaceMethodResolution(
                    InterfaceBindingStatus.NotApplicable,
                    [],
                    null);
        bool qualified = call.Property?.Kind is
            MultinameKind.QName or MultinameKind.QNameA;
        if (!qualified && call.Property is not null)
        {
            Dictionary<string, List<TraitBinding>> visible =
                VisibleTraitBindings(binding, receiver, call.Property);
            if (interface_binding.Status == InterfaceBindingStatus.Resolved)
            {
                InterfaceContractResolution contracts =
                    ResolveInterfaceContract(
                        binding,
                        call.Property,
                        TraitKind.Method);
                Avm2MethodBinding method =
                    interface_binding.Methods.Single();
                if (method.Trait is not null)
                {
                    foreach (TraitBinding contract in contracts.Contracts)
                    {
                        visible[
                            RuntimeSymbolIdentity(
                                contract.Trait.QName)] =
                        [
                            new TraitBinding
                            {
                                AbcIndex = AbcIndex(method.Owner.ABC),
                                Container = method.Owner,
                                Trait = method.Trait,
                                InterfaceBinding = true,
                                InterfaceContract = contract.Trait
                            }
                        ];
                    }
                }
            }
            else if (interface_binding.Status is
                InterfaceBindingStatus.Invalid or
                InterfaceBindingStatus.MissingImplementation)
            {
                yield break;
            }
            Dictionary<RuntimeBindingIdentity, List<TraitBinding>> effective =
                CollapseTraitBindings(visible);
            if (effective.Count == 1)
            {
                List<TraitBinding> traits = effective.Values.Single();
                if (traits.Count != 1 ||
                    traits[0].Trait.Kind != TraitKind.Method)
                {
                    yield break;
                }
                foreach (Avm2MethodBinding method in MethodBindings(
                    traits[0].Container).Where(candidate =>
                        ReferenceEquals(
                            candidate.Trait,
                            traits[0].Trait)))
                {
                    yield return method;
                }
                yield break;
            }
            yield break;
        }

        if (qualified &&
            interface_binding.Status == InterfaceBindingStatus.Resolved)
        {
            foreach (Avm2MethodBinding method in interface_binding.Methods)
                yield return method;
            yield break;
        }
        if (qualified &&
            interface_binding.Status == InterfaceBindingStatus.Invalid)
        {
            yield break;
        }

        TypeBinding? current = binding;
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        while (current is not null && visited.Add(current.Instance))
        {
            ASContainer container = receiver == ReceiverKind.Static
                ? current.Class
                : current.Instance;
            List<ASTrait> matching_traits = container.Traits
                .Where(trait =>
                    call.Property is null
                        ? NameMatches(trait.QName, call.Name)
                        : SameProperty(
                                call.Property,
                                trait.QName,
                                current.Abc) ||
                            ProtectedOverrideMatches(
                                current,
                                call.Property,
                                trait))
                .ToList();
            if (matching_traits.Count > 0)
            {
                if (matching_traits.Count != 1 ||
                    matching_traits[0].Kind != TraitKind.Method)
                {
                    yield break;
                }
                foreach (Avm2MethodBinding method in MethodBindings(container)
                    .Where(candidate => ReferenceEquals(
                        candidate.Trait,
                        matching_traits[0])))
                {
                    yield return method;
                }
                yield break;
            }
            if (receiver == ReceiverKind.Static)
                break;
            current = Parent(current.Instance);
        }

        if (interface_binding.Status == InterfaceBindingStatus.Resolved)
        {
            foreach (Avm2MethodBinding method in interface_binding.Methods)
                yield return method;
            yield break;
        }
        if (interface_binding.Status == InterfaceBindingStatus.Invalid)
            yield break;

        if (!harman_method_aliases ||
            interface_binding.Status != InterfaceBindingStatus.MissingImplementation &&
            !IsPublicImplementation(call.Property))
        {
            yield break;
        }

        foreach (ASContainer container in Containers(binding, receiver))
        {
            List<Avm2MethodBinding> aliases = MethodBindings(container)
                .Where(method => MethodInfoAliasMatches(method, call))
                .ToList();
            if (aliases.Count == 1)
            {
                yield return aliases[0];
                yield break;
            }
            if (aliases.Count > 1)
                yield break;
        }
    }

    static bool MethodInfoAliasMatches(
        Avm2MethodBinding binding,
        CallSite call)
    {
        return binding is
            {
                Method: not null,
                Trait: not null,
                Role: Avm2MethodBindingRole.MethodTrait
            } &&
            binding.Trait.Kind == TraitKind.Method &&
            binding.Method.Parameters.Count == call.ArgumentCount &&
            !string.IsNullOrEmpty(binding.Method.Name) &&
            string.Equals(binding.Method.Name, call.Name, StringComparison.Ordinal) &&
            IsPublicImplementation(binding.Trait.QName);
    }

    bool UsesMethodInfoAlias(
        Avm2MethodBinding binding,
        CallSite call)
    {
        if (!MethodInfoAliasMatches(binding, call))
            return false;
        return call.Property is null ||
            !PropertiesMatchIndexed(
                call.Property,
                call.Property.Pool.ABC,
                binding.Trait!.QName,
                binding.Abc);
    }

    bool UsesInterfaceBinding(
        TypeBinding receiver,
        ReceiverKind receiver_kind,
        Avm2MethodBinding binding,
        CallSite call,
        out ASTrait? contract)
    {
        contract = null;
        if (receiver_kind != ReceiverKind.Instance ||
            call.Property is null ||
            binding.Trait is null ||
            PropertiesMatchIndexed(
                call.Property,
                call.Property.Pool.ABC,
                binding.Trait.QName,
                binding.Abc))
        {
            return false;
        }
        InterfaceMethodResolution resolution =
            ResolveInterfaceMethod(receiver, call);
        if (resolution.Status != InterfaceBindingStatus.Resolved ||
            !resolution.Methods.Any(candidate =>
                ReferenceEquals(candidate, binding) ||
                candidate.Identity == binding.Identity))
        {
            return false;
        }
        contract = resolution.Contract;
        return contract is not null;
    }

    InterfaceMethodResolution ResolveInterfaceMethod(
        TypeBinding binding,
        CallSite call)
    {
        if (call.Property is null)
        {
            return new InterfaceMethodResolution(
                InterfaceBindingStatus.NotApplicable,
                [],
                null);
        }
        InterfaceContractResolution contract =
            ResolveInterfaceContract(
                binding,
                call.Property,
                TraitKind.Method);
        if (contract.Status != InterfaceBindingStatus.Resolved ||
            contract.Contract?.Trait.Kind != TraitKind.Method ||
            contract.Contracts.Count == 0)
        {
            return new InterfaceMethodResolution(
                contract.Status == InterfaceBindingStatus.NotApplicable
                    ? InterfaceBindingStatus.NotApplicable
                    : InterfaceBindingStatus.Invalid,
                [],
                contract.Contract?.Trait);
        }

        var implementations = new List<Avm2MethodBinding>();
        foreach (TraitBinding interface_contract in contract.Contracts)
        {
            InterfaceMethodResolution implementation =
                ResolveInterfaceMethodImplementation(
                    binding,
                    interface_contract);
            if (implementation.Status != InterfaceBindingStatus.Resolved)
                return implementation;
            implementations.AddRange(implementation.Methods);
        }
        List<Avm2MethodBinding> distinct = implementations
            .DistinctBy(value => value.Identity)
            .ToList();
        return new InterfaceMethodResolution(
            distinct.Count == 1
                ? InterfaceBindingStatus.Resolved
                : InterfaceBindingStatus.Invalid,
            distinct,
            contract.Contract.Trait);
    }

    InterfaceContractResolution ResolveInterfaceContract(
        TypeBinding binding,
        ASMultiname property,
        TraitKind expected_kind)
    {
        var pending = new Stack<(TypeBinding Binding, bool Exit)>();
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        var active = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        var contracts = new List<TraitBinding>();
        bool complete = true;
        pending.Push((binding, false));
        while (pending.Count > 0)
        {
            (TypeBinding current, bool exit) = pending.Pop();
            if (exit)
            {
                active.Remove(current.Instance);
                continue;
            }
            if (active.Contains(current.Instance))
            {
                complete = false;
                continue;
            }
            if (!visited.Add(current.Instance))
                continue;
            active.Add(current.Instance);
            pending.Push((current, true));
            if (current.Instance.Flags.HasFlag(ClassFlags.Interface))
            {
                contracts.AddRange(current.Instance.Traits
                    .Where(trait =>
                        (trait.Kind is
                            TraitKind.Method or
                            TraitKind.Getter or
                            TraitKind.Setter) &&
                        SameProperty(
                            property,
                            trait.QName,
                            current.Abc))
                    .Select(trait => new TraitBinding
                    {
                        AbcIndex = current.AbcIndex,
                        Container = current.Instance,
                        Trait = trait
                    }));
            }
            foreach (ASMultiname interface_name in current.Instance.GetInterfaces())
            {
                List<TypeBinding> matches = FindTypes(
                    interface_name,
                    current.Abc);
                if (matches.Count != 1 ||
                    !matches[0].Instance.Flags.HasFlag(ClassFlags.Interface))
                {
                    complete = false;
                }
                else
                {
                    pending.Push((matches[0], false));
                }
            }
            if (current.Instance.Flags.HasFlag(
                    ClassFlags.Interface))
            {
                if (current.Instance.SuperIndex != 0)
                    complete = false;
                continue;
            }
            List<TypeBinding> parents =
                FindTypes(current.Instance.Super, current.Abc);
            bool builtin_object =
                IsBuiltinObject(current.Instance.Super) &&
                parents.Count == 0;
            if (!builtin_object)
            {
                if (parents.Count != 1 ||
                    parents[0].Instance.Flags.HasFlag(ClassFlags.Interface))
                {
                    complete = false;
                }
                else
                {
                    pending.Push((parents[0], false));
                }
            }
        }
        if (!complete)
        {
            return new InterfaceContractResolution(
                InterfaceBindingStatus.Invalid,
                null,
                []);
        }
        if (contracts.Count == 0)
        {
            return new InterfaceContractResolution(
                InterfaceBindingStatus.NotApplicable,
                null,
                []);
        }

        List<IGrouping<string, TraitBinding>> bindings = contracts
            .GroupBy(
                value => RuntimeSymbolIdentity(
                    value.Trait.QName),
                StringComparer.Ordinal)
            .ToList();
        var selected_contracts = new List<TraitBinding>();
        foreach (IGrouping<string, TraitBinding> binding_group in bindings)
        {
            List<IGrouping<TraitKind, TraitBinding>> kinds = binding_group
                .GroupBy(value => value.Trait.Kind)
                .ToList();
            bool method_accessor_conflict =
                kinds.Any(value => value.Key == TraitKind.Method) &&
                kinds.Any(value => value.Key is TraitKind.Getter or TraitKind.Setter);
            if (method_accessor_conflict ||
                kinds.Any(group =>
                group.Skip(1).Any(candidate =>
                    !TraitSignaturesMatch(
                        group.First().Trait,
                        candidate.Trait))))
            {
                return new InterfaceContractResolution(
                    InterfaceBindingStatus.Invalid,
                    null,
                    []);
            }
            TraitBinding? selected = kinds
                .FirstOrDefault(value => value.Key == expected_kind)?
                .FirstOrDefault();
            if (selected is null)
            {
                return new InterfaceContractResolution(
                    InterfaceBindingStatus.Invalid,
                    null,
                    []);
            }
            selected_contracts.Add(selected);
        }

        TraitBinding first = selected_contracts[0];
        if (selected_contracts.Skip(1).Any(candidate =>
            !TraitSignaturesMatch(first.Trait, candidate.Trait)))
        {
            return new InterfaceContractResolution(
                InterfaceBindingStatus.Invalid,
                null,
                []);
        }
        return new InterfaceContractResolution(
            InterfaceBindingStatus.Resolved,
            first,
            selected_contracts);
    }

    InterfaceMethodResolution ResolveInterfaceMethodImplementation(
        TypeBinding binding,
        TraitBinding contract)
    {
        if (contract.Trait.Method is not ASMethod contract_method ||
            Owner(contract.Container) is not TypeBinding interface_type ||
            !Avm2MethodAnalyzer.TryGetStaticName(
                contract.Trait.QName,
                out string contract_name))
        {
            return new InterfaceMethodResolution(
                InterfaceBindingStatus.Invalid,
                [],
                contract.Trait);
        }
        TypeBinding? introducer = InterfaceIntroducer(
            binding,
            interface_type,
            out List<TypeBinding> hierarchy);
        if (introducer is null)
        {
            return new InterfaceMethodResolution(
                InterfaceBindingStatus.Invalid,
                [],
                contract.Trait);
        }
        PublicBindingResolution contract_binding =
            ResolveContractBinding(introducer, contract.Trait.QName);
        bool contract_method_binding = contract_binding.Status ==
            PublicBindingStatus.Resolved &&
            contract_binding.Traits.Count == 1 &&
            contract_binding.Traits[0].Trait.Kind == TraitKind.Method;
        if (contract_method_binding &&
            (contract_binding.Traits[0].Trait.Method is not ASMethod direct_method ||
                !MethodSignaturesMatch(contract_method, direct_method)))
        {
            return new InterfaceMethodResolution(
                InterfaceBindingStatus.Invalid,
                [],
                contract.Trait);
        }
        TraitBinding? selected_trait = contract_method_binding
            ? contract_binding.Traits[0]
            : null;
        bool public_alias = !contract_method_binding;
        PublicBindingResolution public_binding = public_alias
            ? ResolvePublicBinding(
                introducer,
                contract_name)
            : new PublicBindingResolution(
                PublicBindingStatus.Resolved,
                [selected_trait!]);
        if (public_binding.Status == PublicBindingStatus.Missing)
        {
            return new InterfaceMethodResolution(
                InterfaceBindingStatus.MissingImplementation,
                [],
                contract.Trait);
        }
        if (public_binding.Status != PublicBindingStatus.Resolved ||
            public_binding.Traits.Count != 1 ||
            public_binding.Traits[0].Trait.Kind != TraitKind.Method ||
            public_binding.Traits[0].Trait.Method is not ASMethod implementation ||
            !MethodSignaturesMatch(contract_method, implementation))
        {
            return new InterfaceMethodResolution(
                InterfaceBindingStatus.Invalid,
                [],
                contract.Trait);
        }

        selected_trait = public_binding.Traits[0];
        bool tracks_public_vtable = public_alias;
        int introducer_index = hierarchy.FindIndex(value =>
            ReferenceEquals(value.Instance, introducer.Instance));
        for (int index = introducer_index + 1; index < hierarchy.Count; index++)
        {
            List<TraitBinding> contract_traits = DirectContractTraits(
                hierarchy[index],
                contract.Trait.QName);
            if (contract_traits.Count > 0)
            {
                if (contract_traits.Count != 1 ||
                    contract_traits[0].Trait.Kind != TraitKind.Method ||
                    !contract_traits[0].Trait.Attributes.HasFlag(
                        TraitAttributes.Override) ||
                    contract_traits[0].Trait.Method is not ASMethod contract_override ||
                    !MethodSignaturesMatch(
                        contract_method,
                        contract_override))
                {
                    return new InterfaceMethodResolution(
                        InterfaceBindingStatus.Invalid,
                        [],
                        contract.Trait);
                }
                selected_trait = contract_traits[0];
                tracks_public_vtable = false;
                continue;
            }
            if (!tracks_public_vtable)
                continue;
            List<TraitBinding> overrides = DirectPublicTraits(
                    hierarchy[index],
                    contract_name)
                .Where(value =>
                    value.Trait.Attributes.HasFlag(TraitAttributes.Override))
                .ToList();
            if (overrides.Count == 0)
                continue;
            if (overrides.Count != 1 ||
                overrides[0].Trait.Kind != TraitKind.Method ||
                overrides[0].Trait.Method is not ASMethod override_method ||
                !MethodSignaturesMatch(contract_method, override_method))
            {
                return new InterfaceMethodResolution(
                    InterfaceBindingStatus.Invalid,
                    [],
                    contract.Trait);
            }
            selected_trait = overrides[0];
        }

        List<Avm2MethodBinding> methods = MethodBindings(
                selected_trait.Container)
            .Where(value =>
                ReferenceEquals(value.Trait, selected_trait.Trait))
            .DistinctBy(value => value.Identity)
            .ToList();
        return new InterfaceMethodResolution(
            methods.Count == 1
                ? InterfaceBindingStatus.Resolved
                : InterfaceBindingStatus.Invalid,
            methods,
            contract.Trait);
    }

    InterfaceTraitResolution ResolveInterfaceTraitImplementation(
        TypeBinding binding,
        TraitBinding contract)
    {
        if (contract.Trait.Method is not ASMethod contract_method ||
            Owner(contract.Container) is not TypeBinding interface_type ||
            !Avm2MethodAnalyzer.TryGetStaticName(
                contract.Trait.QName,
                out string contract_name))
        {
            return new InterfaceTraitResolution(
                InterfaceBindingStatus.Invalid,
                null);
        }
        TypeBinding? introducer = InterfaceIntroducer(
            binding,
            interface_type,
            out List<TypeBinding> hierarchy);
        if (introducer is null)
        {
            return new InterfaceTraitResolution(
                InterfaceBindingStatus.Invalid,
                null);
        }
        PublicBindingResolution contract_binding =
            ResolveContractBinding(introducer, contract.Trait.QName);
        TraitBinding? selected_trait = contract_binding.Status ==
            PublicBindingStatus.Resolved &&
            contract_binding.Traits.All(value =>
                value.Trait.Kind is TraitKind.Getter or TraitKind.Setter)
                ? contract_binding.Traits.SingleOrDefault(value =>
                    value.Trait.Kind == TraitKind.Getter)
                : null;
        bool contract_accessor_binding =
            contract_binding.Status == PublicBindingStatus.Resolved &&
            contract_binding.Traits.All(value =>
                value.Trait.Kind is TraitKind.Getter or TraitKind.Setter);
        if (contract_accessor_binding &&
            (selected_trait?.Trait.Method is not ASMethod direct_getter ||
                !MethodSignaturesMatch(contract_method, direct_getter)))
        {
            return new InterfaceTraitResolution(
                InterfaceBindingStatus.Invalid,
                null);
        }
        bool public_alias = !contract_accessor_binding;
        PublicBindingResolution public_binding = public_alias
            ? ResolvePublicBinding(
                introducer,
                contract_name)
            : new PublicBindingResolution(
                PublicBindingStatus.Resolved,
                contract_binding.Traits);
        if (public_binding.Status == PublicBindingStatus.Missing)
        {
            return new InterfaceTraitResolution(
                InterfaceBindingStatus.MissingImplementation,
                null);
        }
        selected_trait = public_binding.Traits
            .SingleOrDefault(value =>
                value.Trait.Kind == TraitKind.Getter);
        if (public_binding.Status != PublicBindingStatus.Resolved ||
            selected_trait?.Trait.Method is not ASMethod implementation ||
            public_binding.Traits.Any(value =>
                value.Trait.Kind is not (
                    TraitKind.Getter or
                    TraitKind.Setter)) ||
            !MethodSignaturesMatch(contract_method, implementation))
        {
            return new InterfaceTraitResolution(
                InterfaceBindingStatus.Invalid,
                null);
        }

        bool tracks_public_vtable = public_alias;
        int introducer_index = hierarchy.FindIndex(value =>
            ReferenceEquals(value.Instance, introducer.Instance));
        for (int index = introducer_index + 1; index < hierarchy.Count; index++)
        {
            List<TraitBinding> contract_traits = DirectContractTraits(
                hierarchy[index],
                contract.Trait.QName);
            if (contract_traits.Count > 0)
            {
                if (contract_traits.Count != 1 ||
                    contract_traits[0].Trait.Kind != TraitKind.Getter ||
                    !contract_traits[0].Trait.Attributes.HasFlag(
                        TraitAttributes.Override) ||
                    contract_traits[0].Trait.Method is not ASMethod contract_override ||
                    !MethodSignaturesMatch(
                        contract_method,
                        contract_override))
                {
                    return new InterfaceTraitResolution(
                        InterfaceBindingStatus.Invalid,
                        null);
                }
                selected_trait = contract_traits[0];
                tracks_public_vtable = false;
                continue;
            }
            if (!tracks_public_vtable)
                continue;
            List<TraitBinding> overrides = DirectPublicTraits(
                    hierarchy[index],
                    contract_name)
                .Where(value =>
                    value.Trait.Attributes.HasFlag(TraitAttributes.Override) &&
                    value.Trait.Kind == TraitKind.Getter)
                .ToList();
            if (overrides.Count == 0)
                continue;
            if (overrides.Count != 1 ||
                overrides[0].Trait.Method is not ASMethod override_method ||
                !MethodSignaturesMatch(contract_method, override_method))
            {
                return new InterfaceTraitResolution(
                    InterfaceBindingStatus.Invalid,
                    null);
            }
            selected_trait = overrides[0];
        }
        return new InterfaceTraitResolution(
            InterfaceBindingStatus.Resolved,
            selected_trait);
    }

    TypeBinding? InterfaceIntroducer(
        TypeBinding binding,
        TypeBinding interface_type,
        out List<TypeBinding> hierarchy)
    {
        hierarchy = ClassHierarchy(binding, out bool complete);
        if (!complete)
            return null;
        foreach (TypeBinding candidate in hierarchy)
        {
            if (DirectInterfaceClosureContains(
                candidate,
                interface_type,
                out bool closure_complete))
            {
                return closure_complete ? candidate : null;
            }
            if (!closure_complete)
                return null;
        }
        return null;
    }

    List<TypeBinding> ClassHierarchy(
        TypeBinding binding,
        out bool complete)
    {
        var hierarchy = new List<TypeBinding>();
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        TypeBinding? current = binding;
        complete = true;
        while (current is not null)
        {
            if (!visited.Add(current.Instance) ||
                current.Instance.Flags.HasFlag(ClassFlags.Interface))
            {
                complete = false;
                return [];
            }
            hierarchy.Add(current);
            List<TypeBinding> parents =
                FindTypes(current.Instance.Super, current.Abc);
            if (IsBuiltinObject(current.Instance.Super) &&
                parents.Count == 0)
            {
                break;
            }
            if (parents.Count != 1 ||
                parents[0].Instance.Flags.HasFlag(ClassFlags.Interface))
            {
                complete = false;
                return [];
            }
            current = parents[0];
        }
        hierarchy.Reverse();
        return hierarchy;
    }

    bool DirectInterfaceClosureContains(
        TypeBinding binding,
        TypeBinding interface_type,
        out bool complete)
    {
        var pending = new Stack<TypeBinding>();
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        var active = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        foreach (ASMultiname interface_name in binding.Instance.GetInterfaces())
        {
            List<TypeBinding> matches = FindTypes(interface_name, binding.Abc);
            if (matches.Count != 1 ||
                !matches[0].Instance.Flags.HasFlag(ClassFlags.Interface))
            {
                complete = false;
                return false;
            }
            pending.Push(matches[0]);
        }

        complete = true;
        while (pending.Count > 0)
        {
            TypeBinding current = pending.Pop();
            if (ReferenceEquals(
                current.Instance,
                interface_type.Instance) &&
                ReferenceEquals(current.Abc, interface_type.Abc))
            {
                return true;
            }
            if (!active.Add(current.Instance))
            {
                complete = false;
                return false;
            }
            if (!visited.Add(current.Instance))
            {
                active.Remove(current.Instance);
                continue;
            }
            foreach (ASMultiname interface_name in
                current.Instance.GetInterfaces())
            {
                List<TypeBinding> matches = FindTypes(
                    interface_name,
                    current.Abc);
                if (matches.Count != 1 ||
                    !matches[0].Instance.Flags.HasFlag(ClassFlags.Interface))
                {
                    complete = false;
                    return false;
                }
                if (active.Contains(matches[0].Instance))
                {
                    complete = false;
                    return false;
                }
                pending.Push(matches[0]);
            }
            active.Remove(current.Instance);
        }
        return false;
    }

    PublicBindingResolution ResolvePublicBinding(
        TypeBinding binding,
        string name) =>
        ResolveBinding(
            binding,
            (owner, trait) =>
                NameMatches(trait.QName, name) &&
                IsStandardPublicImplementation(trait.QName));

    PublicBindingResolution ResolveContractBinding(
        TypeBinding binding,
        ASMultiname contract) =>
        ResolveBinding(
            binding,
            (owner, trait) =>
                SameProperty(contract, trait.QName, owner.Abc));

    PublicBindingResolution ResolveBinding(
        TypeBinding binding,
        Func<TypeBinding, ASTrait, bool> predicate)
    {
        List<TypeBinding> hierarchy = ClassHierarchy(binding, out bool complete);
        if (!complete)
        {
            return new PublicBindingResolution(
                PublicBindingStatus.Invalid,
                []);
        }
        IReadOnlyList<TraitBinding> effective = [];
        foreach (TypeBinding owner in hierarchy)
        {
            List<TraitBinding> direct = DirectTraits(owner, predicate);
            if (direct.Count == 0)
                continue;
            if (!DirectBindingIsValid(direct))
            {
                return new PublicBindingResolution(
                    PublicBindingStatus.Invalid,
                    []);
            }
            if (effective.Count == 0)
            {
                effective = direct;
                continue;
            }
            bool direct_accessors = direct.All(value =>
                value.Trait.Kind is TraitKind.Getter or TraitKind.Setter);
            bool inherited_accessors = effective.All(value =>
                value.Trait.Kind is TraitKind.Getter or TraitKind.Setter);
            if (direct_accessors && inherited_accessors)
            {
                if (direct.Any(candidate =>
                {
                    TraitBinding? inherited = effective.FirstOrDefault(value =>
                        value.Trait.Kind == candidate.Trait.Kind);
                    return inherited is null
                        ? candidate.Trait.Attributes.HasFlag(
                            TraitAttributes.Override)
                        : !candidate.Trait.Attributes.HasFlag(
                                TraitAttributes.Override) ||
                            inherited.Trait.Attributes.HasFlag(
                                TraitAttributes.Final) ||
                            !TraitSignaturesMatch(
                                inherited.Trait,
                                candidate.Trait);
                }))
                {
                    return new PublicBindingResolution(
                        PublicBindingStatus.Invalid,
                        []);
                }
                effective =
                [
                    .. direct,
                    .. effective.Where(inherited =>
                        direct.All(candidate =>
                            candidate.Trait.Kind != inherited.Trait.Kind))
                ];
                continue;
            }
            if (direct.Count == 1 &&
                direct[0].Trait.Kind == TraitKind.Method &&
                effective.Count == 1 &&
                effective[0].Trait.Kind == TraitKind.Method)
            {
                if (!direct[0].Trait.Attributes.HasFlag(
                        TraitAttributes.Override) ||
                    effective[0].Trait.Attributes.HasFlag(
                        TraitAttributes.Final) ||
                    !TraitSignaturesMatch(
                        effective[0].Trait,
                        direct[0].Trait))
                {
                    return new PublicBindingResolution(
                        PublicBindingStatus.Invalid,
                        []);
                }
                effective = direct;
                continue;
            }
            if (direct.Count == 1 &&
                direct[0].Trait.Kind is not (
                    TraitKind.Method or
                    TraitKind.Getter or
                    TraitKind.Setter))
            {
                effective = direct;
                continue;
            }
            return new PublicBindingResolution(
                PublicBindingStatus.Invalid,
                []);
        }
        return effective.Count == 0
            ? new PublicBindingResolution(PublicBindingStatus.Missing, [])
            : new PublicBindingResolution(
                PublicBindingStatus.Resolved,
                effective);
    }

    List<TraitBinding> DirectPublicTraits(
        TypeBinding binding,
        string name) =>
        DirectTraits(
            binding,
            (owner, trait) =>
                NameMatches(trait.QName, name) &&
                IsStandardPublicImplementation(trait.QName));

    List<TraitBinding> DirectContractTraits(
        TypeBinding binding,
        ASMultiname contract) =>
        DirectTraits(
            binding,
            (owner, trait) =>
                SameProperty(contract, trait.QName, owner.Abc));

    static List<TraitBinding> DirectTraits(
        TypeBinding binding,
        Func<TypeBinding, ASTrait, bool> predicate) =>
        binding.Instance.Traits
            .Where(value => predicate(binding, value))
            .Select(value => new TraitBinding
            {
                AbcIndex = binding.AbcIndex,
                Container = binding.Instance,
                Trait = value
            })
            .ToList();

    static bool DirectBindingIsValid(
        IReadOnlyList<TraitBinding> traits) =>
        traits.Count == 1 ||
        traits.Count == 2 &&
        traits.All(value =>
            value.Trait.Kind is TraitKind.Getter or TraitKind.Setter) &&
        traits.Select(value => value.Trait.Kind).Distinct().Count() == 2;

    bool TraitSignaturesMatch(ASTrait left, ASTrait right) =>
        left.Kind == right.Kind &&
        left.Method is not null &&
        right.Method is not null &&
        MethodSignaturesMatch(left.Method, right.Method);

    bool MethodSignaturesMatch(ASMethod contract, ASMethod implementation)
    {
        if (contract.Parameters.Count != implementation.Parameters.Count ||
            contract.Parameters.Count(value => value.IsOptional) !=
                implementation.Parameters.Count(value => value.IsOptional) ||
            !DeclaredTypesMatch(
                contract.ReturnType,
                contract.ABC,
                implementation.ReturnType,
                implementation.ABC))
        {
            return false;
        }
        for (int index = 0; index < contract.Parameters.Count; index++)
        {
            if (!DeclaredTypesMatch(
                contract.Parameters[index].Type,
                contract.ABC,
                implementation.Parameters[index].Type,
                implementation.ABC))
            {
                return false;
            }
        }
        return true;
    }

    bool DeclaredTypesMatch(
        ASMultiname? left,
        ABCFile left_abc,
        ASMultiname? right,
        ABCFile right_abc) =>
        DeclaredTypesMatch(
            left,
            left_abc,
            right,
            right_abc,
            0);

    bool DeclaredTypesMatch(
        ASMultiname? left,
        ABCFile left_abc,
        ASMultiname? right,
        ABCFile right_abc,
        int depth)
    {
        if (depth > 32)
            return false;
        if (left is null || right is null)
            return left is null && right is null;
        if (left.Kind == MultinameKind.TypeName ||
            right.Kind == MultinameKind.TypeName)
        {
            if (left.Kind != MultinameKind.TypeName ||
                right.Kind != MultinameKind.TypeName ||
                left.TypeIndices.Count != right.TypeIndices.Count ||
                !DeclaredTypesMatch(
                    left.QName,
                    left_abc,
                    right.QName,
                    right_abc,
                    depth + 1))
            {
                return false;
            }
            for (int index = 0; index < left.TypeIndices.Count; index++)
            {
                if (!DeclaredTypesMatch(
                    left.Pool.Multinames[left.TypeIndices[index]],
                    left_abc,
                    right.Pool.Multinames[right.TypeIndices[index]],
                    right_abc,
                    depth + 1))
                {
                    return false;
                }
            }
            return true;
        }
        if ((IsPrivate(left) || IsPrivate(right)) &&
            !ReferenceEquals(left_abc, right_abc))
        {
            return false;
        }
        if (RuntimeSymbolIdentity(left) !=
            RuntimeSymbolIdentity(right))
        {
            return false;
        }
        List<TypeBinding> left_types = FindTypes(left, left_abc);
        List<TypeBinding> right_types = FindTypes(right, right_abc);
        if (left_types.Count == 0 && right_types.Count == 0)
            return true;
        return left_types.Count == 1 &&
            right_types.Count == 1 &&
            ReferenceEquals(left_types[0].Abc, right_types[0].Abc) &&
            ReferenceEquals(left_types[0].Instance, right_types[0].Instance);
    }

    static bool IsPublicImplementation(ASMultiname? name)
    {
        if (name is null ||
            name.Kind is not (MultinameKind.QName or MultinameKind.QNameA))
        {
            return false;
        }
        return name.Namespace?.IsPublicRoot == true;
    }

    static bool IsStandardPublicImplementation(ASMultiname? name) =>
        name is not null &&
        name.Kind is MultinameKind.QName or MultinameKind.QNameA &&
        name.Namespace?.IsPublicRoot == true;

    InterfaceTraitResolution ResolveInterfaceTrait(
        TypeBinding binding,
        ASMultiname property)
    {
        InterfaceContractResolution contract =
            ResolveInterfaceContract(
                binding,
                property,
                TraitKind.Getter);
        if (contract.Status != InterfaceBindingStatus.Resolved ||
            contract.Contract?.Trait.Kind != TraitKind.Getter ||
            contract.Contracts.Count == 0)
        {
            return new InterfaceTraitResolution(
                contract.Status == InterfaceBindingStatus.NotApplicable
                    ? InterfaceBindingStatus.NotApplicable
                    : InterfaceBindingStatus.Invalid,
                null);
        }

        var implementations = new List<TraitBinding>();
        foreach (TraitBinding interface_contract in contract.Contracts)
        {
            InterfaceTraitResolution implementation =
                ResolveInterfaceTraitImplementation(
                    binding,
                    interface_contract);
            if (implementation.Status != InterfaceBindingStatus.Resolved ||
                implementation.Trait is null)
            {
                return implementation;
            }
            implementations.Add(implementation.Trait);
        }
        List<TraitBinding> distinct = implementations
            .DistinctBy(value => new TraitSourceIdentity(
                value.Container,
                value.Trait.QNameIndex,
                value.Trait.Kind,
                value.Container.ABC,
                Owner(value.Container)?.Instance))
            .ToList();
        if (distinct.Count != 1)
        {
            return new InterfaceTraitResolution(
                InterfaceBindingStatus.Invalid,
                null);
        }
        TraitBinding selected = distinct[0];
        return new InterfaceTraitResolution(
            InterfaceBindingStatus.Resolved,
            new TraitBinding
            {
                AbcIndex = selected.AbcIndex,
                Container = selected.Container,
                Trait = selected.Trait,
                InterfaceBinding = true,
                InterfaceContract = contract.Contract.Trait
            });
    }

    IEnumerable<TraitBinding> FindTraits(
        TypeBinding binding,
        ReceiverKind receiver,
        ASMultiname property)
    {
        bool qualified = property.Kind is
            MultinameKind.QName or MultinameKind.QNameA;
        InterfaceTraitResolution interface_binding =
            receiver == ReceiverKind.Instance
                ? ResolveInterfaceTrait(binding, property)
                : new InterfaceTraitResolution(
                    InterfaceBindingStatus.NotApplicable,
                    null);
        if (!qualified)
        {
            Dictionary<string, List<TraitBinding>> visible =
                VisibleTraitBindings(binding, receiver, property);
            if (interface_binding.Status == InterfaceBindingStatus.Resolved &&
                interface_binding.Trait is not null)
            {
                InterfaceContractResolution contracts =
                    ResolveInterfaceContract(
                        binding,
                        property,
                        TraitKind.Getter);
                foreach (TraitBinding contract in contracts.Contracts)
                {
                    visible[
                        RuntimeSymbolIdentity(
                            contract.Trait.QName)] =
                    [
                        new TraitBinding
                        {
                            AbcIndex = interface_binding.Trait.AbcIndex,
                            Container = interface_binding.Trait.Container,
                            Trait = interface_binding.Trait.Trait,
                            InterfaceBinding = true,
                            InterfaceContract = contract.Trait
                        }
                    ];
                }
            }
            else if (interface_binding.Status is
                InterfaceBindingStatus.Invalid or
                InterfaceBindingStatus.MissingImplementation)
            {
                yield break;
            }
            Dictionary<RuntimeBindingIdentity, List<TraitBinding>> effective =
                CollapseTraitBindings(visible);
            if (effective.Count == 1)
            {
                foreach (TraitBinding trait in effective.Values.Single())
                    yield return trait;
                yield break;
            }
            yield break;
        }

        if (interface_binding.Status == InterfaceBindingStatus.Resolved &&
            interface_binding.Trait is not null)
        {
            yield return interface_binding.Trait;
            yield break;
        }
        if (interface_binding.Status is
            InterfaceBindingStatus.Invalid or
            InterfaceBindingStatus.MissingImplementation)
        {
            yield break;
        }
        foreach (ASContainer container in Containers(binding, receiver))
        {
            List<ASTrait> matches = container.Traits
                .Where(trait =>
                    SameProperty(
                        property,
                        trait.QName,
                        binding.Abc) ||
                    ProtectedOverrideMatches(
                        Owner(container),
                        property,
                        trait))
                .ToList();
            foreach (ASTrait trait in matches)
            {
                yield return new TraitBinding
                {
                    AbcIndex = AbcIndex(container.ABC),
                    Container = container,
                    Trait = trait
                };
            }
            if (matches.Count > 0)
                yield break;
        }
    }

    bool ProtectedOverrideMatches(
        TypeBinding? owner,
        ASMultiname property,
        ASTrait trait)
    {
        if (owner is null ||
            !trait.Attributes.HasFlag(TraitAttributes.Override) ||
            !IsOwnProtectedTrait(owner, trait))
        {
            return false;
        }
        TypeBinding current_owner = owner;
        ASTrait current_trait = trait;
        while (ProtectedBaseTrait(current_owner, current_trait) is
            (TypeBinding Owner, ASTrait Trait) overridden)
        {
            if (SameProperty(
                property,
                overridden.Trait.QName,
                overridden.Owner.Abc))
            {
                return true;
            }
            current_owner = overridden.Owner;
            current_trait = overridden.Trait;
        }
        return false;
    }

    string EffectiveTraitIdentity(TypeBinding? owner, ASTrait trait)
    {
        TypeBinding? current_owner = owner;
        ASTrait current_trait = trait;
        while (current_owner is not null &&
            current_trait.Attributes.HasFlag(TraitAttributes.Override) &&
            IsOwnProtectedTrait(current_owner, current_trait) &&
            ProtectedBaseTrait(current_owner, current_trait) is
                (TypeBinding Owner, ASTrait Trait) overridden)
        {
            current_owner = overridden.Owner;
            current_trait = overridden.Trait;
        }
        return RuntimeSymbolIdentity(current_trait.QName);
    }

    (TypeBinding Owner, ASTrait Trait)? ProtectedBaseTrait(
        TypeBinding owner,
        ASTrait trait)
    {
        TypeBinding? parent = Parent(owner.Instance);
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        while (parent is not null && visited.Add(parent.Instance))
        {
            ASTrait? candidate = parent.Instance.Traits.FirstOrDefault(value =>
                value.Kind == trait.Kind &&
                NamesMatch(value.QName, trait.QName) &&
                IsOwnProtectedTrait(parent, value));
            if (candidate is not null)
                return (parent, candidate);
            parent = Parent(parent.Instance);
        }
        return null;
    }

    bool IsOwnProtectedTrait(TypeBinding owner, ASTrait trait) =>
        owner.Instance.Flags.HasFlag(ClassFlags.ProtectedNamespace) &&
        trait.QName.Kind is MultinameKind.QName or MultinameKind.QNameA &&
        RuntimeNamespaceIdentity(trait.QName.Namespace) ==
        RuntimeNamespaceIdentity(
            owner.Instance.ProtectedNamespace);

    IEnumerable<ASContainer> Containers(
        TypeBinding binding,
        ReceiverKind receiver)
    {
        if (receiver == ReceiverKind.Static)
        {
            yield return binding.Class;
            yield break;
        }
        TypeBinding? current = binding;
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        while (current is not null && visited.Add(current.Instance))
        {
            yield return current.Instance;
            current = Parent(current.Instance);
        }
    }

    TypeBinding? Parent(ASContainer? container)
    {
        ASInstance? instance = container switch
        {
            ASInstance value => value,
            ASClass value => value.Instance,
            _ => null
        };
        return instance is null ? null : Parent(instance);
    }

    TypeBinding? Parent(ASInstance instance)
    {
        if (parents.TryGetValue(
                instance,
                out TypeBinding? cached))
        {
            return cached;
        }
        if (!Avm2MethodAnalyzer.TryGetStaticName(
                instance.Super,
                out _))
        {
            parents.Add(instance, null);
            return null;
        }
        List<TypeBinding> matches = FindTypes(instance.Super, instance.ABC);
        TypeBinding? parent =
            IsBuiltinObject(instance.Super) &&
            matches.Count == 0
                ? null
                : matches.Count == 1
                    ? matches[0]
                    : null;
        parents.Add(instance, parent);
        return parent;
    }

    TypeBinding? Owner(ASContainer? container)
    {
        ASInstance? instance = container switch
        {
            ASInstance value => value,
            ASClass value => value.Instance,
            _ => null
        };
        if (instance is null)
            return null;
        return types_by_instance.GetValueOrDefault(instance);
    }

    TypeBinding? FindSingleType(ASMultiname? name, ABCFile requester)
    {
        List<TypeBinding> matches = FindTypes(name, requester);
        return matches.Count == 1 ? matches[0] : null;
    }

    List<TypeBinding> FindTypes(
        ASMultiname? name,
        ABCFile requester,
        bool preserve_ambiguity = false)
    {
        if (name is null)
            return [];
        if (name.Kind is MultinameKind.QName or MultinameKind.QNameA)
        {
            if (!Avm2MethodAnalyzer.TryGetStaticName(name, out _))
                return [];
            string identity = RuntimeSymbolIdentity(name);
            List<TypeBinding> matches = types.GetValueOrDefault(identity) ?? [];
            return IsPrivate(name)
                ? matches
                    .Where(candidate => ReferenceEquals(candidate.Abc, requester))
                    .ToList()
                : [.. matches];
        }
        if (name.Kind is not (MultinameKind.Multiname or MultinameKind.MultinameA) ||
            !Avm2MethodAnalyzer.TryGetStaticName(name, out _))
        {
            return [];
        }
        HashSet<string> namespaces;
        try
        {
            if (name.NamespaceSet is not ASNamespaceSet namespace_set)
                return [];
            namespaces = namespace_set.NamespaceIndices
                .Where(index => index > 0 && index < name.Pool.Namespaces.Count)
                .Select(index => RuntimeNamespaceIdentity(
                    name.Pool.Namespaces[index]))
                .ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
        List<TypeBinding> set_matches = all_types
            .Where(candidate =>
                NamesMatch(candidate.Instance.QName, name) &&
                namespaces.Contains(RuntimeNamespaceIdentity(
                    candidate.Instance.QName.Namespace)) &&
                (!candidate.PrivateNamespace ||
                    ReferenceEquals(candidate.Abc, requester)))
            .ToList();
        return preserve_ambiguity || set_matches
            .Select(candidate => candidate.RuntimeIdentity)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .Count() == 1
            ? set_matches
            : [];
    }

    List<TypeBinding> FindTypes(string? name, ABCFile requester)
    {
        if (string.IsNullOrEmpty(name))
            return [];
        TypeBinding[] exact = qualified_types.GetValueOrDefault(name) ?? [];
        List<TypeBinding> local = exact
            .Where(candidate => ReferenceEquals(candidate.Abc, requester))
            .ToList();
        if (local.Count > 0)
            return local;
        List<TypeBinding> public_exact = exact
            .Where(candidate => !candidate.PrivateNamespace)
            .ToList();
        if (public_exact.Count > 0)
            return public_exact;
        if (name.Contains('.'))
            return [];
        TypeBinding[] named = static_name_types.GetValueOrDefault(name) ?? [];
        List<TypeBinding> local_named = named
            .Where(candidate =>
                ReferenceEquals(candidate.Abc, requester))
            .ToList();
        if (local_named.Count > 0)
            return local_named;
        List<TypeBinding> public_named = named
            .Where(candidate =>
                !candidate.PrivateNamespace)
            .Take(2)
            .ToList();
        return public_named.Count == 1 ? public_named : [];
    }

    static void AddTypeLookup(
        Dictionary<string, List<TypeBinding>> lookup,
        string name,
        TypeBinding binding)
    {
        if (!lookup.TryGetValue(name, out List<TypeBinding>? values))
        {
            values = [];
            lookup.Add(name, values);
        }
        values.Add(binding);
    }

    void IndexTraits(ASContainer container, int abc_index)
    {
        foreach (ASTrait trait in container.Traits)
        {
            string identity = RuntimeSymbolIdentity(trait.QName);
            if (!traits.TryGetValue(identity, out List<TraitBinding>? values))
            {
                values = [];
                traits.Add(identity, values);
            }
            values.Add(new TraitBinding
            {
                AbcIndex = abc_index,
                Container = container,
                Trait = trait
            });
        }
    }

    MethodContext ExternalContext(
        ASMethod method,
        Avm2MethodBinding? binding,
        IReadOnlyList<ASInstruction> code,
        Avm2MethodAnalysis analysis,
        Avm2DataFlowAnalysis flow)
    {
        ASMethodBody body = method.Body ??
            throw new ArgumentException(
                "The caller has no method body.",
                nameof(method));
        if (flow.ExactReceiver is not null &&
            !ExactReceiverMatches(binding, flow.ExactReceiver))
        {
            throw new ArgumentException(
                "Exact receiver provenance does not match the method binding.",
                nameof(flow));
        }
        Avm2DeclaringScopeResolution scope_resolution =
            binding is null
                ? declaring_scopes.Resolve(method)
                : declaring_scopes.Resolve(binding);
        Avm2DataFlowScopeContext? expected_scope =
            scope_resolution.Proven
                ? scope_resolution.Context
                : flow.DeclaringScopeContext;
        bool external_valid =
            CodeMatches(code, analysis.DecodedCode) &&
            flow.MatchesSource(
                body,
                analysis,
                binding,
                flow.ExactReceiver,
                expected_scope);
        if (external_valid)
        {
            var external_key = new ExternalContextKey(
                method,
                binding,
                flow.ExactReceiver,
                flow);
            if (external_contexts.TryGetValue(
                    external_key,
                    out MethodContext? cached))
            {
                return cached;
            }
            MethodContext external = BuildContext(
                method,
                binding,
                code.ToList(),
                analysis,
                flow);
            external_contexts.Add(
                external_key,
                external);
            return external;
        }
        if (!scope_resolution.Proven)
        {
            throw new ArgumentException(
                "The data-flow analysis has no valid source provenance and no canonical declaring-scope proof is available.",
                nameof(flow));
        }
        MethodContext? canonical = Context(
            method,
            binding,
            flow.ExactReceiver);
        return canonical ??
            throw new ArgumentException(
                "The canonical caller context could not be analyzed.",
                nameof(flow));
    }

    static bool CodeMatches(
        IReadOnlyList<ASInstruction> supplied,
        IReadOnlyList<ASInstruction> decoded)
    {
        if (supplied.Count != decoded.Count)
            return false;
        for (int index = 0;
            index < supplied.Count;
            index++)
        {
            if (!ReferenceEquals(
                    supplied[index],
                    decoded[index]))
            {
                return false;
            }
        }
        return true;
    }

    MethodContext? ScopedContext(
        ASMethod method,
        Avm2MethodBinding? binding,
        Avm2ExactReceiver? exact_receiver,
        Avm2DataFlowScopeContext scope)
    {
        var key = new ScopedMethodContextKey(
            method,
            binding,
            exact_receiver,
            scope);
        if (scoped_contexts.TryGetValue(
                key,
                out MethodContext? cached))
        {
            return cached;
        }
        if (exact_receiver is not null &&
            !ExactReceiverMatches(
                binding,
                exact_receiver))
        {
            scoped_contexts[key] = null;
            return null;
        }
        if (method.Body is not ASMethodBody body)
        {
            scoped_contexts[key] = null;
            return null;
        }
        try
        {
            Avm2MethodAnalysis analysis =
                Avm2MethodAnalyzer.Analyze(body);
            List<ASInstruction> code =
                analysis.DecodedCode.ToList();
            Avm2DataFlowAnalysis flow =
                declaring_scopes.Analyze(
                    body,
                    analysis,
                    binding,
                    exact_receiver,
                    scope);
            MethodContext context = BuildContext(
                method,
                binding,
                code,
                analysis,
                flow);
            scoped_contexts[key] = context;
            return context;
        }
        catch
        {
            scoped_contexts[key] = null;
            return null;
        }
    }

    MethodContext? Context(
        ASMethod method,
        Avm2MethodBinding? binding = null,
        Avm2ExactReceiver? exact_receiver = null)
    {
        var key = new MethodContextKey(
            method,
            binding,
            exact_receiver);
        if (contexts.TryGetValue(key, out MethodContext? cached))
            return cached;
        if (exact_receiver is not null &&
            !ExactReceiverMatches(binding, exact_receiver))
        {
            contexts[key] = null;
            return null;
        }
        if (method.Body is null)
        {
            contexts[key] = null;
            return null;
        }
        try
        {
            Avm2MethodAnalysis analysis = Avm2MethodAnalyzer.Analyze(method.Body);
            List<ASInstruction> code = analysis.DecodedCode.ToList();
            Avm2DataFlowAnalysis flow = declaring_scopes.Analyze(
                method.Body,
                analysis,
                binding,
                exact_receiver);
            MethodContext context = BuildContext(
                method,
                binding,
                code,
                analysis,
                flow);
            contexts[key] = context;
            return context;
        }
        catch
        {
            contexts[key] = null;
            return null;
        }
    }

    static MethodContext BuildContext(
        ASMethod method,
        Avm2MethodBinding? binding,
        List<ASInstruction> code,
        Avm2MethodAnalysis analysis,
        Avm2DataFlowAnalysis flow)
    {
        var producers = new Dictionary<string, Avm2DataFlowOperation>(StringComparer.Ordinal);
        foreach (Avm2DataFlowOperation operation in flow.Operations)
        {
            foreach (string value in operation.Definitions)
                producers.TryAdd(value, operation);
        }
        var context = new MethodContext
        {
            Method = method,
            Binding = binding,
            ExactReceiver = flow.ExactReceiver,
            Code = code,
            Analysis = analysis,
            Flow = flow,
            VerifierValid = Avm2VerifierValidator.Validate(
                method.Body,
                analysis).VerifierValid,
            Operations = flow.Operations.ToDictionary(operation => operation.Instruction),
            Values = flow.Values.ToDictionary(value => value.Id, StringComparer.Ordinal),
            Producers = producers,
            Phis = flow.Phis.ToDictionary(phi => phi.Value, StringComparer.Ordinal),
            Conditions = []
        };
        foreach ((int block, List<Avm2CallCondition> conditions) in BuildConditions(context))
            context.Conditions.Add(block, conditions);
        return context;
    }

    bool ExactReceiverMatches(
        Avm2MethodBinding? binding,
        Avm2ExactReceiver exact_receiver)
    {
        if (!types_by_instance.TryGetValue(
                exact_receiver.RuntimeType,
                out TypeBinding? runtime))
        {
            return false;
        }
        if (binding is null)
            return true;
        if (binding.Scope is not (
                Avm2MethodBindingScope.ClassInstance or
                Avm2MethodBindingScope.ClassStatic))
        {
            return false;
        }
        TypeBinding? owner = Owner(binding.Owner);
        if (owner is null)
            return false;
        if (binding.Scope == Avm2MethodBindingScope.ClassStatic)
        {
            return exact_receiver.Static &&
                ReferenceEquals(owner.Instance, runtime.Instance);
        }
        return !exact_receiver.Static &&
            (ReferenceEquals(owner.Instance, runtime.Instance) ||
                IsStrictSubtype(runtime, owner));
    }

    static Dictionary<int, List<Avm2CallCondition>> BuildConditions(MethodContext context)
    {
        var result = new Dictionary<int, List<Avm2CallCondition>>();
        Dictionary<int, List<Avm2ControlFlowEdgeInventory>> outgoing =
            context.Analysis.ControlFlow.Edges
                .Where(edge => edge.Kind != "Exception" && edge.ToBlock.HasValue)
                .GroupBy(edge => edge.FromBlock)
                .ToDictionary(group => group.Key, group => group.ToList());
        foreach ((int branch, List<Avm2ControlFlowEdgeInventory> edges) in outgoing)
        {
            if (edges.Count < 2)
                continue;
            foreach (Avm2ControlFlowEdgeInventory edge in edges)
            {
                if (!edge.ToBlock.HasValue)
                    continue;
                HashSet<int> own = Reachable(edge.ToBlock.Value, branch, outgoing);
                HashSet<int> other = edges
                    .Where(candidate => candidate != edge && candidate.ToBlock.HasValue)
                    .SelectMany(candidate => Reachable(candidate.ToBlock!.Value, branch, outgoing))
                    .ToHashSet();
                own.ExceptWith(other);
                foreach (int block in own)
                {
                    if (!result.TryGetValue(block, out List<Avm2CallCondition>? conditions))
                    {
                        conditions = [];
                        result.Add(block, conditions);
                    }
                    conditions.Add(new Avm2CallCondition
                    {
                        Instruction = edge.SourceInstruction,
                        Offset = edge.SourceOffset,
                        Edge = edge.Kind,
                        Expression = BranchExpression(context, edge)
                    });
                }
            }
        }
        return result;
    }

    static HashSet<int> Reachable(
        int start,
        int blocked,
        Dictionary<int, List<Avm2ControlFlowEdgeInventory>> outgoing)
    {
        var visited = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(start);
        while (pending.Count > 0)
        {
            int current = pending.Pop();
            if (current == blocked || !visited.Add(current))
                continue;
            foreach (Avm2ControlFlowEdgeInventory edge in outgoing.GetValueOrDefault(current) ?? [])
            {
                if (edge.ToBlock.HasValue)
                    pending.Push(edge.ToBlock.Value);
            }
        }
        return visited;
    }

    static string BranchExpression(
        MethodContext context,
        Avm2ControlFlowEdgeInventory edge)
    {
        if (!context.Operations.TryGetValue(edge.SourceInstruction, out Avm2DataFlowOperation? operation) ||
            edge.SourceInstruction < 0 || edge.SourceInstruction >= context.Code.Count)
        {
            return edge.Kind;
        }
        List<string> values = operation.Inputs
            .Select(value => Expression(context, value, new HashSet<string>(StringComparer.Ordinal), 0))
            .ToList();
        string condition = context.Code[edge.SourceInstruction].OP switch
        {
            OPCode.IfTrue when values.Count >= 1 => values[^1],
            OPCode.IfFalse when values.Count >= 1 => Negate(values[^1]),
            OPCode.IfEq when values.Count >= 2 => $"{values[^2]} == {values[^1]}",
            OPCode.IfNe when values.Count >= 2 => $"{values[^2]} != {values[^1]}",
            OPCode.IfStrictEq when values.Count >= 2 => $"{values[^2]} === {values[^1]}",
            OPCode.IfStrictNE when values.Count >= 2 => $"{values[^2]} !== {values[^1]}",
            OPCode.IfLt when values.Count >= 2 => $"{values[^2]} < {values[^1]}",
            OPCode.IfLe when values.Count >= 2 => $"{values[^2]} <= {values[^1]}",
            OPCode.IfGt when values.Count >= 2 => $"{values[^2]} > {values[^1]}",
            OPCode.IfGe when values.Count >= 2 => $"{values[^2]} >= {values[^1]}",
            OPCode.LookUpSwitch when values.Count >= 1 => values[^1],
            _ => operation.Opcode
        };
        return edge.Kind switch
        {
            "Taken" => condition,
            "Fallthrough" => Negate(condition),
            "Case" => $"{condition} == {edge.CaseIndex}",
            "Default" => $"default({condition})",
            _ => $"{edge.Kind}({condition})"
        };
    }

    static string Expression(
        MethodContext context,
        string value,
        HashSet<string> visited,
        int depth)
    {
        if (depth > 16 || !visited.Add(value))
            return value;
        if (value.StartsWith("v_entry_local_", StringComparison.Ordinal) &&
            int.TryParse(value.AsSpan("v_entry_local_".Length), out int local))
        {
            return local == 0 ? "this" : $"arg_{local}";
        }
        if (context.Values.TryGetValue(value, out Avm2DataFlowValue? model) &&
            model.Literal is not null)
        {
            return model.Literal;
        }
        if (context.Phis.TryGetValue(value, out Avm2DataFlowPhi? phi))
        {
            string[] values = phi.Inputs
                .Select(input => Expression(
                    context,
                    input.Value,
                    new HashSet<string>(visited, StringComparer.Ordinal),
                    depth + 1))
                .Distinct(StringComparer.Ordinal)
                .Take(8)
                .ToArray();
            return values.Length == 1 ? values[0] : $"phi({string.Join(", ", values)})";
        }
        if (!context.Producers.TryGetValue(value, out Avm2DataFlowOperation? producer) ||
            producer.Instruction < 0 || producer.Instruction >= context.Code.Count)
        {
            return value;
        }
        ASInstruction instruction = context.Code[producer.Instruction];
        List<string> inputs = producer.Inputs.Select(input => Expression(
            context,
            input,
            new HashSet<string>(visited, StringComparer.Ordinal),
            depth + 1)).ToList();
        return instruction switch
        {
            GetLexIns lexical => Qualified(lexical.TypeName),
            GetPropertyIns property => PropertyAccess(
                inputs.FirstOrDefault() ?? "?",
                property.PropertyName),
            CallPropertyIns or CallPropLexIns or CallPropVoidIns =>
                CallExpression(instruction, inputs),
            CoerceIns coerce => $"{Qualified(coerce.TypeName)}({inputs.LastOrDefault() ?? "?"})",
            AsTypeIns as_type => $"{Qualified(as_type.TypeName)}({inputs.LastOrDefault() ?? "?"})",
            ConstructPropIns construct =>
                $"new {Qualified(construct.PropertyName)}({string.Join(", ", inputs.TakeLast(construct.ArgCount))})",
            _ => inputs.Count == 0
                ? value
                : $"{instruction.OP}({string.Join(", ", inputs)})"
        };
    }

    static string CallExpression(ASInstruction instruction, List<string> inputs)
    {
        int count = ArgumentCount(instruction);
        ASMultiname? property = PropertyMultiname(instruction);
        int runtime_operands = property is null
            ? 0
            : (property.IsNamespaceNeeded ? 1 : 0) +
              (property.IsNameNeeded ? 1 : 0);
        string receiver = inputs.Count > count + runtime_operands
            ? inputs[inputs.Count - count - runtime_operands - 1]
            : "?";
        return $"{PropertyAccess(receiver, property)}({string.Join(", ", inputs.TakeLast(count))})";
    }

    static string PropertyAccess(
        string receiver,
        ASMultiname? property)
    {
        if (property is null)
            return $"{receiver}.?";
        if (property.IsNameNeeded)
            return $"{receiver}[<runtime>]";
        if (property.IsAnyName)
            return $"{receiver}.*";
        return property.RuntimeName.Length == 0
            ? $"{receiver}[\"\"]"
            : $"{receiver}.{property.RuntimeName}";
    }

    static Avm2CallCondition Substitute(
        Avm2CallCondition condition,
        ASMethod callee,
        CallSite call,
        MethodContext caller)
    {
        string expression = condition.Expression;
        for (int index = callee.Parameters.Count; index >= 1; index--)
        {
            string replacement = index <= call.Arguments.Count
                ? Expression(
                    caller,
                    call.Arguments[index - 1],
                    new HashSet<string>(StringComparer.Ordinal),
                    0)
                : $"missing_arg_{index}";
            expression = ReplaceToken(expression, $"arg_{index}", replacement);
        }
        return new Avm2CallCondition
        {
            Instruction = condition.Instruction,
            Offset = condition.Offset,
            Edge = condition.Edge,
            Expression = expression
        };
    }

    static string ReplaceToken(string source, string token, string replacement)
    {
        int position = 0;
        while ((position = source.IndexOf(token, position, StringComparison.Ordinal)) >= 0)
        {
            int end = position + token.Length;
            bool before = position > 0 &&
                (char.IsLetterOrDigit(source[position - 1]) || source[position - 1] == '_');
            bool after = end < source.Length &&
                (char.IsLetterOrDigit(source[end]) || source[end] == '_');
            if (before || after)
            {
                position = end;
                continue;
            }
            source = source[..position] + $"({replacement})" + source[end..];
            position += replacement.Length + 2;
        }
        return source;
    }

    static List<Avm2CallCondition> PhiInputConditions(
        MethodContext context,
        Avm2DataFlowPhi phi,
        Avm2DataFlowPhiInput input)
    {
        var conditions = new List<Avm2CallCondition>();
        if (input.FromBlock.HasValue)
        {
            conditions.AddRange(
                context.Conditions.GetValueOrDefault(input.FromBlock.Value) ?? []);
            List<Avm2ControlFlowEdgeInventory> outgoing =
                context.Analysis.ControlFlow.Edges
                    .Where(edge =>
                        edge.FromBlock == input.FromBlock.Value &&
                        edge.ToBlock == phi.Block &&
                        edge.Kind != "Exception")
                    .ToList();
            if (outgoing.Count == 1 &&
                context.Analysis.ControlFlow.Edges.Count(edge =>
                    edge.FromBlock == input.FromBlock.Value &&
                    edge.Kind != "Exception" &&
                    edge.ToBlock.HasValue) > 1)
            {
                Avm2ControlFlowEdgeInventory edge = outgoing[0];
                conditions.Add(new Avm2CallCondition
                {
                    Instruction = edge.SourceInstruction,
                    Offset = edge.SourceOffset,
                    Edge = edge.Kind,
                    Expression = BranchExpression(context, edge)
                });
            }
        }
        return DistinctConditions(conditions);
    }

    bool TryCall(
        ASMethod caller,
        Avm2DataFlowOperation operation,
        ASInstruction instruction,
        out CallSite call)
    {
        ASMultiname? property = PropertyMultiname(instruction);
        string? name = instruction switch
        {
            CallStaticIns value => MethodName(value.Method),
            CallMethodIns value => $"disp_id_{value.MethodIndex}",
            CallIns => "<function>",
            ConstructPropIns => "<constructor>",
            ConstructIns => "<constructor>",
            _ when instruction.OP == OPCode.ConstructSuper => "<constructor>",
            _ => property is null
                ? null
                : property.IsNameNeeded
                    ? "<runtime-property>"
                    : property.IsAnyName
                        ? "<any-property>"
                        : property.RuntimeName
        };
        int arguments = ArgumentCount(instruction);
        if (name is null || arguments < 0)
        {
            call = default;
            return false;
        }
        if (instruction is CallIns)
        {
            if (operation.Inputs.Count < arguments + 2)
            {
                call = default;
                return false;
            }
            int callable = operation.Inputs.Count - arguments - 2;
            int call_receiver = callable + 1;
            call = new CallSite(
                name,
                null,
                arguments,
                operation.Inputs[call_receiver],
                operation.Inputs.Skip(operation.Inputs.Count - arguments).ToArray(),
                operation.Inputs[callable]);
            return true;
        }
        int runtime_operands = property is null
            ? 0
            : (property.IsNamespaceNeeded ? 1 : 0) +
              (property.IsNameNeeded ? 1 : 0);
        if (operation.Inputs.Count < arguments + runtime_operands + 1)
        {
            call = default;
            return false;
        }
        int receiver = operation.Inputs.Count - arguments - runtime_operands - 1;
        call = new CallSite(
            name,
            property,
            arguments,
            operation.Inputs[receiver],
            operation.Inputs.Skip(operation.Inputs.Count - arguments).ToArray(),
            null);
        return true;
    }

    static ASInstruction? ProducerInstruction(MethodContext context, string value)
    {
        if (!context.Producers.TryGetValue(value, out Avm2DataFlowOperation? operation) ||
            operation.Instruction < 0 || operation.Instruction >= context.Code.Count)
        {
            return null;
        }
        return context.Code[operation.Instruction];
    }

    PointsToResult TypeHint(MethodContext context, string value)
    {
        if (!context.Values.TryGetValue(value, out Avm2DataFlowValue? model))
            return None();
        if (TryExactRuntimeType(
                model.ExactRuntimeTypeIdentity,
                out TypeBinding? exact,
                out ReceiverKind receiver))
        {
            return One(
                exact,
                receiver,
                "ExactScopeType",
                context,
                model.Instruction ?? -1,
                -1,
                true);
        }
        return Many(
            FindTypes(model.TypeHint, context.Method.ABC),
            ReceiverKind.Instance,
            "DeclaredType",
            context,
            -1,
            -1,
            false);
    }

    bool TryExactRuntimeType(
        string? identity,
        out TypeBinding binding,
        out ReceiverKind receiver)
    {
        binding = null!;
        receiver = ReceiverKind.Instance;
        if (identity is null)
            return false;
        string[] parts = identity.Split(':');
        if (parts.Length != 5 ||
            parts[0] != "abc" ||
            parts[2] != "class" ||
            !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int abc_index) ||
            !int.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int class_index) ||
            parts[4] is not ("instance" or "static"))
        {
            return false;
        }
        TypeBinding? match = all_types.SingleOrDefault(candidate =>
            candidate.AbcIndex == abc_index &&
            candidate.ClassIndex == class_index);
        if (match is null)
            return false;
        binding = match;
        receiver = parts[4] == "static"
            ? ReceiverKind.Static
            : ReceiverKind.Instance;
        return true;
    }

    static bool ScopeIsKnownBuiltin(
        MethodContext context,
        string value)
    {
        if (!context.Values.TryGetValue(
                value,
                out Avm2DataFlowValue? scope))
        {
            return false;
        }
        string? identity =
            scope.ExactRuntimeTypeIdentity ??
            scope.VerifierType.Identity;
        return scope.VerifierType.Kind ==
                Avm2VerifierTypeKind.Known &&
            identity?.StartsWith(
                "builtin-class:",
                StringComparison.Ordinal) == true;
    }

    static Avm2ExactReceiver? ExactReceiver(PointsTo receiver) =>
        receiver.Exhaustive
            ? new Avm2ExactReceiver(
                receiver.Binding.Instance,
                receiver.Receiver == ReceiverKind.Static)
            : null;

    Avm2ExactReceiver? ExactInvocationReceiver(
        MethodContext context,
        CallSite call,
        int depth)
    {
        PointsToResult receivers = ResolveValue(
            context,
            call.Receiver,
            new HashSet<string>(StringComparer.Ordinal),
            depth);
        if (!receivers.Exhaustive ||
            receivers.Outcomes.Count != 0 ||
            receivers.Types.Count != 1 ||
            !receivers.Types[0].Exhaustive)
        {
            return null;
        }
        return ExactReceiver(receivers.Types[0]);
    }

    Avm2ExactReceiver? CompatibleInvocationReceiver(
        MethodContext context,
        CallSite call,
        TypeBinding contract,
        int depth)
    {
        Avm2ExactReceiver? exact_receiver = ExactInvocationReceiver(
            context,
            call,
            depth);
        if (exact_receiver is null || exact_receiver.Static)
            return null;
        return types_by_instance.TryGetValue(
                exact_receiver.RuntimeType,
                out TypeBinding? runtime) &&
            (ReferenceEquals(runtime.Instance, contract.Instance) ||
                IsStrictSubtype(runtime, contract))
                    ? exact_receiver
                    : null;
    }

    Avm2ResolvedCallTarget WithExactReceiver(
        Avm2ResolvedCallTarget target,
        Avm2ExactReceiver? exact_receiver)
    {
        Avm2ExactReceiver? compatible =
            exact_receiver is not null &&
            ExactReceiverMatches(
                target.Binding,
                exact_receiver)
                ? exact_receiver
                : null;
        return new Avm2ResolvedCallTarget
        {
            Method = target.Method,
            Binding = target.Binding,
            ExactReceiver = compatible,
            ClosureScope = target.ClosureScope,
            RequiresClosureScope =
                target.RequiresClosureScope,
            RuntimeType = target.RuntimeType,
            DefinitionAbc = target.DefinitionAbc,
            SelectionKind = target.SelectionKind,
            SelectorExpression = target.SelectorExpression,
            Conditions = target.Conditions,
            Evidence = target.Evidence
        };
    }

    static PointsTo WithSelection(PointsTo value, string selection) =>
        new()
        {
            Binding = value.Binding,
            Receiver = value.Receiver,
            SelectionKind = selection,
            SelectorExpression = value.SelectorExpression,
            Conditions = value.Conditions,
            Evidence =
            [
                .. value.Evidence,
                new Avm2CallTargetEvidence
                {
                    Kind = "TypeUnion",
                    Certainty = "Exact",
                    SourceClass = value.Evidence.LastOrDefault()?.SourceClass ?? "",
                    SourceMethod = value.Evidence.LastOrDefault()?.SourceMethod ?? "",
                    SourceAbc = value.Evidence.LastOrDefault()?.SourceAbc ?? -1,
                    TargetAbc = value.Binding.AbcIndex,
                    Instruction = value.Evidence.LastOrDefault()?.Instruction ?? -1,
                    Offset = value.Evidence.LastOrDefault()?.Offset ?? -1,
                    Symbol = value.Binding.Qualified
                }
            ],
            Exhaustive = value.Exhaustive
        };

    static PointsTo WithConditions(
        PointsTo value,
        IEnumerable<Avm2CallCondition> conditions)
    {
        List<Avm2CallCondition> combined =
            DistinctConditions([.. value.Conditions, .. conditions]);
        return new PointsTo
        {
            Binding = value.Binding,
            Receiver = value.Receiver,
            SelectionKind = value.SelectionKind,
            SelectorExpression = value.SelectorExpression,
            Conditions = combined,
            Evidence = value.Evidence,
            Exhaustive = value.Exhaustive
        };
    }

    static Avm2CallTerminalOutcome WithConditions(
        Avm2CallTerminalOutcome value,
        IEnumerable<Avm2CallCondition> conditions) =>
        new()
        {
            Kind = value.Kind,
            Expression = value.Expression,
            Conditions = DistinctConditions([.. value.Conditions, .. conditions]),
            Evidence = value.Evidence
        };

    PointsToResult One(
        TypeBinding binding,
        ReceiverKind receiver,
        string selection,
        MethodContext context,
        int instruction,
        int offset,
        bool exhaustive,
        string? evidence_symbol = null) =>
        new()
        {
            Types =
            [
                new PointsTo
                {
                    Binding = binding,
                    Receiver = receiver,
                    SelectionKind = selection,
                    SelectorExpression = instruction >= 0 &&
                        context.Operations.TryGetValue(instruction, out Avm2DataFlowOperation? operation) &&
                        context.Conditions.TryGetValue(operation.Block, out List<Avm2CallCondition>? conditions) &&
                        conditions.Count > 0
                            ? string.Join(" && ", conditions.Select(condition => condition.Expression))
                            : null,
                    Conditions = instruction >= 0 &&
                        context.Operations.TryGetValue(instruction, out operation) &&
                        context.Conditions.TryGetValue(operation.Block, out conditions)
                            ? conditions
                            : [],
                    Evidence =
                    [
                        Evidence(
                            selection,
                            exhaustive ? "Exact" : "Declared",
                            context,
                            instruction,
                            offset,
                            evidence_symbol ?? binding.Qualified,
                            binding.AbcIndex)
                    ],
                    Exhaustive = exhaustive
                }
            ],
            Outcomes = [],
            ControlFlowExhaustive = exhaustive,
            TargetExhaustive = exhaustive
        };

    PointsToResult Many(
        IEnumerable<TypeBinding> bindings,
        ReceiverKind receiver,
        string selection,
        MethodContext context,
        int instruction,
        int offset,
        bool exhaustive)
    {
        List<TypeBinding> values = bindings
            .DistinctBy(binding => (binding.Abc, binding.Instance))
            .ToList();
        if (values.Count == 0)
            return None();
        return Result(
            values.Select(binding => One(
                binding,
                receiver,
                selection,
                context,
                instruction,
                offset,
                exhaustive).Types[0]),
            exhaustive);
    }

    PointsToResult Result(IEnumerable<PointsTo> values, bool exhaustive)
        => Result(values, [], exhaustive, exhaustive);

    PointsToResult Result(
        IEnumerable<PointsTo> values,
        IEnumerable<Avm2CallTerminalOutcome> outcomes,
        bool control_flow_exhaustive,
        bool target_exhaustive)
    {
        List<PointsTo> distinct = values
            .GroupBy(value => new PointsToIdentity(
                value.Binding.Abc,
                value.Binding.Instance,
                value.Receiver,
                value.SelectionKind,
                value.SelectorExpression,
                ConditionKey(value.Conditions)))
            .Select(group =>
            {
                PointsTo first = group.First();
                return new PointsTo
                {
                    Binding = first.Binding,
                    Receiver = first.Receiver,
                    SelectionKind = first.SelectionKind,
                    SelectorExpression = first.SelectorExpression,
                    Conditions = DistinctConditions(group.SelectMany(value => value.Conditions)),
                    Evidence = DistinctEvidence(group.SelectMany(value => value.Evidence)),
                    Exhaustive = group.All(value => value.Exhaustive)
                };
            })
            .ToList();
        List<Avm2CallTerminalOutcome> distinct_outcomes = outcomes
            .GroupBy(outcome => new OutcomeIdentity(
                outcome.Kind,
                outcome.Expression,
                ConditionKey(outcome.Conditions)))
            .Select(group => new Avm2CallTerminalOutcome
            {
                Kind = group.First().Kind,
                Expression = group.First().Expression,
                Conditions = DistinctConditions(group.SelectMany(value => value.Conditions)),
                Evidence = DistinctEvidence(group.SelectMany(value => value.Evidence))
            })
            .ToList();
        bool has_result = distinct.Count > 0 || distinct_outcomes.Count > 0;
        return new PointsToResult
        {
            Types = distinct,
            Outcomes = distinct_outcomes,
            ControlFlowExhaustive = control_flow_exhaustive && has_result,
            TargetExhaustive = target_exhaustive &&
                has_result &&
                distinct_outcomes.All(outcome => outcome.Kind != "Unknown")
        };
    }

    bool IsStrictSubtype(TypeBinding candidate, TypeBinding target)
    {
        var key = (Candidate: candidate, Target: target);
        if (strict_subtypes.TryGetValue(key, out bool cached))
            return cached;
        var pending = new Stack<TypeBinding>();
        var visited = new HashSet<ASInstance>(ReferenceEqualityComparer.Instance);
        TypeBinding? parent = Parent(candidate.Instance);
        if (parent is not null)
            pending.Push(parent);
        foreach (ASMultiname interface_name in candidate.Instance.GetInterfaces())
        {
            foreach (TypeBinding contract in FindTypes(interface_name, candidate.Abc))
                pending.Push(contract);
        }
        while (pending.Count > 0)
        {
            TypeBinding current = pending.Pop();
            if (!visited.Add(current.Instance))
                continue;
            if (ReferenceEquals(current.Instance, target.Instance) ||
                !current.PrivateNamespace &&
                !target.PrivateNamespace &&
                current.RuntimeIdentity == target.RuntimeIdentity)
            {
                strict_subtypes.Add(key, true);
                return true;
            }
            parent = Parent(current.Instance);
            if (parent is not null)
                pending.Push(parent);
            foreach (ASMultiname interface_name in current.Instance.GetInterfaces())
            {
                foreach (TypeBinding contract in FindTypes(interface_name, current.Abc))
                    pending.Push(contract);
            }
        }
        strict_subtypes.Add(key, false);
        return false;
    }

    static PointsToResult None() => new()
    {
        Types = [],
        Outcomes = [],
        ControlFlowExhaustive = false,
        TargetExhaustive = false
    };

    static CallableResult EmptyCallable() => new()
    {
        Targets = [],
        ControlFlowExhaustive = false,
        TargetExhaustive = false
    };

    PointsToResult Outcome(
        string kind,
        string expression,
        MethodContext context,
        Avm2DataFlowOperation operation,
        bool exhaustive) =>
        Result(
            [],
            [
                Terminal(
                    kind,
                    expression,
                    context.Conditions.GetValueOrDefault(operation.Block) ?? [],
                    context,
                    operation,
                    exhaustive ? "Exact" : "Unknown")
            ],
            exhaustive,
            exhaustive);

    Avm2CallTerminalOutcome Terminal(
        string kind,
        string expression,
        IEnumerable<Avm2CallCondition> conditions,
        MethodContext context,
        Avm2DataFlowOperation operation,
        string certainty) =>
        new()
        {
            Kind = kind,
            Expression = expression,
            Conditions = DistinctConditions(conditions),
            Evidence =
            [
                Evidence(
                    "TerminalReturn",
                    certainty,
                    context,
                    operation.Instruction,
                    operation.Offset,
                    expression)
            ]
        };

    static Avm2ResolvedCall WithCallConditions(
        Avm2ResolvedCall result,
        MethodContext context,
        Avm2DataFlowOperation operation)
    {
        if (!context.Conditions.TryGetValue(
            operation.Block,
            out List<Avm2CallCondition>? conditions))
        {
            return result;
        }
        result.CallConditions.AddRange(DistinctConditions(conditions));
        return result;
    }

    Avm2CallTargetEvidence Evidence(
        string kind,
        string certainty,
        MethodContext context,
        int instruction,
        int offset,
        string? symbol,
        int target_abc = -1) =>
        new()
        {
            Kind = kind,
            Certainty = certainty,
            SourceClass = MethodOwner(context),
            SourceMethod = MethodName(context),
            SourceAbc = AbcIndex(context.Method.ABC),
            TargetAbc = target_abc,
            Instruction = instruction,
            Offset = offset,
            Symbol = symbol
        };

    Avm2ResolvedCall Exact(
        string name,
        ASMethod method,
        string kind,
        Avm2DataFlowOperation operation,
        MethodContext source,
        string? runtime_type = null,
        Avm2MethodBinding? binding = null,
        Avm2ExactReceiver? exact_receiver = null) =>
        new()
        {
            Name = name,
            Kind = kind,
            Exhaustive = true,
            ControlFlowExhaustive = true,
            TargetExhaustive = true,
            Targets =
            [
                new Avm2ResolvedCallTarget
                {
                    Method = method,
                    Binding = binding,
                    ExactReceiver = exact_receiver,
                    RuntimeType = runtime_type ?? MethodOwner(method),
                    DefinitionAbc = AbcIndex(method.ABC),
                    SelectionKind = kind,
                    Conditions = [],
                    Evidence =
                    [
                        new Avm2CallTargetEvidence
                        {
                            Kind = kind,
                            Certainty = "Exact",
                            SourceClass = MethodOwner(source),
                            SourceMethod = MethodName(source),
                            SourceAbc = AbcIndex(source.Method.ABC),
                            TargetAbc = AbcIndex(method.ABC),
                            Instruction = operation.Instruction,
                            Offset = operation.Offset,
                            Symbol = runtime_type ?? MethodOwner(method)
                        }
                    ]
                }
            ],
            Diagnostics = []
        };

    Avm2ResolvedCall ConstructorTargets(
        string name,
        PointsToResult constructors,
        Avm2DataFlowOperation operation,
        MethodContext source)
    {
        List<PointsTo> values = constructors.Types;
        List<Avm2CallTerminalOutcome> outcomes = DereferenceOutcomes(
            source,
            operation,
            constructors.Outcomes);
        bool has_result = values.Count > 0 || outcomes.Count > 0;
        return new Avm2ResolvedCall
        {
            Name = name,
            Kind = values.Count == 1 ? "ConstructedType" : "TypeUnion",
            Exhaustive = constructors.Exhaustive && has_result,
            ControlFlowExhaustive =
                constructors.ControlFlowExhaustive && has_result,
            TargetExhaustive =
                constructors.TargetExhaustive && has_result,
            Targets = values.Select(value => new Avm2ResolvedCallTarget
            {
                Method = value.Binding.Instance.Constructor,
                Binding = ConstructorBinding(value.Binding),
                ExactReceiver = ConstructedReceiver(value),
                RuntimeType = value.Binding.Qualified,
                DefinitionAbc = value.Binding.AbcIndex,
                SelectionKind = "ConstructedType",
                SelectorExpression = value.SelectorExpression,
                Conditions = value.Conditions,
                Evidence =
                [
                    .. value.Evidence,
                    new Avm2CallTargetEvidence
                    {
                        Kind = "ConstructedType",
                        Certainty =
                            constructors.Exhaustive && value.Exhaustive
                                ? "Exact"
                                : "Partial",
                        SourceClass = MethodOwner(source),
                        SourceMethod = MethodName(source),
                        SourceAbc = AbcIndex(source.Method.ABC),
                        TargetAbc = value.Binding.AbcIndex,
                        Instruction = operation.Instruction,
                        Offset = operation.Offset,
                        Symbol = value.Binding.Qualified
                    }
                ]
            }).ToList(),
            TerminalOutcomes = outcomes,
            Diagnostics = !has_result ? ["constructor-target-unresolved"] : []
        };
    }

    static Avm2ExactReceiver? ConstructedReceiver(PointsTo constructor) =>
        constructor.Exhaustive
            ? new Avm2ExactReceiver(
                constructor.Binding.Instance,
                false)
            : null;

    Avm2ResolvedCall Targets(
        string name,
        List<ASMethod> methods,
        string kind,
        Avm2DataFlowOperation operation,
        ASMethod source,
        bool exhaustive) =>
        new()
        {
            Name = name,
            Kind = methods.Count == 1 ? kind : "TypeUnion",
            Exhaustive = exhaustive,
            ControlFlowExhaustive = exhaustive,
            TargetExhaustive = exhaustive,
            Targets = methods.Select(method => new Avm2ResolvedCallTarget
            {
                Method = method,
                ExactReceiver = null,
                RuntimeType = MethodOwner(method),
                DefinitionAbc = AbcIndex(method.ABC),
                SelectionKind = kind,
                Conditions = [],
                Evidence =
                [
                    new Avm2CallTargetEvidence
                    {
                        Kind = kind,
                        Certainty = exhaustive ? "Exact" : "ClientTypeSet",
                        SourceClass = MethodOwner(source),
                        SourceMethod = MethodName(source),
                        SourceAbc = AbcIndex(source.ABC),
                        TargetAbc = AbcIndex(method.ABC),
                        Instruction = operation.Instruction,
                        Offset = operation.Offset,
                        Symbol = MethodOwner(method)
                    }
                ]
            }).ToList(),
            Diagnostics = []
        };

    static Avm2ResolvedCall Empty(
        string name,
        string kind,
        params string[] diagnostics) =>
        new()
        {
            Name = name,
            Kind = kind,
            Exhaustive = false,
            ControlFlowExhaustive = false,
            TargetExhaustive = false,
            Targets = [],
            Diagnostics = diagnostics.ToList()
        };

    static void AddScopeHash(
        ref HashCode hash,
        Avm2DataFlowScopeContext scope)
    {
        hash.Add(scope.CapturedScopeSize);
        hash.Add(scope.FullScopeSize);
        hash.Add(scope.HasExtraVerifierType);
        hash.Add(scope.ExtraVerifierType.Kind);
        hash.Add(
            scope.ExtraVerifierType.Identity,
            StringComparer.Ordinal);
        foreach (Avm2DataFlowScopeValue value in
            scope.DeclaringScope)
        {
            hash.Add(
                value.Provenance,
                StringComparer.Ordinal);
            hash.Add(
                value.TypeHint,
                StringComparer.Ordinal);
            hash.Add(value.VerifierType.Kind);
            hash.Add(
                value.VerifierType.Identity,
                StringComparer.Ordinal);
            hash.Add(
                value.ExactRuntimeTypeIdentity,
                StringComparer.Ordinal);
            hash.Add(
                value.Literal,
                StringComparer.Ordinal);
            hash.Add(value.IsWith);
        }
    }

    static string ClosureScopeIdentity(
        Avm2ResolvedCallTarget target)
    {
        if (!target.RequiresClosureScope)
            return "canonical";
        if (target.ClosureScope is not
            Avm2DataFlowScopeContext scope)
        {
            return "missing";
        }
        var result = new System.Text.StringBuilder();
        void Add(string? value)
        {
            result.Append(value?.Length ?? -1)
                .Append(':')
                .Append(value)
                .Append(';');
        }
        Add(scope.CapturedScopeSize.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Add(scope.FullScopeSize.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Add(scope.HasExtraVerifierType ? "1" : "0");
        Add(scope.ExtraVerifierType.Kind.ToString());
        Add(scope.ExtraVerifierType.Identity);
        foreach (Avm2DataFlowScopeValue value in
            scope.DeclaringScope)
        {
            Add(value.Provenance);
            Add(value.TypeHint);
            Add(value.VerifierType.Kind.ToString());
            Add(value.VerifierType.Identity);
            Add(value.ExactRuntimeTypeIdentity);
            Add(value.Literal);
            Add(value.IsWith ? "1" : "0");
        }
        return result.ToString();
    }

    static List<Avm2ResolvedCallTarget> Deduplicate(
        IEnumerable<Avm2ResolvedCallTarget> targets) =>
        targets
            .GroupBy(target => new TargetIdentity(
                target.Method,
                target.Binding?.Identity,
                target.ExactReceiver,
                ClosureScopeIdentity(target),
                target.DefinitionAbc,
                target.RuntimeType,
                target.SelectionKind,
                target.SelectorExpression,
                ConditionKey(target.Conditions)))
            .Select(group =>
            {
                Avm2ResolvedCallTarget first = group.First();
                return new Avm2ResolvedCallTarget
                {
                    Method = first.Method,
                    Binding = first.Binding,
                    ExactReceiver = first.ExactReceiver,
                    ClosureScope = first.ClosureScope,
                    RequiresClosureScope =
                        first.RequiresClosureScope,
                    RuntimeType = first.RuntimeType,
                    DefinitionAbc = first.DefinitionAbc,
                    SelectionKind = first.SelectionKind,
                    SelectorExpression = first.SelectorExpression,
                    Conditions = DistinctConditions(group.SelectMany(value => value.Conditions)),
                    Evidence = DistinctEvidence(group.SelectMany(value => value.Evidence))
                };
            })
            .ToList();

    IEnumerable<Avm2MethodBinding> MethodBindings(ASContainer container) =>
        method_bindings.GetBindings(container)
            .Where(binding =>
                binding.Resolved &&
                binding.Trait is not null &&
                binding.Role is
                    Avm2MethodBindingRole.MethodTrait or
                    Avm2MethodBindingRole.GetterTrait or
                    Avm2MethodBindingRole.SetterTrait or
                    Avm2MethodBindingRole.FunctionTrait);

    IEnumerable<ASTrait> MethodTraits(ASContainer container) =>
        MethodBindings(container).Select(binding => binding.Trait!);

    static ASMultiname? PropertyMultiname(ASInstruction instruction) =>
        instruction switch
        {
            SetPropertyIns property => property.PropertyName,
            InitPropertyIns property => property.PropertyName,
            FindPropertyIns property => property.PropertyName,
            FindPropStrictIns property => property.PropertyName,
            IPropertyContainer property => property.PropertyName,
            CallPropLexIns property => property.PropertyName,
            CallSuperIns property => property.MethodName,
            CallSuperVoidIns property => property.MethodName,
            _ => null
        };

    static string DisplayName(ASMultiname? name) =>
        name is null
            ? ""
            : name.IsNameNeeded
                ? "<runtime>"
                : name.IsAnyName
                    ? "*"
                    : name.RuntimeName;

    static bool NameMatches(ASMultiname? name, string expected) =>
        Avm2MethodAnalyzer.TryGetStaticName(name, out string actual) &&
        string.Equals(actual, expected, StringComparison.Ordinal);

    static bool NamesMatch(ASMultiname? left, ASMultiname? right) =>
        Avm2MethodAnalyzer.TryGetStaticName(left, out string left_name) &&
        Avm2MethodAnalyzer.TryGetStaticName(right, out string right_name) &&
        string.Equals(left_name, right_name, StringComparison.Ordinal);

    static int ArgumentCount(ASInstruction instruction) =>
        instruction switch
        {
            CallIns value => value.ArgCount,
            CallPropertyIns value => value.ArgCount,
            CallPropLexIns value => value.ArgCount,
            CallPropVoidIns value => value.ArgCount,
            CallSuperIns value => value.ArgCount,
            CallSuperVoidIns value => value.ArgCount,
            CallStaticIns value => value.ArgCount,
            CallMethodIns value => value.ArgCount,
            ConstructPropIns value => value.ArgCount,
            ConstructIns value => value.ArgCount,
            _ when instruction.OP == OPCode.ConstructSuper =>
                instruction.GetPopCount() - 1,
            _ => -1
        };

    static string Negate(string expression) =>
        expression.StartsWith("!(", StringComparison.Ordinal) && expression.EndsWith(')')
            ? expression[2..^1]
            : $"!({expression})";

    static string Qualified(ASMultiname? name) =>
        Avm2MethodAnalyzer.Qualified(name);

    int AbcIndex(ABCFile abc) =>
        abc_indices.GetValueOrDefault(abc, -1);

    static bool IsPrivate(ASMultiname? name)
    {
        if (name is null)
            return false;
        try
        {
            if (name.Kind is MultinameKind.QName or MultinameKind.QNameA)
                return name.Namespace?.Kind == NamespaceKind.Private;
            if (name.Kind is MultinameKind.Multiname or MultinameKind.MultinameA or
                MultinameKind.MultinameL or MultinameKind.MultinameLA)
            {
                ASNamespaceSet? namespace_set = name.NamespaceSet;
                return namespace_set is not null &&
                    namespace_set.NamespaceIndices.Any(index =>
                    index > 0 &&
                    index < name.Pool.Namespaces.Count &&
                    name.Pool.Namespaces[index]?.Kind == NamespaceKind.Private);
            }
        }
        catch
        {
        }
        return false;
    }

    static bool IsBuiltinObject(ASMultiname? name) =>
        IsBuiltinType(name, "Object");

    static bool IsBuiltinType(ASMultiname? name, params string[] types) =>
        name is not null &&
        name.Kind is MultinameKind.QName or MultinameKind.QNameA &&
        Avm2MethodAnalyzer.TryGetStaticName(name, out string local_name) &&
        types.Contains(local_name, StringComparer.Ordinal) &&
        name.Namespace?.IsPublicRoot == true;

    bool SameProperty(
        ASMultiname property,
        ASMultiname? candidate,
        ABCFile receiver_abc)
    {
        if (candidate is null || !NamesMatch(property, candidate))
            return false;
        bool property_private = IsPrivate(property);
        bool candidate_private = IsPrivate(candidate);
        if (property_private || candidate_private)
        {
            return property_private &&
                candidate_private &&
                ReferenceEquals(property.Pool.ABC, receiver_abc) &&
                ReferenceEquals(candidate.Pool.ABC, receiver_abc) &&
                RuntimeSymbolIdentity(property) ==
                    RuntimeSymbolIdentity(candidate);
        }
        if (RuntimeSymbolIdentity(property) ==
            RuntimeSymbolIdentity(candidate))
        {
            return true;
        }
        if (property.Kind is not (MultinameKind.Multiname or MultinameKind.MultinameA))
        {
            return false;
        }
        try
        {
            if (property.NamespaceSet is not ASNamespaceSet namespace_set ||
                candidate.Namespace is not ASNamespace candidate_namespace_value)
            {
                return false;
            }
            string candidate_namespace = RuntimeNamespaceIdentity(
                candidate_namespace_value);
            return namespace_set.NamespaceIndices.Any(index =>
                index > 0 &&
                index < property.Pool.Namespaces.Count &&
                RuntimeNamespaceIdentity(
                    property.Pool.Namespaces[index]) == candidate_namespace);
        }
        catch
        {
            return false;
        }
    }

    static bool NamespaceSetContains(
        ASMultiname property,
        ASMultiname candidate)
    {
        if (!NamesMatch(property, candidate) ||
            property.Kind is not (MultinameKind.Multiname or MultinameKind.MultinameA) ||
            candidate.Kind is not (MultinameKind.QName or MultinameKind.QNameA))
        {
            return false;
        }
        try
        {
            if (property.NamespaceSet is not ASNamespaceSet namespace_set ||
                candidate.Namespace is not ASNamespace candidate_namespace_value)
            {
                return false;
            }
            string candidate_namespace =
                Avm2MethodAnalyzer.RuntimeNamespaceIdentity(candidate_namespace_value);
            return namespace_set.NamespaceIndices.Any(index =>
                index > 0 &&
                index < property.Pool.Namespaces.Count &&
                Avm2MethodAnalyzer.RuntimeNamespaceIdentity(
                    property.Pool.Namespaces[index]) == candidate_namespace);
        }
        catch
        {
            return false;
        }
    }

    bool NamespaceSetContainsIndexed(
        ASMultiname property,
        ASMultiname candidate)
    {
        if (!NamesMatch(property, candidate) ||
            property.Kind is not (MultinameKind.Multiname or MultinameKind.MultinameA) ||
            candidate.Kind is not (MultinameKind.QName or MultinameKind.QNameA))
        {
            return false;
        }
        try
        {
            if (property.NamespaceSet is not ASNamespaceSet namespace_set ||
                candidate.Namespace is not ASNamespace candidate_namespace_value)
            {
                return false;
            }
            string candidate_namespace = RuntimeNamespaceIdentity(
                candidate_namespace_value);
            return namespace_set.NamespaceIndices.Any(index =>
                index > 0 &&
                index < property.Pool.Namespaces.Count &&
                RuntimeNamespaceIdentity(
                    property.Pool.Namespaces[index]) == candidate_namespace);
        }
        catch
        {
            return false;
        }
    }

    static List<Avm2CallCondition> DistinctConditions(
        IEnumerable<Avm2CallCondition> conditions) =>
        conditions
            .GroupBy(condition => new ConditionIdentity(
                condition.Instruction,
                condition.Offset,
                condition.Edge,
                condition.Expression))
            .Select(group => group.First())
            .OrderBy(condition => condition.Instruction)
            .ThenBy(condition => condition.Offset)
            .ThenBy(condition => condition.Edge, StringComparer.Ordinal)
            .ThenBy(condition => condition.Expression, StringComparer.Ordinal)
            .ToList();

    static string ConditionKey(IEnumerable<Avm2CallCondition> conditions) =>
        StructuredKey(DistinctConditions(conditions).Select(condition =>
            StructuredKey(
            [
                condition.Instruction.ToString(CultureInfo.InvariantCulture),
                condition.Offset.ToString(CultureInfo.InvariantCulture),
                condition.Edge,
                condition.Expression
            ])));

    static List<Avm2CallTargetEvidence> DistinctEvidence(
        IEnumerable<Avm2CallTargetEvidence> evidence) =>
        evidence
            .GroupBy(value => new EvidenceIdentity(
                value.Kind,
                value.Certainty,
                value.SourceAbc,
                value.SourceClass,
                value.SourceMethod,
                value.TargetAbc,
                value.Instruction,
                value.Offset,
                value.Symbol))
            .Select(group => group.First())
            .OrderBy(value => value.SourceAbc)
            .ThenBy(value => value.SourceClass, StringComparer.Ordinal)
            .ThenBy(value => value.SourceMethod, StringComparer.Ordinal)
            .ThenBy(value => value.Instruction)
            .ThenBy(value => value.Offset)
            .ThenBy(value => value.TargetAbc)
            .ThenBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.Symbol, StringComparer.Ordinal)
            .ToList();

    static string StructuredKey(IEnumerable<string?> values)
    {
        var result = new System.Text.StringBuilder();
        foreach (string? value in values)
        {
            if (value is null)
            {
                result.Append("-1:");
                continue;
            }
            result.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            result.Append(':');
            result.Append(value);
        }
        return result.ToString();
    }

    string MethodOwner(ASMethod method)
    {
        ASContainer[] owners = MethodContainers(method);
        if (owners.Length == 1)
            return ContainerName(owners[0]);
        return $"abc[{AbcIndex(method.ABC)}]::method[{method.ABC.Methods.IndexOf(method)}]";
    }

    string MethodOwner(MethodContext context) =>
        context.Binding is null
            ? MethodOwner(context.Method)
            : ContainerName(context.Binding.Owner);

    ASContainer? MethodContainer(ASMethod method)
    {
        ASContainer[] owners = MethodContainers(method);
        if (owners.Length == 1)
            return owners[0];
        return null;
    }

    ASContainer? MethodContainer(MethodContext context) =>
        context.Binding?.Owner ?? MethodContainer(context.Method);

    Avm2MethodBinding? ConstructorBinding(TypeBinding binding)
    {
        List<Avm2MethodBinding> matches = method_bindings
            .GetBindings(binding.Instance)
            .Where(value =>
                value.Role == Avm2MethodBindingRole.InstanceConstructor &&
                ReferenceEquals(value.Method, binding.Instance.Constructor))
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    Avm2MethodBinding? Binding(ASTrait trait)
    {
        List<Avm2MethodBinding> matches = method_bindings
            .GetBindings(trait)
            .Where(binding => binding.Resolved)
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    Avm2MethodBinding? UniqueBinding(ASMethod method)
    {
        List<Avm2MethodBinding> matches = ResolveMethodBindings(method)
            .Where(binding => binding.Resolved)
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    ASContainer[] MethodContainers(ASMethod method) =>
        ResolveMethodBindings(method)
            .Select(binding => binding.Owner)
            .Distinct<ASContainer>(ReferenceEqualityComparer.Instance)
            .Take(2)
            .ToArray();

    static string ContainerName(ASContainer? container)
    {
        ASMultiname? name = container switch
        {
            null => null,
            ASScript script =>
                script.Traits.FirstOrDefault()?.QName,
            _ => container.QName
        };
        return name is null ? "" : Qualified(name);
    }

    string MethodName(ASMethod method)
    {
        IReadOnlyList<Avm2MethodBinding> bindings =
            ResolveMethodBindings(method);
        if (bindings.Any(binding =>
            binding.Role is
                Avm2MethodBindingRole.InstanceConstructor or
                Avm2MethodBindingRole.StaticConstructor))
        {
            return "<constructor>";
        }
        string[] names = bindings
            .Where(binding => binding.Trait is not null)
            .Select(binding => binding.Trait!.QName)
            .DistinctBy(RuntimeSymbolIdentity)
            .Where(name => Avm2MethodAnalyzer.TryGetStaticName(name, out _))
            .Select(name => name.RuntimeName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray() ?? [];
        if (names.Length == 1)
            return names[0];
        if (names.Length == 0 && !string.IsNullOrEmpty(method.Name))
            return method.Name;
        return $"method_{method.ABC.Methods.IndexOf(method)}";
    }

    string MethodName(MethodContext context)
    {
        if (context.Binding?.Trait is ASTrait trait)
            return DisplayName(trait.QName);
        return context.Binding?.Role switch
        {
            Avm2MethodBindingRole.InstanceConstructor or
            Avm2MethodBindingRole.StaticConstructor => "<constructor>",
            Avm2MethodBindingRole.ScriptInitializer => "<script-initializer>",
            _ => MethodName(context.Method)
        };
    }
}
