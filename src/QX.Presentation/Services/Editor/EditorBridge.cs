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

    public Task<string> ListTabsAsync(CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() => _workspace.Documents.Count == 0
            ? "no open tabs"
            : string.Join("\n", _workspace.Documents.Select(Describe)),
            cancellationToken);

    public Task<string?> ReadOpenScriptAsync(string name, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() => OpenScript(name)?.Text, cancellationToken);

    public Task<string?> EditOpenScriptAsync(string name, Func<string, string> edit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return _ui.InvokeAsync<string?>(async () =>
        {
            if (OpenScript(name) is not { } document)
                return null;
            bool clean = document.IsSaved && !document.IsModified;
            string updated = edit(document.Text);
            if (!string.Equals(updated, document.Text, StringComparison.Ordinal))
                document.ReplaceText(updated);
            if (!clean)
            {
                return document.IsSaved
                    ? $"updated the open tab '{document.Name}'; it had unsaved changes, so its file changes when the tab is saved"
                    : $"updated tab '{document.Name}'";
            }
            if (!await _commands.SaveInPlaceAsync(document, cancellationToken))
                throw new InvalidOperationException($"updated the open tab '{document.Name}', but its file could not be written");
            return $"saved '{document.Name}' and its open tab";
        }, cancellationToken);
    }

    public Task<string?> RenameScriptAsync(string name, string newName, CancellationToken cancellationToken) =>
        _ui.InvokeAsync<string?>(async () =>
        {
            FileOperationResult renamed = await _commands.RenameUnattendedAsync(_files.PathFor(name), newName, cancellationToken);
            return renamed.Succeeded
                ? $"renamed '{name}' to '{ScriptFileName.Normalize(newName)}'"
                : throw new InvalidOperationException(renamed.Failure ?? $"'{name}' could not be renamed");
        }, cancellationToken);

    public Task<string?> DeleteScriptAsync(string name, CancellationToken cancellationToken) =>
        _ui.InvokeAsync<string?>(async () =>
        {
            FileOperationResult deleted = await _commands.DeleteUnattendedAsync(_files.PathFor(name), cancellationToken);
            return deleted.Succeeded
                ? $"deleted '{name}'"
                : throw new InvalidOperationException(deleted.Failure ?? $"'{name}' could not be deleted");
        }, cancellationToken);

    public Task<string> OpenTabAsync(string name, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(async () =>
        {
            string typed = name ?? "";
            if (typed.Trim().Length == 0 || !string.Equals(ScriptFileName.Normalize(typed), typed.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException($"no saved script named '{name}'");
            string path = _files.PathFor(typed);
            if (!_files.Exists(path))
                throw new InvalidOperationException($"no saved script named '{name}'");
            OpenResult opened = await _workspace.OpenAsync(path, cancellationToken);
            if (opened.Document is not { } document)
                throw new InvalidOperationException($"no saved script named '{name}'");
            Reveal(document, "opened");
            return opened.Outcome == OpenOutcome.AlreadyOpen
                ? $"'{name}' is already open as tab '{document.Name}'"
                : $"opened '{name}' as tab '{document.Name}'";
        }, cancellationToken);

    public Task<string> CreateTabAsync(string name, string code, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() =>
        {
            string requested = string.IsNullOrWhiteSpace(name) ? ScriptFileName.Untitled : name.Trim();
            string actual = Unique(requested);
            ScriptDocument document = _workspace.Add(actual, code ?? "", null, modified: true);
            Reveal(document, "created");
            return $"created tab '{actual}'";
        }, cancellationToken);

    public Task<string> EditActiveTabAsync(string code, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() =>
        {
            if (_workspace.Active is not { } document)
                throw new InvalidOperationException("no active tab");
            document.ReplaceText(code ?? "");
            return $"updated tab '{document.Name}'";
        }, cancellationToken);

    public Task<string> SelectTabAsync(string name, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() =>
        {
            ScriptDocument document = Require(name);
            Reveal(document, "selected");
            return $"selected '{document.Name}'";
        }, cancellationToken);

    public Task<string> CloseTabAsync(string name, bool discard, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() =>
        {
            ScriptDocument document = Require(name);
            if (document.Run.IsWorking)
            {
                document.Run.RequestStop();
                return $"stopping '{document.Name}'; close it again once it has stopped";
            }
            if (document.IsModified && !discard)
                throw new InvalidOperationException($"'{document.Name}' has unsaved changes; save it first, or pass discard to drop them");
            _workspace.Remove(document);
            return $"closed '{document.Name}'";
        }, cancellationToken);

    public Task<string> RunActiveTabAsync(string name, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() =>
        {
            ScriptDocument document = Target(name);
            if (document.Run.IsAlive)
                return document.Run.IsArmedIdle ? "panel already running; press its buttons or stop it" : "already running";
            document.Run.Start(null, document.PanelMode);
            return $"running '{document.Name}'";
        }, cancellationToken);

    public Task<string> StopActiveTabAsync(string name, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() =>
        {
            ScriptDocument document = Target(name);
            if (!document.Run.IsAlive)
                return "not running";
            document.Run.RequestStop();
            return $"stopping '{document.Name}'";
        }, cancellationToken);

    public Task<string> GetTabOutputAsync(string name, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() =>
        {
            ScriptDocument document = Target(name);
            return document.Run.Output.Lines.Count == 0 ? "(no output)" : document.Run.Output.Text();
        }, cancellationToken);

    public Task<string> GetTabStatusAsync(string name, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() =>
        {
            ScriptDocument document = Target(name);
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
        }, cancellationToken);

    public Task<string> GetTabErrorsAsync(string name, CancellationToken cancellationToken) =>
        _ui.InvokeAsync(() =>
        {
            ScriptDocument document = Target(name);
            return document.Run.Errors.Count == 0 ? "no errors" : JsonSerializer.Serialize(document.Run.Errors.ToList(), _errors_json);
        }, cancellationToken);

    ScriptDocument Target(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? _workspace.Active ?? throw new InvalidOperationException("no active tab")
            : Require(name);

    ScriptDocument Require(string name) =>
        _workspace.FindByName(name) ?? throw new InvalidOperationException($"no tab named '{name}'");

    ScriptDocument? OpenScript(string name) =>
        string.IsNullOrWhiteSpace(name) ? _workspace.Active : _workspace.FindByPath(_files.PathFor(name)) ?? _workspace.FindByName(name);

    string Describe(ScriptDocument document)
    {
        string active = ReferenceEquals(document, _workspace.Active) ? "* " : "  ";
        string file = document.LibraryName is not { } saved
            ? " (unsaved)"
            : string.Equals(saved, document.Name, StringComparison.Ordinal) ? "" : $" (file '{saved}')";
        return $"{active}{document.Name}{file} [{StateWord(document)}]{(document.IsModified ? " ● modified" : "")}";
    }

    static string StateWord(ScriptDocument document) =>
        document.Run.IsArmedIdle ? "armed" : document.Run.State.ToString().ToLowerInvariant();

    string Unique(string requested)
    {
        if (Free(requested))
            return requested;
        for (int number = 2; ; number++)
        {
            string candidate = $"{requested} {number}";
            if (Free(candidate))
                return candidate;
        }
    }

    bool Free(string name) => _workspace.FindByName(name) is null && !_files.Exists(_files.PathFor(name));

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
