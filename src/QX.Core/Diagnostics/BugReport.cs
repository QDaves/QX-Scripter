using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Qx.Diagnostics;

public sealed record BugReportContext(
    string Version,
    string OperatingSystem,
    string Architecture,
    string Runtime,
    string GEarth,
    string Client,
    string Mcp);

public sealed record BugReportLogs(
    IReadOnlyList<string> Entries,
    int Found,
    bool Truncated)
{
    public int Included => Entries.Count;

    public string Text => string.Join(Environment.NewLine + Environment.NewLine, Entries);

    public static BugReportLogs Empty { get; } = new(Array.Empty<string>(), 0, false);
}

public static class BugReport
{
    public const int MaxIssueUrlLength = 5900;

    private const int MaxLogCharacters = 3000;
    private const int MaxLogFileBytes = 8 * 1024 * 1024;
    private const int MaxCrashFileBytes = 64 * 1024;
    private const int MaxRetainedEntries = 512;
    private const int MaxRawEntryCharacters = 32 * 1024;
    private const int MaxProblemEntryCharacters = 1200;
    private const int MaxContextEntryCharacters = 500;
    private const int MinEntryCharacters = 128;
    private static readonly string NewIssueUrl = ProjectLinks.NewIssue.AbsoluteUri;
    private const string IssueTemplate = "bug-report.yml";
    private const string Omitted = "[older diagnostics omitted to fit the report]";

    private static readonly Regex LogStart = new(
        @"^\[(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]\s+(?<level>\w+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex QuerySecret = new(
        """(?<prefix>[?&](?:token|access_token|refresh_token|api_key|apikey|client_secret|mcp_token|x-mcp-token|secret|password|cookie)=)[^&\s"']+""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex HeaderSecret = new(
        @"(?<prefix>\b(?:authorization|x-mcp-token|cookie|set-cookie)\s*[:=]\s*)[^\r\n]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex NamedSecret = new(
        """(?<![\p{L}\p{N}_-])(?<prefix>(?:"|')?(?:authorization|x[_-]?mcp[_-]?token|mcp[_-]?token|token|api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|password|secret|cookie)(?:"|')?\s*[=:]\s*)(?<value>"[^"\r\n]*"|'[^'\r\n]*'|[^\s,;}]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BearerSecret = new(
        @"(?<prefix>\bBearer\s+)[A-Za-z0-9._~+/=-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CookieArgument = new(
        """(?<prefix>(?:^|\s)-c\s+)(?:"[^"\r\n]*"|'[^'\r\n]*'|\S+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public static BugReportLogs Collect(string logPath, string? crashLogPath, DateTime now)
    {
        DateTime cutoff = now.AddHours(-24);
        var collector = new LogCollector(cutoff, now.AddMinutes(5));
        ReadLog(logPath + ".1", collector);
        ReadLog(logPath, collector);
        ReadCrashLog(crashLogPath, cutoff, collector);

        LogRecord[] recent = collector.Entries
            .OrderByDescending(entry => entry.Problem)
            .ThenByDescending(entry => entry.At)
            .ThenByDescending(entry => entry.Order)
            .ToArray();
        if (collector.Found == 0)
            return collector.Truncated
                ? new BugReportLogs(Array.Empty<string>(), 0, true)
                : BugReportLogs.Empty;

        var selected = new List<string>();
        int used = 0;
        bool shortened = collector.Truncated;

        foreach (LogRecord entry in recent)
        {
            string text = Redact(entry.Text).TrimEnd();
            if (text.Length == 0)
                continue;

            int available = MaxLogCharacters - used - (selected.Count == 0 ? 0 : 2);
            if (available < MinEntryCharacters)
                break;

            int entryLimit = Math.Min(
                available,
                entry.Problem ? MaxProblemEntryCharacters : MaxContextEntryCharacters);
            if (text.Length > entryLimit)
                shortened = true;
            text = ShortenEntry(text, entryLimit);

            selected.Add(text);
            used += text.Length + (selected.Count == 1 ? 0 : 2);
        }

        return new BugReportLogs(
            selected.ToArray(),
            collector.Found,
            shortened || selected.Count < collector.Found);
    }

    public static Uri CreateIssueUri(
        string title,
        string description,
        BugReportContext context,
        BugReportLogs? logs)
    {
        string issueTitle = "[Bug] " + CompactLine(Redact(title));
        string issueDescription = Redact(description.Trim());
        if (string.IsNullOrWhiteSpace(issueTitle[6..]))
            throw new ArgumentException("A summary is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(issueDescription))
            throw new ArgumentException("A description is required.", nameof(description));

        var logEntries = logs?.Entries
            .Select(Redact)
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToList() ?? [];
        bool trimmedLogs = logs?.Truncated == true;
        bool minimalDiagnostics = false;

        while (true)
        {
            string diagnostics = minimalDiagnostics
                ? "Diagnostics omitted because the encoded report exceeded GitHub's URL limit."
                : Diagnostics(context, logs, logEntries, trimmedLogs);
            string url = NewIssueUrl
                + "?template=" + Uri.EscapeDataString(IssueTemplate)
                + "&title=" + Uri.EscapeDataString(issueTitle)
                + "&description=" + Uri.EscapeDataString(issueDescription)
                + "&diagnostics=" + Uri.EscapeDataString(diagnostics);
            if (url.Length <= MaxIssueUrlLength)
                return new Uri(url);

            if (logEntries.Count > 0)
            {
                logEntries.RemoveAt(logEntries.Count - 1);
                trimmedLogs = true;
                continue;
            }

            if (minimalDiagnostics)
                throw new InvalidOperationException("The summary and description are too long for GitHub. Shorten the report and try again.");
            minimalDiagnostics = true;
        }
    }

    public static string Redact(string value)
    {
        string redacted = QuerySecret.Replace(value, match => match.Groups["prefix"].Value + "[redacted]");
        redacted = HeaderSecret.Replace(redacted, match => match.Groups["prefix"].Value + "[redacted]");
        redacted = NamedSecret.Replace(redacted, match =>
        {
            string value = match.Groups["value"].Value;
            string replacement = value.StartsWith('"') ? "\"[redacted]\""
                : value.StartsWith('\'') ? "'[redacted]'"
                : "[redacted]";
            return match.Groups["prefix"].Value + replacement;
        });
        redacted = BearerSecret.Replace(redacted, match => match.Groups["prefix"].Value + "[redacted]");
        redacted = CookieArgument.Replace(redacted, match => match.Groups["prefix"].Value + "[redacted]");

        foreach ((string path, string replacement) in SensitivePaths())
            redacted = redacted.Replace(path, replacement, StringComparison.OrdinalIgnoreCase);

        return redacted;
    }

    private static string Diagnostics(
        BugReportContext context,
        BugReportLogs? logs,
        IReadOnlyList<string> logEntries,
        bool truncated)
    {
        var text = new StringBuilder()
            .AppendLine("QX Scripter: " + OneLine(context.Version, 160))
            .AppendLine("OS: " + OneLine(context.OperatingSystem, 160))
            .AppendLine("Architecture: " + OneLine(context.Architecture, 80))
            .AppendLine("Runtime: " + OneLine(context.Runtime, 80))
            .AppendLine("G-Earth: " + OneLine(context.GEarth, 160))
            .AppendLine("Client: " + OneLine(context.Client, 160))
            .AppendLine("MCP: " + OneLine(context.Mcp, 160));

        if (logs is null)
            return text.AppendLine().Append("Application logs were not included.").ToString();

        text.AppendLine()
            .Append("Application logs: last 24 hours, tokens and local profile paths removed.");
        if (logs.Found == 0)
            return text.AppendLine().Append(logs.Truncated
                ? "Application logs could not be read completely."
                : "No matching log entries were found.").ToString();

        text.AppendLine()
            .Append("Entries found: ").Append(logs.Found)
            .Append("; included: ").Append(logEntries.Count)
            .Append("; truncated: ").Append(truncated ? "yes" : "no").AppendLine()
            .AppendLine("---");
        if (truncated)
            text.AppendLine(Omitted);
        return text.Append(string.Join(Environment.NewLine + Environment.NewLine, logEntries)).ToString();
    }

    private static void ReadLog(string path, LogCollector collector)
    {
        if (!File.Exists(path))
            return;

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using MemoryStream snapshot = Snapshot(stream, MaxLogFileBytes, out bool clipped, out bool incomplete);
            if (clipped || incomplete)
                collector.MarkTruncated();
            using var reader = new StreamReader(snapshot, Encoding.UTF8, true);
            if (clipped)
                reader.ReadLine();
            StringBuilder? current = null;
            DateTime currentTime = default;
            bool currentProblem = false;
            bool currentShortened = false;

            while (reader.ReadLine() is { } line)
            {
                Match match = LogStart.Match(line);
                if (match.Success && DateTime.TryParseExact(
                        match.Groups["time"].Value,
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime at))
                {
                    AddCurrent(collector, current, currentTime, currentProblem, currentShortened);
                    current = new StringBuilder();
                    currentShortened = !AppendLine(current, line);
                    currentTime = at;
                    string level = match.Groups["level"].Value;
                    currentProblem = level.Equals("Warning", StringComparison.OrdinalIgnoreCase)
                        || level.Equals("Warn", StringComparison.OrdinalIgnoreCase)
                        || level.Equals(nameof(DiagLevel.Error), StringComparison.OrdinalIgnoreCase);
                }
                else if (current is not null)
                {
                    currentShortened |= !AppendLine(current, line);
                }
            }

            AddCurrent(collector, current, currentTime, currentProblem, currentShortened);
        }
        catch (IOException)
        {
            collector.MarkTruncated();
        }
        catch (UnauthorizedAccessException)
        {
            collector.MarkTruncated();
        }
    }

    private static void ReadCrashLog(string? path, DateTime cutoff, LogCollector collector)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            DateTime at = File.GetLastWriteTime(path);
            if (at < cutoff)
                return;
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using MemoryStream snapshot = Snapshot(stream, MaxCrashFileBytes, out bool clipped, out bool incomplete);
            if (clipped || incomplete)
                collector.MarkTruncated();
            using var reader = new StreamReader(snapshot, Encoding.UTF8, true);
            if (clipped)
                reader.ReadLine();
            string text = reader.ReadToEnd().Trim();
            if (text.Length > 0)
                collector.Add(
                    at,
                    true,
                    $"[{at:yyyy-MM-dd HH:mm:ss}] {DiagLevel.Error} crash{Environment.NewLine}{text}",
                    clipped);
        }
        catch (IOException)
        {
            collector.MarkTruncated();
        }
        catch (UnauthorizedAccessException)
        {
            collector.MarkTruncated();
        }
    }

    private static MemoryStream Snapshot(
        FileStream stream,
        int maxBytes,
        out bool clipped,
        out bool incomplete)
    {
        long end = stream.Length;
        long start = Math.Max(0, end - maxBytes);
        clipped = start > 0;
        stream.Position = start;

        byte[] buffer = GC.AllocateUninitializedArray<byte>((int)(end - start));
        int read = 0;
        while (read < buffer.Length)
        {
            int count = stream.Read(buffer, read, buffer.Length - read);
            if (count == 0)
                break;
            read += count;
        }

        incomplete = read < buffer.Length;
        return new MemoryStream(buffer, 0, read, false, true);
    }

    private static void AddCurrent(
        LogCollector collector,
        StringBuilder? current,
        DateTime at,
        bool problem,
        bool shortened)
    {
        if (current is not null)
            collector.Add(at, problem, current.ToString(), shortened);
    }

    private static bool AppendLine(StringBuilder target, string line)
    {
        int separatorLength = target.Length == 0 ? 0 : Environment.NewLine.Length;
        int available = MaxRawEntryCharacters - target.Length - separatorLength;
        if (available <= 0)
            return false;

        if (separatorLength > 0)
            target.AppendLine();
        if (line.Length <= available)
        {
            target.Append(line);
            return true;
        }

        target.Append(SafePrefix(line, available));
        return false;
    }

    private static string ShortenEntry(string value, int available)
    {
        if (value.Length <= available)
            return value;
        if (available <= Omitted.Length + 4)
            return SafePrefix(value, available);

        int firstBreak = value.IndexOf('\n');
        if (firstBreak < 0)
            return SafePrefix(value, available);
        string first = value[..firstBreak].TrimEnd('\r');
        int tailLength = available - Omitted.Length - first.Length - 4;
        if (tailLength < 32)
            return SafePrefix(value, available);
        return first + Environment.NewLine + Omitted + Environment.NewLine + SafeTail(value, tailLength);
    }

    private static string OneLine(string value, int maxLength)
    {
        return SafePrefix(CompactLine(value), maxLength);
    }

    private static string CompactLine(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string SafePrefix(string value, int length)
    {
        if (value.Length <= length)
            return value;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;
        return value[..Math.Max(0, length)];
    }

    private static string SafeTail(string value, int length)
    {
        if (value.Length <= length)
            return value;
        int start = value.Length - length;
        if (char.IsLowSurrogate(value[start]))
            start++;
        return value[start..];
    }

    private static IEnumerable<(string Path, string Replacement)> SensitivePaths()
    {
        var paths = new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%"),
            (Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), "%TEMP%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%")
        };
        return paths
            .Where(item => !string.IsNullOrWhiteSpace(item.Item1))
            .SelectMany(item => new[]
            {
                item,
                (item.Item1.Replace('\\', '/'), item.Item2)
            })
            .DistinctBy(item => item.Item1, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(item => item.Item1.Length);
    }

    private sealed class LogCollector
    {
        private readonly DateTime _cutoff;
        private readonly DateTime _upperBound;
        private readonly PriorityQueue<LogRecord, (int Problem, long At, int Order)> _entries = new();
        private int _order;

        public LogCollector(DateTime cutoff, DateTime upperBound)
        {
            _cutoff = cutoff;
            _upperBound = upperBound;
        }

        public int Found { get; private set; }

        public bool Truncated { get; private set; }

        public IEnumerable<LogRecord> Entries => _entries.UnorderedItems.Select(item => item.Element);

        public void Add(DateTime at, bool problem, string text, bool shortened)
        {
            if (at < _cutoff || at > _upperBound)
                return;

            int order = _order++;
            var entry = new LogRecord(at, problem, order, text);
            _entries.Enqueue(entry, (problem ? 1 : 0, at.Ticks, order));
            Found++;
            Truncated |= shortened;
            if (_entries.Count <= MaxRetainedEntries)
                return;

            _entries.Dequeue();
            Truncated = true;
        }

        public void MarkTruncated() => Truncated = true;
    }

    private sealed record LogRecord(
        DateTime At,
        bool Problem,
        int Order,
        string Text);
}
