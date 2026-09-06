using Flazzy.ABC;

namespace Qx.Headers.Flash;

internal sealed class Avm2VerifierTypeRegistry
{
    sealed record DomainDefinition(
        int AbcIndex,
        int ScriptIndex,
        int TraitIndex,
        ASTrait Trait);

    static readonly Dictionary<string, string>
        CoreInstanceIdentities = new(
            StringComparer.Ordinal)
        {
            ["ArgumentError"] = "builtin:argumenterror",
            ["Array"] = "builtin:array",
            ["Boolean"] = "builtin:boolean",
            ["Class"] = "builtin:class",
            ["Date"] = "builtin:date",
            ["DefinitionError"] = "builtin:definitionerror",
            ["Error"] = "builtin:error",
            ["EvalError"] = "builtin:evalerror",
            ["Function"] = "builtin:function",
            ["JSON"] = "builtin:json",
            ["Math"] = "builtin:math",
            ["Namespace"] = "builtin:namespace",
            ["Number"] = "builtin:number",
            ["Object"] = "builtin:object",
            ["QName"] = "builtin:qname",
            ["RangeError"] = "builtin:rangeerror",
            ["ReferenceError"] = "builtin:referenceerror",
            ["RegExp"] = "builtin:regexp",
            ["SecurityError"] = "builtin:securityerror",
            ["String"] = "builtin:string",
            ["SyntaxError"] = "builtin:syntaxerror",
            ["TypeError"] = "builtin:typeerror",
            ["URIError"] = "builtin:urierror",
            ["UninitializedError"] = "builtin:uninitializederror",
            ["VerifyError"] = "builtin:verifyerror",
            ["XML"] = "builtin:xml",
            ["XMLList"] = "builtin:xmllist",
            ["float"] = "builtin:float",
            ["float4"] = "builtin:float4",
            ["int"] = "builtin:int",
            ["uint"] = "builtin:uint"
        };

    static readonly Dictionary<string, string>
        DirectSupertypes = new(
            StringComparer.Ordinal)
        {
            ["builtin:argumenterror"] = "builtin:error",
            ["builtin:array"] = "builtin:object",
            ["builtin:boolean"] = "builtin:object",
            ["builtin:class"] = "builtin:object",
            ["builtin:date"] = "builtin:object",
            ["builtin:definitionerror"] = "builtin:error",
            ["builtin:error"] = "builtin:object",
            ["builtin:evalerror"] = "builtin:error",
            ["builtin:float"] = "builtin:number",
            ["builtin:float4"] = "builtin:object",
            ["builtin:function"] = "builtin:object",
            ["builtin:int"] = "builtin:number",
            ["builtin:json"] = "builtin:object",
            ["builtin:math"] = "builtin:object",
            ["builtin:namespace"] = "builtin:object",
            ["builtin:number"] = "builtin:object",
            ["builtin:qname"] = "builtin:object",
            ["builtin:rangeerror"] = "builtin:error",
            ["builtin:referenceerror"] = "builtin:error",
            ["builtin:regexp"] = "builtin:object",
            ["builtin:securityerror"] = "builtin:error",
            ["builtin:string"] = "builtin:object",
            ["builtin:syntaxerror"] = "builtin:error",
            ["builtin:typeerror"] = "builtin:error",
            ["builtin:uint"] = "builtin:number",
            ["builtin:uninitializederror"] = "builtin:error",
            ["builtin:urierror"] = "builtin:error",
            ["builtin:vector"] = "builtin:object",
            ["builtin:verifyerror"] = "builtin:error",
            ["builtin:xml"] = "builtin:object",
            ["builtin:xmllist"] = "builtin:object"
        };

    readonly IReadOnlyDictionary<int, ABCFile>
        abcs_by_index;
    readonly Dictionary<ABCFile, int> abc_indices =
        new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<string, List<DomainDefinition>>
        definitions = new(StringComparer.Ordinal);
    readonly Dictionary<
        string,
        List<(int AbcIndex, int ClassIndex)>>
        defined_classes = new(StringComparer.Ordinal);
    readonly Dictionary<string, int>
        external_types = new(StringComparer.Ordinal);

    Avm2VerifierTypeRegistry(
        IReadOnlyDictionary<int, ABCFile> abcs,
        bool collect_external_types)
    {
        abcs_by_index = abcs;
        foreach ((int abc_index, ABCFile abc) in
            abcs.OrderBy(value => value.Key)
                .Select(value =>
                    (value.Key, value.Value)))
        {
            abc_indices.Add(abc, abc_index);
            for (int class_index = 0;
                class_index < abc.Instances.Count;
                class_index++)
            {
                ASInstance instance =
                    abc.Instances[class_index];
                string symbol =
                    RuntimeSymbol(instance.QName);
                if (symbol.Length != 0)
                {
                    if (!defined_classes.TryGetValue(
                            symbol,
                            out List<(
                                int AbcIndex,
                                int ClassIndex)>? sites))
                    {
                        sites = [];
                        defined_classes.Add(
                            symbol,
                            sites);
                    }
                    sites.Add((
                        abc_index,
                        class_index));
                }
            }
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
                    string symbol = RuntimeSymbol(trait.QName);
                    if (symbol.Length == 0)
                        continue;
                    if (!definitions.TryGetValue(
                            symbol,
                            out List<DomainDefinition>? sites))
                    {
                        sites = [];
                        definitions.Add(symbol, sites);
                    }
                    sites.Add(new DomainDefinition(
                        abc_index,
                        script_index,
                        trait_index,
                        trait));
                }
            }
        }

        if (!collect_external_types)
            return;

        foreach ((int abc_index, ABCFile abc) in
            abcs.OrderBy(value => value.Key)
                .Select(value =>
                    (value.Key, value.Value)))
        {
            var references = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (ASInstance instance in abc.Instances)
            {
                AddReference(
                    references,
                    instance.Super);
                foreach (ASMultiname @interface in
                    instance.GetInterfaces())
                {
                    AddReference(
                        references,
                        @interface);
                }
                AddTraitReferences(
                    references,
                    instance.Traits);
            }
            foreach (ASClass @class in abc.Classes)
            {
                AddTraitReferences(
                    references,
                    @class.Traits);
            }
            foreach (ASScript script in abc.Scripts)
            {
                AddTraitReferences(
                    references,
                    script.Traits);
            }
            foreach (ASMethod method in abc.Methods)
            {
                AddReference(
                    references,
                    method.ReturnType);
                foreach (ASParameter parameter in
                    method.Parameters)
                {
                    AddReference(
                        references,
                        parameter.Type);
                }
            }
            foreach (ASMethodBody body in
                abc.MethodBodies)
            {
                AddTraitReferences(
                    references,
                    body.Traits);
                foreach (ASException exception in
                    body.Exceptions)
                {
                    AddReference(
                        references,
                        exception.ExceptionType);
                }
            }
            foreach (string reference in references)
            {
                if (!external_types.TryGetValue(
                        reference,
                        out int earliest) ||
                    abc_index < earliest)
                {
                    external_types[reference] =
                        abc_index;
                }
            }
        }
    }

    internal static Avm2VerifierTypeRegistry For(
        ABCFile abc)
    {
        ArgumentNullException.ThrowIfNull(abc);
        return new Avm2VerifierTypeRegistry(
            new Dictionary<int, ABCFile>
            {
                [0] = abc
            },
            false);
    }

    internal static Avm2VerifierTypeRegistry Create(
        IReadOnlyList<ABCFile> abcs)
    {
        ArgumentNullException.ThrowIfNull(abcs);
        ABCFile[] files = abcs.ToArray();
        for (int index = 0;
            index < files.Length;
            index++)
        {
            ArgumentNullException.ThrowIfNull(files[index]);
        }
        return new Avm2VerifierTypeRegistry(
            files.Select((file, index) =>
                    (file, index))
                .ToDictionary(
                    value => value.index,
                    value => value.file),
            true);
    }

    internal static Avm2VerifierTypeRegistry Create(
        IReadOnlyDictionary<int, ABCFile> abcs)
    {
        ArgumentNullException.ThrowIfNull(abcs);
        var files = new SortedDictionary<int, ABCFile>();
        foreach ((int abc_index, ABCFile abc) in abcs)
        {
            ArgumentNullException.ThrowIfNull(abc);
            files.Add(abc_index, abc);
        }
        return new Avm2VerifierTypeRegistry(
            files,
            true);
    }

    internal string? ResolveInstanceIdentity(
        ASMultiname? name,
        ABCFile requester)
    {
        if (!abc_indices.TryGetValue(
                requester,
                out int requester_index))
        {
            return null;
        }
        ASMultiname? lookup = name?.Kind ==
            MultinameKind.TypeName
                ? name.QName
                : name;
        string symbol = RuntimeSymbol(lookup);
        if (symbol.Length == 0)
            return null;
        DomainDefinition[] loaded = definitions
            .GetValueOrDefault(symbol, [])
            .Where(value =>
                value.AbcIndex <= requester_index)
            .OrderBy(value => value.AbcIndex)
            .ThenBy(value => value.ScriptIndex)
            .ThenBy(value => value.TraitIndex)
            .ToArray();
        if (loaded.Length != 0)
        {
            DomainDefinition first = loaded[0];
            DomainDefinition[] owner = loaded
                .Where(value =>
                    value.AbcIndex == first.AbcIndex &&
                    value.ScriptIndex == first.ScriptIndex)
                .ToArray();
            if (owner.Length != 1 ||
                owner[0].Trait.Kind != TraitKind.Class ||
                !ValidClass(
                    abcs_by_index[first.AbcIndex],
                    owner[0].Trait.ClassIndex))
            {
                return null;
            }
            return ClassIdentity(
                first.AbcIndex,
                owner[0].Trait.ClassIndex);
        }
        string? core = CoreInstanceIdentity(name);
        if (core is not null)
            return core;
        return external_types.TryGetValue(
                symbol,
                out int first_reference) &&
            first_reference <= requester_index
            ? $"external-type:{FullRuntimeSymbol(name)}"
            : null;
    }

    internal string? ResolveVerifierReferenceIdentity(
        ASMultiname? name,
        ABCFile requester)
    {
        if (!abc_indices.TryGetValue(
                requester,
                out int requester_index))
        {
            return null;
        }
        string? resolved =
            ResolveInstanceIdentity(
                name,
                requester);
        if (resolved is not null)
            return resolved;
        ASMultiname? lookup = name?.Kind ==
            MultinameKind.TypeName
                ? name.QName
                : name;
        string symbol = RuntimeSymbol(lookup);
        if (symbol.Length == 0 ||
            HasLoadedDefinition(
                symbol,
                requester_index) ||
            HasLoadedClass(
                symbol,
                requester_index))
        {
            return null;
        }
        string full = FullRuntimeSymbol(name);
        return full.Length == 0
            ? null
            : $"external-type:{full}";
    }

    bool HasLoadedDefinition(
        string symbol,
        int requester_index) =>
        definitions.TryGetValue(
                symbol,
                out List<DomainDefinition>? sites) &&
            sites.Any(value =>
                value.AbcIndex <= requester_index);

    bool HasLoadedClass(
        string symbol,
        int requester_index) =>
        defined_classes.TryGetValue(
                symbol,
                out List<(
                    int AbcIndex,
                    int ClassIndex)>? sites) &&
            sites.Any(value =>
                value.AbcIndex <= requester_index);

    internal bool TryGetAbcIndex(
        ABCFile abc,
        out int abc_index) =>
        abc_indices.TryGetValue(
            abc,
            out abc_index);

    internal bool IsBuiltinClass(
        ASMultiname? name,
        ABCFile requester)
    {
        string? identity =
            ResolveInstanceIdentity(
                name,
                requester);
        return identity is not null &&
            !identity.StartsWith(
                "abc:",
                StringComparison.Ordinal);
    }

    internal static string? CoreInstanceIdentity(
        ASMultiname? name)
    {
        try
        {
            if (name?.Kind ==
                MultinameKind.TypeName)
            {
                return CoreInstanceIdentity(
                        name.QName) ==
                        "builtin:vector"
                    ? $"builtin-vector:{FullRuntimeSymbol(name)}"
                    : null;
            }
            if (name is null ||
                name.Kind is not (
                    MultinameKind.QName or
                    MultinameKind.QNameA) ||
                name.Namespace is not ASNamespace name_namespace ||
                name_namespace.Kind is not (
                    NamespaceKind.Package or
                    NamespaceKind.Namespace))
            {
                return null;
            }
            string local = name.RuntimeName;
            if (name_namespace.IsPublicRoot)
            {
                return CoreInstanceIdentities
                    .GetValueOrDefault(local);
            }
            return name_namespace.RuntimeName == "__AS3__.vec" &&
                local == "Vector"
                    ? "builtin:vector"
                    : null;
        }
        catch
        {
            return null;
        }
    }

    internal bool IsAssignable(
        string? target,
        string? source)
    {
        if (target is null || source is null)
            return false;
        if (string.Equals(
                target,
                source,
                StringComparison.Ordinal))
        {
            return true;
        }
        if (target == "builtin:object" &&
            ObjectAssignable(source))
        {
            return true;
        }
        if (CoreAssignable(target, source))
            return true;
        if (source.StartsWith(
                "builtin-class:",
                StringComparison.Ordinal) ||
            source.StartsWith(
                "external-class:",
                StringComparison.Ordinal))
        {
            return target is
                "builtin:class" or
                "builtin:object";
        }
        if (!TryClassIdentity(
                source,
                out int source_abc,
                out int source_class,
                out bool source_instance))
        {
            return false;
        }
        if (!source_instance)
        {
            return target is
                "builtin:class" or
                "builtin:object";
        }
        if (target == "builtin:object")
            return true;
        var visited = new HashSet<string>(
            StringComparer.Ordinal);
        var visited_interfaces =
            new HashSet<string>(
                StringComparer.Ordinal);
        string current = source;
        int current_abc = source_abc;
        int current_class = source_class;
        while (abcs_by_index.TryGetValue(
                current_abc,
                out ABCFile? current_file) &&
            visited.Add(current) &&
            ValidClass(
                current_file,
                current_class))
        {
            ASInstance instance =
                current_file
                    .Instances[current_class];
            if (ImplementsInterface(
                    target,
                    instance,
                    current_file,
                    visited_interfaces))
            {
                return true;
            }
            string? parent = ResolveInstanceIdentity(
                instance.Super,
                current_file);
            if (parent is null)
                return false;
            if (string.Equals(
                    target,
                    parent,
                    StringComparison.Ordinal))
            {
                return true;
            }
            if (CoreAssignable(target, parent))
                return true;
            if (!TryClassIdentity(
                    parent,
                    out current_abc,
                    out current_class,
                    out bool parent_instance) ||
                !parent_instance)
            {
                return false;
            }
            current = parent;
        }
        return false;
    }

    bool ImplementsInterface(
        string target,
        ASInstance instance,
        ABCFile requester,
        HashSet<string> visited)
    {
        var pending = new Stack<string>();
        AddInterfaces(
            pending,
            instance,
            requester);
        while (pending.TryPop(
            out string? identity))
        {
            if (!visited.Add(identity) ||
                !TryInterface(
                    identity,
                    out ASInstance contract,
                    out ABCFile contract_file))
            {
                continue;
            }
            if (string.Equals(
                    target,
                    identity,
                    StringComparison.Ordinal))
            {
                return true;
            }
            AddInterfaces(
                pending,
                contract,
                contract_file);
        }
        return false;
    }

    void AddInterfaces(
        Stack<string> pending,
        ASInstance instance,
        ABCFile requester)
    {
        foreach (ASMultiname name in
            instance.GetInterfaces())
        {
            string? identity =
                ResolveInstanceIdentity(
                    name,
                    requester);
            if (identity is not null)
                pending.Push(identity);
        }
    }

    bool TryInterface(
        string identity,
        out ASInstance instance,
        out ABCFile abc)
    {
        instance = null!;
        abc = null!;
        if (!TryClassIdentity(
                identity,
                out int abc_index,
                out int class_index,
                out bool is_instance) ||
            !is_instance ||
            !abcs_by_index.TryGetValue(
                abc_index,
                out ABCFile? file) ||
            !ValidClass(file, class_index))
        {
            return false;
        }
        abc = file;
        ASInstance candidate =
            abc.Instances[class_index];
        if (!candidate.Flags.HasFlag(
                ClassFlags.Interface))
        {
            abc = null!;
            return false;
        }
        instance = candidate;
        return true;
    }

    bool ObjectAssignable(string source)
    {
        if (source.StartsWith(
                "external-type:",
                StringComparison.Ordinal) ||
            source.StartsWith(
                "external-class:",
                StringComparison.Ordinal) ||
            source.StartsWith(
                "builtin-class:",
                StringComparison.Ordinal) ||
            source.StartsWith(
                "builtin-vector:",
                StringComparison.Ordinal) ||
            CoreInstanceIdentities.Values.Contains(
                source,
                StringComparer.Ordinal) ||
            source is
                "builtin:arguments" or
                "builtin:vector")
        {
            return true;
        }
        if (TryClassIdentity(
                source,
                out int abc_index,
                out int class_index,
                out _) &&
            abcs_by_index.TryGetValue(
                abc_index,
                out ABCFile? abc) &&
            ValidClass(abc, class_index))
        {
            return true;
        }
        string[] parts = source.Split(':');
        return parts.Length == 4 &&
            parts[0] == "abc" &&
            parts[2] == "script" &&
            int.TryParse(
                parts[1],
                out int script_abc) &&
            int.TryParse(
                parts[3],
                out int script_index) &&
            abcs_by_index.TryGetValue(
                script_abc,
                out ABCFile? script_file) &&
            script_index >= 0 &&
            script_index <
                script_file.Scripts.Count;
    }

    static bool CoreAssignable(
        string target,
        string source)
    {
        if (source.StartsWith(
                "external-type:",
                StringComparison.Ordinal) ||
            source.StartsWith(
                "builtin-vector:",
                StringComparison.Ordinal))
        {
            return target == "builtin:object";
        }
        var visited = new HashSet<string>(
            StringComparer.Ordinal);
        string current = source;
        while (visited.Add(current) &&
            DirectSupertypes.TryGetValue(
                current,
                out string? parent))
        {
            if (string.Equals(
                    target,
                    parent,
                    StringComparison.Ordinal))
            {
                return true;
            }
            current = parent;
        }
        return false;
    }

    static bool TryClassIdentity(
        string? identity,
        out int abc_index,
        out int class_index,
        out bool instance)
    {
        abc_index = -1;
        class_index = -1;
        instance = false;
        if (identity is null)
            return false;
        string[] parts = identity.Split(':');
        if (parts.Length != 5 ||
            parts[0] != "abc" ||
            parts[2] != "class" ||
            !int.TryParse(
                parts[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out abc_index) ||
            !int.TryParse(
                parts[3],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
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

    static void AddTraitReferences(
        HashSet<string> references,
        IEnumerable<ASTrait> traits)
    {
        foreach (ASTrait trait in traits)
        {
            if (trait.Kind is
                TraitKind.Slot or
                TraitKind.Constant)
            {
                AddReference(
                    references,
                    trait.Type);
            }
        }
    }

    static void AddReference(
        HashSet<string> references,
        ASMultiname? name)
    {
        if (name?.Kind ==
            MultinameKind.TypeName)
        {
            if (name.QNameIndex > 0 &&
                name.QNameIndex < name.Pool.Multinames.Count)
            {
                AddReference(
                    references,
                    name.Pool.Multinames[name.QNameIndex]);
            }
            foreach (int type_index in name.TypeIndices)
            {
                if (type_index <= 0 ||
                    type_index >= name.Pool.Multinames.Count)
                {
                    continue;
                }
                AddReference(
                    references,
                    name.Pool.Multinames[type_index]);
            }
            return;
        }
        string symbol = RuntimeSymbol(name);
        if (symbol.Length != 0 &&
            CoreInstanceIdentity(name) is null)
        {
            references.Add(symbol);
        }
    }

    static string RuntimeSymbol(
        ASMultiname? name)
    {
        try
        {
            if (name is null ||
                name.Kind is not (
                    MultinameKind.QName or
                    MultinameKind.QNameA) ||
                name.Namespace is not ASNamespace name_namespace ||
                name_namespace.Kind !=
                    NamespaceKind.Package)
            {
                return "";
            }
            return Avm2MethodAnalyzer
                .RuntimeSymbolIdentity(name);
        }
        catch
        {
            return "";
        }
    }

    static string FullRuntimeSymbol(
        ASMultiname? name)
    {
        try
        {
            return name is null
                ? ""
                : Avm2MethodAnalyzer
                    .RuntimeSymbolIdentity(name);
        }
        catch
        {
            return "";
        }
    }

    static bool ValidClass(
        ABCFile abc,
        int class_index) =>
        class_index >= 0 &&
        class_index < abc.Classes.Count &&
        class_index < abc.Instances.Count;

    static string ClassIdentity(
        int abc_index,
        int class_index) =>
        $"abc:{abc_index}:class:{class_index}:instance";
}
