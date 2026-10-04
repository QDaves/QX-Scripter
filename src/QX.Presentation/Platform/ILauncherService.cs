namespace Qx.Presentation.Platform;

public interface ILauncherService
{
    Task<bool> OpenUriAsync(Uri uri, CancellationToken cancellationToken = default);

    Task<bool> OpenFolderAsync(string path, CancellationToken cancellationToken = default);
}
