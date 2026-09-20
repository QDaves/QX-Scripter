using System.ComponentModel;
using Avalonia.Controls;
using Qx.Presentation.ViewModels.Room;

namespace Qx.Desktop.Views.Room;

public sealed partial class RoomBansView : UserControl
{
    public RoomBansView() => InitializeComponent();

    void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        if (DataContext is not RoomBansViewModel bans || !bans.Selection.HasAny)
            e.Cancel = true;
    }
}
