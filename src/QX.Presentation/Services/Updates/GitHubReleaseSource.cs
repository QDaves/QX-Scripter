using Qx.Presentation.Runtime;
using Qx.Updates;

namespace Qx.Presentation.Services.Updates;

public sealed class GitHubReleaseSource(DesktopRuntime runtime) : IReleaseSource
{
    readonly DesktopRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public Task<GitHubRelease?> LatestAsync(CancellationToken cancellation_token) =>
        GitHubReleaseUpdates.GetLatestAsync(_runtime.Http, cancellation_token);
}
