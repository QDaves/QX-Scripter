using Qx.Updates;

namespace Qx.Presentation.Services.Updates;

public interface IReleaseSource
{
    Task<GitHubRelease?> LatestAsync(CancellationToken cancellation_token);
}
