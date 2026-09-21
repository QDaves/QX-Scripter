using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Services.Library;
using Qx.Presentation.Services.Status;
using Qx.Presentation.Visuals;
using Qx.Scripting;

namespace Qx.Presentation.ViewModels.Library;

public enum LibraryRunState
{
    Never,
    Running,
    Ready,
    Ran,
    Failed
}

public abstract class LibraryRow : ObservableObject
{
}

public sealed partial class LibraryGroupRow : LibraryRow, ISectionHeader
{
    public LibraryGroupRow(string name, bool invented)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        IsInvented = invented;
        IsExpanded = true;
    }

    public string Name { get; }

    public bool IsInvented { get; }

    public bool CanEdit => !IsInvented;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText))]
    public partial int Count { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FoldIcon))]
    public partial bool IsExpanded { get; set; }

    public string CountText => Count.ToString(CultureInfo.CurrentCulture);

    public IconKind FoldIcon => IsExpanded ? IconKind.ChevronDown : IconKind.ChevronRight;
}

public sealed partial class LibraryScriptRow : LibraryRow
{
    public const string Uncategorised = "Uncategorised";

    public LibraryScriptRow(string path, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Path = path;
        Name = name;
    }

    public string Path { get; }

    public string Name { get; }

    public string DeleteAccessibilityText => $"Delete {Name}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryName), nameof(HasCategory))]
    public partial string? Category { get; private set; }

    [ObservableProperty]
    public partial DateTimeOffset EditedAt { get; private set; }

    [ObservableProperty]
    public partial LastRun? LastRun { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsReady), nameof(HasRun), nameof(IsFailed), nameof(IsNeverRun))]
    public partial LibraryRunState RunState { get; private set; }

    [ObservableProperty]
    public partial bool ShowCategory { get; set; }

    [ObservableProperty]
    public partial string RunText { get; private set; } = "";

    [ObservableProperty]
    public partial string EditedText { get; private set; } = "";

    [ObservableProperty]
    public partial string DetailTooltip { get; private set; } = "";

    [ObservableProperty]
    public partial string AccessibilityText { get; private set; } = "";

    [ObservableProperty]
    public partial bool CanDelete { get; private set; } = true;

    public bool HasCategory => !string.IsNullOrWhiteSpace(Category);

    public string CategoryName => HasCategory ? Category!.Trim() : Uncategorised;

    public bool IsRunning => RunState == LibraryRunState.Running;

    public bool IsReady => RunState == LibraryRunState.Ready;

    public bool HasRun => RunState == LibraryRunState.Ran;

    public bool IsFailed => RunState == LibraryRunState.Failed;

    public bool IsNeverRun => RunState == LibraryRunState.Never;

    public void Apply(DateTimeOffset edited_at, string? category, LastRun? run, bool running, bool armed, DateTimeOffset now)
    {
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        EditedAt = edited_at;
        LastRun = run;
        RunState = StateOf(run, running, armed);
        CanDelete = !running;
        Refresh(now);
    }

    public void Refresh(DateTimeOffset now)
    {
        string edited = RelativeTime.Ago(EditedAt, now);
        RunText = TextFor(RunState, LastRun, now);
        EditedText = $"edited {edited}";
        DetailTooltip = TooltipFor(RunState, LastRun, edited, now);
        AccessibilityText = AccessibilityFor(Name, HasCategory ? CategoryName : null, IsRunning, edited);
    }

    public static LibraryRunState StateOf(LastRun? run, bool running, bool armed)
    {
        if (running)
            return LibraryRunState.Running;
        if (armed)
            return LibraryRunState.Ready;
        if (run is null)
            return LibraryRunState.Never;
        return run.Outcome == ScriptRunState.Faulted ? LibraryRunState.Failed : LibraryRunState.Ran;
    }

    public static string TextFor(LibraryRunState state, LastRun? run, DateTimeOffset now) => state switch
    {
        LibraryRunState.Running => "running",
        LibraryRunState.Ready => "ready",
        LibraryRunState.Failed when run is not null => $"failed {RelativeTime.Ago(run.At, now)}",
        LibraryRunState.Ran when run is not null => $"ran {RelativeTime.Ago(run.At, now)}",
        _ => "never run"
    };

    public static string TooltipFor(LibraryRunState state, LastRun? run, string edited, DateTimeOffset now) => state switch
    {
        LibraryRunState.Running => $"Running; edited {edited}",
        LibraryRunState.Ready => $"Ready; edited {edited}",
        _ when run is not null => $"{OutcomeText(run.Outcome)} {RelativeTime.Ago(run.At, now)} · edited {edited}",
        _ => $"Edited {edited}, never run"
    };

    public static string OutcomeText(ScriptRunState? outcome) => outcome switch
    {
        ScriptRunState.Faulted => "Failed",
        ScriptRunState.Stopped => "Stopped",
        ScriptRunState.Finished => "Finished",
        _ => "Last run"
    };

    static string AccessibilityFor(string name, string? category, bool running, string edited)
    {
        var parts = new List<string>(4) { name };
        if (category is not null)
            parts.Add($"category {category}");
        if (running)
            parts.Add("running");
        parts.Add($"modified {edited}");
        return string.Join(", ", parts);
    }
}
