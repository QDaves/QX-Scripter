namespace Qx.Presentation.Platform;

public interface IFilePickerService
{
    Task<FilePickResult> PickFileAsync(string title, CancellationToken cancellationToken = default);

    Task<FilePickResult> SaveTextAsync(string suggestedName, string content, CancellationToken cancellationToken = default);
}
