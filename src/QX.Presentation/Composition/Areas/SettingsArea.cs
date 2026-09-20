using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.Settings;

namespace Qx.Presentation.Composition.Areas;

public static class SettingsArea
{
    public static IServiceCollection AddSettingsArea(this IServiceCollection services) =>
        services.AddPage<SettingsViewModel>(PageKey.Settings);
}
