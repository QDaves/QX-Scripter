using System.Security.Cryptography;
using System.Text;
using Qx;

namespace Qx.Protocol;

public readonly struct MessageKey : IComparable<MessageKey>, IEquatable<MessageKey>
{
    private readonly string? _value;

    public MessageKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToLowerInvariant();
        if (!IsValid(normalized))
            throw new ArgumentException($"'{value}' is not a valid message key.", nameof(value));
        _value = normalized;
    }

    public string Value => _value ?? string.Empty;

    public bool IsEmpty => _value is null;

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

    public int CompareTo(MessageKey other) =>
        StringComparer.Ordinal.Compare(Value, other.Value);

    public bool Equals(MessageKey other) =>
        StringComparer.Ordinal.Equals(Value, other.Value);

    public override bool Equals(object? obj) =>
        obj is MessageKey other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(MessageKey left, MessageKey right) => left.Equals(right);

    public static bool operator !=(MessageKey left, MessageKey right) => !left.Equals(right);

    internal static MessageKey Legacy(Direction direction, string identity, int occurrence)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        string suffix = Convert.ToHexStringLower(digest);
        string direction_name = direction == Direction.In ? "in" : "out";
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
