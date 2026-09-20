using Qx.Desktop.Views.General;
using Qx.Presentation.ViewModels.General;

namespace Qx.Desktop.Composition.Areas;

static class GeneralViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<GeneralViewModel>(static () => new GeneralView());
}
