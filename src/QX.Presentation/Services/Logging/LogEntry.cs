using Qx.Diagnostics;

namespace Qx.Presentation.Services.Logging;

public sealed record LogEntry(long Sequence, DateTime At, DiagLevel Level, string? Category, string Message)
{
    public string Time => At.ToString("HH:mm:ss");

    public string LevelName => Level switch
    {
        DiagLevel.Trace => "trace",
        DiagLevel.Debug => "debug",
        DiagLevel.Info => "info",
        DiagLevel.Warn => "warning",
        _ => "error"
    };

    public bool IsProblem => Level >= DiagLevel.Warn;

    public string Prefix => string.IsNullOrWhiteSpace(Category) ? $"[{LevelName}]" : $"[{LevelName}] [{Category.Trim()}]";

    public string CopyText => $"[{At:yyyy-MM-dd HH:mm:ss}] {Prefix} {Message}";
}
