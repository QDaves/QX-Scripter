using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Headers.Flash;

public sealed class SignatureDatabase
{
    const string ResourceName = "Qx.Headers.Flash.signatures.json";
    const string DefaultAlgorithm = "avm2-structural-v3";

    readonly Dictionary<string, string> _names;
    readonly HashSet<string> _ambiguous;
    readonly string _algorithm;
    readonly byte[] _canonical_json;
    readonly byte[] _source_json;

    SignatureDatabase(
        Dictionary<string, string> names,
        HashSet<string> ambiguous,
        string algorithm,
        byte[]? source_json)
    {
        _names = names;
        _ambiguous = ambiguous;
        _algorithm = algorithm;
        _canonical_json = CreateCanonicalJson(algorithm, names, ambiguous);
        _source_json = source_json?.ToArray() ?? _canonical_json.ToArray();
        CatalogSha256 = Convert.ToHexStringLower(SHA256.HashData(_canonical_json));
        SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(_source_json));
    }

    public int Count => _names.Count;
    public int AmbiguousCount => _ambiguous.Count;
    public string CatalogSha256 { get; }
    public string SourceSha256 { get; }

    public byte[] ExportCanonicalJson() => _canonical_json.ToArray();
    public byte[] ExportSourceJson() => _source_json.ToArray();

    public bool TryResolve(string signature, out string? name)
    {
        name = null;
        if (_ambiguous.Contains(signature)) return false;
        return _names.TryGetValue(signature, out name);
    }

    public static SignatureDatabase Empty() => new(new(), new(), DefaultAlgorithm, null);

    public static SignatureDatabase Load(string path)
    {
        byte[] source = File.ReadAllBytes(path);
        SignatureFile file = JsonSerializer.Deserialize(source, SignatureJsonContext.Default.SignatureFile)
            ?? new SignatureFile();
        return Create(file, source);
    }

    public static SignatureDatabase LoadDefault()
    {
        using Stream stream = typeof(SignatureDatabase).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        byte[] source = NormalizeDefaultSource(content.ToArray());
        SignatureFile file = JsonSerializer.Deserialize(source, SignatureJsonContext.Default.SignatureFile)
            ?? new SignatureFile();
        return Create(file, source);
    }

    static SignatureDatabase Create(SignatureFile file, byte[]? source_json)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.Algorithm);
        return new SignatureDatabase(
            new Dictionary<string, string>(file.Signatures, StringComparer.Ordinal),
            new HashSet<string>(file.Ambiguous, StringComparer.Ordinal),
            file.Algorithm,
            source_json);
    }

    public void Save(string path)
    {
        string? parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (parent is not null)
            Directory.CreateDirectory(parent);
        var file = new SignatureFile
        {
            Algorithm = _algorithm,
            Signatures = _names
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
            Ambiguous = _ambiguous.OrderBy(value => value, StringComparer.Ordinal).ToList()
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(file, options));
    }

    public static SignatureDatabase Seed(IEnumerable<(string signature, string name)> entries)
    {
        var names = new Dictionary<string, string>();
        var ambiguous = new HashSet<string>();
        AddEntries(names, ambiguous, entries);
        return new SignatureDatabase(names, ambiguous, DefaultAlgorithm, null);
    }

    public SignatureDatabase Extend(IEnumerable<(string signature, string name)> entries)
    {
        var names = new Dictionary<string, string>(_names, StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(_ambiguous, StringComparer.Ordinal);
        AddEntries(names, ambiguous, entries);
        return new SignatureDatabase(names, ambiguous, _algorithm, null);
    }

    static void AddEntries(
        Dictionary<string, string> names,
        HashSet<string> ambiguous,
        IEnumerable<(string signature, string name)> entries)
    {
        foreach ((string signature, string name) in entries)
        {
            if (ambiguous.Contains(signature)) continue;
            if (names.TryGetValue(signature, out string? existing))
            {
                if (existing != name)
                {
                    names.Remove(signature);
                    ambiguous.Add(signature);
                }
            }
            else
            {
                names[signature] = name;
            }
        }
    }

    static byte[] CreateCanonicalJson(
        string algorithm,
        IReadOnlyDictionary<string, string> names,
        IReadOnlySet<string> ambiguous)
    {
        using var content = new MemoryStream();
        using (var writer = new Utf8JsonWriter(content))
        {
            writer.WriteStartObject();
            writer.WriteString("algorithm", algorithm);
            writer.WritePropertyName("signatures");
            writer.WriteStartObject();
            foreach ((string signature, string name) in names.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                writer.WriteString(signature, name);
            writer.WriteEndObject();
            writer.WritePropertyName("ambiguous");
            writer.WriteStartArray();
            foreach (string signature in ambiguous.Order(StringComparer.Ordinal))
                writer.WriteStringValue(signature);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return content.ToArray();
    }

    static byte[] NormalizeDefaultSource(byte[] source)
    {
        if (!source.Contains((byte)'\r'))
            return source;
        byte[] normalized = new byte[source.Length];
        int written = 0;
        for (int index = 0; index < source.Length; index++)
        {
            byte value = source[index];
            if (value == (byte)'\r')
            {
                if (index + 1 < source.Length && source[index + 1] == (byte)'\n')
                    index++;
                normalized[written++] = (byte)'\n';
                continue;
            }
            normalized[written++] = value;
        }
        return normalized.AsSpan(0, written).ToArray();
    }
}

public sealed class SignatureFile
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "avm2-structural-v3";

    [JsonPropertyName("signatures")]
    public Dictionary<string, string> Signatures { get; set; } = new();

    [JsonPropertyName("ambiguous")]
    public List<string> Ambiguous { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SignatureFile))]
internal partial class SignatureJsonContext : JsonSerializerContext;
