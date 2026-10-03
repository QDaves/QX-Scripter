namespace Qx.Presentation.Services.Images;

public interface IImageService
{
    Task<byte[]?> LoadBytesAsync(string? url, CancellationToken cancellationToken = default);

    Task<bool> PreloadAsync(string? url, CancellationToken cancellationToken = default);
}
