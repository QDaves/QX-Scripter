using System.IO;
using System.Windows;
using System.Windows.Threading;
using Qx.Interception.GEarth;

namespace Qx.Ui;

public partial class App : Application
{
    public static UiSettings Settings { get; } = new();
    public static ThemeManager Theme { get; } = new(Settings);

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log((ex.ExceptionObject as Exception)?.ToString() ?? "unknown");

        Theme.Load();
        base.OnStartup(e);

        GEarthOptions options = GEarthOptions.Parse(e.Args, new GEarthOptions
        {
            Title = "QX Scripter",
            Author = "QDave",
            Description = "C# scripting console for Habbo",
            OnClickUsed = true,
            Port = 9092
        });
        bool launched_by_gearth = options.IsLaunchedByGEarth;
        options.SearchPorts = !launched_by_gearth;

        var window = new MainWindow(options, launched_by_gearth);
        MainWindow = window;
        if (launched_by_gearth)
            window.ShowActivated = false;
        window.Show();
        if (launched_by_gearth)
            window.HideForGEarth();
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception.ToString());
    }

    private static void Log(string message)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "qx_crash.log"),
                DateTime.Now + "\n" + message);
        }
        catch
        {
        }
    }
}
