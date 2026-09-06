using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Qx.Ui;

/// <summary>
/// Asks the window manager to round the window's corners.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="System.Windows.Shell.WindowChrome"/> corner radius only shapes what WPF draws
/// inside the window; the frame, the shadow and the area the compositor clips to stay square, so a
/// rounded border over a square frame shows its own corners cut off. The rounding has to be asked
/// of the window manager instead.
/// </para>
/// <para>
/// Windows 11 build 22000 added the attribute. Older builds return a failure code, which is
/// ignored: there the window stays square, which is what every other window there looks like.
/// </para>
/// </remarks>
public static class WindowCorners
{
    private const int WindowCornerPreference = 33;

    private enum CornerPreference
    {
        Default = 0,
        DoNotRound = 1,

        /// <summary>The full radius, as used by dialogs.</summary>
        Round = 2,

        /// <summary>A smaller radius. Enough to soften the shape without rounding off the chrome.</summary>
        RoundSmall = 3
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>Rounds the window once it has a handle, and does nothing where that is unsupported.</summary>
    public static void Round(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (new WindowInteropHelper(window).Handle is var handle && handle != 0)
        {
            Apply(handle);
            return;
        }

        window.SourceInitialized += OnSourceInitialized;

        void OnSourceInitialized(object? sender, EventArgs e)
        {
            window.SourceInitialized -= OnSourceInitialized;
            Apply(new WindowInteropHelper(window).Handle);
        }
    }

    private static void Apply(nint handle)
    {
        if (handle == 0)
            return;

        int preference = (int)CornerPreference.RoundSmall;
        try
        {
            DwmSetWindowAttribute(handle, WindowCornerPreference, ref preference, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}
