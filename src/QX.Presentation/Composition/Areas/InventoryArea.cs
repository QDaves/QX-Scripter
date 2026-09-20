using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.Inventory;

namespace Qx.Presentation.Composition.Areas;

public static class InventoryArea
{
    public static IServiceCollection AddInventoryArea(this IServiceCollection services) =>
        services.AddPage<InventoryViewModel>(PageKey.Inventory);
}
