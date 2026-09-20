using System.Globalization;
using Avalonia.Data.Converters;

namespace Qx.Desktop.Converters;

public sealed class TimeText : IValueConverter
{
    public object? Convert(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset at ? at.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "";

    public object ConvertBack(object? value, Type target_type, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
