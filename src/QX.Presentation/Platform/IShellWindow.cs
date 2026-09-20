using Qx.Presentation.Services.Settings;

namespace Qx.Presentation.Platform;

public interface IShellWindow
{
    bool IsVisible { get; }

    bool IsMinimized { get; }

    bool IsActive { get; }

    bool Topmost { get; set; }

    string Title { get; set; }

    event Action? Activated;

    event Action? VisibilityChanged;

    void ShowAndActivate();

    void PrepareForHost(WindowPlacement? stored);

    void HideForHost();

    void Hide();

    void Shutdown(int exit_code);

    WindowPlacement? CapturePlacement();
}
