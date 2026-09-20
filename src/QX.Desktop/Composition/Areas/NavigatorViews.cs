using Qx.Desktop.Views.Navigator;
using Qx.Presentation.ViewModels.Navigator;

namespace Qx.Desktop.Composition.Areas;

static class NavigatorViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<NavigatorViewModel>(static () => new NavigatorView());
}
