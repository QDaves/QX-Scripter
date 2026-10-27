using Qx.Desktop.Views.Library;
using Qx.Presentation.ViewModels.Library;

namespace Qx.Desktop.Composition.Areas;

static class LibraryViews
{
    public static void Register(ViewRegistry views) =>
        views
            .Register<LibraryViewModel>(static () => new LibraryView())
            .Register<CommunityViewModel>(static () => new CommunityView())
            .Register<CommunityRunDialogViewModel>(static () => new CommunityRunDialogView())
            .Register<CategoryDialogViewModel>(static () => new CategoryDialogView());
}
