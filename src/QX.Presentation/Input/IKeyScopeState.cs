using Qx.Presentation.Navigation;

namespace Qx.Presentation.Input;

public interface IKeyScopeState
{
    PageKey CurrentPage { get; }

    bool IsCodeView { get; }

    bool IsOverlayOpen { get; }
}
