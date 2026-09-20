using Avalonia.Input;
using Qx.Presentation.Input;

namespace Qx.Desktop.Input;

internal static class KeyMap
{
    public static ChordKey? FromKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ChordKey.A + (key - Key.A),
        >= Key.D0 and <= Key.D9 => ChordKey.D0 + (key - Key.D0),
        >= Key.F1 and <= Key.F12 => ChordKey.F1 + (key - Key.F1),
        Key.Tab => ChordKey.Tab,
        Key.Escape => ChordKey.Escape,
        Key.Enter => ChordKey.Enter,
        Key.Delete => ChordKey.Delete,
        Key.OemPlus => ChordKey.Plus,
        Key.OemMinus => ChordKey.Minus,
        Key.NumPad0 => ChordKey.NumPad0,
        Key.Add => ChordKey.Add,
        Key.Subtract => ChordKey.Subtract,
        Key.Up => ChordKey.Up,
        Key.Down => ChordKey.Down,
        _ => null
    };
}
