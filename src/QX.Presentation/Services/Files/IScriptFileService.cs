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

    string PathFor(string typed_name);

    bool Exists(string path);

    Task<IReadOnlyList<ScriptFileEntry>> ListAsync(CancellationToken cancellation_token);

    Task<string?> ReadAsync(string path, CancellationToken cancellation_token);

    Task<FileOperationResult> WriteAsync(string path, string text, CancellationToken cancellation_token);

    Task<FileOperationResult> MoveAsync(string from, string to, CancellationToken cancellation_token);

    Task<FileOperationResult> CopyAsync(string from, string to, CancellationToken cancellation_token);

    Task<FileOperationResult> DeleteAsync(string path, CancellationToken cancellation_token);

    IDisposable Watch(Action changed);
}
