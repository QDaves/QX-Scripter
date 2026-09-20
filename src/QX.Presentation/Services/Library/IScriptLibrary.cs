using System.Text.Json.Serialization;
using Qx.Scripting;

namespace Qx.Presentation.Services.Library;

public enum LibraryView
{
    List,
    Grid
}

public enum LibrarySort
{
    Modified,
    LastRun,
    Name
}

public sealed record ScriptMeta
{
    [JsonPropertyName("group")]
    public string? Category { get; init; }

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Category);

    public static ScriptMeta Empty { get; } = new();
}

public sealed record LastRun(DateTimeOffset At, ScriptRunState? Outcome);

public sealed record ScriptFileEntry(string Path, string Name, DateTimeOffset EditedAt, long Length);

public interface IScriptLibrary
{
    LibraryView View { get; set; }

    LibrarySort Sort { get; set; }

    IReadOnlyList<string> Categories { get; }

    event Action? Changed;

    bool IsCollapsed(string category);

    void SetCollapsed(string category, bool collapsed);

    ScriptMeta Get(string name);

    void Set(string name, ScriptMeta meta);

    void Remove(string name);

    void Rename(string from, string to);

    int RenameCategory(string from, string to);

    int RemoveCategory(string category);

    LastRun? LastRunOf(string name);

    void RecordRunStarted(string name, DateTimeOffset at);

    void RecordRunFinished(string name, ScriptRunState outcome);

    Task FlushAsync(CancellationToken cancellation_token);

    bool FlushNow(TimeSpan budget);
}
