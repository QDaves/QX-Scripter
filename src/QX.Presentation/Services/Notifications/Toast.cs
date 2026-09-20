using System.Windows.Input;
using Qx.Presentation.Mvvm;

namespace Qx.Presentation.Services.Notifications;

public sealed record Toast(long Id, NoticeSeverity Severity, string Text, string? ActionText, ICommand? Action, DateTimeOffset At);
