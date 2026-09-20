using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Qx.Desktop.Composition;

namespace Qx.Desktop;

public sealed partial class App : Application
{
    DesktopComposition? _composition;

    public DesktopComposition Composition => _composition ?? throw new InvalidOperationException("The application was started without its composition.");

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public void Use(DesktopComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        _composition = composition;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        DesktopComposition composition = Composition;
        composition.AttachUi(this);
        DataTemplates.Add(composition.Views);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = composition.CreateShell(desktop);
        base.OnFrameworkInitializationCompleted();
    }
}
