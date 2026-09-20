using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Input;
using Qx.Presentation.ViewModels.CommandPalette;

namespace Qx.Presentation.Composition.Areas;

public static class CommandPaletteArea
{
    public static IServiceCollection AddCommandPaletteArea(this IServiceCollection services) =>
        services
            .AddSingleton<CommandPaletteViewModel>()
            .AddSingleton<ICommandPalette>(static provider => provider.GetRequiredService<CommandPaletteViewModel>());
}
