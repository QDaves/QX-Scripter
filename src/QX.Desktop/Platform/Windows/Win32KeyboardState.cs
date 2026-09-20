using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Qx.Presentation.Platform;

namespace Qx.Desktop.Platform.Windows;

[SupportedOSPlatform("windows")]
sealed partial class Win32KeyboardState : IKeyboardState
{
    const int VirtualKeyShift = 0x10;
    const short PressedMask = unchecked((short)0x8000);

    public bool IsSupported => true;

    public bool IsShiftDown() => (GetAsyncKeyState(VirtualKeyShift) & PressedMask) != 0;

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int virtual_key);
}
