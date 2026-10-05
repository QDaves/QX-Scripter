namespace Qx.Presentation.Services.Updates;

public sealed class NoReleaseSource : IReleaseSource
{
    public Task<Release?> LatestAsync(CancellationToken cancellationToken) => Task.FromResult<Release?>(null);
}
