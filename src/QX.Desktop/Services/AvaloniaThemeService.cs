using Avalonia;
using Avalonia.Styling;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Settings;

namespace Qx.Desktop.Services;

internal sealed class AvaloniaThemeService : IThemeService, IDisposable
{
    readonly ISettingsStore _settings;
    Application? _application;

    public AvaloniaThemeService(ISettingsStore settings) =>
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public bool IsDark => Host?.ActualThemeVariant == ThemeVariant.Dark;

    public event Action<bool>? EffectiveChanged;

    public void Apply(ThemeMode mode)
    {
        Mode = mode;
        if (Host is { } host)
            host.RequestedThemeVariant = Variant(mode);
    }

    public void Change(ThemeMode mode)
    {
        Apply(mode);
        bool dark = IsDark;
        _settings.Update(document => document with { Theme = mode, Dark = dark });
    }

    public void Attach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (_application is not null)
            return;
        _application = application;
        application.ActualThemeVariantChanged += OnVariantChanged;
    }

    public void Dispose()
    {
        if (_application is { } application)
            application.ActualThemeVariantChanged -= OnVariantChanged;
        _application = null;
    }

    Application? Host => _application ?? Application.Current;

    static ThemeVariant Variant(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => ThemeVariant.Light,
        ThemeMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };

    void OnVariantChanged(object? sender, EventArgs e) => EffectiveChanged?.Invoke(IsDark);
}
