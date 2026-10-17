using System.IO.Compression;
using System.Text.Json;

namespace Qx.Hosting;

internal static class WiredKnowledge
{
    const string Prefix = "mcp_knowledge/";
    const string Entry = Prefix + "START.json";
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static readonly Lazy<IReadOnlyDictionary<string, Document>> Documents = new(Load);

    public static string Read(string path, string pointer, int offset, int limit)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (pointer.Length != 0 || offset != 0)
                throw new ArgumentException("Supply a document path when using a pointer or offset.");
            return Overview();
        }
        string name = Normalize(path);
        if (!Documents.Value.TryGetValue(name, out Document? document))
            throw new ArgumentException($"Unknown knowledge document '{name}'. Use list_wired_knowledge or search_wired_knowledge to find its path.");
        string text = document.Text.Value;
        if (pointer.Length > 0)
        {
            using JsonDocument parsed = JsonDocument.Parse(text);
            text = JsonSerializer.Serialize(Select(parsed.RootElement, pointer), Json);
        }
        if (offset < 0 || offset > text.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), $"Offset must be between 0 and {text.Length}.");
        int end = offset + Math.Min(text.Length - offset, Math.Clamp(limit, 1, 32000));
        if (end < text.Length && end > offset && char.IsHighSurrogate(text[end - 1]))
            end++;
        if (offset > 0 && offset < text.Length && char.IsLowSurrogate(text[offset]))
            throw new ArgumentException("The offset splits a Unicode character. Use the returned next_offset.");
        return Serialize(new
        {
            path = document.Path,
            pointer,
            offset,
            total_characters = text.Length,
            next_offset = end < text.Length ? (int?)end : null,
            complete = end == text.Length,
            content = text[offset..end]
        });
    }

    public static string List(string path, int offset, int limit)
    {
        string prefix = string.IsNullOrWhiteSpace(path) ? Prefix : Normalize(path);
        Document[] matches = [.. Documents.Value.Values.Where(document => document.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(document => document.Path, StringComparer.Ordinal)];
        var selected = matches.Skip(offset).Take(limit).Select(document => new { path = document.Path, title = document.Title, characters = document.Text.Value.Length }).ToArray();
        return Serialize(new { path = prefix, offset, total = matches.Length, next_offset = offset + selected.Length < matches.Length ? (int?)(offset + selected.Length) : null, documents = selected });
    }

    public static string Search(string query, string path, int offset, int limit)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 512)
            throw new ArgumentException("Supply a search query between 1 and 512 characters.");
        string prefix = string.IsNullOrWhiteSpace(path) ? Prefix : Normalize(path);
        string[] terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var matches = Documents.Value.Values.Where(document => document.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(document => new { document, rank = Rank(document, terms) })
            .Where(hit => hit.rank > 0).OrderByDescending(hit => hit.rank).ThenBy(hit => hit.document.Path, StringComparer.Ordinal).ToArray();
        var selected = matches.Skip(offset).Take(limit).Select(hit => new
        {
            path = hit.document.Path,
            title = hit.document.Title,
            excerpts = terms.Select(term => Excerpt(hit.document.Text.Value, term)).Where(text => text.Length > 0).Distinct().Take(3).ToArray()
        }).ToArray();
        return Serialize(new { query, path = prefix, offset, total = matches.Length, next_offset = offset + selected.Length < matches.Length ? (int?)(offset + selected.Length) : null, documents = selected });
    }

    static string Overview()
    {
        using JsonDocument parsed = JsonDocument.Parse(Documents.Value[Entry].Text.Value);
        JsonElement root = parsed.RootElement;
        return Serialize(new
        {
            entrypoint = Entry,
            purpose = root.GetProperty("purpose"),
            defaults = root.GetProperty("defaults"),
            scope = root.GetProperty("scope"),
            read_first = root.GetProperty("read_first"),
            access = root.GetProperty("access"),
            indexes = root.GetProperty("find").EnumerateObject().Take(15).ToDictionary(item => item.Name, item => item.Value),
            document_count = Documents.Value.Count,
            more_routes = "Read mcp_knowledge/START.json for every route, or search_wired_knowledge for a class, rule, variable or gameplay pattern. Follow dependency IDs through index/ids.json."
        });
    }

    static int Rank(Document document, string[] terms)
    {
        int rank = 0;
        foreach (string term in terms)
        {
            if (document.Path.Contains(term, StringComparison.OrdinalIgnoreCase)) rank += 20;
            else if (document.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) rank += 10;
            else if (document.Text.Value.Contains(term, StringComparison.OrdinalIgnoreCase)) rank++;
            else return 0;
        }
        return rank;
    }

    static string Excerpt(string text, string term)
    {
        int found = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (found < 0) return "";
        int start = Math.Max(0, found - 70);
        int length = Math.Min(text.Length - start, Math.Max(220, term.Length));
        return text.Substring(start, length).Replace('\n', ' ').Replace('\r', ' ');
    }

    static JsonElement Select(JsonElement value, string pointer)
    {
        if (!pointer.StartsWith('/'))
            throw new ArgumentException("A JSON pointer must start with '/'.");
        foreach (string encoded in pointer[1..].Split('/'))
        {
            for (int index = 0; index < encoded.Length; index++)
                if (encoded[index] == '~' && (index + 1 == encoded.Length || encoded[++index] is not ('0' or '1')))
                    throw new ArgumentException("Invalid JSON pointer escape.");
            string part = encoded.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(part, out JsonElement child)) value = child;
            else if (value.ValueKind == JsonValueKind.Array && int.TryParse(part, out int index) && index >= 0 && index < value.GetArrayLength()) value = value[index];
            else throw new ArgumentException($"JSON pointer '{pointer}' does not exist.");
        }
        return value;
    }

    static string Normalize(string path)
    {
        string name = path.Trim().Replace('\\', '/');
        if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(part => part is ".." or "."))
            throw new ArgumentException("Use a package-relative knowledge path.");
        return name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? name : Prefix + name;
    }

    static IReadOnlyDictionary<string, Document> Load()
    {
        using Stream source = typeof(WiredKnowledge).Assembly.GetManifestResourceStream("Qx.WiredKnowledge.zip")
            ?? throw new InvalidOperationException("The bundled Wired knowledge is missing.");
        using var archive = new ZipArchive(source, ZipArchiveMode.Read);
        using Stream manifest_stream = archive.GetEntry("manifest.json")?.Open()
            ?? throw new InvalidOperationException("The Wired knowledge manifest is missing.");
        using JsonDocument manifest = JsonDocument.Parse(manifest_stream);
        var result = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement row in manifest.RootElement.GetProperty("documents").EnumerateArray())
        {
            string path = Normalize(row.GetProperty("path").GetString()!);
            string title = row.GetProperty("title").GetString()!;
            ZipArchiveEntry entry = archive.GetEntry(path) ?? throw new InvalidOperationException($"Knowledge document '{path}' is missing.");
            using var reader = new StreamReader(entry.Open());
            string text = reader.ReadToEnd();
            result.Add(path, new Document(path, title, new Lazy<string>(() =>
            {
                using JsonDocument document = JsonDocument.Parse(text);
                return JsonSerializer.Serialize(document.RootElement, Json);
            })));
        }
        return result;
    }

    static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    sealed record Document(string Path, string Title, Lazy<string> Text);
}
