using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Collections;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Output;
using Qx.Presentation.Threading;

namespace Qx.Presentation.ViewModels.Editor;

public sealed partial class OutputConsoleViewModel : ViewModelBase
{
    public static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(150);

    readonly OutputBuffer _buffer;
    readonly ResettableCollection<OutputLine> _filtered = [];
    readonly Debouncer _refilter;
    readonly CopyAction _copy_selection;
    IReadOnlyList<OutputLine> _selected = [];
    string _applied_filter = "";

    public OutputConsoleViewModel(OutputBuffer buffer, IClipboardService clipboard, IUiDispatcher dispatcher, TimeProvider time)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _refilter = Own(new Debouncer(dispatcher, time, FilterDelay, Refilter));
        Copy = Own(new CopyAction(clipboard, dispatcher, time, VisibleText, failed => _buffer.Write("Copy failed: " + failed, OutputLevel.Error)));
        _copy_selection = Own(new CopyAction(clipboard, dispatcher, time, SelectedText, failed => _buffer.Write("Copy failed: " + failed, OutputLevel.Error)));
        _buffer.Appended += OnAppended;
        _buffer.Trimmed += OnTrimmed;
        _buffer.Cleared += OnCleared;
        Own(() =>
        {
            _buffer.Appended -= OnAppended;
            _buffer.Trimmed -= OnTrimmed;
            _buffer.Cleared -= OnCleared;
        });
        Visible = _buffer.Lines;
    }

    public CopyAction Copy { get; }

    [ObservableProperty]
    public partial IList Visible { get; private set; }

    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    [ObservableProperty]
    public partial bool ProblemsOnly { get; set; }

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblems), nameof(ProblemText))]
    public partial int ProblemCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    public partial int ErrorCount { get; private set; }

    [ObservableProperty]
    public partial bool IsCollapsed { get; set; } = true;

    [ObservableProperty]
    public partial double? ExpandedHeight { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending), nameof(PendingText))]
    public partial int PendingCount { get; private set; }

    [ObservableProperty]
    public partial bool IsFollowing { get; set; } = true;

    [ObservableProperty]
    public partial long ScrollToEndRequest { get; private set; }

    public bool HasPending => PendingCount > 0;

    public string PendingText => PendingCount == 1 ? "1 new line" : $"{PendingCount} new lines";

    public bool IsFiltering => _applied_filter.Length > 0 || ProblemsOnly;

    public bool HasOutput => _buffer.Lines.Count > 0;

    public bool HasProblems => ProblemCount > 0;

    public bool HasErrors => ErrorCount > 0;

    public string ProblemText => ProblemCount == 1 ? "1 problem" : $"{ProblemCount} problems";

    public void Expand() => IsCollapsed = false;

    public void CopySelected(IReadOnlyList<OutputLine> lines)
    {
        _selected = lines ?? [];
        _copy_selection.CopyCommand.Execute(null);
    }

    [RelayCommand]
    void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    [RelayCommand]
    void ClearFilter() => FilterText = "";

    [RelayCommand]
    void Clear() => _buffer.Clear();

    [RelayCommand]
    void JumpToLatest()
    {
        PendingCount = 0;
        IsFollowing = true;
        ScrollToEndRequest++;
    }

    string SelectedText() => string.Join(Environment.NewLine, _selected.Select(line => line.Text));

    partial void OnFilterTextChanged(string value) => _refilter.Trigger();

    partial void OnProblemsOnlyChanged(bool value) => _refilter.Flush();

    partial void OnIsFollowingChanged(bool value)
    {
        if (value)
            PendingCount = 0;
    }

    string VisibleText() => string.Join(Environment.NewLine, Visible.Cast<OutputLine>().Select(line => line.Text));

    bool Matches(OutputLine line) =>
        (!ProblemsOnly || line.Level != OutputLevel.Info) &&
        (_applied_filter.Length == 0 || line.Text.Contains(_applied_filter, StringComparison.OrdinalIgnoreCase));

    void Refilter()
    {
        _applied_filter = FilterText.Trim();
        if (!IsFiltering)
        {
            _filtered.ReplaceAll([]);
            Visible = _buffer.Lines;
        }
        else
        {
            _filtered.ReplaceAll(_buffer.Lines.Where(Matches));
            Visible = _filtered;
        }
        Recount();
        ScrollToEndRequest++;
    }

    void OnAppended(IReadOnlyList<OutputLine> batch)
    {
        foreach (OutputLine line in batch)
        {
            if (line.Level == OutputLevel.Info)
                continue;
            ProblemCount++;
            if (line.Level == OutputLevel.Error)
                ErrorCount++;
        }
        int shown = batch.Count;
        if (IsFiltering)
        {
            shown = 0;
            foreach (OutputLine line in batch)
            {
                if (!Matches(line))
                    continue;
                _filtered.Add(line);
                shown++;
            }
            Recount();
        }
        OnPropertyChanged(nameof(HasOutput));
        if (shown == 0)
            return;
        if (IsFollowing)
            ScrollToEndRequest++;
        else
            PendingCount += shown;
    }

    void OnTrimmed(int removed)
    {
        RecountProblems();
        if (!IsFiltering || _buffer.Lines.Count == 0)
            return;
        long first = _buffer.Lines[0].Sequence;
        while (_filtered.Count > 0 && _filtered[0].Sequence < first)
            _filtered.RemoveAt(0);
        Recount();
    }

    void OnCleared()
    {
        _filtered.ReplaceAll([]);
        ProblemCount = 0;
        ErrorCount = 0;
        PendingCount = 0;
        Recount();
        OnPropertyChanged(nameof(HasOutput));
    }

    void Recount() =>
        CountText = !IsFiltering ? "" : _filtered.Count == 0 ? "no matches" : $"{_filtered.Count} matching";

    void RecountProblems()
    {
        int problems = 0;
        int errors = 0;
        foreach (OutputLine line in _buffer.Lines)
        {
            if (line.Level == OutputLevel.Info)
                continue;
            problems++;
            if (line.Level == OutputLevel.Error)
                errors++;
        }
        ProblemCount = problems;
        ErrorCount = errors;
    }
}
