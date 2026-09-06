using System.Runtime.InteropServices;
using System.Text;

using Flazzy.IO;

namespace Flazzy.ABC;

/// <summary>
/// Represents a block of array-based entries that reflect the constants used by all the methods.
/// </summary>
public class ASConstantPool : IFlashItem
{
    private readonly Dictionary<ASMultiname, int> _multinamesIndicesCache;
    private readonly Dictionary<string, List<ASMultiname>> _multinamesByNameCache;

    public ABCFile? ABC { get; }

    public List<int> Integers { get; }
    public List<uint> UIntegers { get; }
    public List<double> Doubles { get; }
    public List<float> Floats { get; }
    public List<ASFloat4> Float4s { get; }
    public List<string?> Strings { get; }
    public List<ASNamespace?> Namespaces { get; }
    public List<ASNamespaceSet?> NamespaceSets { get; }
    public List<ASMultiname?> Multinames { get; }

    public ASConstantPool()
    {
        _multinamesIndicesCache = new Dictionary<ASMultiname, int>();
        _multinamesByNameCache = new Dictionary<string, List<ASMultiname>>();

        Integers = new List<int>();
        UIntegers = new List<uint>();
        Doubles = new List<double>();
        Floats = new List<float>();
        Float4s = new List<ASFloat4>();
        Strings = new List<string?>();
        Namespaces = new List<ASNamespace?>();
        NamespaceSets = new List<ASNamespaceSet?>();
        Multinames = new List<ASMultiname?>();
    }
    public ASConstantPool(ABCFile abc)
        : this()
    {
        ABC = abc;
    }
    public ASConstantPool(ABCFile abc, ref SpanFlashReader input)
        : this(abc)
    {
        Integers.Capacity = input.ReadEncodedInt();
        if (Integers.Capacity > 0) Integers.Add(0);
        for (int i = 1; i < Integers.Capacity; i++)
        {
            Integers.Add(input.ReadEncodedInt());
        }

        UIntegers.Capacity = input.ReadEncodedInt();
        if (UIntegers.Capacity > 0) UIntegers.Add(0);
        for (int i = 1; i < UIntegers.Capacity; i++)
        {
            UIntegers.Add(input.ReadEncodedUInt());
        }

        Doubles.Capacity = input.ReadEncodedInt();
        if (Doubles.Capacity > 0)
            Doubles.Add(
                ASRuntimeDefaults.NumberNaN);
        for (int i = 1; i < Doubles.Capacity; i++)
        {
            Doubles.Add(input.ReadDouble());
        }

        if (ABC?.HasFloatSupport == true)
        {
            Floats.Capacity = input.ReadEncodedInt();
            if (Floats.Capacity > 0)
                Floats.Add(
                    ASRuntimeDefaults.FloatNaN);
            for (int i = 1; i < Floats.Capacity; i++)
            {
                Floats.Add(input.ReadSingle());
            }

            Float4s.Capacity = input.ReadEncodedInt();
            if (Float4s.Capacity > 0)
                Float4s.Add(
                    ASRuntimeDefaults.Float4NaN);
            for (int i = 1; i < Float4s.Capacity; i++)
            {
                Float4s.Add(new ASFloat4(
                    input.ReadSingle(),
                    input.ReadSingle(),
                    input.ReadSingle(),
                    input.ReadSingle()));
            }
        }

        Strings.Capacity = input.ReadEncodedInt();
        if (Strings.Capacity > 0) Strings.Add(default);
        for (int i = 1; i < Strings.Capacity; i++)
        {
            Strings.Add(input.ReadString());
        }

        Namespaces.Capacity = input.ReadEncodedInt();
        if (Namespaces.Capacity > 0) Namespaces.Add(default);
        for (int i = 1; i < Namespaces.Capacity; i++)
        {
            Namespaces.Add(new ASNamespace(this, ref input));
        }

        NamespaceSets.Capacity = input.ReadEncodedInt();
        if (NamespaceSets.Capacity > 0) NamespaceSets.Add(default);
        for (int i = 1; i < NamespaceSets.Capacity; i++)
        {
            NamespaceSets.Add(new ASNamespaceSet(this, ref input));
        }

        Multinames.Capacity = input.ReadEncodedInt();
        if (Multinames.Capacity > 0) Multinames.Add(default);
        for (int i = 1; i < Multinames.Capacity; i++)
        {
            Multinames.Add(ReadMultiname(ref input));
        }

        _multinamesByNameCache.TrimExcess();
        _multinamesIndicesCache.TrimExcess();
    }

    public object? GetConstant(ConstantKind type, int index) => type switch
    {
        ConstantKind.True => true,
        ConstantKind.False => false,
        ConstantKind.String => Strings[index],
        ConstantKind.Float => Floats[index],
        ConstantKind.Float4 => Float4s[index],
        ConstantKind.Double => Doubles[index],
        ConstantKind.Integer => Integers[index],
        ConstantKind.UInteger => UIntegers[index],
        ConstantKind.PrivateNs or
        ConstantKind.Namespace or
        ConstantKind.PackageNamespace or
        ConstantKind.PackageInternalNs or
        ConstantKind.ProtectedNs or
        ConstantKind.ExplicitNamespace or
        ConstantKind.StaticProtectedNs => Namespaces[index],

        ConstantKind.Null or ConstantKind.Undefined or _ => null,
    };

    public object? GetDefaultValue(
        ConstantKind type,
        int index,
        ASMultiname? declared_type)
    {
        if (index is < 0 or > 0x3FFFFFFF)
            throw new InvalidDataException(
                $"AVM2 default-value index is outside u30: {index}.");
        if (index > 0)
        {
            object? value = type switch
            {
                ConstantKind.String or
                ConstantKind.Float or
                ConstantKind.Float4 or
                ConstantKind.Double or
                ConstantKind.Integer or
                ConstantKind.UInteger or
                ConstantKind.PrivateNs or
                ConstantKind.Namespace or
                ConstantKind.PackageNamespace or
                ConstantKind.PackageInternalNs or
                ConstantKind.ProtectedNs or
                ConstantKind.ExplicitNamespace or
                ConstantKind.StaticProtectedNs or
                ConstantKind.Null or
                ConstantKind.True or
                ConstantKind.False =>
                    GetConstant(type, index),
                _ => throw new InvalidDataException(
                    $"Illegal AVM2 default-value kind {type} for index {index}.")
            };
            if (!IsLegalDefaultValue(declared_type, value))
            {
                throw new InvalidDataException(
                    $"Illegal AVM2 default value {type}:{index} for the declared type.");
            }
            return value;
        }
        if (declared_type is null)
            return ASUndefined.Value;
        return GetPublicBuiltinName(declared_type) switch
        {
            "int" => 0,
            "uint" => 0u,
            "Number" => ASRuntimeDefaults.NumberNaN,
            "Boolean" => false,
            "float" => ASRuntimeDefaults.FloatNaN,
            "float4" => ASRuntimeDefaults.Float4NaN,
            _ => null
        };
    }

    static bool IsLegalDefaultValue(
        ASMultiname? declared_type,
        object? value)
    {
        if (declared_type is null)
            return true;
        return GetPublicBuiltinName(declared_type) switch
        {
            "Object" => value is not ASUndefined,
            "Number" => value is int or uint or double,
            "float" => value is float,
            "float4" => value is ASFloat4,
            "Boolean" => value is bool,
            "uint" => value switch
            {
                uint => true,
                int number => number >= 0,
                double number =>
                    number >= uint.MinValue &&
                    number <= uint.MaxValue &&
                    number == Math.Truncate(number),
                _ => false
            },
            "int" => value switch
            {
                int => true,
                uint number => number <= int.MaxValue,
                double number =>
                    number >= int.MinValue &&
                    number <= int.MaxValue &&
                    number == Math.Truncate(number),
                _ => false
            },
            "String" => value is null or string,
            "Namespace" => value is null or ASNamespace,
            _ => value is null
        };
    }

    public static string? GetPublicBuiltinName(
        ASMultiname? declared_type)
    {
        if (declared_type is null ||
            declared_type.Kind != MultinameKind.QName)
            return null;
        ASNamespace? declared_namespace = declared_type.Namespace;
        if (declared_namespace?.IsPublicRoot != true)
        {
            return null;
        }
        return declared_type.Name;
    }

    public int AddConstant(object value, bool recycle = true)
    {
        switch (Type.GetTypeCode(value.GetType()))
        {
            case TypeCode.Int32:
                EnsureSentinel(Integers, 0);
                return AddConstant(Integers, (int)value, recycle);
            case TypeCode.UInt32:
                EnsureSentinel(UIntegers, 0u);
                return AddConstant(UIntegers, (uint)value, recycle);
            case TypeCode.Single:
                EnsureFloatSupport();
                EnsureFloatSentinel();
                return AddConstant(Floats, (float)value, recycle);
            case TypeCode.Double:
                EnsureSentinel(Doubles, ASRuntimeDefaults.NumberNaN);
                return AddConstant(Doubles, (double)value, recycle);
            case TypeCode.String:
                EnsureSentinel(Strings, null);
                return AddConstant(Strings, (string)value, recycle);
            default:
            {
                return value switch
                {
                    ASMultiname multiname => AddReferenceConstant(Multinames, multiname, recycle),
                    ASNamespace @namespace => AddReferenceConstant(Namespaces, @namespace, recycle),
                    ASNamespaceSet namespaceSet => AddReferenceConstant(NamespaceSets, namespaceSet, recycle),
                    ASFloat4 float4 => AddFloat4Constant(float4, recycle),
                    _ => throw new ArgumentException("The provided value does not belong anywhere in the constant pool.", nameof(value)),
                };
            }
        }
    }

    int AddFloat4Constant(ASFloat4 value, bool recycle)
    {
        EnsureFloatSupport();
        if (Float4s.Count == 0)
            Float4s.Add(
                ASRuntimeDefaults.Float4NaN);
        return AddConstant(Float4s, value, recycle);
    }

    void EnsureFloatSentinel()
    {
        if (Floats.Count == 0)
            Floats.Add(
                ASRuntimeDefaults.FloatNaN);
    }

    void EnsureFloatSupport()
    {
        ABCFile abc = ABC ??
            throw new InvalidOperationException("Float constants require an ABC context.");
        if (!abc.HasFloatSupport)
            throw new InvalidOperationException($"ABC version {abc.Version} does not support float constants.");
    }

    int AddReferenceConstant<T>(List<T?> constants, T value, bool recycle)
        where T : class
    {
        EnsureSentinel(constants, null);
        return AddConstant(constants, value, recycle);
    }

    static void EnsureSentinel<T>(List<T> constants, T sentinel)
    {
        if (constants.Count == 0)
            constants.Add(sentinel);
    }

    protected virtual int AddConstant<T>(List<T> constants, T value, bool recycle)
    {
        int index = (recycle ? constants.IndexOf(value, 1) : -1);
        if (index == -1)
        {
            constants.Add(value);
            index = (constants.Count - 1);
        }
        return index;
    }

    public int GetMultinameIndex(string name)
    {
        return GetMultinameIndices(name).FirstOrDefault();
    }
    public ASMultiname? GetMultiname(string name)
    {
        return GetMultinames(name).FirstOrDefault();
    }

    public IEnumerable<int> GetMultinameIndices(string name)
    {
        foreach (ASMultiname multiname in GetMultinames(name))
        {
            yield return _multinamesIndicesCache[multiname];
        }
    }
    public IEnumerable<ASMultiname> GetMultinames(string name)
    {
        return _multinamesByNameCache.GetValueOrDefault(name) ?? Enumerable.Empty<ASMultiname>();
    }

    private ASMultiname ReadMultiname(ref SpanFlashReader input)
    {
        ASMultiname multiname = new(this, ref input);
        if (multiname.NameIndex > 0 &&
            multiname.NameIndex < Strings.Count)
        {
            string name = multiname.Name ?? string.Empty;
            if (!_multinamesByNameCache.TryGetValue(name, out List<ASMultiname>? multinames))
            {
                multinames = new List<ASMultiname>();
                _multinamesByNameCache.Add(name, multinames);
            }
            multinames.Add(multiname);
        }
        _multinamesIndicesCache.Add(multiname, Multinames.Count);
        return multiname;
    }

    public int GetSize()
    {
        bool has_float_support = RequireFloatLayout();
        int size = 0;
        size += SpanFlashWriter.GetEncodedIntSize(Integers.Count);
        for (int i = 1; i < Integers.Count; i++)
        {
            size += SpanFlashWriter.GetEncodedIntSize(Integers[i]);
        }

        size += SpanFlashWriter.GetEncodedIntSize(UIntegers.Count);
        for (int i = 1; i < UIntegers.Count; i++)
        {
            size += SpanFlashWriter.GetEncodedUIntSize(UIntegers[i]);
        }

        size += SpanFlashWriter.GetEncodedIntSize(Doubles.Count);
        if (Doubles.Count > 1)
        {
            size += (Doubles.Count - 1) * sizeof(double);
        }

        if (has_float_support)
        {
            size += SpanFlashWriter.GetEncodedIntSize(Floats.Count);
            if (Floats.Count > 1)
            {
                size += (Floats.Count - 1) * sizeof(float);
            }

            size += SpanFlashWriter.GetEncodedIntSize(Float4s.Count);
            if (Float4s.Count > 1)
            {
                size += (Float4s.Count - 1) * sizeof(float) * 4;
            }
        }

        size += SpanFlashWriter.GetEncodedIntSize(Strings.Count);
        for (int i = 1; i < Strings.Count; i++)
        {
            string value = Strings[i] ?? throw new InvalidDataException($"String constant {i} is null.");
            int length = Encoding.UTF8.GetByteCount(value);
            size += SpanFlashWriter.GetEncodedIntSize(length);
            size += length;
        }

        size += SpanFlashWriter.GetEncodedIntSize(Namespaces.Count);
        for (int i = 1; i < Namespaces.Count; i++)
        {
            size += (Namespaces[i] ?? throw new InvalidDataException($"Namespace constant {i} is null.")).GetSize();
        }

        size += SpanFlashWriter.GetEncodedIntSize(NamespaceSets.Count);
        for (int i = 1; i < NamespaceSets.Count; i++)
        {
            size += (NamespaceSets[i] ?? throw new InvalidDataException($"Namespace-set constant {i} is null.")).GetSize();
        }

        size += SpanFlashWriter.GetEncodedIntSize(Multinames.Count);
        for (int i = 1; i < Multinames.Count; i++)
        {
            size += (Multinames[i] ?? throw new InvalidDataException($"Multiname constant {i} is null.")).GetSize();
        }
        return size;
    }
    public void WriteTo(ref SpanFlashWriter output)
    {
        bool has_float_support = RequireFloatLayout();
        output.WriteEncodedInt(Integers.Count);
        for (int i = 1; i < Integers.Count; i++)
        {
            output.WriteEncodedInt(Integers[i]);
        }

        output.WriteEncodedInt(UIntegers.Count);
        for (int i = 1; i < UIntegers.Count; i++)
        {
            output.WriteEncodedUInt(UIntegers[i]);
        }

        output.WriteEncodedInt(Doubles.Count);
        if (Doubles.Count > 1)
        {
            output.WriteDoubleArray(CollectionsMarshal.AsSpan(Doubles).Slice(1));
        }

        if (has_float_support)
        {
            output.WriteEncodedInt(Floats.Count);
            for (int i = 1; i < Floats.Count; i++)
            {
                output.Write(Floats[i]);
            }

            output.WriteEncodedInt(Float4s.Count);
            for (int i = 1; i < Float4s.Count; i++)
            {
                ASFloat4 value = Float4s[i];
                output.Write(value.X);
                output.Write(value.Y);
                output.Write(value.Z);
                output.Write(value.W);
            }
        }

        output.WriteEncodedInt(Strings.Count);
        for (int i = 1; i < Strings.Count; i++)
        {
            output.WriteString(Strings[i] ?? throw new InvalidDataException($"String constant {i} is null."));
        }

        WriteItems(ref output, Namespaces);
        WriteItems(ref output, NamespaceSets);
        WriteItems(ref output, Multinames);
    }

    bool RequireFloatLayout()
    {
        return ABC?.HasFloatSupport ??
            throw new InvalidOperationException("Constant-pool serialization requires an ABC context.");
    }

    private static void WriteItems<T>(ref SpanFlashWriter output, List<T?> constants)
        where T : class, IFlashItem
    {
        output.WriteEncodedInt(constants.Count);
        for (int i = 1; i < constants.Count; i++)
        {
            (constants[i] ?? throw new InvalidDataException($"{typeof(T).Name} constant {i} is null.")).WriteTo(ref output);
        }
    }
}
