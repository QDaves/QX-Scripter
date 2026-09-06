namespace Qx.Mcp;

public interface IEditorBridge
{
    string ListTabs() => AsyncOnly(nameof(ListTabsAsync));
    string GetActiveTab() => AsyncOnly(nameof(GetActiveTabAsync));
    string OpenTab(string name) => AsyncOnly(nameof(OpenTabAsync));
    string CreateTab(string name, string code) => AsyncOnly(nameof(CreateTabAsync));
    string EditActiveTab(string code) => AsyncOnly(nameof(EditActiveTabAsync));
    string SelectTab(string name) => AsyncOnly(nameof(SelectTabAsync));
    string CloseTab(string name) => AsyncOnly(nameof(CloseTabAsync));
    string RunActiveTab(string name) => AsyncOnly(nameof(RunActiveTabAsync));
    string StopActiveTab(string name) => AsyncOnly(nameof(StopActiveTabAsync));
    string GetTabOutput(string name) => AsyncOnly(nameof(GetTabOutputAsync));
    string GetTabStatus(string name) => AsyncOnly(nameof(GetTabStatusAsync));
    string GetTabErrors(string name) => AsyncOnly(nameof(GetTabErrorsAsync));

    Task<string> ListTabsAsync(CancellationToken cancellationToken) =>
        FromSynchronous(ListTabs, cancellationToken);

    Task<string> GetActiveTabAsync(CancellationToken cancellationToken) =>
        FromSynchronous(GetActiveTab, cancellationToken);

    Task<string> OpenTabAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => OpenTab(name), cancellationToken);

    Task<string> CreateTabAsync(string name, string code, CancellationToken cancellationToken) =>
        FromSynchronous(() => CreateTab(name, code), cancellationToken);

    Task<string> EditActiveTabAsync(string code, CancellationToken cancellationToken) =>
        FromSynchronous(() => EditActiveTab(code), cancellationToken);

    Task<string> SelectTabAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => SelectTab(name), cancellationToken);

    Task<string> CloseTabAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => CloseTab(name), cancellationToken);

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
