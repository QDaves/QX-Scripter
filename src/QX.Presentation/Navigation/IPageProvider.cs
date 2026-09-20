using Qx.Presentation.Mvvm;

namespace Qx.Presentation.Navigation;

public interface IPageProvider
{
    PageViewModel Get(PageKey key);

    bool IsCreated(PageKey key);
}
