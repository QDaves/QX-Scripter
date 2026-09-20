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

    Task SaveDraftsAsync(bool include_modified_files, CancellationToken cancellation_token);

    Task ClearDraftsAsync(CancellationToken cancellation_token);

    bool SaveDraftsNow(bool include_modified_files, TimeSpan budget);

    void SealDrafts();
}
