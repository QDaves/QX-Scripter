using Qx.Desktop.Views.Inventory;
using Qx.Presentation.ViewModels.Inventory;

namespace Qx.Desktop.Composition.Areas;

static class InventoryViews
{
    public static void Register(ViewRegistry views)
    {
        ArgumentNullException.ThrowIfNull(views);
        views.Register<InventoryViewModel>(static () => new InventoryView());
        views.Register<SellDialogViewModel>(static () => new SellDialogView());
    }
}
