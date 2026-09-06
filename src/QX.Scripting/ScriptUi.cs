using System.Globalization;

namespace Qx.Scripting;

/// <summary>
/// The panel a script declared with <c>//@ui:</c> directives: the values a user entered, and the
/// ways of writing back to it while the script runs.
/// </summary>
/// <remarks>
/// Outside panel mode every getter returns its fallback, <see cref="Clicked"/> is always false, and
/// the writers do nothing. A script can therefore use this unconditionally and still run from the
/// editor.
/// </remarks>
public sealed class ScriptUi
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Func<Task>>> _handlers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _handler_sync = new();
    private string? _clicked;

    /// <summary>
    /// Attaches a handler to a panel button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A script that registers a handler keeps running: pressing the button calls the handler
    /// rather than starting the script again. That is the difference between a panel and a form,
    /// and it is why a panel does not need a loop watching <see cref="Clicked"/> to stay responsive.
    /// </para>
    /// <para>
    /// Handlers run alongside one another, so a button that works for a minute does not stop the
    /// others from answering. A script that does not want that can say so itself, by disabling the
    /// button with <see cref="Enable"/> while it works.
    /// </para>
    /// <para>
    /// Registering the same button twice adds a second handler rather than replacing the first;
    /// both run.
    /// </para>
    /// </remarks>
    /// <param name="button">The button's name, as the directive declared it.</param>
    /// <param name="handler">What to do when it is pressed.</param>
    public void OnClick(string button, Func<Task> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(button);
        ArgumentNullException.ThrowIfNull(handler);
        lock (_handler_sync)
        {
            if (!_handlers.TryGetValue(button, out List<Func<Task>>? list))
                _handlers[button] = list = [];
            list.Add(handler);
        }
    }

    /// <inheritdoc cref="OnClick(string, Func{Task})"/>
    public void OnClick(string button, Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        OnClick(button, () =>
        {
            handler();
            return Task.CompletedTask;
        });
    }

    /// <summary>Whether any button has a handler, which is what keeps a panel script alive.</summary>
    public bool HasClickHandlers
    {
        get
        {
            lock (_handler_sync)
                return _handlers.Count > 0;
        }
    }

    /// <summary>The buttons that have a handler.</summary>
    public IReadOnlyCollection<string> HandledButtons
    {
        get
        {
            lock (_handler_sync)
                return _handlers.Keys.ToArray();
        }
    }

    /// <summary>
    /// Runs the handlers for a button. Called by the host when the button is pressed.
    /// </summary>
    /// <param name="button">The button's name.</param>
    /// <returns>
    /// Every handler's work, or <see langword="null"/> when the button has none, which lets the
    /// host tell "nothing happened" from "something started".
    /// </returns>
    public Task? Invoke(string button)
    {
        ArgumentNullException.ThrowIfNull(button);

        Func<Task>[] handlers;
        lock (_handler_sync)
        {
            if (!_handlers.TryGetValue(button, out List<Func<Task>>? list) || list.Count == 0)
                return null;
            handlers = list.ToArray();
        }

        _clicked = button;

        // Each handler is started inside its own try and a synchronous throw is turned into a
        // faulted task. Handing the delegates straight to Task.WhenAll ran them lazily inside this
        // frame, so a handler that threw before returning a task threw out of Invoke: the host had
        // no task to attach its error reporting to, the exception surfaced in a click callback with
        // nothing to catch it, and the handlers after it never ran at all. An Action handler always
        // takes that path, because its wrapper runs the body before returning.
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

    /// <summary>Raised when the script writes a line to an output box.</summary>
    public event Action<string, string>? Logged;

    /// <summary>Raised when the script offers a file for download.</summary>
    public event Action<string, string>? Downloaded;

    /// <summary>Raised when the script empties an output box.</summary>
    public event Action<string>? Cleared;

    /// <summary>Raised when the script changes a control's value.</summary>
    public event Action<string, string>? Changed;

    /// <summary>Raised when the script moves a progress bar. The value is between 0 and 1.</summary>
    public event Action<string, double>? ProgressChanged;

    /// <summary>Raised when the script replaces a status line.</summary>
    public event Action<string, string>? StatusChanged;

    /// <summary>Raised when the script enables or disables a control.</summary>
    public event Action<string, bool>? EnabledChanged;

    /// <summary>Raised when the script shows or hides a control.</summary>
    public event Action<string, bool>? VisibilityChanged;

    /// <summary>
    /// Changes a control's value, both for later reads and on screen.
    /// </summary>
    /// <param name="name">The control's name.</param>
    /// <param name="value">The new value.</param>
    public void Set(string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        _values[name] = value ?? "";
        Changed?.Invoke(name, _values[name]);
    }

    /// <summary>Records which button started this run. Called by the host, not by scripts.</summary>
    /// <param name="button">The button's name, or null when the run was not started by one.</param>
    public void SetClicked(string? button) => _clicked = button;

    /// <summary>Whether a named button started this run.</summary>
    /// <param name="button">The button's name.</param>
    public bool Clicked(string button) =>
        string.Equals(_clicked, button, StringComparison.OrdinalIgnoreCase);

    /// <summary>The name of the button that started this run, or null.</summary>
    public string? ClickedButton => _clicked;

    /// <summary>A text value, or the fallback when it is missing or empty.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="fallback">What to return when nothing was entered.</param>
    public string String(string name, string fallback = "") =>
        _values.TryGetValue(name, out string? v) && v.Length > 0 ? v : fallback;

    /// <inheritdoc cref="String"/>
    public string Text(string name, string fallback = "") => String(name, fallback);

    /// <inheritdoc cref="String"/>
    public string Select(string name, string fallback = "") => String(name, fallback);

    /// <summary>A whole number, or the fallback when it is missing or unparsable.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="fallback">What to return when nothing usable was entered.</param>
    public int Int(string name, int fallback = 0) =>
        _values.TryGetValue(name, out string? v) && int.TryParse(v, out int i) ? i : fallback;

    /// <summary>A number, or the fallback when it is missing or unparsable.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="fallback">What to return when nothing usable was entered.</param>
    public double Number(string name, double fallback = 0) =>
        _values.TryGetValue(name, out string? v) &&
        double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)
            ? d
            : fallback;

    /// <summary>A checkbox, or the fallback when it is missing.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="fallback">What to return when the control was not declared.</param>
    public bool Bool(string name, bool fallback = false) =>
        _values.TryGetValue(name, out string? v) ? v is "true" or "True" or "1" : fallback;

    /// <summary>A chosen file path, or null when none was chosen.</summary>
    /// <param name="name">The control's name.</param>
    public string? File(string name) =>
        _values.TryGetValue(name, out string? v) && v.Length > 0 ? v : null;

    /// <summary>The contents of a chosen file, or an empty string when there is none.</summary>
    /// <param name="name">The control's name.</param>
    public string FileText(string name) =>
        File(name) is { } path && System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";

    /// <summary>
    /// Writes a line to an output box.
    /// </summary>
    /// <param name="box">
    /// The box's name. An empty name goes to the first box the panel declares, which is why a
    /// panel with several boxes should always name one.
    /// </param>
    /// <param name="text">The line. Its <c>ToString</c> is used.</param>
    public void Log(string box, object? text)
    {
        ArgumentNullException.ThrowIfNull(box);
        Logged?.Invoke(box, text?.ToString() ?? "");
    }

    /// <summary>Empties an output box.</summary>
    /// <param name="box">The box's name.</param>
    public void Clear(string box)
    {
        ArgumentNullException.ThrowIfNull(box);
        Cleared?.Invoke(box);
    }

    /// <summary>
    /// Moves a progress bar.
    /// </summary>
    /// <param name="name">The bar's name.</param>
    /// <param name="value">
    /// How far along, from 0 to 1. Values outside that range are clamped, so a caller dividing by
    /// a total that turned out to be zero does not produce a bar drawn off its own end.
    /// </param>
    public void Progress(string name, double value)
    {
        ArgumentNullException.ThrowIfNull(name);
        double clamped = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
        ProgressChanged?.Invoke(name, clamped);
    }

    /// <summary>Moves a progress bar by a count rather than a fraction.</summary>
    /// <param name="name">The bar's name.</param>
    /// <param name="done">How many are finished.</param>
    /// <param name="total">How many there are. Zero leaves the bar at nothing.</param>
    public void Progress(string name, int done, int total) =>
        Progress(name, total <= 0 ? 0 : (double)done / total);

    /// <summary>Replaces a status line.</summary>
    /// <param name="name">The line's name.</param>
    /// <param name="text">What it should say.</param>
    public void Status(string name, object? text)
    {
        ArgumentNullException.ThrowIfNull(name);
        StatusChanged?.Invoke(name, text?.ToString() ?? "");
    }

    /// <summary>Enables or disables a control.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="enabled">Whether it can be used.</param>
    public void Enable(string name, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(name);
        EnabledChanged?.Invoke(name, enabled);
    }

    /// <summary>Shows or hides a control.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="visible">Whether it is shown. A hidden control takes no space.</param>
    public void Show(string name, bool visible = true)
    {
        ArgumentNullException.ThrowIfNull(name);
        VisibilityChanged?.Invoke(name, visible);
    }

    /// <summary>Offers a file for the user to save.</summary>
    /// <param name="fileName">The suggested name.</param>
    /// <param name="content">The contents.</param>
    public void Download(string fileName, string content) => Downloaded?.Invoke(fileName, content);

    /// <summary>Raised when the script appends a row to a table.</summary>
    public event Action<string, IReadOnlyList<string>>? RowAdded;

    /// <summary>Raised when the script wants a short message shown.</summary>
    public event Action<string, bool>? Toasted;

    /// <summary>Raised when the script marks a button as working, or done.</summary>
    public event Action<string, bool>? BusyChanged;

    /// <summary>Raised when the script asks the user to confirm something.</summary>
    public event Func<string, string, Task<bool>>? ConfirmRequested;

    /// <summary>Raised when the script asks the user for a value.</summary>
    public event Func<string, string, Task<string?>>? PromptRequested;

    /// <summary>
    /// Appends a row to a table.
    /// </summary>
    /// <remarks>
    /// Cells are converted with <c>ToString</c>, and a null cell becomes an empty one rather than
    /// the word "null". Extra cells beyond the declared columns are kept but not shown.
    /// </remarks>
    /// <param name="table">The table's name.</param>
    /// <param name="cells">The row, left to right.</param>
    public void AddRow(string table, params object?[] cells)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentNullException.ThrowIfNull(cells);
        RowAdded?.Invoke(table, cells.Select(cell => cell?.ToString() ?? "").ToArray());
    }

    /// <summary>
    /// Shows a short message that fades on its own.
    /// </summary>
    /// <remarks>
    /// For something worth noticing but not worth a line in an output box. A message marked as a
    /// problem is tinted, so a script can say "that did not work" without a box for it.
    /// </remarks>
    /// <param name="text">What to say.</param>
    /// <param name="problem">Whether this reports something going wrong.</param>
    public void Toast(string text, bool problem = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        Toasted?.Invoke(text, problem);
    }

    /// <summary>
    /// Marks a button as working, so it shows that something is happening.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Enable"/>: a button can be busy and still clickable, and a script
    /// that wants both says both. The panel clears it by itself when the handler returns, so this
    /// is only needed for work a script starts elsewhere.
    /// </remarks>
    /// <param name="button">The button's name.</param>
    /// <param name="busy">Whether it is working.</param>
    public void Busy(string button, bool busy = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(button);
        BusyChanged?.Invoke(button, busy);
    }

    /// <summary>
    /// Asks the user to confirm something and waits for the answer.
    /// </summary>
    /// <remarks>
    /// Outside panel mode, and anywhere else with nobody to ask, this answers no at once rather
    /// than waiting: a script running headless must not hang on a question that will never be seen,
    /// and refusing is the safe half of a yes-or-no.
    /// </remarks>
    /// <param name="title">The heading.</param>
    /// <param name="message">What is being confirmed.</param>
    public Task<bool> Confirm(string title, string message)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        return ConfirmRequested?.Invoke(title, message) ?? Task.FromResult(false);
    }

    /// <summary>
    /// Asks the user for a value and waits for it.
    /// </summary>
    /// <remarks>
    /// Answers <see langword="null"/> at once where there is nobody to ask, the same way and for
    /// the same reason as <see cref="Confirm"/>. Null also means the user dismissed the question,
    /// which is not the same as an empty answer.
    /// </remarks>
    /// <param name="title">The heading.</param>
    /// <param name="initial">What the box starts with.</param>
    public Task<string?> Prompt(string title, string initial = "")
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(initial);
        return PromptRequested?.Invoke(title, initial) ?? Task.FromResult<string?>(null);
    }
}
