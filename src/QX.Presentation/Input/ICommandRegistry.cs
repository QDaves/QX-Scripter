namespace Qx.Presentation.Input;

public interface ICommandRegistry
{
    IReadOnlyList<AppCommand> Commands { get; }

    void Register(AppCommand command);

    void Update(string id, Func<AppCommand, AppCommand> change);

    AppCommand? Find(KeyChord chord, KeyRoute route);
}
