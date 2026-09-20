using Microsoft.Extensions.DependencyInjection;
using Qx;
using Qx.Mcp;
using Qx.Presentation.Composition.Areas;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Input;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Drafts;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Services.Library;
using Qx.Presentation.Services.Lifecycle;
using Qx.Presentation.Services.Marketplace;
using Qx.Presentation.Services.Notifications;
using Qx.Presentation.Services.Outfits;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Settings;
using Qx.Presentation.Services.Status;
using Qx.Presentation.Services.Updates;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.ViewModels.Editor;
using Qx.Presentation.Threading;
using Qx.Presentation.ViewModels.Shell;

namespace Qx.Presentation.Composition;

public static class PresentationRegistration
{
    public static IServiceCollection AddQxPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(new PageRegistrations());
        services.AddSingleton<IPageProvider>(static provider => new PageProvider(provider, provider.GetRequiredService<PageRegistrations>().Pages));
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ScriptDocumentFactory>();
        services.AddSingleton<ScriptWorkspace>();
        services.AddSingleton<IScriptWorkspace>(static provider => provider.GetRequiredService<ScriptWorkspace>());
        services.AddSingleton<IWorkspacePresence>(static provider => provider.GetRequiredService<ScriptWorkspace>());
        services.AddSingleton<IWorkspaceSession>(static provider => provider.GetRequiredService<ScriptWorkspace>());
        services.AddSingleton<ICommandRegistry, CommandRegistry>();
        services.AddSingleton<IKeyScopeState, KeyScopeState>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<HotelContext>();
        services.AddSingleton<GameGateway>();
        services.AddSingleton<IGameGateway>(static provider => provider.GetRequiredService<GameGateway>());
        services.AddSingleton<IAlwaysOn>(static provider => provider.GetRequiredService<GameGateway>());
        services.AddSingleton<IScriptFileService, ScriptFileService>();
        services.AddSingleton<IScriptFileCommands, ScriptFileCommands>();
        services.AddSingleton<ScriptLibraryStore>();
        services.AddSingleton<IScriptLibrary>(static provider => provider.GetRequiredService<ScriptLibraryStore>());
        services.AddSingleton<DraftStore>();
        services.AddSingleton<IDraftStore>(static provider => provider.GetRequiredService<DraftStore>());
        services.AddSingleton<ScriptPrompts>();
        services.AddSingleton<IScriptPrompts>(static provider => provider.GetRequiredService<ScriptPrompts>());
        services.AddSingleton<ScriptRunRegistry>();
        services.AddSingleton<IScriptRunRegistry>(static provider => provider.GetRequiredService<ScriptRunRegistry>());
        services.AddSingleton<PanicKey>();
        services.AddSingleton<EditorPreferences>();
        services.AddSingleton<OutputPreferences>();
        services.AddSingleton<EditorBridge>();
        services.AddSingleton<IEditorBridge>(static provider => provider.GetRequiredService<EditorBridge>());
        services.AddSingleton<SessionStatusService>();
        services.AddSingleton<ISessionStatusService>(static provider => provider.GetRequiredService<SessionStatusService>());
        services.AddSingleton<IAlwaysOn>(static provider => provider.GetRequiredService<SessionStatusService>());
        services.AddSingleton<OutfitStore>();
        services.AddSingleton<IOutfitStore>(static provider => provider.GetRequiredService<OutfitStore>());
        services.AddSingleton<ShellCloseCoordinator>();
        services.AddSingleton<HostedLifecyclePolicy>();
        services.AddSingleton<IAlwaysOn>(static provider => provider.GetRequiredService<HostedLifecyclePolicy>());
        services.AddSingleton<IUpdateNoticePresenter, DialogUpdatePresenter>();
        services.AddSingleton(static provider =>
        {
            IReleaseSource releases = provider.GetRequiredService<IReleaseSource>();
            return new UpdateNoticeCoordinator(
                releases.LatestAsync,
                provider.GetRequiredService<ISettingsStore>(),
                provider.GetRequiredService<IShellWindow>(),
                provider.GetRequiredService<IUpdateNoticePresenter>(),
                provider.GetRequiredService<IUiDispatcher>(),
                provider.GetRequiredService<TimeProvider>(),
                ProductVersion.Current,
                provider.GetRequiredService<AppLifetime>().Token);
        });
        services.AddSingleton<IImageService>(static provider => new HabboImageService(
            provider.GetRequiredService<IAppPaths>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IMarketplacePrices>(static provider => new MarketplacePriceService(
            provider.GetRequiredService<HotelContext>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ShellStartup>();
        services.AddPage<WorkspaceViewModel>(PageKey.Editor);
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<ShellViewModel>();
        return services;
    }

    public static IServiceCollection AddPage<TPage>(this IServiceCollection services, PageKey key)
        where TPage : PageViewModel
    {
        ArgumentNullException.ThrowIfNull(services);
        ServiceDescriptor registrations = services.Single(service => service.ServiceType == typeof(PageRegistrations));
        if (registrations.ImplementationInstance is not PageRegistrations pages)
            throw new InvalidOperationException("AddQxPresentation must run before pages are added.");
        pages.Add(key, typeof(TPage));
        services.AddSingleton<TPage>();
        return services;
    }

    public static IServiceCollection AddQxAreas(this IServiceCollection services) =>
        services
            .AddLibraryArea()
            .AddLogArea()
            .AddRoomArea()
            .AddGeneralArea()
            .AddChatArea()
            .AddFriendsArea()
            .AddNavigatorArea()
            .AddInventoryArea()
            .AddWardrobeArea()
            .AddGameCatalogArea()
            .AddSettingsArea()
            .AddAboutArea()
            .AddBugReportsArea()
            .AddScriptPanelsArea()
            .AddApiBrowserArea()
            .AddCommandPaletteArea();
}
