using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Qx.Mcp;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Notifications;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Editor;

public sealed class EditorBridge : IEditorBridge
{
    static readonly JsonSerializerOptions _status_json = new() { WriteIndented = true };
    static readonly JsonSerializerOptions _errors_json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    readonly IScriptWorkspace _workspace;
    readonly IScriptFileService _files;
    readonly IScriptFileCommands _commands;
    readonly INavigationService _navigation;
    readonly INotificationService _notifications;
    readonly IUiDispatcher _ui;

    public EditorBridge(
        IScriptWorkspace workspace,
        IScriptFileService files,
        IScriptFileCommands commands,
        INavigationService navigation,
        INotificationService notifications,
        IUiDispatcher ui)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    public Task<string> ListTabsAsync(CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() => _workspace.Documents.Count == 0
            ? "no open tabs"
            : string.Join("\n", _workspace.Documents.Select(document =>
                $"{(ReferenceEquals(document, _workspace.Active) ? "* " : "  ")}{document.Name} [{StateWord(document)}]{(document.IsModified ? " ●" : "")}")),
            cancellation_token);

    public Task<string> GetActiveTabAsync(CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() => _workspace.Active is { } document ? $"{document.Name}\n----\n{document.Text}" : "no active tab", cancellation_token);

    public Task<string> OpenTabAsync(string name, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(async () =>
        {
            string typed = name ?? "";
            if (typed.Trim().Length == 0 || !string.Equals(ScriptFileName.Normalize(typed), typed.Trim(), StringComparison.Ordinal))
                return $"no saved script named '{name}'";
            string path = _files.PathFor(typed);
            if (!_files.Exists(path))
                return $"no saved script named '{name}'";
            OpenResult opened = await _workspace.OpenAsync(path, cancellation_token);
            if (opened.Document is not { } document)
                return $"no saved script named '{name}'";
            Reveal(document, "opened");
            return $"opened '{name}'";
        }, cancellation_token);

    public Task<string> CreateTabAsync(string name, string code, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() =>
        {
            string requested = string.IsNullOrWhiteSpace(name) ? ScriptFileName.Untitled : name.Trim();
            string actual = Unique(requested);
            ScriptDocument document = _workspace.Add(actual, code ?? "", null, modified: true);
            Reveal(document, "created");
            return $"created tab '{actual}'";
        }, cancellation_token);

    public Task<string> EditActiveTabAsync(string code, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() =>
        {
            if (_workspace.Active is not { } document)
                return "no active tab";
            document.ReplaceText(code ?? "");
            return "updated active tab";
        }, cancellation_token);

    public Task<string> SelectTabAsync(string name, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() =>
        {
            if (_workspace.FindByName(name) is not { } document)
                return $"no tab named '{name}'";
            Reveal(document, "selected");
            return $"selected '{name}'";
        }, cancellation_token);

    public Task<string> CloseTabAsync(string name, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(async () =>
        {
            if (_workspace.FindByName(name) is not { } document)
                return $"no tab named '{name}'";
            CloseOutcome outcome = await _commands.CloseAsync(document, cancellation_token);
            return outcome switch
            {
                CloseOutcome.Closed => $"closed '{name}'",
                CloseOutcome.Stopping => $"stopping '{name}'",
                _ => $"close cancelled for '{name}'"
            };
        }, cancellation_token);

    public Task<string> RunActiveTabAsync(string name, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() =>
        {
            if (Target(name) is not { } document)
                return string.IsNullOrWhiteSpace(name) ? "no active tab" : $"no tab named '{name}'";
            if (document.Run.IsAlive)
                return document.Run.IsArmedIdle ? "panel already running; press its buttons or stop it" : "already running";
            document.Run.Start(null, document.PanelMode);
            return $"running '{document.Name}'";
        }, cancellation_token);

    public Task<string> StopActiveTabAsync(string name, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() =>
        {
            if (Target(name) is not { } document)
                return string.IsNullOrWhiteSpace(name) ? "no active tab" : $"no tab named '{name}'";
            if (!document.Run.IsAlive)
                return "not running";
            document.Run.RequestStop();
            return $"stopping '{document.Name}'";
        }, cancellation_token);

    public Task<string> GetTabOutputAsync(string name, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() =>
        {
            if (Target(name) is not { } document)
                return string.IsNullOrWhiteSpace(name) ? "no tab" : $"no tab named '{name}'";
            return document.Run.Output.Lines.Count == 0 ? "(no output)" : document.Run.Output.Text();
        }, cancellation_token);

    public Task<string> GetTabStatusAsync(string name, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() =>
        {
            if (Target(name) is not { } document)
                return string.IsNullOrWhiteSpace(name) ? "no tab" : $"no tab named '{name}'";
            ScriptRunController run = document.Run;
            return JsonSerializer.Serialize(new
            {
                name = document.Name,
                state = run.State.ToString().ToLowerInvariant(),
                running = run.IsRunning,
                working = run.IsWorking,
                armed = run.IsArmedIdle,
                handlers = run.BusyHandlers,
                faulted = run.IsFaulted,
                runtimeMs = run.RuntimeMs,
                startedAt = run.StartedAt,
                finishedAt = run.FinishedAt,
                outputLength = run.Output.Text().Length,
                errorCount = run.Errors.Count
            }, _status_json);
        }, cancellation_token);

    public Task<string> GetTabErrorsAsync(string name, CancellationToken cancellation_token) =>
        _ui.InvokeAsync(() =>
        {
            if (Target(name) is not { } document)
                return string.IsNullOrWhiteSpace(name) ? "no tab" : $"no tab named '{name}'";
            return document.Run.Errors.Count == 0 ? "no errors" : JsonSerializer.Serialize(document.Run.Errors.ToList(), _errors_json);
        }, cancellation_token);

    ScriptDocument? Target(string name) =>
        string.IsNullOrWhiteSpace(name) ? _workspace.Active : _workspace.FindByName(name);

    static string StateWord(ScriptDocument document) =>
        document.Run.IsArmedIdle ? "armed" : document.Run.State.ToString().ToLowerInvariant();

    string Unique(string requested)
    {
        if (_workspace.FindByName(requested) is null)
            return requested;
        for (int number = 2; ; number++)
        {
            string candidate = $"{requested} {number}";
            if (_workspace.FindByName(candidate) is null)
                return candidate;
        }
    }

    void Reveal(ScriptDocument document, string verb)
    {
        _workspace.Active = document;
        if (_navigation.Current is PageKey.Editor or PageKey.Library)
        {
            _navigation.Navigate(PageKey.Editor);
            return;
        }
        _notifications.Show(
            $"An MCP client {verb} “{document.Name}”.",
            NoticeSeverity.Info,
            "Show",
            new RelayCommand(() =>
            {
                if (document.IsClosed)
                    return;
                _workspace.Active = document;
                _navigation.Navigate(PageKey.Editor);
            }));
    }
}
