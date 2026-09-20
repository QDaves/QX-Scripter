using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Input;
using Qx.Presentation.Navigation;
using Qx.Presentation.Services.Room;
using Qx.Presentation.ViewModels.Room;

namespace Qx.Presentation.Composition.Areas;

public static class RoomArea
{
    public static IServiceCollection AddRoomArea(this IServiceCollection services) =>
        services
            .AddSingleton<IRoomSnapshotSource, RoomSnapshotSource>()
            .AddSingleton<IFurniDirections, FurniDirectionCatalog>()
            .AddSingleton<ICommandContributor, RoomCommands>()
            .AddPage<RoomViewModel>(PageKey.Room);
}
