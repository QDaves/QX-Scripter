using Avalonia;
using Avalonia.Input;
using Qx.Presentation.Input;

namespace Qx.Desktop.Input;

public sealed class GestureFormatter : IGestureFormatter
{
    KeyModifiers? _primary;

    public KeyModifiers Primary
    {
        get
        {
            if (_primary is { } known)
                return known;
            if (Application.Current?.PlatformSettings is not null)
                return (_primary = PrimaryModifier.Current).Value;
            return PrimaryModifier.Current;
        }
    }

    public bool PrimaryIsControl => Primary == KeyModifiers.Control;

    public string Describe(KeyChord chord) => GestureText.Describe(chord, apple_glyphs: Primary == KeyModifiers.Meta);

    public KeyChord? ToChord(Key key, KeyModifiers modifiers)
    {
        if (KeyMap.FromKey(key) is not { } chord_key)
            return null;
        KeyModifiers primary = Primary;
        ChordModifiers mapped = ChordModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Shift))
            mapped |= ChordModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Alt))
            mapped |= ChordModifiers.Alt;
        if (modifiers.HasFlag(primary))
            mapped |= ChordModifiers.Primary;
        if (primary != KeyModifiers.Control && modifiers.HasFlag(KeyModifiers.Control))
            mapped |= ChordModifiers.Control;
        return new KeyChord(chord_key, mapped);
    }
}
