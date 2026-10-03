using Qx.Presentation.Navigation;
using Qx.Presentation.Services.Settings;

namespace Qx.Presentation.Services.Lifecycle;

public interface IWorkspaceSession : IWorkspacePresence
{
    int ModifiedCount { get; }

    SessionState CaptureSession();

    void RememberAllPanels();

    Task StopAutosaveAsync();

    void StopAutosave();

    Task SaveDraftsAsync(bool includeModifiedFiles, CancellationToken cancellationToken);

    Task ClearDraftsAsync(CancellationToken cancellationToken);

    bool SaveDraftsNow(bool includeModifiedFiles, TimeSpan budget);

    void SealDrafts();
}
