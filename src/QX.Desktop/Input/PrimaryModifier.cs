using Avalonia;
using Avalonia.Input;

namespace Qx.Desktop.Input;

public static class PrimaryModifier
{
    public static KeyModifiers Current =>
        Application.Current?.PlatformSettings is { } settings
            ? settings.HotkeyConfiguration.CommandModifiers
            : OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
}
