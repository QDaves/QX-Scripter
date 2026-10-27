using System.Text.Json.Serialization;
using Qx.Scripting.Hosting;

namespace Qx.Presentation.Services.Library;

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

    public CommunityOrigin? Community { get; init; }

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Category) && Community is null;

    public static ScriptMeta Empty { get; } = new();
}

public sealed record LastRun(DateTimeOffset At, ScriptRunState? Outcome);

public sealed record ScriptFileEntry(string Path, string Name, DateTimeOffset EditedAt, long Length, ScriptHeader Header);

public interface IScriptLibrary
{
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

    bool IsCommunityApproved(string id, string sha256);

    void ApproveCommunity(string id, string sha256);

    Task FlushAsync(CancellationToken cancellationToken);

    bool FlushNow(TimeSpan budget);
}
