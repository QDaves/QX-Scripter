using Qx.Desktop.Views.About;
using Qx.Presentation.ViewModels.About;

namespace Qx.Desktop.Composition.Areas;

static class AboutViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<AboutViewModel>(static () => new AboutView());
}
