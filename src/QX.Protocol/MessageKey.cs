using System.Security.Cryptography;
using System.Text;
using Qx;

namespace Qx.Protocol;

/// <summary>Represents a stable, client-independent message key such as <c>room.chat.talk</c>.</summary>
/// <remarks>
/// A key contains only <c>a</c> to <c>z</c>, <c>0</c> to <c>9</c>, <c>.</c>, <c>_</c> and <c>-</c>,
/// with no leading, trailing or repeated dot. The <see langword="default"/> value is empty. See
/// <see cref="MessageKeys"/> for the declared keys.
/// </remarks>
public readonly struct MessageKey : IComparable<MessageKey>, IEquatable<MessageKey>
{
    private readonly string? _value;

    /// <summary>Initializes a new message key from a string, which is trimmed and lower-cased.</summary>
    /// <param name="value">The key text.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is blank or not a valid key.</exception>
    public MessageKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToLowerInvariant();
        if (!IsValid(normalized))
            throw new ArgumentException($"'{value}' is not a valid message key.", nameof(value));
        _value = normalized;
    }

    /// <summary>Gets the key text, or an empty string for an empty key.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Gets whether the key is empty, which is the case for the <see langword="default"/> value.</summary>
    public bool IsEmpty => _value is null;

    /// <summary>Tries to parse a string into a message key.</summary>
    /// <param name="value">The key text, which is trimmed and lower-cased, or <see langword="null"/>.</param>
    /// <param name="key">The parsed key, or an empty key when parsing fails.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is a valid key; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out MessageKey key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            key = default;
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (!IsValid(normalized))
        {
            key = default;
            return false;
        }

        key = new MessageKey(normalized);
        return true;
    }

    /// <summary>Compares this key with another key by ordinal text order.</summary>
    /// <param name="other">The key to compare with.</param>
    /// <returns>A negative number, zero or a positive number when this key sorts before, with or after <paramref name="other"/>.</returns>
    public int CompareTo(MessageKey other) =>
        StringComparer.Ordinal.Compare(Value, other.Value);

    /// <summary>Gets whether this key has the same text as another key.</summary>
    /// <param name="other">The key to compare with.</param>
    public bool Equals(MessageKey other) =>
        StringComparer.Ordinal.Equals(Value, other.Value);

    /// <summary>Gets whether an object is a <see cref="MessageKey"/> with the same text.</summary>
    /// <param name="obj">The object to compare with.</param>
    public override bool Equals(object? obj) =>
        obj is MessageKey other && Equals(other);

    /// <summary>Returns a hash code of the key text.</summary>
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>Returns the key text.</summary>
    public override string ToString() => Value;

    /// <summary>Gets whether two keys have the same text.</summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    public static bool operator ==(MessageKey left, MessageKey right) => left.Equals(right);

    /// <summary>Gets whether two keys have different text.</summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    public static bool operator !=(MessageKey left, MessageKey right) => !left.Equals(right);

    internal static MessageKey Legacy(MessageDirection direction, string identity, int occurrence)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        string suffix = Convert.ToHexStringLower(digest);
        string direction_name = direction == MessageDirection.In ? "in" : "out";
        string duplicate = occurrence > 1 ? $".{occurrence}" : string.Empty;
        return new MessageKey($"legacy.{direction_name}.{suffix}{duplicate}");
    }

    private static bool IsValid(string value)
    {
        if (value.Length == 0 || value[0] == '.' || value[^1] == '.' || value.Contains("..", StringComparison.Ordinal))
            return false;

        foreach (char character in value)
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')
                continue;
            return false;
        }

        return true;
    }
}
