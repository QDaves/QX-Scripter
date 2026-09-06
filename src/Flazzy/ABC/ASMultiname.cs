using Flazzy.IO;

namespace Flazzy.ABC;

public sealed class ASMultiname : IFlashItem, IEquatable<ASMultiname>, IPoolConstant, IQName, IRTQName, IMultiname, IMultinameL
{
    public MultinameKind Kind { get; set; }
    public ASConstantPool Pool { get; init; }

    public bool IsRuntime => Kind switch
    {
        MultinameKind.RTQName or
        MultinameKind.RTQNameA or
        MultinameKind.RTQNameL or
        MultinameKind.RTQNameLA or
        MultinameKind.MultinameL or
        MultinameKind.MultinameLA => true,
        _ => false,
    };
    public bool IsAttribute => Kind switch
    {
        MultinameKind.QNameA or
        MultinameKind.RTQNameA or
        MultinameKind.RTQNameLA or
        MultinameKind.MultinameA or
        MultinameKind.MultinameLA => true,
        _ => false,
    };
    public bool IsNameNeeded => Kind switch
    {
        MultinameKind.RTQNameL or
        MultinameKind.RTQNameLA or
        MultinameKind.MultinameL or
        MultinameKind.MultinameLA => true,
        _ => false,
    };
    public bool IsNamespaceNeeded => Kind switch
    {
        MultinameKind.RTQName or
        MultinameKind.RTQNameA or
        MultinameKind.RTQNameL or
        MultinameKind.RTQNameLA => true,
        _ => false,
    };

    public int NameIndex { get; set; }
    public string? Name => Pool.Strings[NameIndex];
    public bool IsAnyName =>
        NameIndex == 0 &&
        Kind is
            MultinameKind.QName or
            MultinameKind.QNameA or
            MultinameKind.RTQName or
            MultinameKind.RTQNameA or
            MultinameKind.Multiname or
            MultinameKind.MultinameA;
    bool HasName =>
        NameIndex >= 0 &&
        NameIndex < Pool.Strings.Count &&
        Pool.Strings[NameIndex] is not null;
    public string RuntimeName =>
        IsNameNeeded
            ? "<runtime>"
            : IsAnyName
                ? "*"
                : HasName
                    ? Pool.Strings[NameIndex] ?? "<invalid>"
                    : "<invalid>";

    public int QNameIndex { get; set; }
    public ASMultiname? QName => Pool.Multinames[QNameIndex];

    public int NamespaceIndex { get; set; }
    public ASNamespace? Namespace => Pool.Namespaces[NamespaceIndex];

    public int NamespaceSetIndex { get; set; }
    public ASNamespaceSet? NamespaceSet => Pool.NamespaceSets[NamespaceSetIndex];

    public List<int> TypeIndices { get; }

    public ASMultiname(ASConstantPool pool)
    {
        Pool = pool;
        TypeIndices = new List<int>();
    }
    public ASMultiname(ASConstantPool pool, ref SpanFlashReader input)
        : this(pool)
    {
        Kind = (MultinameKind)input.ReadByte();
        switch (Kind)
        {
            case MultinameKind.QName:
            case MultinameKind.QNameA:
            {
                NamespaceIndex = input.ReadEncodedInt();
                NameIndex = input.ReadEncodedInt();
                break;
            }

            case MultinameKind.RTQName:
            case MultinameKind.RTQNameA:
            {
                NameIndex = input.ReadEncodedInt();
                break;
            }

            case MultinameKind.RTQNameL:
            case MultinameKind.RTQNameLA:
            {
                /* No data. */
                break;
            }

            case MultinameKind.Multiname:
            case MultinameKind.MultinameA:
            {
                NameIndex = input.ReadEncodedInt();
                NamespaceSetIndex = input.ReadEncodedInt();
                break;
            }

            case MultinameKind.MultinameL:
            case MultinameKind.MultinameLA:
            {
                NamespaceSetIndex = input.ReadEncodedInt();
                break;
            }

            case MultinameKind.TypeName:
            {
                QNameIndex = input.ReadEncodedInt();
                TypeIndices.Capacity = input.ReadEncodedInt();
                for (int i = 0; i < TypeIndices.Capacity; i++)
                {
                    int typeIndex = input.ReadEncodedInt();
                    TypeIndices.Add(typeIndex);
                }
                break;
            }
        }
    }

    public IEnumerable<ASMultiname?> GetTypes()
    {
        for (int i = 0; i < TypeIndices.Count; i++)
        {
            int type_index = TypeIndices[i];
            if (type_index == 0)
            {
                yield return null;
                continue;
            }
            if ((uint)type_index >= (uint)Pool.Multinames.Count)
            {
                throw new InvalidDataException($"Multiname constant {type_index} is outside the constant pool.");
            }
            yield return Pool.Multinames[type_index] ??
                throw new InvalidDataException($"Multiname constant {type_index} is null.");
        }
    }

    public int GetSize()
    {
        int size = 0;
        size += sizeof(byte);
        switch (Kind)
        {
            case MultinameKind.QName:
            case MultinameKind.QNameA:
            {
                size += SpanFlashWriter.GetEncodedIntSize(NamespaceIndex);
                size += SpanFlashWriter.GetEncodedIntSize(NameIndex);
                break;
            }

            case MultinameKind.RTQName:
            case MultinameKind.RTQNameA:
            {
                size += SpanFlashWriter.GetEncodedIntSize(NameIndex);
                break;
            }

            case MultinameKind.Multiname:
            case MultinameKind.MultinameA:
            {
                size += SpanFlashWriter.GetEncodedIntSize(NameIndex);
                size += SpanFlashWriter.GetEncodedIntSize(NamespaceSetIndex);
                break;
            }

            case MultinameKind.MultinameL:
            case MultinameKind.MultinameLA:
            {
                size += SpanFlashWriter.GetEncodedIntSize(NamespaceSetIndex);
                break;
            }

            case MultinameKind.TypeName:
            {
                size += SpanFlashWriter.GetEncodedIntSize(QNameIndex);
                size += SpanFlashWriter.GetEncodedIntSize(TypeIndices.Count);
                for (int i = 0; i < TypeIndices.Count; i++)
                {
                    size += SpanFlashWriter.GetEncodedIntSize(TypeIndices[i]);
                }
                break;
            }
        }
        return size;
    }
    public void WriteTo(ref SpanFlashWriter output)
    {
        output.Write((byte)Kind);
        switch (Kind)
        {
            case MultinameKind.QName:
            case MultinameKind.QNameA:
            {
                output.WriteEncodedInt(NamespaceIndex);
                output.WriteEncodedInt(NameIndex);
                break;
            }

            case MultinameKind.RTQName:
            case MultinameKind.RTQNameA:
            {
                output.WriteEncodedInt(NameIndex);
                break;
            }

            case MultinameKind.RTQNameL:
            case MultinameKind.RTQNameLA:
            {
                /* No data. */
                break;
            }

            case MultinameKind.Multiname:
            case MultinameKind.MultinameA:
            {
                output.WriteEncodedInt(NameIndex);
                output.WriteEncodedInt(NamespaceSetIndex);
                break;
            }

            case MultinameKind.MultinameL:
            case MultinameKind.MultinameLA:
            {
                output.WriteEncodedInt(NamespaceSetIndex);
                break;
            }

            case MultinameKind.TypeName:
            {
                output.WriteEncodedInt(QNameIndex);
                output.WriteEncodedInt(TypeIndices.Count);
                for (int i = 0; i < TypeIndices.Count; i++)
                {
                    int typeIndex = TypeIndices[i];
                    output.WriteEncodedInt(typeIndex);
                }
                break;
            }
        }
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        if (Kind != MultinameKind.TypeName && !IsNameNeeded)
        {
            hash.Add(IsAnyName);
            if (!IsAnyName)
            {
                hash.Add(HasName);
                if (HasName)
                    hash.Add(Pool.Strings[NameIndex], StringComparer.Ordinal);
                else
                    hash.Add(NameIndex);
            }
        }
        switch (Kind)
        {
            case MultinameKind.QName:
            case MultinameKind.QNameA:
                AddNamespaceHash(ref hash);
                break;
            case MultinameKind.Multiname:
            case MultinameKind.MultinameA:
            case MultinameKind.MultinameL:
            case MultinameKind.MultinameLA:
                AddNamespaceSetHash(ref hash);
                break;
        }
        return hash.ToHashCode();
    }
    public bool Equals(ASMultiname? other)
    {
        if (other == null) return false;
        if (!ReferenceEquals(this, other) && Pool == other.Pool) return false;
        if (Kind != other.Kind) return false;
        if (Kind != MultinameKind.TypeName && !IsNameNeeded &&
            (IsAnyName != other.IsAnyName ||
             !IsAnyName && !SameName(other)))
        {
            return false;
        }
        return Kind switch
        {
            MultinameKind.QName or
            MultinameKind.QNameA =>
                SameNamespace(other),
            MultinameKind.RTQName or
            MultinameKind.RTQNameA or
            MultinameKind.RTQNameL or
            MultinameKind.RTQNameLA => true,
            MultinameKind.Multiname or
            MultinameKind.MultinameA or
            MultinameKind.MultinameL or
            MultinameKind.MultinameLA =>
                SameNamespaceSet(other),
            MultinameKind.TypeName =>
                SameTypeName(other),
            _ => false
        };
    }

    bool SameName(ASMultiname other)
    {
        if (HasName != other.HasName)
            return false;
        return HasName
            ? string.Equals(
                Pool.Strings[NameIndex],
                other.Pool.Strings[other.NameIndex],
                StringComparison.Ordinal)
            : NameIndex == other.NameIndex;
    }

    void AddNamespaceHash(ref HashCode hash)
    {
        bool valid = NamespaceIndex >= 0 &&
            NamespaceIndex < Pool.Namespaces.Count;
        hash.Add(valid);
        if (valid)
            hash.Add(Pool.Namespaces[NamespaceIndex]);
        else
            hash.Add(NamespaceIndex);
    }

    void AddNamespaceSetHash(ref HashCode hash)
    {
        bool valid = NamespaceSetIndex >= 0 &&
            NamespaceSetIndex < Pool.NamespaceSets.Count;
        hash.Add(valid);
        if (valid)
            hash.Add(Pool.NamespaceSets[NamespaceSetIndex]);
        else
            hash.Add(NamespaceSetIndex);
    }

    bool SameNamespace(ASMultiname other)
    {
        bool left_valid = NamespaceIndex >= 0 &&
            NamespaceIndex < Pool.Namespaces.Count;
        bool right_valid = other.NamespaceIndex >= 0 &&
            other.NamespaceIndex < other.Pool.Namespaces.Count;
        if (left_valid != right_valid)
            return false;
        return left_valid
            ? Pool.Namespaces[NamespaceIndex] ==
                other.Pool.Namespaces[other.NamespaceIndex]
            : NamespaceIndex == other.NamespaceIndex;
    }

    bool SameNamespaceSet(ASMultiname other)
    {
        bool left_valid = NamespaceSetIndex >= 0 &&
            NamespaceSetIndex < Pool.NamespaceSets.Count;
        bool right_valid = other.NamespaceSetIndex >= 0 &&
            other.NamespaceSetIndex < other.Pool.NamespaceSets.Count;
        if (left_valid != right_valid)
            return false;
        return left_valid
            ? Pool.NamespaceSets[NamespaceSetIndex] ==
                other.Pool.NamespaceSets[other.NamespaceSetIndex]
            : NamespaceSetIndex == other.NamespaceSetIndex;
    }

    bool SameTypeName(ASMultiname other)
    {
        if (!SameMultinameReference(QNameIndex, other.QNameIndex, other))
            return false;
        if (TypeIndices.Count != other.TypeIndices.Count)
            return false;
        for (int index = 0; index < TypeIndices.Count; index++)
        {
            if (!SameMultinameReference(
                    TypeIndices[index],
                    other.TypeIndices[index],
                    other))
            {
                return false;
            }
        }
        return true;
    }

    bool SameMultinameReference(
        int left_index,
        int right_index,
        ASMultiname other)
    {
        bool left_valid = left_index >= 0 &&
            left_index < Pool.Multinames.Count;
        bool right_valid = right_index >= 0 &&
            right_index < other.Pool.Multinames.Count;
        if (left_valid != right_valid)
            return false;
        return left_valid
            ? Pool.Multinames[left_index] ==
                other.Pool.Multinames[right_index]
            : left_index == right_index;
    }
    public override bool Equals(object? obj)
        => obj is ASMultiname multiname && Equals(multiname);

    public static bool operator ==(ASMultiname? left, ASMultiname? right)
    {
        return EqualityComparer<ASMultiname>.Default.Equals(left, right);
    }
    public static bool operator !=(ASMultiname? left, ASMultiname? right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        string prefix = string.Empty;
        if (Kind is MultinameKind.QName or MultinameKind.QNameA)
        {
            string namespace_name = NamespaceIndex switch
            {
                0 => "*",
                _ when NamespaceIndex > 0 &&
                    NamespaceIndex < Pool.Namespaces.Count =>
                    Pool.Namespaces[NamespaceIndex]?.RuntimeName ??
                    "<invalid>",
                _ => "<invalid>"
            };
            prefix = $"{namespace_name}.";
        }
        return $"{Kind}: \"{prefix}{RuntimeName}\"";
    }
}
