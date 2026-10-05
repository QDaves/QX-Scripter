namespace Qx.Presentation.Services.Updates;

public interface IUpdateNoticePresenter
{
    Task ShowAsync(string installedVersion, Release release, CancellationToken cancellationToken);
}
