using Qx.Presentation.Services.Settings;

namespace Qx.Presentation.Platform;

public static class WindowPlacementMath
{
    public const double MinimumWidth = 820;
    public const double MinimumHeight = 520;
    public const int ReachableWidth = 160;
    public const int ReachableHeight = 40;

    public static PixelPlacement? Restore(WindowPlacement? stored, IReadOnlyList<ScreenArea> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        if (stored is null || screens.Count == 0)
            return null;
        if (!double.IsFinite(stored.Left) || !double.IsFinite(stored.Top) || !double.IsFinite(stored.Width) || !double.IsFinite(stored.Height))
            return null;
        if (stored.Width < 1 || stored.Height < 1)
            return null;
        double scaling = Primary(screens).Scaling;
        int x = (int)Math.Round(stored.Left * scaling);
        int y = (int)Math.Round(stored.Top * scaling);
        int width = (int)Math.Round(stored.Width * scaling);
        int height = (int)Math.Round(stored.Height * scaling);
        if (!IsReachable(x, y, width, height, screens))
            return null;
        return new PixelPlacement(x, y, Math.Max(MinimumWidth, stored.Width), Math.Max(MinimumHeight, stored.Height), stored.Maximized);
    }

    public static WindowPlacement? Capture(PixelPlacement normal_bounds, bool maximized, IReadOnlyList<ScreenArea> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        if (screens.Count == 0 || !double.IsFinite(normal_bounds.Width) || !double.IsFinite(normal_bounds.Height))
            return null;
        if (normal_bounds.Width < 1 || normal_bounds.Height < 1)
            return null;
        double scaling = Primary(screens).Scaling;
        return new WindowPlacement
        {
            Left = normal_bounds.X / scaling,
            Top = normal_bounds.Y / scaling,
            Width = normal_bounds.Width,
            Height = normal_bounds.Height,
            Maximized = maximized
        };
    }

    public static (double Width, double Height) DefaultSize(ScreenArea work_area) =>
        (Math.Clamp(work_area.Width / work_area.Scaling * 0.80, MinimumWidth, 1400),
         Math.Clamp(work_area.Height / work_area.Scaling * 0.86, MinimumHeight, 950));

    static bool IsReachable(int x, int y, int width, int height, IReadOnlyList<ScreenArea> screens)
    {
        foreach (ScreenArea screen in screens)
        {
            int left = Math.Max(x, screen.X);
            int top = Math.Max(y, screen.Y);
            int right = Math.Min(x + width, screen.X + screen.Width);
            int bottom = Math.Min(y + height, screen.Y + screen.Height);
            if (right - left >= ReachableWidth * screen.Scaling && bottom - top >= ReachableHeight * screen.Scaling)
                return true;
        }
        return false;
    }

    static ScreenArea Primary(IReadOnlyList<ScreenArea> screens) =>
        screens.FirstOrDefault(screen => screen.IsPrimary) is { Width: > 0 } primary ? primary : screens[0];
}
