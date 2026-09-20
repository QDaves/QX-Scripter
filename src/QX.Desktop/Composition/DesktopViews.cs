using Qx.Desktop.Composition.Areas;
using Qx.Desktop.Editor;
using Qx.Desktop.Views.Dialogs;
using Qx.Presentation.Dialogs;

namespace Qx.Desktop.Composition;

public static class DesktopViews
{
    public static void RegisterAll(ViewRegistry views, RoslynHostProvider hosts)
    {
        ArgumentNullException.ThrowIfNull(views);
        views
            .Register<ConfirmDialogViewModel>(static () => new ConfirmDialogView())
            .Register<AlertDialogViewModel>(static () => new AlertDialogView())
            .Register<PromptDialogViewModel>(static () => new PromptDialogView())
            .Register<UpdateDialogViewModel>(static () => new UpdateDialogView());
        EditorViews.Register(views, hosts);
        LibraryViews.Register(views);
        LogViews.Register(views);
        RoomViews.Register(views);
        GeneralViews.Register(views);
        ChatViews.Register(views);
        FriendsViews.Register(views);
        NavigatorViews.Register(views);
        InventoryViews.Register(views);
        WardrobeViews.Register(views);
        GameCatalogViews.Register(views);
        SettingsViews.Register(views);
        AboutViews.Register(views);
        BugReportsViews.Register(views);
        ScriptPanelsViews.Register(views);
        ApiBrowserViews.Register(views);
        CommandPaletteViews.Register(views);
    }
}
