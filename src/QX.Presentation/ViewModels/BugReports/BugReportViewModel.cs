using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Diagnostics;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.BugReports;
using Qx.Presentation.Threading;

namespace Qx.Presentation.ViewModels.BugReports;

public sealed partial class BugReportViewModel : PageViewModel
{
    public const int SummaryLimit = 100;
    public const int DescriptionLimit = 3000;
    public const string NotReadYet = "Application diagnostics have not been read yet.";
    public const string Reading = "Reading sanitized application logs from the last 24 hours…";
    public const string Unreadable = "Application logs could not be read completely. The report will include this diagnostic state.";
    public const string NothingFound = "No application log entries were found for the last 24 hours.";
    public const string Success = "GitHub opened with the report ready to review and send.";
    public const string LauncherFailed = "Could not open GitHub. Copy the link and open it in your browser.";
    public const string WaitingForLogs = "Reading the application log first.";
    public const string SummaryMissing = "Add a summary to continue.";
    public const string DescriptionMissing = "Add a description to continue.";
    public const string BothMissing = "Add a summary and a description to continue.";

    static readonly BugReportLogs _unreadable = new([], 0, true);

    readonly IBugReportService _reports;
    readonly ILauncherService _launcher;
    BugReportLogs _logs = BugReportLogs.Empty;
    bool? _include_logs_choice;
    bool _syncing;
    int _read_generation;

    public BugReportViewModel(
        IBugReportService reports,
        ILauncherService launcher,
        IClipboardService clipboard,
        IUiDispatcher dispatcher,
        TimeProvider time)
        : base(PageKey.BugReport)
    {
        _reports = reports ?? throw new ArgumentNullException(nameof(reports));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(time);

        Subtitle = "Describe the problem and review what will be sent to GitHub.";
        Notices = Own(new NoticeLine(dispatcher, time));
        LinkCopy = Own(new CopyAction(
            clipboard,
            dispatcher,
            time,
            Link,
            reason => Notices.Show(NoticeSeverity.Error, reason)));
        State.ShowReady();
    }

    public NoticeLine Notices { get; }

    public CopyAction LinkCopy { get; }

    public string SummaryCounter => $"{Summary.Length}/{SummaryLimit}";

    public string DescriptionCounter => $"{Description.Length}/{DescriptionLimit}";

    public string ContinueHint
    {
        get
        {
            if (IsReading)
                return WaitingForLogs;
            bool summary = !string.IsNullOrWhiteSpace(Summary);
            bool description = !string.IsNullOrWhiteSpace(Description);
            if (summary && description)
                return "";
            if (!summary && !description)
                return BothMissing;
            return summary ? DescriptionMissing : SummaryMissing;
        }
    }

    public bool HasContinueHint => ContinueHint.Length > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    [NotifyPropertyChangedFor(nameof(SummaryCounter), nameof(ContinueHint), nameof(HasContinueHint))]
    public partial string Summary { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    [NotifyPropertyChangedFor(nameof(DescriptionCounter), nameof(ContinueHint), nameof(HasContinueHint))]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    public partial string DiagnosticsText { get; private set; } = NotReadYet;

    [ObservableProperty]
    public partial bool IncludeLogs { get; set; }

    [ObservableProperty]
    public partial bool CanIncludeLogs { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand), nameof(ReloadCommand))]
    [NotifyPropertyChangedFor(nameof(ContinueHint), nameof(HasContinueHint))]
    public partial bool IsReading { get; private set; }

    [ObservableProperty]
    public partial bool IsPreviewOpen { get; set; }

    [ObservableProperty]
    public partial string PreviewText { get; private set; } = "";

    [ObservableProperty]
    public partial bool FocusSummary { get; set; }

    protected override async Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        if (string.IsNullOrWhiteSpace(Summary))
            FocusSummary = true;
        await ReadAsync(cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanReload))]
    Task ReloadAsync(CancellationToken cancellation_token) => ReadAsync(cancellation_token);

    bool CanReload() => !IsReading;

    [RelayCommand(CanExecute = nameof(CanContinue))]
    async Task ContinueAsync(CancellationToken cancellation_token)
    {
        Notices.Clear();
        if (Build() is not { } issue)
            return;
        if (await _launcher.OpenUriAsync(issue, cancellation_token))
        {
            Notices.Show(NoticeSeverity.Success, Success);
            return;
        }
        Notices.Show(NoticeSeverity.Error, LauncherFailed);
    }

    bool CanContinue() => !IsReading && !string.IsNullOrWhiteSpace(Summary) && !string.IsNullOrWhiteSpace(Description);

    async Task ReadAsync(CancellationToken cancellation_token)
    {
        int generation = ++_read_generation;
        IsReading = true;
        CanIncludeLogs = false;
        DiagnosticsText = Reading;
        Notices.Clear();
        try
        {
            BugReportLogs logs = await _reports.CollectAsync(cancellation_token);
            if (generation == _read_generation)
                Apply(logs);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            if (generation == _read_generation)
            {
                IsReading = false;
                DiagnosticsText = NotReadYet;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"Bug report diagnostics could not be read: {error.Message}", "ui");
            if (generation == _read_generation)
                Apply(_unreadable);
        }
    }

    void Apply(BugReportLogs logs)
    {
        _logs = logs;
        IsReading = false;
        DiagnosticsText = Describe(logs);
        CanIncludeLogs = logs.Found > 0;
        _syncing = true;
        IncludeLogs = logs.Found > 0 && (_include_logs_choice ?? true);
        _syncing = false;
        RefreshPreview();
    }

    Uri? Build()
    {
        try
        {
            return _reports.CreateIssueUri(Summary, Description, ReportLogs());
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            Notices.Show(NoticeSeverity.Error, error.Message);
            return null;
        }
    }

    string? Link() => Build()?.AbsoluteUri;

    BugReportLogs? ReportLogs() => IncludeLogs || _logs.Found == 0 ? _logs : null;

    void RefreshPreview()
    {
        if (!IsPreviewOpen)
            return;
        try
        {
            PreviewText = _reports.Diagnostics(Summary, Description, ReportLogs());
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            PreviewText = error.Message;
        }
    }

    partial void OnSummaryChanged(string value)
    {
        Notices.Clear();
        RefreshPreview();
    }

    partial void OnDescriptionChanged(string value)
    {
        Notices.Clear();
        RefreshPreview();
    }

    partial void OnIncludeLogsChanged(bool value)
    {
        if (_syncing)
            return;
        _include_logs_choice = value;
        Notices.Clear();
        RefreshPreview();
    }

    partial void OnIsPreviewOpenChanged(bool value) => RefreshPreview();

    static string Describe(BugReportLogs logs) => logs.Found switch
    {
        0 when logs.Truncated => Unreadable,
        0 => NothingFound,
        _ => $"{logs.Included} relevant {(logs.Included == 1 ? "entry" : "entries")} in the last 24 hours."
    };
}
