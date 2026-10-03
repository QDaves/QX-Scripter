using Qx.Mcp;

namespace Qx.Presentation.Services.Editor;

public sealed class DeferredEditorBridge : IEditorBridge
{
    public const string Unavailable = "editor UI not available";

    IEditorBridge? _target;

    public void Attach(IEditorBridge target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (ReferenceEquals(target, this))
            throw new ArgumentException("The bridge cannot forward to itself.", nameof(target));
        Volatile.Write(ref _target, target);
    }

    public void Detach() => Volatile.Write(ref _target, null);

    public Task<string> ListTabsAsync(CancellationToken cancellationToken) =>
        Target?.ListTabsAsync(cancellationToken) ?? MissingAsync();

    public Task<string> OpenTabAsync(string name, CancellationToken cancellationToken) =>
        Target?.OpenTabAsync(name, cancellationToken) ?? MissingAsync();

    public Task<string> CreateTabAsync(string name, string code, CancellationToken cancellationToken) =>
        Target?.CreateTabAsync(name, code, cancellationToken) ?? MissingAsync();

    public Task<string> EditActiveTabAsync(string code, CancellationToken cancellationToken) =>
        Target?.EditActiveTabAsync(code, cancellationToken) ?? MissingAsync();

    public Task<string> SelectTabAsync(string name, CancellationToken cancellationToken) =>
        Target?.SelectTabAsync(name, cancellationToken) ?? MissingAsync();

    public Task<string> CloseTabAsync(string name, bool discard, CancellationToken cancellationToken) =>
        Target?.CloseTabAsync(name, discard, cancellationToken) ?? MissingAsync();

    public Task<string> RunActiveTabAsync(string name, CancellationToken cancellationToken) =>
        Target?.RunActiveTabAsync(name, cancellationToken) ?? MissingAsync();

    public Task<string> StopActiveTabAsync(string name, CancellationToken cancellationToken) =>
        Target?.StopActiveTabAsync(name, cancellationToken) ?? MissingAsync();

    public Task<string> GetTabOutputAsync(string name, CancellationToken cancellationToken) =>
        Target?.GetTabOutputAsync(name, cancellationToken) ?? MissingAsync();

    public Task<string> GetTabStatusAsync(string name, CancellationToken cancellationToken) =>
        Target?.GetTabStatusAsync(name, cancellationToken) ?? MissingAsync();

    public Task<string> GetTabErrorsAsync(string name, CancellationToken cancellationToken) =>
        Target?.GetTabErrorsAsync(name, cancellationToken) ?? MissingAsync();

    public Task<string?> ReadOpenScriptAsync(string name, CancellationToken cancellationToken) =>
        Target?.ReadOpenScriptAsync(name, cancellationToken) ?? Task.FromResult<string?>(null);

    public Task<string?> EditOpenScriptAsync(string name, Func<string, string> edit, CancellationToken cancellationToken) =>
        Target?.EditOpenScriptAsync(name, edit, cancellationToken) ?? Task.FromResult<string?>(null);

    public Task<string?> RenameScriptAsync(string name, string newName, CancellationToken cancellationToken) =>
        Target?.RenameScriptAsync(name, newName, cancellationToken) ?? Task.FromResult<string?>(null);

    public Task<string?> DeleteScriptAsync(string name, CancellationToken cancellationToken) =>
        Target?.DeleteScriptAsync(name, cancellationToken) ?? Task.FromResult<string?>(null);

    IEditorBridge? Target => Volatile.Read(ref _target);

    static Task<string> MissingAsync() => Task.FromResult(Unavailable);
}
