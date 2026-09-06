namespace Qx.Mcp;

public interface IMcpHost
{
    McpRuntimeCapability RuntimeCapabilities { get; }
    Task<string> RunCodeAsync(string code, CancellationToken cancellationToken);
    string SendToServer(string name, object[] values);
    string SendToClient(string name, object[] values);
    string GetConnection();
    string GetRoom();
    string GetAvatars();
    string GetFurni();
    string GetProfile();
    string GetFriends();
    string GetInventory();
    string GetBadgeInventory() => "badge inventory not available";
    string GetPetInventory() => "pet inventory not available";
    string GetAchievements() => "achievements not available";
    Task<string> GetConnectionAsync(
        bool detail,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetConnection, cancellationToken);
    Task<string> GetRoomAsync(
        bool detail,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetRoom, cancellationToken);
    Task<string> GetAvatarsAsync(
        bool detail,
        int limit,
        int offset,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetAvatars, cancellationToken);
    Task<string> GetControllersAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetControllers, cancellationToken);
    Task<string> GetFurniAsync(
        bool detail,
        int limit,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetFurni, cancellationToken);
    Task<string> GetFurniAsync(
        bool detail,
        int limit,
        int offset,
        CancellationToken cancellationToken) =>
        GetFurniAsync(detail, limit, cancellationToken);
    Task<string> GetProfileAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetProfile, cancellationToken);
    Task<string> GetFriendsAsync(
        bool fetch,
        bool detail,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetFriends, cancellationToken);
    Task<string> GetFriendsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetFriendsAsync(fetch, detail, limit, timeoutMs, cancellationToken);
    Task<string> GetInventoryAsync(
        bool fetch,
        bool detail,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetInventory, cancellationToken);
    Task<string> GetInventoryAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetInventoryAsync(fetch, detail, limit, offset, null, timeoutMs, cancellationToken);
    Task<string> GetInventoryAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) => offset == 0 && snapshotRevision is null
            ? GetInventoryAsync(fetch, detail, limit, timeoutMs, cancellationToken)
            : Task.FromException<string>(new NotSupportedException("Snapshot inventory paging is not supported by this host."));
    Task<string> GetBadgeInventoryAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetBadgeInventory, cancellationToken);
    Task<string> GetBadgeInventoryAsync(
        bool fetch,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetBadgeInventoryAsync(fetch, timeoutMs, cancellationToken);
    Task<string> GetBadgeInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetBadgeInventoryAsync(fetch, limit, timeoutMs, cancellationToken);
    Task<string> GetBadgeInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) => offset == 0 && snapshotRevision is null
            ? GetBadgeInventoryAsync(fetch, limit, offset, timeoutMs, cancellationToken)
            : Task.FromException<string>(new NotSupportedException("Snapshot badge inventory paging is not supported by this host."));
    Task<string> GetPetInventoryAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetPetInventory, cancellationToken);
    Task<string> GetPetInventoryAsync(
        bool fetch,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetPetInventoryAsync(fetch, timeoutMs, cancellationToken);
    Task<string> GetPetInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetPetInventoryAsync(fetch, limit, offset, null, timeoutMs, cancellationToken);
    Task<string> GetPetInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) => offset == 0 && snapshotRevision is null
            ? GetPetInventoryAsync(fetch, limit, timeoutMs, cancellationToken)
            : Task.FromException<string>(new NotSupportedException("Snapshot pet inventory paging is not supported by this host."));
    Task<string> GetAchievementsAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetAchievements, cancellationToken);
    Task<string> GetAchievementsAsync(
        bool fetch,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetAchievementsAsync(fetch, timeoutMs, cancellationToken);
    Task<string> GetAchievementsAsync(
        bool fetch,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetAchievementsAsync(fetch, limit, timeoutMs, cancellationToken);
    Task<string> GetAchievementsAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) => offset == 0 && snapshotRevision is null
            ? GetAchievementsAsync(fetch, limit, offset, timeoutMs, cancellationToken)
            : Task.FromException<string>(new NotSupportedException("Snapshot achievement paging is not supported by this host."));
    /// <summary>Reads the cached group-forum list, optionally requesting a page first.</summary>
    Task<string> GetForumsAsync(
        bool fetch,
        bool detail,
        string list,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(static () => "forums not available", cancellationToken);
    Task<string> GetForumsAsync(
        bool fetch,
        bool detail,
        string list,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) => offset == 0 && snapshotRevision is null
            ? GetForumsAsync(fetch, detail, list, limit, offset, timeoutMs, cancellationToken)
            : Task.FromException<string>(new NotSupportedException("Snapshot forum paging is not supported by this host."));

    /// <summary>Reads the cached threads of one group forum, optionally requesting a page first.</summary>
    Task<string> GetForumThreadsAsync(
        bool fetch,
        bool detail,
        long groupId,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(static () => "forum threads not available", cancellationToken);
    Task<string> GetForumThreadsAsync(
        bool fetch,
        bool detail,
        long groupId,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) => offset == 0 && snapshotRevision is null
            ? GetForumThreadsAsync(fetch, detail, groupId, limit, offset, timeoutMs, cancellationToken)
            : Task.FromException<string>(new NotSupportedException("Snapshot forum-thread paging is not supported by this host."));

    /// <summary>Reads the available, seasonal, active and daily quest state.</summary>
    Task<string> GetQuestsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(static () => "quests not available", cancellationToken);
    Task<string> GetQuestsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) => offset == 0 && snapshotRevision is null
            ? GetQuestsAsync(fetch, detail, limit, offset, timeoutMs, cancellationToken)
            : Task.FromException<string>(
                new NotSupportedException("Snapshot quest paging is not supported by this host."));

    /// <summary>Reads the cached crafting products, recipe and last result.</summary>
    Task<string> GetCraftingAsync(
        bool fetch,
        long furniId,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(static () => "crafting not available", cancellationToken);
    Task<string> GetCraftingAsync(
        bool fetch,
        long furniId,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) => offset == 0 && snapshotRevision is null
            ? GetCraftingAsync(fetch, furniId, limit, offset, timeoutMs, cancellationToken)
            : Task.FromException<string>(
                new NotSupportedException("Snapshot crafting paging is not supported by this host."));

    /// <summary>Reads the subscription products, kickback and Builders Club state.</summary>
    Task<string> GetSubscriptionsAsync(
        bool fetch,
        string product,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(static () => "subscriptions not available", cancellationToken);

    /// <summary>Reads the gift wrapping configuration, club gifts and giftability answers.</summary>
    Task<string> GetGiftsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(static () => "gifts not available", cancellationToken);
    Task<string> GetGiftsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) => offset == 0 && snapshotRevision is null
            ? GetGiftsAsync(fetch, detail, limit, offset, timeoutMs, cancellationToken)
            : Task.FromException<string>(
                new NotSupportedException("Snapshot gift paging is not supported by this host."));

    /// <summary>Reads the central Flash and Unity message registry with active catalog provenance and header evidence.</summary>
    Task<string> GetProtocolMessagesAsync(
        string query,
        string direction,
        string client,
        bool explicitOnly,
        bool resolvedOnly,
        int limit,
        int offset,
        CancellationToken cancellationToken) =>
        FromSynchronous(static () => "protocol registry not available", cancellationToken);

    IReadOnlyList<string> ListScripts();
    string GetScript(string name);
    string SaveScript(string name, string code);

    string GetRoomData();
    string GetAvatar(string name);
    string Say(string message);
    string Shout(string message);
    string Walk(int x, int y);
    string Wave();
    string Dance(int style);
    string Sign(int sign);

    Task<string> GetUserProfileAsync(long userId, CancellationToken cancellationToken);
    Task<string> GetGroupAsync(long groupId, CancellationToken cancellationToken);
    Task<string> GetBadgesAsync(long userId, CancellationToken cancellationToken);
    Task<string> GetRelationshipAsync(long userId, CancellationToken cancellationToken);
    Task<string> SearchUserAsync(string name, CancellationToken cancellationToken);
    Task<string> GetStickyAsync(long itemId, CancellationToken cancellationToken);
    Task<string> GetPetInfoAsync(long petId, CancellationToken cancellationToken);
    Task<string> GetRoomSettingsAsync(long roomId, CancellationToken cancellationToken);
    Task<string> RunScriptAsync(string name, CancellationToken cancellationToken);

    string Kick(long userId);
    string Mute(long userId, int minutes);
    string Ban(long userId);
    string GiveRights(long userId);
    string RemoveRights(long userId);
    string LetIn(string name);
    string RespectPet(long petId);
    string GetControllers();
    string GetCurrencies();
    string GetHeightmap();
    Task<string> GetCurrenciesAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetCurrencies, cancellationToken);
    Task<string> GetHeightmapAsync(
        bool detail,
        int limit,
        CancellationToken cancellationToken) =>
        FromSynchronous(GetHeightmap, cancellationToken);
    Task<string> GetHeightmapAsync(
        bool detail,
        int limit,
        int offset,
        CancellationToken cancellationToken) =>
        GetHeightmapAsync(detail, limit, cancellationToken);

    string DeleteScript(string name);
    string RenameScript(string name, string newName);
    IReadOnlyList<string> SearchScripts(string query);

    string ListApi(string filter);
    string ListLibraries();
    string SearchTypes(string query, string assembly, int limit);
    string SearchTypes(string query, string assembly, int limit, int offset) =>
        SearchTypes(query, assembly, limit);
    string GetTypeInfo(string name);
    string SearchMembers(string query, string kind, int limit);
    string SearchMembers(string query, string kind, int limit, int offset) =>
        SearchMembers(query, kind, limit);
    string GetScriptingGuide();
    string CompileCheck(string code);

    string ListTabs() => AsyncOnly(nameof(ListTabsAsync));
    string GetActiveTab() => AsyncOnly(nameof(GetActiveTabAsync));
    string OpenTab(string name) => AsyncOnly(nameof(OpenTabAsync));
    string CreateTab(string name, string code) => AsyncOnly(nameof(CreateTabAsync));
    string EditActiveTab(string code) => AsyncOnly(nameof(EditActiveTabAsync));
    string SelectTab(string name) => AsyncOnly(nameof(SelectTabAsync));
    string CloseTabByName(string name) => AsyncOnly(nameof(CloseTabByNameAsync));
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

    Task<string> CloseTabByNameAsync(string name, CancellationToken cancellationToken) =>
        FromSynchronous(() => CloseTabByName(name), cancellationToken);

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
