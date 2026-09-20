using Qx.Desktop.Views.ScriptPanels;
using Qx.Presentation.ViewModels.ScriptPanels;

namespace Qx.Desktop.Composition.Areas;

static class ScriptPanelsViews
{
    public static void Register(ViewRegistry views)
    {
        ArgumentNullException.ThrowIfNull(views);
        views.Register<ScriptPanelViewModel>(static () => new ScriptPanelView());
    }
}
