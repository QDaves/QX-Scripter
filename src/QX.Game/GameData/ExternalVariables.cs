using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Qx.Game;

/// <summary>
/// The hotel's <c>external_variables</c> configuration, which drives large parts of client
/// behaviour that never appears on the wire: feature switches, limits, prices and endpoint URLs.
/// </summary>
/// <remarks>
/// <para>
/// The lookups reproduce <c>HabboConfigurationManager</c> rather than a plain dictionary read,
/// because the file is not a flat map. Around one entry in seven contains a <c>${other.key}</c>
/// reference, so reading the backing store directly hands back the placeholder instead of the
/// value the client would have used.
/// </para>
/// <para>
/// The three accessors deliberately differ from one another exactly as they do in the client:
    /// only <see cref="Get(string)"/> interpolates and rewrites URLs, while <see cref="Flag"/> and
/// <see cref="Number"/> read the raw entry.
/// </para>
/// </remarks>
public sealed partial class ExternalVariables : IReadOnlyDictionary<string, string>
{
    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _readonly_keys = new(StringComparer.Ordinal);

    [GeneratedRegex(@"\n\r+|\n+|\r+")]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"\$\{([^}]*)\}")]
    private static partial Regex Placeholder();

    /// <summary>Whether the values are served over a secure connection.</summary>
    /// <remarks>
    /// The client tracks this to upgrade the protocol of every URL it hands out. Habbo is served
    /// over TLS, so this defaults to <see langword="true"/> and only exists to keep
    /// <see cref="Get(string)"/> faithful.
    /// </remarks>
    public bool IsSecure { get; init; } = true;

    public int Count => _entries.Count;

    public IEnumerable<string> Keys => _entries.Keys;

    public IEnumerable<string> Values => _entries.Values;

    /// <summary>The raw, uninterpolated entry, or <see langword="null"/> when absent.</summary>
    /// <remarks>Prefer <see cref="Get(string)"/>, which resolves <c>${...}</c> references.</remarks>
    public string? this[string key] => _entries.GetValueOrDefault(key);

    string IReadOnlyDictionary<string, string>.this[string key] => _entries[key];

    public bool ContainsKey(string key) => _entries.ContainsKey(key);

    public bool TryGetValue(string key, out string value) => _entries.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Whether an entry was declared while the file was marked read-only, which is the hotel's way
    /// of saying a later source must not override it.
    /// </summary>
    /// <param name="key">The key to check.</param>
    public bool IsReadOnly(string key) => _readonly_keys.Contains(key);

    /// <summary>
    /// The value of a key the way the client would use it, with <c>${...}</c> references resolved
    /// and URLs normalised.
    /// </summary>
    /// <remarks>
    /// Returns an empty string for a missing key and for one whose interpolation could not be
    /// completed, which is what the client does — it has no separate "not configured" state.
    /// </remarks>
    /// <param name="key">The key to read.</param>
    public string Get(string key) => Get(key, active: null);

    private string Get(string key, HashSet<string>? active)
    {
        string? value = Interpolate(_entries.GetValueOrDefault(key), key, active);
        if (value is null)
            return "";
        if (value.StartsWith("//", StringComparison.Ordinal))
            value = (IsSecure ? "https:" : "http:") + value;
        return UpdateUrlProtocol(value);
    }

    /// <summary>
    /// The value of a key with its <c>%name%</c> markers filled in from a caller-supplied table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These markers are a separate mechanism from the <c>${...}</c> references <see cref="Get(string)"/>
    /// resolves: those name other configuration keys, while these name values the caller supplies,
    /// and the two are not interchangeable. Templated endpoint URLs use the marker form.
    /// </para>
    /// <para>
    /// The client replaces at most ten markers per value and leaves the rest standing. It also
    /// writes the literal text <c>undefined</c> where a marker has no matching entry, which is not
    /// reproduced here: an unsupplied marker is left untouched so the caller can see which one it
    /// missed instead of shipping the word into a URL.
    /// </para>
    /// </remarks>
    /// <param name="key">The key to read.</param>
    /// <param name="parameters">The replacements, keyed without the surrounding percent signs.</param>
    public string Get(string key, IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        string value = Get(key);
        int searchFrom = 0;
        for (int replacements = 0; replacements < 10; replacements++)
        {
            int open = value.IndexOf('%', searchFrom);
            if (open < 0)
                break;
            int close = value.IndexOf('%', open + 1);
            if (close < 0)
                break;

            string name = value[(open + 1)..close];
            if (!parameters.TryGetValue(name, out string? replacement))
            {
                searchFrom = close;
                continue;
            }

            value = string.Concat(value.AsSpan(0, open), replacement, value.AsSpan(close + 1));
            searchFrom = open + replacement.Length;
        }
        return value;
    }

    /// <summary>
    /// A configuration switch. Only <c>1</c> and <c>true</c> in any casing are true; anything else,
    /// including a missing key, is false.
    /// </summary>
    /// <param name="key">The key to read.</param>
    public bool Flag(string key)
    {
        if (!_entries.TryGetValue(key, out string? value))
            return false;
        return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A numeric setting, falling back to <paramref name="fallback"/> only when the key is absent.
    /// </summary>
    /// <remarks>
    /// A present but unparsable value yields zero rather than the fallback. That is not a
    /// convenience decision: the client casts with ActionScript's <c>int()</c>, which runs the
    /// string through <c>Number()</c> and turns the resulting <c>NaN</c> into zero. A limit that
    /// the hotel misconfigures therefore reads as zero in the client, and code that mirrors the
    /// client has to see the same zero to agree with it.
    /// </remarks>
    /// <param name="key">The key to read.</param>
    /// <param name="fallback">The value to use when the key is not configured.</param>
    public int Number(string key, int fallback = 0)
    {
        if (!_entries.TryGetValue(key, out string? value))
            return fallback;
        return ToInt(value);
    }

    /// <summary>
    /// A comma separated list, with empty elements dropped and the rest trimmed.
    /// </summary>
    /// <param name="key">The key to read.</param>
    public IReadOnlyList<string> List(string key)
    {
        string value = Get(key);
        if (value.Length == 0)
            return [];
        return value
            .Split(',')
            .Select(entry => entry.Trim())
            .Where(entry => entry.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// Reads a configuration file in the hotel's line based format.
    /// </summary>
    /// <remarks>
    /// Blank lines and lines opening with <c>#</c> are comments. A line without an <c>=</c>, or one
    /// whose key or value side is empty before trimming, is skipped exactly as the client skips it;
    /// this is why <c>key=</c> does not clear a key. Everything after the first <c>=</c> belongs to
    /// the value, so URLs carrying query strings survive intact. Once
    /// <c>configuration.readonly=true</c> appears, it and every later entry in the same file are
    /// recorded as protected.
    /// </remarks>
    /// <param name="content">The file contents.</param>
    /// <param name="isSecure">Whether the session is served over TLS. See <see cref="IsSecure"/>.</param>
    /// <param name="arguments">
    /// Values the embedding page would have supplied, most importantly <c>url.prefix</c>. They are
    /// applied after the file and lose to any key the file marked read-only, which is the order and
    /// the precedence the client uses. Around thirty published URLs are written as
    /// <c>${url.prefix}/…</c> and resolve to nothing without it.
    /// </param>
    public static ExternalVariables Load(
        string content,
        bool isSecure = true,
        IReadOnlyDictionary<string, string>? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var variables = new ExternalVariables { IsSecure = isSecure };
        bool locked = false;

        foreach (string line in LineBreaks().Split(content))
        {
            if (line.Length == 0 || line[0] == '#')
                continue;

            int split = line.IndexOf('=');
            if (split <= 0 || split == line.Length - 1)
                continue;

            string key = line[..split].Trim();
            string value = line[(split + 1)..].Trim();
            if (key.Length == 0)
                continue;

            if (key == "configuration.readonly" && value == "true")
                locked = true;

            variables._entries[key] = value;
            if (locked)
                variables._readonly_keys.Add(key);
        }

        if (arguments is not null)
        {
            foreach ((string key, string value) in arguments)
            {
                if (key.Length > 0 && !variables._readonly_keys.Contains(key))
                    variables._entries[key] = value;
            }
        }

        return variables;
    }

    private string? Interpolate(string? value, string? key, HashSet<string>? active)
    {
        if (value is null)
            return null;

        // A reference chain that loops back on itself would recurse until the stack runs out, which
        // is what the client does with such a file. Treating a key already being resolved as
        // unresolvable ends the chain at the same place the client's own three-pass bound would
        // have left it: with no value.
        HashSet<string> resolving = active ?? new HashSet<string>(StringComparer.Ordinal);
        if (key is not null && !resolving.Add(key))
            return null;

        try
        {
            string current = value;
            // The client gives up after three passes, which bounds how deep a chain it will follow.
            // Matching that bound matters: a value the client leaves half-resolved must not resolve
            // here, or the two disagree about the same key.
            for (int pass = 0; pass < 3; pass++)
            {
                bool unresolved = false;
                string replaced = Placeholder().Replace(current, match =>
                {
                    string reference = match.Groups[1].Value;
                    if (!_entries.ContainsKey(reference) || resolving.Contains(reference))
                    {
                        unresolved = true;
                        return match.Value;
                    }
                    return Get(reference, resolving);
                });

                if (unresolved)
                    return null;
                if (replaced == current)
                    break;
                current = replaced;
            }

            return current;
        }
        finally
        {
            if (key is not null)
                resolving.Remove(key);
        }
    }

    private string UpdateUrlProtocol(string value)
    {
        if (!IsSecure)
            return value;
        return value
            .Replace("http://", "https://", StringComparison.Ordinal)
            .Replace(":8090/", ":8443/", StringComparison.Ordinal);
    }

    private static int ToInt(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0)
            return 0;

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
        {
            return hex;
        }

        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
            double.IsNaN(number))
        {
            return 0;
        }

        double truncated = Math.Truncate(number);
        if (truncated is > int.MaxValue or < int.MinValue)
            return unchecked((int)(long)truncated);
        return (int)truncated;
    }
}
