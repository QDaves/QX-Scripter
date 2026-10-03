namespace Qx.Presentation.Services.Updates;

public interface IReleaseSource
{
    Task<GitHubRelease?> LatestAsync(CancellationToken cancellationToken);
}
