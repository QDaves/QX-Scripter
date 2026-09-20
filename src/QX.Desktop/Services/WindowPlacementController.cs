using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Settings;

namespace Qx.Desktop.Services;

public sealed class WindowPlacementController(ISettingsStore settings)
{
    readonly ISettingsStore _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public static IReadOnlyList<ScreenArea> ScreensOf(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var areas = new List<ScreenArea>();
        foreach (Screen screen in window.Screens.All)
        {
            PixelRect work = screen.WorkingArea;
            areas.Add(new ScreenArea(work.X, work.Y, work.Width, work.Height, screen.Scaling, screen.IsPrimary));
        }
        return areas;
    }

    public PixelPlacement? Restore(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        IReadOnlyList<ScreenArea> screens = ScreensOf(window);
        if (WindowPlacementMath.Restore(_settings.Current.Window, screens) is { } placement)
        {
            Apply(window, placement);
            return placement;
        }
        ScreenArea primary = screens.FirstOrDefault(screen => screen.IsPrimary && screen.Width > 0);
        if (primary.Width <= 0 && screens.Count > 0)
            primary = screens[0];
        if (primary.Width > 0)
        {
            (double width, double height) = WindowPlacementMath.DefaultSize(primary);
            window.Width = width;
            window.Height = height;
        }
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return null;
    }

    public static void Apply(Window window, PixelPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = placement.Width;
        window.Height = placement.Height;
        window.Position = new PixelPoint(placement.X, placement.Y);
    }
}
