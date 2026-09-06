using Flazzy.ABC;

namespace Flazzy.Tools;

public class AS3MultinameUpgrader
{
    private readonly bool _isApplyingMetadata;
    private readonly bool _isParsingInstructions;

    private readonly Dictionary<string, string> _oldClassNames, _newClassNames;
    private readonly Dictionary<string, string> _oldNamespaceNames, _newNamespaceNames;

    public AS3MultinameUpgrader(bool isApplyingMetadata, bool isParsingInstructions)
    {
        _isApplyingMetadata = isApplyingMetadata;
        _isParsingInstructions = isParsingInstructions;

        _oldClassNames = new Dictionary<string, string>();
        _newClassNames = new Dictionary<string, string>();

        _oldNamespaceNames = new Dictionary<string, string>();
        _newNamespaceNames = new Dictionary<string, string>();
    }

    public int Search(ABCFile abc)
    {
        int metadataNameIndex = _isApplyingMetadata ? abc.Pool.AddConstant("Flazzy", false) : 0;
        int previousNamespaceIndex = _isApplyingMetadata ? abc.Pool.AddConstant("PreviousNamespace", false) : 0;
        int previousQualifiedNameIndex = _isApplyingMetadata ? abc.Pool.AddConstant("PreviousQualifiedName", false) : 0;

        int namesUpgraded = 0;
        string? newClassName = null;
        string? newNamespaceName = null;
        foreach (ASTrait trait in abc.Scripts.SelectMany(s => s.Traits))
        {
            if (trait.Kind != TraitKind.Class) continue;

            ASClass? @class = trait.Class;
            if (@class is null) continue;
            if (!SearchClass(@class, ref newNamespaceName, ref newClassName, out string oldNamespaceName, out string oldClassName)) continue;

            namesUpgraded++;
            ASMetadata? metadata = null;
            if (_isApplyingMetadata)
            {
                trait.Attributes |= TraitAttributes.Metadata;

                metadata = new ASMetadata(abc)
                {
                    NameIndex = metadataNameIndex
                };

                trait.MetadataIndices.Add(abc.AddMetadata(metadata, false));
            }

            if (!string.IsNullOrWhiteSpace(newNamespaceName))
            {
                if (_isApplyingMetadata)
                {
                    (metadata ?? throw new InvalidOperationException("Upgrade metadata is unavailable."))
                        .Items.Add(new ASItemInfo(abc, previousNamespaceIndex, abc.Pool.AddConstant(oldNamespaceName, false)));
                }
                ASNamespace? @namespace = trait.QName.Namespace;
                if (@namespace is not null && @namespace.RuntimeName != newNamespaceName)
                {
                    SetPoolString(
                        abc.Pool,
                        @namespace.NameIndex,
                        newNamespaceName);
                }
            }

            if (!string.IsNullOrWhiteSpace(newClassName))
            {
                if (_isApplyingMetadata)
                {
                    (metadata ?? throw new InvalidOperationException("Upgrade metadata is unavailable."))
                        .Items.Add(new ASItemInfo(abc, previousQualifiedNameIndex, abc.Pool.AddConstant(oldClassName, false)));
                }
                if (trait.QName.Name != newClassName)
                {
                    SetPoolString(
                        abc.Pool,
                        trait.QName.NameIndex,
                        newClassName);
                }
            }

            ASNamespace? protected_namespace = @class.Instance.ProtectedNamespace;
            ASNamespace? class_namespace = trait.QName.Namespace;
            if (@class.Instance.Flags.HasFlag(ClassFlags.ProtectedNamespace) &&
                protected_namespace is not null &&
                class_namespace is not null)
            {
                string protectedNamespaceUpgrade = $"{class_namespace.RuntimeName}:{trait.QName.RuntimeName}";
                _newNamespaceNames[
                    protected_namespace.RuntimeName] =
                    protectedNamespaceUpgrade;
                SetPoolString(
                    abc.Pool,
                    protected_namespace.NameIndex,
                    protectedNamespaceUpgrade);
            }
        }
        SearchMultinames(abc);
        return namesUpgraded;
    }

    private void SearchMultinames(ABCFile abc)
    {
        foreach (ASMultiname? multiname in abc.Pool.Multinames)
        {
            if (multiname == null) continue;
            ASNamespaceSet? namespace_set = multiname.NamespaceSet;
            if (multiname.NamespaceSetIndex != 0 && namespace_set is not null)
            {
                foreach (ASNamespace @namespace in namespace_set.GetNamespaces())
                {
                    if (_newNamespaceNames.TryGetValue(@namespace.RuntimeName, out string? newNamespaceName))
                    {
                        SetPoolString(
                            abc.Pool,
                            @namespace.NameIndex,
                            newNamespaceName);
                    }
                    else if (TryParseNamespace(@namespace.RuntimeName, out ReadOnlySpan<char> left, out ReadOnlySpan<char> right))
                    {
                        // TODO: Come back to avoid string allocations if this feature is implemented https://github.com/dotnet/runtime/issues/27229
                        string leftPart = left.ToString();
                        string rightPart = right.ToString();

                        if (AccessCache(_oldNamespaceNames, _newNamespaceNames, leftPart, out string oldNamespaceName, out newNamespaceName))
                        {
                            string? newClassName = null;
                            if (_newClassNames.TryGetValue($"{oldNamespaceName}.{right}", out string? newFullClassName))
                            {
                                newClassName = GetClassName(newFullClassName);
                            }
                            SetPoolString(
                                abc.Pool,
                                @namespace.NameIndex,
                                $"{newNamespaceName ?? leftPart}:{newClassName ?? rightPart}");
                        }
                    }
                }
            }

            ASNamespace? multiname_namespace = multiname.Namespace;
            if (multiname.NamespaceIndex != 0 &&
                multiname_namespace is not null &&
                _newNamespaceNames.TryGetValue(multiname_namespace.RuntimeName, out string? namespace_upgrade))
            {
                SetPoolString(
                    abc.Pool,
                    multiname_namespace.NameIndex,
                    namespace_upgrade);
            }

            if (multiname.NameIndex != 0)
            {
                string oldNamespaceName = multiname_namespace?.RuntimeName ?? string.Empty;
                if (!_oldNamespaceNames.TryGetValue(oldNamespaceName, out string? cached_namespace))
                {
                    cached_namespace = multiname_namespace?.RuntimeName;
                }
                oldNamespaceName = cached_namespace ?? string.Empty;

                string? multiname_name = multiname.Name;
                if (string.IsNullOrWhiteSpace(multiname_name)) continue;
                string fullOldClassName = multiname_name;
                if (!string.IsNullOrWhiteSpace(oldNamespaceName))
                {
                    fullOldClassName = $"{oldNamespaceName}.{multiname.Name}";
                }

                if (_newClassNames.TryGetValue(fullOldClassName, out string? newClassName))
                {
                    int lastDot = newClassName.LastIndexOf('.');
                    SetPoolString(
                        abc.Pool,
                        multiname.NameIndex,
                        newClassName.Substring(lastDot + 1));
                }
            }
        }
    }
    private bool SearchClass(ASClass @class, ref string? newNamespaceName, ref string? newClassName, out string oldNamespaceName, out string oldClassName)
    {
        ASNamespace? @namespace = @class.QName.Namespace;
        string? class_name = @class.QName.Name;
        if (@namespace is null || string.IsNullOrWhiteSpace(class_name))
        {
            oldNamespaceName = string.Empty;
            oldClassName = string.Empty;
            return false;
        }
        bool isNewNamespaceNameCached = AccessCache(_oldNamespaceNames, _newNamespaceNames, @namespace.RuntimeName, out oldNamespaceName, out newNamespaceName);

        // Namespace must be checked first, to ensure that we're using the old full qualified class name.
        newClassName = null;
        oldClassName = class_name;
        string oldFullClassName = oldClassName;
        if (!string.IsNullOrWhiteSpace(oldNamespaceName))
        {
            oldFullClassName = $"{oldNamespaceName}.{@class.QName.Name}";
        }

        bool isNewClassNameCached = AccessCache(_oldClassNames, _newClassNames, oldFullClassName, out oldFullClassName, out string? newFullClassName);
        if (isNewClassNameCached) // Separate the namespace from the full qualified class name.
        {
            oldClassName = GetClassName(oldFullClassName);
            newClassName = GetClassName(newFullClassName ?? throw new InvalidOperationException("Cached class upgrade is unavailable."));
        }

        // New names do exist, no need to search through the traits.
        if (isNewNamespaceNameCached && isNewClassNameCached) return true;

        ASInstance instance = @class.Instance;
        foreach (ASTrait trait in @class.Traits.Concat(instance.Traits))
        {
            if (SearchTrait(@class, trait, ref newNamespaceName, ref newClassName) && !_isParsingInstructions) break;
            if (!string.IsNullOrWhiteSpace(newNamespaceName) && !string.IsNullOrWhiteSpace(newClassName)) break;

            ASMethod? method = trait.Method ?? trait.Function;
            if (method == null || method.Body == null) continue;

            if (SearchInstructions(@class, method, ref newNamespaceName, ref newClassName)) break;
        }

        // Check if the constructor name isn't already the same as the class name, and only check the ending of the constructor name as it may have a package name prefix.
        string? constructor_name = instance.Constructor.Name;
        if (string.IsNullOrWhiteSpace(newClassName) && !string.IsNullOrWhiteSpace(constructor_name) &&
            !constructor_name.EndsWith(class_name, StringComparison.OrdinalIgnoreCase))
        {
            newClassName = constructor_name;
        }

        if (!isNewClassNameCached && !string.IsNullOrWhiteSpace(newClassName))
        {
            newFullClassName = $"{oldNamespaceName}.{newClassName}";
        }

        isNewClassNameCached = isNewClassNameCached || TryUpdateCache(_oldClassNames, _newClassNames, oldFullClassName, newFullClassName);
        isNewNamespaceNameCached = isNewNamespaceNameCached || TryUpdateCache(_oldNamespaceNames, _newNamespaceNames, @namespace.RuntimeName, newNamespaceName);
        return isNewNamespaceNameCached || isNewClassNameCached;
    }

    protected virtual bool SearchTrait(ASClass @class, ASTrait trait, ref string? namespaceNameUpgrade, ref string? qualifiedNameUpgrade)
    {
        ASNamespace? trait_namespace = trait.QName.Namespace;
        string? class_name = @class.QName.Name;
        ASNamespace? class_namespace = @class.QName.Namespace;
        if (trait_namespace is null || class_namespace is null || string.IsNullOrWhiteSpace(class_name)) return false;
        if (trait_namespace.RuntimeName.Length < 2) return false;
        if (trait_namespace.Kind == NamespaceKind.PackageInternal) return false;

        ReadOnlySpan<char> traitNamespaceName = trait_namespace.RuntimeName;
        if (traitNamespaceName.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return false;

        if (!TryParseNamespace(trait_namespace.RuntimeName, out ReadOnlySpan<char> left, out ReadOnlySpan<char> right)) return false;

        // Return true only if any names were upgraded from this trait.
        bool wasQualifiedNameUpgraded = TryUpgrade(class_name, right, ref qualifiedNameUpgrade);
        bool wasNamespaceNameUpgraded = TryUpgrade(class_namespace.RuntimeName, left, ref namespaceNameUpgrade);
        return wasQualifiedNameUpgraded || wasNamespaceNameUpgraded;
    }
    protected virtual bool SearchInstructions(ASClass @class, ASMethod method, ref string? namespaceNameUpgrade, ref string? qualifiedNameUpgrade)
    {
        /*
         * As a last resort, if no name has been successfully resolved, we can attempt to extract the fully qualified name from an instruction attempting to resolve the current instance/scope.
         * If an internal method like 'toString' is called on a locally scoped/initialized variable(try/catch, switch, etc), it may utilize the real fully qualified name of the class when invoking the internal method call.
         * setproperty MultinameL([ !!>>> PrivateNamespace("com.hurlant.util:Hex") <<<!! ,StaticProtectedNs("com.hurlant.util:Hex"),StaticProtectedNs("Object"),PackageNamespace("com.hurlant.util"),PackageInternalNs("com.hurlant.util"),PrivateNamespace("FilePrivateNS:Hex"),PackageNamespace(""),Namespace("http://adobe.com/AS3/2006/builtin")])
         */

        return false;
    }

    private static bool TryUpgrade(ReadOnlySpan<char> previous, ReadOnlySpan<char> current, ref string? nameUpgrade)
    {
        // Upgrade has already been applied.
        if (!string.IsNullOrWhiteSpace(nameUpgrade)) return false;

        // Names already match, nothing to upgrade.
        if (current.Equals(previous, StringComparison.OrdinalIgnoreCase)) return false;

        nameUpgrade = current.ToString();
        return true;
    }
    private static bool TryParseNamespace(ReadOnlySpan<char> fullName, out ReadOnlySpan<char> left, out ReadOnlySpan<char> right)
    {
        left = default;
        right = default;

        // Internal AS3 method
        if (fullName.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return false;

        // File scoped class
        if (fullName.StartsWith("FilePrivateNS:")) return false;

        int separatorIndex = fullName.IndexOf(':');
        if (separatorIndex == -1) return false;

        left = fullName.Slice(0, separatorIndex);
        right = fullName.Slice(separatorIndex + 1);
        return true;
    }

    private static string GetClassName(string fullClassName)
    {
        if (string.IsNullOrWhiteSpace(fullClassName)) return fullClassName;

        int dotIndex = fullClassName.LastIndexOf('.');
        return dotIndex != -1 ? fullClassName.Substring(dotIndex + 1) : fullClassName;
    }
    private static void SetPoolString(
        ASConstantPool pool,
        int index,
        string value)
    {
        if (index > 0 && index < pool.Strings.Count)
            pool.Strings[index] = value;
    }
    private static bool TryUpdateCache(Dictionary<string, string> oldNames, Dictionary<string, string> newNames, string oldName, string? newName)
    {
        // Indicates that the cache has already been updated with the 'current' parameter, or that the 'current' parameter is not valid for caching.
        if (string.IsNullOrWhiteSpace(newName)) return false;

        oldNames.Add(newName, oldName);
        newNames.Add(oldName, newName);
        return true;
    }
    private static bool AccessCache(Dictionary<string, string> oldNames, Dictionary<string, string> newNames, string name, out string oldName, out string? newName)
    {
        oldName = name;
        if (string.IsNullOrWhiteSpace(name))
        {
            newName = null;
            return false;
        }

        // Check if the current name has an existing update from a previous search.
        // If it does not, then check if the name was updated implicitly through the string constant pool.
        if (!newNames.TryGetValue(name, out newName) &&
            oldNames.TryGetValue(name, out string? cachedOldName) &&
            cachedOldName is not null)
        {
            newName = name;
            oldName = cachedOldName;
        }

        return !string.IsNullOrWhiteSpace(newName);
    }
}
