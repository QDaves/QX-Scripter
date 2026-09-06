using System.Windows;
using Qx.Updates;

namespace Qx.Ui;

public partial class MainWindow
{
    private static readonly TimeSpan ReleaseCheckTimeout = TimeSpan.FromSeconds(5);

    private GitHubRelease? _pending_update;
    private bool _update_check_started;
    private bool _update_notice_shown;

    private void StartUpdateCheck()
    {
        if (_update_check_started || _runtime is null)
            return;

        _update_check_started = true;
        Observe(CheckForUpdateAsync);
    }

    private async Task CheckForUpdateAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        timeout.CancelAfter(ReleaseCheckTimeout);
        GitHubRelease? release = await GitHubReleaseUpdates.GetLatestAsync(
            _runtime!.Http,
            timeout.Token);
        await InvokeUiAsync(() =>
        {
            if (release is null || !GitHubReleaseUpdates.ShouldNotify(
                    Qx.ProductVersion.Current,
                    App.Settings.LastNotifiedRelease == $"{Qx.ProjectLinks.Repository}/{release.Tag}"
                        ? release.Tag : null,
                    release))
            {
                return true;
            }

            _pending_update = release;
            TryShowUpdateNotice();
            return true;
        }, _cts.Token);
    }

    private void TryShowUpdateNotice()
    {
        if (_closed || _update_notice_shown || _pending_update is not { } release ||
            !IsVisible || WindowState == WindowState.Minimized)
        {
            return;
        }

        _update_notice_shown = true;
        _pending_update = null;
        try
        {
            UpdateDialog.Show(this, Qx.ProductVersion.Current, release);
            App.Settings.LastNotifiedRelease = $"{Qx.ProjectLinks.Repository}/{release.Tag}";
        }
        catch
        {
            _update_notice_shown = false;
            _pending_update = release;
            throw;
        }
    }
}
