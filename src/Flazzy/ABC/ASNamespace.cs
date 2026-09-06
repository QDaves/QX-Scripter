using System.Diagnostics;

using System.Runtime.CompilerServices;

using Flazzy.IO;

namespace Flazzy.ABC;

/// <summary>
/// Represents a namespace in the bytecode.
/// </summary>
[DebuggerDisplay("{Kind}: \"{RuntimeName}\"")]
public class ASNamespace : IFlashItem, IEquatable<ASNamespace>, IPoolConstant
{
    public ASConstantPool Pool { get; init; }

    /// <summary>
    /// Gets or sets the index of the string in <see cref="ASConstantPool.Strings"/> representing the namespace name.
    /// </summary>
    public int NameIndex { get; set; }

    /// <summary>
    /// Gets the name of the namespace.
    /// </summary>
    public string? Name => Pool.Strings[NameIndex];

    bool HasName =>
        NameIndex >= 0 &&
        NameIndex < Pool.Strings.Count &&
        Pool.Strings[NameIndex] is not null;
    public string RuntimeName => NameIndex == 0
        ? Kind == NamespaceKind.Private
            ? string.Empty
            : "undefined"
        : HasName
            ? Pool.Strings[NameIndex] ?? "<invalid>"
            : "<invalid>";

    public bool IsPublicRoot =>
        NameIndex > 0 &&
        HasName &&
        Kind is NamespaceKind.Package or NamespaceKind.Namespace &&
        string.Equals(RuntimeName, string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Gets or sets the kind of namespace this entry should be interpreted as by the loader.
    /// </summary>
    public NamespaceKind Kind { get; set; }

    public ASNamespace(ASConstantPool pool)
    {
        Pool = pool;
    }
    public ASNamespace(ASConstantPool pool, ref SpanFlashReader input)
        : this(pool)
    {
        Kind = (NamespaceKind)input.ReadByte();
        if (!Enum.IsDefined(typeof(NamespaceKind), Kind))
        {
            throw new InvalidCastException($"Invalid namespace kind for value {Kind:0x00}.");
        }
        NameIndex = input.ReadEncodedInt();
    }

    public string GetAS3Modifiers() => Kind switch
    {
        NamespaceKind.Package => "public",
        NamespaceKind.Private => "private",
        NamespaceKind.Explicit => "explicit",
        NamespaceKind.StaticProtected or NamespaceKind.Protected => "protected",
        _ => string.Empty,
    };

    public int GetSize() => sizeof(byte) + SpanFlashWriter.GetEncodedIntSize(NameIndex);
    public void WriteTo(ref SpanFlashWriter output)
    {
        output.Write((byte)Kind);
        output.WriteEncodedInt(NameIndex);
    }

    public override int GetHashCode() =>
        Kind == NamespaceKind.Private
            ? RuntimeHelpers.GetHashCode(this)
            : NameIndex == 0
                ? HashCode.Combine(Kind, true, RuntimeName)
                : HasName
                    ? HashCode.Combine(
                        Kind,
                        false,
                        RuntimeName)
                    : HashCode.Combine(
                        Kind,
                        false,
                        NameIndex);
    public bool Equals(ASNamespace? other)
    {
        if (other == null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Kind != other.Kind) return false;
        if (Kind == NamespaceKind.Private) return false;
        if (NameIndex == 0 || other.NameIndex == 0)
        {
            return NameIndex == 0 && other.NameIndex == 0;
        }
        if (HasName != other.HasName)
            return false;
        return HasName
            ? string.Equals(
                RuntimeName,
                other.RuntimeName,
                StringComparison.Ordinal)
            : NameIndex == other.NameIndex;
    }
    public override bool Equals(object? obj)
    {
        return Equals(obj as ASNamespace);
    }

    public static bool operator ==(ASNamespace? left, ASNamespace? right)
    {
        return EqualityComparer<ASNamespace>.Default.Equals(left, right);
    }
    public static bool operator !=(ASNamespace? left, ASNamespace? right)
    {
        return !(left == right);
    }

    public override string ToString() => $"{Kind}: \"{RuntimeName}\"";
}
