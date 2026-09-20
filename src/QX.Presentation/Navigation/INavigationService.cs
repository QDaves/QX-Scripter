using Qx.Presentation.Mvvm;

namespace Qx.Presentation.Navigation;

public interface INavigationService
{
    PageKey Current { get; }

    PageViewModel? CurrentPage { get; }

    bool IsStarted { get; }

    event Action<PageKey>? Navigated;

    void Start();

    void Navigate(PageKey key);

    void Toggle(PageKey key);

    void ShowWorkspace();

    bool Back();
}
