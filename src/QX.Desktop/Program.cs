using Avalonia;
using Avalonia.Controls;
using Qx.Desktop.Composition;
using Qx.Desktop.Services;
using Qx.Presentation.Platform;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Logging;

namespace Qx.Desktop;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        ProfileOverrides overrides = ProfileOverrides.FromEnvironment();
        IAppPaths paths = overrides.Paths();
        CrashLog.Install(paths.CrashLogFile);
        using DiagnosticsHub diagnostics = DiagnosticsHub.Start(paths);
        CrashLog.UseLogFlush(diagnostics.FlushNow);
        LaunchOptions launch = LaunchOptions.Parse(args);
        using DesktopComposition composition = DesktopComposition.Create(launch, paths, diagnostics, DesktopCompositionOptions.Live with { McpPort = overrides.McpPort });
        return BuildAvaloniaApp()
            .AfterSetup(builder => ((App)builder.Instance!).Use(composition))
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .ConfigureFonts(DesktopComposition.RegisterFonts)
            .LogToTrace();
}
