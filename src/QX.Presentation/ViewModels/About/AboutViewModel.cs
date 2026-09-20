using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Threading;

namespace Qx.Presentation.ViewModels.About;

public sealed class AboutViewModel : PageViewModel
{
    readonly ILauncherService _launcher;

    public AboutViewModel(ILauncherService launcher, IUiDispatcher dispatcher, TimeProvider time)
        : base(PageKey.About)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(time);

        Subtitle = "What this is and what it is made of.";
        Notices = Own(new NoticeLine(dispatcher, time));
        VersionText = $"Version {ProductVersion.Current}";
        Author = Credit("QDave", "QDaves");
        Xabbo = Credit("b7", "b7c");
        Contributors =
        [
            Credit("hasancodedit", "hasancodedit"),
            Credit("JoaninhaJNS", "JoaninhaJNS"),
            Credit("DarkStar851", "scottstamp")
        ];
        Repository = External(new Uri($"https://github.com/{ProjectLinks.Repository}"), "Open the QX Scripter repository on GitHub");
        Releases = External(ProjectLinks.Releases, "Open the QX Scripter releases on GitHub");
        State.ShowReady();
    }

    public NoticeLine Notices { get; }

    public string Wordmark => "Scripter";

    public string VersionText { get; }

    public string Description => "A C# scripting console for Habbo that runs as a G-Earth extension.";

    public string ThanksBefore => "Special thanks to ";

    public string ThanksAfter => " and his work on xabbo.";

    public AboutLink Author { get; }

    public AboutLink Xabbo { get; }

    public IReadOnlyList<AboutLink> Contributors { get; }

    public AboutLink Repository { get; }

    public AboutLink Releases { get; }

    public static string OpenFailure(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return $"Could not open your browser. Visit {url.AbsoluteUri} instead.";
    }

    protected override void OnDeactivated() => Notices.Clear();

    AboutLink Credit(string name, string handle) =>
        new(name, new Uri($"https://github.com/{handle}"), $"Open {name} on GitHub", OpenAsync);

    AboutLink External(Uri url, string tooltip) =>
        new(url.Host + url.AbsolutePath, url, tooltip, OpenAsync);

    async Task OpenAsync(AboutLink link, CancellationToken cancellation_token)
    {
        Notices.Clear();
        if (!await _launcher.OpenUriAsync(link.Url, cancellation_token))
            Notices.Show(NoticeSeverity.Error, OpenFailure(link.Url));
    }
}
