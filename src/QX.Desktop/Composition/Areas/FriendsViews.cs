using Qx.Desktop.Views.Friends;
using Qx.Presentation.ViewModels.Friends;

namespace Qx.Desktop.Composition.Areas;

static class FriendsViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<FriendsViewModel>(static () => new FriendsView());
}
