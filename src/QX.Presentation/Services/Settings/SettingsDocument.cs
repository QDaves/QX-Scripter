using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Presentation.Services.Settings;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public sealed record SessionState
{
    public IReadOnlyList<string> Open { get; init; } = [];

    public string? Active { get; init; }
}

public sealed record ScriptPanelMemory
{
    public bool Panel { get; init; }

    public IReadOnlyDictionary<string, string>? Values { get; init; }
}

public sealed record WindowPlacement
{
    public double Left { get; init; }

    public double Top { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public bool Maximized { get; init; }
}

public sealed record SettingsDocument
{
    public bool Dark { get; init; } = true;

    public ThemeMode? Theme { get; init; }

    public bool Topmost { get; init; }

    public bool OutputWrap { get; init; }

    public bool EditorWrap { get; init; }

    public double EditorFontSize { get; init; } = 13;

    public bool RestoreSession { get; init; } = true;

    public SessionState? Session { get; init; }

    public WindowPlacement? Window { get; init; }

    public IReadOnlyDictionary<string, ScriptPanelMemory>? Panels { get; init; }

    public string? LastNotifiedRelease { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; set; }
}
