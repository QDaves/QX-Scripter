using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.ViewModels.Editor;
using Qx.Presentation.ViewModels.ScriptPanels;

namespace Qx.Presentation.Composition.Areas;

public static class ScriptPanelsArea
{
    public static IServiceCollection AddScriptPanelsArea(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IScriptPanelFactory, ScriptPanelFactory>();
    }
}
