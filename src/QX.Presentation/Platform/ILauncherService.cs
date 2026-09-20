namespace Qx.Presentation.Platform;

public interface ILauncherService
{
    Task<bool> OpenUriAsync(Uri uri, CancellationToken cancellation_token = default);

    Task<bool> OpenFolderAsync(string path, CancellationToken cancellation_token = default);
}
