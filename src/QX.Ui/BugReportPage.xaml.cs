using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Qx.Diagnostics;

namespace Qx.Ui;

public partial class BugReportPage : GamePage
{
    private Func<BugReportContext>? _context;
    private string _log_path = "";
    private string? _crash_log_path;
    private BugReportLogs _logs = BugReportLogs.Empty;
    private bool? _include_logs_preference;
    private int _load_generation;
    private bool _loading;

    public BugReportPage()
    {
        InitializeComponent();
        DiagnosticsText.Text = "Application diagnostics have not been read yet.";
        UpdateState();
    }

    public void Bind(
        Func<BugReportContext> context,
        string log_path,
        string? crash_log_path)
    {
        _context = context;
        _log_path = log_path;
        _crash_log_path = crash_log_path;
        UpdateState();
    }

    public override void Refresh()
    {
        UpdateState();
    }

    public override void Opened()
    {
        int generation = ++_load_generation;
        _ = ReadLogsAsync(generation);
        if (string.IsNullOrWhiteSpace(SummaryBox.Text))
            SummaryBox.Focus();
    }

    private async Task ReadLogsAsync(int generation)
    {
        _loading = true;
        DiagnosticsText.Text = "Reading sanitized application logs from the last 24 hours…";
        IncludeLogsOption.IsEnabled = false;
        ClearFeedback();
        UpdateState();

        BugReportLogs logs;
        try
        {
            DateTime now = DateTime.Now;
            logs = await Task.Run(() => BugReport.Collect(_log_path, _crash_log_path, now));
        }
        catch
        {
            logs = new BugReportLogs(Array.Empty<string>(), 0, true);
        }

        if (generation != _load_generation)
            return;

        _logs = logs;
        _loading = false;
        DiagnosticsText.Text = logs.Found switch
        {
            0 when logs.Truncated => "Application logs could not be read completely. The report will include this diagnostic state.",
            0 => "No application log entries were found for the last 24 hours.",
            _ => $"{logs.Included} relevant {(logs.Included == 1 ? "entry" : "entries")} in the last 24 hours."
        };
        IncludeLogsOption.IsEnabled = logs.Found > 0;
        IncludeLogsOption.IsChecked = logs.Found > 0 && (_include_logs_preference ?? true);
        UpdateState();
    }

    private void OnContinue(object sender, RoutedEventArgs e)
    {
        if (!Ready() || _context is null)
            return;

        ClearFeedback();
        try
        {
            BugReportLogs? report_logs = IncludeLogsOption.IsChecked == true || _logs.Found == 0
                ? _logs
                : null;
            Uri issue = BugReport.CreateIssueUri(
                SummaryBox.Text,
                DescriptionBox.Text,
                _context(),
                report_logs);
            Process.Start(new ProcessStartInfo(issue.AbsoluteUri) { UseShellExecute = true });
            SuccessText.Visibility = Visibility.Visible;
        }
        catch (InvalidOperationException error)
        {
            ShowError(error.Message);
        }
        catch (ArgumentException error)
        {
            ShowError(error.Message);
        }
        catch (Exception error)
        {
            ShowError("Could not open GitHub: " + error.Message);
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ClearFeedback();
        UpdateState();
    }

    private void OnIncludeLogsChanged(object sender, RoutedEventArgs e)
    {
        _include_logs_preference = IncludeLogsOption.IsChecked == true;
        ClearFeedback();
    }

    private void UpdateState()
    {
        if (ContinueButton is not null)
            ContinueButton.IsEnabled = !_loading && _context is not null && Ready();
    }

    private bool Ready() =>
        !string.IsNullOrWhiteSpace(SummaryBox?.Text)
        && !string.IsNullOrWhiteSpace(DescriptionBox?.Text);

    private void ClearFeedback()
    {
        if (ErrorText is not null)
            ErrorText.Visibility = Visibility.Collapsed;
        if (SuccessText is not null)
            SuccessText.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
