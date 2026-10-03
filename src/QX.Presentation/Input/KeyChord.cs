namespace Qx.Presentation.Input;

public readonly record struct KeyChord(ChordKey Key, ChordModifiers Modifiers = ChordModifiers.None)
{
    public KeyChord Canonical(bool primaryIsControl) =>
        primaryIsControl && Modifiers.HasFlag(ChordModifiers.Control)
            ? this with { Modifiers = (Modifiers & ~ChordModifiers.Control) | ChordModifiers.Primary }
            : this;
}
