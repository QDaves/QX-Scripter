namespace Qx.Presentation.Services.Images;

public interface IImageService
{
    Task<byte[]?> LoadBytesAsync(string? url, CancellationToken cancellation_token = default);

    Task<bool> PreloadAsync(string? url, CancellationToken cancellation_token = default);
}
