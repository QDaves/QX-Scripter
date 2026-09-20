using System.Globalization;
using Avalonia.Controls.Converters;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace Qx.Desktop.Converters;

public sealed class KeyChordText : IValueConverter
{
    static readonly PlatformKeyGestureConverter _platform = new();

    public object? Convert(object? value, Type target_type, object? parameter, CultureInfo culture) => value switch
    {
        null => null,
        string text => text,
        KeyGesture gesture => _platform.Convert(gesture, typeof(string), parameter, culture) is string formatted ? formatted.Replace("Return", "Enter", StringComparison.Ordinal) : null,
        _ => null
    };

    public object? ConvertBack(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
