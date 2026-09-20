using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Navigation;
using Qx.Presentation.ViewModels.Library;

namespace Qx.Presentation.Composition.Areas;

public static class LibraryArea
{
    public static IServiceCollection AddLibraryArea(this IServiceCollection services) =>
        services.AddPage<LibraryViewModel>(PageKey.Library);
}
