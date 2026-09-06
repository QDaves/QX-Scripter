using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Qx.Diagnostics;

namespace Qx.Ui;

/// <summary>
/// Which page the window is showing, and the one place that decides it.
/// </summary>
/// <remarks>
/// <para>
/// Before this the pages hid and showed each other from wherever they were opened, so opening the
/// library over the room left the room underneath, and nothing in the rail said which of them was
/// in front. One method now owns it: everything is hidden, one thing is shown, and the rail is
/// marked to match.
/// </para>
/// <para>
/// The script tab strip and the run button go with it. They belong to the editor, and leaving them
/// standing over a page about the room made the window look like two applications sharing a frame.
/// </para>
/// </remarks>
public partial class MainWindow
{
    private NavPage _page = NavPage.Editor;

    /// <summary>The page in front.</summary>
    public NavPage CurrentPage => _page;

    private GamePage[] GamePages =>
        [LoggingView, Room, GameData, ChatView, FriendsView, NavigatorView, InventoryView,
         WardrobeView, GeneralView, BugReportView, SettingsView, AboutView];

    private void GoTo(NavPage page)
    {
        // Asking for the page already in front steps back to the editor, so the same button both
        // opens and closes it. That is what the removed close button was for.
        if (_page == page && page is not NavPage.Editor)
            page = _tabs.Count == 0 ? NavPage.Library : NavPage.Editor;

        if (page is NavPage.Editor && _tabs.Count == 0)
            page = NavPage.Library;

        _page = page;

        foreach (GamePage view in GamePages)
            view.Visibility = Visibility.Collapsed;

        Home.Visibility = Visibility.Collapsed;
        CodeArea.Visibility = Visibility.Collapsed;
        Panel.Visibility = Visibility.Collapsed;

        // Only the editor keeps its tab strip, its run button and its console. A page about the
        // room has no script to run and no tabs to switch between.
        bool editing = page is NavPage.Editor;
        TabStrip.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceActions.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        OutputShell.Visibility = editing ? OutputShell.Visibility : Visibility.Collapsed;
        OutputSplitter.Visibility = editing ? OutputSplitter.Visibility : Visibility.Collapsed;

        switch (page)
        {
            case NavPage.Library:
                Home.Visibility = Visibility.Visible;
                Home.Reload(_scriptsDir, RunningFilePaths());
                Home.FocusSearch();
                break;

            case NavPage.Editor:
                ShowEditorSurface();
                break;

            default:
                if (PageFor(page) is { } view)
                {
                    view.Visibility = Visibility.Visible;
                    view.Opened();
                }
                break;
        }

        MarkRail();
        RefreshTitle();
    }

    private GamePage? PageFor(NavPage page) => page switch
    {
        NavPage.Logging => LoggingView,
        NavPage.Room => Room,
        NavPage.GameData => GameData,
        NavPage.Chat => ChatView,
        NavPage.Friends => FriendsView,
        NavPage.Navigator => NavigatorView,
        NavPage.Inventory => InventoryView,
        NavPage.Wardrobe => WardrobeView,
        NavPage.General => GeneralView,
        NavPage.BugReport => BugReportView,
        NavPage.Settings => SettingsView,
        NavPage.About => AboutView,
        _ => null
    };

    /// <summary>Lights the one rail button that matches the page in front.</summary>
    private void MarkRail()
    {
        LibraryButton.IsChecked = _page is NavPage.Library;
        LoggingButton.IsChecked = _page is NavPage.Logging;
        RoomButton.IsChecked = _page is NavPage.Room;
        GameDataButton.IsChecked = _page is NavPage.GameData;
        ChatButton.IsChecked = _page is NavPage.Chat;
        FriendsButton.IsChecked = _page is NavPage.Friends;
        NavigatorButton.IsChecked = _page is NavPage.Navigator;
        InventoryButton.IsChecked = _page is NavPage.Inventory;
        WardrobeButton.IsChecked = _page is NavPage.Wardrobe;
        GeneralButton.IsChecked = _page is NavPage.General;
        ReportBugButton.IsChecked = _page is NavPage.BugReport;
        SettingsButton.IsChecked = _page is NavPage.Settings;
        AboutButton.IsChecked = _page is NavPage.About;
    }

    /// <summary>Puts the editor back, on whichever tab was last in front.</summary>
    private void ShowEditorSurface()
    {
        if (_tabs.Count == 0)
        {
            // Nothing to edit, so the library is the only sensible thing to be looking at.
            _page = NavPage.Library;
            Home.Visibility = Visibility.Visible;
            Home.Reload(_scriptsDir, RunningFilePaths());
            return;
        }

        if (TabList.SelectedItem is not ScriptTab)
            TabList.SelectedItem = _tabs[^1];

        ApplyConsoleVisibility();

        if (Active is not { } tab)
        {
            CodeArea.Visibility = Visibility.Visible;
            return;
        }

        ShowEditor(tab);

        // Which of the two the tab is in decides what is shown. Making the code area visible on the
        // way in and leaving it at that meant a script last left in its panel came back to the
        // editor: the toggle read UI, because the tab remembered it, while the code sat in front of
        // it. Coming back from the library did the same thing, so the only way to see the panel was
        // to switch away from it and back.
        if (tab.PanelMode)
            ShowPanel(tab);
        else
            ShowCode();
    }

    private void ShowRoom(object sender, RoutedEventArgs e) => GoTo(NavPage.Room);

    private void ShowLogging(object sender, RoutedEventArgs e) => GoTo(NavPage.Logging);

    private void ShowGameData(object sender, RoutedEventArgs e) => GoTo(NavPage.GameData);

    private void ShowChat(object sender, RoutedEventArgs e) => GoTo(NavPage.Chat);

    private void ShowFriends(object sender, RoutedEventArgs e) => GoTo(NavPage.Friends);

    private void ShowNavigator(object sender, RoutedEventArgs e) => GoTo(NavPage.Navigator);

    private void ShowInventory(object sender, RoutedEventArgs e) => GoTo(NavPage.Inventory);

    private void ShowWardrobe(object sender, RoutedEventArgs e) => GoTo(NavPage.Wardrobe);

    private void ShowGeneral(object sender, RoutedEventArgs e) => GoTo(NavPage.General);

    private void ShowBugReport(object sender, RoutedEventArgs e) => GoTo(NavPage.BugReport);

    private void ShowSettings(object sender, RoutedEventArgs e) => GoTo(NavPage.Settings);

    private void ShowAbout(object sender, RoutedEventArgs e) => GoTo(NavPage.About);

    /// <summary>Hands the state to every page once, when the window has it.</summary>
    private void GivePagesState()
    {
        foreach (GamePage view in GamePages)
        {
            view.Game = _game;
            view.Application = _runtime?.Application;
        }

        GeneralView.Rules = _runtime?.Rules;
        SettingsView.Rules = _runtime?.Rules;
    }

    private void GiveToolPagesState()
    {
        SettingsView.Bind(_scriptsDir, _mcp, () =>
            _extension.ConnectedPort == 0 ? _gearthPort : _extension.ConnectedPort, () =>
        {
            RefreshThemeButton();
            RebuildEditors();
        });

        BugReportView.Bind(() =>
        {
            string client = _status?.GameClientTooltip ?? "";
            int gearth_port = _extension.ConnectedPort == 0 ? _gearthPort : _extension.ConnectedPort;
            return new BugReportContext(
                Qx.ProductVersion.Current,
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                ".NET " + Environment.Version,
                _extension.IsInterceptorConnected ? $"connected on port {gearth_port}" : "not connected",
                client.Length == 0 ? "not connected" : client,
                _mcp is { IsRunning: true } mcp ? $"running on port {mcp.Port}" : "not running");
        }, LogPath, Path.Combine(Path.GetTempPath(), "qx_crash.log"));

    }

    /// <summary>Whether a page is in front that Escape should close.</summary>
    private bool OnGamePage => _page is not (NavPage.Editor or NavPage.Library);

    /// <summary>Whether the page in front is holding a search that Escape should clear first.</summary>
    private bool PageIsSearching => PageFor(_page)?.IsSearching == true;
}
