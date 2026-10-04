using System.Diagnostics.CodeAnalysis;

namespace Qx.Scripting.Hosting;

/// <summary>
/// Represents the host side of a <see cref="ScriptUi"/>, which connects the panel a script declared
/// to the panel the host shows.
/// </summary>
/// <remarks>
/// <para>
/// Scripts use <see cref="ScriptUi"/>; a host reaches the other side of the same panel through
/// <see cref="Of"/>. The events report what the script writes to its panel, <see cref="Confirm"/>
/// and <see cref="Prompt"/> answer the questions it asks, and <see cref="Push"/>,
/// <see cref="SetClicked"/> and <see cref="Invoke"/> hand it what the user does.
/// </para>
/// <para>
/// The events are raised on the thread the script writes from, so a host with a UI thread moves
/// them to that thread itself. A panel nobody attaches to stays inert, which is how a script runs
/// outside panel mode.
/// </para>
/// </remarks>
public sealed class ScriptUiHost
{
    private readonly ScriptUi _ui;
    private readonly Dictionary<string, List<Func<Task>>> _clicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Action<string>>> _changes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _handler_sync = new();
    private Task _changing = Task.CompletedTask;
    private string? _clicked;
    private Func<string, string, Task<bool>>? _confirm;
    private Func<string, string, Task<string?>>? _prompt;

    internal ScriptUiHost(ScriptUi ui) => _ui = ui;

    /// <summary>Gets the host side of a script's panel.</summary>
    /// <param name="ui">The panel, normally <see cref="ScriptGlobals.Ui"/>.</param>
    /// <returns>The host side, which is the same instance on every call for the same panel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ui"/> is <see langword="null"/>.</exception>
    public static ScriptUiHost Of(ScriptUi ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        return ui.Host;
    }

    /// <summary>Occurs when the script writes a line to an output box.</summary>
    /// <remarks>The arguments are the box name and the text.</remarks>
    public event Action<string, string>? Logged;

    /// <summary>Occurs when the script offers a file for download.</summary>
    /// <remarks>The arguments are the suggested file name and the contents.</remarks>
    public event Action<string, string>? Downloaded;

    /// <summary>Occurs when the script empties an output box or a table.</summary>
    /// <remarks>The argument is the name of the box or table.</remarks>
    public event Action<string>? Cleared;

    /// <summary>Occurs when the script changes a control's value.</summary>
    /// <remarks>
    /// The arguments are the control name and the new value. A value the host stores with
    /// <see cref="Push"/> does not raise it.
    /// </remarks>
    public event Action<string, string>? Changed;

    /// <summary>Occurs when the script moves a progress bar.</summary>
    /// <remarks>The arguments are the bar name and the fraction done, between 0 and 1.</remarks>
    public event Action<string, double>? ProgressChanged;

    /// <summary>Occurs when the script replaces a status line.</summary>
    /// <remarks>The arguments are the line name and the new text.</remarks>
    public event Action<string, string>? StatusChanged;

    /// <summary>Occurs when the script enables or disables a control.</summary>
    /// <remarks>The arguments are the control name and whether it is enabled.</remarks>
    public event Action<string, bool>? EnabledChanged;

    /// <summary>Occurs when the script shows or hides a control.</summary>
    /// <remarks>The arguments are the control name and whether it is shown.</remarks>
    public event Action<string, bool>? VisibilityChanged;

    /// <summary>Occurs when the script appends a row to a table.</summary>
    /// <remarks>The arguments are the table name and the cells, left to right.</remarks>
    public event Action<string, IReadOnlyList<string>>? RowAdded;

    /// <summary>Occurs when the script replaces every row of a table.</summary>
    /// <remarks>The arguments are the table name and the rows, top to bottom.</remarks>
    public event Action<string, IReadOnlyList<IReadOnlyList<string>>>? RowsSet;

    /// <summary>Occurs when the script wants a short message shown.</summary>
    /// <remarks>The arguments are the text and whether it reports a problem.</remarks>
    public event Action<string, bool>? Toasted;

    /// <summary>Occurs when the script marks a button as working, or done.</summary>
    /// <remarks>The arguments are the button name and whether it is working.</remarks>
    public event Action<string, bool>? BusyChanged;

    /// <summary>Gets or sets the callback that answers <see cref="ScriptUi.Confirm"/>.</summary>
    /// <remarks>
    /// The arguments are the title and the message, and the callback returns the answer. It can be
    /// set once, so the host that sets it before the script runs answers every question of the run
    /// and nothing set later takes its place. While it is <see langword="null"/>,
    /// <see cref="ScriptUi.Confirm"/> answers <see langword="false"/> at once.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the callback is already set.</exception>
    [DisallowNull]
    public Func<string, string, Task<bool>>? Confirm
    {
        get => Volatile.Read(ref _confirm);
        set => SetOnce(ref _confirm, value, nameof(Confirm));
    }

    /// <summary>Gets or sets the callback that answers <see cref="ScriptUi.Prompt"/>.</summary>
    /// <remarks>
    /// The arguments are the title and the initial text, and the callback returns the answer or
    /// <see langword="null"/>. It can be set once, so the host that sets it before the script runs
    /// answers every question of the run and nothing set later takes its place. While it is
    /// <see langword="null"/>, <see cref="ScriptUi.Prompt"/> answers <see langword="null"/> at once.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the callback is already set.</exception>
    [DisallowNull]
    public Func<string, string, Task<string?>>? Prompt
    {
        get => Volatile.Read(ref _prompt);
        set => SetOnce(ref _prompt, value, nameof(Prompt));
    }

    /// <summary>
    /// Gets whether any button or control has a handler, which is what keeps a panel script alive.
    /// </summary>
    public bool HasHandlers
    {
        get
        {
            lock (_handler_sync)
                return _clicks.Count > 0 || _changes.Count > 0;
        }
    }

    /// <summary>Gets a copy of the names of the buttons that have a click handler.</summary>
    public IReadOnlyCollection<string> HandledButtons
    {
        get
        {
            lock (_handler_sync)
                return _clicks.Keys.ToArray();
        }
    }

    /// <summary>
    /// Runs the handlers registered for a button.
    /// </summary>
    /// <remarks>
    /// Call it when the button is pressed. It also records the button as the one
    /// <see cref="ScriptUi.ClickedButton"/> reports. Every handler is started, and one that throws
    /// before returning a task gives a faulted task instead of stopping the others.
    /// </remarks>
    /// <param name="button">The button's name.</param>
    /// <returns>
    /// A task that completes when every handler has finished, or <see langword="null"/> when the
    /// button has none, which lets the host tell "nothing happened" from "something started".
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="button"/> is <see langword="null"/>.</exception>
    public Task? Invoke(string button)
    {
        ArgumentNullException.ThrowIfNull(button);

        Func<Task>[] handlers;
        lock (_handler_sync)
        {
            if (!_clicks.TryGetValue(button, out List<Func<Task>>? list) || list.Count == 0)
                return null;
            handlers = list.ToArray();
        }

        Volatile.Write(ref _clicked, button);

        var running = new Task[handlers.Length];
        for (int i = 0; i < handlers.Length; i++)
        {
            try
            {
                running[i] = handlers[i]() ?? Task.CompletedTask;
            }
            catch (Exception error)
            {
                running[i] = Task.FromException(error);
            }
        }
        return Task.WhenAll(running);
    }

    /// <summary>Records which button started this run.</summary>
    /// <remarks>
    /// Call it before the script runs. <see cref="ScriptUi.Clicked"/> and
    /// <see cref="ScriptUi.ClickedButton"/> report it until <see cref="Invoke"/> runs the handlers
    /// of another button.
    /// </remarks>
    /// <param name="button">The button's name, or <see langword="null"/> when the run was not started by one.</param>
    public void SetClicked(string? button) => Volatile.Write(ref _clicked, button);

    /// <summary>
    /// Stores a value the user entered and runs the handlers registered for the control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is what the script's reads return from now on. It is not written back to the
    /// panel, so <see cref="Changed"/> is not raised. A host also pushes the values its panel shows
    /// when it attaches, before the script runs.
    /// </para>
    /// <para>
    /// The handlers registered with <see cref="ScriptUi.OnChange(string, Action{string})"/> run on
    /// the thread pool, never on the calling thread. They start one at a time, in the order the
    /// values were pushed, each once the one before has returned, so an async handler lets the next
    /// one start at its first <c>await</c>.
    /// </para>
    /// </remarks>
    /// <param name="name">The control's name.</param>
    /// <param name="value">The new value, or <see langword="null"/> to store an empty string.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    public void Push(string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        string stored = value ?? "";
        _ui.Store(name, stored);

        lock (_handler_sync)
        {
            if (!_changes.TryGetValue(name, out List<Action<string>>? list))
                return;
            Action<string>[] handlers = [.. list];
            _changing = _changing.ContinueWith(
                _ =>
                {
                    foreach (Action<string> handler in handlers)
                        handler(stored);
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    internal string? ClickedButton => Volatile.Read(ref _clicked);

    internal void AddClick(string button, Func<Task> handler)
    {
        lock (_handler_sync)
        {
            if (!_clicks.TryGetValue(button, out List<Func<Task>>? list))
                _clicks[button] = list = [];
            list.Add(handler);
        }
    }

    internal void AddChange(string name, Action<string> handler)
    {
        lock (_handler_sync)
        {
            if (!_changes.TryGetValue(name, out List<Action<string>>? list))
                _changes[name] = list = [];
            list.Add(handler);
        }
    }

    internal void RaiseLogged(string box, string text) => Logged?.Invoke(box, text);

    internal void RaiseDownloaded(string file_name, string content) => Downloaded?.Invoke(file_name, content);

    internal void RaiseCleared(string name) => Cleared?.Invoke(name);

    internal void RaiseChanged(string name, string value) => Changed?.Invoke(name, value);

    internal void RaiseProgressChanged(string name, double fraction) => ProgressChanged?.Invoke(name, fraction);

    internal void RaiseStatusChanged(string name, string text) => StatusChanged?.Invoke(name, text);

    internal void RaiseEnabledChanged(string name, bool enabled) => EnabledChanged?.Invoke(name, enabled);

    internal void RaiseVisibilityChanged(string name, bool visible) => VisibilityChanged?.Invoke(name, visible);

    internal void RaiseRowAdded(string table, IReadOnlyList<string> cells) => RowAdded?.Invoke(table, cells);

    internal void RaiseRowsSet(string table, IReadOnlyList<IReadOnlyList<string>> rows) => RowsSet?.Invoke(table, rows);

    internal void RaiseToasted(string text, bool problem) => Toasted?.Invoke(text, problem);

    internal void RaiseBusyChanged(string button, bool busy) => BusyChanged?.Invoke(button, busy);

    private static void SetOnce<T>(ref T? callback, T value, string name) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Interlocked.CompareExchange(ref callback, value, null) is not null)
            throw new InvalidOperationException($"The {name} callback of this panel is already set.");
    }
}
