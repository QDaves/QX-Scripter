using System.Collections.Concurrent;
using Qx.Presentation.Threading;
using Qx.Scripting;

namespace Qx.Presentation.Services.Panels;

public sealed class PanelRunLink : IDisposable
{
    readonly PanelDocument _panel;
    readonly ScriptUi _ui;
    readonly IUiDispatcher _dispatcher;
    readonly ConcurrentQueue<Action> _pending = new();
    readonly CoalescingSignal _drain;
    readonly CancellationToken _run_token;
    bool _pushing;
    int _disposed;

    internal PanelRunLink(PanelDocument panel, ScriptUi ui, IUiDispatcher dispatcher, string? pressed_button, CancellationToken run_token)
    {
        _panel = panel;
        _ui = ui;
        _dispatcher = dispatcher;
        _run_token = run_token;
        _drain = new CoalescingSignal(dispatcher, Drain, UiPriority.Normal);
        foreach ((string name, string value) in panel.Values())
            _ui.Set(name, value);
        _ui.SetClicked(pressed_button);
        _ui.Logged += OnLogged;
        _ui.Cleared += OnCleared;
        _ui.Changed += OnChanged;
        _ui.ProgressChanged += OnProgress;
        _ui.StatusChanged += OnStatus;
        _ui.EnabledChanged += OnEnabled;
        _ui.VisibilityChanged += OnVisibility;
        _ui.RowAdded += OnRowAdded;
        _ui.Toasted += OnToasted;
        _ui.BusyChanged += OnBusy;
        _ui.Downloaded += OnDownloaded;
        _ui.ConfirmRequested += OnConfirmAsync;
        _ui.PromptRequested += OnPromptAsync;
        _panel.FieldEdited += Push;
    }

    public ScriptUi Ui => _ui;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _panel.FieldEdited -= Push;
        _ui.Logged -= OnLogged;
        _ui.Cleared -= OnCleared;
        _ui.Changed -= OnChanged;
        _ui.ProgressChanged -= OnProgress;
        _ui.StatusChanged -= OnStatus;
        _ui.EnabledChanged -= OnEnabled;
        _ui.VisibilityChanged -= OnVisibility;
        _ui.RowAdded -= OnRowAdded;
        _ui.Toasted -= OnToasted;
        _ui.BusyChanged -= OnBusy;
        _ui.Downloaded -= OnDownloaded;
        _ui.ConfirmRequested -= OnConfirmAsync;
        _ui.PromptRequested -= OnPromptAsync;
        Apply();
    }

    void Push(string name, string value)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        _pushing = true;
        try
        {
            _ui.Set(name, value);
        }
        finally
        {
            _pushing = false;
        }
    }

    void Enqueue(Action action)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        _pending.Enqueue(action);
        _drain.Raise();
    }

    void Drain()
    {
        if (Volatile.Read(ref _disposed) == 0)
            Apply();
    }

    void Apply()
    {
        while (_pending.TryDequeue(out Action? action))
            action();
    }

    void OnLogged(string box, string text) => Enqueue(() => _panel.AppendOutput(box, text));

    void OnCleared(string name) => Enqueue(() => _panel.ClearByScript(name));

    void OnChanged(string name, string value)
    {
        if (_pushing && _dispatcher.CheckAccess())
            return;
        Enqueue(() => _panel.SetFieldValue(name, value));
    }

    void OnProgress(string name, double fraction) => Enqueue(() => _panel.SetProgress(name, fraction));

    void OnStatus(string name, string text) => Enqueue(() => _panel.SetStatus(name, text));

    void OnEnabled(string name, bool enabled) => Enqueue(() => _panel.SetEnabled(name, enabled));

    void OnVisibility(string name, bool visible) => Enqueue(() => _panel.SetVisible(name, visible));

    void OnRowAdded(string table, IReadOnlyList<string> cells) => Enqueue(() => _panel.AddRow(table, cells));

    void OnToasted(string text, bool problem) => Enqueue(() => _panel.Toast(text, problem));

    void OnBusy(string button, bool busy) => Enqueue(() => _panel.SetButtonBusy(button, busy));

    void OnDownloaded(string file_name, string content) =>
        Enqueue(() => _panel.DownloadAsync(file_name, content, _run_token).Observe("scripts"));

    Task<bool> OnConfirmAsync(string title, string message) =>
        Volatile.Read(ref _disposed) != 0 ? Task.FromResult(false) : _panel.ConfirmAsync(title, message, _run_token);

    Task<string?> OnPromptAsync(string title, string initial) =>
        Volatile.Read(ref _disposed) != 0 ? Task.FromResult<string?>(null) : _panel.PromptAsync(title, initial, _run_token);
}
