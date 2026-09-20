using System.Globalization;
using Avalonia.Data.Converters;
using Qx.Desktop.Controls;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed class PanelStatusTone : IValueConverter
{
    public object Convert(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "compiling…" => StatusTone.Warning,
            "running…" => StatusTone.Running,
            "ready" => StatusTone.Accent,
            "done" => StatusTone.Success,
            "error" => StatusTone.Danger,
            _ => StatusTone.Neutral
        };

    public object ConvertBack(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
