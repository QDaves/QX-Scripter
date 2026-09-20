using System.Globalization;
using Avalonia.Data.Converters;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed class PanelPercent : IValueConverter
{
    public object Convert(object? value, Type target_type, object? parameter, CultureInfo culture)
    {
        double fraction = value is double share && double.IsFinite(share) ? Math.Clamp(share, 0, 1) : 0;
        return string.Create(CultureInfo.InvariantCulture, $"{Math.Round(fraction * 100)}%");
    }

    public object ConvertBack(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
