using System.Collections.ObjectModel;
using Qx.Diagnostics;
using Qx.Presentation.Services.Drafts;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Settings;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Workspace;

public sealed class ScriptWorkspace : IScriptWorkspace, IDisposable
{
    public static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(5);
    public const int ClosedHistoryLimit = 20;

    readonly ObservableCollection<ScriptDocument> _documents = [];
    readonly Dictionary<string, Task<OpenResult>> _opening;
    readonly List<string> _closed = [];
    readonly ScriptDocumentFactory _factory;
    readonly IScriptFileService _files;
    readonly IDraftStore _drafts;
    readonly ISettingsStore _settings;
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    readonly AppLifetime _lifetime;
    CancellationTokenSource? _autosave;
    Task? _autosave_loop;
    ScriptDocument? _active;

    public ScriptWorkspace(
        ScriptDocumentFactory factory,
        IScriptFileService files,
        IDraftStore drafts,
        ISettingsStore settings,
        IUiDispatcher dispatcher,
        TimeProvider time,
        AppLifetime lifetime)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _opening = new Dictionary<string, Task<OpenResult>>(PathComparison.Comparer);
        Documents = new ReadOnlyObservableCollection<ScriptDocument>(_documents);
    }

    public ReadOnlyObservableCollection<ScriptDocument> Documents { get; }

    public ScriptDocument? Active
    {
        get => _active;
        set
        {
            if (ReferenceEquals(_active, value))
                return;
            if (value is not null && !_documents.Contains(value))
                return;
            _active = value;
            ActiveChanged?.Invoke(value);
        }
    }

    public bool HasDocuments => _documents.Count > 0;

    public bool IsCodeViewActive => _active is { PanelMode: false };

    public int ModifiedCount => _documents.Count(document => document.IsModified);

    public bool CanReopenClosed => _closed.Count > 0;

    public event Action? DocumentsChanged;

    public event Action<ScriptDocument?>? ActiveChanged;

    public ScriptDocument AddNew()
    {
        string name = ScriptFileName.NextUntitled(
            _documents.Select(document => document.Name),
            candidate => _files.Exists(_files.PathFor(candidate)));
        return Add(name, "", null, modified: false);
    }

    public ScriptDocument Add(string name, string code, string? path, bool modified)
    {
        ScriptDocument document = _factory.Create(name, code ?? "", path);
        if (path is not null && _settings.PanelFor(PathComparison.Full(path)) is { } memory)
        {
            document.Panel.Restore(memory.Values);
            document.SetPanelMode(memory.Panel);
            if (document.PanelMode)
                document.Panel.Rebuild(document.Text);
        }
        if (modified)
            document.MarkModified();
        _documents.Add(document);
        Active = document;
        DocumentsChanged?.Invoke();
        return document;
    }

    public Task<OpenResult> OpenAsync(string path, CancellationToken cancellation_token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = PathComparison.Full(path);
        if (FindByPath(full) is { } open)
        {
            Active = open;
            return Task.FromResult(new OpenResult(OpenOutcome.AlreadyOpen, open));
        }
        if (_opening.TryGetValue(full, out Task<OpenResult>? running))
            return AwaitSharedAsync(running, cancellation_token);
        var shared = new TaskCompletionSource<OpenResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _opening[full] = shared.Task;
        return ReadAndAddAsync(full, shared, cancellation_token);
    }

    public ScriptDocument? FindByPath(string path) =>
        string.IsNullOrWhiteSpace(path) ? null : _documents.FirstOrDefault(document => PathComparison.Same(document.FilePath, path));

    public ScriptDocument? FindByName(string name) =>
        string.IsNullOrWhiteSpace(name) ? null : _documents.FirstOrDefault(document => string.Equals(document.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool Contains(ScriptDocument document) => document is not null && _documents.Contains(document);

    public ScriptDocument? Adjacent(int offset)
    {
        if (_documents.Count < 2 || _active is null)
            return _active;
        int index = _documents.IndexOf(_active);
        if (index < 0)
            return _active;
        int next = ((index + offset) % _documents.Count + _documents.Count) % _documents.Count;
        return _documents[next];
    }

    public void Move(ScriptDocument document, int index)
    {
        ArgumentNullException.ThrowIfNull(document);
        int from = _documents.IndexOf(document);
        if (from < 0)
            return;
        int target = Math.Clamp(index, 0, _documents.Count - 1);
        if (from == target)
            return;
        _documents.Move(from, target);
        DocumentsChanged?.Invoke();
    }

    public void Remove(ScriptDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Contains(document))
            return;
        if (document.Run.IsAlive)
            document.Run.RequestStop();
        RememberPanel(document);
        if (document.FilePath is { } path)
        {
            _closed.RemoveAll(known => PathComparison.Same(known, path));
            _closed.Add(path);
            while (_closed.Count > ClosedHistoryLimit)
                _closed.RemoveAt(0);
        }
        document.MarkClosed();
        bool was_active = ReferenceEquals(_active, document);
        _documents.Remove(document);
        if (was_active)
        {
            _active = _documents.Count > 0 ? _documents[^1] : null;
            ActiveChanged?.Invoke(_active);
        }
        DocumentsChanged?.Invoke();
        RetireAsync(document).Observe("scripts");
    }

    public void NoteMoved(string from, string to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        string moved = PathComparison.Full(to);
        bool touched = false;
        for (int index = _closed.Count - 1; index >= 0; index--)
        {
            if (!PathComparison.Same(_closed[index], from))
                continue;
            _closed[index] = moved;
            touched = true;
        }
        if (!touched)
            return;
        var kept = new List<string>(_closed.Count);
        for (int index = _closed.Count - 1; index >= 0; index--)
        {
            if (!kept.Any(known => PathComparison.Same(known, _closed[index])))
                kept.Add(_closed[index]);
        }
        kept.Reverse();
        _closed.Clear();
        _closed.AddRange(kept);
    }

    public string? TakeReopenable()
    {
        while (_closed.Count > 0)
        {
            string path = _closed[^1];
            _closed.RemoveAt(_closed.Count - 1);
            if (_files.Exists(path) && FindByPath(path) is null)
                return path;
        }
        return null;
    }

    public void RememberPanel(ScriptDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FilePath is not { } path)
            return;
        _settings.RememberPanel(path, document.PanelMode, document.Panel.PersistedValues());
    }

    public void RememberAllPanels()
    {
        foreach (ScriptDocument document in _documents)
            RememberPanel(document);
    }

    public SessionState CaptureSession()
    {
        if (!_settings.Current.RestoreSession)
            return _settings.Current.Session ?? new SessionState();
        return new SessionState
        {
            Open = [.. _documents.Where(document => document.FilePath is not null).Select(document => document.FilePath!)],
            Active = _active?.FilePath
        };
    }

    public IReadOnlyList<Draft> CaptureDrafts(bool include_modified_files)
    {
        var drafts = new List<Draft>();
        foreach (ScriptDocument document in _documents)
        {
            if (document.FilePath is { } path)
            {
                if (include_modified_files && document.IsModified)
                    drafts.Add(new Draft(document.Name, document.Text, path));
                continue;
            }
            if (!string.IsNullOrWhiteSpace(document.Text))
                drafts.Add(new Draft(document.Name, document.Text));
        }
        return drafts;
    }

    public async Task RestoreAsync(CancellationToken cancellation_token)
    {
        try
        {
            if (_settings.Current.RestoreSession && _settings.Current.Session is { } session)
            {
                foreach (string path in session.Open)
                {
                    if (_files.Exists(path))
                        await OpenAsync(path, cancellation_token);
                }
                if (session.Active is { Length: > 0 } active && FindByPath(active) is { } selected)
                    Active = selected;
            }
            IReadOnlyList<Draft> drafts = await _drafts.LoadAsync(cancellation_token);
            foreach (Draft draft in drafts)
                await RestoreDraftAsync(draft, cancellation_token);
        }
        finally
        {
            StartAutosave();
            DocumentsChanged?.Invoke();
        }
    }

    public Task SaveDraftsAsync(bool include_modified_files, CancellationToken cancellation_token) =>
        _drafts.SaveAsync(CaptureDrafts(include_modified_files), cancellation_token);

    public Task ClearDraftsAsync(CancellationToken cancellation_token) => _drafts.ClearAsync(cancellation_token);

    public bool SaveDraftsNow(bool include_modified_files, TimeSpan budget) =>
        _drafts.SaveNow(CaptureDrafts(include_modified_files), budget);

    public void SealDrafts() => _drafts.Seal();

    public async Task StopAutosaveAsync()
    {
        (CancellationTokenSource? source, Task? loop) = DetachAutosave();
        if (source is null)
            return;
        await source.CancelAsync();
        if (loop is not null)
            await loop.WaitAsync(CancellationToken.None);
        source.Dispose();
    }

    public void StopAutosave()
    {
        (CancellationTokenSource? source, Task? _) = DetachAutosave();
        if (source is null)
            return;
        source.Cancel();
        source.Dispose();
    }

    public void Dispose()
    {
        StopAutosave();
        foreach (ScriptDocument document in _documents)
            document.Dispose();
        _documents.Clear();
    }

    (CancellationTokenSource? Source, Task? Loop) DetachAutosave()
    {
        CancellationTokenSource? source = Interlocked.Exchange(ref _autosave, null);
        Task? loop = Interlocked.Exchange(ref _autosave_loop, null);
        return (source, loop);
    }

    void StartAutosave()
    {
        if (_autosave is not null)
            return;
        var source = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _autosave = source;
        _autosave_loop = AutosaveAsync(source.Token);
    }

    async Task AutosaveAsync(CancellationToken cancellation_token)
    {
        using var ticks = new PeriodicTimer(AutosaveInterval, _time);
        try
        {
            while (await ticks.WaitForNextTickAsync(cancellation_token))
                await _drafts.SaveAsync(CaptureDrafts(include_modified_files: false), cancellation_token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            Diag.Warn($"Draft autosave stopped: {error.Message}", "drafts");
        }
    }

    async Task RestoreDraftAsync(Draft draft, CancellationToken cancellation_token)
    {
        if (draft.Path is { Length: > 0 } path && _files.Exists(path))
        {
            ScriptDocument? document = FindByPath(path);
            if (document is null)
            {
                OpenResult opened = await OpenAsync(path, cancellation_token);
                document = opened.Document;
            }
            if (document is null)
                return;
            document.ReplaceText(draft.Code);
            Active = document;
            return;
        }
        Add(draft.Name, draft.Code, null, modified: true);
    }

    async Task<OpenResult> AwaitSharedAsync(Task<OpenResult> running, CancellationToken cancellation_token)
    {
        OpenResult result = await running.WaitAsync(cancellation_token);
        return result.Document is null ? result : new OpenResult(OpenOutcome.AlreadyOpen, result.Document);
    }

    async Task<OpenResult> ReadAndAddAsync(string full, TaskCompletionSource<OpenResult> shared, CancellationToken cancellation_token)
    {
        try
        {
            OpenResult result = await ReadAsync(full, cancellation_token);
            shared.TrySetResult(result);
            return result;
        }
        catch (Exception error)
        {
            shared.TrySetException(error);
            throw;
        }
        finally
        {
            _opening.Remove(full);
        }
    }

    async Task<OpenResult> ReadAsync(string full, CancellationToken cancellation_token)
    {
        if (!_files.Exists(full))
            return new OpenResult(OpenOutcome.Missing, null);
        string? text = await _files.ReadAsync(full, cancellation_token);
        if (text is null)
            return new OpenResult(OpenOutcome.Unreadable, null, "The script could not be read.");
        if (FindByPath(full) is { } existing)
        {
            Active = existing;
            return new OpenResult(OpenOutcome.AlreadyOpen, existing);
        }
        return new OpenResult(OpenOutcome.Opened, Add(ScriptFileName.NameOf(full), text, full, modified: false));
    }

    async Task RetireAsync(ScriptDocument document)
    {
        try
        {
            await document.Run.Completion.WaitAsync(CancellationToken.None);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"The run of {document.Name} ended badly: {error.Message}", "scripts");
        }
        await _dispatcher.InvokeAsync(document.Dispose);
    }
}
