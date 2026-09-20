using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.General;

namespace Qx.Presentation.Composition.Areas;

public static class GeneralArea
{
    public static IServiceCollection AddGeneralArea(this IServiceCollection services) =>
        services.AddPage<GeneralViewModel>(PageKey.General);
}
