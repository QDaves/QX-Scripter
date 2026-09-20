using Qx.Diagnostics;
using Qx.Presentation.Platform;

namespace Qx.Presentation.Services.BugReports;

public interface IBugReportService
{
    Task<BugReportLogs> CollectAsync(CancellationToken cancellation_token);

    BugReportContext Context();

    Uri CreateIssueUri(string summary, string description, BugReportLogs? logs);

    string Diagnostics(string summary, string description, BugReportLogs? logs);
}

public sealed class BugReportService : IBugReportService
{
    const string Placeholder = ".";
    const string DiagnosticsField = "diagnostics";

    readonly IAppPaths _paths;
    readonly Func<BugReportContext> _context;
    readonly TimeProvider _time;

    public BugReportService(IAppPaths paths, Func<BugReportContext> context, TimeProvider time)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public Task<BugReportLogs> CollectAsync(CancellationToken cancellation_token) =>
        Task.Run(() => BugReport.Collect(_paths.LogFile, _paths.CrashLogFile, _time.GetLocalNow().DateTime), cancellation_token);

    public BugReportContext Context() => _context();

    public Uri CreateIssueUri(string summary, string description, BugReportLogs? logs) =>
        BugReport.CreateIssueUri(summary, description, _context(), logs);

    public string Diagnostics(string summary, string description, BugReportLogs? logs)
    {
        Uri issue = CreateIssueUri(
            string.IsNullOrWhiteSpace(summary) ? Placeholder : summary,
            string.IsNullOrWhiteSpace(description) ? Placeholder : description,
            logs);
        return Field(issue, DiagnosticsField);
    }

    static string Field(Uri issue, string name)
    {
        foreach (string pair in issue.Query.TrimStart('?').Split('&'))
        {
            int separator = pair.IndexOf('=');
            if (separator > 0 && string.Equals(pair[..separator], name, StringComparison.Ordinal))
                return Uri.UnescapeDataString(pair[(separator + 1)..]);
        }
        return "";
    }
}
