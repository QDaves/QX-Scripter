using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Microsoft.Extensions.DependencyInjection;
using Qx.Desktop.Editor;
using Qx.Desktop.Input;
using Qx.Desktop.Platform.Portable;
using Qx.Desktop.Platform.Windows;
using Qx.Desktop.Services;
using Qx.Desktop.Views.Shell;
using Qx.Diagnostics;
using Qx.Presentation.Composition;
using Qx.Presentation.Input;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Lifecycle;
using Qx.Presentation.Services.Logging;
using Qx.Presentation.Services.Notifications;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Settings;
using Qx.Presentation.Services.Status;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Services.Updates;
using Qx.Presentation.Threading;
using Qx.Presentation.ViewModels.Shell;
using BitmapCache = Qx.Desktop.Services.BitmapCache;

namespace Qx.Desktop.Composition;

public sealed class DesktopComposition : IDisposable
{
    readonly ServiceProvider _services;
    readonly DiagnosticsHub _diagnostics;
    readonly LaunchOptions _launch;
    IClassicDesktopStyleApplicationLifetime? _lifetime;
    KeyRouter? _router;
    MainWindow? _shell;

    DesktopComposition(ServiceProvider services, DiagnosticsHub diagnostics, LaunchOptions launch, ViewRegistry views)
    {
        _services = services;
        _diagnostics = diagnostics;
        _launch = launch;
        Views = views;
    }

    public IServiceProvider Services => _services;

    public ViewRegistry Views { get; }

    public static DesktopComposition Create(LaunchOptions launch, IAppPaths paths, DiagnosticsHub diagnostics, DesktopCompositionOptions options)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(options);
        var services = new ServiceCollection();
        services.AddSingleton(launch);
        services.AddSingleton(paths);
        services.AddSingleton(options);
        services.AddSingleton(options.Time);
        services.AddSingleton(diagnostics);
        services.AddSingleton<IApplicationLog>(diagnostics);
        services.AddSingleton<AppLifetime>();
        AddDesktopPlatform(services, options);
        IKeyboardState keyboard = KeyboardStateFor(options);
        services.AddSingleton(keyboard);
        var editor = new DeferredEditorBridge();
        services.AddSingleton(editor);
        var runtime = new DesktopRuntime(launch, paths, options.Runtime, options.McpPort, editor, keyboard);
        services.AddSingleton(runtime);
        diagnostics.AttachRuntime(runtime);
        services.AddQxPresentation();
        services.AddQxAreas();
        ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<ISettingsStore>();
        var views = new ViewRegistry();
        DesktopViews.RegisterAll(views, provider.GetRequiredService<RoslynHostProvider>());
        return new DesktopComposition(provider, diagnostics, launch, views);
    }

    public static void RegisterFonts(FontManager fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        fonts.AddFontCollection(new EmbeddedFontCollection(new Uri("fonts:Qx"), new Uri("avares://QX/Assets/Fonts")));
    }

    public void AttachUi(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _diagnostics.AttachDispatcher(_services.GetRequiredService<IUiDispatcher>());
        CrashLog.AttachUi();
        INotificationService notifications = _services.GetRequiredService<INotificationService>();
        CrashLog.UseNotice(text => notifications.Show(text, NoticeSeverity.Error));
        AvaloniaThemeService theme = _services.GetRequiredService<AvaloniaThemeService>();
        theme.Attach(application);
        theme.Apply(SettingsCodec.EffectiveTheme(_services.GetRequiredService<ISettingsStore>().Current));
    }

    public MainWindow CreateShell(IClassicDesktopStyleApplicationLifetime? lifetime)
    {
        var window = new MainWindow();
        _services.GetRequiredService<TopLevelAccessor>().Attach(window);
        ShellWindow host = _services.GetRequiredService<ShellWindow>();
        host.Attach(window, lifetime);
        if (lifetime is not null)
        {
            _lifetime = lifetime;
            lifetime.ShutdownRequested += OnShutdownRequested;
        }
        IAlwaysOn[] subscribers = [.. _services.GetServices<IAlwaysOn>()];
        var shell = _services.GetRequiredService<ShellViewModel>();
        Diag.Info($"Shell created with {subscribers.Length} always-on services.", "app");
        ICommandRegistry registry = _services.GetRequiredService<ICommandRegistry>();
        ShellCommands.Register(
            registry,
            shell,
            _services.GetRequiredService<INavigationService>(),
            _services.GetRequiredService<IPageProvider>(),
            _services.GetRequiredService<ISessionStatusService>(),
            _services.GetRequiredService<IScriptRunRegistry>(),
            _services.GetRequiredService<IScriptWorkspace>(),
            _services.GetRequiredService<PanicKey>(),
            _services.GetRequiredService<EditorPreferences>(),
            _services.GetRequiredService<ICommandPalette>());
        foreach (ICommandContributor contributor in _services.GetServices<ICommandContributor>())
            contributor.Contribute(registry);
        _router = new KeyRouter(registry, _services.GetRequiredService<IKeyScopeState>(), _services.GetRequiredService<GestureFormatter>());
        _router.Attach(window);
        window.Use(shell, Views, _services.GetRequiredService<BitmapCache>(), host, _services.GetRequiredService<ShellCloseCoordinator>(), _services.GetRequiredService<IPageProvider>());
        if (_launch.HostedByGEarth)
        {
            host.PrepareForHost(_services.GetRequiredService<ISettingsStore>().Current.Window);
        }
        else
        {
            PixelPlacement? placement = _services.GetRequiredService<WindowPlacementController>().Restore(window);
            host.Remember(placement?.Maximized == true);
        }
        window.Opened += OnOpened;
        _shell = window;
        return window;
    }

    public void Dispose()
    {
        if (_lifetime is { } lifetime)
            lifetime.ShutdownRequested -= OnShutdownRequested;
        _lifetime = null;
        if (_router is { } router && _shell is { } window)
            router.Detach(window);
        _router = null;
        _shell = null;
        _services.Dispose();
    }

    static void AddDesktopPlatform(IServiceCollection services, DesktopCompositionOptions options)
    {
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<TopLevelAccessor>();
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        services.AddSingleton<ILauncherService, AvaloniaLauncherService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton<AvaloniaThemeService>();
        services.AddSingleton<IThemeService>(static provider => provider.GetRequiredService<AvaloniaThemeService>());
        services.AddSingleton<ShellWindow>();
        services.AddSingleton<IShellWindow>(static provider => provider.GetRequiredService<ShellWindow>());
        services.AddSingleton<BitmapCache>();
        services.AddSingleton<WindowPlacementController>();
        services.AddSingleton<RoslynHostProvider>();
        services.AddSingleton<IEditorWarmup>(static provider => provider.GetRequiredService<RoslynHostProvider>());
        services.AddSingleton<GestureFormatter>();
        services.AddSingleton<IGestureFormatter>(static provider => provider.GetRequiredService<GestureFormatter>());
        if (options.UpdateCheck)
            services.AddSingleton<IReleaseSource, GitHubReleaseSource>();
        else
            services.AddSingleton<IReleaseSource, NoReleaseSource>();
        AddPlatformNatives(services, options);
    }

    static void AddPlatformNatives(IServiceCollection services, DesktopCompositionOptions options)
    {
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IFileRevealer, ExplorerRevealer>();
            if (options.GlobalHotkeys)
                services.AddSingleton<IGlobalHotkeys, Win32GlobalHotkeys>();
            else
                services.AddSingleton<IGlobalHotkeys, NoGlobalHotkeys>();
            return;
        }
        services.AddSingleton<IFileRevealer, FolderRevealer>();
        services.AddSingleton<IGlobalHotkeys, NoGlobalHotkeys>();
    }

    static IKeyboardState KeyboardStateFor(DesktopCompositionOptions options) =>
        OperatingSystem.IsWindows() && options.Runtime == RuntimeProfile.Live ? new Win32KeyboardState() : new NoKeyboardState();

    void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs args)
    {
        var close = _services.GetRequiredService<ShellCloseCoordinator>();
        if (OperatingSystem.IsMacOS())
        {
            args.Cancel = true;
            close.RequestCloseAsync(CloseReason.QuitRequested).Observe("app");
            return;
        }
        close.EndSession();
    }

    void OnOpened(object? sender, EventArgs e)
    {
        if (sender is MainWindow window)
            window.Opened -= OnOpened;
        ShellWindow host = _services.GetRequiredService<ShellWindow>();
        if (_launch.HostedByGEarth)
            host.HideForHost();
        else
            host.RestoreState();
        _services.GetRequiredService<ShellStartup>().RunAsync(_services.GetRequiredService<AppLifetime>().Token).Observe("app");
    }
}
