using Qx.Presentation.Dialogs;
using Qx.Presentation.Platform;
using Qx.Updates;

namespace Qx.Presentation.Services.Updates;

public sealed class DialogUpdatePresenter(IDialogService dialogs, ILauncherService launcher) : IUpdateNoticePresenter
{
    public Task ShowAsync(string installed_version, GitHubRelease release, CancellationToken cancellation_token) =>
        dialogs.ShowAsync(new UpdateDialogViewModel(installed_version, release, launcher), cancellation_token);
}
