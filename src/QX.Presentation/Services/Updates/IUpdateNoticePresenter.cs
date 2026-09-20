using Qx.Updates;

namespace Qx.Presentation.Services.Updates;

public interface IUpdateNoticePresenter
{
    Task ShowAsync(string installed_version, GitHubRelease release, CancellationToken cancellation_token);
}
