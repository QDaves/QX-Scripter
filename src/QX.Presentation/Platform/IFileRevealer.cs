namespace Qx.Presentation.Platform;

public interface IFileRevealer
{
    Task<bool> RevealAsync(string path, CancellationToken cancellation_token = default);
}
