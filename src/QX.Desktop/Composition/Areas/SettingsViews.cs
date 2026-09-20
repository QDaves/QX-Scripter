using Qx.Desktop.Views.Settings;
using Qx.Presentation.ViewModels.Settings;

namespace Qx.Desktop.Composition.Areas;

static class SettingsViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<SettingsViewModel>(static () => new SettingsView());
}
