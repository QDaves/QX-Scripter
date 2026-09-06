using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Qx.Ui;

/// <summary>
/// A system-wide key combination that stops every running script.
/// </summary>
/// <remarks>
/// <para>
/// Registered with the OS rather than handled in WPF because of when it is needed: a script that
/// is misbehaving is misbehaving while the user is looking at the game, not at QX. Ordinary key
/// handling never sees that press — the Habbo client has the focus.
/// </para>
/// <para>
/// A failure to register is not an error worth interrupting anyone for. Another application may
/// already hold the combination, and QX still has its per-tab stop; the caller is told so it can
/// say so quietly and carry on.
/// </para>
/// </remarks>
public sealed class PanicHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int Id = 0x5158;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    // Ctrl+Alt+Shift+F12: three modifiers and a key nothing else claims, because the cost of a
    // clash here is a hotkey that silently belongs to another program.
    private const uint Modifiers = ModControl | ModAlt | ModShift | ModNoRepeat;
    private const uint VkF12 = 0x7B;

    /// <summary>How the combination is written for a tooltip or the shortcut list.</summary>
    public const string Gesture = "Ctrl+Alt+Shift+F12";

    private readonly Action _pressed;
    private HwndSource? _source;
    private bool _registered;

    private PanicHotkey(Action pressed) => _pressed = pressed;

    /// <summary>
    /// Registers the combination for a window.
    /// </summary>
    /// <param name="window">The window whose handle receives the message; it must have one.</param>
    /// <param name="pressed">Runs on the UI thread when the combination is pressed.</param>
    /// <returns>The registration, or null when the combination could not be claimed.</returns>
    public static PanicHotkey? Register(Window window, Action pressed)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(pressed);

        if (PresentationSource.FromVisual(window) is not HwndSource source)
            return null;

        var hotkey = new PanicHotkey(pressed) { _source = source };
        if (!RegisterHotKey(source.Handle, Id, Modifiers, VkF12))
            return null;

        hotkey._registered = true;
        source.AddHook(hotkey.OnMessage);
        return hotkey;
    }

    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (message != WmHotkey || w.ToInt32() != Id)
            return IntPtr.Zero;

        handled = true;
        _pressed();
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is not { } source)
            return;

        source.RemoveHook(OnMessage);
        if (_registered)
            UnregisterHotKey(source.Handle, Id);
        _registered = false;
        _source = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
