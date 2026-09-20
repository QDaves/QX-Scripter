using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Qx.Presentation.ViewModels.General;

namespace Qx.Desktop.Views.General;

public sealed partial class GeneralView : UserControl
{
    public GeneralView() => InitializeComponent();

    void OnNumberKey(object? sender, KeyEventArgs args)
    {
        if (sender is not TextBox { DataContext: GeneralNumberViewModel number })
            return;
        if (args.Key == Key.Enter)
            number.Commit();
        else if (args.Key == Key.Escape)
            number.Revert();
        else
            return;
        args.Handled = true;
    }

    void OnNumberLost(object? sender, RoutedEventArgs args)
    {
        if (sender is TextBox { DataContext: GeneralNumberViewModel number })
            number.Commit();
    }
}
