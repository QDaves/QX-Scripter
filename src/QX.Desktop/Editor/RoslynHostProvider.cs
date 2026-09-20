using Qx.Diagnostics;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Threading;

namespace Qx.Desktop.Editor;

public sealed class RoslynHostProvider : IEditorWarmup
{
    readonly AsyncOnce<QxRoslynHost?> _host;

    public RoslynHostProvider(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        WorkingDirectory = paths.ScriptsDirectory;
        _host = new AsyncOnce<QxRoslynHost?>(BuildAsync);
    }

    public string WorkingDirectory { get; }

    public Task<QxRoslynHost?> GetAsync(CancellationToken cancellation_token) => _host.GetAsync(cancellation_token);

    public void WarmUp() => _host.GetAsync().Observe("editor");

    static Task<QxRoslynHost?> BuildAsync()
    {
        try
        {
            return Task.FromResult<QxRoslynHost?>(new QxRoslynHost());
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"Editor code-intelligence unavailable ({error.Message}); scripts still run.", "editor");
            return Task.FromResult<QxRoslynHost?>(null);
        }
    }
}
