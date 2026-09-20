using System.Globalization;

namespace Qx.Presentation.Services.Status;

public static class RelativeTime
{
    public static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        TimeSpan elapsed = now - at;
        if (elapsed < TimeSpan.FromMinutes(1))
            return "just now";
        if (elapsed < TimeSpan.FromHours(1))
            return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed < TimeSpan.FromDays(1))
            return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed < TimeSpan.FromDays(7))
            return $"{(int)elapsed.TotalDays}d ago";
        return at.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture);
    }

    public static string McpTooltip(SessionStatus status, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (status.IsMcpRunning)
        {
            string activity = status.McpLastRequestUtc is { } last
                ? $"A client last called {Ago(new DateTimeOffset(DateTime.SpecifyKind(last, DateTimeKind.Utc)), now)}."
                : "No client has called yet.";
            return $"MCP server listening on 127.0.0.1:{status.McpPort}. {activity} Settings has the client URL.";
        }
        return status.HasMcpFailure ? status.McpFailure : "MCP server is not running.";
    }
}
