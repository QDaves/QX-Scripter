using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.GameCatalog;
using Qx.Presentation.Threading;
using Qx.Presentation.ViewModels.GameCatalog;

namespace Qx.Presentation.Composition.Areas;

public static class GameCatalogArea
{
    public static IServiceCollection AddGameCatalogArea(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddSingleton(static provider => new ShopCatalog(
                provider.GetRequiredService<IGameGateway>(),
                provider.GetRequiredService<IUiDispatcher>()))
            .AddSingleton<MarketplaceTrade>()
            .AddPage<GameDataViewModel>(PageKey.GameData);
    }
}
