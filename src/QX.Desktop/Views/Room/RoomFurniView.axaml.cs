using System.ComponentModel;
using Avalonia.Controls;
using Qx.Presentation.ViewModels.Room;

namespace Qx.Desktop.Views.Room;

public sealed partial class RoomFurniView : UserControl
{
    public RoomFurniView() => InitializeComponent();

    void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        if (DataContext is not RoomFurniViewModel furni)
        {
            e.Cancel = true;
            return;
        }
        furni.RefreshMenu();
        if (!furni.HasSelection)
            e.Cancel = true;
    }
}
