namespace Qx.Presentation.Platform;

public interface IFileRevealer
{
    Task<bool> RevealAsync(string path, CancellationToken cancellationToken = default);
}
