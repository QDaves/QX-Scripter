using System.Text.Json;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Settings;

public sealed class SettingsStore : ISettingsStore, IDisposable
{
    public const int MaxRememberedPanels = 200;
    public const double MinEditorFontSize = 8;
    public const double MaxEditorFontSize = 32;

    readonly AtomicJsonFile _file;

    public SettingsStore(IAppPaths paths, IUiDispatcher dispatcher, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Current = Load(paths.SettingsFile);
        _file = new AtomicJsonFile(paths.SettingsFile, "settings", "Settings could not be saved", () => SettingsCodec.Write(Current), dispatcher, time);
    }

    public SettingsDocument Current { get; private set; }

    public event Action<SettingsDocument>? Changed;

    public void Update(Func<SettingsDocument, SettingsDocument> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        SettingsDocument next = Normalize(change(Current));
        if (next == Current)
            return;
        Current = next;
        Changed?.Invoke(next);
        _file.Schedule();
    }

    public ScriptPanelMemory? PanelFor(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Current.Panels?.TryGetValue(path, out ScriptPanelMemory? memory) == true ? memory : null;
    }

    public void RememberPanel(string path, bool panel, IReadOnlyDictionary<string, string>? values)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var memory = new ScriptPanelMemory
        {
            Panel = panel,
            Values = values is { Count: > 0 } ? new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase) : null
        };
        if (PanelFor(path) is { } current && SamePanel(current, memory))
            return;
        Dictionary<string, ScriptPanelMemory> panels = Keyed(Current.Panels);
        panels[path] = memory;
        if (panels.Count > MaxRememberedPanels)
        {
            foreach (string known in panels.Keys.ToArray())
            {
                if (!string.Equals(known, path, StringComparison.OrdinalIgnoreCase) && !File.Exists(known))
                    panels.Remove(known);
            }
        }
        Update(document => document with { Panels = panels });
    }

    public void ForgetPanel(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (PanelFor(path) is null)
            return;
        Dictionary<string, ScriptPanelMemory> panels = Keyed(Current.Panels);
        panels.Remove(path);
        Update(document => document with { Panels = panels });
    }

    public Task FlushAsync(CancellationToken cancellation_token) => _file.FlushAsync(cancellation_token);

    public bool FlushNow(TimeSpan budget) => _file.FlushNow(budget);

    public void Dispose() => _file.Dispose();

    static SettingsDocument Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new SettingsDocument { Theme = ThemeMode.System };
            SettingsDocument loaded = SettingsCodec.Read(File.ReadAllText(path));
            return Normalize(loaded with { Panels = loaded.Panels is null ? null : Keyed(loaded.Panels) });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new SettingsDocument();
        }
    }

    static SettingsDocument Normalize(SettingsDocument document)
    {
        double size = Math.Clamp(document.EditorFontSize, MinEditorFontSize, MaxEditorFontSize);
        return size.Equals(document.EditorFontSize) ? document : document with { EditorFontSize = size };
    }

    static Dictionary<string, ScriptPanelMemory> Keyed(IReadOnlyDictionary<string, ScriptPanelMemory>? panels)
    {
        var keyed = new Dictionary<string, ScriptPanelMemory>(StringComparer.OrdinalIgnoreCase);
        if (panels is null)
            return keyed;
        foreach ((string path, ScriptPanelMemory memory) in panels)
            keyed[path] = memory;
        return keyed;
    }

    static bool SamePanel(ScriptPanelMemory left, ScriptPanelMemory right)
    {
        if (left.Panel != right.Panel)
            return false;
        int count = left.Values?.Count ?? 0;
        if (count != (right.Values?.Count ?? 0))
            return false;
        if (count == 0)
            return true;
        foreach ((string name, string value) in left.Values!)
        {
            if (!right.Values!.TryGetValue(name, out string? other) || !string.Equals(value, other, StringComparison.Ordinal))
                return false;
        }
        return true;
    }
}
