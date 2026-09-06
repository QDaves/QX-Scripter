using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Qx.Game;
using Qx.Game.Application;

namespace Qx.Ui;

/// <summary>Which page of the game side of the tool is open.</summary>
public enum NavPage
{
    /// <summary>The editor, which is what everything else steps aside for.</summary>
    Editor,

    Library,
    Logging,
    Room,
    GameData,
    Chat,
    Friends,
    Navigator,
    Inventory,
    Wardrobe,
    General,
    BugReport,

    /// <summary>The tool's own settings and its key bindings.</summary>
    Settings,

    /// <summary>What the tool is and what it is holding.</summary>
    About
}

/// <summary>
/// The shape every page onto the game shares: a heading, a subheading and a body.
/// </summary>
/// <remarks>
/// Written once rather than per page. Seven pages that each drew their own title in their own size
/// with their own margin is seven chances to drift, and the difference would be visible the moment
/// anyone switched between two of them.
/// </remarks>
public abstract class GamePage : UserControl
{
    private readonly UiTaskScope _ui_tasks;
    private GameState? _game;
    private IApplicationRuntime? _application;

    protected GamePage()
    {
        _ui_tasks = new UiTaskScope(Dispatcher, "ui");
    }

    /// <summary>The state to read. Set by the window before the page is first shown.</summary>
    public GameState? Game
    {
        get => _game;
        set
        {
            if (ReferenceEquals(_game, value))
                return;

            if (_game is not null)
                Detach(_game);
            _game = value;
            if (_game is not null)
                Attach(_game);
        }
    }

    public IApplicationRuntime? Application
    {
        get => _application;
        set
        {
            if (ReferenceEquals(_application, value))
                return;

            if (_application is not null)
                DetachApplication(_application);
            _application = value;
            if (_application is not null)
                AttachApplication(_application);
        }
    }

    /// <summary>Subscribes to whatever this page needs to stay current.</summary>
    protected virtual void Attach(GameState game)
    {
    }

    protected virtual void Detach(GameState game)
    {
    }

    protected virtual void AttachApplication(IApplicationRuntime application)
    {
    }

    protected virtual void DetachApplication(IApplicationRuntime application)
    {
    }

    /// <summary>Reads the state again and redraws.</summary>
    public abstract void Refresh();

    /// <summary>Whether a filter on this page is holding text, which decides what Escape means.</summary>
    public virtual bool IsSearching => false;

    /// <summary>Brings the page up, giving it a chance to take focus where that helps.</summary>
    public virtual void Opened()
    {
        Refresh();
        Observe(FetchThenRefreshAsync);
    }

    /// <summary>
    /// Asks the hotel for whatever this page needs and has not got.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tool is normally attached to a game that has been running for a while, so nothing can be
    /// assumed to have been seen. A page that waited for the friend list or the inventory to happen
    /// past on the wire would sit empty until you opened the same thing in the client, which is a
    /// page telling you to go and do its job for it.
    /// </para>
    /// <para>
    /// Asked for on opening rather than on connecting: fetching all of it up front costs a burst of
    /// requests at the exact moment the hotel is least tolerant of one, and most of it would never
    /// be looked at.
    /// </para>
    /// </remarks>
    protected virtual Task FetchAsync() => Task.CompletedTask;

    /// <summary>Says what is being waited for, or what went wrong. Overridden where there is a place to put it.</summary>
    protected virtual void Fetching(string? message)
    {
    }

    private async Task FetchThenRefreshAsync()
    {
        if (Game is null)
            return;

        Fetching("Asking the hotel…");
        try
        {
            await FetchAsync().ConfigureAwait(true);
            Fetching(null);
        }
        catch (Exception error)
        {
            // Shown rather than swallowed. A page that silently stays empty is the thing this was
            // meant to fix.
            Fetching(Reason(error));
            return;
        }

        if (Visibility == Visibility.Visible)
            Refresh();
    }

    private static string Reason(Exception error) => error switch
    {
        TimeoutException => "The hotel did not answer in time.",
        OperationCanceledException => "",
        _ => error.Message
    };

    /// <summary>Marshals onto the interface thread, since game events arrive on the read loop.</summary>
    protected void OnUi(Action work) => _ui_tasks.OnUi(work);

    protected void PostOnUi(Action work, DispatcherPriority priority) =>
        _ui_tasks.Post(work, priority);

    protected void Observe(Func<Task> task_factory) => _ui_tasks.Observe(task_factory);

    /// <summary>Redraws only while the page is actually on screen.</summary>
    protected void RefreshIfVisible() => OnUi(() =>
    {
        if (Visibility == Visibility.Visible)
            Refresh();
    });
}
