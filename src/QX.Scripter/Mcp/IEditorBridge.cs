using Qx.Mcp;

namespace Qx.Scripter;

public interface IEditorBridge
{
    string ListTabs() => AsyncOnly(nameof(ListTabsAsync));
    string OpenTab(string name) => AsyncOnly(nameof(OpenTabAsync));
    string CreateTab(string name, string code) => AsyncOnly(nameof(CreateTabAsync));
    string EditActiveTab(string code) => AsyncOnly(nameof(EditActiveTabAsync));
    string SelectTab(string name) => AsyncOnly(nameof(SelectTabAsync));
    string CloseTab(string name, bool discard) => AsyncOnly(nameof(CloseTabAsync));
    string RunActiveTab(string name) => AsyncOnly(nameof(RunActiveTabAsync));
    string StopActiveTab(string name) => AsyncOnly(nameof(StopActiveTabAsync));
    string GetTabOutput(string name) => AsyncOnly(nameof(GetTabOutputAsync));
    string GetTabStatus(string name) => AsyncOnly(nameof(GetTabStatusAsync));
    string GetTabErrors(string name) => AsyncOnly(nameof(GetTabErrorsAsync));

    Task<string> ListTabsAsync(CancellationToken cancellationToken) =>
        FromSynchronous(ListTabs, cancellationToken);

    Task<string> OpenTabAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => OpenTab(name), cancellationToken);

    Task<string> CreateTabAsync(string name, string code, CancellationToken cancellationToken) =>
        FromSynchronous(() => CreateTab(name, code), cancellationToken);

    Task<string> EditActiveTabAsync(string code, CancellationToken cancellationToken) =>
        FromSynchronous(() => EditActiveTab(code), cancellationToken);

    Task<string> SelectTabAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => SelectTab(name), cancellationToken);

    Task<string> CloseTabAsync(string name, bool discard, CancellationToken cancellationToken) =>
        FromSynchronous(() => CloseTab(name, discard), cancellationToken);

    Task<string> RunActiveTabAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => RunActiveTab(name), cancellationToken);

    Task<string> StopActiveTabAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => StopActiveTab(name), cancellationToken);

    Task<string> GetTabOutputAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => GetTabOutput(name), cancellationToken);

    Task<string> GetTabStatusAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => GetTabStatus(name), cancellationToken);

    Task<string> GetTabErrorsAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => GetTabErrors(name), cancellationToken);

    /// <summary>
    /// The live text of the open tab holding a saved script, or of the active tab when the name is
    /// empty, so a reader sees what the editor shows rather than a stale file. Returns
    /// <see langword="null"/> when there is no such tab.
    /// </summary>
    Task<string?> ReadOpenScriptAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// Applies an edit to the open tab holding a saved script, or to the active tab when the name
    /// is empty, in one step on the editor's thread. A tab without unsaved changes is saved after
    /// the edit so tab and file stay the same. Returns <see langword="null"/> when there is no such
    /// tab, leaving the file to the caller.
    /// </summary>
    Task<string?> EditOpenScriptAsync(string name, Func<string, string> edit, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// Renames a saved script the way the editor does, moving its tab, library entry and panel
    /// memory with it. Returns <see langword="null"/> when the editor does not handle files.
    /// </summary>
    Task<string?> RenameScriptAsync(string name, string newName, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// Deletes a saved script the way the editor does. Returns <see langword="null"/> when the
    /// editor does not handle files.
    /// </summary>
    Task<string?> DeleteScriptAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    private static Task<string> FromSynchronous(
        Func<string> operation,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<string>(cancellationToken);

        try
        {
            return Task.FromResult(operation());
        }
        catch (Exception error)
        {
            return Task.FromException<string>(error);
        }
    }

    private static string AsyncOnly(string asyncMember) =>
        throw new NotSupportedException(
            $"The synchronous editor API is not implemented. Use {asyncMember} instead.");
}
