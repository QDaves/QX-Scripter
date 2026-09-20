using Qx.Desktop.Views.CommandPalette;
using Qx.Presentation.ViewModels.CommandPalette;

namespace Qx.Desktop.Composition.Areas;

static class CommandPaletteViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<CommandPaletteViewModel>(static () => new CommandPaletteView());
}
