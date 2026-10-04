namespace Qx.Presentation.Services.Updates;

public sealed class NoReleaseSource : IReleaseSource
{
    public Task<GitHubRelease?> LatestAsync(CancellationToken cancellationToken) => Task.FromResult<GitHubRelease?>(null);
}
