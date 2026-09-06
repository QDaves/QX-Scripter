using System.Collections;

namespace Qx.Game;

public sealed class ExternalTexts : IReadOnlyDictionary<string, string>
{
    private readonly Dictionary<string, string> _entries = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _entries.Count;

    public string? this[string key] => _entries.GetValueOrDefault(key);

    string IReadOnlyDictionary<string, string>.this[string key] => _entries[key];

    public IEnumerable<string> Keys => _entries.Keys;

    public IEnumerable<string> Values => _entries.Values;

    public bool ContainsKey(string key) => _entries.ContainsKey(key);

    public bool TryGet(string key, out string value) => _entries.TryGetValue(key, out value!);

    public bool TryGetValue(string key, out string value) =>
        _entries.TryGetValue(key, out value!);

    public string? BadgeName(string code) => _entries.GetValueOrDefault($"badge_name_{code}");

    public string? EffectName(int id) => _entries.GetValueOrDefault($"fx_{id}");

    public string? HandItemName(int id) => _entries.GetValueOrDefault($"handitem{id}");

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
        _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static ExternalTexts Load(string content)
    {
        var texts = new ExternalTexts();
        foreach (string line in content.Split('\n'))
        {
            int split = line.IndexOf('=');
            if (split <= 0)
                continue;
            string key = line[..split].Trim();
            string value = line[(split + 1)..].TrimEnd('\r');
            if (key.Length > 0)
                texts._entries.TryAdd(key, value);
        }
        return texts;
    }
}
