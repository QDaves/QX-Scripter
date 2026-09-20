using Qx.Presentation.Visuals;

namespace Qx.Presentation.Navigation;

public static class PageCatalog
{
    public const string NewScriptRailTip = "New script";
    public const string NewScriptCommandId = "file.new";

    public static IReadOnlyList<PageDescriptor> All { get; } =
    [
        new(PageKey.Editor, "Editor", "Editor", IconKind.Code, PageGroup.Workspace, 0, "view.editor"),
        new(PageKey.Log, "Application log", "Application log", IconKind.Log, PageGroup.Workspace, 1, "page.log"),
        new(PageKey.Library, "Script library", "Script library", IconKind.Library, PageGroup.Workspace, 2, "page.library"),
        new(PageKey.Room, "Room", "Room", IconKind.Room, PageGroup.Game, 1, "page.room"),
        new(PageKey.General, "General", "General", IconKind.General, PageGroup.Game, 2, "page.general"),
        new(PageKey.Chat, "Chat", "Chat", IconKind.Chat, PageGroup.Game, 3, "page.chat"),
        new(PageKey.Friends, "Friends", "Friends", IconKind.Friends, PageGroup.Game, 4, "page.friends"),
        new(PageKey.Navigator, "Navigator", "Navigator", IconKind.Navigator, PageGroup.Game, 5, "page.navigator"),
        new(PageKey.Inventory, "Inventory", "Inventory", IconKind.Inventory, PageGroup.Game, 6, "page.inventory"),
        new(PageKey.Wardrobe, "Wardrobe", "Wardrobe", IconKind.Wardrobe, PageGroup.Game, 7, "page.wardrobe"),
        new(PageKey.GameData, "Game data", "Game data", IconKind.GameData, PageGroup.Game, 8, "page.gamedata"),
        new(PageKey.BugReport, "Report a bug", "Report a bug", IconKind.Bug, PageGroup.Support, 1, "page.bugreport"),
        new(PageKey.Settings, "Settings", "Settings", IconKind.Settings, PageGroup.Support, 2, "page.settings"),
        new(PageKey.About, "About", "About QX Scripter", IconKind.About, PageGroup.Support, 3, "page.about")
    ];

    public static PageDescriptor For(PageKey key) =>
        All.FirstOrDefault(page => page.Key == key) ?? throw new ArgumentOutOfRangeException(nameof(key), key, null);

    public static IEnumerable<PageDescriptor> InGroup(PageGroup group) =>
        All.Where(page => page.Group == group && page.InRail).OrderBy(page => page.Order);
}
