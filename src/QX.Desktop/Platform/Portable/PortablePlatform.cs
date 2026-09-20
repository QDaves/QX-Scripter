using Qx.Presentation.Input;
using Qx.Presentation.Platform;

namespace Qx.Desktop.Platform.Portable;

sealed class NoKeyboardState : IKeyboardState
{
    public bool IsSupported => false;

    public bool IsShiftDown() => false;
}

sealed class NoGlobalHotkeys : IGlobalHotkeys
{
    public bool IsSupported => false;

    public Task<IDisposable?> TryRegisterAsync(KeyChord chord, Action pressed, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        return Task.FromResult<IDisposable?>(null);
    }
}

sealed class FolderRevealer(ILauncherService launcher) : IFileRevealer
{
    public Task<bool> RevealAsync(string path, CancellationToken cancellation_token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string? folder = Path.GetDirectoryName(path);
        return folder is { Length: > 0 } ? launcher.OpenFolderAsync(folder, cancellation_token) : Task.FromResult(false);
    }
}
