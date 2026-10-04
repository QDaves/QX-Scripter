using Qx.Diagnostics;
using Qx.Presentation.Platform;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Threading;
using Qx.Protocol;

namespace Qx.Desktop.Editor;

public sealed class RoslynHostProvider : IEditorWarmup
{
    readonly AsyncOnce<QxRoslynHost?> _host;
    readonly IMessageResolver _messages;

    public RoslynHostProvider(IAppPaths paths, DesktopRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(runtime);
        WorkingDirectory = paths.ScriptsDirectory;
        _messages = runtime.Messages;
        _host = new AsyncOnce<QxRoslynHost?>(BuildAsync);
    }

    public string WorkingDirectory { get; }

    public Task<QxRoslynHost?> GetAsync(CancellationToken cancellationToken) => _host.GetAsync(cancellationToken);

    public void WarmUp() => _host.GetAsync().Observe("editor");

    async Task<QxRoslynHost?> BuildAsync()
    {
        QxRoslynHost host;
        try
        {
            host = new QxRoslynHost(_messages);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"Editor code-intelligence unavailable ({error.Message}); scripts still run.", "editor");
            return null;
        }
        try
        {
            Directory.CreateDirectory(WorkingDirectory);
            await host.WarmUpAsync(WorkingDirectory);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"Editor warm-up failed ({error.Message}); the first tab may open slower.", "editor");
        }
        return host;
    }
}
