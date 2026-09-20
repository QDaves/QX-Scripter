using Microsoft.Extensions.DependencyInjection;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Services.Chat;
using Qx.Presentation.ViewModels.Chat;

namespace Qx.Presentation.Composition.Areas;

public static class ChatArea
{
    public static IServiceCollection AddChatArea(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ActivityLog>();
        services.AddSingleton<IAlwaysOn>(static provider => provider.GetRequiredService<ActivityLog>());
        return services.AddPage<ChatViewModel>(PageKey.Chat);
    }
}
