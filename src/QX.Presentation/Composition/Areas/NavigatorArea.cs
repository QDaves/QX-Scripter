using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.Navigator;

namespace Qx.Presentation.Composition.Areas;

public static class NavigatorArea
{
    public static IServiceCollection AddNavigatorArea(this IServiceCollection services) =>
        services.AddPage<NavigatorViewModel>(PageKey.Navigator);
}
