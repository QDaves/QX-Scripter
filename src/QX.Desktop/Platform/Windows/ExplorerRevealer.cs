using System.Diagnostics;
using System.Runtime.Versioning;
using Qx.Diagnostics;
using Qx.Presentation.Platform;

namespace Qx.Desktop.Platform.Windows;

[SupportedOSPlatform("windows")]
sealed class ExplorerRevealer : IFileRevealer
{
    public Task<bool> RevealAsync(string path, CancellationToken cancellation_token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathRooted(path) || path.Contains('"', StringComparison.Ordinal))
        {
            Diag.Warn($"Refused to reveal {path}: the path is not a plain absolute path.", "ui");
            return Task.FromResult(false);
        }
        string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var start = new ProcessStartInfo(explorer)
        {
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = false
        };
        try
        {
            using Process? started = Process.Start(start);
            return Task.FromResult(started is not null);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Diag.Warn($"Could not reveal {path}: {error.Message}", "ui");
            return Task.FromResult(false);
        }
    }
}
