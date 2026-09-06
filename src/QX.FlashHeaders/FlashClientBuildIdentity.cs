namespace Qx.Headers.Flash;

public static class FlashClientBuildIdentity
{
    const string Prefix = "WIN63-";
    const int TimestampLength = 12;

    public static IReadOnlyList<string> FromAbcConstants(SwfInfo swf)
    {
        ArgumentNullException.ThrowIfNull(swf);
        return FromConstants(swf.AbcFiles.SelectMany(abc => abc.Pool.Strings));
    }

    public static IReadOnlyList<string> FromConstants(IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values
            .Where(IsBuildId)
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }

    static bool IsBuildId(string? value)
    {
        if (value is null ||
            value.Length <= Prefix.Length + TimestampLength + 1 ||
            !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }
        ReadOnlySpan<char> body = value.AsSpan(Prefix.Length);
        return body.Length > TimestampLength + 1 &&
            body[TimestampLength] == '-' &&
            Digits(body[..TimestampLength]) &&
            Digits(body[(TimestampLength + 1)..]);
    }

    static bool Digits(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
            return false;
        foreach (char character in value)
        {
            if (character is < '0' or > '9')
                return false;
        }
        return true;
    }
}
