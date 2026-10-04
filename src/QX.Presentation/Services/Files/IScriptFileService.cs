using Qx.Presentation.Services.Library;

namespace Qx.Presentation.Services.Files;

public sealed record FileOperationResult(bool Succeeded, string? Failure)
{
    public static FileOperationResult Ok { get; } = new(true, null);

    public static FileOperationResult Failed(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new FileOperationResult(false, error.Message);
    }
}

public interface IScriptFileService
{
    string ScriptsDirectory { get; }

    string PathFor(string typedName);

    bool Exists(string path);

    Task<IReadOnlyList<ScriptFileEntry>> ListAsync(CancellationToken cancellationToken);

    Task<string?> ReadAsync(string path, CancellationToken cancellationToken);

    Task<FileOperationResult> WriteAsync(string path, string text, CancellationToken cancellationToken);

    Task<FileOperationResult> MoveAsync(string from, string to, CancellationToken cancellationToken);

    Task<FileOperationResult> CopyAsync(string from, string to, CancellationToken cancellationToken);

    Task<FileOperationResult> DeleteAsync(string path, CancellationToken cancellationToken);

    IDisposable Watch(Action changed);
}
