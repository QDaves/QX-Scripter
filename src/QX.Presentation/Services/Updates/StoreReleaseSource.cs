using Qx.Presentation.Runtime;

namespace Qx.Presentation.Services.Updates;

public sealed class StoreReleaseSource(DesktopRuntime runtime) : IReleaseSource
{
    readonly DesktopRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public Task<Release?> LatestAsync(CancellationToken cancellationToken) =>
        GitHubReleaseUpdates.GetStoreReleaseAsync(_runtime.Http, cancellationToken);
}
