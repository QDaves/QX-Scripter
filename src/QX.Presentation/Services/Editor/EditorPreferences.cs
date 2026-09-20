using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Services.Settings;

namespace Qx.Presentation.Services.Editor;

public sealed partial class EditorPreferences : ObservableObject
{
    public const double DefaultFontSize = 13;
    public const double MinimumFontSize = 8;
    public const double MaximumFontSize = 32;
    public const double ZoomStep = 1;

    readonly ISettingsStore _settings;

    public EditorPreferences(ISettingsStore settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        FontSize = settings.Current.EditorFontSize;
        WordWrap = settings.Current.EditorWrap;
    }

    [ObservableProperty]
    public partial double FontSize { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WrapTitle))]
    public partial bool WordWrap { get; private set; }

    public string WrapTitle => WordWrap ? "Stop wrapping editor lines" : "Wrap editor lines";

    public void ZoomIn() => SetFontSize(FontSize + ZoomStep);

    public void ZoomOut() => SetFontSize(FontSize - ZoomStep);

    public void ResetZoom() => SetFontSize(DefaultFontSize);

    public void ToggleWrap()
    {
        WordWrap = !WordWrap;
        _settings.Update(document => document with { EditorWrap = WordWrap });
    }

    void SetFontSize(double size)
    {
        double wanted = Math.Clamp(size, MinimumFontSize, MaximumFontSize);
        if (wanted.Equals(FontSize))
            return;
        FontSize = wanted;
        _settings.Update(document => document with { EditorFontSize = wanted });
    }
}
