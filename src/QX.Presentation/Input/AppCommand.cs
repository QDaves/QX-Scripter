using System.Windows.Input;

namespace Qx.Presentation.Input;

public sealed record AppCommand(
    string Id,
    string Title,
    CommandGroup Group,
    ICommand Command,
    Func<bool> IsAvailable,
    IReadOnlyList<KeyChord> Chords,
    KeyRoute Route = KeyRoute.Bubble,
    bool InPalette = true,
    KeyScope Scope = KeyScope.Anywhere,
    string? GestureText = null)
{
    public KeyChord? PrimaryChord => Chords.Count > 0 ? Chords[0] : null;
}
