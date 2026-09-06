using Flazzy.IO;

namespace Flazzy.ABC;

public class ASNamespaceSet : IFlashItem, IEquatable<ASNamespaceSet>, IPoolConstant
{
    public ASConstantPool Pool { get; init; }
    public List<int> NamespaceIndices { get; }

    public ASNamespaceSet(ASConstantPool pool)
    {
        Pool = pool;
        NamespaceIndices = new List<int>();
    }
    public ASNamespaceSet(ASConstantPool pool, ref SpanFlashReader input)
        : this(pool)
    {
        NamespaceIndices.Capacity = input.ReadEncodedInt();
        for (int i = 0; i < NamespaceIndices.Capacity; i++)
        {
            NamespaceIndices.Add(input.ReadEncodedInt());
        }
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(NamespaceIndices.Count);
        foreach (int index in NamespaceIndices)
        {
            bool valid = index >= 0 && index < Pool.Namespaces.Count;
            hash.Add(valid);
            if (valid)
                hash.Add(Pool.Namespaces[index]);
            else
                hash.Add(index);
        }
        return hash.ToHashCode();
    }
    public override bool Equals(object? obj)
    {
        return Equals(obj as ASNamespaceSet);
    }
    public bool Equals(ASNamespaceSet? other)
    {
        if (other == null) return false;
        if (!ReferenceEquals(this, other))
        {
            if (NamespaceIndices.Count != other.NamespaceIndices.Count) return false;
            for (int i = 0; i < NamespaceIndices.Count; i++)
            {
                int left_index = NamespaceIndices[i];
                int right_index = other.NamespaceIndices[i];
                bool left_valid = left_index >= 0 &&
                    left_index < Pool.Namespaces.Count;
                bool right_valid = right_index >= 0 &&
                    right_index < other.Pool.Namespaces.Count;
                if (left_valid != right_valid)
                {
                    return false;
                }
                if (!left_valid)
                {
                    if (left_index != right_index)
                        return false;
                    continue;
                }
                if (Pool.Namespaces[left_index] !=
                    other.Pool.Namespaces[right_index])
                    return false;
            }
        }
        return true;
    }

    public IEnumerable<ASNamespace> GetNamespaces()
    {
        for (int i = 0; i < NamespaceIndices.Count; i++)
        {
            yield return Pool.Namespaces[NamespaceIndices[i]] ??
                throw new InvalidDataException($"Namespace constant {NamespaceIndices[i]} is null.");
        }
    }

    public int GetSize()
    {
        int size = 0;
        size += SpanFlashWriter.GetEncodedIntSize(NamespaceIndices.Count);
        for (int i = 0; i < NamespaceIndices.Count; i++)
        {
            size += SpanFlashWriter.GetEncodedIntSize(NamespaceIndices[i]);
        }
        return size;
    }
    public void WriteTo(ref SpanFlashWriter output)
    {
        output.WriteEncodedInt(NamespaceIndices.Count);
        for (int i = 0; i < NamespaceIndices.Count; i++)
        {
            output.WriteEncodedInt(NamespaceIndices[i]);
        }
    }

    public static bool operator ==(ASNamespaceSet? left, ASNamespaceSet? right)
    {
        return EqualityComparer<ASNamespaceSet>.Default.Equals(left, right);
    }
    public static bool operator !=(ASNamespaceSet? left, ASNamespaceSet? right)
    {
        return !(left == right);
    }

    public override string ToString() => $"Namespaces: {NamespaceIndices.Count:n0}";
}
