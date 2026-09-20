using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Qx.Presentation.Mvvm;

namespace Qx.Desktop.Converters;

public sealed class NoticeBrush : IValueConverter
{
    public object? Convert(object? value, Type target_type, object? parameter, CultureInfo culture)
    {
        if (value is not NoticeSeverity severity || Application.Current is not { } application)
            return null;
        bool soft = string.Equals(parameter as string, "soft", StringComparison.OrdinalIgnoreCase);
        string key = severity switch
        {
            NoticeSeverity.Success => soft ? "QxSuccessSoftBrush" : "QxSuccessBrush",
            NoticeSeverity.Warning => soft ? "QxWarningSoftBrush" : "QxWarningBrush",
            NoticeSeverity.Error => soft ? "QxDangerSoftBrush" : "QxDangerBrush",
            _ => soft ? "QxInfoSoftBrush" : "QxInfoBrush"
        };
        return application.TryGetResource(key, application.ActualThemeVariant, out object? resource) && resource is IBrush brush ? brush : null;
    }

    public object? ConvertBack(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
