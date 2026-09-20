using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.ViewModels.ApiBrowser;
using Qx.Presentation.ViewModels.Editor;

namespace Qx.Presentation.Composition.Areas;

public static class ApiBrowserArea
{
    public static IServiceCollection AddApiBrowserArea(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IApiBrowserFactory, ApiBrowserFactory>();
    }
}
