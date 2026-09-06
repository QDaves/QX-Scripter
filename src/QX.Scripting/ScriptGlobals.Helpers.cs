using Qx.Game;
using Qx.Game.Application;
using Qx.Messages;
using Qx.Model;
using Qx.Protocol;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    private static readonly ConcurrentDictionary<string, object?> _globals = new();
    private static readonly object _global_sync = new();

    /// <summary>
    /// Finds a user in the current room by account id.
    /// </summary>
    /// <returns>
    /// The user, or <see langword="null"/> when nobody with that id is in the room. Bots and
    /// pets are never returned even when their id matches.
    /// </returns>
    public User? GetUser(Id id) => Room.AvatarById(id) as User;

    /// <summary>
    /// Finds a user in the current room by name, case-insensitively.
    /// </summary>
    /// <returns>The user, or <see langword="null"/> when nobody in the room matches.</returns>
    public User? GetUser(string name) => Room.UserByName(name);

    /// <summary>
    /// Finds a pet in the current room by name, case-insensitively.
    /// </summary>
    /// <returns>The pet, or <see langword="null"/> when no pet in the room matches.</returns>
    public Pet? GetPet(string name) =>
        Pets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds a bot in the current room by name, case-insensitively.
    /// </summary>
    /// <returns>The bot, or <see langword="null"/> when no bot in the room matches.</returns>
    public Bot? GetBot(string name) =>
        Bots.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a trade window is currently open.</summary>
    public bool IsTrading => Trade.Active is not null;

    /// <summary>
    /// Where the room session currently stands: outside a room, entering, ready, or leaving.
    /// </summary>
    public RoomSessionState RoomState => Room.State;

    /// <summary>
    /// Whether the room session is fully loaded - room data, avatars, furni, floor plan and
    /// heightmap have all arrived. Prefer this over <see cref="InRoom"/> before reading room
    /// contents.
    /// </summary>
    public bool IsRoomReady => Room.IsReady;

    /// <summary>
    /// Whether the furni inventory has been fully received. While this is
    /// <see langword="false"/>, <see cref="InventoryItems"/> is empty or partial.
    /// </summary>
    public bool IsInventoryLoaded => ReadInventoryState().Furni.Loaded;

    /// <summary>
    /// Whether the server has invalidated the cached furni inventory, meaning the loaded items
    /// may no longer be accurate. It stays loaded and readable until reloaded.
    /// </summary>
    public bool IsInventoryStale => ReadInventoryState().Furni.Stale;

    /// <summary>Whether the pet inventory has been fully received.</summary>
    public bool IsPetInventoryLoaded => ReadInventoryState().Pets.Loaded;

    /// <summary>
    /// Whether the server has invalidated the cached pet inventory.
    /// </summary>
    public bool IsPetInventoryStale => ReadInventoryState().Pets.Stale;

    /// <summary>
    /// Whether the friend list has been fully received. While this is <see langword="false"/>,
    /// <see cref="Friends"/> and <see cref="IsFriend(string)"/> are not authoritative.
    /// </summary>
    public bool IsFriendsLoaded => Game.Friends.IsLoaded;

    /// <summary>Whether the server has invalidated the cached friend list.</summary>
    public bool IsFriendsStale => Game.Friends.IsStale;

    /// <summary>
    /// Whether the achievement list has been received, which is what makes an empty
    /// <see cref="Achievements"/> meaningful.
    /// </summary>
    public bool IsAchievementsLoaded => Game.Achievements.IsLoaded;

    /// <summary>
    /// The achievement category the server marked as the default one for the achievement UI.
    /// Empty until the achievement list has been received.
    /// </summary>
    public string AchievementDefaultCategory => Game.Achievements.DefaultCategory;

    /// <summary>
    /// Finds a pet in the pet inventory by id.
    /// </summary>
    /// <returns>
    /// The pet, or <see langword="null"/> when it is not in the inventory - which is also the
    /// answer while the pet inventory has not been loaded.
    /// </returns>
    public InventoryPet? GetInventoryPet(Id id) =>
        InventoryApplicationPages.ReadPets(
                Application,
                pet_id: id,
                cancellation_token: Ct)
            .Pets
            .Select(LegacyInventoryPet)
            .FirstOrDefault();

    /// <summary>
    /// Finds a pet in the pet inventory by name, case-insensitively.
    /// </summary>
    /// <returns>
    /// The pet, or <see langword="null"/> when no inventory pet matches, including when the pet
    /// inventory has not been loaded.
    /// </returns>
    public InventoryPet? GetInventoryPet(string name) =>
        InventoryApplicationPages.ReadPets(
                Application,
                name: name,
                cancellation_token: Ct)
            .Pets
            .Select(LegacyInventoryPet)
            .FirstOrDefault();

    /// <summary>
    /// The room's static floor plan: which tiles exist and at what stack height. Available
    /// early in room entry. <see langword="null"/> outside a room or before it has arrived.
    /// </summary>
    public FloorPlan? FloorPlan => Room.FloorPlan;

    /// <summary>
    /// The room's heightmap, which unlike <see cref="FloorPlan"/> also reflects furni currently
    /// blocking a tile. <see langword="null"/> outside a room or before it has arrived.
    /// </summary>
    public Heightmap? Heightmap => Room.Heightmap;

    /// <summary>
    /// The floor-plan stack height of a tile.
    /// </summary>
    /// <returns>
    /// The height, or -1 when the tile is a hole, is outside the room, or the floor plan has not
    /// arrived yet.
    /// </returns>
    public int TileHeight(int x, int y) => Room.FloorPlan?.HeightAt(x, y) ?? -1;

    /// <summary>
    /// Whether a tile is part of the room's floor at all, ignoring anything standing on it.
    /// Uses the heightmap when available and falls back to the floor plan.
    /// </summary>
    /// <returns><see langword="false"/> for holes, out-of-bounds tiles and when neither map has loaded.</returns>
    public bool IsOpenTile(int x, int y) => Room.Heightmap is { } map
        ? map.TileAt(x, y).IsFloor
        : Room.FloorPlan?.IsOpen(x, y) ?? false;

    /// <summary>
    /// Whether a tile can currently be stepped on: it is floor, the heightmap does not mark it
    /// blocked by furni, and no avatar is standing on it.
    /// </summary>
    /// <remarks>
    /// Without a heightmap this degrades to <see cref="IsOpenTile"/> plus the avatar check, so
    /// blocking furni is not accounted for.
    /// </remarks>
    public bool IsWalkable(int x, int y)
    {
        if (AvatarAt(x, y) is not null)
            return false;
        if (Room.Heightmap is { } map)
            return map.TileAt(x, y).IsFree;
        return IsOpenTile(x, y);
    }

    /// <summary>
    /// Returns the furni inventory, requesting it from the server and waiting for the full load
    /// if it is not already there. Concurrent callers share one request, and an
    /// already-loaded inventory returns immediately without touching the network.
    /// </summary>
    /// <param name="timeout_ms">How long to wait for the load, in milliseconds.</param>
    /// <param name="cancellation_token">
    /// An extra token to cancel on, combined with the script's own. Leave unset to use only the
    /// script's.
    /// </param>
    /// <returns>A snapshot of every inventory item.</returns>
    /// <exception cref="TimeoutException">The inventory did not finish loading in time.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped, or the supplied token fired.</exception>
    public async Task<IReadOnlyCollection<InventoryItem>> EnsureInventoryLoaded(
        int timeout_ms = 10000,
        CancellationToken cancellation_token = default)
    {
        if (cancellation_token == default)
            return await LoadInventory(timeout_ms, Ct).ConfigureAwait(false);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(Ct, cancellation_token);
        return await LoadInventory(timeout_ms, linked.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the pet inventory, requesting it and waiting for the full load if needed.
    /// Concurrent callers share one request; an already-loaded inventory returns immediately.
    /// </summary>
    /// <param name="timeout_ms">How long to wait for the load, in milliseconds.</param>
    /// <param name="cancellation_token">An extra token to cancel on, combined with the script's own.</param>
    /// <returns>A snapshot of every inventory pet.</returns>
    /// <exception cref="TimeoutException">The pet inventory did not finish loading in time.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped, or the supplied token fired.</exception>
    public async Task<IReadOnlyCollection<InventoryPet>> EnsurePetInventoryLoaded(
        int timeout_ms = 10000,
        CancellationToken cancellation_token = default)
    {
        if (cancellation_token == default)
            return await LoadPetInventory(timeout_ms, Ct).ConfigureAwait(false);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(Ct, cancellation_token);
        return await LoadPetInventory(timeout_ms, linked.Token).ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<InventoryItem>> LoadInventory(
        int timeout_ms,
        CancellationToken cancellation_token)
    {
        InventoryFurniPage first = await Application
            .InvokeAsync<InventoryFurniPageRequest, InventoryFurniPage>(
                ApplicationMemberIds.InventoryFurniList,
                new InventoryFurniPageRequest(Limit: 500),
                cancellation_token)
            .ConfigureAwait(false);
        if (!first.Loaded || first.Stale || first.RecoveryPending)
        {
            first = await Application
                .InvokeAsync<InventoryFurniRefreshRequest, InventoryFurniPage>(
                    ApplicationMemberIds.InventoryFurniRefresh,
                    new InventoryFurniRefreshRequest(
                        Limit: 500,
                        TimeoutMilliseconds: timeout_ms),
                    cancellation_token)
                .ConfigureAwait(false);
        }
        InventoryFurniPage inventory = await InventoryApplicationPages.CompleteFurniAsync(
            Application,
            first,
            cancellation_token: cancellation_token).ConfigureAwait(false);
        return Array.AsReadOnly(inventory.Items.Select(LegacyInventoryItem).ToArray());
    }

    private async Task<IReadOnlyCollection<InventoryPet>> LoadPetInventory(
        int timeout_ms,
        CancellationToken cancellation_token)
    {
        InventoryPetPage first = await Application
            .InvokeAsync<InventoryPetPageRequest, InventoryPetPage>(
                ApplicationMemberIds.InventoryPetsList,
                new InventoryPetPageRequest(Limit: 500),
                cancellation_token)
            .ConfigureAwait(false);
        if (!first.Loaded || first.Stale || first.RecoveryPending)
        {
            first = await Application
                .InvokeAsync<InventoryPetRefreshRequest, InventoryPetPage>(
                    ApplicationMemberIds.InventoryPetsRefresh,
                    new InventoryPetRefreshRequest(
                        Limit: 500,
                        TimeoutMilliseconds: timeout_ms),
                    cancellation_token)
                .ConfigureAwait(false);
        }
        InventoryPetPage inventory = await InventoryApplicationPages.CompletePetsAsync(
            Application,
            first,
            cancellation_token: cancellation_token).ConfigureAwait(false);
        return Array.AsReadOnly(inventory.Pets.Select(LegacyInventoryPet).ToArray());
    }

    /// <summary>
    /// Returns the friend list, requesting it and waiting for the full load if needed. Call this
    /// before relying on <see cref="IsFriend(string)"/> or <see cref="FindFriend"/>.
    /// </summary>
    /// <param name="timeout_ms">How long to wait for the load, in milliseconds.</param>
    /// <param name="cancellation_token">An extra token to cancel on, combined with the script's own.</param>
    /// <returns>A snapshot of every friend.</returns>
    /// <exception cref="TimeoutException">The friend list did not finish loading in time.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped, or the supplied token fired.</exception>
    public async Task<IReadOnlyCollection<Friend>> EnsureFriendsLoaded(
        int timeout_ms = 10000,
        CancellationToken cancellation_token = default)
    {
        if (cancellation_token == default)
            return await LoadFriends(timeout_ms, Ct);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(Ct, cancellation_token);
        return await LoadFriends(timeout_ms, linked.Token);
    }

    /// <summary>Writes a line to the script output. Alias of <see cref="Log"/>.</summary>
    public void Status(object? message) => Log(message);

    /// <summary>
    /// Ends the script immediately and successfully, by throwing
    /// <see cref="ScriptFinishedException"/>. The host treats that as a normal finish, but a
    /// <c>catch (Exception)</c> in the script will swallow it.
    /// </summary>
    /// <exception cref="ScriptFinishedException">Always.</exception>
    public void Finish() => throw new ScriptFinishedException();

    /// <summary>
    /// Stores a value in the process-wide store that outlives a single script run and is shared
    /// by every script and tab. Use it to pass state between runs.
    /// </summary>
    /// <param name="key">The key, compared case-sensitively.</param>
    /// <param name="value">The value; <see langword="null"/> is stored as a real null entry.</param>
    public void SetGlobal(string key, object? value)
    {
        lock (_global_sync)
            _globals[key] = value;
    }

    /// <summary>
    /// Reads a value from the shared store.
    /// </summary>
    /// <returns>
    /// The stored value, or <see langword="null"/> when the key is absent - which is
    /// indistinguishable from a stored null.
    /// </returns>
    public object? GetGlobal(string key) => _globals.GetValueOrDefault(key);

    /// <summary>
    /// Reads a value from the shared store and casts it.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <returns>
    /// The value, or <c>default</c> when the key is absent or the stored value is of another
    /// type. A type mismatch is not reported.
    /// </returns>
    public T? GetGlobal<T>(string key) => _globals.TryGetValue(key, out object? value) && value is T typed ? typed : default;

    /// <summary>
    /// Serialises a value to JSON with the default options: no indentation, property names kept
    /// exactly as declared.
    /// </summary>
    public static string ToJson(object? value) => JsonSerializer.Serialize(value);

    /// <summary>
    /// Deserialises JSON into <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="JsonException">The JSON is malformed or does not fit <typeparamref name="T"/>.</exception>
    public static T? FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json);

    /// <summary>
    /// Enters a room with no password. Fire-and-forget: the room may still refuse entry (locked
    /// door, ban, full room). Subscribe to <see cref="OnRoomReady"/> to know when the entry
    /// succeeded.
    /// </summary>
    /// <param name="roomId">The room id.</param>
    public void EnterRoom(Id roomId) => EnterRoom(roomId, "");

    /// <summary>
    /// Enters a room, supplying a door password.
    /// </summary>
    /// <param name="room_id">The room id.</param>
    /// <param name="password">The door password; empty for rooms that need none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="password"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Fire-and-forget. A wrong password produces a client-side error message rather than an
    /// exception here.
    /// </remarks>
    public void EnterRoom(Id room_id, string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        Application.Invoke<RoomEnterRequest, RoomLifecycleDispatchResult>(
            ApplicationMemberIds.RoomEnter,
            new RoomEnterRequest(room_id, password),
            Ct);
    }

    /// <summary>
    /// The straight-line (Euclidean) distance between two tiles, in tiles. Note that avatars
    /// walk diagonally, so this is not the number of steps between them.
    /// </summary>
    public static double Distance(int x1, int y1, int x2, int y2)
    {
        double dx = (double)x1 - x2;
        double dy = (double)y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The straight-line distance between two tiles, in tiles.</summary>
    public static double Distance(Tile a, Tile b) => Distance(a.X, a.Y, b.X, b.Y);

    /// <summary>
    /// The straight-line distance between two avatars' current tiles, in tiles. Height is
    /// ignored.
    /// </summary>
    public double Distance(Avatar a, Avatar b) => Distance(a.X, a.Y, b.X, b.Y);
}
