using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Ui;

public enum LibraryView
{
    List,
    Grid
}

/// <summary>How the library orders scripts inside each category.</summary>
/// <remarks>
/// Within a category, never across them: the grouping is the outer structure and a sort that broke
/// it up would leave the headers meaningless.
/// </remarks>
public enum LibrarySort
{
    /// <summary>Most recently edited first, which is what the library did before it could be told.</summary>
    Modified,

    /// <summary>Most recently run first, and never-run last.</summary>
    LastRun,

    Name
}

/// <summary>
/// What the user chose for a script, which is its category and nothing else.
/// </summary>
/// <remarks>
/// The stored name stays <c>group</c>. It is the only field an existing library file holds and
/// renaming the key on disk would quietly drop every category anyone had already assigned.
/// </remarks>
public sealed record ScriptMeta
{
    [JsonPropertyName("group")]
    public string? Category { get; init; }

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Category);
}

/// <summary>
/// Library presentation stored beside the scripts, keyed by script name. Kept out of
/// <c>settings.json</c> because <see cref="ThemeManager"/> rewrites that file wholesale.
/// </summary>
public sealed class ScriptLibrary
{
    private sealed record Document
    {
        public string? View { get; init; }
        public string? Sort { get; init; }
        public List<string>? Collapsed { get; init; }

        public Dictionary<string, DateTime>? LastRun { get; init; }

        public Dictionary<string, string>? LastOutcome { get; init; }

        public Dictionary<string, ScriptMeta>? Scripts { get; init; }
    }

    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QX Scripter",
        "library.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Dictionary<string, ScriptMeta> _meta = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Categories the user has folded away, by name.
    /// </summary>
    /// <remarks>
    /// Held here rather than in the view because the library rebuilds its collection view on every
    /// keystroke of the search, which throws away every expander along with it. Collapsed rather
    /// than expanded is stored so that a category made later starts open, which is what someone who
    /// has never seen it expects.
    /// </remarks>
    private readonly HashSet<string> _collapsed = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, DateTime> _last_run = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _last_outcome = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _path;
    private LibraryView _view = LibraryView.List;
    private LibrarySort _sort = LibrarySort.Modified;

    public ScriptLibrary() : this(DefaultPath)
    {
    }

    public ScriptLibrary(string path)
    {
        _path = path;
        Load();
    }

    public LibraryView View
    {
        get => _view;
        set
        {
            if (_view == value)
                return;
            _view = value;
            Save();
        }
    }

    /// <summary>Whether a category is folded away.</summary>
    public bool IsCollapsed(string category) =>
        !string.IsNullOrWhiteSpace(category) && _collapsed.Contains(category.Trim());

    /// <summary>Remembers whether a category is folded away, writing only on a real change.</summary>
    public void SetCollapsed(string category, bool collapsed)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;

        bool changed = collapsed ? _collapsed.Add(category.Trim()) : _collapsed.Remove(category.Trim());
        if (changed)
            Save();
    }

    public LibrarySort Sort
    {
        get => _sort;
        set
        {
            if (_sort == value)
                return;
            _sort = value;
            Save();
        }
    }

    public ScriptMeta Get(string name) =>
        _meta.TryGetValue(name, out ScriptMeta? meta) ? meta : new ScriptMeta();

    public void Set(string name, ScriptMeta meta)
    {
        if (meta.IsEmpty)
            _meta.Remove(name);
        else
            _meta[name] = meta;
        Save();
    }

    public void Remove(string name)
    {
        if (_meta.Remove(name))
            Save();
    }

    public void Rename(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return;
        if (!_meta.Remove(from, out ScriptMeta? meta))
            return;

        _meta[to] = meta;
        Save();
    }

    public int RenameCategory(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return 0;

        string source = from.Trim();
        string target = to.Trim();
        if (string.Equals(source, target, StringComparison.Ordinal))
            return 0;

        int moved = 0;
        foreach (string name in _meta.Keys.ToArray())
        {
            if (!string.Equals(_meta[name].Category?.Trim(), source, StringComparison.OrdinalIgnoreCase))
                continue;
            _meta[name] = _meta[name] with { Category = target };
            moved++;
        }

        if (_collapsed.Remove(source))
            _collapsed.Add(target);

        if (moved > 0)
            Save();
        return moved;
    }

    /// <summary>
    /// Takes every script out of a category and forgets the category entirely.
    /// </summary>
    /// <returns>How many scripts were uncategorised.</returns>
    public int RemoveCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return 0;

        string key = category.Trim();
        int moved = 0;
        foreach (string name in _meta.Keys.ToArray())
        {
            if (!string.Equals(_meta[name].Category?.Trim(), key, StringComparison.OrdinalIgnoreCase))
                continue;

            // The category was the only thing stored, so the entry itself goes with it.
            _meta.Remove(name);
            moved++;
        }

        _collapsed.Remove(key);
        Save();
        return moved;
    }

    public DateTime? LastRun(string name) =>
        !string.IsNullOrWhiteSpace(name) && _last_run.TryGetValue(name, out DateTime when) ? when : null;

    public void SetLastRun(string name, DateTime when)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        _last_run[name] = when;
        _last_outcome.Remove(name);
    }

    public string? LastOutcome(string name) =>
        !string.IsNullOrWhiteSpace(name) && _last_outcome.TryGetValue(name, out string? outcome) ? outcome : null;

    public void SetLastOutcome(string name, string outcome)
    {
        if (string.IsNullOrWhiteSpace(name) || !_last_run.ContainsKey(name))
            return;
        _last_outcome[name] = outcome;
    }

    /// <summary>Categories already in use, for seeding the category picker.</summary>
    public IReadOnlyList<string> Categories => _meta.Values
        .Select(meta => meta.Category)
        .Where(category => !string.IsNullOrWhiteSpace(category))
        .Select(category => category!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;

            Document? document = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path), Json);
            if (document is null)
                return;

            if (Enum.TryParse(document.View, ignoreCase: true, out LibraryView view))
                _view = view;

            if (Enum.TryParse(document.Sort, ignoreCase: true, out LibrarySort sort))
                _sort = sort;

            foreach (string category in document.Collapsed ?? [])
            {
                if (!string.IsNullOrWhiteSpace(category))
                    _collapsed.Add(category.Trim());
            }

            foreach ((string name, ScriptMeta meta) in document.Scripts ?? [])
            {
                if (!meta.IsEmpty)
                    _meta[name] = meta;
            }

            if (document.LastRun is not null || document.LastOutcome is not null)
                Save();
        }
        catch
        {
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var document = new Document
            {
                View = _view.ToString(),
                Sort = _sort.ToString(),
                Collapsed = _collapsed.Count > 0 ? [.. _collapsed.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)] : null,
                Scripts = _meta
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(document, Json));
        }
        catch
        {
        }
    }
}
