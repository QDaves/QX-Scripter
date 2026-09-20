using Qx.Updates;

namespace Qx.Presentation.Services.Updates;

public sealed class NoReleaseSource : IReleaseSource
{
    public Task<GitHubRelease?> LatestAsync(CancellationToken cancellation_token) => Task.FromResult<GitHubRelease?>(null);
}
