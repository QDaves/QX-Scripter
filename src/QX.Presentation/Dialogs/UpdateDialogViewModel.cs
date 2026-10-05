using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Updates;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.Dialogs;

public sealed partial class UpdateDialogViewModel : DialogViewModel<bool>
{
    readonly Release _release;
    readonly ILauncherService _launcher;

    public UpdateDialogViewModel(string installedVersion, Release release, ILauncherService launcher)
    {
        InstalledVersion = installedVersion ?? throw new ArgumentNullException(nameof(installedVersion));
        _release = release ?? throw new ArgumentNullException(nameof(release));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public override string Title => "Update available";

    public override IconKind Icon => IconKind.Update;

    public string Headline => "A newer QX Scripter release is ready.";

    public string ReleaseLine => string.Equals(_release.Name, _release.Tag, StringComparison.OrdinalIgnoreCase) ? "A new version is available." : _release.Name;

    public string InstalledVersion { get; }

    public string AvailableVersion => _release.Version;

    public bool CanOpen => _release.Uri is not null;

    public string Note => CanOpen
        ? "GitHub has the release notes and download."
        : "Open the G-ExtensionStore in G-Earth and update QX Scripter there.";

    public string LaterText => CanOpen ? "Later" : "OK";

    public string OpenText => "View on GitHub";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    public partial string Failure { get; private set; } = "";

    public bool HasFailure => Failure.Length > 0;

    protected override bool DismissResult => false;

    [RelayCommand]
    void Later() => Close(false);

    [RelayCommand]
    async Task OpenAsync(CancellationToken cancellation_token)
    {
        if (_release.Uri is not { } uri)
            return;
        bool opened;
        try
        {
            opened = await _launcher.OpenUriAsync(uri, cancellation_token);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Failure = "Could not open GitHub: " + error.Message;
            return;
        }
        if (opened)
        {
            Close(true);
            return;
        }
        Failure = $"Could not open GitHub: the browser did not open {uri}.";
    }
}
