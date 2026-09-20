namespace Qx.Presentation.Input;

public static class ShellCommandCatalog
{
    public const string FileNew = "file.new";
    public const string PageLibrary = "page.library";
    public const string FileSave = "file.save";
    public const string FileSaveAs = "file.saveAs";
    public const string FileClose = "file.close";
    public const string FileReopen = "file.reopen";
    public const string RunToggle = "run.toggle";
    public const string RunStopAll = "run.stopAll";
    public const string RunNextError = "run.nextError";
    public const string ViewCode = "view.code";
    public const string ViewPanel = "view.panel";
    public const string ViewConsole = "view.console";
    public const string ViewApi = "view.api";
    public const string ViewEditorWrap = "view.editorWrap";
    public const string ViewTheme = "view.theme";
    public const string ViewTopmost = "view.topmost";
    public const string PageLog = "page.log";
    public const string OutputCopy = "output.copy";
    public const string OutputClear = "output.clear";
    public const string SessionCopyRoomId = "session.copyRoomId";
    public const string RoomInfo = "room.info";
    public const string RoomPeople = "room.people";
    public const string RoomFurni = "room.furni";
    public const string PageSettings = "page.settings";
    public const string PageAbout = "page.about";
    public const string PaletteOpen = "palette.open";
    public const string DocumentNext = "document.next";
    public const string DocumentPrevious = "document.previous";
    public const string EditorZoomIn = "editor.zoomIn";
    public const string EditorZoomOut = "editor.zoomOut";
    public const string EditorZoomReset = "editor.zoomReset";
    public const string NavigationBack = "navigation.back";

    public static IReadOnlyList<CommandSpec> All { get; } =
    [
        new(FileNew, "New script", CommandGroup.Script, [new KeyChord(ChordKey.N, ChordModifiers.Primary)], KeyRoute.Tunnel),
        new(PageLibrary, "Open the script library", CommandGroup.Script, [new KeyChord(ChordKey.O, ChordModifiers.Primary)], KeyRoute.Tunnel),
        new(FileSave, "Save", CommandGroup.Script, [new KeyChord(ChordKey.S, ChordModifiers.Primary)], KeyRoute.Tunnel, KeyScope.EditorPage),
        new(FileSaveAs, "Save as…", CommandGroup.Script, [new KeyChord(ChordKey.S, ChordModifiers.Primary | ChordModifiers.Shift)], KeyRoute.Tunnel, KeyScope.EditorPage),
        new(FileClose, "Close the tab", CommandGroup.Script, [new KeyChord(ChordKey.W, ChordModifiers.Primary)], KeyRoute.Tunnel, KeyScope.EditorPage),
        new(FileReopen, "Reopen the last closed script", CommandGroup.Script, [new KeyChord(ChordKey.T, ChordModifiers.Primary | ChordModifiers.Shift)], KeyRoute.Tunnel),
        new(RunToggle, "Run or stop the script", CommandGroup.Run, [new KeyChord(ChordKey.F5)], KeyRoute.Tunnel, KeyScope.EditorPage),
        new(RunStopAll, "Stop every running script", CommandGroup.Run, []),
        new(RunNextError, "Go to the next error", CommandGroup.Run, [new KeyChord(ChordKey.F8)], KeyRoute.Tunnel, KeyScope.EditorPage),
        new(ViewCode, "Code view", CommandGroup.View, [new KeyChord(ChordKey.D1, ChordModifiers.Primary)], KeyRoute.Tunnel, KeyScope.EditorPage),
        new(ViewPanel, "UI panel view", CommandGroup.View, [new KeyChord(ChordKey.D2, ChordModifiers.Primary)], KeyRoute.Tunnel, KeyScope.EditorPage),
        new(ViewConsole, "Show or hide output", CommandGroup.View, [new KeyChord(ChordKey.J, ChordModifiers.Primary)], KeyRoute.Tunnel, KeyScope.EditorCode),
        new(ViewApi, "Script API library", CommandGroup.View, [new KeyChord(ChordKey.F2)], KeyRoute.Tunnel, KeyScope.EditorPage),
        new(ViewEditorWrap, "Wrap editor lines", CommandGroup.View, []),
        new(ViewTheme, "Switch theme", CommandGroup.View, []),
        new(ViewTopmost, "Keep the window on top", CommandGroup.View, []),
        new(PageLog, "Application log", CommandGroup.View, []),
        new(OutputCopy, "Copy output", CommandGroup.Output, []),
        new(OutputClear, "Clear output", CommandGroup.Output, []),
        new(SessionCopyRoomId, "Copy the room id", CommandGroup.Session, []),
        new(RoomInfo, "Room", CommandGroup.Room, []),
        new(RoomPeople, "People in the room", CommandGroup.Room, []),
        new(RoomFurni, "Furni in the room", CommandGroup.Room, []),
        new(PageSettings, "Settings and shortcuts", CommandGroup.Window, [new KeyChord(ChordKey.F1)], KeyRoute.Tunnel),
        new(PageAbout, "About QX Scripter", CommandGroup.Window, []),
        new(PaletteOpen, "Open the command palette", CommandGroup.Window, [new KeyChord(ChordKey.P, ChordModifiers.Primary | ChordModifiers.Shift)], KeyRoute.Tunnel, InPalette: false),
        new(DocumentNext, "Next tab", CommandGroup.Script, [new KeyChord(ChordKey.Tab, ChordModifiers.Control)], KeyRoute.Tunnel, InPalette: false),
        new(DocumentPrevious, "Previous tab", CommandGroup.Script, [new KeyChord(ChordKey.Tab, ChordModifiers.Control | ChordModifiers.Shift)], KeyRoute.Tunnel, InPalette: false),
        new(EditorZoomIn, "Zoom in", CommandGroup.View, [new KeyChord(ChordKey.Plus, ChordModifiers.Primary), new KeyChord(ChordKey.Add, ChordModifiers.Primary)], KeyRoute.Tunnel, KeyScope.EditorCode, InPalette: false),
        new(EditorZoomOut, "Zoom out", CommandGroup.View, [new KeyChord(ChordKey.Minus, ChordModifiers.Primary), new KeyChord(ChordKey.Subtract, ChordModifiers.Primary)], KeyRoute.Tunnel, KeyScope.EditorCode, InPalette: false),
        new(EditorZoomReset, "Reset zoom", CommandGroup.View, [new KeyChord(ChordKey.D0, ChordModifiers.Primary), new KeyChord(ChordKey.NumPad0, ChordModifiers.Primary)], KeyRoute.Tunnel, KeyScope.EditorCode, InPalette: false),
        new(NavigationBack, "Back", CommandGroup.Window, [new KeyChord(ChordKey.Escape)], KeyRoute.Bubble, InPalette: false)
    ];

    public static CommandSpec? Find(string id) =>
        All.FirstOrDefault(spec => string.Equals(spec.Id, id, StringComparison.Ordinal));
}
