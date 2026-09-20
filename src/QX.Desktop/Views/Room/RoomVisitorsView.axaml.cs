using System.ComponentModel;
using Avalonia.Controls;
using Qx.Presentation.ViewModels.Room;

namespace Qx.Desktop.Views.Room;

public sealed partial class RoomVisitorsView : UserControl
{
    public RoomVisitorsView() => InitializeComponent();

    void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        if (DataContext is not RoomPeopleViewModel people || !people.Selection.HasAny)
        {
            e.Cancel = true;
            return;
        }
        people.RefreshMenu();
    }
}
