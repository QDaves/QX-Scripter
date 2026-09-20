namespace Qx.Presentation.Input;

public sealed record CommandSpec(
    string Id,
    string Title,
    CommandGroup Group,
    IReadOnlyList<KeyChord> Chords,
    KeyRoute Route = KeyRoute.Bubble,
    KeyScope Scope = KeyScope.Anywhere,
    bool InPalette = true,
    string? GestureText = null);
