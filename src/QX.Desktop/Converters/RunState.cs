using System.Globalization;
using Avalonia.Data.Converters;
using Qx.Desktop.Controls;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Workspace;

namespace Qx.Desktop.Converters;

public sealed class RunState : IValueConverter
{
    public object Convert(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        value is RunPhase phase
            ? phase switch
            {
                RunPhase.Compiling => RunButtonState.Compiling,
                RunPhase.Running => RunButtonState.Running,
                RunPhase.Ready => RunButtonState.Ready,
                RunPhase.Stopping => RunButtonState.Stopping,
                _ => RunButtonState.Idle
            }
            : RunButtonState.Idle;

    public object ConvertBack(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TabState : IValueConverter
{
    public object Convert(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        value is DocumentBadge badge
            ? badge switch
            {
                DocumentBadge.Compiling => TabHeaderState.Compiling,
                DocumentBadge.Running => TabHeaderState.Running,
                DocumentBadge.Armed => TabHeaderState.Armed,
                DocumentBadge.Failed => TabHeaderState.Failed,
                _ => TabHeaderState.Idle
            }
            : TabHeaderState.Idle;

    public object ConvertBack(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
