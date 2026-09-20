using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.Friends;

namespace Qx.Presentation.Composition.Areas;

public static class FriendsArea
{
    public static IServiceCollection AddFriendsArea(this IServiceCollection services) =>
        services.AddPage<FriendsViewModel>(PageKey.Friends);
}
