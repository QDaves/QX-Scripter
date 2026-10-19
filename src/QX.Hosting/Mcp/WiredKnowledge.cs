using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Qx.Hosting;

internal static class WiredKnowledge
{
    const string Resource = "Qx.WiredSkill";
    const string Skill = "SKILL.md";
    const string References = "references/";
    const string Start = References + "START.json";
    const string Manifest = References + "manifest.json";
    static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static readonly Lazy<Package> Bundle = new(Load);

    public static string Read(string path, string pointer, int offset, int limit)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (pointer.Length != 0 || offset != 0)
                throw new ArgumentException("Supply a document path when using a pointer or offset.");
            return Overview();
        }
        string name = Normalize(path);
        if (!Bundle.Value.Documents.TryGetValue(name, out Document? document))
            throw new ArgumentException($"Unknown knowledge document '{name}'. Use list_wired_knowledge or search_wired_knowledge to find its path.");
        string text = document.Text;
        if (pointer.Length > 0)
        {
            if (!document.IsJson)
                throw new ArgumentException($"'{document.Path}' is not a JSON document; read it without a pointer.");
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
        string prefix = string.IsNullOrWhiteSpace(path) ? "" : Normalize(path);
        Document[] matches = [.. Bundle.Value.Documents.Values.Where(document => document.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(document => document.Path, StringComparer.Ordinal)];
        var selected = matches.Skip(offset).Take(limit).Select(document => new { path = document.Path, title = document.Title, characters = document.Text.Length }).ToArray();
        return Serialize(new { skill = Bundle.Value.Name, path = prefix, offset, total = matches.Length, next_offset = offset + selected.Length < matches.Length ? (int?)(offset + selected.Length) : null, documents = selected });
    }

    public static string Search(string query, string path, int offset, int limit)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 512)
            throw new ArgumentException("Supply a search query between 1 and 512 characters.");
        string prefix = string.IsNullOrWhiteSpace(path) ? "" : Normalize(path);
        string[] terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var matches = Bundle.Value.Documents.Values.Where(document => document.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(document => new { document, rank = Rank(document, terms) })
            .Where(hit => hit.rank > 0).OrderByDescending(hit => hit.rank).ThenBy(hit => hit.document.Path, StringComparer.Ordinal).ToArray();
        var selected = matches.Skip(offset).Take(limit).Select(hit => new
        {
            path = hit.document.Path,
            title = hit.document.Title,
            excerpts = terms.Select(term => Excerpt(hit.document.Text, term)).Where(text => text.Length > 0).Distinct().Take(3).ToArray()
        }).ToArray();
        return Serialize(new { query, path = prefix, offset, total = matches.Length, next_offset = offset + selected.Length < matches.Length ? (int?)(offset + selected.Length) : null, documents = selected });
    }

    static string Overview()
    {
        Package package = Bundle.Value;
        using JsonDocument parsed = JsonDocument.Parse(package.Documents[Start].Text);
        JsonElement root = parsed.RootElement;
        return Serialize(new
        {
            skill = package.Name,
            description = package.Description,
            entrypoint = Skill,
            catalog = package.Catalog,
            index = Start,
            purpose = root.GetProperty("purpose"),
            defaults = root.GetProperty("defaults"),
            scope = root.GetProperty("scope"),
            read_first = root.GetProperty("read_first"),
            access = root.GetProperty("access"),
            indexes = root.GetProperty("find").EnumerateObject().Take(15).ToDictionary(item => item.Name, item => item.Value),
            document_count = package.Documents.Count,
            more_routes = $"Read {Skill} for the guide, {package.Catalog ?? Start} for every Wired form by type, {Start} for the machine-readable route table, or search_wired_knowledge for a class, rule, variable or gameplay pattern. Follow dependency IDs through {References}index/ids.json."
        });
    }

    static int Rank(Document document, string[] terms)
    {
        int rank = 0;
        foreach (string term in terms)
        {
            if (document.Path.Contains(term, StringComparison.OrdinalIgnoreCase)) rank += 20;
            else if (document.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) rank += 10;
            else if (document.Text.Contains(term, StringComparison.OrdinalIgnoreCase)) rank++;
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
        return Regex.Replace(text.Substring(start, length), @"\s+", " ");
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
            throw new ArgumentException("Use a skill-relative knowledge path such as references/index/wired.json.");
        string root = Bundle.Value.Name + "/";
        if (name.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            name = name[root.Length..];
        if (name.Equals(Skill, StringComparison.OrdinalIgnoreCase))
            return Skill;
        return name.StartsWith(References, StringComparison.OrdinalIgnoreCase) ? name : References + name;
    }

    static Package Load()
    {
        using Stream source = typeof(WiredKnowledge).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException("The bundled Wired skill is missing.");
        using var archive = new ZipArchive(source, ZipArchiveMode.Read);
        ZipArchiveEntry skill = archive.Entries.SingleOrDefault(entry => entry.FullName.Count(ch => ch == '/') == 1 && entry.FullName.EndsWith("/" + Skill, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The Wired skill must contain exactly one <name>/SKILL.md.");
        string root = skill.FullName[..^Skill.Length];
        string markdown = Text(skill);
        (string name, string description) = Frontmatter(markdown);
        if (!string.Equals(name + "/", root, StringComparison.Ordinal))
            throw new InvalidOperationException($"The skill name '{name}' does not match its folder '{root.TrimEnd('/')}'.");
        using JsonDocument manifest = JsonDocument.Parse(Text(archive.GetEntry(root + Manifest)
            ?? throw new InvalidOperationException("The Wired skill manifest is missing.")));
        var documents = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [Skill] = new(Skill, description, markdown, false)
        };
        foreach (JsonElement row in manifest.RootElement.GetProperty("documents").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            if (!path.StartsWith(References, StringComparison.Ordinal))
                throw new InvalidOperationException($"Knowledge document '{path}' is outside {References}.");
            ZipArchiveEntry entry = archive.GetEntry(root + path) ?? throw new InvalidOperationException($"Knowledge document '{path}' is missing.");
            bool json = !row.TryGetProperty("format", out JsonElement format) || format.GetString() == "json";
            documents.Add(path, new Document(path, row.GetProperty("title").GetString()!, Text(entry), json));
        }
        if (!documents.ContainsKey(Start))
            throw new InvalidOperationException($"The Wired skill index {Start} is missing.");
        string? catalog = manifest.RootElement.TryGetProperty("catalog", out JsonElement value) ? value.GetString() : null;
        return new Package(name, description, catalog is not null && documents.ContainsKey(catalog) ? catalog : null, documents);
    }

    static (string Name, string Description) Frontmatter(string markdown)
    {
        Match block = Regex.Match(markdown, @"\A---\r?\n(.*?)\r?\n---\r?\n", RegexOptions.Singleline);
        if (!block.Success)
            throw new InvalidOperationException("SKILL.md has no frontmatter.");
        string field(string key) => Regex.Match(block.Groups[1].Value, $@"^{key}:\s*(.+)$", RegexOptions.Multiline).Groups[1].Value.Trim();
        string name = field("name");
        if (!Regex.IsMatch(name, "^[a-z0-9]+(-[a-z0-9]+)*$") || name.Length > 64)
            throw new InvalidOperationException($"SKILL.md has an invalid name '{name}'.");
        return (name, field("description"));
    }

    static string Text(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    sealed record Package(string Name, string Description, string? Catalog, IReadOnlyDictionary<string, Document> Documents);

    sealed record Document(string Path, string Title, string Text, bool IsJson);
}
