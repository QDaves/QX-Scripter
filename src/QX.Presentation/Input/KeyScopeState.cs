using Qx.Presentation.Dialogs;
using Qx.Presentation.Navigation;

namespace Qx.Presentation.Input;

public sealed class KeyScopeState(INavigationService navigation, IWorkspacePresence workspace, IDialogService dialogs, ICommandPalette palette) : IKeyScopeState
{
    public PageKey CurrentPage => navigation.Current;

    public bool IsCodeView => workspace.IsCodeViewActive;

    public bool IsOverlayOpen => dialogs.IsOpen || palette.IsOpen;
}
