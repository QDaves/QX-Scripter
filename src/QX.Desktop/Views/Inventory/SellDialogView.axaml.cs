using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Qx.Presentation.ViewModels.Inventory;

namespace Qx.Desktop.Views.Inventory;

public sealed partial class SellDialogView : UserControl
{
    public SellDialogView() => InitializeComponent();

    void OnPriceKey(object? sender, KeyEventArgs args)
    {
        if (sender is not TextBox { DataContext: SellRowViewModel row })
            return;
        if (args.Key == Key.Enter)
        {
            row.Commit();
            args.Handled = true;
            return;
        }
        if (args.Key != Key.Escape)
            return;
        row.Revert();
        args.Handled = true;
    }

    void OnPriceLost(object? sender, RoutedEventArgs args)
    {
        if (sender is TextBox { DataContext: SellRowViewModel row })
            row.Commit();
    }
}
