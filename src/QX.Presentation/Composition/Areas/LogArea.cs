using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.Log;

namespace Qx.Presentation.Composition.Areas;

public static class LogArea
{
    public static IServiceCollection AddLogArea(this IServiceCollection services) =>
        services.AddPage<LogViewModel>(PageKey.Log);
}
