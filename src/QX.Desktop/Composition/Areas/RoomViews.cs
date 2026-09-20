using Qx.Desktop.Views.Room;
using Qx.Presentation.ViewModels.Room;

namespace Qx.Desktop.Composition.Areas;

static class RoomViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<RoomViewModel>(static () => new RoomView());
}
