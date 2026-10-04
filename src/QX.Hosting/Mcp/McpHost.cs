using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Interception.GEarth;
using Qx.Mcp;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;
using Qx.Scripting;
using Qx.Scripting.Hosting;

namespace Qx.Hosting;

internal sealed class McpHost(
    GEarthExtension extension,
    GameState game,
    GameQueryService query_service,
    IApplicationRuntime application,
    MessageContractCatalog contracts,
    ScriptExecutionService execution_service,
    string scripts_directory,
    IEditorBridge? editor = null) : IMcpHost
{
    private const int GiftApplicationPageLimit = 500;
    private const int GiftMaximumOffers = 4096;
    private const int GiftMaximumCollectionCount = ushort.MaxValue;
    private const int GiftDetailEntryLimit = 500;
    private const int CraftingAncillaryEntryLimit = 500;

    private static readonly ApiTypeCatalog ApiTypes = new();
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonSerializerOptions ProtocolJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };
    private readonly string scripts_root = Path.GetFullPath(scripts_directory);
    private readonly ScriptExecutionService script_execution = execution_service;
    private readonly BackgroundRuns background_runs = new(execution_service);
    private readonly GameQueryService queries = query_service;
    private readonly IApplicationRuntime application_runtime = application;
    private readonly Lazy<IReadOnlyDictionary<MessageKey, MessageRegistryMembers>> message_members =
        new(() => MessageMembers.Declared(contracts));

    public McpRuntimeCapability RuntimeCapabilities =>
        editor is null ? McpRuntimeCapability.None : McpRuntimeCapability.Editor;

    public string ListTabs() => Editor(editor, static value => value.ListTabs());
    public string OpenTab(string name) => Editor(editor, value => value.OpenTab(name));
    public string CreateTab(string name, string code) =>
        Editor(editor, value => value.CreateTab(name, code));

    public string EditActiveTab(string code) => Editor(editor, value => value.EditActiveTab(code));
    public string SelectTab(string name) => Editor(editor, value => value.SelectTab(name));
    public string CloseTabByName(string name, bool discard) => Editor(editor, value => value.CloseTab(name, discard));
    public string RunActiveTab(string name) => Editor(editor, value => value.RunActiveTab(name));
    public string StopActiveTab(string name) => Editor(editor, value => value.StopActiveTab(name));
    public string GetTabOutput(string name) => Editor(editor, value => value.GetTabOutput(name));
    public string GetTabStatus(string name) => Editor(editor, value => value.GetTabStatus(name));
    public string GetTabErrors(string name) => Editor(editor, value => value.GetTabErrors(name));

    public Task<string> ListTabsAsync(CancellationToken cancellationToken) =>
        editor?.ListTabsAsync(cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string> OpenTabAsync(string name, CancellationToken cancellationToken) =>
        editor?.OpenTabAsync(name, cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string> CreateTabAsync(string name, string code, CancellationToken cancellationToken) =>
        editor?.CreateTabAsync(name, code, cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string> EditActiveTabAsync(string code, CancellationToken cancellationToken) =>
        editor?.EditActiveTabAsync(code, cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string> SelectTabAsync(string name, CancellationToken cancellationToken) =>
        editor?.SelectTabAsync(name, cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string> CloseTabByNameAsync(string name, bool discard, CancellationToken cancellationToken) =>
        editor?.CloseTabAsync(name, discard, cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string?> ReadOpenScriptAsync(string name, CancellationToken cancellationToken) =>
        editor?.ReadOpenScriptAsync(name, cancellationToken) ?? Task.FromResult<string?>(null);

    public Task<string?> EditOpenScriptAsync(string name, Func<string, string> edit, CancellationToken cancellationToken) =>
        editor?.EditOpenScriptAsync(name, edit, cancellationToken) ?? Task.FromResult<string?>(null);

    public Task<string> RunActiveTabAsync(string name, CancellationToken cancellationToken) =>
        editor?.RunActiveTabAsync(name, cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string> StopActiveTabAsync(string name, CancellationToken cancellationToken) =>
        editor?.StopActiveTabAsync(name, cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string> GetTabOutputAsync(string name, CancellationToken cancellationToken) =>
        editor?.GetTabOutputAsync(name, cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string> GetTabStatusAsync(string name, CancellationToken cancellationToken) =>
        editor?.GetTabStatusAsync(name, cancellationToken) ?? Task.FromResult("editor UI not available");

    public Task<string> GetTabErrorsAsync(string name, CancellationToken cancellationToken) =>
        editor?.GetTabErrorsAsync(name, cancellationToken) ?? Task.FromResult("editor UI not available");

    public async Task<string> RunCodeAsync(string code, CancellationToken cancellationToken)
    {
        ScriptExecutionResult result = await RunSourceAsync(
            code,
            "mcp:run_code",
            "mcp.csx",
            cancellationToken).ConfigureAwait(false);
        return SerializeExecution(result);
    }

    public string SendToServer(string name, object[] values)
    {
        Globals().SendToServer(name, values);
        return $"sent '{name}' to server";
    }

    public string SendToClient(string name, object[] values)
    {
        Globals().SendToClient(name, values);
        return $"sent '{name}' to client";
    }

    public string GetConnection() => QueryJson.Serialize(queries.Connection());

    public string GetRoom() => QueryJson.Serialize(queries.Room());

    public string GetAvatars() => QueryJson.Serialize(queries.Avatars());

    public string GetFurni() => QueryJson.Serialize(queries.Furni());

    public string GetProfile() => QueryJson.Serialize(queries.Profile());

    public string GetFriends() => QueryJson.Serialize(FriendEnvelope(
        application_runtime.Invoke<FriendsListRequest, FriendListPage>(
            ApplicationMemberIds.FriendsList,
            new FriendsListRequest(Limit: 500))));

    public string GetInventory()
    {
        InventoryFurniPage page = application_runtime.Invoke<InventoryFurniPageRequest, InventoryFurniPage>(
            ApplicationMemberIds.InventoryFurniList,
            new InventoryFurniPageRequest(Limit: 500));
        ValidateFurniPage(page, 0, 500, null);
        return WithPageLease(
            QueryJson.Serialize(InventoryEnvelope(page, 500)),
            page.SnapshotRevision,
            page.NextOffset);
    }

    public string GetBadgeInventory()
    {
        BadgeSnapshotRead read = ReadBadgeSnapshot(
            (request, token) => application_runtime.Invoke<
                OwnedBadgePageRequest,
                OwnedBadgePage>(
                    ApplicationMemberIds.BadgesOwnedList,
                    request,
                    token),
            null,
            null,
            default);
        QueryEnvelope<BadgeInventorySnapshot> envelope = BadgeInventoryEnvelope(
            read.Page,
            read.Badges,
            0,
            500);
        return QueryJson.Serialize(envelope);
    }

    public string GetPetInventory()
    {
        InventoryPetPage page = application_runtime.Invoke<InventoryPetPageRequest, InventoryPetPage>(
            ApplicationMemberIds.InventoryPetsList,
            new InventoryPetPageRequest(Limit: 200));
        ValidatePetPage(page, 0, 200, null);
        return WithPageLease(
            QueryJson.Serialize(PetInventoryEnvelope(page, 200)),
            page.SnapshotRevision,
            page.NextOffset);
    }

    public string GetAchievements()
    {
        AchievementSnapshotRead read = ReadAchievementSnapshot(
            (request, token) => application_runtime.Invoke<
                AchievementPageRequest,
                AchievementPage>(
                    ApplicationMemberIds.AchievementsList,
                    request,
                    token),
            null,
            null,
            default);
        QueryEnvelope<AchievementCollectionSnapshot> envelope = AchievementEnvelope(
            read.Page,
            read.Achievements,
            0,
            500);
        return QueryJson.Serialize(envelope);
    }

    public Task<string> GetConnectionAsync(bool detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QueryEnvelope<ConnectionSnapshot> snapshot = queries.Connection();
        return Task.FromResult(detail
            ? QueryJson.Serialize(snapshot)
            : QueryJson.Serialize(CompactConnection(snapshot)));
    }

    public Task<string> GetProtocolMessagesAsync(
        string query,
        string direction,
        bool explicitOnly,
        bool resolvedOnly,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MessageRegistrySnapshot snapshot = MessageRegistryQuery.Read(
            extension.Messages,
            query,
            direction,
            explicitOnly,
            resolvedOnly,
            limit,
            offset,
            message_members.Value);
        return Task.FromResult(JsonSerializer.Serialize(snapshot, ProtocolJsonOptions));
    }

    public Task<string> GetRoomAsync(bool detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QueryEnvelope<RoomSnapshot> snapshot = queries.Room();
        return Task.FromResult(detail
            ? QueryJson.Serialize(snapshot)
            : QueryJson.Serialize(CompactRoom(snapshot)));
    }

    public Task<string> GetAvatarsAsync(
        bool detail,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int avatar_limit = NormalizeLimit(limit, 500);
        int start = NormalizeOffset(offset);
        QueryEnvelope<AvatarCollectionSnapshot> source = queries.Avatars();
        AvatarCollectionSnapshot? data = source.Data;
        IReadOnlyList<AvatarSnapshot> avatars = data?.Avatars ?? [];
        AvatarSnapshot[] page = [.. avatars.Skip(start).Take(avatar_limit)];
        bool truncated = start + page.Length < avatars.Count;
        IReadOnlyDictionary<string, int> counts = AvatarCounts(avatars);
        string json = detail
            ? QueryJson.Serialize(Reshape(
                source,
                data is null
                    ? null
                    : new McpAvatarDetailsSnapshot(
                        data.RoomId,
                        data.Generation,
                        avatars.Count,
                        page.Length,
                        start,
                        avatar_limit,
                        truncated,
                        counts,
                        page),
                truncated))
            : QueryJson.Serialize(Reshape(
                source,
                data is null
                    ? null
                    : new McpAvatarCollectionSnapshot(
                        data.RoomId,
                        data.Generation,
                        avatars.Count,
                        page.Length,
                        start,
                        avatar_limit,
                        truncated,
                        counts,
                        [.. page.Select(CompactAvatar)]),
                truncated));
        return Task.FromResult(WithNextOffset(json, truncated ? start + avatar_limit : null));
    }

    public Task<string> GetControllersAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int controller_limit = NormalizeLimit(limit, 500);
        int start = NormalizeOffset(offset);
        QueryEnvelope<ControllerCollectionSnapshot> source = queries.Controllers();
        ControllerCollectionSnapshot? data = source.Data;
        IReadOnlyList<ControllerSnapshot> controllers = data?.Controllers ?? [];
        ControllerSnapshot[] page = [.. controllers.Skip(start).Take(controller_limit)];
        bool truncated = start + page.Length < controllers.Count;
        string json = QueryJson.Serialize(Reshape(
            source,
            data is null
                ? null
                : new McpControllerCollectionSnapshot(
                    data.RoomId,
                    data.Generation,
                    data.IsOwner,
                    controllers.Count,
                    page.Length,
                    start,
                    controller_limit,
                    truncated,
                    page),
            truncated));
        return Task.FromResult(WithNextOffset(json, truncated ? start + controller_limit : null));
    }

    public Task<string> GetFurniAsync(
        bool detail,
        int limit,
        CancellationToken cancellationToken) =>
        GetFurniAsync(detail, limit, 0, cancellationToken);

    public Task<string> GetFurniAsync(
        bool detail,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int item_limit = NormalizeLimit(limit, 200);
        int start = NormalizeOffset(offset);
        QueryEnvelope<FurniCollectionSnapshot> snapshot = queries.Furni();
        QueryEnvelope<FurniCollectionSnapshot> window = SkipFurni(snapshot, start);
        string json = detail
            ? QueryJson.Serialize(McpReadProjection.FurniDetails(window, item_limit))
            : QueryJson.Serialize(McpReadProjection.Furni(window, item_limit));
        int available = snapshot.Data is { } data
            ? Math.Max(data.FloorItems.Count, data.WallItems.Count)
            : 0;
        return Task.FromResult(WithNextOffset(
            json,
            start + item_limit < available ? start + item_limit : null));
    }

    public Task<string> GetProfileAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        ReadAsync(
            "profile",
            fetch,
            timeoutMs,
            cancellationToken,
            async (timeout, token) =>
            {
                await application_runtime.InvokeAsync<ProfileRefreshRequest, ProfileStateView>(
                    ApplicationMemberIds.ProfileRefresh,
                    new ProfileRefreshRequest(timeout),
                    token).ConfigureAwait(false);
            },
            queries.Profile,
            static snapshot => snapshot);

    public Task<string> GetFriendsAsync(
        bool fetch,
        bool detail,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetFriendsAsync(fetch, detail, limit, 0, timeoutMs, cancellationToken);

    public Task<string> GetFriendsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        int friend_limit = NormalizeLimit(limit, 500);
        int start = NormalizeOffset(offset);
        FriendListPage? refreshed_page = null;
        async Task Refresh(int timeout, CancellationToken token)
        {
            refreshed_page = await application_runtime.InvokeAsync<FriendsRefreshRequest, FriendListPage>(
                ApplicationMemberIds.FriendsRefresh,
                new FriendsRefreshRequest(Offset: start, Limit: friend_limit, TimeoutMilliseconds: timeout),
                token);
        }
        QueryEnvelope<FriendCollectionSnapshot> Capture() => FriendEnvelope(
            refreshed_page ?? application_runtime.Invoke<FriendsListRequest, FriendListPage>(
                ApplicationMemberIds.FriendsList,
                new FriendsListRequest(Offset: start, Limit: friend_limit)));

        return detail
            ? ReadPagedAsync<FriendCollectionSnapshot, McpFriendDetailsSnapshot>(
                "friends",
                fetch,
                timeoutMs,
                cancellationToken,
                Refresh,
                Capture,
                snapshot => McpReadProjection.FriendDetails(snapshot, friend_limit),
                snapshot => FriendNextOffset(snapshot, start))
            : ReadPagedAsync<FriendCollectionSnapshot, McpFriendCollectionSnapshot>(
                "friends",
                fetch,
                timeoutMs,
                cancellationToken,
                Refresh,
                Capture,
                snapshot => McpReadProjection.Friends(snapshot, friend_limit),
                snapshot => FriendNextOffset(snapshot, start));
    }

    public Task<string> GetInventoryAsync(
        bool fetch,
        bool detail,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetInventoryAsync(fetch, detail, limit, 0, null, timeoutMs, cancellationToken);

    public Task<string> GetInventoryAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetInventoryAsync(fetch, detail, limit, offset, null, timeoutMs, cancellationToken);

    public Task<string> GetInventoryAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        int item_limit = NormalizeLimit(limit, 500);
        int start = NormalizeOffset(offset);
        long? snapshot_revision = NormalizeSnapshotRevision(snapshotRevision, start);
        InventoryFurniPage? page = null;
        async Task Refresh(int timeout, CancellationToken token)
        {
            page = await application_runtime.InvokeAsync<InventoryFurniPageRequest, InventoryFurniPage>(
                ApplicationMemberIds.InventoryFurniList,
                new InventoryFurniPageRequest(Limit: item_limit),
                token).ConfigureAwait(false);
            ValidateFurniPage(page, 0, item_limit, null);
            if (page.Loaded && !page.Stale && !page.RecoveryPending)
                return;
            page = null;
            page = await application_runtime.InvokeAsync<InventoryFurniRefreshRequest, InventoryFurniPage>(
                ApplicationMemberIds.InventoryFurniRefresh,
                new InventoryFurniRefreshRequest(
                    Limit: item_limit,
                    TimeoutMilliseconds: timeout),
                token).ConfigureAwait(false);
            ValidateFurniPage(page, 0, item_limit, null);
        }
        QueryEnvelope<InventorySnapshot> Capture()
        {
            page ??= application_runtime.Invoke<InventoryFurniPageRequest, InventoryFurniPage>(
                ApplicationMemberIds.InventoryFurniList,
                new InventoryFurniPageRequest(
                    Offset: start,
                    Limit: item_limit,
                    SnapshotRevision: snapshot_revision));
            ValidateFurniPage(page, start, item_limit, snapshot_revision);
            return InventoryEnvelope(page, item_limit);
        }

        return detail
            ? ReadPagedAsync<InventorySnapshot, InventorySnapshot>(
                "inventory",
                fetch && snapshot_revision is null,
                timeoutMs,
                cancellationToken,
                Refresh,
                Capture,
                snapshot => McpReadProjection.InventoryDetails(snapshot, item_limit),
                _ => page?.NextOffset,
                _ => page?.SnapshotRevision)
            : ReadPagedAsync<InventorySnapshot, McpInventorySnapshot>(
                "inventory",
                fetch && snapshot_revision is null,
                timeoutMs,
                cancellationToken,
                Refresh,
                Capture,
                snapshot => McpReadProjection.Inventory(snapshot, item_limit),
                _ => page?.NextOffset,
                _ => page?.SnapshotRevision);
    }

    public Task<string> GetBadgeInventoryAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetLegacyBadgeInventoryAsync(fetch, 500, 0, timeoutMs, cancellationToken);

    public Task<string> GetBadgeInventoryAsync(
        bool fetch,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetLegacyBadgeInventoryAsync(fetch, limit, 0, timeoutMs, cancellationToken);

    public Task<string> GetBadgeInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetLegacyBadgeInventoryAsync(
            fetch,
            limit,
            offset,
            timeoutMs,
            cancellationToken);

    public Task<string> GetBadgeInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetBadgeInventoryAsync(
            fetch,
            limit,
            offset,
            snapshotRevision,
            timeoutMs,
            cancellationToken,
            extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            (request, token) => application_runtime.InvokeAsync<
                BadgeRefreshRequest,
                BadgeRefreshResult>(
                    ApplicationMemberIds.BadgesRefresh,
                    request,
                    token),
            (request, token) => application_runtime.Invoke<
                OwnedBadgePageRequest,
                OwnedBadgePage>(
                    ApplicationMemberIds.BadgesOwnedList,
                    request,
                    token));

    public Task<string> GetPetInventoryAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetPetInventoryAsync(fetch, 200, 0, null, timeoutMs, cancellationToken);

    public Task<string> GetPetInventoryAsync(
        bool fetch,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetPetInventoryAsync(fetch, limit, 0, null, timeoutMs, cancellationToken);

    public Task<string> GetPetInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetPetInventoryAsync(fetch, limit, offset, null, timeoutMs, cancellationToken);

    public Task<string> GetPetInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        int item_limit = NormalizeLimit(limit, 200);
        int start = NormalizeOffset(offset);
        long? snapshot_revision = NormalizeSnapshotRevision(snapshotRevision, start);
        InventoryPetPage? page = null;
        async Task Refresh(int timeout, CancellationToken token)
        {
            page = await application_runtime.InvokeAsync<InventoryPetPageRequest, InventoryPetPage>(
                ApplicationMemberIds.InventoryPetsList,
                new InventoryPetPageRequest(Limit: item_limit),
                token).ConfigureAwait(false);
            ValidatePetPage(page, 0, item_limit, null);
            if (page.Loaded && !page.Stale && !page.RecoveryPending)
                return;
            page = null;
            page = await application_runtime.InvokeAsync<InventoryPetRefreshRequest, InventoryPetPage>(
                ApplicationMemberIds.InventoryPetsRefresh,
                new InventoryPetRefreshRequest(
                    Limit: item_limit,
                    TimeoutMilliseconds: timeout),
                token).ConfigureAwait(false);
            ValidatePetPage(page, 0, item_limit, null);
        }
        QueryEnvelope<PetInventorySnapshot> Capture()
        {
            page ??= application_runtime.Invoke<InventoryPetPageRequest, InventoryPetPage>(
                ApplicationMemberIds.InventoryPetsList,
                new InventoryPetPageRequest(
                    Offset: start,
                    Limit: item_limit,
                    SnapshotRevision: snapshot_revision));
            ValidatePetPage(page, start, item_limit, snapshot_revision);
            return PetInventoryEnvelope(page, item_limit);
        }

        return ReadPagedAsync<PetInventorySnapshot, PetInventorySnapshot>(
            "pet_inventory",
            fetch && snapshot_revision is null,
            timeoutMs,
            cancellationToken,
            Refresh,
            Capture,
            static snapshot => snapshot,
            _ => page?.NextOffset,
            _ => page?.SnapshotRevision);
    }

    public Task<string> GetAchievementsAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetLegacyAchievementsAsync(fetch, 500, 0, timeoutMs, cancellationToken);

    public Task<string> GetAchievementsAsync(
        bool fetch,
        int limit,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetLegacyAchievementsAsync(fetch, limit, 0, timeoutMs, cancellationToken);

    public Task<string> GetAchievementsAsync(
        bool fetch,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetLegacyAchievementsAsync(
            fetch,
            limit,
            offset,
            timeoutMs,
            cancellationToken);

    public Task<string> GetAchievementsAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetAchievementsAsync(
            fetch,
            limit,
            offset,
            snapshotRevision,
            timeoutMs,
            cancellationToken,
            extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            (request, token) => application_runtime.InvokeAsync<
                AchievementRefreshRequest,
                AchievementRefreshResult>(
                    ApplicationMemberIds.AchievementsRefresh,
                    request,
                    token),
            (request, token) => application_runtime.Invoke<
                AchievementPageRequest,
                AchievementPage>(
                    ApplicationMemberIds.AchievementsList,
                    request,
                    token));

    private Task<string> GetLegacyBadgeInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        int timeout_ms,
        CancellationToken cancellation_token) =>
        ReadBadgeInventoryAsync(
            fetch,
            limit,
            offset,
            null,
            timeout_ms,
            cancellation_token,
            extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            (request, token) => application_runtime.InvokeAsync<
                BadgeRefreshRequest,
                BadgeRefreshResult>(
                    ApplicationMemberIds.BadgesRefresh,
                    request,
                    token),
            (request, token) => application_runtime.Invoke<
                OwnedBadgePageRequest,
                OwnedBadgePage>(
                    ApplicationMemberIds.BadgesOwnedList,
                    request,
                    token),
            false);

    private static Task<string> GetBadgeInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool connected,
        Func<CancellationToken, Task> await_readiness,
        Func<BadgeRefreshRequest, CancellationToken, ValueTask<BadgeRefreshResult>> refresh,
        Func<OwnedBadgePageRequest, CancellationToken, OwnedBadgePage> read_page) =>
        ReadBadgeInventoryAsync(
            fetch,
            limit,
            offset,
            snapshot_revision,
            timeout_ms,
            cancellation_token,
            connected,
            await_readiness,
            refresh,
            read_page,
            true);

    private static async Task<string> ReadBadgeInventoryAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool connected,
        Func<CancellationToken, Task> await_readiness,
        Func<BadgeRefreshRequest, CancellationToken, ValueTask<BadgeRefreshResult>> refresh,
        Func<OwnedBadgePageRequest, CancellationToken, OwnedBadgePage> read_page,
        bool include_snapshot_revision)
    {
        ArgumentNullException.ThrowIfNull(await_readiness);
        ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(read_page);
        int item_limit = NormalizeLimit(limit, 500);
        int start = NormalizeOffset(offset);
        long? requested_revision = include_snapshot_revision
            ? NormalizeSnapshotRevision(snapshot_revision, start)
            : null;
        OwnedBadgePage? first_page = null;
        int? next_offset = null;
        long? result_revision = null;

        async Task Load(int timeout, CancellationToken token)
        {
            first_page = read_page(new OwnedBadgePageRequest(Limit: 500), token);
            ValidateBadgePage(first_page, 0, 500, null, null);
            if (first_page.Inventory.Loaded &&
                !first_page.Inventory.Loading &&
                !first_page.Inventory.Stale &&
                !first_page.Inventory.RecoveryPending)
            {
                return;
            }

            BadgeRefreshResult refreshed = await refresh(
                new BadgeRefreshRequest(Limit: 500, TimeoutMilliseconds: timeout),
                token).ConfigureAwait(false);
            ValidateBadgeRefresh(refreshed);
            first_page = refreshed.FirstPage;
        }

        QueryEnvelope<BadgeInventorySnapshot> Capture()
        {
            BadgeSnapshotRead read = ReadBadgeSnapshot(
                read_page,
                first_page,
                requested_revision,
                cancellation_token);
            QueryEnvelope<BadgeInventorySnapshot> envelope = BadgeInventoryEnvelope(
                read.Page,
                read.Badges,
                start,
                item_limit);
            next_offset = BadgeNextOffset(read.Page.Total, start, envelope.Data!.Returned);
            result_revision = read.Page.SnapshotRevision;
            return envelope;
        }

        string envelope = await McpReadPipeline.ReadAsync<
            BadgeInventorySnapshot,
            BadgeInventorySnapshot>(
                "badge_inventory",
                fetch && requested_revision is null,
                timeout_ms,
                cancellation_token,
                connected,
                await_readiness,
                Load,
                Capture,
                static source => source).ConfigureAwait(false);
        return include_snapshot_revision && result_revision is long revision
            ? WithPageLease(envelope, revision, next_offset)
            : WithNextOffset(envelope, next_offset);
    }

    private Task<string> GetLegacyAchievementsAsync(
        bool fetch,
        int limit,
        int offset,
        int timeout_ms,
        CancellationToken cancellation_token) =>
        ReadAchievementsAsync(
            fetch,
            limit,
            offset,
            null,
            timeout_ms,
            cancellation_token,
            extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            (request, token) => application_runtime.InvokeAsync<
                AchievementRefreshRequest,
                AchievementRefreshResult>(
                    ApplicationMemberIds.AchievementsRefresh,
                    request,
                    token),
            (request, token) => application_runtime.Invoke<
                AchievementPageRequest,
                AchievementPage>(
                    ApplicationMemberIds.AchievementsList,
                    request,
                    token),
            false);

    private static Task<string> GetAchievementsAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool connected,
        Func<CancellationToken, Task> await_readiness,
        Func<
            AchievementRefreshRequest,
            CancellationToken,
            ValueTask<AchievementRefreshResult>> refresh,
        Func<AchievementPageRequest, CancellationToken, AchievementPage> read_page) =>
        ReadAchievementsAsync(
            fetch,
            limit,
            offset,
            snapshot_revision,
            timeout_ms,
            cancellation_token,
            connected,
            await_readiness,
            refresh,
            read_page,
            true);

    private static async Task<string> ReadAchievementsAsync(
        bool fetch,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool connected,
        Func<CancellationToken, Task> await_readiness,
        Func<
            AchievementRefreshRequest,
            CancellationToken,
            ValueTask<AchievementRefreshResult>> refresh,
        Func<AchievementPageRequest, CancellationToken, AchievementPage> read_page,
        bool include_snapshot_revision)
    {
        ArgumentNullException.ThrowIfNull(await_readiness);
        ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(read_page);
        int item_limit = NormalizeLimit(limit, 500);
        int start = NormalizeOffset(offset);
        long? requested_revision = include_snapshot_revision
            ? NormalizeSnapshotRevision(snapshot_revision, start)
            : null;
        AchievementPage? first_page = null;
        int? next_offset = null;
        long? result_revision = null;

        async Task Load(int timeout, CancellationToken token)
        {
            first_page = read_page(new AchievementPageRequest(Limit: 500), token);
            ValidateAchievementPage(first_page, 0, 500, null, null);
            if (first_page.Loaded)
                return;

            AchievementRefreshResult refreshed = await refresh(
                new AchievementRefreshRequest(Limit: 500, TimeoutMilliseconds: timeout),
                token).ConfigureAwait(false);
            ValidateAchievementRefresh(refreshed);
            first_page = refreshed.FirstPage;
        }

        QueryEnvelope<AchievementCollectionSnapshot> Capture()
        {
            AchievementSnapshotRead read = ReadAchievementSnapshot(
                read_page,
                first_page,
                requested_revision,
                cancellation_token);
            QueryEnvelope<AchievementCollectionSnapshot> envelope = AchievementEnvelope(
                read.Page,
                read.Achievements,
                start,
                item_limit);
            next_offset = AchievementNextOffset(
                read.Page.Total,
                start,
                envelope.Data!.Returned);
            result_revision = read.Page.SnapshotRevision;
            return envelope;
        }

        string envelope = await McpReadPipeline.ReadAsync<
            AchievementCollectionSnapshot,
            AchievementCollectionSnapshot>(
                "achievements",
                fetch && requested_revision is null,
                timeout_ms,
                cancellation_token,
                connected,
                await_readiness,
                Load,
                Capture,
                static source => source).ConfigureAwait(false);
        return include_snapshot_revision && result_revision is long revision
            ? WithPageLease(envelope, revision, next_offset)
            : WithNextOffset(envelope, next_offset);
    }

    public Task<string> GetForumsAsync(
        bool fetch,
        bool detail,
        string list,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        ReadForumsAsync(
            fetch,
            detail,
            list,
            limit,
            offset,
            null,
            timeoutMs,
            cancellationToken,
            false);

    public Task<string> GetForumsAsync(
        bool fetch,
        bool detail,
        string list,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        ReadForumsAsync(
            fetch,
            detail,
            list,
            limit,
            offset,
            snapshotRevision,
            timeoutMs,
            cancellationToken,
            true);

    public Task<string> GetForumThreadsAsync(
        bool fetch,
        bool detail,
        long groupId,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        ReadForumThreadsAsync(
            fetch,
            detail,
            groupId,
            limit,
            offset,
            null,
            timeoutMs,
            cancellationToken,
            false);

    public Task<string> GetForumThreadsAsync(
        bool fetch,
        bool detail,
        long groupId,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        ReadForumThreadsAsync(
            fetch,
            detail,
            groupId,
            limit,
            offset,
            snapshotRevision,
            timeoutMs,
            cancellationToken,
            true);

    private async Task<string> ReadForumsAsync(
        bool fetch,
        bool detail,
        string list,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool include_snapshot_revision)
    {
        int forum_limit = NormalizeLimit(limit, 200);
        int start = NormalizeOffset(offset);
        ForumListCode list_code = ForumList(list);
        long? requested_revision = include_snapshot_revision
            ? NormalizeSnapshotRevision(snapshot_revision, start)
            : null;
        long? expected_generation = null;
        long? result_revision = null;
        int? next_offset = null;

        async Task Load(int timeout, CancellationToken token)
        {
            ForumStateView state = ReadForumState(null, token);
            RequireForumSession(state);
            expected_generation = state.SessionGeneration;
            await application_runtime.InvokeAsync<
                ForumListRefreshRequest,
                ForumListRefreshResult>(
                    ApplicationMemberIds.ForumsListRefresh,
                    new ForumListRefreshRequest(
                        list_code,
                        start,
                        forum_limit,
                        timeout,
                        expected_generation),
                    token).ConfigureAwait(false);
            application_runtime.Invoke<ForumUnreadRequest, ForumDispatchResult>(
                ApplicationMemberIds.ForumsUnreadRequest,
                new ForumUnreadRequest(
                    ExpectedSessionGeneration: expected_generation),
                token);
        }

        QueryEnvelope<ForumSnapshot> Capture()
        {
            ForumStateView state = ReadForumState(requested_revision, cancellation_token);
            ValidateForumState(state, requested_revision, expected_generation);
            result_revision = state.SnapshotRevision;
            return ForumState(
                "forums",
                state,
                static snapshot =>
                    snapshot.ForumPages.Count > 0 || snapshot.KnownForums.Count > 0);
        }

        string envelope = await McpReadPipeline.ReadAsync<
            ForumSnapshot,
            McpForumCollectionSnapshot>(
                "forums",
                fetch && requested_revision is null,
                timeout_ms,
                cancellation_token,
                extension.IsConnected,
                extension.WaitForCatalogBuildAsync,
                Load,
                Capture,
                source =>
                {
                    QueryEnvelope<McpForumCollectionSnapshot> page =
                        PageForums(source, detail, start, forum_limit);
                    next_offset = NextOffset(
                        source,
                        start,
                        forum_limit,
                        data => data.KnownForums.Count);
                    return page;
                }).ConfigureAwait(false);
        return include_snapshot_revision && result_revision is long revision
            ? WithPageLease(envelope, revision, next_offset)
            : WithNextOffset(envelope, next_offset);
    }

    private async Task<string> ReadForumThreadsAsync(
        bool fetch,
        bool detail,
        long group_id_value,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool include_snapshot_revision)
    {
        int thread_limit = NormalizeLimit(limit, 100);
        int start = NormalizeOffset(offset);
        Id group_id = group_id_value;
        long? requested_revision = include_snapshot_revision
            ? NormalizeSnapshotRevision(snapshot_revision, start)
            : null;
        long? expected_generation = null;
        long? result_revision = null;
        int? next_offset = null;

        async Task Load(int timeout, CancellationToken token)
        {
            ForumStateView state = ReadForumState(null, token);
            RequireForumSession(state);
            expected_generation = state.SessionGeneration;
            await application_runtime.InvokeAsync<
                ForumThreadsRefreshRequest,
                ForumThreadsRefreshResult>(
                    ApplicationMemberIds.ForumThreadsRefresh,
                    new ForumThreadsRefreshRequest(
                        group_id,
                        start,
                        thread_limit,
                        timeout,
                        expected_generation),
                    token).ConfigureAwait(false);
        }

        QueryEnvelope<ForumSnapshot> Capture()
        {
            ForumStateView state = ReadForumState(requested_revision, cancellation_token);
            ValidateForumState(state, requested_revision, expected_generation);
            result_revision = state.SnapshotRevision;
            return ForumState(
                "forum_threads",
                state,
                snapshot =>
                    snapshot.FindThreadPage(group_id, start) is not null ||
                    ThreadsOf(snapshot, group_id).Count > 0);
        }

        string envelope = await McpReadPipeline.ReadAsync<
            ForumSnapshot,
            McpForumThreadCollectionSnapshot>(
                "forum_threads",
                fetch && requested_revision is null,
                timeout_ms,
                cancellation_token,
                extension.IsConnected,
                extension.WaitForCatalogBuildAsync,
                Load,
                Capture,
                source =>
                {
                    QueryEnvelope<McpForumThreadCollectionSnapshot> page =
                        PageForumThreads(source, group_id, detail, start, thread_limit);
                    next_offset = NextOffset(
                        source,
                        start,
                        thread_limit,
                        data => ThreadsOf(data, group_id).Count);
                    return page;
                }).ConfigureAwait(false);
        return include_snapshot_revision && result_revision is long revision
            ? WithPageLease(envelope, revision, next_offset)
            : WithNextOffset(envelope, next_offset);
    }

    public Task<string> GetQuestsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        ReadQuestsAsync(
            fetch,
            detail,
            limit,
            offset,
            null,
            timeoutMs,
            cancellationToken,
            extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            (request, token) => application_runtime.Invoke<
                QuestStateRequest,
                QuestStateView>(
                    ApplicationMemberIds.QuestsState,
                    request,
                    token),
            (request, token) => application_runtime.InvokeAsync<
                QuestAvailableRefreshRequest,
                QuestAvailableRefreshResult>(
                    ApplicationMemberIds.QuestsAvailableRefresh,
                    request,
                    token),
            (request, token) => application_runtime.InvokeAsync<
                QuestSeasonalRefreshRequest,
                QuestSeasonalRefreshResult>(
                    ApplicationMemberIds.QuestsSeasonalRefresh,
                    request,
                    token),
            (request, token) => application_runtime.Invoke<
                QuestEntryPageRequest,
                QuestEntryPage>(
                    ApplicationMemberIds.QuestsEntriesList,
                    request,
                    token),
            false);

    public Task<string> GetQuestsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetQuestsAsync(
            fetch,
            detail,
            limit,
            offset,
            snapshotRevision,
            timeoutMs,
            cancellationToken,
            extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            (request, token) => application_runtime.Invoke<
                QuestStateRequest,
                QuestStateView>(
                    ApplicationMemberIds.QuestsState,
                    request,
                    token),
            (request, token) => application_runtime.InvokeAsync<
                QuestAvailableRefreshRequest,
                QuestAvailableRefreshResult>(
                    ApplicationMemberIds.QuestsAvailableRefresh,
                    request,
                    token),
            (request, token) => application_runtime.InvokeAsync<
                QuestSeasonalRefreshRequest,
                QuestSeasonalRefreshResult>(
                    ApplicationMemberIds.QuestsSeasonalRefresh,
                    request,
                    token),
            (request, token) => application_runtime.Invoke<
                QuestEntryPageRequest,
                QuestEntryPage>(
                    ApplicationMemberIds.QuestsEntriesList,
                    request,
                    token));

    private static Task<string> GetQuestsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool connected,
        Func<CancellationToken, Task> await_readiness,
        Func<QuestStateRequest, CancellationToken, QuestStateView> read_state,
        Func<
            QuestAvailableRefreshRequest,
            CancellationToken,
            ValueTask<QuestAvailableRefreshResult>> refresh_available,
        Func<
            QuestSeasonalRefreshRequest,
            CancellationToken,
            ValueTask<QuestSeasonalRefreshResult>> refresh_seasonal,
        Func<QuestEntryPageRequest, CancellationToken, QuestEntryPage> read_page) =>
        ReadQuestsAsync(
            fetch,
            detail,
            limit,
            offset,
            snapshot_revision,
            timeout_ms,
            cancellation_token,
            connected,
            await_readiness,
            read_state,
            refresh_available,
            refresh_seasonal,
            read_page,
            true);

    private static async Task<string> ReadQuestsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool connected,
        Func<CancellationToken, Task> await_readiness,
        Func<QuestStateRequest, CancellationToken, QuestStateView> read_state,
        Func<
            QuestAvailableRefreshRequest,
            CancellationToken,
            ValueTask<QuestAvailableRefreshResult>> refresh_available,
        Func<
            QuestSeasonalRefreshRequest,
            CancellationToken,
            ValueTask<QuestSeasonalRefreshResult>> refresh_seasonal,
        Func<QuestEntryPageRequest, CancellationToken, QuestEntryPage> read_page,
        bool include_snapshot_revision)
    {
        ArgumentNullException.ThrowIfNull(await_readiness);
        ArgumentNullException.ThrowIfNull(read_state);
        ArgumentNullException.ThrowIfNull(refresh_available);
        ArgumentNullException.ThrowIfNull(refresh_seasonal);
        ArgumentNullException.ThrowIfNull(read_page);
        int item_limit = NormalizeLimit(limit, 200);
        int start = NormalizeOffset(offset);
        long? requested_revision = include_snapshot_revision
            ? NormalizeSnapshotRevision(snapshot_revision, start)
            : null;
        QuestStateView? refreshed_state = null;
        int? next_offset = null;
        long? result_revision = null;

        async Task Load(int timeout, CancellationToken token)
        {
            QuestStateView initial = read_state(new QuestStateRequest(), token);
            ValidateQuestState(initial, null, null, true);
            long generation = initial.SessionGeneration;
            Task<QuestAvailableRefreshResult> available = refresh_available(
                new QuestAvailableRefreshRequest(
                    Limit: 500,
                    TimeoutMilliseconds: timeout,
                    ExpectedSessionGeneration: generation),
                token).AsTask();
            Task<QuestSeasonalRefreshResult> seasonal = refresh_seasonal(
                new QuestSeasonalRefreshRequest(
                    Limit: 500,
                    TimeoutMilliseconds: timeout,
                    ExpectedSessionGeneration: generation),
                token).AsTask();
            await Task.WhenAll(available, seasonal).ConfigureAwait(false);
            ValidateQuestRefresh(await available.ConfigureAwait(false), generation);
            ValidateQuestRefresh(await seasonal.ConfigureAwait(false), generation);
            QuestStateView current = read_state(new QuestStateRequest(), token);
            ValidateQuestState(current, null, generation, true);
            if (!current.Summary.AvailableLoaded || !current.Summary.SeasonalLoaded)
            {
                throw new InvalidDataException(
                    "The quest refresh did not load both quest collections.");
            }
            refreshed_state = current;
        }

        QueryEnvelope<McpQuestCollectionSnapshot> Capture()
        {
            cancellation_token.ThrowIfCancellationRequested();
            QuestStateView state = requested_revision is long revision
                ? read_state(new QuestStateRequest(revision), cancellation_token)
                : refreshed_state ?? read_state(new QuestStateRequest(), cancellation_token);
            ValidateQuestState(state, requested_revision, null, false);
            QuestEntryPage page = read_page(
                new QuestEntryPageRequest(
                    QuestCollection.Combined,
                    start,
                    item_limit,
                    state.SnapshotRevision),
                cancellation_token);
            ValidateQuestPage(state, page, start, item_limit);
            QueryEnvelope<McpQuestCollectionSnapshot> envelope = QuestEnvelope(
                state,
                page,
                detail,
                item_limit);
            next_offset = page.NextOffset;
            result_revision = page.SnapshotRevision;
            return envelope;
        }

        string envelope = await McpReadPipeline.ReadAsync<
            McpQuestCollectionSnapshot,
            McpQuestCollectionSnapshot>(
                "quests",
                fetch && requested_revision is null,
                timeout_ms,
                cancellation_token,
                connected,
                await_readiness,
                Load,
                Capture,
                static source => source).ConfigureAwait(false);
        return include_snapshot_revision && result_revision is long revision
            ? WithPageLease(envelope, revision, next_offset)
            : WithNextOffset(envelope, next_offset);
    }

    public Task<string> GetCraftingAsync(
        bool fetch,
        long furniId,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetCraftingAsync(
            fetch,
            furniId,
            limit,
            offset,
            null,
            timeoutMs,
            cancellationToken);

    public Task<string> GetCraftingAsync(
        bool fetch,
        long furniId,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetCraftingAsync(
            fetch,
            furniId,
            limit,
            offset,
            snapshotRevision,
            timeoutMs,
            cancellationToken,
            extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            (request, token) => application_runtime.InvokeAsync<
                CraftingProductsRefreshRequest,
                CraftingProductsRefreshResult>(
                ApplicationMemberIds.CraftingProductsRefresh,
                request,
                token),
            (request, token) => application_runtime.Invoke<
                CraftingStateRequest,
                CraftingStateView>(
                ApplicationMemberIds.CraftingState,
                request,
                token),
            (request, token) => application_runtime.Invoke<
                CraftingProductsPageRequest,
                CraftingProductsPage>(
                ApplicationMemberIds.CraftingProductsList,
                request,
                token),
            (request, token) => application_runtime.Invoke<
                CraftingRecipePageRequest,
                CraftingRecipePage>(
                ApplicationMemberIds.CraftingRecipeList,
                request,
                token));

    private static async Task<string> GetCraftingAsync(
        bool fetch,
        long furni_id,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool connected,
        Func<CancellationToken, Task> await_readiness,
        Func<
            CraftingProductsRefreshRequest,
            CancellationToken,
            ValueTask<CraftingProductsRefreshResult>> refresh,
        Func<CraftingStateRequest, CancellationToken, CraftingStateView> read_state,
        Func<
            CraftingProductsPageRequest,
            CancellationToken,
            CraftingProductsPage> read_products,
        Func<CraftingRecipePageRequest, CancellationToken, CraftingRecipePage> read_recipe)
    {
        int product_limit = NormalizeLimit(limit, 200);
        int start = NormalizeOffset(offset);
        long? requested_revision = NormalizeSnapshotRevision(snapshot_revision, start);
        Id requested_furni_id = furni_id;
        CraftingProductsRefreshResult? refresh_result = null;
        int? next_offset = null;
        long? result_revision = null;
        async Task Load(int timeout, CancellationToken token)
        {
            refresh_result = await refresh(
                new CraftingProductsRefreshRequest(
                    requested_furni_id,
                    product_limit,
                    timeout),
                token).ConfigureAwait(false);
            ValidateCraftingRefresh(
                refresh_result,
                requested_furni_id,
                product_limit);
        }
        QueryEnvelope<McpCraftingSnapshot> Capture()
        {
            long? revision = refresh_result?.SnapshotRevision ?? requested_revision;
            CraftingStateView state = read_state(
                new CraftingStateRequest(revision),
                cancellation_token);
            ValidateCraftingState(state, revision, refresh_result);
            revision = state.SnapshotRevision;
            CraftingProductsPage products_page = read_products(
                new CraftingProductsPageRequest(
                    CraftingProductsCollection.Products,
                    start,
                    product_limit,
                    revision),
                cancellation_token);
            ValidateCraftingProductsPage(
                products_page,
                state,
                CraftingProductsCollection.Products,
                start,
                product_limit);

            int usable_limit = CraftingAncillaryEntryLimit;
            CraftingProductsPage usable_page = read_products(
                new CraftingProductsPageRequest(
                    CraftingProductsCollection.UsableInventoryFurnitureClasses,
                    0,
                    usable_limit,
                    revision),
                cancellation_token);
            ValidateCraftingProductsPage(
                usable_page,
                state,
                CraftingProductsCollection.UsableInventoryFurnitureClasses,
                0,
                usable_limit);
            int recipe_limit = CraftingAncillaryEntryLimit -
                usable_page.UsableInventoryFurnitureClasses.Count;
            int recipe_total = state.Recipe?.IngredientCount ?? 0;
            IReadOnlyList<CraftingIngredient> recipe_ingredients =
                Array.AsReadOnly(Array.Empty<CraftingIngredient>());
            if (recipe_limit > 0)
            {
                CraftingRecipePage recipe_page = read_recipe(
                    new CraftingRecipePageRequest(0, recipe_limit, revision),
                    cancellation_token);
                ValidateCraftingRecipePage(
                    recipe_page,
                    state,
                    0,
                    recipe_limit);
                recipe_ingredients = recipe_page.Ingredients;
            }

            bool products_truncated = products_page.NextOffset is not null;
            bool usable_truncated =
                usable_page.UsableInventoryFurnitureClasses.Count < usable_page.Total;
            bool recipe_truncated = recipe_ingredients.Count < recipe_total;
            bool truncated = products_truncated || usable_truncated || recipe_truncated;
            var snapshot = new McpCraftingSnapshot(
                products_page.Total,
                products_page.Products.Count,
                start,
                product_limit,
                truncated,
                usable_page.UsableInventoryFurnitureClasses,
                recipe_ingredients,
                state.LastResult,
                state.AvailableRecipes,
                products_page.Products,
                new McpCraftingCollectionMetadata(
                    usable_page.Total,
                    usable_page.UsableInventoryFurnitureClasses.Count,
                    usable_limit,
                    usable_truncated),
                new McpCraftingCollectionMetadata(
                    recipe_total,
                    recipe_ingredients.Count,
                    recipe_limit,
                    recipe_truncated));
            next_offset = products_page.NextOffset;
            result_revision = state.SnapshotRevision;
            bool loaded = state.Products is not null;
            return QueryResults.Success(
                "crafting",
                snapshot,
                state.Connected,
                loaded,
                false,
                truncated,
                loaded ? [] : ["craftableProducts"]);
        }

        string envelope = await McpReadPipeline.ReadAsync<
            McpCraftingSnapshot,
            McpCraftingSnapshot>(
            "crafting",
            fetch && furni_id != 0 && requested_revision is null,
            timeout_ms,
            cancellation_token,
            connected,
            await_readiness,
            Load,
            Capture,
            static source => source).ConfigureAwait(false);
        return result_revision is long revision
            ? WithPageLease(envelope, revision, next_offset)
            : WithNextOffset(envelope, next_offset);
    }

    public Task<string> GetSubscriptionsAsync(
        bool fetch,
        string product,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        int product_limit = NormalizeLimit(limit, 100);
        int start = NormalizeOffset(offset);
        return ReadSubscriptionsAsync(
            () => extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            async (product_name, timeout, token) => await application_runtime.InvokeAsync<
                SubscriptionUserInfoRefreshRequest,
                SubscriptionUserInfoRefreshResult>(
                    ApplicationMemberIds.SubscriptionsUserInfoRefresh,
                    new SubscriptionUserInfoRefreshRequest(product_name, timeout),
                    token)
                .ConfigureAwait(false),
            () => application_runtime.Invoke<
                SubscriptionStateRequest,
                SubscriptionStateView>(
                    ApplicationMemberIds.SubscriptionsState,
                    new SubscriptionStateRequest(Limit: 500)),
            fetch,
            product,
            product_limit,
            start,
            timeoutMs,
            cancellationToken);
    }

    private static async Task<string> ReadSubscriptionsAsync(
        Func<bool> connected,
        Func<CancellationToken, Task> await_readiness,
        Func<string, int, CancellationToken, Task<SubscriptionUserInfoRefreshResult>> refresh,
        Func<SubscriptionStateView> capture_state,
        bool fetch,
        string product_name,
        int product_limit,
        int start,
        int timeout_ms,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(connected);
        ArgumentNullException.ThrowIfNull(await_readiness);
        ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(capture_state);
        string requested_product = string.IsNullOrWhiteSpace(product_name)
            ? "habbo_club"
            : product_name.Trim();
        long? refreshed_generation = null;
        QueryEnvelope<McpSubscriptionState> capture()
        {
            try
            {
                return SubscriptionState(
                    capture_state(),
                    connected(),
                    refreshed_generation);
            }
            catch when (refreshed_generation is not null)
            {
                refreshed_generation = null;
                throw;
            }
        }
        int? next_offset = null;
        string envelope = await McpReadPipeline.ReadAsync<
            McpSubscriptionState,
            McpSubscriptionSnapshot>(
            "subscriptions",
            fetch,
            timeout_ms,
            cancellation_token,
            connected(),
            await_readiness,
            async (timeout, token) =>
            {
                SubscriptionUserInfoRefreshResult result = await refresh(
                    requested_product,
                    timeout,
                    token)
                    .ConfigureAwait(false);
                refreshed_generation = result.SessionGeneration;
            },
            capture,
            source =>
            {
                QueryEnvelope<McpSubscriptionSnapshot> result = PageSubscriptions(
                    source,
                    start,
                    product_limit);
                next_offset = NextOffset(
                    source,
                    start,
                    product_limit,
                    data => data.Products.Count);
                return result;
            }).ConfigureAwait(false);
        return WithNextOffset(envelope, next_offset);
    }

    public Task<string> GetGiftsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetGiftsAsync(
            fetch,
            detail,
            limit,
            offset,
            null,
            timeoutMs,
            cancellationToken);

    public Task<string> GetGiftsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        long? snapshotRevision,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        GetGiftsAsync(
            fetch,
            detail,
            limit,
            offset,
            snapshotRevision,
            timeoutMs,
            cancellationToken,
            extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            (request, token) => application_runtime.InvokeAsync<
                GiftRefreshRequest,
                GiftRefreshResult>(
                ApplicationMemberIds.GiftsRefresh,
                request,
                token),
            (request, token) => application_runtime.Invoke<GiftStateRequest, GiftStateView>(
                ApplicationMemberIds.GiftsState,
                request,
                token),
            (request, token) => application_runtime.Invoke<
                GiftClubInfoPageRequest,
                GiftClubInfoPage>(
                ApplicationMemberIds.GiftsClubInfoList,
                request,
                token),
            (request, token) => application_runtime.Invoke<
                GiftWrappingPageRequest,
                GiftWrappingPage>(
                ApplicationMemberIds.GiftsWrappingList,
                request,
                token));

    private static async Task<string> GetGiftsAsync(
        bool fetch,
        bool detail,
        int limit,
        int offset,
        long? snapshot_revision,
        int timeout_ms,
        CancellationToken cancellation_token,
        bool connected,
        Func<CancellationToken, Task> await_readiness,
        Func<GiftRefreshRequest, CancellationToken, ValueTask<GiftRefreshResult>> refresh,
        Func<GiftStateRequest, CancellationToken, GiftStateView> read_state,
        Func<GiftClubInfoPageRequest, CancellationToken, GiftClubInfoPage> read_club_info,
        Func<GiftWrappingPageRequest, CancellationToken, GiftWrappingPage> read_wrapping)
    {
        int offer_limit = NormalizeLimit(limit, 100);
        int start = NormalizeOffset(offset);
        long? requested_revision = NormalizeSnapshotRevision(snapshot_revision, start);
        GiftClubInfoPage? club_page = null;
        GiftRefreshResult? refresh_result = null;
        async Task Load(int timeout, CancellationToken token)
        {
            refresh_result = await refresh(
                new GiftRefreshRequest(offer_limit, timeout),
                token).ConfigureAwait(false);
            club_page = refresh_result.ClubInfoPage;
            ValidateGiftRefresh(refresh_result, club_page, offer_limit);
        }
        QueryEnvelope<McpGiftState> Capture()
        {
            club_page ??= read_club_info(
                new GiftClubInfoPageRequest(
                    GiftClubInfoCollection.Offers,
                    start,
                    offer_limit,
                    requested_revision),
                cancellation_token);
            ValidateGiftClubPage(
                club_page,
                GiftClubInfoCollection.Offers,
                start,
                offer_limit,
                requested_revision,
                null);
            GiftStateView state = read_state(new GiftStateRequest(), cancellation_token);
            ValidateGiftState(state, club_page);
            GiftWrappingConfiguration? wrapping = ReadGiftWrapping(
                club_page,
                refresh_result,
                read_wrapping,
                cancellation_token);
            bool loaded = wrapping is not null || club_page.Loaded;
            return QueryResults.Success(
                "gifts",
                new McpGiftState(state, club_page, wrapping),
                state.Connected,
                loaded,
                false,
                false,
                loaded ? [] : ["giftConfiguration"]);
        }

        int? next_offset = null;
        long? result_revision = null;
        string envelope = await McpReadPipeline.ReadAsync<McpGiftState, McpGiftSnapshot>(
            "gifts",
            fetch && requested_revision is null,
            timeout_ms,
            cancellation_token,
            connected,
            await_readiness,
            Load,
            Capture,
            source =>
            {
                QueryEnvelope<McpGiftSnapshot> result = PageGifts(
                    source,
                    detail,
                    offer_limit,
                    read_club_info,
                    cancellation_token);
                if (source.Data is { } data)
                {
                    next_offset = data.ClubInfoPage.NextOffset;
                    result_revision = data.ClubInfoPage.SnapshotRevision;
                }
                return result;
            }).ConfigureAwait(false);
        return result_revision is long revision
            ? WithPageLease(envelope, revision, next_offset)
            : WithNextOffset(envelope, next_offset);
    }

    public IReadOnlyList<string> ListScripts() =>
        Directory.Exists(scripts_root)
            ? Directory.GetFiles(scripts_root, "*.csx").Select(f => Path.GetFileNameWithoutExtension(f)!).ToList()
            : [];

    public string GetScript(string name)
    {
        string path = ScriptPath(name);
        return File.Exists(path) ? File.ReadAllText(path) : throw new InvalidOperationException($"no script named '{name}'");
    }

    public string SaveScript(string name, string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string path = ScriptPath(name);
        Directory.CreateDirectory(scripts_root);
        string staging = path + ".tmp";
        File.WriteAllText(staging, code);
        File.Move(staging, path, overwrite: true);
        return $"saved '{name}'";
    }

    public string GetAvatar(string name)
    {
        QueryEnvelope<AvatarCollectionSnapshot> source = queries.Avatars();
        AvatarSnapshot? avatar = source.Data?.Avatars.FirstOrDefault(candidate =>
            candidate.User is not null &&
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        ExternalTexts? texts = game.GameData.Texts;
        return QueryJson.Serialize(new QueryEnvelope<McpAvatarState>(
            "avatar",
            source.Metadata,
            avatar is null
                ? null
                : new McpAvatarState(
                    avatar,
                    avatar.Effect > 0 ? texts?.EffectName(avatar.Effect) : null,
                    avatar.HandItem > 0 ? texts?.HandItemName(avatar.HandItem) : null),
            source.Error));
    }

    public async Task<string> GetPetInfoAsync(long petId, CancellationToken cancellationToken)
    {
        object? request_session = null;
        long request_generation = -1;
        return await QueryAsync(
            "pet_info",
            cancellationToken,
            async globals =>
            {
                InventoryStateView state = application_runtime.Invoke<InventoryStateRequest, InventoryStateView>(
                    ApplicationMemberIds.InventoryState,
                    new InventoryStateRequest(),
                    cancellationToken);
                object session = extension.Session
                    ?? throw new InvalidOperationException("An active hotel session is required.");
                request_session = session;
                if (!state.Connected)
                    throw new InvalidOperationException("The inventory state is not bound to the active hotel session.");
                request_generation = state.SessionGeneration;
                RequireInventorySession(session, request_generation);
                PetInfo pet = await globals.GetPetInfo(petId).ConfigureAwait(false);
                RequireInventorySession(session, request_generation);
                return pet;
            },
            pet =>
            {
                object session = request_session
                    ?? throw new InvalidOperationException("The hotel session changed while pet information was being read.");
                RequireInventorySession(session, request_generation);

                int? pet_type = game.Room.Capture(
                    room => (room.AvatarById(pet.Id) as Pet)?.PetType);
                if (pet_type is null)
                {
                    InventoryPetPage page = application_runtime.Invoke<InventoryPetPageRequest, InventoryPetPage>(
                        ApplicationMemberIds.InventoryPetsList,
                        new InventoryPetPageRequest(PetId: pet.Id, Limit: 1),
                        cancellationToken);
                    ValidatePetPage(page, 0, 1, null);
                    if (!page.Connected || page.SessionGeneration != request_generation)
                        throw new InvalidOperationException("The hotel session changed while pet information was being read.");
                    pet_type = page.Pets.FirstOrDefault(candidate => candidate.Id == pet.Id)?.TypeId;
                }

                RequireInventorySession(session, request_generation);
                return SnapshotFactory.From(pet, pet_type);
            }).ConfigureAwait(false);
    }

    public string GetHeightmap() => QueryJson.Serialize(queries.Heightmap());

    public Task<string> GetHeightmapAsync(
        bool detail,
        int limit,
        CancellationToken cancellationToken) =>
        GetHeightmapAsync(detail, limit, 0, cancellationToken);

    public Task<string> GetHeightmapAsync(
        bool detail,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int tile_limit = NormalizeLimit(limit, 4096);
        int start = NormalizeOffset(offset);
        QueryEnvelope<HeightmapSnapshot?> snapshot = queries.Heightmap();
        if (!detail)
            return Task.FromResult(QueryJson.Serialize(McpReadProjection.Heightmap(snapshot)));

        string json = QueryJson.Serialize(McpReadProjection.HeightmapDetails(
            SkipTiles(snapshot, start),
            tile_limit));
        int available = snapshot.Data?.Tiles.Count ?? 0;
        return Task.FromResult(WithNextOffset(
            json,
            start + tile_limit < available ? start + tile_limit : null));
    }

    public async Task<string> RunScriptAsync(string name, CancellationToken cancellationToken)
    {
        string path = ScriptPath(name);
        string code = await RunnableTextAsync(name, path, cancellationToken).ConfigureAwait(false);
        ScriptExecutionResult result = await RunSourceAsync(
            code,
            path,
            path,
            cancellationToken).ConfigureAwait(false);
        return SerializeExecution(result);
    }

    public string StartCode(string code, int? timeoutMs) =>
        JsonSerializer.Serialize(
            background_runs.Start(code, "run_code", null, "mcp.csx", RunLimit(timeoutMs)).Read(0),
            JsonOptions);

    public async Task<string> StartScriptAsync(string name, int? timeoutMs, CancellationToken cancellationToken)
    {
        string path = ScriptPath(name);
        string code = await RunnableTextAsync(name, path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(
            background_runs.Start(code, Path.GetFileNameWithoutExtension(path), path, path, RunLimit(timeoutMs)).Read(0),
            JsonOptions);
    }

    public string ReadRun(long? id, int since) =>
        id is { } value
            ? JsonSerializer.Serialize(FindRun(value).Read(since), JsonOptions)
            : JsonSerializer.Serialize(background_runs.All.Select(run => run.Read(int.MaxValue)), JsonOptions);

    public string StopRun(long id) =>
        FindRun(id).Stop() ? $"stopping run {id}" : $"run {id} has already ended";

    private BackgroundRun FindRun(long id) =>
        background_runs.Find(id) ?? throw new InvalidOperationException($"no background run {id}; get_run without an id lists them");

    private static TimeSpan? RunLimit(int? timeout_ms) =>
        timeout_ms is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null;

    private async Task<string> RunnableTextAsync(string name, string path, CancellationToken cancellation_token) =>
        await ReadOpenScriptAsync(name, cancellation_token).ConfigureAwait(false)
            ?? (File.Exists(path)
                ? await File.ReadAllTextAsync(path, cancellation_token).ConfigureAwait(false)
                : throw new InvalidOperationException($"no script named '{name}'"));

    private static string SerializeExecution(ScriptExecutionResult result) =>
        JsonSerializer.Serialize(new ScriptExecutionSnapshot(
            result.State,
            result.Faulted,
            result.RuntimeMs,
            result.Output,
            result.Errors), JsonOptions);

    private Task<ScriptExecutionResult> RunSourceAsync(
        string code,
        string source_identity,
        string file_name,
        CancellationToken cancellation_token) =>
        script_execution.RunAsync(new ScriptExecutionRequest
        {
            Code = code,
            SourceIdentity = source_identity,
            FileName = file_name
        }, cancellation_token);

    public string RemoveRights(long userId) { Globals().RemoveRights(userId); return $"removed rights from #{userId}"; }

    public string GetControllers() => QueryJson.Serialize(queries.Controllers());

    public string GetCurrencies() => QueryJson.Serialize(queries.Currencies());

    public Task<string> GetCurrenciesAsync(
        bool fetch,
        int timeoutMs,
        CancellationToken cancellationToken) =>
        ReadAsync(
            "currencies",
            fetch,
            timeoutMs,
            cancellationToken,
            (timeout, token) => application_runtime.InvokeAsync<
                    WalletRefreshRequest,
                    WalletStateView>(
                    ApplicationMemberIds.WalletRefresh,
                    new WalletRefreshRequest(
                        PointLimit: 500,
                        TimeoutMilliseconds: timeout),
                    token)
                .AsTask(),
            queries.Currencies,
            static snapshot => snapshot);

    public async Task<string> DeleteScriptAsync(string name, CancellationToken cancellationToken)
    {
        string path = ScriptPath(name);
        if (!File.Exists(path))
            throw new InvalidOperationException($"no script named '{name}'");
        if (editor is not null && await editor.DeleteScriptAsync(name, cancellationToken).ConfigureAwait(false) is { } done)
            return done;
        File.Delete(path);
        return $"deleted '{name}'";
    }

    public async Task<string> RenameScriptAsync(string name, string newName, CancellationToken cancellationToken)
    {
        string source = ScriptPath(name);
        string target = ScriptPath(newName);
        if (!File.Exists(source))
            throw new InvalidOperationException($"no script named '{name}'");
        if (editor is not null && await editor.RenameScriptAsync(name, newName, cancellationToken).ConfigureAwait(false) is { } done)
            return done;
        if (File.Exists(target) && !string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StoragePaths.FileComparison))
            throw new InvalidOperationException($"a script named '{newName}' already exists");
        File.Move(source, target);
        return $"renamed '{name}' to '{newName}'";
    }

    public IReadOnlyList<string> SearchScripts(string query) =>
        ListScripts().Where(s => s.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

    public string ListApi(string filter, int limit, int offset)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        IReadOnlyList<ScriptApiMember> matches = ScriptApiCatalog.Search(filter);
        int start = NormalizeOffset(offset);
        ScriptApiMember[] page = [.. matches.Skip(start).Take(limit)];
        var text = new StringBuilder();
        if (string.IsNullOrWhiteSpace(filter) && start == 0)
        {
            text.Append($"ScriptGlobals has {matches.Count} members. A filter matches member names first, then groups, signatures and summaries.\nGroups: ")
                .AppendJoin(", ", matches
                    .GroupBy(member => member.Group, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => $"{group.Key} {group.Count()}"))
                .Append('\n');
        }
        foreach (ScriptApiMember member in page)
            text.Append(member.HasSummary ? $"{member.Signature}  — {member.Summary}" : member.Signature).Append('\n');
        int end = start + page.Length;
        text.Append($"total {matches.Count}");
        if (end < matches.Count)
            text.Append($", nextOffset {end}");
        return text.ToString();
    }

    public string ListLibraries() => JsonSerializer.Serialize(ApiTypes.Assemblies, JsonOptions);

    public string SearchTypes(string query, string assembly, int limit, int offset) =>
        JsonSerializer.Serialize(ApiTypes.SearchTypes(query, assembly, limit, offset), JsonOptions);

    public string GetTypeInfo(string name, string member) =>
        JsonSerializer.Serialize(ApiTypes.GetType(name, member), JsonOptions);

    public string SearchMembers(string query, string kind, bool includeGenerated, int limit, int offset) =>
        JsonSerializer.Serialize(ApiTypes.SearchMembers(query, kind, limit, offset, includeGenerated), JsonOptions);

    public string CompileCheck(string code)
    {
        const string file_name = "mcp.csx";
        string[] lines =
        [
            .. script_execution.Compile(code, file_name)
                .Where(diagnostic => diagnostic.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .Select(diagnostic => CompileCheckLine(diagnostic, file_name))
        ];
        return lines.Length == 0 ? "OK — compiles with no errors or warnings" : string.Join("\n", lines);
    }

    private static string CompileCheckLine(Microsoft.CodeAnalysis.Diagnostic diagnostic, string file_name)
    {
        ScriptExecutionError error = ScriptExecutionError.FromDiagnostic(diagnostic, file_name);
        string file = error.File is null || error.File == file_name ? "" : error.File + ":";
        string position = error.Line is null ? "" : $" {file}{error.Line}:{error.Column}";
        string hint = ScriptEngine.UsingHint(diagnostic) is { } text ? " — " + text : "";
        return $"{diagnostic.Severity.ToString().ToLowerInvariant()} {error.Type}{position}: {error.Message}{hint}";
    }

    public string GetScriptingGuide(string topic) =>
        string.IsNullOrWhiteSpace(topic) ? ScriptingGuide.Overview() : ScriptingGuide.Read(topic);

    private static string Editor(
        IEditorBridge? editor,
        Func<IEditorBridge, string> operation)
    {
        if (editor is null)
            return "editor UI not available";

        try
        {
            return operation(editor);
        }
        catch (NotSupportedException)
        {
            return "synchronous editor UI access not available";
        }
    }

    private string ScriptPath(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Script name must be a file name without a path.", nameof(name));
        }

        string path = Path.GetFullPath(Path.Combine(scripts_root, name + ".csx"));
        string relative = Path.GetRelativePath(scripts_root, path);
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new ArgumentException("Script path is outside the script library.", nameof(name));
        }
        return path;
    }

    private async Task<string> QueryAsync<T>(
        string query,
        CancellationToken cancellation_token,
        Func<ScriptGlobals, Task<T>> request) =>
        await QueryAsync(query, cancellation_token, request, static result => result);

    private async Task<string> QueryAsync<T, TResult>(
        string query,
        CancellationToken cancellation_token,
        Func<ScriptGlobals, Task<T>> request,
        Func<T, TResult> projection)
    {
        try
        {
            cancellation_token.ThrowIfCancellationRequested();
            using ScriptGlobals globals = Globals(cancellation_token);
            T response = await request(globals).ConfigureAwait(false);
            TResult result = projection(response);
            return QueryJson.Serialize(QueryResults.Success(query, result));
        }
        catch (Exception error)
        {
            return QueryJson.SerializeFailure(query, error, cancellation_token);
        }
    }

    private ScriptGlobals Globals(CancellationToken cancellation_token = default) =>
        new(extension, game, application_runtime, _ => { }, cancellation_token);

    private async Task<string> ReadPagedAsync<TSource, TResult>(
        string query,
        bool fetch,
        int timeout_ms,
        CancellationToken cancellation_token,
        Func<int, CancellationToken, Task> load,
        Func<QueryEnvelope<TSource>> capture,
        Func<QueryEnvelope<TSource>, QueryEnvelope<TResult>> project,
        Func<QueryEnvelope<TSource>, int?> next,
        Func<QueryEnvelope<TSource>, long?>? lease = null)
    {
        int? next_offset = null;
        long? snapshot_revision = null;
        string envelope = await ReadAsync<TSource, TResult>(
            query,
            fetch,
            timeout_ms,
            cancellation_token,
            load,
            capture,
            source =>
            {
                QueryEnvelope<TResult> result = project(source);
                next_offset = next(source);
                snapshot_revision = lease?.Invoke(source);
                return result;
            }).ConfigureAwait(false);
        return snapshot_revision is long revision
            ? WithPageLease(envelope, revision, next_offset)
            : WithNextOffset(envelope, next_offset);
    }

    private async Task<string> ReadAsync<TSource, TResult>(
        string query,
        bool fetch,
        int timeout_ms,
        CancellationToken cancellation_token,
        Func<int, CancellationToken, Task> load,
        Func<QueryEnvelope<TSource>> capture,
        Func<QueryEnvelope<TSource>, QueryEnvelope<TResult>> project) =>
        await McpReadPipeline.ReadAsync(
            query,
            fetch,
            timeout_ms,
            cancellation_token,
            extension.IsConnected,
            extension.WaitForCatalogBuildAsync,
            load,
            capture,
            project).ConfigureAwait(false);

    private ForumStateView ReadForumState(
        long? snapshot_revision,
        CancellationToken cancellation_token) =>
        application_runtime.Invoke<ForumStateRequest, ForumStateView>(
            ApplicationMemberIds.ForumsState,
            new ForumStateRequest(snapshot_revision),
            cancellation_token);

    private static QueryEnvelope<ForumSnapshot> ForumState(
        string query,
        ForumStateView state,
        Func<ForumSnapshot, bool> loaded)
    {
        ArgumentNullException.ThrowIfNull(state);
        ForumSnapshot snapshot = state.Snapshot ??
            throw new InvalidDataException("The forum application returned no snapshot.");
        bool is_loaded = loaded(snapshot);
        return QueryResults.Success(
            query,
            snapshot,
            state.Connected,
            is_loaded,
            !state.Connected && is_loaded,
            false,
            is_loaded ? [] : [query]);
    }

    private static void RequireForumSession(ForumStateView state)
    {
        ValidateForumState(state, null, null);
        if (!state.Connected || state.SessionGeneration <= 0)
            throw new InvalidOperationException("An active hotel session is required.");
    }

    private static void ValidateForumState(
        ForumStateView state,
        long? snapshot_revision,
        long? expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(state.Snapshot);
        if (state.SnapshotRevision <= 0 ||
            state.SessionGeneration < 0 ||
            state.Connected && state.SessionGeneration <= 0 ||
            snapshot_revision is long revision && state.SnapshotRevision != revision ||
            expected_session_generation is long generation &&
                state.SessionGeneration != generation)
        {
            throw new InvalidDataException("The forum application returned an invalid state.");
        }
    }

    private static QueryEnvelope<McpQuestCollectionSnapshot> QuestEnvelope(
        QuestStateView state,
        QuestEntryPage page,
        bool detail,
        int limit)
    {
        QuestData[] quests = [.. page.Entries.Select(entry => ToQuestData(entry.Quest))];
        QuestData? current = state.Current is null ? null : ToQuestData(state.Current);
        QuestDaily? daily = state.Daily is null ? null : ToQuestDaily(state.Daily);
        QuestCompleted? completed = state.LastCompletion is null
            ? null
            : new QuestCompleted(
                ToQuestData(state.LastCompletion.Quest),
                state.LastCompletion.ShowDialog);
        QuestCancelled? cancelled = state.LastCancellation is null
            ? null
            : new QuestCancelled(
                state.LastCancellation.IsExpired,
                ToQuestData(state.LastCancellation.Quest));
        bool loaded = state.Connected &&
            state.Summary.AvailableLoaded &&
            state.Summary.SeasonalLoaded;
        var data = new McpQuestCollectionSnapshot(
            page.Total,
            quests.Length,
            page.Offset,
            limit,
            page.NextOffset is not null,
            state.Summary.AvailableCount,
            state.Summary.SeasonalCount,
            current is null ? null : CompactQuest(current),
            daily,
            completed,
            cancelled,
            [.. quests.Select(CompactQuest)],
            detail ? Array.AsReadOnly(quests) : null);
        return QueryResults.Success(
            "quests",
            data,
            state.Connected,
            loaded,
            !state.Connected &&
                state.Summary.AvailableLoaded &&
                state.Summary.SeasonalLoaded,
            page.NextOffset is not null,
            loaded ? [] : ["quests"]);
    }

    private static QuestData ToQuestData(QuestView value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var quest = new QuestData(
            value.CampaignCode,
            value.CompletedQuestsInCampaign,
            value.QuestCountInCampaign,
            value.ActivityPointType,
            value.Id,
            value.IsAccepted,
            value.Type,
            value.ImageVersion,
            value.RewardCurrencyAmount,
            value.LocalizationCode,
            value.CompletedSteps,
            value.TotalSteps,
            value.SortOrder,
            value.CatalogPageName,
            value.ChainCode,
            value.IsEasy,
            value.IsSeasonal,
            value.SeasonalSecondsLeft);
        if (quest.IsCompleted != value.IsCompleted ||
            quest.IsCampaignCompleted != value.IsCampaignCompleted ||
            quest.IsLastQuestInCampaign != value.IsLastQuestInCampaign ||
            quest.CampaignChainCode != value.CampaignChainCode)
        {
            throw new InvalidDataException(
                "The quest application returned inconsistent derived quest data.");
        }
        return quest;
    }

    private static QuestDaily ToQuestDaily(QuestDailyView value)
    {
        ArgumentNullException.ThrowIfNull(value);
        QuestData? quest = value.Quest is null ? null : ToQuestData(value.Quest);
        if (value.HasQuest != (quest is not null))
            throw new InvalidDataException("The quest application returned an invalid daily quest.");
        return new QuestDaily(quest, value.EasyQuestCount, value.HardQuestCount);
    }

    private static QueryEnvelope<McpSubscriptionState> SubscriptionState(
        SubscriptionStateView subscriptions,
        bool connected,
        long? expected_session_generation = null)
    {
        if (expected_session_generation is long expected_generation &&
            subscriptions.SessionGeneration != expected_generation)
        {
            throw new InvalidOperationException(
                "The hotel session changed after the subscription refresh completed.");
        }
        ScrSendUserInfo[] products =
        [
            .. subscriptions.Products
                .Select(SubscriptionProduct)
                .OrderBy(info => info.ProductName, StringComparer.Ordinal)
        ];
        var state = new McpSubscriptionState(
            products,
            subscriptions.Kickback is { } kickback
                ? SubscriptionKickback(kickback)
                : null,
            subscriptions.BuildersClubFurniCount is int furni_count
                ? new BuildersClubFurniCount(furni_count)
                : null,
            subscriptions.BuildersClubMembership is { } membership
                ? SubscriptionMembership(membership)
                : null);
        IReadOnlyList<string> missing = state.Products.Count > 0
            ? []
            : ["subscriptions"];
        return QueryResults.Success(
            "subscriptions",
            state,
            connected,
            state.Products.Count > 0,
            !connected && state.Products.Count > 0,
            false,
            missing);
    }

    private static ScrSendUserInfo SubscriptionProduct(
        SubscriptionProductView product) => new(
        product.ProductName,
        product.DaysToPeriodEnd,
        product.MemberPeriods,
        product.PeriodsSubscribedAhead,
        product.ResponseType,
        product.HasEverBeenMember,
        product.IsVip,
        product.PastClubDays,
        product.PastVipDays,
        product.MinutesUntilExpiration,
        product.MinutesSinceLastModified);

    private static ScrSendKickbackInfo SubscriptionKickback(
        SubscriptionKickbackView kickback) => new(
        kickback.CurrentHcStreak,
        kickback.FirstSubscriptionDate,
        kickback.KickbackPercentage,
        kickback.TotalCreditsMissed,
        kickback.TotalCreditsRewarded,
        kickback.TotalCreditsSpent,
        kickback.CreditRewardForStreakBonus,
        kickback.CreditRewardForMonthlySpent,
        kickback.TimeUntilPayday);

    private static BuildersClubMembershipStatus SubscriptionMembership(
        SubscriptionBuildersClubMembershipView membership) => new(
        membership.SecondsLeft,
        membership.FurniLimit,
        membership.MaxFurniLimit,
        membership.SecondsLeftWithGrace);

    private QueryEnvelope<T> Subsystem<T>(
        string query,
        T data,
        bool loaded,
        string pending)
    {
        bool connected = extension.Session is not null;
        IReadOnlyList<string> missing = [];
        if (!loaded)
            missing = [pending];
        return QueryResults.Success(
            query,
            data,
            connected,
            loaded,
            !connected && loaded,
            false,
            missing);
    }

    private static QueryEnvelope<McpForumCollectionSnapshot> PageForums(
        QueryEnvelope<ForumSnapshot> source,
        bool detail,
        int offset,
        int limit)
    {
        if (source.Data is not { } data)
            return Reshape<ForumSnapshot, McpForumCollectionSnapshot>(source, null, false);

        ForumSummary[] forums = [.. data.KnownForums.Values.OrderBy(forum => forum.GroupId)];
        ForumSummary[] page = [.. forums.Skip(offset).Take(limit)];
        bool truncated = offset + page.Length < forums.Length;
        IReadOnlyList<ForumDetails>? details = null;
        if (detail)
        {
            details =
            [
                .. page
                    .Select(forum => data.FindDetails(forum.GroupId))
                    .OfType<ForumDetails>()
            ];
        }

        return Reshape(
            source,
            new McpForumCollectionSnapshot(
                data.UnreadForumsCount,
                forums.Length,
                page.Length,
                offset,
                limit,
                truncated,
                data.ForumDetails.Count,
                data.KnownThreads.Count,
                data.KnownMessages.Count,
                [.. page.Select(CompactForum)],
                details),
            truncated);
    }

    private static QueryEnvelope<McpForumThreadCollectionSnapshot> PageForumThreads(
        QueryEnvelope<ForumSnapshot> source,
        Id group_id,
        bool detail,
        int offset,
        int limit)
    {
        if (source.Data is not { } data)
            return Reshape<ForumSnapshot, McpForumThreadCollectionSnapshot>(source, null, false);

        IReadOnlyList<ForumThread> threads = ThreadsOf(data, group_id);
        ForumThread[] page = [.. threads.Skip(offset).Take(limit)];
        bool truncated = offset + page.Length < threads.Count;
        ForumDetails? forum_details = data.FindDetails(group_id);
        IReadOnlyList<ForumThread>? details = null;
        if (detail)
            details = page;

        return Reshape(
            source,
            new McpForumThreadCollectionSnapshot(
                group_id,
                forum_details?.Name ?? data.FindForum(group_id)?.Name,
                threads.Count,
                page.Length,
                offset,
                limit,
                truncated,
                data.KnownMessages.Count(entry => entry.Key.GroupId == group_id),
                forum_details?.Permissions,
                [.. page.Select(CompactForumThread)],
                details),
            truncated);
    }

    private static void ValidateQuestState(
        QuestStateView state,
        long? snapshot_revision,
        long? expected_session_generation,
        bool require_connected)
    {
        ArgumentNullException.ThrowIfNull(state);
        QuestSummary summary = state.Summary ??
            throw new InvalidDataException("The quest application returned no summary.");
        bool connected = state.Connected && state.SessionGeneration > 0;
        if (state.SnapshotRevision <= 0 ||
            state.Revision < 0 ||
            state.AvailableRevision < 0 ||
            state.SeasonalRevision < 0 ||
            state.CurrentRevision < 0 ||
            state.CompletionRevision < 0 ||
            state.CancellationRevision < 0 ||
            state.DailyRevision < 0 ||
            summary.AvailableCount < 0 ||
            summary.SeasonalCount < 0 ||
            state.SessionGeneration < 0 ||
            state.Connected != connected ||
            require_connected && !connected ||
            snapshot_revision is long revision && state.SnapshotRevision != revision ||
            expected_session_generation is long generation &&
                state.SessionGeneration != generation ||
            summary.HasCurrent != (state.Current is not null) ||
            summary.HasCompletion != (state.LastCompletion is not null) ||
            summary.HasCancellation != (state.LastCancellation is not null) ||
            summary.HasDailyQuest != (state.Daily?.HasQuest is true) ||
            !summary.AvailableLoaded && summary.AvailableCount != 0 ||
            !summary.SeasonalLoaded && summary.SeasonalCount != 0)
        {
            throw new InvalidDataException(
                "The quest application returned an invalid state snapshot.");
        }
        if (state.Daily is { } daily)
            _ = ToQuestDaily(daily);
        if (state.Current is { } current)
            _ = ToQuestData(current);
        if (state.LastCompletion is { } completed)
            _ = ToQuestData(completed.Quest);
        if (state.LastCancellation is { } cancelled)
            _ = ToQuestData(cancelled.Quest);
    }

    private static void ValidateQuestRefresh(
        QuestAvailableRefreshResult result,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(result);
        QuestEntryPage page = result.FirstPage ??
            throw new InvalidDataException("The quest refresh returned no first page.");
        ValidateQuestPageShape(page, QuestCollection.Available, 0, 500);
        if (result.SnapshotRevision <= 0 ||
            result.MessagesDispatched is < 0 or > 1 ||
            result.SessionGeneration != expected_session_generation ||
            !page.Connected ||
            page.SessionGeneration != result.SessionGeneration ||
            page.StateRevision != result.StateRevision ||
            page.AvailableRevision != result.AvailableRevision ||
            page.SnapshotRevision != result.SnapshotRevision ||
            !page.Summary.AvailableLoaded)
        {
            throw new InvalidDataException(
                "The available quest refresh returned an inconsistent snapshot.");
        }
    }

    private static void ValidateQuestRefresh(
        QuestSeasonalRefreshResult result,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(result);
        QuestEntryPage page = result.FirstPage ??
            throw new InvalidDataException("The quest refresh returned no first page.");
        ValidateQuestPageShape(page, QuestCollection.Seasonal, 0, 500);
        if (result.SnapshotRevision <= 0 ||
            result.MessagesDispatched is < 0 or > 1 ||
            result.SessionGeneration != expected_session_generation ||
            !page.Connected ||
            page.SessionGeneration != result.SessionGeneration ||
            page.StateRevision != result.StateRevision ||
            page.SeasonalRevision != result.SeasonalRevision ||
            page.SnapshotRevision != result.SnapshotRevision ||
            !page.Summary.SeasonalLoaded)
        {
            throw new InvalidDataException(
                "The seasonal quest refresh returned an inconsistent snapshot.");
        }
    }

    private static void ValidateQuestPage(
        QuestStateView state,
        QuestEntryPage page,
        int offset,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(page);
        ValidateQuestPageShape(page, QuestCollection.Combined, offset, limit);
        if (page.Connected != state.Connected ||
            page.SessionGeneration != state.SessionGeneration ||
            page.StateRevision != state.Revision ||
            page.AvailableRevision != state.AvailableRevision ||
            page.SeasonalRevision != state.SeasonalRevision ||
            page.SnapshotRevision != state.SnapshotRevision ||
            page.Summary != state.Summary)
        {
            throw new InvalidDataException(
                "The quest application returned a page from another snapshot.");
        }
    }

    private static void ValidateQuestPageShape(
        QuestEntryPage page,
        QuestCollection collection,
        int offset,
        int limit)
    {
        QuestSummary summary = page.Summary ??
            throw new InvalidDataException("The quest application returned no page summary.");
        IReadOnlyList<QuestEntryView> entries = page.Entries ??
            throw new InvalidDataException("The quest application returned no page entries.");
        int expected_total = collection switch
        {
            QuestCollection.Available => summary.AvailableCount,
            QuestCollection.Seasonal => summary.SeasonalCount,
            QuestCollection.Combined => checked(summary.AvailableCount + summary.SeasonalCount),
            _ => throw new ArgumentOutOfRangeException(nameof(collection))
        };
        int expected_count = offset >= expected_total
            ? 0
            : Math.Min(limit, expected_total - offset);
        int consumed = checked(offset + entries.Count);
        int? expected_next = consumed < expected_total ? consumed : null;
        if (page.SnapshotRevision <= 0 ||
            page.Connected && page.SessionGeneration <= 0 ||
            !page.Connected && page.SessionGeneration < 0 ||
            summary.AvailableCount < 0 ||
            summary.SeasonalCount < 0 ||
            page.Collection != collection ||
            page.Total != expected_total ||
            page.Offset != offset ||
            entries.Count != expected_count ||
            page.NextOffset != expected_next)
        {
            throw new InvalidDataException(
                "The quest application returned an invalid snapshot page.");
        }
        for (int index = 0; index < entries.Count; index++)
        {
            QuestEntryView entry = entries[index] ??
                throw new InvalidDataException("The quest application returned a null entry.");
            int ordinal = checked(offset + index);
            QuestCollection expected_collection;
            int expected_collection_ordinal;
            if (collection is QuestCollection.Combined)
            {
                expected_collection = ordinal < summary.AvailableCount
                    ? QuestCollection.Available
                    : QuestCollection.Seasonal;
                expected_collection_ordinal = expected_collection is QuestCollection.Available
                    ? ordinal
                    : ordinal - summary.AvailableCount;
            }
            else
            {
                expected_collection = collection;
                expected_collection_ordinal = ordinal;
            }
            if (entry.Ordinal != ordinal ||
                entry.Collection != expected_collection ||
                entry.CollectionOrdinal != expected_collection_ordinal)
            {
                throw new InvalidDataException(
                    "The quest application returned an out-of-order entry.");
            }
            _ = ToQuestData(entry.Quest);
        }
    }

    private static void ValidateCraftingRefresh(
        CraftingProductsRefreshResult result,
        Id requested_furni_id,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(result);
        CraftingProductsPage page = result.FirstPage;
        ArgumentNullException.ThrowIfNull(page);
        int expected_count = PageCount(page.Total, 0, limit);
        int consumed = page.Products.Count;
        int? expected_next = consumed < page.Total ? consumed : null;
        if (result.SnapshotRevision <= 0 ||
            result.RequestedCraftingFurnitureId != requested_furni_id ||
            result.MessagesDispatched != 1 ||
            page.Connected is false ||
            page.SessionGeneration != result.SessionGeneration ||
            page.StateRevision != result.StateRevision ||
            page.ProductsRevision != result.ProductsRevision ||
            page.SnapshotRevision != result.SnapshotRevision ||
            page.Collection is not CraftingProductsCollection.Products ||
            page.Loaded is false ||
            page.Total != page.ProductCount ||
            page.Offset != 0 ||
            page.Products.Count != expected_count ||
            page.UsableInventoryFurnitureClasses.Count != 0 ||
            page.NextOffset != expected_next)
        {
            throw new InvalidDataException(
                "The crafting products refresh returned an inconsistent application snapshot.");
        }
    }

    private static void ValidateCraftingState(
        CraftingStateView state,
        long? snapshot_revision,
        CraftingProductsRefreshResult? refresh)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SnapshotRevision <= 0 ||
            snapshot_revision is long expected_revision &&
            state.SnapshotRevision != expected_revision)
        {
            throw new InvalidDataException(
                "The crafting state returned an inconsistent application snapshot.");
        }
        if (refresh is not null &&
            (state.Connected is false ||
             state.SessionGeneration != refresh.SessionGeneration ||
             state.Revision != refresh.StateRevision ||
             state.ProductsRevision != refresh.ProductsRevision ||
             state.SnapshotRevision != refresh.SnapshotRevision ||
             state.Products is null ||
             state.Products.ProductCount != refresh.FirstPage.ProductCount ||
             state.Products.UsableInventoryFurnitureClassCount !=
                 refresh.FirstPage.UsableInventoryFurnitureClassCount))
        {
            throw new InvalidDataException(
                "The crafting session changed after the products refresh.");
        }
    }

    private static void ValidateCraftingProductsPage(
        CraftingProductsPage page,
        CraftingStateView state,
        CraftingProductsCollection collection,
        int offset,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(page);
        int product_count = state.Products?.ProductCount ?? 0;
        int usable_count = state.Products?.UsableInventoryFurnitureClassCount ?? 0;
        int total = collection is CraftingProductsCollection.Products
            ? product_count
            : usable_count;
        int expected_count = PageCount(total, offset, limit);
        int returned = collection is CraftingProductsCollection.Products
            ? page.Products.Count
            : page.UsableInventoryFurnitureClasses.Count;
        int consumed = checked(offset + returned);
        int? expected_next = consumed < total ? consumed : null;
        bool invalid_shape = collection is CraftingProductsCollection.Products
            ? page.UsableInventoryFurnitureClasses.Count != 0
            : page.Products.Count != 0;
        if (page.Connected != state.Connected ||
            page.SessionGeneration != state.SessionGeneration ||
            page.StateRevision != state.Revision ||
            page.ProductsRevision != state.ProductsRevision ||
            page.SnapshotRevision != state.SnapshotRevision ||
            page.Loaded != (state.Products is not null) ||
            page.ProductCount != product_count ||
            page.UsableInventoryFurnitureClassCount != usable_count ||
            page.Collection != collection ||
            page.Total != total ||
            page.Offset != offset ||
            returned != expected_count ||
            page.NextOffset != expected_next ||
            invalid_shape)
        {
            throw new InvalidDataException(
                "The crafting products page is inconsistent with its application snapshot.");
        }
    }

    private static void ValidateCraftingRecipePage(
        CraftingRecipePage page,
        CraftingStateView state,
        int offset,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(page);
        int total = state.Recipe?.IngredientCount ?? 0;
        int expected_count = PageCount(total, offset, limit);
        int consumed = checked(offset + page.Ingredients.Count);
        int? expected_next = consumed < total ? consumed : null;
        if (page.Connected != state.Connected ||
            page.SessionGeneration != state.SessionGeneration ||
            page.StateRevision != state.Revision ||
            page.RecipeRevision != state.RecipeRevision ||
            page.SnapshotRevision != state.SnapshotRevision ||
            page.Loaded != (state.Recipe is not null) ||
            page.Total != total ||
            page.Offset != offset ||
            page.Ingredients.Count != expected_count ||
            page.NextOffset != expected_next)
        {
            throw new InvalidDataException(
                "The crafting recipe page is inconsistent with its application snapshot.");
        }
    }

    private static QueryEnvelope<McpSubscriptionSnapshot> PageSubscriptions(
        QueryEnvelope<McpSubscriptionState> source,
        int offset,
        int limit)
    {
        if (source.Data is not { } data)
            return Reshape<McpSubscriptionState, McpSubscriptionSnapshot>(source, null, false);

        ScrSendUserInfo[] page = [.. data.Products.Skip(offset).Take(limit)];
        bool truncated = offset + page.Length < data.Products.Count;
        return Reshape(
            source,
            new McpSubscriptionSnapshot(
                data.Products.Count,
                page.Length,
                offset,
                limit,
                truncated,
                data.Kickback,
                data.BuildersClubFurni?.FurniCount,
                data.BuildersClubMembership,
                page),
            truncated);
    }

    private static QueryEnvelope<McpGiftSnapshot> PageGifts(
        QueryEnvelope<McpGiftState> source,
        bool detail,
        int limit,
        Func<GiftClubInfoPageRequest, CancellationToken, GiftClubInfoPage> read_club_info,
        CancellationToken cancellation_token)
    {
        if (source.Data is not { } data)
            return Reshape<McpGiftState, McpGiftSnapshot>(source, null, false);

        GiftClubInfoPage page = data.ClubInfoPage;
        bool truncated = page.NextOffset is not null;
        GiftDetailResult? detail_result = null;
        if (detail)
        {
            detail_result = ReadGiftDetails(
                page,
                read_club_info,
                cancellation_token);
        }

        return Reshape(
            source,
            new McpGiftSnapshot(
                data.Wrapping,
                page.DaysUntilNextGift,
                page.GiftsAvailable,
                data.State.LastClubSelected?.ProductCode,
                data.State.LastOpenedPresent,
                data.State.LatestNotification?.NumGifts,
                data.State.NewUserOffer?.StepCount,
                data.State.OfferGiftability,
                page.TotalOffers,
                page.Offers.Count,
                page.Offset,
                limit,
                truncated,
                [.. page.Offers.Select(CompactClubGiftOffer)],
                detail_result?.Metadata,
                detail_result?.Offers),
            truncated || detail_result?.Metadata.Truncated is true);
    }

    private static IReadOnlyList<ForumThread> ThreadsOf(
        ForumSnapshot snapshot,
        Id group_id) =>
        [
            .. snapshot.KnownThreads
                .Where(entry => entry.Key.GroupId == group_id)
                .Select(entry => entry.Value)
                .OrderByDescending(thread => thread.IsSticky)
                .ThenBy(thread => thread.LastMessageSecondsAgo)
                .ThenBy(thread => thread.ThreadId)
        ];

    private static ForumListCode ForumList(string list) =>
        list.Trim().ToLowerInvariant() switch
        {
            "" or "my" or "myforums" or "my_forums" => ForumListCode.MyForums,
            "active" => ForumListCode.Active,
            "popular" => ForumListCode.Popular,
            _ => throw new ArgumentException(
                $"Unknown forum list '{list}'. Use my, active or popular.",
                nameof(list))
        };

    private static McpForumSummary CompactForum(ForumSummary forum) =>
        new(
            forum.GroupId,
            forum.Name,
            forum.Description,
            forum.TotalThreads,
            forum.TotalMessages,
            forum.UnreadMessages,
            forum.LeaderboardScore,
            forum.LastMessageAuthorName,
            forum.LastMessageSecondsAgo);

    private static McpForumThread CompactForumThread(ForumThread thread) =>
        new(
            thread.ThreadId,
            thread.Header,
            thread.AuthorId,
            thread.AuthorName,
            thread.IsSticky,
            thread.IsLocked,
            thread.IsHidden,
            thread.State,
            thread.MessageCount,
            thread.UnreadMessageCount,
            thread.CreationSecondsAgo,
            thread.LastMessageAuthorName,
            thread.LastMessageSecondsAgo);

    private static McpQuest CompactQuest(QuestData quest) =>
        new(
            quest.Id,
            quest.CampaignCode,
            quest.ChainCode,
            quest.LocalizationCode,
            quest.Type,
            quest.IsAccepted,
            quest.IsCompleted,
            quest.IsEasy,
            quest.IsSeasonal,
            quest.CompletedSteps,
            quest.TotalSteps,
            quest.CompletedQuestsInCampaign,
            quest.QuestCountInCampaign,
            quest.RewardCurrencyAmount,
            quest.ActivityPointType,
            quest.CatalogPageName,
            quest.SeasonalSecondsLeft);

    private static McpClubGiftOffer CompactClubGiftOffer(GiftClubOfferView offer) =>
        new(
            offer.OfferId,
            offer.LocalizationId,
            offer.PriceInCredits,
            offer.PriceInActivityPoints,
            offer.ActivityPointType,
            offer.ClubLevel,
            offer.Giftable,
            offer.ProductCount,
            offer.PreviewImage);

    private static void ValidateGiftRefresh(
        GiftRefreshResult result,
        GiftClubInfoPage page,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(page);
        ValidateGiftClubPage(
            page,
            GiftClubInfoCollection.Offers,
            0,
            limit,
            null,
            null);
    }

    private static void ValidateGiftState(GiftStateView state, GiftClubInfoPage page)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Connected != page.Connected ||
            state.SessionGeneration != page.SessionGeneration ||
            state.OfferGiftability is null ||
            state.OfferGiftability.Count > 500 ||
            state.Connected && state.SessionGeneration <= 0)
        {
            throw new InvalidDataException("Gift state does not match the snapshot session.");
        }
    }

    private static GiftWrappingConfiguration? ReadGiftWrapping(
        GiftClubInfoPage club_page,
        GiftRefreshResult? refresh_result,
        Func<GiftWrappingPageRequest, CancellationToken, GiftWrappingPage> read_wrapping,
        CancellationToken cancellation_token)
    {
        GiftWrappingCollectionRead stuff_types = ReadGiftWrappingCollection(
            GiftWrappingCollection.StuffTypes,
            club_page,
            read_wrapping,
            cancellation_token);
        GiftWrappingCollectionRead box_types = ReadGiftWrappingCollection(
            GiftWrappingCollection.BoxTypes,
            club_page,
            read_wrapping,
            cancellation_token);
        GiftWrappingCollectionRead ribbon_types = ReadGiftWrappingCollection(
            GiftWrappingCollection.RibbonTypes,
            club_page,
            read_wrapping,
            cancellation_token);
        GiftWrappingCollectionRead default_stuff_types = ReadGiftWrappingCollection(
            GiftWrappingCollection.DefaultStuffTypes,
            club_page,
            read_wrapping,
            cancellation_token);
        GiftWrappingCollectionRead[] collections =
            [box_types, ribbon_types, default_stuff_types];
        foreach (GiftWrappingCollectionRead collection in collections)
        {
            if (collection.Loaded != stuff_types.Loaded ||
                collection.WrappingRevision != stuff_types.WrappingRevision ||
                collection.IsWrappingEnabled != stuff_types.IsWrappingEnabled ||
                collection.WrappingPrice != stuff_types.WrappingPrice)
            {
                throw new InvalidDataException(
                    "Gift wrapping collections do not belong to one snapshot.");
            }
        }
        if (refresh_result is not null &&
            (stuff_types.WrappingRevision != refresh_result.WrappingRevision ||
             !stuff_types.Loaded ||
             stuff_types.IsWrappingEnabled != refresh_result.Wrapping.IsWrappingEnabled ||
             stuff_types.WrappingPrice != refresh_result.Wrapping.WrappingPrice ||
             stuff_types.Values.Count != refresh_result.Wrapping.StuffTypeCount ||
             box_types.Values.Count != refresh_result.Wrapping.BoxTypeCount ||
             ribbon_types.Values.Count != refresh_result.Wrapping.RibbonTypeCount ||
             default_stuff_types.Values.Count != refresh_result.Wrapping.DefaultStuffTypeCount))
        {
            throw new InvalidDataException("Gift refresh returned inconsistent wrapping state.");
        }
        if (!stuff_types.Loaded)
            return null;
        return new GiftWrappingConfiguration(
            stuff_types.IsWrappingEnabled!.Value,
            stuff_types.WrappingPrice!.Value,
            stuff_types.Values,
            box_types.Values,
            ribbon_types.Values,
            default_stuff_types.Values);
    }

    private static GiftWrappingCollectionRead ReadGiftWrappingCollection(
        GiftWrappingCollection collection,
        GiftClubInfoPage club_page,
        Func<GiftWrappingPageRequest, CancellationToken, GiftWrappingPage> read_wrapping,
        CancellationToken cancellation_token)
    {
        GiftWrappingPage? first = null;
        var values = new List<int>();
        int offset = 0;
        while (true)
        {
            GiftWrappingPage page = read_wrapping(
                new GiftWrappingPageRequest(
                    collection,
                    offset,
                    GiftApplicationPageLimit,
                    club_page.SnapshotRevision),
                cancellation_token);
            ValidateGiftWrappingPage(
                page,
                collection,
                offset,
                GiftApplicationPageLimit,
                club_page,
                first);
            first ??= page;
            values.AddRange(page.Values);
            if (page.NextOffset is not int next_offset)
                break;
            offset = next_offset;
        }
        if (first is null || values.Count != first.Total)
            throw new InvalidDataException("Gift wrapping pagination returned an incomplete result.");
        return new GiftWrappingCollectionRead(
            first.WrappingRevision,
            first.Loaded,
            first.IsWrappingEnabled,
            first.WrappingPrice,
            Array.AsReadOnly(values.ToArray()));
    }

    private static void ValidateGiftWrappingPage(
        GiftWrappingPage page,
        GiftWrappingCollection collection,
        int offset,
        int limit,
        GiftClubInfoPage club_page,
        GiftWrappingPage? first)
    {
        ArgumentNullException.ThrowIfNull(page);
        int expected_count = PageCount(page.Total, offset, limit);
        int consumed = checked(offset + page.Values.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (page.Values is null ||
            page.SnapshotRevision != club_page.SnapshotRevision ||
            page.Connected != club_page.Connected ||
            page.SessionGeneration != club_page.SessionGeneration ||
            page.WrappingRevision < 0 ||
            page.Collection != collection ||
            page.Total is < 0 or > GiftMaximumCollectionCount ||
            page.Offset != offset ||
            page.Values.Count != expected_count ||
            page.NextOffset != expected_next ||
            page.Loaded && (page.IsWrappingEnabled is null || page.WrappingPrice is null) ||
            !page.Loaded &&
                (page.IsWrappingEnabled is not null || page.WrappingPrice is not null || page.Total != 0) ||
            first is not null &&
                (page.WrappingRevision != first.WrappingRevision ||
                 page.Loaded != first.Loaded ||
                 page.IsWrappingEnabled != first.IsWrappingEnabled ||
                 page.WrappingPrice != first.WrappingPrice ||
                 page.Total != first.Total))
        {
            throw new InvalidDataException("Gift wrapping returned an invalid snapshot page.");
        }
    }

    private static void ValidateGiftClubPage(
        GiftClubInfoPage page,
        GiftClubInfoCollection collection,
        int offset,
        int limit,
        long? snapshot_revision,
        GiftClubInfoPage? source)
    {
        ArgumentNullException.ThrowIfNull(page);
        int collection_total = collection switch
        {
            GiftClubInfoCollection.Offers => page.TotalOffers,
            GiftClubInfoCollection.Eligibility => page.TotalEligibility,
            GiftClubInfoCollection.Products => page.TotalProducts,
            _ => throw new ArgumentOutOfRangeException(nameof(collection))
        };
        int returned = collection switch
        {
            GiftClubInfoCollection.Offers => page.Offers?.Count ?? -1,
            GiftClubInfoCollection.Eligibility => page.Eligibility?.Count ?? -1,
            GiftClubInfoCollection.Products => page.Products?.Count ?? -1,
            _ => throw new ArgumentOutOfRangeException(nameof(collection))
        };
        int expected_count = PageCount(collection_total, offset, limit);
        int consumed = checked(offset + Math.Max(returned, 0));
        int? expected_next = consumed < collection_total ? consumed : null;
        if (page.SnapshotRevision <= 0 ||
            page.ClubInfoRevision < 0 ||
            page.Collection != collection ||
            page.Offset != offset ||
            page.Total != collection_total ||
            returned != expected_count ||
            page.NextOffset != expected_next ||
            page.TotalOffers is < 0 or > GiftMaximumOffers ||
            page.TotalEligibility is < 0 or > GiftMaximumCollectionCount ||
            page.TotalProducts is < 0 or > GiftMaximumCollectionCount ||
            page.Connected && page.SessionGeneration <= 0 ||
            page.Loaded && (page.DaysUntilNextGift is null || page.GiftsAvailable is null) ||
            !page.Loaded &&
                (page.DaysUntilNextGift is not null ||
                 page.GiftsAvailable is not null ||
                 page.TotalOffers != 0 ||
                 page.TotalEligibility != 0 ||
                 page.TotalProducts != 0) ||
            snapshot_revision is long revision && page.SnapshotRevision != revision ||
            source is not null && !SameGiftClubSnapshot(page, source))
        {
            throw new InvalidDataException("Club gifts returned an invalid snapshot page.");
        }
        ValidateGiftClubPageShape(page, collection);
        if (collection is GiftClubInfoCollection.Offers)
        {
            IReadOnlyList<GiftClubOfferView> offers = page.Offers ??
                throw new InvalidDataException("Club gifts returned no offer collection.");
            for (int index = 0; index < offers.Count; index++)
            {
                GiftClubOfferView offer = offers[index];
                if (offer is null ||
                    offer.LocalizationId is null ||
                    offer.PreviewImage is null ||
                    offer.OfferOrdinal != checked(offset + index) ||
                    offer.ProductCount is < 0 or > GiftMaximumCollectionCount)
                {
                    throw new InvalidDataException("Club gifts returned an invalid offer page.");
                }
            }
        }
    }

    private static void ValidateGiftClubPageShape(
        GiftClubInfoPage page,
        GiftClubInfoCollection collection)
    {
        if (page.Offers is null ||
            page.Eligibility is null ||
            page.Products is null ||
            collection is not GiftClubInfoCollection.Offers && page.Offers.Count != 0 ||
            collection is not GiftClubInfoCollection.Eligibility && page.Eligibility.Count != 0 ||
            collection is not GiftClubInfoCollection.Products && page.Products.Count != 0)
        {
            throw new InvalidDataException("Club gifts returned an invalid collection shape.");
        }
    }

    private static bool SameGiftClubSnapshot(
        GiftClubInfoPage page,
        GiftClubInfoPage source) =>
        page.Connected == source.Connected &&
        page.SessionGeneration == source.SessionGeneration &&
        page.ClubInfoRevision == source.ClubInfoRevision &&
        page.SnapshotRevision == source.SnapshotRevision &&
        page.Loaded == source.Loaded &&
        page.DaysUntilNextGift == source.DaysUntilNextGift &&
        page.GiftsAvailable == source.GiftsAvailable &&
        page.TotalOffers == source.TotalOffers &&
        page.TotalEligibility == source.TotalEligibility &&
        page.TotalProducts == source.TotalProducts;

    private static int PageCount(int total, int offset, int limit) =>
        offset >= total ? 0 : Math.Min(limit, total - offset);

    private static GiftDetailResult ReadGiftDetails(
        GiftClubInfoPage page,
        Func<GiftClubInfoPageRequest, CancellationToken, GiftClubInfoPage> read_club_info,
        CancellationToken cancellation_token)
    {
        if (page.Offers.Count == 0)
        {
            return new GiftDetailResult(
                new McpGiftDetailMetadata(0, 0, GiftDetailEntryLimit, false),
                Array.AsReadOnly(Array.Empty<CatalogPageOffer>()));
        }
        GiftNestedOffsets offsets = ReadGiftNestedOffsets(
            page,
            read_club_info,
            cancellation_token);
        int count = page.Offers.Count;
        var product_limits = new int[count];
        int remaining = GiftDetailEntryLimit;
        int total = 0;
        int selected_products = 0;
        for (int index = 0; index < count; index++)
        {
            GiftClubOfferView offer = page.Offers[index];
            total = checked(total + offer.ProductCount);
            product_limits[index] = ReserveGiftDetailEntries(offer.ProductCount, ref remaining);
            selected_products = checked(selected_products + product_limits[index]);
        }
        GiftClubInfoPage? product_page = ReadGiftNestedPage(
            GiftClubInfoCollection.Products,
            offsets.Products,
            selected_products,
            page,
            read_club_info,
            cancellation_token);
        IReadOnlyList<CatalogProduct>[] products = CreateGiftDetailLists<CatalogProduct>(count);
        FillGiftProducts(page, product_page, product_limits, products);
        var offers = new CatalogPageOffer[count];
        for (int index = 0; index < count; index++)
        {
            GiftClubOfferView offer = page.Offers[index];
            offers[index] = new CatalogPageOffer(
                offer.OfferId,
                offer.LocalizationId,
                offer.IsRent,
                offer.PriceInCredits,
                offer.PriceInActivityPoints,
                offer.ActivityPointType,
                offer.PriceInSilver,
                offer.Giftable,
                products[index],
                offer.ClubLevel,
                offer.BundlePurchaseAllowed,
                offer.IsPet,
                offer.PreviewImage);
        }
        return new GiftDetailResult(
            new McpGiftDetailMetadata(
                total,
                selected_products,
                GiftDetailEntryLimit,
                selected_products < total),
            Array.AsReadOnly(offers));
    }

    private static GiftNestedOffsets ReadGiftNestedOffsets(
        GiftClubInfoPage page,
        Func<GiftClubInfoPageRequest, CancellationToken, GiftClubInfoPage> read_club_info,
        CancellationToken cancellation_token)
    {
        int products = 0;
        int offset = 0;
        while (offset < page.Offset)
        {
            int limit = Math.Min(GiftApplicationPageLimit, page.Offset - offset);
            GiftClubInfoPage prefix = read_club_info(
                new GiftClubInfoPageRequest(
                    GiftClubInfoCollection.Offers,
                    offset,
                    limit,
                    page.SnapshotRevision),
                cancellation_token);
            ValidateGiftClubPage(
                prefix,
                GiftClubInfoCollection.Offers,
                offset,
                limit,
                page.SnapshotRevision,
                page);
            foreach (GiftClubOfferView offer in prefix.Offers)
            {
                products = checked(products + offer.ProductCount);
            }
            offset = checked(offset + prefix.Offers.Count);
        }
        return new GiftNestedOffsets(products);
    }

    private static GiftClubInfoPage? ReadGiftNestedPage(
        GiftClubInfoCollection collection,
        int offset,
        int count,
        GiftClubInfoPage source,
        Func<GiftClubInfoPageRequest, CancellationToken, GiftClubInfoPage> read_club_info,
        CancellationToken cancellation_token)
    {
        if (count == 0)
            return null;
        GiftClubInfoPage page = read_club_info(
            new GiftClubInfoPageRequest(
                collection,
                offset,
                count,
                source.SnapshotRevision),
            cancellation_token);
        ValidateGiftClubPage(
            page,
            collection,
            offset,
            count,
            source.SnapshotRevision,
            source);
        return page;
    }

    private static int ReserveGiftDetailEntries(int requested, ref int remaining)
    {
        int reserved = Math.Min(requested, remaining);
        remaining -= reserved;
        return reserved;
    }

    private static IReadOnlyList<T>[] CreateGiftDetailLists<T>(int count)
    {
        var values = new IReadOnlyList<T>[count];
        for (int index = 0; index < values.Length; index++)
            values[index] = Array.AsReadOnly(Array.Empty<T>());
        return values;
    }

    private static void FillGiftProducts(
        GiftClubInfoPage offers,
        GiftClubInfoPage? page,
        IReadOnlyList<int> limits,
        IReadOnlyList<CatalogProduct>[] output)
    {
        int source_index = 0;
        for (int offer_index = 0; offer_index < offers.Offers.Count; offer_index++)
        {
            var values = new CatalogProduct[limits[offer_index]];
            for (int product_index = 0; product_index < values.Length; product_index++)
            {
                GiftClubProductView value = page?.Products[source_index++] ??
                    throw new InvalidDataException("Club gift product details are incomplete.");
                if (value is null ||
                    value.Product is null ||
                    value.OfferOrdinal != offers.Offers[offer_index].OfferOrdinal ||
                    value.ProductOrdinal != product_index)
                {
                    throw new InvalidDataException("Club gift product details are out of order.");
                }
                values[product_index] = value.Product;
            }
            output[offer_index] = Array.AsReadOnly(values);
        }
        if (source_index != (page?.Products.Count ?? 0))
            throw new InvalidDataException("Club gift product details contain extra entries.");
    }

    private sealed record GiftWrappingCollectionRead(
        long WrappingRevision,
        bool Loaded,
        bool? IsWrappingEnabled,
        int? WrappingPrice,
        IReadOnlyList<int> Values);

    private sealed record GiftNestedOffsets(
        int Products);

    private sealed record GiftDetailResult(
        McpGiftDetailMetadata Metadata,
        IReadOnlyList<CatalogPageOffer> Offers);

    private sealed record BadgeSnapshotRead(
        OwnedBadgePage Page,
        IReadOnlyList<OwnedBadgeSnapshot> Badges);

    private sealed record AchievementSnapshotRead(
        AchievementPage Page,
        IReadOnlyList<AchievementSnapshot> Achievements);

    private static int NormalizeLimit(int value, int maximum)
    {
        if (value < 1 || value > maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        return value;
    }

    private static int NormalizeOffset(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        return value;
    }

    private static long? NormalizeSnapshotRevision(long? value, int offset)
    {
        if (value is <= 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (offset > 0 && value is null)
            throw new ArgumentException("Continuation pages require a snapshot revision.", nameof(value));
        return value;
    }

    private static void ValidateFurniPage(
        InventoryFurniPage page,
        int offset,
        int limit,
        long? snapshot_revision)
    {
        int consumed = checked(offset + page.Items.Count);
        int? expected_next = consumed < page.Matched ? consumed : null;
        if (page.SnapshotRevision <= 0 ||
            page.Offset != offset ||
            page.Total < 0 ||
            page.Matched < 0 ||
            page.Matched > page.Total ||
            page.Items.Count > limit ||
            consumed > Math.Max(offset, page.Matched) ||
            page.NextOffset != expected_next ||
            snapshot_revision is long revision && page.SnapshotRevision != revision)
        {
            throw new InvalidOperationException("The furni inventory returned an invalid snapshot page.");
        }
    }

    private static void ValidatePetPage(
        InventoryPetPage page,
        int offset,
        int limit,
        long? snapshot_revision)
    {
        int consumed = checked(offset + page.Pets.Count);
        int? expected_next = consumed < page.Matched ? consumed : null;
        if (page.SnapshotRevision <= 0 ||
            page.Offset != offset ||
            page.Total < 0 ||
            page.Matched < 0 ||
            page.Matched > page.Total ||
            page.Pets.Count > limit ||
            consumed > Math.Max(offset, page.Matched) ||
            page.NextOffset != expected_next ||
            snapshot_revision is long revision && page.SnapshotRevision != revision)
        {
            throw new InvalidOperationException("The pet inventory returned an invalid snapshot page.");
        }
    }

    private static BadgeSnapshotRead ReadBadgeSnapshot(
        Func<OwnedBadgePageRequest, CancellationToken, OwnedBadgePage> read_page,
        OwnedBadgePage? first_page,
        long? snapshot_revision,
        CancellationToken cancellation_token)
    {
        OwnedBadgePage first = first_page ?? read_page(
            new OwnedBadgePageRequest(Limit: 500, SnapshotRevision: snapshot_revision),
            cancellation_token);
        ValidateBadgePage(first, 0, 500, snapshot_revision, null);
        var badges = new List<OwnedBadgeSnapshot>(first.Total);
        badges.AddRange(first.Badges);
        OwnedBadgePage current = first;
        while (current.NextOffset is int offset)
        {
            cancellation_token.ThrowIfCancellationRequested();
            current = read_page(
                new OwnedBadgePageRequest(offset, 500, first.SnapshotRevision),
                cancellation_token);
            ValidateBadgePage(current, offset, 500, first.SnapshotRevision, first);
            badges.AddRange(current.Badges);
        }
        if (badges.Count != first.Total)
            throw new InvalidDataException("The badge application returned an incomplete snapshot.");

        OwnedBadgeSnapshot[] sorted = badges
            .OrderBy(badge => badge.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(badge => (long)badge.Id)
            .ToArray();
        return new BadgeSnapshotRead(first, Array.AsReadOnly(sorted));
    }

    private static AchievementSnapshotRead ReadAchievementSnapshot(
        Func<AchievementPageRequest, CancellationToken, AchievementPage> read_page,
        AchievementPage? first_page,
        long? snapshot_revision,
        CancellationToken cancellation_token)
    {
        AchievementPage first = first_page ?? read_page(
            new AchievementPageRequest(Limit: 500, SnapshotRevision: snapshot_revision),
            cancellation_token);
        ValidateAchievementPage(first, 0, 500, snapshot_revision, null);
        var achievements = new List<AchievementSnapshot>(first.Total);
        AddAchievementSnapshots(first, achievements);
        AchievementPage current = first;
        while (current.NextOffset is int offset)
        {
            cancellation_token.ThrowIfCancellationRequested();
            current = read_page(
                new AchievementPageRequest(offset, 500, first.SnapshotRevision),
                cancellation_token);
            ValidateAchievementPage(current, offset, 500, first.SnapshotRevision, first);
            AddAchievementSnapshots(current, achievements);
        }
        if (achievements.Count != first.Total)
            throw new InvalidDataException("The achievement application returned an incomplete snapshot.");

        AchievementSnapshot[] sorted = achievements
            .OrderBy(achievement => achievement.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(achievement => achievement.Subcategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(achievement => achievement.Id)
            .ToArray();
        return new AchievementSnapshotRead(first, Array.AsReadOnly(sorted));
    }

    private static void AddAchievementSnapshots(
        AchievementPage page,
        List<AchievementSnapshot> achievements)
    {
        achievements.AddRange(page.Achievements.Select(achievement => new AchievementSnapshot(
            achievement.Id,
            achievement.Level,
            achievement.BadgeCode,
            achievement.BaseProgress,
            achievement.MaxProgress,
            achievement.LevelRewardPoints,
            achievement.LevelRewardPointType,
            achievement.CurrentProgress,
            achievement.IsComplete,
            achievement.Category,
            achievement.Subcategory,
            achievement.MaxLevel,
            achievement.DisplayMethod,
            achievement.State)));
    }

    private static void ValidateBadgePage(
        OwnedBadgePage page,
        int offset,
        int limit,
        long? snapshot_revision,
        OwnedBadgePage? first)
    {
        int consumed = checked(offset + page.Badges.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (page.SnapshotRevision <= 0 ||
            page.Offset != offset ||
            page.Total < 0 ||
            page.Inventory.OwnedCount != page.Total ||
            page.Badges.Count > limit ||
            consumed > page.Total ||
            consumed < page.Total && page.Badges.Count == 0 ||
            page.NextOffset != expected_next ||
            snapshot_revision is long revision && page.SnapshotRevision != revision ||
            first is not null &&
            (page.Connected != first.Connected ||
             page.SessionGeneration != first.SessionGeneration ||
             page.StateRevision != first.StateRevision ||
             page.InventoryRevision != first.InventoryRevision ||
             page.BaselineRevision != first.BaselineRevision ||
             page.Total != first.Total ||
             page.Inventory != first.Inventory))
        {
            throw new InvalidDataException("The badge application returned an invalid snapshot page.");
        }
    }

    private static void ValidateAchievementPage(
        AchievementPage page,
        int offset,
        int limit,
        long? snapshot_revision,
        AchievementPage? first)
    {
        int consumed = checked(offset + page.Achievements.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (page.SnapshotRevision <= 0 ||
            page.Offset != offset ||
            page.Total < 0 ||
            page.Completed < 0 ||
            page.Completed > page.Total ||
            page.Achievements.Count > limit ||
            consumed > page.Total ||
            consumed < page.Total && page.Achievements.Count == 0 ||
            page.NextOffset != expected_next ||
            snapshot_revision is long revision && page.SnapshotRevision != revision ||
            first is not null &&
            (page.Connected != first.Connected ||
             page.SessionGeneration != first.SessionGeneration ||
             page.StateRevision != first.StateRevision ||
             page.ListRevision != first.ListRevision ||
             page.BaselineRevision != first.BaselineRevision ||
             page.NewCodesRevision != first.NewCodesRevision ||
             page.Loaded != first.Loaded ||
             page.DefaultCategory != first.DefaultCategory ||
             page.Total != first.Total ||
             page.Completed != first.Completed))
        {
            throw new InvalidDataException("The achievement application returned an invalid snapshot page.");
        }
    }

    private static void ValidateBadgeRefresh(BadgeRefreshResult refreshed)
    {
        ValidateBadgePage(refreshed.FirstPage, 0, 500, refreshed.SnapshotRevision, null);
        if (refreshed.SnapshotRevision <= 0 ||
            !refreshed.FirstPage.Connected ||
            refreshed.FirstPage.SessionGeneration != refreshed.SessionGeneration ||
            refreshed.FirstPage.StateRevision != refreshed.StateRevision ||
            refreshed.FirstPage.InventoryRevision != refreshed.InventoryRevision ||
            refreshed.FirstPage.BaselineRevision != refreshed.BaselineRevision ||
            !refreshed.FirstPage.Inventory.Loaded ||
            refreshed.FirstPage.Inventory.Loading ||
            refreshed.FirstPage.Inventory.Stale ||
            refreshed.FirstPage.Inventory.RecoveryPending)
        {
            throw new InvalidDataException("The badge application returned an invalid refresh result.");
        }
    }

    private static void ValidateAchievementRefresh(AchievementRefreshResult refreshed)
    {
        ValidateAchievementPage(
            refreshed.FirstPage,
            0,
            500,
            refreshed.SnapshotRevision,
            null);
        if (refreshed.SnapshotRevision <= 0 ||
            !refreshed.FirstPage.Connected ||
            refreshed.FirstPage.SessionGeneration != refreshed.SessionGeneration ||
            refreshed.FirstPage.StateRevision != refreshed.StateRevision ||
            refreshed.FirstPage.ListRevision != refreshed.ListRevision ||
            refreshed.FirstPage.BaselineRevision != refreshed.BaselineRevision ||
            !refreshed.FirstPage.Loaded)
        {
            throw new InvalidDataException("The achievement application returned an invalid refresh result.");
        }
    }

    private static QueryEnvelope<BadgeInventorySnapshot> BadgeInventoryEnvelope(
        OwnedBadgePage page,
        IReadOnlyList<OwnedBadgeSnapshot> badges,
        int offset,
        int limit)
    {
        OwnedBadgeSnapshot[] items = [.. badges.Skip(offset).Take(limit)];
        bool truncated = (long)offset + items.Length < page.Total;
        BadgeInventorySummary inventory = page.Inventory;
        var snapshot = new BadgeInventorySnapshot(
            inventory.Loading,
            inventory.Stale,
            inventory.LoadGeneration,
            inventory.ExpectedFragments,
            inventory.ReceivedFragments,
            page.Total,
            items.Length,
            limit,
            truncated,
            Array.AsReadOnly(items));
        return QueryResults.Success(
            "badge_inventory",
            snapshot,
            page.Connected && inventory.Loaded,
            inventory.Loaded,
            inventory.Stale || !page.Connected && page.Total > 0,
            truncated,
            inventory.Loaded ? [] : ["badgeInventory"]);
    }

    private static QueryEnvelope<AchievementCollectionSnapshot> AchievementEnvelope(
        AchievementPage page,
        IReadOnlyList<AchievementSnapshot> achievements,
        int offset,
        int limit)
    {
        AchievementSnapshot[] items = [.. achievements.Skip(offset).Take(limit)];
        bool truncated = (long)offset + items.Length < page.Total;
        var snapshot = new AchievementCollectionSnapshot(
            page.DefaultCategory,
            page.Total,
            page.Completed,
            items.Length,
            limit,
            truncated,
            Array.AsReadOnly(items));
        return QueryResults.Success(
            "achievements",
            snapshot,
            page.Connected && page.Loaded,
            page.Loaded,
            !page.Connected && page.Total > 0,
            truncated,
            page.Loaded ? [] : ["achievements"]);
    }

    private static int? BadgeNextOffset(int total, int offset, int returned) =>
        (long)offset + returned < total ? checked(offset + returned) : null;

    private static int? AchievementNextOffset(int total, int offset, int returned) =>
        (long)offset + returned < total ? checked(offset + returned) : null;

    private static string WithNextOffset(string envelope, int? next_offset)
    {
        if (next_offset is not { } value)
            return envelope;

        JsonNode? parsed = JsonNode.Parse(envelope);
        if (parsed is not JsonObject document || document["metadata"] is not JsonObject metadata)
            return envelope;

        metadata["nextOffset"] = value;
        return document.ToJsonString(JsonOptions);
    }

    private static string WithPageLease(
        string envelope,
        long snapshot_revision,
        int? next_offset)
    {
        JsonNode? parsed = JsonNode.Parse(envelope);
        if (parsed is not JsonObject document || document["metadata"] is not JsonObject metadata)
            return envelope;

        metadata["snapshotRevision"] = snapshot_revision;
        if (next_offset is int value)
            metadata["nextOffset"] = value;
        return document.ToJsonString(JsonOptions);
    }

    private static int? NextOffset<T>(
        QueryEnvelope<T> source,
        int offset,
        int limit,
        Func<T, int> available) =>
        source.Data is { } data && offset + limit < available(data)
            ? offset + limit
            : null;

    private static QueryEnvelope<TTarget> Reshape<TSource, TTarget>(
        QueryEnvelope<TSource> source,
        TTarget? data,
        bool truncated) =>
        new(
            source.Query,
            source.Metadata with { Truncated = source.Metadata.Truncated || truncated },
            data,
            source.Error);

    private static QueryEnvelope<FurniCollectionSnapshot> SkipFurni(
        QueryEnvelope<FurniCollectionSnapshot> source,
        int offset) =>
        offset <= 0 || source.Data is not { } data
            ? source
            : source with
            {
                Data = data with
                {
                    FloorItems = [.. data.FloorItems.Skip(offset)],
                    WallItems = [.. data.WallItems.Skip(offset)]
                }
            };

    private QueryEnvelope<FriendCollectionSnapshot> FriendEnvelope(FriendListPage page) =>
        QueryResults.Success(
            "friends",
            new FriendCollectionSnapshot(
                page.Total,
                page.Online,
                page.UserLimit,
                page.NormalLimit,
                page.ExtendedLimit,
                page.Categories,
                page.Friends),
            extension.IsConnected && page.Loaded,
            page.Loaded,
            page.Stale,
            page.NextOffset is not null,
            page.Loaded ? [] : ["friends"]);

    private static int? FriendNextOffset(
        QueryEnvelope<FriendCollectionSnapshot> source,
        int offset) => source.Data is { } data && offset + data.Friends.Count < data.Total
            ? offset + data.Friends.Count
            : null;

    private QueryEnvelope<InventorySnapshot> InventoryEnvelope(
        InventoryFurniPage page,
        int limit)
    {
        FurniData? definitions = game.GameData.Furni;
        bool game_data_loaded = game.GameData.IsLoaded;
        InventoryItemSnapshot[] items =
            [.. page.Items.Select(item => SnapshotFactory.WithDefinition(item, definitions))];
        var pending = new List<string>();
        if (!page.Loaded)
            pending.Add("inventory");
        if (!game_data_loaded)
            pending.Add("definitions");
        bool truncated = page.NextOffset is not null;
        var snapshot = new InventorySnapshot(
            definitions is not null,
            page.Loading,
            page.Stale,
            page.LoadGeneration,
            page.ExpectedFragments,
            page.ReceivedFragments,
            page.Total,
            items.Length,
            limit,
            truncated,
            Array.AsReadOnly(items));
        return QueryResults.Success(
            "inventory",
            snapshot,
            page.Connected && page.Loaded,
            page.Connected && page.Loaded && pending.Count == 0,
            page.Stale || !page.Connected && page.Total > 0,
            truncated,
            pending);
    }

    private void RequireInventorySession(object session, long generation)
    {
        InventoryStateView state = application_runtime.Invoke<InventoryStateRequest, InventoryStateView>(
            ApplicationMemberIds.InventoryState,
            new InventoryStateRequest());
        if (!state.Connected ||
            state.SessionGeneration != generation ||
            !ReferenceEquals(extension.Session, session))
        {
            throw new InvalidOperationException("The hotel session changed while pet information was being read.");
        }
    }

    private static QueryEnvelope<PetInventorySnapshot> PetInventoryEnvelope(
        InventoryPetPage page,
        int limit)
    {
        bool truncated = page.NextOffset is not null;
        var snapshot = new PetInventorySnapshot(
            page.Loading,
            page.Stale,
            page.LoadGeneration,
            page.ExpectedFragments,
            page.ReceivedFragments,
            page.Total,
            page.Pets.Count,
            limit,
            truncated,
            page.Pets);
        return QueryResults.Success(
            "pet_inventory",
            snapshot,
            page.Connected && page.Loaded,
            page.Loaded,
            page.Stale || !page.Connected && page.Total > 0,
            truncated,
            page.Loaded ? [] : ["petInventory"]);
    }

    private static QueryEnvelope<HeightmapSnapshot?> SkipTiles(
        QueryEnvelope<HeightmapSnapshot?> source,
        int offset) =>
        offset <= 0 || source.Data is not { } data
            ? source
            : source with { Data = data with { Tiles = [.. data.Tiles.Skip(offset)] } };

    private static QueryEnvelope<McpConnectionSnapshot> CompactConnection(
        QueryEnvelope<ConnectionSnapshot> source) =>
        Reshape(
            source,
            source.Data is not { } data
                ? null
                : new McpConnectionSnapshot(
                    data.InterceptorConnected,
                    data.HotelConnected,
                    data.MessageCatalogLoaded,
                    data.WireProfileAnalyzed,
                    data.WireProfileExact,
                    data.MissingWireCapabilities.Count),
            false);

    private static QueryEnvelope<McpRoomSnapshot> CompactRoom(QueryEnvelope<RoomSnapshot> source) =>
        Reshape(
            source,
            source.Data is not { } data
                ? null
                : new McpRoomSnapshot(
                    data.IsInRoom,
                    data.IsReady,
                    data.State,
                    data.Generation,
                    data.Id,
                    data.Access,
                    data.RoomType,
                    data.IsOwner,
                    data.HasRights,
                    data.Authority,
                    data.Data,
                    data.Details,
                    data.Environment,
                    data.Content,
                    data.AvatarCount,
                    data.FloorItemCount,
                    data.WallItemCount,
                    data.ControllerCount,
                    data.FloorPlan is not { } plan
                        ? null
                        : new McpFloorPlanSummary(
                            plan.UseLegacyScale,
                            plan.WallHeight,
                            plan.Width,
                            plan.Length,
                            plan.Scale,
                            plan.Tiles.Count,
                            plan.Map.Length,
                            plan.HiddenAreas,
                            plan.HasCameraData,
                            plan.CameraX,
                            plan.CameraY,
                            plan.CameraZ),
                    data.Heightmap),
            false);

    private static IReadOnlyDictionary<string, int> AvatarCounts(IReadOnlyList<AvatarSnapshot> avatars) =>
        avatars
            .GroupBy(avatar => avatar.Type, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static McpAvatar CompactAvatar(AvatarSnapshot avatar) =>
        new(
            avatar.Type,
            avatar.Id,
            avatar.Index,
            avatar.Name,
            avatar.Position,
            avatar.Direction,
            avatar.CurrentStatus?.Stance,
            avatar.CurrentStatus?.IsController ?? false,
            avatar.Dance,
            avatar.Effect,
            avatar.HandItem,
            avatar.IsIdle,
            avatar.IsTyping,
            avatar.User?.Gender,
            avatar.Pet?.OwnerName ?? avatar.Bot?.OwnerName);

}

internal sealed record ScriptExecutionSnapshot(
    ScriptRunState State,
    bool Faulted,
    double RuntimeMs,
    string Output,
    ImmutableArray<ScriptExecutionError> Errors);

internal sealed record McpConnectionSnapshot(
    bool InterceptorConnected,
    bool HotelConnected,
    bool MessageCatalogLoaded,
    bool WireProfileAnalyzed,
    bool WireProfileExact,
    int MissingWireCapabilityCount);

internal sealed record McpFloorPlanSummary(
    bool UseLegacyScale,
    int WallHeight,
    int Width,
    int Length,
    int Scale,
    int TileCount,
    int MapLength,
    IReadOnlyList<HiddenAreaSnapshot> HiddenAreas,
    bool HasCameraData,
    int? CameraX,
    int? CameraY,
    float? CameraZ);

internal sealed record McpRoomSnapshot(
    bool IsInRoom,
    bool IsReady,
    string State,
    long Generation,
    Id? Id,
    RoomAccessSnapshot Access,
    string RoomType,
    bool IsOwner,
    bool HasRights,
    RoomAuthoritySnapshot Authority,
    RoomDataSnapshot? Data,
    RoomResultDetailsSnapshot? Details,
    RoomEnvironmentSnapshot Environment,
    RoomContentStateSnapshot Content,
    int AvatarCount,
    int FloorItemCount,
    int WallItemCount,
    int ControllerCount,
    McpFloorPlanSummary? FloorPlan,
    HeightmapSummarySnapshot? Heightmap);

internal sealed record McpAvatar(
    string Type,
    Id Id,
    int Index,
    string Name,
    PositionSnapshot Position,
    int Direction,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Stance,
    bool IsController,
    int Dance,
    int Effect,
    int HandItem,
    bool IsIdle,
    bool IsTyping,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Gender,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? OwnerName);

internal sealed record McpAvatarState(
    AvatarSnapshot Avatar,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EffectName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? HandItemName);

internal sealed record McpAvatarCollectionSnapshot(
    Id? RoomId,
    long Generation,
    int Total,
    int Returned,
    int Offset,
    int MaxItems,
    bool Truncated,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<McpAvatar> Avatars);

internal sealed record McpAvatarDetailsSnapshot(
    Id? RoomId,
    long Generation,
    int Total,
    int Returned,
    int Offset,
    int MaxItems,
    bool Truncated,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<AvatarSnapshot> Avatars);

internal sealed record McpControllerCollectionSnapshot(
    Id? RoomId,
    long Generation,
    bool IsOwner,
    int Total,
    int Returned,
    int Offset,
    int MaxItems,
    bool Truncated,
    IReadOnlyList<ControllerSnapshot> Controllers);

internal sealed record McpForumSummary(
    Id GroupId,
    string Name,
    string Description,
    int TotalThreads,
    int TotalMessages,
    int UnreadMessages,
    int LeaderboardScore,
    string LastMessageAuthorName,
    int LastMessageSecondsAgo);

internal sealed record McpForumCollectionSnapshot(
    int? UnreadForumsCount,
    int Total,
    int Returned,
    int Offset,
    int MaxItems,
    bool Truncated,
    int CachedForumDetails,
    int CachedThreads,
    int CachedMessages,
    IReadOnlyList<McpForumSummary> Forums,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ForumDetails>? Details);

internal sealed record McpForumThread(
    Id ThreadId,
    string Header,
    Id AuthorId,
    string AuthorName,
    bool IsSticky,
    bool IsLocked,
    bool IsHidden,
    byte State,
    int MessageCount,
    int UnreadMessageCount,
    int CreationSecondsAgo,
    string LastMessageAuthorName,
    int LastMessageSecondsAgo);

internal sealed record McpForumThreadCollectionSnapshot(
    Id GroupId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ForumName,
    int Total,
    int Returned,
    int Offset,
    int MaxItems,
    bool Truncated,
    int CachedMessages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ForumPermissions? Permissions,
    IReadOnlyList<McpForumThread> Threads,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ForumThread>? Details);

internal sealed record McpQuest(
    int Id,
    string CampaignCode,
    string ChainCode,
    string LocalizationCode,
    string Type,
    bool IsAccepted,
    bool IsCompleted,
    bool IsEasy,
    bool IsSeasonal,
    int CompletedSteps,
    int TotalSteps,
    int CompletedQuestsInCampaign,
    int QuestCountInCampaign,
    int RewardCurrencyAmount,
    int ActivityPointType,
    string CatalogPageName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? SeasonalSecondsLeft);

internal sealed record McpQuestCollectionSnapshot(
    int Total,
    int Returned,
    int Offset,
    int MaxItems,
    bool Truncated,
    int AvailableCount,
    int SeasonalCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    McpQuest? Current,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    QuestDaily? Daily,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    QuestCompleted? LastCompleted,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    QuestCancelled? LastCancelled,
    IReadOnlyList<McpQuest> Quests,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<QuestData>? Details);

internal sealed record McpCraftingSnapshot(
    int Total,
    int Returned,
    int Offset,
    int MaxItems,
    bool Truncated,
    IReadOnlyList<string> UsableInventoryFurnitureClasses,
    IReadOnlyList<CraftingIngredient> CurrentRecipeIngredients,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CraftingResult? LastResult,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CraftingRecipesAvailable? AvailableRecipes,
    IReadOnlyList<CraftingProduct> Products,
    McpCraftingCollectionMetadata UsableInventoryFurnitureClassesMetadata,
    McpCraftingCollectionMetadata CurrentRecipeIngredientsMetadata);

internal sealed record McpCraftingCollectionMetadata(
    int Total,
    int Returned,
    int MaxItems,
    bool Truncated);

internal sealed record McpSubscriptionState(
    IReadOnlyList<ScrSendUserInfo> Products,
    ScrSendKickbackInfo? Kickback,
    BuildersClubFurniCount? BuildersClubFurni,
    BuildersClubMembershipStatus? BuildersClubMembership);

internal sealed record McpSubscriptionSnapshot(
    int Total,
    int Returned,
    int Offset,
    int MaxItems,
    bool Truncated,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ScrSendKickbackInfo? Kickback,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? BuildersClubFurniCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    BuildersClubMembershipStatus? BuildersClubMembership,
    IReadOnlyList<ScrSendUserInfo> Products);

internal sealed record McpGiftState(
    GiftStateView State,
    GiftClubInfoPage ClubInfoPage,
    GiftWrappingConfiguration? Wrapping);

internal sealed record McpClubGiftOffer(
    int OfferId,
    string LocalizationId,
    int PriceInCredits,
    int PriceInActivityPoints,
    int ActivityPointType,
    int ClubLevel,
    bool Giftable,
    int ProductCount,
    string PreviewImage);

internal sealed record McpGiftDetailMetadata(
    int Total,
    int Returned,
    int MaxItems,
    bool Truncated);

internal sealed record McpGiftSnapshot(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    GiftWrappingConfiguration? Wrapping,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? DaysUntilNextClubGift,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? ClubGiftsAvailable,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? LastSelectedClubGiftProductCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PresentOpened? LastOpenedPresent,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? PendingClubGiftNotifications,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? NewUserGiftSteps,
    IReadOnlyDictionary<int, bool> OfferGiftability,
    int Total,
    int Returned,
    int Offset,
    int MaxItems,
    bool Truncated,
    IReadOnlyList<McpClubGiftOffer> ClubGiftOffers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    McpGiftDetailMetadata? DetailMetadata,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CatalogPageOffer>? Details);
