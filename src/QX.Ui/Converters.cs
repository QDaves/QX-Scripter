using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Qx.Ui;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public bool Collapse { get; set; } = true;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (Invert) flag = !flag;
        return flag ? Visibility.Visible : (Collapse ? Visibility.Collapsed : Visibility.Hidden);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility v && v == Visibility.Visible;
}

public sealed class NotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool has = value is not null;
        if (Invert) has = !has;
        return has ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Green while somebody is online, grey while they are not.
/// </summary>
/// <remarks>
/// A dot rather than the word "offline" in every row: it is read at a glance and costs no column
/// width. The brushes are looked up rather than fixed so the light theme is not left with a colour
/// chosen for the dark one.
/// </remarks>
public sealed class OnlineBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Application.Current?.TryFindResource(
            value is true ? "QxSuccessBrush" : "QxTextMutedBrush") ?? Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
