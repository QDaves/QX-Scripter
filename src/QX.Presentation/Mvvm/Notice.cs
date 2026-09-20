namespace Qx.Presentation.Mvvm;

public sealed record Notice(NoticeSeverity Severity, string Text, DateTimeOffset At);
