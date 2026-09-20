namespace Qx.Presentation.ViewModels.Settings;

public sealed record ShortcutRow(string Title, IReadOnlyList<string> Keys, string? Note)
{
    public bool HasKeys => Keys.Count > 0;

    public bool HasNote => !string.IsNullOrEmpty(Note);
}

public sealed record ShortcutGroup(string Name, IReadOnlyList<ShortcutRow> Rows);
