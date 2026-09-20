using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Input;
using Qx.Presentation.Navigation;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Status;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.ViewModels.Editor;
using Qx.Presentation.ViewModels.Room;

namespace Qx.Presentation.ViewModels.Shell;

public static class ShellCommands
{
    public static void Register(
        ICommandRegistry registry,
        ShellViewModel shell,
        INavigationService navigation,
        IPageProvider pages,
        ISessionStatusService status,
        IScriptRunRegistry runs,
        IScriptWorkspace workspace,
        PanicKey panic,
        EditorPreferences editor_preferences,
        ICommandPalette palette)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(panic);
        ArgumentNullException.ThrowIfNull(editor_preferences);
        ArgumentNullException.ThrowIfNull(palette);
        WorkspaceViewModel Editor() => (WorkspaceViewModel)pages.Get(PageKey.Editor);
        RelayCommand OnEditor(Action<WorkspaceViewModel> work) => new(() =>
        {
            navigation.Navigate(PageKey.Editor);
            work(Editor());
        });
        bool HasActive() => workspace.Active is not null;
        bool HasOutput() => workspace.Active?.Console.HasOutput == true;
        var bindings = new Dictionary<string, Binding>(StringComparer.Ordinal)
        {
            [ShellCommandCatalog.FileNew] = new(new RelayCommand(() => Editor().NewCommand.Execute(null)), Always),
            [ShellCommandCatalog.PageLibrary] = new(Toggle(navigation, PageKey.Library), Always),
            [ShellCommandCatalog.FileSave] = new(new RelayCommand(() => Editor().SaveCommand.Execute(null)), HasActive),
            [ShellCommandCatalog.FileSaveAs] = new(new RelayCommand(() => Editor().SaveAsCommand.Execute(null)), HasActive),
            [ShellCommandCatalog.FileClose] = new(new RelayCommand(() => Editor().CloseCommand.Execute(null)), HasActive),
            [ShellCommandCatalog.FileReopen] = new(new RelayCommand(() => Editor().ReopenClosedCommand.Execute(null)), () => workspace.CanReopenClosed),
            [ShellCommandCatalog.RunToggle] = new(new RelayCommand(() => Editor().ToggleRun()), () => workspace.Active is not null),
            [ShellCommandCatalog.RunStopAll] = new(new RelayCommand(() => runs.StopAll()), () => runs.RunningCount > 0),
            [ShellCommandCatalog.RunNextError] = new(new RelayCommand(() => Editor().GoToNextError()), () => workspace.Active is { } document && document.Run.Errors.Any(error => error.Line > 0)),
            [ShellCommandCatalog.ViewCode] = new(OnEditor(editor => editor.SelectCodeMode()), HasActive),
            [ShellCommandCatalog.ViewPanel] = new(OnEditor(editor => editor.SelectPanelMode()), () => workspace.Active is { HasUi: true }),
            [ShellCommandCatalog.ViewConsole] = new(OnEditor(editor => editor.ToggleConsoleCommand.Execute(null)), HasActive),
            [ShellCommandCatalog.ViewApi] = new(OnEditor(editor => editor.ToggleApiBrowser()), Always),
            [ShellCommandCatalog.ViewEditorWrap] = new(new RelayCommand(() => Editor().ToggleEditorWrapCommand.Execute(null)), Always),
            [ShellCommandCatalog.ViewTheme] = new(shell.ToggleThemeCommand, Always),
            [ShellCommandCatalog.ViewTopmost] = new(shell.ToggleTopmostCommand, Always),
            [ShellCommandCatalog.PageLog] = new(Toggle(navigation, PageKey.Log), Always),
            [ShellCommandCatalog.OutputCopy] = new(new RelayCommand(() => workspace.Active?.Console.Copy.CopyCommand.Execute(null)), HasOutput),
            [ShellCommandCatalog.OutputClear] = new(new RelayCommand(() => workspace.Active?.Console.ClearCommand.Execute(null)), HasOutput),
            [ShellCommandCatalog.SessionCopyRoomId] = new(shell.Status.CopyRoomId.CopyCommand, () => status.Current.RoomId > 0),
            [ShellCommandCatalog.RoomInfo] = new(Section(navigation, pages, RoomSection.Info), () => status.Current.IsInRoom),
            [ShellCommandCatalog.RoomPeople] = new(Section(navigation, pages, RoomSection.Users), () => status.Current.IsInRoom),
            [ShellCommandCatalog.RoomFurni] = new(Section(navigation, pages, RoomSection.Furni), () => status.Current.IsInRoom),
            [ShellCommandCatalog.PageSettings] = new(Toggle(navigation, PageKey.Settings), Always),
            [ShellCommandCatalog.PageAbout] = new(Toggle(navigation, PageKey.About), Always),
            [ShellCommandCatalog.PaletteOpen] = new(new RelayCommand(palette.Open), Always),
            [ShellCommandCatalog.DocumentNext] = new(new RelayCommand(() => Editor().NextDocumentCommand.Execute(null)), () => workspace.Documents.Count > 0),
            [ShellCommandCatalog.DocumentPrevious] = new(new RelayCommand(() => Editor().PreviousDocumentCommand.Execute(null)), () => workspace.Documents.Count > 0),
            [ShellCommandCatalog.EditorZoomIn] = new(new RelayCommand(() => Editor().ZoomInCommand.Execute(null)), Always),
            [ShellCommandCatalog.EditorZoomOut] = new(new RelayCommand(() => Editor().ZoomOutCommand.Execute(null)), Always),
            [ShellCommandCatalog.EditorZoomReset] = new(new RelayCommand(() => Editor().ResetZoomCommand.Execute(null)), Always),
            [ShellCommandCatalog.NavigationBack] = new(new RelayCommand(() => navigation.Back()), Always)
        };
        foreach (CommandSpec spec in ShellCommandCatalog.All)
        {
            if (!bindings.TryGetValue(spec.Id, out Binding? binding))
                continue;
            string title = spec.Id == ShellCommandCatalog.ViewEditorWrap ? editor_preferences.WrapTitle : spec.Title;
            string? gesture = spec.Id == ShellCommandCatalog.RunStopAll ? panic.GestureText : spec.GestureText;
            registry.Register(new AppCommand(spec.Id, title, spec.Group, binding.Command, binding.Available, spec.Chords, spec.Route, spec.InPalette, spec.Scope, gesture));
        }
        editor_preferences.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EditorPreferences.WrapTitle))
                registry.Update(ShellCommandCatalog.ViewEditorWrap, command => command with { Title = editor_preferences.WrapTitle });
        };
    }

    static bool Always() => true;

    static RelayCommand Toggle(INavigationService navigation, PageKey key) => new(() => navigation.Toggle(key));

    static RelayCommand Section(INavigationService navigation, IPageProvider pages, RoomSection section) =>
        new(() =>
        {
            navigation.Navigate(PageKey.Room);
            ((RoomViewModel)pages.Get(PageKey.Room)).ShowSection(section);
        });

    sealed record Binding(ICommand Command, Func<bool> Available);
}
