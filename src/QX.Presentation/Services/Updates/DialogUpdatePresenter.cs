using Qx.Presentation.Dialogs;
using Qx.Presentation.Platform;

namespace Qx.Presentation.Services.Updates;

public sealed class DialogUpdatePresenter(IDialogService dialogs, ILauncherService launcher) : IUpdateNoticePresenter
{
    public Task ShowAsync(string installedVersion, GitHubRelease release, CancellationToken cancellationToken) =>
        dialogs.ShowAsync(new UpdateDialogViewModel(installedVersion, release, launcher), cancellationToken);
}
