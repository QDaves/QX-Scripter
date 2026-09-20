using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Diagnostics;
using Qx.Presentation.Collections;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Logging;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Log;

public sealed partial class LogViewModel : PageViewModel
{
    public static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(150);

    readonly IApplicationLog _log;
    readonly FilteredRows<LogEntry> _rows = new();
    readonly Debouncer _refilter;
    readonly List<LogEntry> _late_arrivals = [];
    readonly CancellationTokenSource _stop = new();
    string _applied_filter = "";
    long _pass;
    bool _refiltering;

    public LogViewModel(IApplicationLog log, IClipboardService clipboard, IUiDispatcher dispatcher, TimeProvider time)
        : base(PageKey.Log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        Subtitle = "Connection, protocol and runtime events that do not belong to a script.";
        _refilter = Own(new Debouncer(dispatcher, time, FilterDelay, Refilter));
        Copy = Own(new CopyAction(clipboard, dispatcher, time, CopiedText, failed => Diag.Error("Copy failed: " + failed, "ui")));
        Visible = _log.Entries;
        _log.Appended += OnAppended;
        _log.Trimmed += OnTrimmed;
        _log.Cleared += OnCleared;
        Own(() =>
        {
            _log.Appended -= OnAppended;
            _log.Trimmed -= OnTrimmed;
            _log.Cleared -= OnCleared;
            _stop.Cancel();
            _stop.Dispose();
        });
        RecountProblems();
        Refresh(0);
    }

    public CopyAction Copy { get; }

    public SelectionList<LogEntry> Selection { get; } = new();

    [ObservableProperty]
    public partial IList Visible { get; private set; }

    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    [ObservableProperty]
    public partial bool ProblemsOnly { get; set; }

    [ObservableProperty]
    public partial bool WrapLines { get; set; }

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblems), nameof(HasWarningsOnly), nameof(ProblemText))]
    public partial int ProblemCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors), nameof(HasWarningsOnly))]
    public partial int ErrorCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending), nameof(PendingText))]
    public partial int PendingCount { get; private set; }

    [ObservableProperty]
    public partial bool IsFollowing { get; set; } = true;

    [ObservableProperty]
    public partial long ScrollToEndRequest { get; private set; }

    public bool HasPending => PendingCount > 0;

    public string PendingText => PendingCount == 1 ? "1 new event" : $"{PendingCount:N0} new events";

    public bool HasProblems => ProblemCount > 0;

    public bool HasErrors => ErrorCount > 0;

    public bool HasWarningsOnly => ProblemCount > 0 && ErrorCount == 0;

    public string ProblemText => ProblemCount == 1 ? "1 problem" : $"{ProblemCount:N0} problems";

    public bool IsFiltering => _applied_filter.Length > 0 || ProblemsOnly;

    public override bool TryClearSearch()
    {
        if (FilterText.Length == 0 && !ProblemsOnly)
            return false;
        FilterText = "";
        ProblemsOnly = false;
        _refilter.Flush();
        return true;
    }

    protected override Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        Refresh(0);
        if (IsFollowing)
            JumpToLatest();
        return Task.CompletedTask;
    }

    [RelayCommand]
    void ClearFilters() => TryClearSearch();

    [RelayCommand]
    void Clear() => _log.Clear();

    [RelayCommand]
    void JumpToLatest()
    {
        PendingCount = 0;
        IsFollowing = true;
        ScrollToEndRequest++;
    }

    partial void OnFilterTextChanged(string value) => _refilter.Trigger();

    partial void OnProblemsOnlyChanged(bool value) => _refilter.Flush();

    partial void OnIsFollowingChanged(bool value)
    {
        if (value)
            PendingCount = 0;
    }

    string CopiedText()
    {
        IReadOnlyList<LogEntry> selected = Selection.Items;
        IEnumerable<LogEntry> entries = selected.Count > 0
            ? selected.OrderBy(entry => entry.Sequence)
            : Visible.Cast<LogEntry>();
        return string.Join(Environment.NewLine, entries.Select(entry => entry.CopyText));
    }

    bool Matches(LogEntry entry) => Matches(entry, _applied_filter, ProblemsOnly);

    static bool Matches(LogEntry entry, string filter, bool problems) =>
        (!problems || entry.IsProblem) &&
        (filter.Length == 0 ||
            entry.Message.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            entry.LevelName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            (entry.Category is { } category && category.Trim().Contains(filter, StringComparison.OrdinalIgnoreCase)));

    void Refilter() => RefilterAsync().Observe("ui");

    async Task RefilterAsync()
    {
        _applied_filter = FilterText.Trim();
        _late_arrivals.Clear();
        if (!IsFiltering)
        {
            _pass++;
            _refiltering = false;
            _rows.Visible.ReplaceAll([]);
            Visible = _log.Entries;
            Refresh(0);
            ScrollToEndRequest++;
            return;
        }
        long pass = ++_pass;
        _refiltering = true;
        string filter = _applied_filter;
        bool problems = ProblemsOnly;
        await _rows.ApplyAsync(_log.Entries, entry => Matches(entry, filter, problems), null, _stop.Token);
        if (pass != _pass || IsDisposed)
            return;
        _refiltering = false;
        foreach (LogEntry entry in _late_arrivals)
        {
            if (Matches(entry))
                _rows.Visible.Add(entry);
        }
        _late_arrivals.Clear();
        DropTrimmed();
        Visible = _rows.Visible;
        Refresh(0);
        ScrollToEndRequest++;
    }

    void OnAppended(IReadOnlyList<LogEntry> batch)
    {
        foreach (LogEntry entry in batch)
        {
            if (!entry.IsProblem)
                continue;
            ProblemCount++;
            if (entry.Level >= DiagLevel.Error)
                ErrorCount++;
        }
        int shown = batch.Count;
        if (IsFiltering)
        {
            shown = 0;
            if (_refiltering)
            {
                _late_arrivals.AddRange(batch);
            }
            else
            {
                foreach (LogEntry entry in batch)
                {
                    if (!Matches(entry))
                        continue;
                    _rows.Visible.Add(entry);
                    shown++;
                }
            }
        }
        Refresh(shown);
    }

    void OnTrimmed(int removed)
    {
        RecountProblems();
        DropTrimmed();
        Refresh(0);
    }

    void OnCleared()
    {
        _pass++;
        _refiltering = false;
        _late_arrivals.Clear();
        _rows.ApplyAsync([], static _ => true, null, _stop.Token).Observe("ui");
        ProblemCount = 0;
        ErrorCount = 0;
        PendingCount = 0;
        Refresh(0);
    }

    void DropTrimmed()
    {
        if (_log.Entries.Count == 0)
        {
            _rows.Visible.ReplaceAll([]);
            return;
        }
        long first = _log.Entries[0].Sequence;
        while (_rows.Visible.Count > 0 && _rows.Visible[0].Sequence < first)
            _rows.Visible.RemoveAt(0);
    }

    void RecountProblems()
    {
        int problems = 0;
        int errors = 0;
        foreach (LogEntry entry in _log.Entries)
        {
            if (!entry.IsProblem)
                continue;
            problems++;
            if (entry.Level >= DiagLevel.Error)
                errors++;
        }
        ProblemCount = problems;
        ErrorCount = errors;
    }

    void Refresh(int appended)
    {
        Selection.Prune(Visible.Cast<LogEntry>().ToArray());
        int total = _log.Entries.Count;
        int shown = IsFiltering ? _rows.Visible.Count : total;
        CountText = shown == total
            ? total == 1 ? "1 event" : $"{total:N0} events"
            : $"{shown:N0} of {total:N0}";
        if (total == 0)
            State.ShowEmpty(IconKind.Log, "No application events yet.");
        else if (shown == 0)
            State.ShowEmpty(IconKind.Search, "No events match this filter.", "", ClearFiltersCommand, "Clear filters");
        else
            State.ShowReady();
        if (appended == 0)
            return;
        if (IsFollowing && IsActive)
            ScrollToEndRequest++;
        else
            PendingCount += appended;
    }
}
