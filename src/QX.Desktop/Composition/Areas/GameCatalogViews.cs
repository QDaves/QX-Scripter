using Qx.Desktop.Views.GameCatalog;
using Qx.Presentation.ViewModels.GameCatalog;

namespace Qx.Desktop.Composition.Areas;

static class GameCatalogViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<GameDataViewModel>(static () => new GameDataView());
}
