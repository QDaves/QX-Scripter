using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Library;
using Qx.Presentation.Services.Runs;
using Qx.Scripting.Hosting;

namespace Qx.Presentation.Services.Workspace;

public sealed class DocumentRunHooks(ScriptDocument document, IScriptFileService files, IScriptLibrary library) : IRunHooks
{
    public async Task SaveBeforeRunAsync(string code, CancellationToken cancellationToken)
    {
        if (document.FilePath is not { } path)
            return;
        FileOperationResult result = await files.WriteAsync(path, code, cancellationToken);
        if (!result.Succeeded)
            throw new IOException(result.Failure);
        document.MarkClean(code);
    }

    public void RecordStarted(DateTimeOffset at)
    {
        if (document.LibraryName is { Length: > 0 } name)
            library.RecordRunStarted(name, at);
    }

    public void RecordFinished(ScriptRunState state)
    {
        if (document.LibraryName is { Length: > 0 } name)
            library.RecordRunFinished(name, state);
    }
}
