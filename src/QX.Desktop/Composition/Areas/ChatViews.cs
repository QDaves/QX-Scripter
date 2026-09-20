using Qx.Desktop.Views.Chat;
using Qx.Presentation.ViewModels.Chat;

namespace Qx.Desktop.Composition.Areas;

static class ChatViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<ChatViewModel>(static () => new ChatView());
}
