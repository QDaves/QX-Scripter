namespace Qx.Presentation.Input;

public static class CommandMatcher
{
    public static bool Matches(string title, string query)
    {
        ArgumentNullException.ThrowIfNull(title);
        int at = 0;
        foreach (char wanted in query ?? "")
        {
            if (char.IsWhiteSpace(wanted))
                continue;
            at = IndexOf(title, wanted, at);
            if (at < 0)
                return false;
            at++;
        }
        return true;
    }

    public static int Score(string title, string query)
    {
        string trimmed = (query ?? "").Trim();
        if (trimmed.Length == 0)
            return 0;
        if (title.StartsWith(trimmed, StringComparison.CurrentCultureIgnoreCase))
            return 0;
        if (title.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase))
            return 1;
        return 2;
    }

    public static IReadOnlyList<AppCommand> Filter(IEnumerable<AppCommand> commands, string query) =>
        [.. commands
            .Where(command => command.InPalette && command.IsAvailable() && Matches(command.Title, query))
            .Select((command, order) => (command, order))
            .OrderBy(pair => Score(pair.command.Title, query))
            .ThenBy(pair => pair.order)
            .Select(pair => pair.command)];

    static int IndexOf(string title, char wanted, int start)
    {
        for (int index = start; index < title.Length; index++)
        {
            if (char.ToUpperInvariant(title[index]) == char.ToUpperInvariant(wanted))
                return index;
        }
        return -1;
    }
}
