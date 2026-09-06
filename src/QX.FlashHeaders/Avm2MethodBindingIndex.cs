using System.Collections.ObjectModel;
using System.Globalization;
using Flazzy.ABC;

namespace Qx.Headers.Flash;

public enum Avm2MethodBindingScope
{
    ClassInstance,
    ClassStatic,
    Script,
    Activation
}

public enum Avm2MethodBindingRole
{
    InstanceConstructor,
    StaticConstructor,
    ScriptInitializer,
    MethodTrait,
    GetterTrait,
    SetterTrait,
    FunctionTrait
}

public sealed class Avm2MethodBinding
{
    internal Avm2MethodBinding(
        int index,
        ABCFile abc,
        int abc_index,
        int method_index,
        ASContainer owner,
        int owner_index,
        int container_index,
        ASTrait? trait,
        int? trait_index,
        Avm2MethodBindingScope scope,
        Avm2MethodBindingRole role,
        string provenance)
    {
        Index = index;
        Abc = abc;
        AbcIndex = abc_index;
        MethodIndex = method_index;
        Method = method_index >= 0 && method_index < abc.Methods.Count
            ? abc.Methods[method_index]
            : null;
        Owner = owner;
        OwnerIndex = owner_index;
        ContainerIndex = container_index;
        Trait = trait;
        TraitIndex = trait_index;
        Scope = scope;
        Role = role;
        Provenance = provenance;
        string trait_value = trait_index?.ToString(CultureInfo.InvariantCulture) ?? "-";
        Identity = string.Create(
            CultureInfo.InvariantCulture,
            $"abc:{abc_index}:scope:{scope}:container:{container_index}:owner:{owner_index}:trait:{trait_value}:role:{role}:method:{method_index}");
    }

    public int Index { get; }
    public ABCFile Abc { get; }
    public int AbcIndex { get; }
    public ASMethod? Method { get; }
    public int MethodIndex { get; }
    public bool Resolved => Method is not null;
    public ASContainer Owner { get; }
    public int OwnerIndex { get; }
    public int ContainerIndex { get; }
    public ASTrait? Trait { get; }
    public int? TraitIndex { get; }
    public Avm2MethodBindingScope Scope { get; }
    public Avm2MethodBindingRole Role { get; }
    public string Provenance { get; }
    public string Identity { get; }
}

public sealed class Avm2MethodBindingIndex
{
    static readonly IReadOnlyList<Avm2MethodBinding> Empty =
        Array.Empty<Avm2MethodBinding>();

    readonly Dictionary<ABCFile, int> abc_indices;
    readonly Dictionary<ASMethod, (int AbcIndex, int MethodIndex)> method_indices;
    readonly Dictionary<(int AbcIndex, int MethodIndex), IReadOnlyList<Avm2MethodBinding>>
        by_method;
    readonly Dictionary<ASContainer, IReadOnlyList<Avm2MethodBinding>> by_owner;
    readonly Dictionary<ASTrait, IReadOnlyList<Avm2MethodBinding>> by_trait;

    Avm2MethodBindingIndex(
        IReadOnlyList<ABCFile> abcs,
        IReadOnlyDictionary<int, ABCFile> abcs_by_index,
        IReadOnlyList<Avm2MethodBinding> bindings,
        Dictionary<ABCFile, int> abc_indices,
        Dictionary<ASMethod, (int AbcIndex, int MethodIndex)> method_indices)
    {
        Abcs = abcs;
        AbcsByIndex = abcs_by_index;
        Bindings = bindings;
        this.abc_indices = abc_indices;
        this.method_indices = method_indices;
        by_method = Group(
            bindings,
            value => (value.AbcIndex, value.MethodIndex));
        by_owner = Group(bindings, value => value.Owner);
        by_trait = Group(
            bindings.Where(value => value.Trait is not null),
            value => value.Trait!);
    }

    public IReadOnlyList<ABCFile> Abcs { get; }
    public IReadOnlyDictionary<int, ABCFile> AbcsByIndex { get; }
    public IReadOnlyList<Avm2MethodBinding> Bindings { get; }

    public static Avm2MethodBindingIndex Create(IReadOnlyList<ABCFile> abcs)
    {
        ArgumentNullException.ThrowIfNull(abcs);
        var sources = new (int Index, ABCFile Abc)[abcs.Count];
        for (int index = 0; index < abcs.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(abcs[index]);
            sources[index] = (index, abcs[index]);
        }
        return Create(sources);
    }

    public static Avm2MethodBindingIndex Create(ABCFile abc, int abc_index = 0)
    {
        ArgumentNullException.ThrowIfNull(abc);
        ArgumentOutOfRangeException.ThrowIfNegative(abc_index);
        return Create([(abc_index, abc)]);
    }

    public IReadOnlyList<Avm2MethodBinding> GetBindings(
        int abc_index,
        int method_index)
    {
        return by_method.GetValueOrDefault((abc_index, method_index)) ?? Empty;
    }

    public IReadOnlyList<Avm2MethodBinding> GetBindings(
        ABCFile abc,
        int method_index)
    {
        ArgumentNullException.ThrowIfNull(abc);
        return abc_indices.TryGetValue(abc, out int abc_index)
            ? GetBindings(abc_index, method_index)
            : Empty;
    }

    public bool TryGetAbcIndex(ABCFile abc, out int abc_index)
    {
        ArgumentNullException.ThrowIfNull(abc);
        return abc_indices.TryGetValue(abc, out abc_index);
    }

    public IReadOnlyList<Avm2MethodBinding> GetBindings(ASMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return method_indices.TryGetValue(
            method,
            out (int AbcIndex, int MethodIndex) key)
            ? GetBindings(key.AbcIndex, key.MethodIndex)
            : Empty;
    }

    public IReadOnlyList<Avm2MethodBinding> GetBindings(ASContainer owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return by_owner.GetValueOrDefault(owner) ?? Empty;
    }

    public IReadOnlyList<Avm2MethodBinding> GetBindings(ASTrait trait)
    {
        ArgumentNullException.ThrowIfNull(trait);
        return by_trait.GetValueOrDefault(trait) ?? Empty;
    }

    public Avm2MethodBinding? GetScriptInitializer(
        int abc_index,
        int script_index)
    {
        if (!AbcsByIndex.TryGetValue(
                abc_index,
                out ABCFile? abc) ||
            script_index < 0 ||
            script_index >= abc.Scripts.Count)
        {
            return null;
        }
        ASScript script = abc.Scripts[script_index];
        Avm2MethodBinding[] candidates = GetBindings(script)
            .Where(binding =>
                binding.Resolved &&
                ReferenceEquals(binding.Abc, abc) &&
                binding.AbcIndex == abc_index &&
                ReferenceEquals(binding.Owner, script) &&
                binding.OwnerIndex == script_index &&
                binding.ContainerIndex == script_index &&
                binding.Trait is null &&
                binding.TraitIndex is null &&
                binding.Scope == Avm2MethodBindingScope.Script &&
                binding.Role ==
                    Avm2MethodBindingRole.ScriptInitializer &&
                binding.MethodIndex == script.InitializerIndex &&
                ReferenceEquals(
                    binding.Method,
                    script.Initializer))
            .Take(2)
            .ToArray();
        if (candidates.Length != 1)
            return null;
        Avm2MethodBinding[] method_owners = GetBindings(
                abc_index,
                script.InitializerIndex)
            .Where(binding => binding.Resolved)
            .Take(2)
            .ToArray();
        return method_owners.Length == 1 &&
            string.Equals(
                method_owners[0].Identity,
                candidates[0].Identity,
                StringComparison.Ordinal)
                ? candidates[0]
                : null;
    }

    static Avm2MethodBindingIndex Create(
        IReadOnlyList<(int Index, ABCFile Abc)> sources)
    {
        var bindings = new List<Avm2MethodBinding>();
        var abc_indices = new Dictionary<ABCFile, int>(
            ReferenceEqualityComparer.Instance);
        var method_indices =
            new Dictionary<ASMethod, (int AbcIndex, int MethodIndex)>(
                ReferenceEqualityComparer.Instance);
        var source_indices = new HashSet<int>();

        foreach ((int abc_index, ABCFile abc) in sources)
        {
            if (!source_indices.Add(abc_index))
                throw new ArgumentException($"Duplicate ABC index {abc_index}.");
            if (!abc_indices.TryAdd(abc, abc_index))
                throw new ArgumentException("The same ABC file cannot be indexed twice.");
            for (int method_index = 0;
                method_index < abc.Methods.Count;
                method_index++)
            {
                if (!method_indices.TryAdd(
                    abc.Methods[method_index],
                    (abc_index, method_index)))
                {
                    throw new ArgumentException(
                        "The same method object cannot occupy multiple method slots.");
                }
            }
            AddAbc(bindings, abc, abc_index);
        }

        IReadOnlyList<ABCFile> abcs = Array.AsReadOnly(
            sources.Select(value => value.Abc).ToArray());
        IReadOnlyDictionary<int, ABCFile> abcs_by_index =
            new ReadOnlyDictionary<int, ABCFile>(
                sources.ToDictionary(value => value.Index, value => value.Abc));
        IReadOnlyList<Avm2MethodBinding> immutable_bindings =
            Array.AsReadOnly(bindings.ToArray());
        return new Avm2MethodBindingIndex(
            abcs,
            abcs_by_index,
            immutable_bindings,
            abc_indices,
            method_indices);
    }

    static void AddAbc(
        List<Avm2MethodBinding> bindings,
        ABCFile abc,
        int abc_index)
    {
        int class_count = Math.Max(abc.Instances.Count, abc.Classes.Count);
        for (int class_index = 0; class_index < class_count; class_index++)
        {
            ASInstance? instance = class_index < abc.Instances.Count
                ? abc.Instances[class_index]
                : null;
            ASClass? @class = class_index < abc.Classes.Count
                ? abc.Classes[class_index]
                : null;

            if (instance is not null)
            {
                Add(
                    bindings,
                    abc,
                    abc_index,
                    instance.ConstructorIndex,
                    instance,
                    class_index,
                    class_index,
                    null,
                    null,
                    Avm2MethodBindingScope.ClassInstance,
                    Avm2MethodBindingRole.InstanceConstructor,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"class[{class_index}].instance.constructor"));
            }
            if (@class is not null)
            {
                Add(
                    bindings,
                    abc,
                    abc_index,
                    @class.ConstructorIndex,
                    @class,
                    class_index,
                    class_index,
                    null,
                    null,
                    Avm2MethodBindingScope.ClassStatic,
                    Avm2MethodBindingRole.StaticConstructor,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"class[{class_index}].static.constructor"));
            }
            if (instance is not null)
            {
                AddTraits(
                    bindings,
                    abc,
                    abc_index,
                    instance,
                    class_index,
                    class_index,
                    Avm2MethodBindingScope.ClassInstance,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"class[{class_index}].instance"));
            }
            if (@class is not null)
            {
                AddTraits(
                    bindings,
                    abc,
                    abc_index,
                    @class,
                    class_index,
                    class_index,
                    Avm2MethodBindingScope.ClassStatic,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"class[{class_index}].static"));
            }
        }

        for (int script_index = 0; script_index < abc.Scripts.Count; script_index++)
        {
            ASScript script = abc.Scripts[script_index];
            Add(
                bindings,
                abc,
                abc_index,
                script.InitializerIndex,
                script,
                script_index,
                script_index,
                null,
                null,
                Avm2MethodBindingScope.Script,
                Avm2MethodBindingRole.ScriptInitializer,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"script[{script_index}].initializer"));
            AddTraits(
                bindings,
                abc,
                abc_index,
                script,
                script_index,
                script_index,
                Avm2MethodBindingScope.Script,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"script[{script_index}]"));
        }

        for (int body_index = 0;
            body_index < abc.MethodBodies.Count;
            body_index++)
        {
            ASMethodBody body = abc.MethodBodies[body_index];
            AddTraits(
                bindings,
                abc,
                abc_index,
                body,
                body.MethodIndex,
                body_index,
                Avm2MethodBindingScope.Activation,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"method[{body.MethodIndex}].activation"));
        }
    }

    static void AddTraits(
        List<Avm2MethodBinding> bindings,
        ABCFile abc,
        int abc_index,
        ASContainer owner,
        int owner_index,
        int container_index,
        Avm2MethodBindingScope scope,
        string provenance)
    {
        for (int trait_index = 0;
            trait_index < owner.Traits.Count;
            trait_index++)
        {
            ASTrait trait = owner.Traits[trait_index];
            if (!TryMethod(trait, out int method_index, out Avm2MethodBindingRole role))
                continue;
            Add(
                bindings,
                abc,
                abc_index,
                method_index,
                owner,
                owner_index,
                container_index,
                trait,
                trait_index,
                scope,
                role,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{provenance}.trait[{trait_index}]"));
        }
    }

    static bool TryMethod(
        ASTrait trait,
        out int method_index,
        out Avm2MethodBindingRole role)
    {
        switch (trait.Kind)
        {
            case TraitKind.Method:
                method_index = trait.MethodIndex;
                role = Avm2MethodBindingRole.MethodTrait;
                return true;
            case TraitKind.Getter:
                method_index = trait.MethodIndex;
                role = Avm2MethodBindingRole.GetterTrait;
                return true;
            case TraitKind.Setter:
                method_index = trait.MethodIndex;
                role = Avm2MethodBindingRole.SetterTrait;
                return true;
            case TraitKind.Function:
                method_index = trait.FunctionIndex;
                role = Avm2MethodBindingRole.FunctionTrait;
                return true;
            default:
                method_index = -1;
                role = default;
                return false;
        }
    }

    static void Add(
        List<Avm2MethodBinding> bindings,
        ABCFile abc,
        int abc_index,
        int method_index,
        ASContainer owner,
        int owner_index,
        int container_index,
        ASTrait? trait,
        int? trait_index,
        Avm2MethodBindingScope scope,
        Avm2MethodBindingRole role,
        string provenance)
    {
        bindings.Add(new Avm2MethodBinding(
            bindings.Count,
            abc,
            abc_index,
            method_index,
            owner,
            owner_index,
            container_index,
            trait,
            trait_index,
            scope,
            role,
            provenance));
    }

    static Dictionary<TKey, IReadOnlyList<Avm2MethodBinding>> Group<TKey>(
        IEnumerable<Avm2MethodBinding> bindings,
        Func<Avm2MethodBinding, TKey> key)
        where TKey : notnull
    {
        return bindings
            .GroupBy(key)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Avm2MethodBinding>)Array.AsReadOnly(
                    group.ToArray()));
    }
}
