using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.About;

namespace Qx.Presentation.Composition.Areas;

public static class AboutArea
{
    public static IServiceCollection AddAboutArea(this IServiceCollection services) =>
        services.AddPage<AboutViewModel>(PageKey.About);
}
