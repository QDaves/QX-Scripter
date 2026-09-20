using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Mvvm;

namespace Qx.Presentation.Navigation;

public sealed partial class NavigationService : ObservableObject, INavigationService
{
    readonly IPageProvider _pages;
    readonly IWorkspacePresence _workspace;

    public NavigationService(IPageProvider pages, IWorkspacePresence workspace)
    {
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        Current = PageKey.Library;
    }

    [ObservableProperty]
    public partial PageKey Current { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStarted))]
    public partial PageViewModel? CurrentPage { get; private set; }

    public bool IsStarted => CurrentPage is not null;

    public event Action<PageKey>? Navigated;

    public void Start()
    {
        if (CurrentPage is null)
            Show(_workspace.HasDocuments ? PageKey.Editor : PageKey.Library);
    }

    public void Navigate(PageKey key)
    {
        if (key == PageKey.Editor && !_workspace.HasDocuments)
            key = PageKey.Library;
        if (CurrentPage is not null && key == Current)
            return;
        Show(key);
    }

    public void Toggle(PageKey key)
    {
        if (CurrentPage is not null && key == Current && key != PageKey.Editor)
        {
            ShowWorkspace();
            return;
        }
        Navigate(key);
    }

    public void ShowWorkspace() => Navigate(_workspace.HasDocuments ? PageKey.Editor : PageKey.Library);

    public bool Back()
    {
        if (CurrentPage is not { } page)
            return false;
        if (page.TryClearSearch())
            return true;
        if (Current == PageKey.Editor)
            return false;
        if (Current == PageKey.Library && !_workspace.HasDocuments)
            return false;
        ShowWorkspace();
        return true;
    }

    void Show(PageKey key)
    {
        PageViewModel next = _pages.Get(key);
        PageViewModel? previous = CurrentPage;
        Current = key;
        CurrentPage = next;
        previous?.Deactivate();
        next.Activate();
        Navigated?.Invoke(key);
    }
}
