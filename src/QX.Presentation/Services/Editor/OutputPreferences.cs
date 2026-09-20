using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Services.Settings;

namespace Qx.Presentation.Services.Editor;

public sealed partial class OutputPreferences : ObservableObject
{
    readonly ISettingsStore _settings;

    public OutputPreferences(ISettingsStore settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Wrap = settings.Current.OutputWrap;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WrapTip))]
    public partial bool Wrap { get; private set; }

    public string WrapTip => Wrap ? "Stop wrapping output lines" : "Wrap output lines";

    public void ToggleWrap()
    {
        Wrap = !Wrap;
        _settings.Update(document => document with { OutputWrap = Wrap });
    }
}
