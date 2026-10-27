using Avalonia.Controls;
using Avalonia.Interactivity;
using Qx.Presentation.ViewModels.Library;

namespace Qx.Desktop.Views.Library;

public sealed partial class CommunityRunDialogView : UserControl
{
    public CommunityRunDialogView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is CommunityRunDialogViewModel model)
            model.Start();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (DataContext is CommunityRunDialogViewModel model)
            model.Dispose();
        base.OnUnloaded(e);
    }
}
