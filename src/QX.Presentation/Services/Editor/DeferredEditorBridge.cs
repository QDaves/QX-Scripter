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

    public Task<string> ListTabsAsync(CancellationToken cancellation_token) =>
        Target?.ListTabsAsync(cancellation_token) ?? MissingAsync();

    public Task<string> GetActiveTabAsync(CancellationToken cancellation_token) =>
        Target?.GetActiveTabAsync(cancellation_token) ?? MissingAsync();

    public Task<string> OpenTabAsync(string name, CancellationToken cancellation_token) =>
        Target?.OpenTabAsync(name, cancellation_token) ?? MissingAsync();

    public Task<string> CreateTabAsync(string name, string code, CancellationToken cancellation_token) =>
        Target?.CreateTabAsync(name, code, cancellation_token) ?? MissingAsync();

    public Task<string> EditActiveTabAsync(string code, CancellationToken cancellation_token) =>
        Target?.EditActiveTabAsync(code, cancellation_token) ?? MissingAsync();

    public Task<string> SelectTabAsync(string name, CancellationToken cancellation_token) =>
        Target?.SelectTabAsync(name, cancellation_token) ?? MissingAsync();

    public Task<string> CloseTabAsync(string name, CancellationToken cancellation_token) =>
        Target?.CloseTabAsync(name, cancellation_token) ?? MissingAsync();

    public Task<string> RunActiveTabAsync(string name, CancellationToken cancellation_token) =>
        Target?.RunActiveTabAsync(name, cancellation_token) ?? MissingAsync();

    public Task<string> StopActiveTabAsync(string name, CancellationToken cancellation_token) =>
        Target?.StopActiveTabAsync(name, cancellation_token) ?? MissingAsync();

    public Task<string> GetTabOutputAsync(string name, CancellationToken cancellation_token) =>
        Target?.GetTabOutputAsync(name, cancellation_token) ?? MissingAsync();

    public Task<string> GetTabStatusAsync(string name, CancellationToken cancellation_token) =>
        Target?.GetTabStatusAsync(name, cancellation_token) ?? MissingAsync();

    public Task<string> GetTabErrorsAsync(string name, CancellationToken cancellation_token) =>
        Target?.GetTabErrorsAsync(name, cancellation_token) ?? MissingAsync();

    IEditorBridge? Target => Volatile.Read(ref _target);

    static Task<string> MissingAsync() => Task.FromResult(Unavailable);
}
