using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Panels;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Threading;
using Qx.Presentation.ViewModels.ApiBrowser;
using Qx.Presentation.ViewModels.ScriptPanels;
using Qx.Scripting;

namespace Qx.Presentation.ViewModels.Editor;

public enum DocumentView
{
    Code,
    Panel
}

public sealed partial class WorkspaceViewModel : PageViewModel, IApiInsertTarget
{
    public const string NoUiTip = "No UI directives in this script";
    public const string ScriptsLabel = "scripts";

    readonly IScriptWorkspace _workspace;
    readonly IScriptFileCommands _commands;
    readonly INavigationService _navigation;
    readonly IScriptRunRegistry _runs;
    readonly IScriptFileService _files;
    readonly IShellWindow _shell;
    readonly IFileRevealer _revealer;
    readonly ILauncherService _launcher;
    readonly IScriptPanelFactory _panels;
    readonly IUiDispatcher _dispatcher;
    ScriptDocument? _watched;
    ScriptRunController? _watched_run;
    int _error_index = -1;

    public WorkspaceViewModel(
        IScriptWorkspace workspace,
        IScriptFileCommands commands,
        INavigationService navigation,
        IScriptRunRegistry runs,
        IScriptFileService files,
        EditorPreferences editor,
        OutputPreferences output,
        IShellWindow shell,
        IFileRevealer revealer,
        ILauncherService launcher,
        IScriptPanelFactory panels,
        IApiBrowserFactory browsers,
        IUiDispatcher dispatcher)
        : base(PageKey.Editor)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _revealer = revealer ?? throw new ArgumentNullException(nameof(revealer));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _panels = panels ?? throw new ArgumentNullException(nameof(panels));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(browsers);
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Documents = workspace.Documents;
        ApiBrowser = browsers.Create(this);
        _workspace.ActiveChanged += OnActiveChanged;
        _workspace.DocumentsChanged += OnDocumentsChanged;
        Own(() =>
        {
            _workspace.ActiveChanged -= OnActiveChanged;
            _workspace.DocumentsChanged -= OnDocumentsChanged;
            Watch(null);
        });
        Bind(workspace.Active);
    }

    public ReadOnlyObservableCollection<ScriptDocument> Documents { get; }

    public EditorPreferences Editor { get; }

    public OutputPreferences Output { get; }

    public ApiBrowserViewModel ApiBrowser { get; }

    public ScriptDocument? Active
    {
        get => _workspace.Active;
        set => _workspace.Active = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanelView), nameof(ConsoleVisible), nameof(ToolbarTitle))]
    public partial DocumentView ActiveView { get; private set; }

    [ObservableProperty]
    public partial ScriptPanelViewModel? PanelSurface { get; private set; }

    [ObservableProperty]
    public partial OutputConsoleViewModel? Console { get; private set; }

    [ObservableProperty]
    public partial bool IsApiBrowserOpen { get; private set; }

    [ObservableProperty]
    public partial double? ApiBrowserWidth { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FolderCrumb), nameof(FileCrumb), nameof(HasFile), nameof(ToolbarTitle))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand), nameof(RevealCommand))]
    public partial string? ActivePath { get; private set; }

    public bool IsPanelView => ActiveView == DocumentView.Panel;

    public bool ConsoleVisible => ActiveView == DocumentView.Code;

    public bool HasDocuments => _workspace.Documents.Count > 0;

    public bool HasFile => ActivePath is { Length: > 0 };

    public bool CanUsePanelView => Active is { HasUi: true };

    public string PanelTip => CanUsePanelView ? "UI panel view" : NoUiTip;

    public RunPhase RunPhase => Active is { } document ? document.Run.Phase : RunPhase.Idle;

    public bool CanRun => Active is not null;

    public string FolderCrumb => ActivePath is { Length: > 0 } path ? FolderLabel(path) : ScriptsLabel;

    public string FileCrumb => ActivePath is { Length: > 0 } path ? Path.GetFileName(path) : Active?.Name ?? "";

    public string ToolbarTitle => IsPanelView && Active?.Panel.Title is { } title && !string.IsNullOrWhiteSpace(title)
        ? title
        : FileCrumb;

    public void InsertFromApi(string text, int caret_offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Active?.Buffer is not { } buffer)
            return;
        buffer.Insert(text, caret_offset);
        buffer.Focus();
    }

    void IApiInsertTarget.Insert(string text, int caret_offset) => InsertFromApi(text, caret_offset);

    void IApiInsertTarget.CloseApiBrowser() => IsApiBrowserOpen = false;

    public void GoToNextError()
    {
        if (Active is not { } document)
            return;
        ScriptExecutionError[] located = [.. document.Run.Errors.Where(error => error.Line > 0)];
        if (located.Length == 0)
            return;
        _error_index = (_error_index + 1) % located.Length;
        ScriptExecutionError next = located[_error_index];
        if (document.PanelMode)
            SelectCodeMode();
        document.Buffer?.GoTo(next.Line ?? 1, Math.Max(1, next.Column ?? 1));
        document.Buffer?.Focus();
    }

    [RelayCommand]
    void New() => _commands.NewDocument();

    [RelayCommand]
    async Task SaveAsync(CancellationToken cancellation_token)
    {
        if (Active is { } document)
            await _commands.SaveAsync(document, cancellation_token);
    }

    [RelayCommand]
    async Task SaveAsAsync(CancellationToken cancellation_token)
    {
        if (Active is { } document)
            await _commands.SaveAsAsync(document, cancellation_token);
    }

    [RelayCommand]
    async Task CloseAsync(ScriptDocument? document, CancellationToken cancellation_token)
    {
        ScriptDocument? target = document ?? Active;
        if (target is not null)
            await _commands.CloseAsync(target, cancellation_token);
    }

    [RelayCommand]
    async Task CloseOthersAsync(ScriptDocument? document, CancellationToken cancellation_token)
    {
        ScriptDocument? target = document ?? Active;
        if (target is not null)
            await _commands.CloseOthersAsync(target, cancellation_token);
    }

    [RelayCommand]
    async Task CloseToTheRightAsync(ScriptDocument? document, CancellationToken cancellation_token)
    {
        ScriptDocument? target = document ?? Active;
        if (target is not null)
            await _commands.CloseToTheRightAsync(target, cancellation_token);
    }

    [RelayCommand]
    async Task RenameAsync(ScriptDocument? document, CancellationToken cancellation_token)
    {
        ScriptDocument? target = document ?? Active;
        if (target is not null)
            await _commands.RenameAsync(target, cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(HasSavedFile))]
    async Task DuplicateAsync(ScriptDocument? document, CancellationToken cancellation_token)
    {
        if ((document ?? Active)?.FilePath is { } path)
            await _commands.DuplicateAsync(path, cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(HasSavedFile))]
    async Task RevealAsync(ScriptDocument? document, CancellationToken cancellation_token)
    {
        if ((document ?? Active)?.FilePath is { } path)
            await _commands.RevealAsync(path, cancellation_token);
    }

    bool HasSavedFile(ScriptDocument? document) => (document ?? Active)?.FilePath is not null;

    [RelayCommand]
    async Task OpenFolderAsync(CancellationToken cancellation_token)
    {
        string folder = ActivePath is { Length: > 0 } path ? Path.GetDirectoryName(path) ?? _files.ScriptsDirectory : _files.ScriptsDirectory;
        await _launcher.OpenFolderAsync(folder, cancellation_token);
    }

    [RelayCommand]
    async Task RevealActiveAsync(CancellationToken cancellation_token)
    {
        if (ActivePath is { Length: > 0 } path)
            await _revealer.RevealAsync(path, cancellation_token);
    }

    [RelayCommand]
    async Task ReopenClosedAsync(CancellationToken cancellation_token) => await _commands.ReopenClosedAsync(cancellation_token);

    [RelayCommand]
    async Task OpenDroppedAsync(IReadOnlyList<string>? paths, CancellationToken cancellation_token)
    {
        if (paths is { Count: > 0 })
            await _commands.OpenDroppedAsync(paths, cancellation_token);
    }

    [RelayCommand]
    public void ToggleRun()
    {
        if (Active is not { } document)
            return;
        if (document.Run.IsAlive)
            document.Run.RequestStop();
        else
            document.Run.Start(null, panel_mode: document.PanelMode);
    }

    [RelayCommand]
    void StopActive() => Active?.Run.RequestStop();

    [RelayCommand]
    void StopAll() => _runs.StopAll();

    [RelayCommand]
    void NextError() => GoToNextError();

    [RelayCommand]
    public void SelectCodeMode()
    {
        if (Active is not { } document)
            return;
        document.SetPanelMode(false);
        _workspace.RememberPanel(document);
        ActiveView = DocumentView.Code;
    }

    [RelayCommand]
    public void SelectPanelMode()
    {
        if (Active is not { HasUi: true } document)
            return;
        document.SetPanelMode(true);
        document.Panel.Rebuild(document.Text);
        _workspace.RememberPanel(document);
        ActiveView = DocumentView.Panel;
    }

    [RelayCommand]
    void ToggleConsole() => Console?.ToggleCollapsedCommand.Execute(null);

    [RelayCommand]
    public void ToggleApiBrowser()
    {
        IsApiBrowserOpen = !IsApiBrowserOpen;
        if (IsApiBrowserOpen)
            _dispatcher.Post(ApiBrowser.Activate, UiPriority.Input);
    }

    [RelayCommand]
    void ZoomIn() => Editor.ZoomIn();

    [RelayCommand]
    void ZoomOut() => Editor.ZoomOut();

    [RelayCommand]
    void ResetZoom() => Editor.ResetZoom();

    [RelayCommand]
    void ToggleEditorWrap() => Editor.ToggleWrap();

    [RelayCommand]
    void NextDocument() => GoToAdjacent(1);

    [RelayCommand]
    void PreviousDocument() => GoToAdjacent(-1);

    public void MoveDocument(int from, int to)
    {
        if (from < 0 || from >= _workspace.Documents.Count)
            return;
        _workspace.Move(_workspace.Documents[from], to);
    }

    public void ShowDocument(ScriptDocument document)
    {
        if (!Documents.Contains(document))
            return;
        Active = document;
        _navigation.Navigate(PageKey.Editor);
    }

    protected override void OnDisposed()
    {
        PanelSurface?.Dispose();
        PanelSurface = null;
        base.OnDisposed();
    }

    void GoToAdjacent(int offset)
    {
        if (_workspace.Adjacent(offset) is { } next)
            _workspace.Active = next;
        _navigation.Navigate(PageKey.Editor);
    }

    void OnDocumentsChanged()
    {
        OnPropertyChanged(nameof(HasDocuments));
        RefreshTitle();
        if (!HasDocuments && _navigation.IsStarted && _navigation.Current == PageKey.Editor)
            _navigation.Navigate(PageKey.Library);
    }

    void OnActiveChanged(ScriptDocument? document) => Bind(document);

    void Bind(ScriptDocument? document)
    {
        if (PanelSurface is { } previous)
        {
            previous.Dispose();
            PanelSurface = null;
        }
        _error_index = -1;
        Console = document?.Console;
        ActivePath = document?.FilePath;
        ActiveView = document is { PanelMode: true } ? DocumentView.Panel : DocumentView.Code;
        if (document is not null)
            PanelSurface = _panels.Create(document);
        Watch(document);
        OnPropertyChanged(nameof(Active));
        OnPropertyChanged(nameof(CanUsePanelView));
        OnPropertyChanged(nameof(PanelTip));
        OnPropertyChanged(nameof(RunPhase));
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(FileCrumb));
        OnPropertyChanged(nameof(ToolbarTitle));
        RefreshTitle();
    }

    void Watch(ScriptDocument? document)
    {
        if (_watched is { } previous)
        {
            previous.PropertyChanged -= OnDocumentChanged;
            previous.Panel.PropertyChanged -= OnPanelChanged;
        }
        if (_watched_run is { } running)
            running.PropertyChanged -= OnRunChanged;
        _watched = document;
        _watched_run = document?.Run;
        if (_watched is { } next)
        {
            next.PropertyChanged += OnDocumentChanged;
            next.Panel.PropertyChanged += OnPanelChanged;
        }
        if (_watched_run is { } run)
            run.PropertyChanged += OnRunChanged;
    }

    void OnPanelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(PanelDocument.Title))
            OnPropertyChanged(nameof(ToolbarTitle));
    }

    void OnRunChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ScriptRunController.Phase))
            OnPropertyChanged(nameof(RunPhase));
    }

    void OnDocumentChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(ScriptDocument.IsModified):
            case nameof(ScriptDocument.Name):
                OnPropertyChanged(nameof(FileCrumb));
                OnPropertyChanged(nameof(ToolbarTitle));
                RefreshTitle();
                break;
            case nameof(ScriptDocument.FilePath):
                ActivePath = Active?.FilePath;
                RefreshTitle();
                break;
            case nameof(ScriptDocument.HasUi):
                OnPropertyChanged(nameof(CanUsePanelView));
                OnPropertyChanged(nameof(PanelTip));
                break;
            case nameof(ScriptDocument.PanelMode):
                ActiveView = Active is { PanelMode: true } ? DocumentView.Panel : DocumentView.Code;
                OnPropertyChanged(nameof(CanRun));
                break;
        }
    }

    void RefreshTitle() =>
        _shell.Title = Active is { } document
            ? $"{(document.IsModified ? "● " : "")}{document.Name} - QX Scripter"
            : "QX Scripter";

    string FolderLabel(string path)
    {
        string folder = Path.GetDirectoryName(path) ?? "";
        if (folder.Length == 0)
            return ScriptsLabel;
        string root = _files.ScriptsDirectory;
        if (PathComparison.Same(folder, root))
            return ScriptsLabel;
        string full = PathComparison.Full(folder);
        string rooted = PathComparison.Full(root);
        if (Inside(full, rooted))
            return ScriptsLabel + "/" + full[(rooted.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/');
        string home = PathComparison.Full(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (home.Length > 0 && Inside(full, home))
            return "~/" + full[(home.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/');
        return full.Replace(Path.DirectorySeparatorChar, '/');
    }

    static bool Inside(string full, string folder) =>
        full.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, PathComparison.Comparison);
}
