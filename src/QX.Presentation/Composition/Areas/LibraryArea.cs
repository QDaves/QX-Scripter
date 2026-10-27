using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.Library;
using Qx.Presentation.Services.Library;

namespace Qx.Presentation.Composition.Areas;

public static class LibraryArea
{
    public static IServiceCollection AddLibraryArea(this IServiceCollection services) =>
        services.AddSingleton<CommunityClient>()
            .AddSingleton<CommunityCopies>()
            .AddPage<LibraryViewModel>(PageKey.Library)
            .AddPage<CommunityViewModel>(PageKey.Community);
}
