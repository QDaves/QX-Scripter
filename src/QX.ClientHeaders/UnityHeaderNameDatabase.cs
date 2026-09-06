using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Qx.Unity;

public sealed record UnityHeaderNames(
    string? Name,
    string? FlashName)
{
    public bool IsNamed => Name is not null || FlashName is not null;
}

public sealed class UnityHeaderNameDatabase
{
    const int MaximumCatalogBytes = 16 * 1024 * 1024;
    public const string DefaultSource = "QX/merged-unity-header-catalog";
    public const string DefaultBaseRevision =
        "G-Realm/G-Earth#38:17aa70ebc40000fe8a0cbb2d6cc68d1154e7fb21";
    public const string DefaultHeadRevision =
        "verified-additions:2415:9e7e513d03fb71fd1c1d40818ce9342db2b582eed6d928ff5af364d342bfe7ba;2431:c51f99497a605d7fdfafc3a6d8d835ae5d7aed8e13e237e65ce47d97ad4b061c";

    readonly IReadOnlyDictionary<short, UnityHeaderNames> _incoming;
    readonly IReadOnlyDictionary<short, UnityHeaderNames> _outgoing;
    readonly byte[] _canonical_json;
    readonly byte[] _source_json;

    UnityHeaderNameDatabase(
        IReadOnlyDictionary<short, UnityHeaderNames> incoming,
        IReadOnlyDictionary<short, UnityHeaderNames> outgoing,
        byte[]? source_json,
        string source,
        string? base_revision,
        string? head_revision)
    {
        _incoming = incoming;
        _outgoing = outgoing;
        _canonical_json = CreateCanonicalJson(incoming, outgoing);
        _source_json = source_json?.ToArray() ?? _canonical_json.ToArray();
        CatalogSha256 = Convert.ToHexStringLower(SHA256.HashData(_canonical_json));
        SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(_source_json));
        Source = source;
        BaseRevision = base_revision;
        HeadRevision = head_revision;
    }

    public IReadOnlyDictionary<short, UnityHeaderNames> Incoming => _incoming;
    public IReadOnlyDictionary<short, UnityHeaderNames> Outgoing => _outgoing;
    public string CatalogSha256 { get; }
    public string SourceSha256 { get; }
    public string Source { get; }
    public string? BaseRevision { get; }
    public string? HeadRevision { get; }

    public byte[] ExportCanonicalJson() => _canonical_json.ToArray();
    public byte[] ExportSourceJson() => _source_json.ToArray();

    public static UnityHeaderNameDatabase LoadDefault()
    {
        Assembly assembly = typeof(UnityHeaderNameDatabase).Assembly;
        using Stream stream = assembly.GetManifestResourceStream("QX.Unity.headers.json")
            ?? throw new InvalidOperationException("Embedded Unity header database is missing.");
        return LoadDefault(stream);
    }

    internal static UnityHeaderNameDatabase LoadDefault(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] source = NormalizeDefaultSource(stream);
        using var normalized = new MemoryStream(source, writable: false);
        return Load(
            normalized,
            DefaultSource,
            DefaultBaseRevision,
            DefaultHeadRevision);
    }

    public static UnityHeaderNameDatabase Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return Load(stream, "custom", null, null);
    }

    public static UnityHeaderNameDatabase Create(
        IEnumerable<KeyValuePair<short, string>> incoming,
        IEnumerable<KeyValuePair<short, string>> outgoing) =>
        new(BuildMap(incoming), BuildMap(outgoing), null, "generated", null, null);

    public UnityHeaderNames? Find(UnityHeaderDirection direction, short id) =>
        direction == UnityHeaderDirection.Incoming
            ? _incoming.GetValueOrDefault(id)
            : _outgoing.GetValueOrDefault(id);

    static UnityHeaderNameDatabase Load(
        Stream stream,
        string source,
        string? base_revision,
        string? head_revision)
    {
        if (stream.CanSeek && stream.Length is <= 0 or > MaximumCatalogBytes)
            throw new InvalidDataException("Unity header database has an invalid size.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        if (content.Length is <= 0 or > MaximumCatalogBytes)
            throw new InvalidDataException("Unity header database has an invalid size.");
        byte[] source_json = content.ToArray();
        using JsonDocument document = JsonDocument.Parse(source_json);
        ValidateRoot(document.RootElement);
        return new UnityHeaderNameDatabase(
            ReadEntries(document.RootElement, "Incoming"),
            ReadEntries(document.RootElement, "Outgoing"),
            source_json,
            source,
            base_revision,
            head_revision);
    }

    static byte[] NormalizeDefaultSource(Stream stream)
    {
        if (stream.CanSeek && stream.Length is <= 0 or > MaximumCatalogBytes)
            throw new InvalidDataException("Unity header database has an invalid size.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        if (content.Length is <= 0 or > MaximumCatalogBytes)
            throw new InvalidDataException("Unity header database has an invalid size.");
        byte[] source = content.ToArray();
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

    static IReadOnlyDictionary<short, UnityHeaderNames> ReadEntries(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Unity header database property '{property}' is missing.");

        var values = new Dictionary<short, UnityHeaderNames>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            ValidateEntry(entry, property);
            if (!entry.TryGetProperty("Id", out JsonElement id_value) || !id_value.TryGetInt16(out short id))
                throw new InvalidDataException($"Unity header database contains an invalid {property} ID.");
            string? name = ReadOptionalString(entry, "Name", property, id);
            string? flash_name = ReadOptionalString(entry, "FlashName", property, id);
            if (name is null && flash_name is null)
                throw new InvalidDataException($"Unity header database contains an unnamed {property} entry {id}.");
            if (!values.TryAdd(id, new UnityHeaderNames(name, flash_name)))
                throw new InvalidDataException($"Unity header database contains duplicate {property} ID {id}.");
        }
        return values;
    }

    static void ValidateRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Unity header database root is not an object.");
        var properties = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name is not ("Incoming" or "Outgoing") || !properties.Add(property.Name))
                throw new InvalidDataException($"Unity header database contains invalid property '{property.Name}'.");
        }
        if (!properties.SetEquals(["Incoming", "Outgoing"]))
            throw new InvalidDataException("Unity header database must contain Incoming and Outgoing arrays.");
    }

    static void ValidateEntry(JsonElement entry, string direction)
    {
        if (entry.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Unity header database contains an invalid {direction} entry.");
        var properties = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in entry.EnumerateObject())
        {
            if (property.Name is not ("Id" or "Name" or "FlashName") || !properties.Add(property.Name))
            {
                throw new InvalidDataException(
                    $"Unity header database contains invalid property '{property.Name}' in {direction}.");
            }
        }
        if (!properties.Contains("Id"))
            throw new InvalidDataException($"Unity header database contains a {direction} entry without an ID.");
    }

    static IReadOnlyDictionary<short, UnityHeaderNames> BuildMap(IEnumerable<KeyValuePair<short, string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var values = new Dictionary<short, UnityHeaderNames>();
        foreach ((short id, string name) in entries)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"Header {id} has an empty name.", nameof(entries));
            if (!values.TryAdd(id, new UnityHeaderNames(name, null)))
                throw new ArgumentException($"Header {id} is duplicated.", nameof(entries));
        }
        return values;
    }

    static string? ReadOptionalString(JsonElement entry, string property, string direction, short id)
    {
        if (!entry.TryGetProperty(property, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException(
                $"Unity header database contains an invalid {property} for {direction} entry {id}.");
        string? text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException(
                $"Unity header database contains an empty {property} for {direction} entry {id}.");
        return text;
    }

    static byte[] CreateCanonicalJson(
        IReadOnlyDictionary<short, UnityHeaderNames> incoming,
        IReadOnlyDictionary<short, UnityHeaderNames> outgoing)
    {
        using var content = new MemoryStream();
        using (var writer = new Utf8JsonWriter(content))
        {
            writer.WriteStartObject();
            WriteDirection(writer, "Incoming", incoming);
            WriteDirection(writer, "Outgoing", outgoing);
            writer.WriteEndObject();
        }
        return content.ToArray();
    }

    static void WriteDirection(
        Utf8JsonWriter writer,
        string property,
        IReadOnlyDictionary<short, UnityHeaderNames> values)
    {
        writer.WritePropertyName(property);
        writer.WriteStartArray();
        foreach ((short id, UnityHeaderNames names) in values.OrderBy(pair => pair.Key))
        {
            writer.WriteStartObject();
            writer.WriteNumber("Id", id);
            WriteOptionalString(writer, "Name", names.Name);
            WriteOptionalString(writer, "FlashName", names.FlashName);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    static void WriteOptionalString(
        Utf8JsonWriter writer,
        string property,
        string? value)
    {
        writer.WritePropertyName(property);
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}
