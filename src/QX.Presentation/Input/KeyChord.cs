namespace Qx.Presentation.Input;

public readonly record struct KeyChord(ChordKey Key, ChordModifiers Modifiers = ChordModifiers.None)
{
    public KeyChord Canonical(bool primary_is_control) =>
        primary_is_control && Modifiers.HasFlag(ChordModifiers.Control)
            ? this with { Modifiers = (Modifiers & ~ChordModifiers.Control) | ChordModifiers.Primary }
            : this;
}
