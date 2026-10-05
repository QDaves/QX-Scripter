namespace Qx.Presentation.Services.Updates;

public interface IReleaseSource
{
    Task<Release?> LatestAsync(CancellationToken cancellationToken);
}
