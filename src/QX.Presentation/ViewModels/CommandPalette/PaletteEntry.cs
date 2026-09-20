using Qx.Presentation.Input;

namespace Qx.Presentation.ViewModels.CommandPalette;

public sealed record PaletteEntry(AppCommand Command, string Group, string Title, string Gesture);
