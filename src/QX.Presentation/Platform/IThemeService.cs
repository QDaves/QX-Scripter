using Qx.Presentation.Services.Settings;

namespace Qx.Presentation.Platform;

public interface IThemeService
{
    ThemeMode Mode { get; }

    bool IsDark { get; }

    event Action<bool>? EffectiveChanged;

    void Apply(ThemeMode mode);

    void Change(ThemeMode mode);
}
