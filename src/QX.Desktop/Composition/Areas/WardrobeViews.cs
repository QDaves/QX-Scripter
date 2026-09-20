using Qx.Desktop.Views.Wardrobe;
using Qx.Presentation.ViewModels.Wardrobe;

namespace Qx.Desktop.Composition.Areas;

static class WardrobeViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<WardrobeViewModel>(static () => new WardrobeView());
}
