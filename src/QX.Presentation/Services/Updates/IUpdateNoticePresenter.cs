namespace Qx.Presentation.Services.Updates;

public interface IUpdateNoticePresenter
{
    Task ShowAsync(string installedVersion, GitHubRelease release, CancellationToken cancellationToken);
}
