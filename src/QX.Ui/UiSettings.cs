using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Ui;

/// <summary>The scripts that were open when QX last closed.</summary>
public sealed record SessionState
{
    public IReadOnlyList<string> Open { get; init; } = [];
    public string? Active { get; init; }
}

/// <summary>
/// How a script's panel was left: whether the tab was showing it, and what was in its controls.
/// </summary>
/// <remarks>
/// Output text and table rows are deliberately not here. They are a record of one run, not a
/// setting, and keeping them would grow the settings file without bound and bring back stale
/// output on a tab the user reopened to start something new.
/// </remarks>
public sealed record ScriptPanelMemory
{
    /// <summary>Whether the script was last left in panel mode rather than the code view.</summary>
    public bool Panel { get; init; }

    /// <summary>What was entered into the panel's controls, by control name.</summary>
    public IReadOnlyDictionary<string, string>? Values { get; init; }
}

/// <summary>Where the window was left, in device-independent units.</summary>
public sealed record WindowPlacement
{
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool Maximized { get; init; }
}

/// <summary>
/// The shell's own preferences in <c>settings.json</c>. One owner for the whole file: it used to be
/// written from <see cref="ThemeManager"/> as a single-property object, so anything else stored
/// alongside the theme was dropped on the next toggle.
/// </summary>
public sealed class UiSettings
{
    private sealed record Document
    {
        public bool Dark { get; init; } = true;
        public bool Topmost { get; init; }
        public bool OutputWrap { get; init; }
        public bool EditorWrap { get; init; }
        public double EditorFontSize { get; init; } = DefaultEditorFontSize;
        public bool RestoreSession { get; init; } = true;
        public SessionState? Session { get; init; }
        public WindowPlacement? Window { get; init; }
        public IReadOnlyDictionary<string, ScriptPanelMemory>? Panels { get; init; }
        public string? LastNotifiedRelease { get; init; }
    }

    public const double DefaultEditorFontSize = 13;
    public const double MinEditorFontSize = 8;
    public const double MaxEditorFontSize = 32;

    /// <summary>
    /// How many scripts' panels are remembered before the ones whose file is gone are dropped.
    /// </summary>
    /// <remarks>
    /// The map is keyed by path and nothing else ever removes an entry, so a library that is
    /// renamed and rewritten over years would otherwise accumulate one for every path ever used.
    /// </remarks>
    private const int MaxRememberedPanels = 200;

    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QX Scripter",
        "settings.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;
    private Document _document = new();

    public UiSettings() : this(DefaultPath)
    {
    }

    public UiSettings(string path)
    {
        _path = path;
        Load();
    }

    public bool Dark
    {
        get => _document.Dark;
        set => Update(_document with { Dark = value });
    }

    public bool Topmost
    {
        get => _document.Topmost;
        set => Update(_document with { Topmost = value });
    }

    public bool OutputWrap
    {
        get => _document.OutputWrap;
        set => Update(_document with { OutputWrap = value });
    }

    /// <summary>Whether the editor wraps long lines, kept separate from the console's own setting.</summary>
    public bool EditorWrap
    {
        get => _document.EditorWrap;
        set => Update(_document with { EditorWrap = value });
    }

    /// <summary>Clamped on the way in, so a hand-edited file cannot produce an unusable editor.</summary>
    public double EditorFontSize
    {
        get => Math.Clamp(_document.EditorFontSize, MinEditorFontSize, MaxEditorFontSize);
        set => Update(_document with
        {
            EditorFontSize = Math.Clamp(value, MinEditorFontSize, MaxEditorFontSize)
        });
    }

    /// <summary>Whether the scripts open at close are reopened on the next start.</summary>
    public bool RestoreSession
    {
        get => _document.RestoreSession;
        set => Update(_document with { RestoreSession = value });
    }

    public SessionState? Session
    {
        get => _document.Session;
        set => Update(_document with { Session = value });
    }

    public WindowPlacement? Window
    {
        get => _document.Window;
        set => Update(_document with { Window = value });
    }

    public string? LastNotifiedRelease
    {
        get => _document.LastNotifiedRelease;
        set => Update(_document with { LastNotifiedRelease = value });
    }

    /// <summary>How a script's panel was left, or null when nothing was ever saved for it.</summary>
    /// <param name="path">The script's file path.</param>
    public ScriptPanelMemory? PanelFor(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return _document.Panels?.TryGetValue(path, out ScriptPanelMemory? memory) == true ? memory : null;
    }

    /// <summary>
    /// Remembers how a script's panel was left.
    /// </summary>
    /// <remarks>
    /// Writes nothing when nothing changed: this is called on every tab switch and every view
    /// change, and rewriting the file each time would put a disk write behind clicking a tab.
    /// </remarks>
    /// <param name="path">The script's file path; unsaved buffers have nothing to key on.</param>
    /// <param name="panel">Whether the tab is showing the panel.</param>
    /// <param name="values">What the panel's controls hold.</param>
    public void RememberPanel(string path, bool panel, IReadOnlyDictionary<string, string>? values)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var memory = new ScriptPanelMemory
        {
            Panel = panel,
            Values = values is { Count: > 0 }
                ? new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
                : null
        };

        if (PanelFor(path) is { } current && SamePanel(current, memory))
            return;

        Dictionary<string, ScriptPanelMemory> panels = Keyed(_document.Panels);
        panels[path] = memory;
        Prune(panels, path);
        Update(_document with { Panels = panels });
    }

    /// <summary>A record holding a dictionary compares it by reference, so it is compared here.</summary>
    private static bool SamePanel(ScriptPanelMemory left, ScriptPanelMemory right)
    {
        if (left.Panel != right.Panel)
            return false;

        int leftCount = left.Values?.Count ?? 0;
        int rightCount = right.Values?.Count ?? 0;
        if (leftCount != rightCount)
            return false;
        if (leftCount == 0)
            return true;

        foreach ((string name, string value) in left.Values!)
        {
            if (!right.Values!.TryGetValue(name, out string? other) || !string.Equals(value, other, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Paths are compared the way Windows compares them, whatever the file held.
    /// </summary>
    /// <remarks>
    /// Filled entry by entry rather than through the copying constructor, which throws on two keys
    /// that differ only in case — a hand-edited file must not be able to cost the user every other
    /// setting in it.
    /// </remarks>
    private static Dictionary<string, ScriptPanelMemory> Keyed(IReadOnlyDictionary<string, ScriptPanelMemory>? panels)
    {
        var keyed = new Dictionary<string, ScriptPanelMemory>(StringComparer.OrdinalIgnoreCase);
        if (panels is null)
            return keyed;

        foreach ((string path, ScriptPanelMemory memory) in panels)
            keyed[path] = memory;
        return keyed;
    }

    private static void Prune(Dictionary<string, ScriptPanelMemory> panels, string keep)
    {
        if (panels.Count <= MaxRememberedPanels)
            return;

        foreach (string path in panels.Keys.ToArray())
        {
            if (!string.Equals(path, keep, StringComparison.OrdinalIgnoreCase) && !File.Exists(path))
                panels.Remove(path);
        }
    }

    private void Update(Document next)
    {
        if (next == _document)
            return;

        _document = next;
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;

            Document loaded = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path), Json) ?? new Document();

            // Json gives back an ordinal dictionary, so a path written with a different drive letter
            // case would never be found again.
            _document = loaded.Panels is null ? loaded : loaded with { Panels = Keyed(loaded.Panels) };
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
            File.WriteAllText(_path, JsonSerializer.Serialize(_document, Json));
        }
        catch
        {
        }
    }
}
