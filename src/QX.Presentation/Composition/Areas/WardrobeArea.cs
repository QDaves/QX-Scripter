using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.Wardrobe;

namespace Qx.Presentation.Composition.Areas;

public static class WardrobeArea
{
    public static IServiceCollection AddWardrobeArea(this IServiceCollection services) =>
        services.AddPage<WardrobeViewModel>(PageKey.Wardrobe);
}
