using Qx.Desktop.Views.ApiBrowser;
using Qx.Presentation.ViewModels.ApiBrowser;

namespace Qx.Desktop.Composition.Areas;

static class ApiBrowserViews
{
    public static void Register(ViewRegistry views)
    {
        ArgumentNullException.ThrowIfNull(views);
        views.Register<ApiBrowserViewModel>(static () => new ApiBrowserView());
    }
}
