using System;

namespace Qx.Presentation.ViewModels.Settings;

internal static class SettingsMcpText
{
    const string Bullets = "••••••••";
    const string TokenMarker = "token=";

    public const string RunningHint = "Copy puts the full URL, token and all, on the clipboard.";
    public const string StoppedHint = "Start QX with the port free to expose the MCP server.";
    public const string NotRunning = "not running";

    public static string Mask(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        int at = url.IndexOf(TokenMarker, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? url : string.Concat(url.AsSpan(0, at + TokenMarker.Length), Bullets);
    }

    public static (string Display, string Hint, bool CanCopy) Describe(bool running, string client_url)
    {
        ArgumentNullException.ThrowIfNull(client_url);
        return running ? (Mask(client_url), RunningHint, true) : (NotRunning, StoppedHint, false);
    }

    public static string? CopyUrl(bool running, string client_url) => running ? client_url : null;
}
