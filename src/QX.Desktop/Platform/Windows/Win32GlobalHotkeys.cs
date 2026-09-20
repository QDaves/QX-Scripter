using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Qx.Diagnostics;
using Qx.Presentation.Input;
using Qx.Presentation.Platform;

namespace Qx.Desktop.Platform.Windows;

[SupportedOSPlatform("windows")]
sealed class Win32GlobalHotkeys : IGlobalHotkeys
{
    public bool IsSupported => true;

    public async Task<IDisposable?> TryRegisterAsync(KeyChord chord, Action pressed, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        if (chord.Modifiers.HasFlag(ChordModifiers.Primary))
        {
            Diag.Warn("A global hotkey needs physical modifiers; Primary is not accepted.", "hotkey");
            return null;
        }
        if (HotkeyMap.VirtualKey(chord.Key) is not { } key)
        {
            Diag.Warn($"The key {chord.Key} cannot be a global hotkey on this system.", "hotkey");
            return null;
        }
        var registration = new HotkeyRegistration(HotkeyMap.Modifiers(chord.Modifiers), key, pressed);
        if (await registration.StartAsync(cancellation_token))
            return registration;
        registration.Dispose();
        return null;
    }
}

[SupportedOSPlatform("windows")]
sealed partial class HotkeyRegistration : IDisposable
{
    public const int RegistrationId = 0x5158;
    const uint MessageHotkey = 0x0312;
    const uint MessageQuit = 0x0012;

    readonly uint _modifiers;
    readonly uint _key;
    readonly Action _pressed;
    readonly TaskCompletionSource<bool> _registered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly Thread _thread;
    uint _thread_id;
    int _disposed;

    public HotkeyRegistration(uint modifiers, uint key, Action pressed)
    {
        _modifiers = modifiers;
        _key = key;
        _pressed = pressed;
        _thread = new Thread(Loop) { IsBackground = true, Name = "QX panic key" };
    }

    public async Task<bool> StartAsync(CancellationToken cancellation_token)
    {
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        return await _registered.Task.WaitAsync(cancellation_token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        uint thread = Volatile.Read(ref _thread_id);
        if (thread != 0)
            PostThreadMessageW(thread, MessageQuit, 0, 0);
    }

    void Loop()
    {
        Volatile.Write(ref _thread_id, GetCurrentThreadId());
        if (!RegisterHotKey(0, RegistrationId, _modifiers, _key))
        {
            _registered.TrySetResult(false);
            return;
        }
        _registered.TrySetResult(true);
        try
        {
            while (Volatile.Read(ref _disposed) == 0 && GetMessageW(out NativeMessage message, 0, 0, 0) > 0)
            {
                if (message.Message == MessageHotkey && message.WParam == RegistrationId)
                    _pressed();
            }
        }
        finally
        {
            UnregisterHotKey(0, RegistrationId);
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint window, int id);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int GetMessageW(out NativeMessage message, nint window, uint filter_min, uint filter_max);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessageW(uint thread, uint message, nint w_param, nint l_param);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    struct NativeMessage
    {
        public nint Window;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }
}

static class HotkeyMap
{
    const uint ModifierAlt = 0x0001;
    const uint ModifierControl = 0x0002;
    const uint ModifierShift = 0x0004;
    const uint ModifierNoRepeat = 0x4000;

    public static uint Modifiers(ChordModifiers modifiers)
    {
        uint mapped = ModifierNoRepeat;
        if (modifiers.HasFlag(ChordModifiers.Control))
            mapped |= ModifierControl;
        if (modifiers.HasFlag(ChordModifiers.Alt))
            mapped |= ModifierAlt;
        if (modifiers.HasFlag(ChordModifiers.Shift))
            mapped |= ModifierShift;
        return mapped;
    }

    public static uint? VirtualKey(ChordKey key) => key switch
    {
        >= ChordKey.A and <= ChordKey.Z => (uint)(0x41 + (key - ChordKey.A)),
        >= ChordKey.D0 and <= ChordKey.D9 => (uint)(0x30 + (key - ChordKey.D0)),
        >= ChordKey.F1 and <= ChordKey.F12 => (uint)(0x70 + (key - ChordKey.F1)),
        ChordKey.Tab => 0x09,
        ChordKey.Escape => 0x1B,
        ChordKey.Enter => 0x0D,
        ChordKey.Delete => 0x2E,
        ChordKey.Plus => 0xBB,
        ChordKey.Minus => 0xBD,
        ChordKey.NumPad0 => 0x60,
        ChordKey.Add => 0x6B,
        ChordKey.Subtract => 0x6D,
        ChordKey.Up => 0x26,
        ChordKey.Down => 0x28,
        _ => null
    };
}
