using Qx.Desktop.Views.Log;
using Qx.Presentation.ViewModels.Log;

namespace Qx.Desktop.Composition.Areas;

static class LogViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<LogViewModel>(static () => new LogView());
}
