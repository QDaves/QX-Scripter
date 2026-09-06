using System.Diagnostics;

namespace Qx.Diagnostics;

public static class Diag
{
    public static bool IsBuiltWithDebug =>
#if QX_DEBUG
        true;
#else
        false;
#endif

    public static bool Enabled { get; set; } = IsBuiltWithDebug;

    public static DiagLevel MinLevel { get; set; } = DiagLevel.Trace;

    public static event Action<DiagLevel, string, string?>? Emitted;

    public static void Log(DiagLevel level, string message, string? category = null)
    {
        if (!Enabled || level < MinLevel)
            return;
        Emitted?.Invoke(level, message, category);
    }

    [Conditional("QX_DEBUG")]
    public static void Trace(string message, string? category = null) => Log(DiagLevel.Trace, message, category);

    [Conditional("QX_DEBUG")]
    public static void Debug(string message, string? category = null) => Log(DiagLevel.Debug, message, category);

    public static void Info(string message, string? category = null) => Log(DiagLevel.Info, message, category);

    public static void Warn(string message, string? category = null) => Log(DiagLevel.Warn, message, category);

    public static void Error(string message, string? category = null) => Log(DiagLevel.Error, message, category);
}
