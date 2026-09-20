using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Qx.Presentation.Input;
using Qx.Presentation.Navigation;

namespace Qx.Desktop.Input;

public sealed class KeyRouter(ICommandRegistry commands, IKeyScopeState scope, GestureFormatter gestures)
{
    readonly ICommandRegistry _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    readonly IKeyScopeState _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    readonly GestureFormatter _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.AddHandler(InputElement.KeyDownEvent, OnTunnel, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.KeyDownEvent, OnBubble, RoutingStrategies.Bubble);
    }

    public void Detach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.RemoveHandler(InputElement.KeyDownEvent, OnTunnel);
        window.RemoveHandler(InputElement.KeyDownEvent, OnBubble);
    }

    void OnTunnel(object? sender, KeyEventArgs args) => Route(args, KeyRoute.Tunnel);

    void OnBubble(object? sender, KeyEventArgs args) => Route(args, KeyRoute.Bubble);

    void Route(KeyEventArgs args, KeyRoute route)
    {
        if (args.Handled || _scope.IsOverlayOpen)
            return;
        if (_gestures.ToChord(args.Key, args.KeyModifiers) is not { } chord)
            return;
        if (_commands.Find(chord, route) is not { } command)
            return;
        if (!InScope(command.Scope) || !command.IsAvailable() || !command.Command.CanExecute(null))
            return;
        command.Command.Execute(null);
        args.Handled = true;
    }

    bool InScope(KeyScope wanted) => wanted switch
    {
        KeyScope.EditorPage => _scope.CurrentPage == PageKey.Editor,
        KeyScope.EditorCode => _scope.CurrentPage == PageKey.Editor && _scope.IsCodeView,
        _ => true
    };
}
