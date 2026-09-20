namespace Qx.Presentation.Input;

public sealed class CommandRegistry(IGestureFormatter gestures) : ICommandRegistry
{
    readonly List<AppCommand> _commands = [];
    readonly List<KeyChord[]> _chords = [];

    public IReadOnlyList<AppCommand> Commands => _commands;

    public void Register(AppCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_commands.Any(existing => string.Equals(existing.Id, command.Id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Command {command.Id} is already registered.");
        KeyChord[] canonical = [.. command.Chords.Select(chord => chord.Canonical(gestures.PrimaryIsControl))];
        foreach (KeyChord chord in canonical)
        {
            int taken = _chords.FindIndex(chords => chords.Contains(chord));
            if (taken >= 0)
                throw new InvalidOperationException($"{chord} is already bound to {_commands[taken].Id}.");
        }
        _commands.Add(command);
        _chords.Add(canonical);
    }

    public void Update(string id, Func<AppCommand, AppCommand> change)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(change);
        int index = _commands.FindIndex(existing => string.Equals(existing.Id, id, StringComparison.Ordinal));
        if (index < 0)
            return;
        AppCommand updated = change(_commands[index]);
        if (!string.Equals(updated.Id, id, StringComparison.Ordinal))
            throw new InvalidOperationException($"Command {id} cannot be renamed to {updated.Id}.");
        KeyChord[] canonical = [.. updated.Chords.Select(chord => chord.Canonical(gestures.PrimaryIsControl))];
        foreach (KeyChord chord in canonical)
        {
            int taken = _chords.FindIndex(chords => chords.Contains(chord));
            if (taken >= 0 && taken != index)
                throw new InvalidOperationException($"{chord} is already bound to {_commands[taken].Id}.");
        }
        _commands[index] = updated;
        _chords[index] = canonical;
    }

    public AppCommand? Find(KeyChord chord, KeyRoute route)
    {
        KeyChord wanted = chord.Canonical(gestures.PrimaryIsControl);
        for (int index = 0; index < _commands.Count; index++)
        {
            if (_commands[index].Route == route && _chords[index].Contains(wanted))
                return _commands[index];
        }
        return null;
    }
}
