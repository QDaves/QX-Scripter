using Qx.Diagnostics;
using Xunit;

namespace QX.Tests;

public sealed class BugReportTests
{
    [Fact]
    public void Collects_rotated_multiline_and_crash_logs_from_the_last_day()
    {
        string directory = Directory.CreateTempSubdirectory("qx-bug-report-").FullName;
        try
        {
            DateTime now = new(2026, 8, 31, 12, 0, 0);
            string log = Path.Combine(directory, "qx.log");
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            File.WriteAllText(
                log + ".1",
                $"[2026-08-30 10:59:59] Info app too-old{Environment.NewLine}"
                + $"[2026-08-30 12:00:00] Warning mcp token=rotated-secret{Environment.NewLine}"
                + $"stack at {profile}\\source.cs:12{Environment.NewLine}");
            File.WriteAllText(
                log,
                $"malformed prefix{Environment.NewLine}"
                + $"[2026-08-31 11:00:00] Info gearth connected{Environment.NewLine}"
                + $"[2026-08-31 11:30:00] Error game Authorization: Bearer live-secret{Environment.NewLine}"
                + $"continuation line{Environment.NewLine}");
            string crash = Path.Combine(directory, "qx_crash.log");
            File.WriteAllText(crash, "crashed with api_key=crash-secret");
            File.SetLastWriteTime(crash, now.AddMinutes(-5));

            BugReportLogs result = BugReport.Collect(log, crash, now);

            Assert.Equal(4, result.Found);
            Assert.Equal(4, result.Included);
            Assert.False(result.Truncated);
            Assert.DoesNotContain("too-old", result.Text);
            Assert.Contains("continuation line", result.Text);
            Assert.Contains("[redacted]", result.Text);
            Assert.DoesNotContain("rotated-secret", result.Text);
            Assert.DoesNotContain("live-secret", result.Text);
            Assert.DoesNotContain("crash-secret", result.Text);
            Assert.DoesNotContain(profile, result.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("%USERPROFILE%", result.Text);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Keeps_problems_and_newest_context_when_the_day_is_too_large()
    {
        string directory = Directory.CreateTempSubdirectory("qx-bug-report-").FullName;
        try
        {
            DateTime now = new(2026, 8, 31, 12, 0, 0);
            string log = Path.Combine(directory, "qx.log");
            var lines = new List<string>
            {
                "[2026-08-31 00:00:00] Error game older-parser-error",
                new string('s', 5000),
                "[2026-08-31 10:00:00] Error game newer-error"
            };
            lines.AddRange(Enumerable.Range(0, 100).Select(index =>
                $"[2026-08-31 11:{index / 60:00}:{index % 60:00}] Info app info-{index:000} {new string('x', 90)}"));
            File.WriteAllLines(log, lines);

            BugReportLogs result = BugReport.Collect(log, null, now);

            Assert.Equal(102, result.Found);
            Assert.True(result.Truncated);
            Assert.True(result.Included < result.Found);
            Assert.True(result.Text.Length <= 3000);
            Assert.Contains("older-parser-error", result.Text);
            Assert.Contains("newer-error", result.Text);
            Assert.Contains("info-099", result.Text);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Redacts_supported_secret_shapes_and_local_paths()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string input = "https://localhost/mcp?token=query-secret&x=1\n"
            + "Authorization: Bearer authorization-secret\n"
            + "Cookie: session=cookie-secret\n"
            + "api_key=plain-secret password=pass-secret\n"
            + "{\"token\":\"json-secret\",\"api_key\": \"json-api-secret\"}\n"
            + "{\"authorization\":\"Basic json-basic-secret\"}\n"
            + "cookie=cookie-assignment client_secret=oauth-secret refresh_token=refresh-secret\n"
            + "X-MCP-Token=header-secret mcp_token=mcp-secret\n"
            + "Bearer bearer-secret\n"
            + "launch -c gearth-cookie -p 9092\n"
            + Path.Combine(profile, "project", "file.cs") + "\n"
            + Path.Combine(profile, "project", "file.cs").Replace('\\', '/');

        string result = BugReport.Redact(input);

        foreach (string secret in new[]
                 {
                     "query-secret", "authorization-secret", "cookie-secret", "plain-secret",
                     "pass-secret", "json-secret", "json-api-secret", "cookie-assignment",
                     "json-basic-secret", "oauth-secret", "refresh-secret", "header-secret",
                     "mcp-secret", "bearer-secret", "gearth-cookie"
                 })
            Assert.DoesNotContain(secret, result);
        Assert.DoesNotContain(profile, result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(profile.Replace('\\', '/'), result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", result);
        Assert.Contains("%USERPROFILE%", result);
    }

    [Fact]
    public void Issue_uri_prefills_the_form_and_stays_below_the_github_limit()
    {
        var context = new BugReportContext(
            "0.1.0",
            "Windows 11",
            "X64",
            ".NET 10.0.0",
            "connected on port 9092",
            "Unity build UNITY21",
            "running on port 9390");
        var logs = new BugReportLogs(
            Enumerable.Range(0, 100)
                .Select(index => $"[2026-08-31 11:00:{index % 60:00}] Error ENTRY-{index:000} token=unsafe-secret {new string('x', 100)}")
                .ToArray(),
            100,
            false);
        string title = new('ä', 100);
        string description = string.Concat(Enumerable.Repeat("description 🚀 ", 40)).Trim();

        Uri result = BugReport.CreateIssueUri(
            title,
            description,
            context,
            logs);

        Assert.True(result.AbsoluteUri.Length <= BugReport.MaxIssueUrlLength);
        Assert.Equal("bug-report.yml", Query(result, "template"));
        Assert.Equal("[Bug] " + title, Query(result, "title"));
        Assert.Equal(description, Query(result, "description"));
        string diagnostics = Query(result, "diagnostics");
        Assert.Contains("QX Scripter: 0.1.0", diagnostics);
        Assert.DoesNotContain("unsafe-secret", diagnostics);
        Assert.Contains("truncated: yes", diagnostics);
        int included = diagnostics.Split("ENTRY-", StringSplitOptions.None).Length - 1;
        Assert.Contains($"included: {included}", diagnostics);
        Assert.True(included < logs.Included);
        foreach (string entry in logs.Entries.Where(entry => diagnostics.Contains(entry[..entry.IndexOf(" token=", StringComparison.Ordinal)], StringComparison.Ordinal)))
            Assert.Contains(new string('x', 100), diagnostics);
    }

    [Fact]
    public void Issue_uri_rejects_oversized_user_text_instead_of_truncating_it()
    {
        var context = new BugReportContext("1", "Windows", "X64", ".NET", "offline", "none", "stopped");
        string description = string.Concat(Enumerable.Repeat("🚀", 1000));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            BugReport.CreateIssueUri("summary", description, context, null));

        Assert.Contains("too long", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_logs_produce_an_empty_collection()
    {
        string directory = Directory.CreateTempSubdirectory("qx-bug-report-").FullName;
        try
        {
            BugReportLogs result = BugReport.Collect(
                Path.Combine(directory, "missing.log"),
                Path.Combine(directory, "missing-crash.log"),
                DateTime.Now);

            Assert.Equal(BugReportLogs.Empty, result);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Unreadable_logs_preserve_the_incomplete_diagnostic_state()
    {
        string directory = Directory.CreateTempSubdirectory("qx-bug-report-").FullName;
        try
        {
            string log = Path.Combine(directory, "qx.log");
            File.WriteAllText(log, "[2026-08-31 11:00:00] Error game locked");
            using var locked = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.None);

            BugReportLogs result = BugReport.Collect(log, null, new DateTime(2026, 8, 31, 12, 0, 0));

            Assert.Equal(0, result.Found);
            Assert.Equal(0, result.Included);
            Assert.True(result.Truncated);
            Assert.NotEqual(BugReportLogs.Empty, result);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string Query(Uri uri, string name)
    {
        foreach (string pair in uri.Query.TrimStart('?').Split('&'))
        {
            int separator = pair.IndexOf('=');
            if (separator < 0 || !pair[..separator].Equals(name, StringComparison.Ordinal))
                continue;
            return Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        return "";
    }
}
