namespace Qx.Ui;

public sealed record ApplicationLogEntry(
    string Message,
    string Level,
    string? Category,
    OutputLevel Severity,
    DateTime At)
{
    public string Time { get; } = At.ToString("HH:mm:ss");

    public string Prefix { get; } = string.IsNullOrWhiteSpace(Category)
        ? $"[{Level}]"
        : $"[{Level}] [{Category.Trim()}]";

    public string CopyText { get; } = string.IsNullOrWhiteSpace(Category)
        ? $"[{At:yyyy-MM-dd HH:mm:ss}] [{Level}] {Message}"
        : $"[{At:yyyy-MM-dd HH:mm:ss}] [{Level}] [{Category.Trim()}] {Message}";
}
