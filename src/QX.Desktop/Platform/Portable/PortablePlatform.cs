using Qx.Presentation.Input;
using Qx.Presentation.Platform;

namespace Qx.Desktop.Platform.Portable;

sealed class NoGlobalHotkeys : IGlobalHotkeys
{
    public bool IsSupported => false;

    public Task<IDisposable?> TryRegisterAsync(KeyChord chord, Action pressed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        return Task.FromResult<IDisposable?>(null);
    }
}

sealed class FolderRevealer(ILauncherService launcher) : IFileRevealer
{
    public Task<bool> RevealAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string? folder = Path.GetDirectoryName(path);
        return folder is { Length: > 0 } ? launcher.OpenFolderAsync(folder, cancellationToken) : Task.FromResult(false);
    }
}
