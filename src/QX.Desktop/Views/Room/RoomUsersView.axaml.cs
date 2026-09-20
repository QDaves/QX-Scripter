using System.ComponentModel;
using Avalonia.Controls;
using Qx.Presentation.ViewModels.Room;

namespace Qx.Desktop.Views.Room;

public sealed partial class RoomUsersView : UserControl
{
    public RoomUsersView() => InitializeComponent();

    void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        if (DataContext is not RoomPeopleViewModel people)
        {
            e.Cancel = true;
            return;
        }
        if (!people.Selection.HasAny)
        {
            e.Cancel = true;
            return;
        }
        people.RefreshMenu();
    }
}
