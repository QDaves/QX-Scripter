using Qx.Presentation.Runtime;

namespace Qx.Presentation.Services.Updates;

public sealed class GitHubReleaseSource(DesktopRuntime runtime) : IReleaseSource
{
    readonly DesktopRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public Task<GitHubRelease?> LatestAsync(CancellationToken cancellationToken) =>
        GitHubReleaseUpdates.GetLatestAsync(_runtime.Http, cancellationToken);
}
