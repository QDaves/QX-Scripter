using System.Runtime.InteropServices;

namespace Qx.Ui;

/// <summary>
/// Keeps a maximized borderless window inside the monitor's work area.
/// </summary>
/// <remarks>
/// <para>
/// A window with <c>WindowStyle="None"</c> maximizes to the whole monitor rather than to the space
/// left over by the taskbar, so its bottom edge — here the status bar — ends up underneath it.
/// Windows never asks the window how big maximized should be; it announces its own answer through
/// <c>WM_GETMINMAXINFO</c> and expects the window to correct it in place.
/// </para>
/// <para>
/// Answered per monitor rather than from the primary one, so the window is right on a second
/// screen with the taskbar on a different edge, or none at all.
/// </para>
/// </remarks>
public static class WorkAreaMaximize
{
    public const int WmGetMinMaxInfo = 0x0024;

    private const int MonitorDefaultToNearest = 0x00000002;

    /// <summary>Rewrites the maximized size and position that Windows proposed.</summary>
    /// <param name="window">The window being asked about.</param>
    /// <param name="info">The <c>MINMAXINFO</c> the message carried.</param>
    public static void Apply(IntPtr window, IntPtr info)
    {
        IntPtr monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
            return;

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return;

        MinMaxInfo bounds = Marshal.PtrToStructure<MinMaxInfo>(info);

        // Relative to the monitor, not the desktop: a second screen left of the primary one has
        // negative desktop coordinates, and passing those through puts the window off-screen.
        bounds.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
        bounds.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
        bounds.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
        bounds.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;

        // Without the track size the window may still be dragged larger than the work area, which
        // is the same fault arriving by a different route.
        bounds.MaxTrackSize = bounds.MaxSize;

        Marshal.StructureToPtr(bounds, info, fDeleteOld: true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
