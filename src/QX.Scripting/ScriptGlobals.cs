using Qx;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Scripting;

/// <summary>
/// The globals object every QX script runs against: all of its public members are in scope
/// unqualified inside a <c>.csx</c> script, so <c>Say("hi")</c> and <c>Room.RoomId</c> work
/// without any receiver.
/// </summary>
/// <remarks>
/// <para>
/// <b>State properties</b> such as <see cref="Users"/>, <see cref="FloorItems"/> or
/// <see cref="Credits"/> read whatever the interceptor has observed on the wire so far.
/// Nothing on this class polls the server on its own: if the game client never asked for a
/// piece of state, the corresponding property stays empty or zero rather than blocking. The
/// <c>Is...Loaded</c> flags distinguish "empty" from "not yet received", and the
/// <c>Ensure...Loaded</c> / <c>Get...</c> methods are the ones that actually go to the server.
/// Collections are snapshots taken per read, while the objects inside them are live and keep
/// updating.
/// </para>
/// <para>
/// <b>Events.</b> Every <c>On...</c> method returns an <see cref="IDisposable"/> handle.
/// Disposing it unsubscribes that one handler; all handles are disposed automatically when the
/// script stops, so a script that runs to completion never has to unsubscribe. Discarding the
/// handle does not unsubscribe. Handlers run on the interceptor's dispatch thread in packet
/// order and must not block for long; an exception thrown by one handler does not affect the
/// others. Callbacks that carry a before/after pair always pass the subject first, then the
/// previous value, then the new one.
/// </para>
/// <para>
/// <b>Requests</b> (<c>Get...</c>, <c>Search...</c>) send a message and await the matching reply.
/// Their <c>timeoutMs</c> is milliseconds and is a total budget across one automatic retry;
/// the reply that satisfies the request is blocked so the game client's own UI never sees it;
/// nothing is cached, so every call goes to the server again. They throw
/// <see cref="Qx.Game.RequestTimeoutException"/> on timeout,
/// <see cref="Qx.Game.RequestDisconnectedException"/> when the connection drops while waiting,
/// <see cref="OperationCanceledException"/> when the script is stopped, and
/// <see cref="NotSupportedException"/> where the active client cannot express the request.
/// </para>
/// <para>
/// <b>Actions</b> (<see cref="Walk(int, int)"/>, <see cref="Say"/>, <see cref="PickupFurni(FloorItem, bool)"/>
/// and the rest) are fire-and-forget: they compose one packet and return. They never confirm
/// success, and the server silently ignores requests that fail on rights, flood limits or a
/// missing target. Observe the matching event instead.
/// </para>
/// <para>
/// <b>Raw messages.</b> Message names are plain strings resolved against the catalog loaded for
/// the active session; the compile-checked constants live on <see cref="Msg"/> in
/// <c>QX.Protocol</c>. A name that cannot be resolved throws when sending, but binds nothing
/// and stays silent when intercepting.
/// </para>
/// </remarks>
public partial class ScriptGlobals : IDisposable
{
    private readonly Action<string> _log;
    private readonly CancellationToken _cancellationToken;
    private readonly Action<Exception>? _backgroundError;
    private readonly Action? _backgroundFinishedCallback;

    public ScriptGlobals(
        IInterceptor extension,
        GameState game,
        IApplicationRuntime application,
        Action<string> log,
        CancellationToken cancellationToken,
        Action<Exception>? backgroundError = null)
        : this(
            extension,
            game,
            application,
            log,
            cancellationToken,
            backgroundError,
            null)
    {
    }

    internal ScriptGlobals(
        IInterceptor extension,
        GameState game,
        IApplicationRuntime application,
        Action<string> log,
        CancellationToken cancellationToken,
        Action<Exception>? backgroundError,
        Action? backgroundFinished)
    {
        Ext = extension;
        Game = game;
        Application = application;
        _log = log;
        _cancellationToken = cancellationToken;
        _backgroundError = backgroundError;
        _backgroundFinishedCallback = backgroundFinished;
    }

    /// <summary>
    /// The interceptor the script is attached to. Use it for packet sending and interception
    /// that the higher-level helpers on this class do not cover, and for
    /// <see cref="IInterceptor.Messages"/> when a message name has to be resolved by hand.
    /// </summary>
    public IInterceptor Ext { get; }

    /// <summary>
    /// The shared game-state tracker that backs every state property on this class. It is
    /// owned by the host and survives across script runs, so state observed before the script
    /// started is already present.
    /// </summary>
    public GameState Game { get; }

    public IApplicationRuntime Application { get; }

    private ProfileStateView ReadProfileState() =>
        Application.Invoke<ProfileStateRequest, ProfileStateView>(
            ApplicationMemberIds.ProfileState,
            new ProfileStateRequest(),
            Ct);

    private static UserData? LegacyProfile(ProfileIdentitySnapshot? identity) =>
        identity is null
            ? null
            : new UserData
            {
                Id = identity.Id,
                Name = identity.Name,
                Figure = identity.Figure,
                Gender = identity.Gender,
                Motto = identity.Motto,
                RealName = identity.RealName,
                DirectMail = identity.DirectMail,
                RespectTotal = identity.RespectTotal,
                RespectLeft = identity.RespectLeft,
                PetRespectLeft = identity.PetRespectLeft,
                StreamPublishingAllowed = identity.StreamPublishingAllowed,
                LastAccessDate = identity.LastAccessDate,
                IsNameChangeable = identity.IsNameChangeable,
                IsSafetyLocked = identity.IsSafetyLocked,
                IsTradeLocked = identity.IsTradeLocked,
                NameColor = identity.NameColor,
                RespectReplenishesLeft = identity.RespectReplenishesLeft,
                MaxRespectPerDay = identity.MaxRespectPerDay,
                TrailingFields = identity.TrailingFields
            };

    private InventoryStateView ReadInventoryState() =>
        Application.Invoke<InventoryStateRequest, InventoryStateView>(
            ApplicationMemberIds.InventoryState,
            new InventoryStateRequest(),
            Ct);

    private TradeStateView ReadTradeState() =>
        Application.Invoke<TradeStateRequest, TradeStateView>(
            ApplicationMemberIds.TradeState,
            new TradeStateRequest(),
            Ct);

    private void SendTradeCommand(string member_id)
    {
        TradeStateView trade = ReadTradeState();
        Application.Invoke<TradeCommandRequest, TradeDispatchResult>(
            member_id,
            new TradeCommandRequest(
                trade.SessionGeneration,
                trade.Revision,
                trade.LatestEpoch),
            Ct);
    }

    internal static IReadOnlyList<InventoryItem> ReadInventoryItems(
        IApplicationRuntime application,
        CancellationToken cancellation_token = default) =>
        Array.AsReadOnly(
            InventoryApplicationPages.ReadFurni(
                    application,
                    cancellation_token: cancellation_token)
                .Items
                .Select(LegacyInventoryItem)
                .ToArray());

    internal static IReadOnlyList<InventoryPet> ReadInventoryPetModels(
        IApplicationRuntime application,
        CancellationToken cancellation_token = default) =>
        Array.AsReadOnly(
            InventoryApplicationPages.ReadPets(
                    application,
                    cancellation_token: cancellation_token)
                .Pets
                .Select(LegacyInventoryPet)
                .ToArray());

    internal static InventoryItem LegacyInventoryItem(InventoryItemSnapshot snapshot)
    {
        if (!Enum.TryParse(snapshot.Type, false, out ItemType item_type) ||
            item_type is not (ItemType.Floor or ItemType.Wall))
        {
            throw new InvalidDataException($"Unsupported inventory item type '{snapshot.Type}'.");
        }

        return new InventoryItem
        {
            ItemId = snapshot.ItemId,
            Type = item_type,
            Id = snapshot.Id,
            Kind = snapshot.Kind,
            Category = snapshot.Category,
            Data = LegacyItemData(snapshot.Data),
            IsRecyclable = snapshot.IsRecyclable,
            IsTradeable = snapshot.IsTradeable,
            IsGroupable = snapshot.IsGroupable,
            IsSellable = snapshot.IsSellable,
            SecondsToExpiration = snapshot.SecondsToExpiration,
            HasRentPeriodStarted = snapshot.HasRentPeriodStarted,
            RoomId = snapshot.RoomId,
            IsUnseen = snapshot.IsUnseen,
            Timestamp = snapshot.Timestamp,
            IsNft = snapshot.IsNft,
            NftName = snapshot.NftName,
            IsExternalImage = snapshot.IsExternalImage,
            SlotId = snapshot.SlotId,
            Extra = snapshot.Extra
        };
    }

    internal static InventoryPet LegacyInventoryPet(InventoryPetSnapshot snapshot)
    {
        var pet = new InventoryPet
        {
            Id = snapshot.Id,
            Name = snapshot.Name,
            TypeId = snapshot.TypeId,
            PaletteId = snapshot.PaletteId,
            Color = snapshot.Color,
            BreedId = snapshot.BreedId,
            CustomParts = snapshot.CustomParts
                .Select(part => new PetCustomPart(part.LayerId, part.PartId, part.PaletteId))
                .ToArray(),
            Level = snapshot.Level,
            RarityLevel = snapshot.RarityLevel,
            RoomId = snapshot.RoomId,
            RoomName = snapshot.RoomName,
            RoomContext = snapshot.RoomContext
        };
        if (pet.HasRoomContext != snapshot.HasRoomContext ||
            pet.IsInRoom != snapshot.IsInRoom ||
            pet.FigureString != snapshot.FigureString)
        {
            throw new InvalidDataException("The inventory pet snapshot is internally inconsistent.");
        }
        return pet;
    }

    private static ItemData LegacyItemData(ItemDataSnapshot snapshot)
    {
        ItemData data = snapshot.Type switch
        {
            nameof(ItemDataType.Legacy) => new LegacyData(),
            nameof(ItemDataType.Map) => LegacyMapData(snapshot),
            nameof(ItemDataType.StringArray) => LegacyStringArrayData(snapshot),
            nameof(ItemDataType.VoteResult) => new VoteResultData
            {
                Result = snapshot.VoteResult ?? throw MissingItemData(snapshot, nameof(snapshot.VoteResult))
            },
            nameof(ItemDataType.Empty) => new EmptyItemData(),
            nameof(ItemDataType.IntArray) => LegacyIntArrayData(snapshot),
            nameof(ItemDataType.HighScore) => LegacyHighScoreData(snapshot),
            nameof(ItemDataType.CrackableFurni) => new CrackableFurniData
            {
                Hits = snapshot.Hits ?? throw MissingItemData(snapshot, nameof(snapshot.Hits)),
                Target = snapshot.Target ?? throw MissingItemData(snapshot, nameof(snapshot.Target))
            },
            _ => throw new InvalidDataException($"Unsupported inventory item data type '{snapshot.Type}'.")
        };
        data.Flags = (ItemDataFlags)snapshot.Flags;
        data.Value = snapshot.Value;
        data.UniqueSerialNumber = snapshot.UniqueSerialNumber;
        data.UniqueSeriesSize = snapshot.UniqueSeriesSize;
        data.UniqueLimitedData = snapshot.UniqueLimitedData;
        if (data.IsLimitedRare != snapshot.IsLimitedRare || data.State != snapshot.State)
            throw new InvalidDataException("The inventory item data snapshot is internally inconsistent.");
        return data;
    }

    private static MapData LegacyMapData(ItemDataSnapshot snapshot)
    {
        var data = new MapData();
        foreach ((string key, string value) in
                 snapshot.MapEntries ?? throw MissingItemData(snapshot, nameof(snapshot.MapEntries)))
        {
            data.Entries.Add(key, value);
        }
        return data;
    }

    private static StringArrayData LegacyStringArrayData(ItemDataSnapshot snapshot)
    {
        var data = new StringArrayData();
        data.Values.AddRange(
            snapshot.StringValues ?? throw MissingItemData(snapshot, nameof(snapshot.StringValues)));
        return data;
    }

    private static IntArrayData LegacyIntArrayData(ItemDataSnapshot snapshot)
    {
        var data = new IntArrayData();
        data.Values.AddRange(
            snapshot.IntValues ?? throw MissingItemData(snapshot, nameof(snapshot.IntValues)));
        return data;
    }

    private static HighScoreData LegacyHighScoreData(ItemDataSnapshot snapshot)
    {
        var data = new HighScoreData
        {
            ScoreType = snapshot.ScoreType ?? throw MissingItemData(snapshot, nameof(snapshot.ScoreType)),
            ClearType = snapshot.ClearType ?? throw MissingItemData(snapshot, nameof(snapshot.ClearType))
        };
        foreach (HighScoreSnapshot score in
                 snapshot.HighScores ?? throw MissingItemData(snapshot, nameof(snapshot.HighScores)))
        {
            data.Scores.Add(new HighScore
            {
                Score = score.Score,
                Names = [.. score.Names]
            });
        }
        return data;
    }

    private static InvalidDataException MissingItemData(ItemDataSnapshot snapshot, string member) =>
        new($"Inventory item data type '{snapshot.Type}' is missing {member}.");

    /// <summary>
    /// The panel a tab declares with <c>//@ui:</c> directives: the values the user entered, the
    /// button that started the run, the click handlers that keep the script alive after its body
    /// returns, and the sinks that write output boxes, tables, progress bars, status lines and
    /// toasts back to it. Outside panel mode every getter returns its fallback,
    /// <see cref="ScriptUi.Clicked"/> is always <see langword="false"/>, the writers do nothing and
    /// <see cref="ScriptUi.Confirm"/>/<see cref="ScriptUi.Prompt"/> answer at once rather than wait.
    /// </summary>
    public ScriptUi Ui { get; } = new();

    /// <summary>
    /// Live tracker of the current room session: room identity, entry/exit state, avatars,
    /// furni, floor plan and the room-related events. Present even when the user is outside a
    /// room, in which case its collections are empty and <see cref="RoomManager.RoomId"/> is 0.
    /// </summary>
    public RoomManager Room => Game.Room;

    public ProfileStateView Profile => ReadProfileState();

    /// <summary>Tracker of the badges owned and the badge slots currently equipped.</summary>
    public BadgeInventoryManager BadgeInventory => Game.Badges;

    public TradeStateView Trade => ReadTradeState();

    /// <summary>Tracker of quest and campaign state.</summary>
    public QuestManager Quests => Game.Quests;

    public MarketplaceStateView Marketplace => GetMarketplaceStatePage();

    /// <summary>Tracker of the crafting (alchemy) session state.</summary>
    public CraftingManager Crafting => Game.Crafting;

    /// <summary>Tracker of received gifts and gift-opening results.</summary>
    public GiftManager Gifts => Game.Gifts;

    /// <summary>Tracker of Habbo Club / subscription state for the local user.</summary>
    public SubscriptionManager Subscriptions => Game.Subscriptions;

    /// <summary>
    /// Tracker of group-forum state: thread lists, individual threads and moderation results.
    /// </summary>
    /// <remarks>Group forums exist only on the Flash client.</remarks>
    public ForumManager Forums => Game.Forums;

    /// <summary>
    /// The cancellation token that is cancelled when the script is stopped. Pass it to every
    /// awaited call that accepts one; awaiting it, or calling <see cref="Sleep(int)"/> /
    /// <see cref="Delay(int)"/>, throws <see cref="OperationCanceledException"/> once the
    /// script is asked to stop.
    /// </summary>
    /// <remarks>
    /// Inside a script body this resolves to the ambient per-run token; outside a run it falls
    /// back to the token the globals were constructed with.
    /// </remarks>
    public CancellationToken Ct
    {
        get
        {
            CancellationToken current = ScriptExecutionContext.CancellationToken;
            return current.CanBeCanceled ? current : _cancellationToken;
        }
    }

    internal CancellationToken BaseCancellationToken => _cancellationToken;

    private readonly List<TrackedSubscription> _subscriptions = [];
    private readonly List<Task> _backgroundTasks = [];
    private bool _cleanupHooked;
    private bool _backgroundClosed;
    private bool _disposed;
    private int _backgroundFinished;
    private CancellationTokenRegistration _cleanupRegistration;

    private IDisposable Track(IDisposable subscription)
    {
        CancellationToken cancellation_token = Ct;
        var tracked = new TrackedSubscription(subscription, Untrack);
        lock (_subscriptions)
        {
            if (_disposed)
            {
                tracked.Dispose();
                throw new ObjectDisposedException(nameof(ScriptGlobals));
            }
            if (_backgroundClosed || cancellation_token.IsCancellationRequested)
            {
                tracked.Dispose();
                throw new OperationCanceledException(
                    "Script event subscriptions are closed.",
                    cancellation_token);
            }

            try
            {
                if (!_cleanupHooked)
                {
                    _cleanupHooked = true;
                    _cleanupRegistration = cancellation_token.Register(DisposeSubscriptions);
                }
                if (_backgroundClosed || cancellation_token.IsCancellationRequested)
                    throw new OperationCanceledException(cancellation_token);
                _subscriptions.Add(tracked);
            }
            catch
            {
                tracked.Dispose();
                throw;
            }
        }

        return tracked;
    }

    private void Untrack(TrackedSubscription subscription)
    {
        lock (_subscriptions)
            _subscriptions.Remove(subscription);
    }

    private void DisposeSubscriptions()
    {
        TrackedSubscription[] subscriptions;
        lock (_subscriptions)
        {
            _backgroundClosed = true;
            subscriptions = [.. _subscriptions];
            _subscriptions.Clear();
        }
        foreach (TrackedSubscription subscription in subscriptions)
            subscription.Dispose();
    }

    /// <summary>
    /// Unsubscribes every handler this instance is still holding. The host calls this when the
    /// script run ends; a script does not need to call it.
    /// </summary>
    public void Dispose()
    {
        lock (_subscriptions)
        {
            if (_disposed)
                return;
            _disposed = true;
            _backgroundClosed = true;
        }
        DisposeSubscriptions();
        _cleanupRegistration.Dispose();
    }

    /// <summary>
    /// Waits for the tasks started by <see cref="RunTask(Action)"/> to finish. Called by the
    /// host during shutdown; scripts rarely need it.
    /// </summary>
    /// <param name="timeoutMs">How long to wait, in milliseconds.</param>
    /// <returns>
    /// <see langword="true"/> when every background task completed (or none was running),
    /// <see langword="false"/> when the timeout elapsed first. Never throws on timeout.
    /// </returns>
    public async Task<bool> WaitForBackgroundTasksAsync(int timeoutMs = 500)
    {
        Task[] tasks;
        lock (_subscriptions)
        {
            if (Ct.IsCancellationRequested)
                _backgroundClosed = true;
            lock (_backgroundTasks)
                tasks = [.. _backgroundTasks];
        }
        if (tasks.Length == 0)
            return true;

        Task all = Task.WhenAll(tasks);
        if (all.IsCompleted)
        {
            await all.ConfigureAwait(false);
            return true;
        }

        Task completed = await Task.WhenAny(all, Task.Delay(timeoutMs)).ConfigureAwait(false);
        return ReferenceEquals(completed, all);
    }

    private bool TryTrackBackgroundTask(Task task, CancellationToken cancellation_token)
    {
        lock (_subscriptions)
        {
            if (_disposed || _backgroundClosed || cancellation_token.IsCancellationRequested)
                return false;
            lock (_backgroundTasks)
                _backgroundTasks.Add(task);
        }
        _ = RemoveBackgroundTask(task);
        return true;
    }

    private async Task RemoveBackgroundTask(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        finally
        {
            lock (_backgroundTasks)
                _backgroundTasks.Remove(task);
        }
    }

    private void ReportBackgroundFinished()
    {
        if (Interlocked.Exchange(ref _backgroundFinished, 1) == 0)
            _backgroundFinishedCallback?.Invoke();
    }

    /// <summary>
    /// Whether the interceptor currently has a live connection between the game client and the
    /// server. Sending while this is <see langword="false"/> has no effect on the wire.
    /// </summary>
    public bool IsConnected => Ext.IsConnected;

    /// <summary>
    /// Details of the intercepted connection (host, port, hotel version, client identifier and
    /// client flavour), or <see langword="null"/> before a connection has been observed.
    /// </summary>
    public Session? Session => Ext.Session;

    /// <summary>
    /// The client flavour the session is running: <see cref="ClientType.Flash"/> or
    /// <see cref="ClientType.Unity"/>. Falls back to <see cref="ClientType.Flash"/> when no
    /// session has reported a flavour yet. Several subsystems behave differently per flavour,
    /// so branch on this rather than guessing.
    /// </summary>
    public ClientType Client => CurrentClient;

    /// <summary>
    /// The local user's account data (id, name, figure, motto, gender, home room), or
    /// <see langword="null"/> until the server has sent it. Snapshot: it is replaced, not
    /// mutated, on every profile update.
    /// </summary>
    public UserData? SelfProfile => LegacyProfile(Profile.Identity);

    /// <summary>
    /// The local user's avatar in the current room, or <see langword="null"/> when outside a
    /// room, before the avatar list has loaded, or before the own identity is known. Live
    /// object: its position, dance, effect and idle state are updated in place as packets
    /// arrive.
    /// </summary>
    public User? SelfAvatar => SelfProfile is { } data ? Room.AvatarById(data.Id) as User : null;

    /// <summary>Alias of <see cref="SelfProfile"/>.</summary>
    public UserData? Self => SelfProfile;

    /// <summary>Alias of <see cref="SelfAvatar"/>.</summary>
    public User? Me => SelfAvatar;

    /// <summary>
    /// Whether a room session is open. It becomes <see langword="true"/> while entering, so it
    /// is not a guarantee that avatars and furni have loaded; use <see cref="IsRoomReady"/> for
    /// that.
    /// </summary>
    public bool InRoom => Room.IsInRoom;

    /// <summary>
    /// How the last room session ended (room id, whether the room had been fully entered, the
    /// native reason and the kick, if any), or <see langword="null"/> when no room has been
    /// left yet this session.
    /// </summary>
    public RoomExitState? LastRoomExit => Room.LastExit;

    /// <summary>
    /// The reason the server itself gave for the last room exit, or <see langword="null"/> when
    /// the exit carried no reason (for example a plain client-side leave).
    /// </summary>
    public RoomExitReason? LastNativeRoomExitReason => Room.LastNativeExitReason;

    /// <summary>Whether the last room exit was caused by the local user being kicked.</summary>
    public bool WasKickedFromRoom => Room.WasKicked;

    /// <summary>
    /// The most recent kick observed in the current or a previous room session, which
    /// is cleared when a new room session begins.
    /// </summary>
    public RoomKick? LastRoomKick => Room.LastKick;

    /// <summary>
    /// The kick that caused <see cref="LastRoomExit"/>, or <see langword="null"/> when
    /// the last room exit was not caused by a kick.
    /// </summary>
    public RoomKick? LastRoomExitKick => Room.LastExitKick;

    /// <summary>
    /// Every avatar currently in the room: users, bots and pets. A snapshot is taken on each
    /// read, so the sequence itself does not change while it is enumerated, but the
    /// <see cref="Avatar"/> objects in it are live and keep updating. Empty when outside a room
    /// or before the avatar list has arrived.
    /// </summary>
    public IEnumerable<Avatar> Avatars => Room.Avatars;

    /// <summary>
    /// The human players in the room, filtered out of <see cref="Avatars"/>. Includes the local
    /// user. Snapshot per read; the elements are live.
    /// </summary>
    public IEnumerable<User> Users => Room.Avatars.OfType<User>();

    /// <summary>
    /// The pets in the room, filtered out of <see cref="Avatars"/>. Snapshot per read; the
    /// elements are live.
    /// </summary>
    public IEnumerable<Pet> Pets => Room.Avatars.OfType<Pet>();

    /// <summary>
    /// The bots in the room, filtered out of <see cref="Avatars"/>. Snapshot per read; the
    /// elements are live.
    /// </summary>
    public IEnumerable<Bot> Bots => Room.Avatars.OfType<Bot>();

    /// <summary>
    /// Every floor item currently placed in the room. A snapshot is taken on each read; the
    /// <see cref="FloorItem"/> objects are live and their location and state keep updating.
    /// Empty until the server has sent the object list - check
    /// <see cref="RoomManager.FloorItemsAreLoaded"/> to tell "empty room" from "not loaded".
    /// </summary>
    public IEnumerable<FloorItem> FloorItems => Room.FloorItems;

    /// <summary>
    /// Every wall item currently placed in the room, on the same terms as
    /// <see cref="FloorItems"/>. Use <see cref="RoomManager.WallItemsAreLoaded"/> to tell
    /// "empty" from "not loaded".
    /// </summary>
    public IEnumerable<WallItem> WallItems => Room.WallItems;

    /// <summary>
    /// The furni currently held in the inventory. Empty until the inventory has been requested;
    /// call <see cref="EnsureInventoryLoaded"/> first, or check <see cref="IsInventoryLoaded"/>.
    /// Snapshot per read.
    /// </summary>
    public IEnumerable<InventoryItem> InventoryItems => ReadInventoryItems(Application, Ct);

    /// <summary>
    /// The pets currently held in the inventory. Empty until the pet inventory has been
    /// requested; call <see cref="EnsurePetInventoryLoaded"/> first. Snapshot per read.
    /// </summary>
    public IEnumerable<InventoryPet> InventoryPets => ReadInventoryPetModels(Application, Ct);

    /// <summary>
    /// The friend list. Empty until the friend list has been received; call
    /// <see cref="EnsureFriendsLoaded"/> first, or check <see cref="IsFriendsLoaded"/>.
    /// Snapshot per read.
    /// </summary>
    public IEnumerable<Friend> Friends => Game.Friends.Friends;

    /// <summary>
    /// Finds a user in the current room by name, case-insensitively.
    /// </summary>
    /// <returns>The matching user, or <see langword="null"/> when nobody in the room matches.</returns>
    public User? FindUser(string name) => Room.UserByName(name);

    /// <summary>
    /// The first avatar standing on the given tile, or <see langword="null"/> when the tile is
    /// free. Only the tile the avatar occupies is considered, not the tiles a walk animation
    /// passes over.
    /// </summary>
    public Avatar? AvatarAt(int x, int y) => Room.Avatars.FirstOrDefault(a => a.X == x && a.Y == y);

    /// <summary>
    /// Finds an entry in the friend list by name, case-insensitively.
    /// </summary>
    /// <returns>
    /// The friend, or <see langword="null"/> when there is no such friend - which is also what
    /// is returned when the friend list has not been loaded yet.
    /// </returns>
    public Friend? FindFriend(string name) => Game.Friends.FriendByName(name);

    /// <summary>
    /// Whether the named user is in the friend list. Returns <see langword="false"/> when the
    /// friend list has not been loaded yet, so call <see cref="EnsureFriendsLoaded"/> first if
    /// the answer must be authoritative.
    /// </summary>
    public bool IsFriend(string name) => Game.Friends.IsFriend(name);

    /// <summary>
    /// Writes a line to the script's output log. Non-string values are rendered with
    /// <see cref="object.ToString"/>; <see langword="null"/> logs an empty line.
    /// </summary>
    public void Log(object? message) => _log(message?.ToString() ?? "");

    /// <summary>
    /// Asynchronously waits for the given number of milliseconds, observing script
    /// cancellation.
    /// </summary>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
    public Task Delay(int milliseconds) => Task.Delay(milliseconds, Ct);

    /// <summary>
    /// Sends a packet in the direction recorded in its header, translating it to the active
    /// client's wire format when necessary: a Flash packet sent through a Unity session is
    /// re-composed as the equivalent Unity message, and a client-agnostic packet is treated as
    /// Flash.
    /// </summary>
    /// <param name="packet">The packet to send. Its header carries the direction and message.</param>
    /// <exception cref="NotSupportedException">
    /// The packet is bound to a client flavour that cannot be sent through this session, or a
    /// native Unity packet does not match its verified wire schema.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The packet's header is not present in the active message catalog.
    /// </exception>
    public void Send(IPacket packet)
    {
        if (CurrentClient is ClientType.Unity && packet.Client is ClientType.None)
        {
            using Packet flash_packet = UnityCompatibilityPacket.CopyAs(packet, ClientType.Flash);
            Send(flash_packet);
            return;
        }

        if (CurrentClient is ClientType.Unity &&
            packet.Header.Direction is Direction.In &&
            packet.Client is ClientType.Flash)
        {
            SendIncomingFlashPacket(packet);
            return;
        }

        if (CurrentClient is ClientType.Unity &&
            packet.Header.Direction is Direction.Out &&
            packet.Client is ClientType.Flash)
        {
            SendOutgoingFlashPacket(packet);
            return;
        }

        if (packet.Client is not ClientType.None && packet.Client != CurrentClient)
            throw new NotSupportedException($"A {packet.Client} packet cannot be sent through a {CurrentClient} session.");

        if (CurrentClient is ClientType.Unity && packet.Client is ClientType.Unity)
        {
            switch (packet.Header.Direction)
            {
                case Direction.In:
                    if (!Ext.Messages.TryGetIdentifier(packet.Header, out Identifier incoming_identifier))
                        throw new InvalidOperationException($"Unknown incoming Unity header '{packet.Header.Value}'.");
                    SendNativeUnityIncoming(incoming_identifier.Name, packet);
                    return;
                case Direction.Out:
                    ValidateNativeUnityOutgoing(packet);
                    Ext.Send(packet);
                    return;
                default:
                    throw new NotSupportedException("A native Unity packet must have an incoming or outgoing direction.");
            }
        }

        Ext.Send(packet);
    }

    /// <summary>
    /// Composes and sends an outgoing message to the server by name.
    /// </summary>
    /// <param name="name">
    /// The message name as it appears in the catalog - use the constants on
    /// <see cref="Msg.Out"/> rather than free-form strings. Flash names are accepted on a Unity
    /// session and translated.
    /// </param>
    /// <param name="values">
    /// The field values in wire order. Numeric literals are written as 32-bit integers; wrap a
    /// value in <see cref="Id"/> or <see cref="Length"/> when the field is client-width
    /// dependent.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The name is not in the catalog for this session, or the values cannot be matched to a
    /// Unity wire schema.
    /// </exception>
    /// <remarks>Fire-and-forget: it returns as soon as the packet is handed to the client.</remarks>
    public void SendToServer(string name, params object[] values) => SendNamed(Direction.Out, name, values);

    public void SendToServer(MessageKey key, params object[] values) => SendNamed(Direction.Out, key, values);

    public void SendToServer<T>(MessageKey key, T message) where T : IComposer
    {
        if (key.IsEmpty ||
            !Ext.Messages.Registry.TryGet(key, out MessageDescriptor? descriptor) ||
            descriptor.Direction != Direction.Out ||
            !Ext.Messages.TryGetHeader(key, out Header header))
        {
            throw new InvalidOperationException($"Unknown outgoing semantic message '{key.Value}'.");
        }

        using var packet = new Packet(header, CurrentClient)
        {
            Context = new ParserContext(
                Ext.Messages,
                Ext.Messages.GetWireProfile(CurrentClient))
        };
        packet.Writer().Compose(message);
        Send(packet);
    }

    /// <summary>
    /// Composes and sends an incoming message to the game client by name, as if the server had
    /// sent it. The server never sees it, so this changes only what the local client displays.
    /// </summary>
    /// <param name="name">
    /// The incoming message name - use the constants on <see cref="Msg.In"/>. Flash names are
    /// accepted on a Unity session and translated to the native Unity layout.
    /// </param>
    /// <param name="values">The field values in wire order.</param>
    /// <exception cref="InvalidOperationException">The name is not in the catalog for this session.</exception>
    public void SendToClient(string name, params object[] values) => SendNamed(Direction.In, name, values);

    private void SendToClient<T>(MessageContract<T> contract, T message)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(message);

        ClientType client = CurrentClient;
        if (!contract.Supports(client))
            throw new UnsupportedClientException(client);
        if (!Ext.Messages.TryGetHeader(contract.Key, out Header header))
            throw new InvalidOperationException($"Unknown incoming semantic message '{contract.Key.Value}'.");

        using var packet = new Packet(header, client)
        {
            Context = new ParserContext(
                Ext.Messages,
                Ext.Messages.GetWireProfile(client))
        };
        PacketWriter writer = packet.Writer();
        contract.Compose(message, in writer);
        Ext.Send(packet);
    }

    /// <summary>
    /// Intercepts every packet with the given header, in either direction.
    /// </summary>
    /// <param name="header">The exact header to intercept.</param>
    /// <param name="handler">
    /// Invoked on the interceptor's dispatch thread for each match. Call
    /// <see cref="Intercept.Block"/> inside it to stop the packet from reaching its
    /// destination, or replace <see cref="Intercept.Packet"/> to rewrite it. Exceptions thrown
    /// here are isolated and do not stop the other handlers for the same header.
    /// </param>
    /// <returns>
    /// A handle that unsubscribes when disposed. Keep it for as long as the subscription should
    /// live; it is also disposed automatically when the script stops.
    /// </returns>
    public IDisposable OnIntercept(Header header, Action<Intercept> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Ext.Intercept(
            header,
            Guarded<Intercept>(intercept => InvokeHeaderIntercept(header, intercept, handler))));
    }

    /// <summary>
    /// Intercepts every packet matching the identifier's client, direction and message name.
    /// </summary>
    /// <returns>
    /// A handle that unsubscribes when disposed, and is disposed automatically when the script
    /// stops. If the identifier cannot be resolved against the active catalog the handler is
    /// silently bound to nothing and never fires.
    /// </returns>
    public IDisposable OnIntercept(Identifier identifier, Action<Intercept> handler)
        => Track(identifier.Direction is Direction.In
            ? InterceptIncomingEvent(identifier.Name, identifier.Client, handler)
            : InterceptOutgoingEvent(identifier.Name, identifier.Client, handler));

    /// <summary>
    /// Intercepts an incoming (server to client) message by name, whichever client flavour the
    /// session runs. On a Unity session the packet handed to the handler is presented in the
    /// Flash field layout whenever a Flash equivalent exists, so one handler works for both.
    /// </summary>
    /// <param name="name">
    /// The message name - prefer the constants on <see cref="Msg.In"/>. An unknown name binds
    /// nothing and fails silently.
    /// </param>
    /// <param name="handler">Receives each matching incoming packet.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnIn(string name, Action<Intercept> handler) =>
        Track(InterceptIncomingEvent(name, ClientType.None, handler));

    /// <summary>
    /// Intercepts an incoming message by its Flash name and Flash field layout only. Binds
    /// nothing on a Unity session.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFlashIn(string name, Action<Intercept> handler) =>
        Track(InterceptIncomingEvent(name, ClientType.Flash, handler));

    /// <summary>
    /// Intercepts an incoming message by its Unity name, delivering the native Unity field
    /// layout without Flash translation. Binds nothing on a Flash session.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnUnityIn(string name, Action<Intercept> handler) =>
        Track(InterceptIncomingEvent(name, ClientType.Unity, handler));

    /// <summary>
    /// Intercepts an outgoing (client to server) message by name on either client flavour. On a
    /// Unity session the packet is presented in the Flash field layout where an equivalent
    /// exists, and related Unity-only message names are subscribed as well.
    /// </summary>
    /// <param name="name">
    /// The message name - prefer the constants on <see cref="Msg.Out"/>. An unknown name binds
    /// nothing and fails silently.
    /// </param>
    /// <param name="handler">Receives each matching outgoing packet.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnOut(string name, Action<Intercept> handler) =>
        Track(InterceptOutgoingEvent(name, ClientType.None, handler));

    /// <summary>
    /// Intercepts an outgoing message by its Flash name and Flash field layout only. Binds
    /// nothing on a Unity session.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnFlashOut(string name, Action<Intercept> handler) =>
        Track(InterceptOutgoingEvent(name, ClientType.Flash, handler));

    /// <summary>
    /// Intercepts an outgoing message by its Unity name, delivering the native Unity field
    /// layout without Flash translation. Binds nothing on a Flash session.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnUnityOut(string name, Action<Intercept> handler) =>
        Track(InterceptOutgoingEvent(name, ClientType.Unity, handler));

    /// <summary>
    /// Intercepts an incoming message and parses each packet into <typeparamref name="T"/>
    /// before invoking the handler. The packet is copied first, so blocking or rewriting is not
    /// possible from this overload - use <see cref="OnIn(string, Action{Intercept})"/> for that.
    /// </summary>
    /// <typeparam name="T">The message model to parse the packet as.</typeparam>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown from inside the handler's dispatch when the packet does not parse cleanly into
    /// <typeparamref name="T"/> or leaves trailing bytes.
    /// </exception>
    public IDisposable OnIn<T>(string name, Action<T> handler) where T : IParserComposer<T> =>
        Track(InterceptIncomingEvent(
            name,
            ClientType.None,
            intercept => handler(ParseCopy<T>(name, intercept.Packet))));

    private IDisposable OnIn<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T> =>
        Track(Ext.Intercept(
            contract.Key,
            Guarded<Intercept>(intercept => handler(ParseCopy(contract, intercept.Packet)))));

    /// <summary>
    /// Intercepts an outgoing message and parses each packet into <typeparamref name="T"/>
    /// before invoking the handler. The packet is copied first, so the original cannot be
    /// blocked or rewritten from this overload.
    /// </summary>
    /// <typeparam name="T">The message model to parse the packet as.</typeparam>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown from inside the handler's dispatch when the packet does not parse cleanly into
    /// <typeparamref name="T"/> or leaves trailing bytes.
    /// </exception>
    public IDisposable OnOut<T>(string name, Action<T> handler) where T : IParserComposer<T> =>
        Track(InterceptOutgoingEvent(
            name,
            ClientType.None,
            intercept => handler(ParseCopy<T>(name, intercept.Packet))));

    /// <summary>
    /// Waits for the next packet with the given message name, in either direction, and parses
    /// it into <typeparamref name="T"/>. The packet is not blocked and still reaches its
    /// destination.
    /// </summary>
    /// <typeparam name="T">The message model to parse the packet as.</typeparam>
    /// <param name="name">The message name; resolved against both directions.</param>
    /// <param name="timeoutMs">How long to wait, in milliseconds.</param>
    /// <exception cref="OperationCanceledException">
    /// The timeout elapsed, or the script was stopped, before a matching packet arrived.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The packet did not parse cleanly into <typeparamref name="T"/> or left trailing bytes.
    /// </exception>
    public async Task<T> ReceiveAsync<T>(string name, int timeoutMs = 10000) where T : IParserComposer<T>
    {
        using IPacket packet = await ReceiveAsync(name, timeoutMs);
        PacketReader reader = packet.Reader();
        T message = reader.Parse<T>();
        if (reader.Available != 0)
            throw new InvalidOperationException($"Message '{name}' contains {reader.Available} unparsed bytes for model '{typeof(T).Name}'.");
        return message;
    }

    /// <summary>
    /// Subscribes to every chat message seen in the room: talk, shout and whisper, from users,
    /// bots and pets alike. The chat carries the speaker's room index rather than their name.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnChat(Action<AvatarChat> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Action<RoomChatEntry> guarded = Guarded<RoomChatEntry>(entry => handler(entry.Chat));
        return Track(Application.Subscribe(
            ApplicationMemberIds.RoomChatReceived,
            guarded));
    }

    /// <summary>
    /// Subscribes to room chat and resolves the speaker for the handler.
    /// </summary>
    /// <param name="handler">
    /// Receives the speaking avatar and the chat. The avatar is <see langword="null"/> when the
    /// chat's room index is not (or no longer) in the avatar list.
    /// </param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnChat(Action<Avatar?, AvatarChat> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Action<RoomChatEntry> guarded = Guarded<RoomChatEntry>(entry =>
            handler(
                Room.Capture(room =>
                    room.Generation == entry.RoomGeneration
                        ? room.AvatarByIndex(entry.SpeakerIndex)
                        : null),
                entry.Chat));
        return Track(Application.Subscribe(
            ApplicationMemberIds.RoomChatReceived,
            guarded));
    }

    /// <summary>
    /// Waits for the next packet with the given message name, in either direction, and returns
    /// a copy of it. The original is not blocked and still reaches its destination.
    /// </summary>
    /// <param name="name">The message name; resolved against both directions.</param>
    /// <param name="timeoutMs">How long to wait, in milliseconds.</param>
    /// <returns>
    /// A copy of the packet, positioned at the start. The caller owns it and should dispose it.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// The timeout elapsed, or the script was stopped, before a matching packet arrived.
    /// </exception>
    public async Task<IPacket> ReceiveAsync(string name, int timeoutMs = 10000)
    {
        var completion = new TaskCompletionSource<IPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(Intercept intercept)
        {
            IPacket copy = intercept.Packet.Copy();
            if (!completion.TrySetResult(copy))
                copy.Dispose();
        }

        IDisposable incoming = InterceptIncoming(name, ClientType.None, Handler);
        IDisposable outgoing = InterceptOutgoing(name, ClientType.None, Handler);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(timeoutMs);
        await using CancellationTokenRegistration registration = timeout.Token.Register(() => completion.TrySetCanceled());

        try
        {
            return await completion.Task;
        }
        finally
        {
            incoming.Dispose();
            outgoing.Dispose();
        }
    }

    // High-level actions (field orders verified against the decompiled Flash composers).

    /// <summary>
    /// Says a message in the room, audible to everyone nearby. Fire-and-forget: it does not
    /// wait for the server to echo the chat back, and gives no indication when the server drops
    /// it for flood control or filtering.
    /// </summary>
    /// <param name="message">The text to say.</param>
    /// <param name="bubble">
    /// The chat-bubble style id; 0 is the account's default bubble. Styles beyond the default
    /// set require the corresponding club or item.
    /// </param>
    public void Talk(string message, int bubble = 0) =>
        Application.Invoke<RoomChatTalkRequest, RoomChatSendResult>(
            ApplicationMemberIds.RoomChatTalk,
            new RoomChatTalkRequest(message, bubble),
            Ct);

    /// <summary>Alias of <see cref="Talk"/>.</summary>
    public void Say(string message, int bubble = 0) => Talk(message, bubble);

    /// <summary>
    /// Shouts a message, which reaches the whole room instead of only nearby avatars.
    /// Fire-and-forget, on the same terms as <see cref="Talk"/>.
    /// </summary>
    /// <param name="message">The text to shout.</param>
    /// <param name="bubble">The chat-bubble style id; 0 is the account's default bubble.</param>
    public void Shout(string message, int bubble = 0) =>
        Application.Invoke<RoomChatShoutRequest, RoomChatSendResult>(
            ApplicationMemberIds.RoomChatShout,
            new RoomChatShoutRequest(message, bubble),
            Ct);

    /// <summary>
    /// Whispers to a single user in the room. Fire-and-forget; nothing is reported when the
    /// recipient is not present or has the sender ignored.
    /// </summary>
    /// <param name="recipient">The recipient's user name as shown in the room.</param>
    /// <param name="message">The text to whisper.</param>
    /// <param name="bubble">The chat-bubble style id; 0 is the account's default bubble.</param>
    /// <remarks>
    /// Flash puts the recipient and the text into one space-separated field, while Unity sends
    /// them as two fields. The room action selects the native layout for the session.
    /// </remarks>
    public void Whisper(string recipient, string message, int bubble = 0) =>
        Application.Invoke<RoomChatWhisperRequest, RoomChatWhisperResult>(
            ApplicationMemberIds.RoomChatWhisper,
            new RoomChatWhisperRequest(recipient, message, bubble),
            Ct);

    /// <summary>
    /// Requests a walk to the given tile. The server computes the path and may refuse or stop
    /// short; nothing is thrown and no completion is reported. Subscribe to
    /// <see cref="OnAvatarMoved"/> on the own avatar to observe the actual movement.
    /// </summary>
    public void Walk(int x, int y) =>
        Application.Invoke<RoomAvatarWalkRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarWalk,
            new RoomAvatarWalkRequest(x, y),
            Ct);

    /// <summary>
    /// Turns the avatar to face the given tile without moving. Ignored by the server while the
    /// avatar is walking.
    /// </summary>
    public void LookTo(int x, int y) =>
        Application.Invoke<RoomAvatarLookRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarLook,
            new RoomAvatarLookRequest(x, y),
            Ct);

    /// <summary>
    /// Starts dancing. Fire-and-forget; the server silently ignores it while the avatar is
    /// sitting or lying, and rejects club-only styles for accounts without a subscription.
    /// </summary>
    /// <param name="style">
    /// The dance style. 0 stops dancing; the client's dance menu offers styles 1 to 4, of which
    /// only 1 is available without Habbo Club.
    /// </param>
    public void Dance(int style = 1) =>
        Application.Invoke<RoomAvatarDanceRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarDance,
            new RoomAvatarDanceRequest(style),
            Ct);

    /// <summary>Stops dancing. Equivalent to <c>Dance(0)</c>.</summary>
    public void StopDancing() => Dance(0);

    /// <summary>
    /// Plays an avatar expression. Fire-and-forget.
    /// </summary>
    /// <param name="type">
    /// The expression id: 0 clears the current expression (and wakes an idle avatar), 1 wave,
    /// 2 blow a kiss, 3 laugh, 4 cry, 5 go idle, 6 jump, 7 thumbs up.
    /// </param>
    public void Expression(int type) =>
        Application.Invoke<RoomAvatarExpressionRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarExpression,
            new RoomAvatarExpressionRequest(type),
            Ct);

    /// <summary>Waves. Equivalent to <c>Expression(1)</c>.</summary>
    public void Wave() => Expression(1);

    /// <summary>
    /// Sits down on the current tile. Ignored by the server when the avatar is standing on
    /// furni that dictates its own posture.
    /// </summary>
    public void Sit() =>
        Application.Invoke<RoomAvatarPostureRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarPosture,
            new RoomAvatarPostureRequest(1),
            Ct);

    /// <summary>Stands up from a sitting or lying posture.</summary>
    public void Stand() =>
        Application.Invoke<RoomAvatarPostureRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarPosture,
            new RoomAvatarPostureRequest(0),
            Ct);

    /// <summary>
    /// Uses (clicks) a floor item. Fire-and-forget; no result is reported, and the server
    /// silently ignores it when the item is not interactive or the user lacks rights.
    /// </summary>
    /// <param name="id">The item's room id.</param>
    /// <param name="state">
    /// The interaction slot to trigger. 0 is the item's normal click action; multi-state furni
    /// use higher values for their additional actions.
    /// </param>
    public void UseFloorItem(Id id, int state = 0) =>
        Application.Invoke<RoomFloorItemUseRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemFloorUse,
            new RoomFloorItemUseRequest(id, state),
            Ct);

    /// <summary>
    /// Uses (clicks) a wall item. Fire-and-forget, on the same terms as
    /// <see cref="UseFloorItem(Id, int)"/>.
    /// </summary>
    /// <param name="id">The item's room id.</param>
    /// <param name="state">The interaction slot to trigger; 0 is the normal click action.</param>
    public void UseWallItem(Id id, int state = 0) =>
        Application.Invoke<RoomWallItemUseRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemWallUse,
            new RoomWallItemUseRequest(id, state),
            Ct);

    /// <summary>
    /// Moves a floor item that is already placed in the room to a new tile and rotation.
    /// Requires room rights; the server silently drops the request otherwise, and does not
    /// report a rejection when the target tile is occupied.
    /// </summary>
    /// <param name="id">The item's room id.</param>
    /// <param name="x">The target tile's x coordinate.</param>
    /// <param name="y">The target tile's y coordinate.</param>
    /// <param name="direction">The rotation, in eighths of a turn (0 to 7).</param>
    public void MoveFloorItem(Id id, int x, int y, int direction) =>
        Application.Invoke<RoomPlacementFloorMoveRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementFloorMove,
            new RoomPlacementFloorMoveRequest(
                id,
                new RoomPlacementFloorPosition(x, y, direction)),
            Ct);

    /// <summary>
    /// Holds up a sign above the avatar for a few seconds.
    /// </summary>
    /// <param name="type">
    /// The sign to show: 0 to 10 are the numbered signs, 11 a heart, 12 a skull, and 13 to 17
    /// the remaining picture signs.
    /// </param>
    public void Sign(int type) =>
        Application.Invoke<RoomAvatarSignRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarSign,
            new RoomAvatarSignRequest(type),
            Ct);

    /// <summary>Alias of <see cref="Walk(int, int)"/>.</summary>
    public void WalkTo(int x, int y) => Walk(x, y);

    /// <summary>Walks to the given tile. See <see cref="Walk(int, int)"/>.</summary>
    public void WalkTo(Tile tile) => Walk(tile.X, tile.Y);

    /// <summary>
    /// Walks toward the tile the avatar currently occupies. Since that tile is taken, the
    /// server normally stops on an adjacent tile.
    /// </summary>
    public void WalkTo(Avatar avatar) => Walk(avatar.X, avatar.Y);

    /// <summary>Alias of <see cref="LookTo(int, int)"/>.</summary>
    public void FaceTo(int x, int y) => LookTo(x, y);

    /// <summary>Turns to face the given avatar's current tile.</summary>
    public void FaceTo(Avatar avatar) => LookTo(avatar.X, avatar.Y);

    /// <summary>
    /// Leaves the current room. Fire-and-forget; subscribe to <see cref="OnLeftRoom"/> or
    /// <see cref="OnRoomExited"/> to know when the room session has actually ended.
    /// </summary>
    public void LeaveRoom() =>
        Application.Invoke<RoomLeaveRequest, RoomLifecycleDispatchResult>(
            ApplicationMemberIds.RoomLeave,
            new RoomLeaveRequest(),
            Ct);

    /// <summary>
    /// Asks the other user to open a trade. Fire-and-forget: the trade opens only if they
    /// accept and the room's trade mode allows it. Subscribe to <see cref="OnTradeOpened"/> and
    /// <see cref="OnTradeOpenFailed"/> for the outcome.
    /// </summary>
    /// <param name="userIndex">The other user's room index, not their user id.</param>
    public void OpenTrade(int userIndex) => OpenTrade(userIndex, null);

    private void OpenTrade(int user_index, Id? expected_user_id)
    {
        TradeStateView trade = ReadTradeState();
        Application.Invoke<TradeOpenRequest, TradeDispatchResult>(
            ApplicationMemberIds.TradeOpen,
            new TradeOpenRequest(
                user_index,
                trade.SessionGeneration,
                trade.Revision,
                trade.LatestEpoch,
                trade.RoomGeneration,
                expected_user_id),
            Ct);
    }

    /// <summary>Asks the given user to open a trade. See <see cref="OpenTrade(int)"/>.</summary>
    public void OpenTrade(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        OpenTrade(user.Index, user.Id);
    }

    /// <summary>Adds a single inventory item to the open trade offer.</summary>
    /// <param name="itemId">The inventory item id.</param>
    public void OfferTradeItem(long itemId) => OfferTradeItems(itemId);

    /// <summary>
    /// Adds several inventory items to the open trade offer in one message. Adding items resets
    /// both sides' acceptance.
    /// </summary>
    /// <param name="itemIds">The inventory item ids to offer.</param>
    public void OfferTradeItems(params long[] itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        TradeStateView trade = ReadTradeState();
        Application.Invoke<TradeItemsAddRequest, TradeDispatchResult>(
            ApplicationMemberIds.TradeItemsAdd,
            new TradeItemsAddRequest(
                itemIds.Select(item_id => (Id)item_id).ToArray(),
                trade.SessionGeneration,
                trade.Revision,
                trade.LatestEpoch),
            Ct);
    }

    /// <summary>Removes an item from the own trade offer, resetting both sides' acceptance.</summary>
    public void RemoveTradeItem(Id itemId)
    {
        TradeStateView trade = ReadTradeState();
        Application.Invoke<TradeItemRemoveRequest, TradeDispatchResult>(
            ApplicationMemberIds.TradeItemRemove,
            new TradeItemRemoveRequest(
                itemId,
                trade.SessionGeneration,
                trade.Revision,
                trade.LatestEpoch),
            Ct);
    }

    /// <summary>
    /// Accepts the current trade offer. This is the first of the two confirmation steps; the
    /// trade still needs <see cref="ConfirmTrade"/> from both sides afterwards.
    /// </summary>
    public void AcceptTrade() => SendTradeCommand(ApplicationMemberIds.TradeAccept);

    /// <summary>Withdraws a previous <see cref="AcceptTrade"/>, returning the trade to the offer phase.</summary>
    public void UnacceptTrade() => SendTradeCommand(ApplicationMemberIds.TradeUnaccept);

    /// <summary>
    /// Confirms the trade in the final phase, after both sides have accepted. The trade
    /// completes once both sides have confirmed.
    /// </summary>
    public void ConfirmTrade() => SendTradeCommand(ApplicationMemberIds.TradeConfirm);

    /// <summary>Cancels the trade for both participants.</summary>
    public void CancelTrade() => SendTradeCommand(ApplicationMemberIds.TradeClose);

    private Packet NewPacket(Direction direction, string name)
    {
        var identifier = new Identifier(ClientType.None, direction, name);
        if (!Ext.Messages.TryGetHeader(identifier, out Header header))
            throw new InvalidOperationException($"Unknown {(direction == Direction.Out ? "outgoing" : "incoming")} message '{name}'.");
        return new Packet(header, CurrentClient);
    }

    private void SendNamed(Direction direction, string name, object[] values, Header? preferred_header = null)
    {
        if (direction is Direction.In && CurrentClient is ClientType.Unity)
        {
            if (PreferredIncomingView(name, ClientType.None) is ClientType.Flash)
            {
                SendIncomingFlashValues(name, IncomingHeader(name), values);
                return;
            }

            using Packet unity_packet = NewPacket(direction, name);
            unity_packet.Writer().WriteValues(values);
            SendNativeUnityIncoming(name, unity_packet);
            return;
        }

        if (direction is Direction.Out && CurrentClient is ClientType.Unity)
        {
            UnityOutgoingMessage message = UnityOutgoingCompatibility.Translate(name, values);
            using Packet unity_packet = CreateUnityOutgoingPacket(message, preferred_header);
            Ext.Send(unity_packet);
            return;
        }

        using Packet packet = NewPacket(direction, name);
        packet.Writer().WriteValues(values);
        Ext.Send(packet);
    }

    private void SendNamed(Direction direction, MessageKey key, object[] values)
    {
        if (key.IsEmpty ||
            !Ext.Messages.Registry.TryGet(key, out MessageDescriptor? descriptor) ||
            descriptor.Direction != direction)
        {
            throw new InvalidOperationException($"Unknown semantic message '{key.Value}'.");
        }

        string name = descriptor.NameFor(CurrentClient) ??
            throw new NotSupportedException($"Message '{key.Value}' is unavailable for {CurrentClient}.");
        SendNamed(direction, name, values);
    }

    private Packet CreateUnityOutgoingPacket(UnityOutgoingMessage message, Header? preferred_header)
    {
        if (preferred_header is Header explicit_header)
        {
            if (explicit_header.Direction is not Direction.Out)
                throw new ArgumentException("The preferred Unity header must be outgoing.", nameof(preferred_header));
            try
            {
                return ComposeUnityOutgoing(explicit_header, message);
            }
            catch (Exception error) when (IsCompositionError(error))
            {
                IReadOnlyList<Header> resolved = ResolveUnityOutgoingHeaders(message);
                if (!resolved.Contains(explicit_header))
                    throw;
                Header[] alternatives = resolved.Where(header => header != explicit_header).ToArray();
                if (alternatives.Length == 0)
                    throw;
                return ComposeUnityOutgoingCandidates(alternatives, message, error);
            }
        }

        IReadOnlyList<Header> headers = ResolveUnityOutgoingHeaders(message);
        if (headers.Count == 1)
            return ComposeUnityOutgoing(headers[0], message);
        return ComposeUnityOutgoingCandidates(headers, message);
    }

    private IReadOnlyList<Header> ResolveUnityOutgoingHeaders(UnityOutgoingMessage message)
    {
        var headers = new List<Header>();
        foreach (string name in new[] { message.HeaderName, message.SchemaName }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var identifier = new Identifier(ClientType.None, Direction.Out, name);
            if (!Ext.Messages.TryGetHeaders(identifier, out IReadOnlyList<Header> resolved))
                continue;
            foreach (Header header in resolved)
                if (!headers.Contains(header))
                    headers.Add(header);
        }
        if (headers.Count == 0)
            throw new InvalidOperationException($"Unknown outgoing message '{message.HeaderName}'.");
        return headers;
    }

    private Packet ComposeUnityOutgoingCandidates(
        IReadOnlyList<Header> headers,
        UnityOutgoingMessage message,
        Exception? initial_error = null)
    {
        var candidates = new List<Packet>();
        var errors = new List<Exception>();
        if (initial_error is not null)
            errors.Add(initial_error);
        foreach (Header header in headers)
        {
            try
            {
                candidates.Add(ComposeUnityOutgoing(header, message));
            }
            catch (Exception error) when (IsCompositionError(error))
            {
                errors.Add(error);
            }
        }

        if (candidates.Count == 1)
            return candidates[0];
        foreach (Packet candidate in candidates)
            candidate.Dispose();
        if (candidates.Count > 1)
            throw new InvalidOperationException($"Unity message '{message.HeaderName}' matches multiple outgoing headers.");
        throw new InvalidOperationException(
            $"Unity message '{message.HeaderName}' does not match any outgoing header candidate.",
            errors.Count == 1 ? errors[0] : new AggregateException(errors));
    }

    private static bool IsCompositionError(Exception error) => error is
        ArgumentException or
        InvalidOperationException or
        NotSupportedException or
        OverflowException;

    private Packet ComposeUnityOutgoing(Header header, UnityOutgoingMessage message)
    {
        var packet = new Packet(header, ClientType.Unity);
        try
        {
            Ext.Messages.TryGetOutgoingSchemas(
                ClientType.Unity,
                header,
                out IReadOnlyList<OutgoingMessageSchema> schemas);
            UnityOutgoingCompatibility.Write(packet.Writer(), message, schemas);
            return packet;
        }
        catch
        {
            packet.Dispose();
            throw;
        }
    }

    private ClientType CurrentClient
    {
        get
        {
            ClientType client = Session?.Client ?? Ext.Messages.ActiveClient;
            return client is ClientType.None ? ClientType.Flash : client;
        }
    }

    private static T ParseCopy<T>(string name, IPacket packet) where T : IParserComposer<T>
    {
        using IPacket copy = packet.Copy();
        copy.Position = 0;
        PacketReader reader = copy.Reader();
        T message = reader.Parse<T>();
        if (reader.Available != 0)
            throw new InvalidOperationException($"Message '{name}' contains {reader.Available} unparsed bytes for model '{typeof(T).Name}'.");
        return message;
    }

    private static T ParseCopy<T>(MessageContract<T> contract, IPacket packet)
        where T : IParserComposer<T>
    {
        using IPacket copy = packet.Copy();
        copy.Position = 0;
        PacketReader reader = copy.Reader();
        T message = contract.Parse(in reader);
        if (reader.Available != 0)
        {
            throw new InvalidOperationException(
                $"Message '{contract.Key}' contains {reader.Available} unparsed bytes for model '{typeof(T).Name}'.");
        }
        return message;
    }

    private ClientType PreferredIncomingView(string name, ClientType requested)
    {
        if (requested != ClientType.None)
            return requested;

        bool unity = HasMessage(ClientType.Unity, Direction.In, name);
        bool flash = HasMessage(ClientType.Flash, Direction.In, name);
        return unity && !flash ? ClientType.Unity : ClientType.Flash;
    }

    private IDisposable InterceptIncoming(string name, ClientType requested, Action<Intercept> handler)
    {
        var identifier = new Identifier(requested, Direction.In, name);
        return Ext.Intercept(identifier, IncomingCallback(name, requested, handler));
    }

    private IDisposable InterceptIncomingEvent(string name, ClientType requested, Action<Intercept> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var identifier = new Identifier(requested, Direction.In, name);
        return Ext.Intercept(identifier, Guarded(IncomingCallback(name, requested, handler)));
    }

    private Action<Intercept> IncomingCallback(
        string name,
        ClientType requested,
        Action<Intercept> handler) =>
        intercept =>
        {
            if (PreferredIncomingView(name, requested) is ClientType.Flash)
                UnityIncomingCompatibility.Invoke(name, intercept, handler);
            else
                handler(intercept);
        };

    private ClientType PreferredOutgoingView(string name, ClientType requested)
    {
        if (requested != ClientType.None)
            return requested;

        bool unity = HasMessage(ClientType.Unity, Direction.Out, name);
        bool flash = HasMessage(ClientType.Flash, Direction.Out, name);
        return unity && !flash ? ClientType.Unity : ClientType.Flash;
    }

    private IDisposable InterceptOutgoing(string name, ClientType requested, Action<Intercept> handler)
        => BindOutgoing(name, requested, OutgoingCallback(name, requested, handler));

    private IDisposable InterceptOutgoingEvent(string name, ClientType requested, Action<Intercept> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BindOutgoing(name, requested, Guarded(OutgoingCallback(name, requested, handler)));
    }

    private Action<Intercept> OutgoingCallback(
        string name,
        ClientType requested,
        Action<Intercept> handler) =>
        intercept =>
        {
            if (PreferredOutgoingView(name, requested) is ClientType.Flash)
                UnityOutgoingInterception.Invoke(name, intercept, handler, Ext.Messages);
            else
                handler(intercept);
        };

    private IDisposable BindOutgoing(string name, ClientType requested, Action<Intercept> callback)
    {
        var identifier = new Identifier(requested, Direction.Out, name);
        var subscriptions = new List<IDisposable>();
        var identifiers = new HashSet<Identifier>();
        if (identifiers.Add(identifier))
            subscriptions.Add(Ext.Intercept(identifier, callback));

        foreach (string unity_name in UnityOutgoingInterception.AdditionalUnityNames(name))
        {
            var unity_identifier = new Identifier(ClientType.Unity, Direction.Out, unity_name);
            if (identifiers.Add(unity_identifier))
                subscriptions.Add(Ext.Intercept(unity_identifier, callback));
        }

        return new Unsubscriber(() =>
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
        });
    }

    private bool HasMessage(ClientType client, Direction direction, string name) =>
        Ext.Messages.HasCatalog(client)
            ? Ext.Messages.HasMessage(client, direction, name)
            : Ext.Messages.Map.TryGetEntry(client, direction, name, out _);

    private void InvokeHeaderIntercept(Header header, Intercept intercept, Action<Intercept> handler)
    {
        if (CurrentClient is not ClientType.Unity ||
            !Ext.Messages.TryGetIdentifier(header, out Identifier identifier))
        {
            handler(intercept);
            return;
        }

        string name = identifier.Name;
        if (Ext.Messages.Map.TryTranslate(ClientType.Unity, ClientType.Flash, header.Direction, name, out string flash_name))
            name = flash_name;

        if (header.Direction is Direction.In && PreferredIncomingView(name, ClientType.None) is ClientType.Flash)
            UnityIncomingCompatibility.Invoke(name, intercept, handler);
        else if (header.Direction is Direction.Out && PreferredOutgoingView(name, ClientType.None) is ClientType.Flash)
            UnityOutgoingInterception.Invoke(name, intercept, handler, Ext.Messages);
        else
            handler(intercept);
    }

    private sealed class Unsubscriber(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    private sealed class TrackedSubscription(
        IDisposable subscription,
        Action<TrackedSubscription> untrack) : IDisposable
    {
        private IDisposable? _subscription = subscription;

        public void Dispose()
        {
            IDisposable? current = Interlocked.Exchange(ref _subscription, null);
            if (current is null)
                return;
            untrack(this);
            current.Dispose();
        }
    }

}
