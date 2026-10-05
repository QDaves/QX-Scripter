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
using Qx.Platform;
using Qx.Protocol;
using Qx.Scripting.Hosting;

namespace Qx.Scripting;

/// <summary>
/// Represents the globals object that every QX script runs against.
/// </summary>
/// <remarks>
/// <para>
/// All public members are in scope unqualified inside a <c>.csx</c> script, so
/// <c>Talk("hi")</c> and <c>Room.RoomId</c> work without any receiver.
/// </para>
/// <para>
/// <b>State properties</b> such as <see cref="Users"/>, <see cref="FloorItems"/> or
/// <see cref="Credits"/> read whatever the interceptor has observed on the wire so far.
/// Nothing on this class polls the server on its own: if the game client never asked for a
/// piece of state, the corresponding property stays empty or zero rather than blocking. The
/// <c>Is...Loaded</c> flags distinguish "empty" from "not yet received". Collections are
/// snapshots taken per read, while the objects inside them are live and keep updating.
/// </para>
/// <para>
/// <b>The signature tells what a method does.</b> A method with a plain return value, such as
/// <see cref="GetUser(string)"/> or <see cref="GetFloorItem"/>, reads local state and sends
/// nothing. A method that returns a <see cref="Task"/> and takes a <c>timeoutMs</c> waits: a
/// request such as <see cref="GetProfile"/> or <see cref="SearchRooms"/> sends a message and
/// awaits the reply, while <c>Wait...</c> and <c>Receive...</c> wait for the room or for a
/// packet. A <c>Request...</c> method sends a request and returns at once; the answer arrives as
/// an event and in the state properties. An <c>Ensure...Loaded</c> method requests a piece of
/// state only when it is not complete yet, and returns it. A method named <c>...Async</c> awaits
/// what the method without the suffix only sends or blocks for: <see cref="EnterRoomAsync"/> and
/// <see cref="BuyMarketplaceOfferAsync"/> await the hotel's answer to
/// <see cref="EnterRoom(Id)"/> and <see cref="BuyMarketplaceOffer"/>, and
/// <see cref="ReceiveAsync(string, int)"/> awaits the packet <see cref="Receive(string[])"/>
/// blocks for.
/// </para>
/// <para>
/// <b>Events.</b> Every <c>On...</c> method returns an <see cref="IDisposable"/> handle.
/// Disposing it unsubscribes that one handler; all handles are disposed automatically when the
/// script stops, so a script that runs to completion never has to unsubscribe. Discarding the
/// handle does not unsubscribe. Handlers run on the interceptor's dispatch thread in packet
/// order and must not block for long. An exception thrown by a handler does not stop the other
/// handlers of the same event; it is reported as a script error, which stops the run in the QX
/// host. Callbacks that carry a before/after pair always pass the subject first, then the
/// previous value, then the new one.
/// </para>
/// <para>
/// <b>Requests</b> give up after <c>timeoutMs</c> milliseconds, which for requests that retry is a
/// total budget across the automatic retry. Whether the reply is also delivered to the game client
/// depends on the request and is stated on each member. Every call goes to the server again, except
/// where a member says it answers from a cache. They throw
/// <see cref="Qx.Game.RequestTimeoutException"/> on timeout,
/// <see cref="Qx.Game.RequestDisconnectedException"/> when the connection drops while waiting,
/// <see cref="OperationCanceledException"/> when the script is stopped, and
/// <see cref="NotSupportedException"/> where the connected client build cannot express the request.
/// </para>
/// <para>
/// <b>Actions</b> (<see cref="Walk(int, int)"/>, <see cref="Talk"/>, <see cref="PickupFurni(FloorItem, bool)"/>
/// and the rest) are fire-and-forget: they compose one packet and return. They never confirm
/// success, and the server silently ignores requests that fail on rights, flood limits or a
/// missing target. Observe the matching event instead. Actions do check their local
/// preconditions first and throw <see cref="InvalidOperationException"/> when one is not met,
/// for example when there is no active hotel session or no ready room.
/// </para>
/// <para>
/// <b>Raw messages.</b> Message names are plain strings resolved against the catalog loaded for
/// the active session; the compile-checked constants live on <see cref="Msg"/> in
/// <c>QX.Protocol</c>. A name that cannot be resolved throws when sending, but binds nothing
/// when intercepting. A constant name that is neither in the message registry nor in the
/// catalog is reported as warning QX1001 when the script compiles, and any such name writes one
/// warning line to the script output when a handler or a wait is registered for it.
/// </para>
/// </remarks>
public partial class ScriptGlobals : IDisposable
{
    private readonly Action<string> _log;
    private readonly CancellationToken _cancellationToken;
    private readonly Action<Exception>? _backgroundError;
    private readonly Action? _backgroundFinishedCallback;
    private readonly KeyboardReader _hostKeyboard;
    private readonly IInterceptor _interceptor;
    private readonly IApplicationRuntime _application;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptGlobals"/> class.
    /// </summary>
    /// <remarks>
    /// Instances created through this constructor have no keyboard access, so
    /// <see cref="Keyboard"/> reports the keyboard as unsupported.
    /// </remarks>
    /// <param name="extension">The interceptor the script sends and intercepts messages through.</param>
    /// <param name="game">The shared game state that backs the state properties.</param>
    /// <param name="application">The application runtime that serves state views and commands.</param>
    /// <param name="log">The callback that receives every line written with <see cref="Log(object)"/>.</param>
    /// <param name="cancellationToken">The token that is canceled when the script is stopped.</param>
    /// <param name="backgroundError">The callback that receives exceptions thrown by background tasks, or <see langword="null"/> to write them to the log.</param>
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
            null,
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
        Action? backgroundFinished,
        KeyboardReader? keyboard)
    {
        _interceptor = extension;
        _application = application;
        Ext = new ScopedInterceptor(extension, this);
        Application = new ScopedApplication(application, this);
        Ui = new ScriptUi(Guarded);
        _hostKeyboard = keyboard ?? NoKeyboard;
        Game = game;
        _log = log;
        _cancellationToken = cancellationToken;
        _backgroundError = backgroundError;
        _backgroundFinishedCallback = backgroundFinished;
    }

    /// <summary>
    /// Gets the interceptor the script is attached to, scoped to this run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prefer <see cref="OnIn(string, Action{Intercept})"/>,
    /// <see cref="OnOut(string, Action{Intercept})"/>,
    /// <see cref="OnIntercept(Header, Action{Intercept})"/>, <see cref="OnSessionStarted"/> and
    /// <see cref="OnSessionEnded"/>. Use this property for what they do not cover: intercepting by
    /// <see cref="MessageKey"/>, observing every packet through
    /// <see cref="IInterceptor.Intercepted"/>, and sends that must not cross into another session or
    /// catalog.
    /// </para>
    /// <para>
    /// Every callback and event handler registered through it is removed when the script stops, and
    /// an exception it throws is reported as a script error, the same as for the <c>On...</c>
    /// methods. A wait for the catalog that is given no cancelable token ends when the script stops.
    /// </para>
    /// <para>
    /// <see cref="IInterceptor.Messages"/> is a read-only resolver over the host's message manager,
    /// shared with the UI and every other script. It resolves names, keys and headers, and cannot bind
    /// or clear a catalog.
    /// </para>
    /// </remarks>
    public IInterceptor Ext { get; }

    /// <summary>
    /// Gets the shared game state tracker that backs the state properties on this class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is owned by the host and survives across script runs, so state observed before the
    /// script started is already present.
    /// </para>
    /// <para>
    /// Each of its feature properties, such as <see cref="GameState.Marketplace"/> or
    /// <see cref="GameState.Achievements"/>, is the live manager of that feature. A property of the
    /// same name on this class usually returns that same manager, as <see cref="Room"/> and
    /// <see cref="Quests"/> do. The exceptions are <see cref="Marketplace"/>, <see cref="Wired"/>
    /// and <see cref="Navigator"/>, which return a snapshot of the feature's state, and
    /// <see cref="Friends"/> and <see cref="Achievements"/>, which return the items themselves.
    /// </para>
    /// </remarks>
    public GameState Game { get; }

    /// <summary>
    /// Gets the application runtime that serves the state views, requests and commands behind
    /// the higher-level helpers, scoped to this run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Members are addressed by their string id, as listed in <see cref="ApplicationMemberIds"/>
    /// and <see cref="IApplicationRuntime.Members"/>.
    /// </para>
    /// <para>
    /// Every subscription made through it is removed when the script stops, and an exception its
    /// receiver throws is reported as a script error. An invocation that is given no cancelable
    /// token uses <see cref="Ct"/>, so it is canceled when the script stops.
    /// </para>
    /// </remarks>
    public IApplicationRuntime Application { get; }

    private ProfileStateView ReadProfileState() =>
        _application.Invoke<ProfileStateRequest, ProfileStateView>(
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
        _application.Invoke<InventoryStateRequest, InventoryStateView>(
            ApplicationMemberIds.InventoryState,
            new InventoryStateRequest(),
            Ct);

    private TradeStateView ReadTradeState() =>
        _application.Invoke<TradeStateRequest, TradeStateView>(
            ApplicationMemberIds.TradeState,
            new TradeStateRequest(),
            Ct);

    private void SendTradeCommand(string member_id)
    {
        TradeStateView trade = ReadTradeState();
        _application.Invoke<TradeCommandRequest, TradeDispatchResult>(
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
                    cancellationToken: cancellation_token)
                .Items
                .Select(LegacyInventoryItem)
                .ToArray());

    internal static IReadOnlyList<InventoryPet> ReadInventoryPetModels(
        IApplicationRuntime application,
        CancellationToken cancellation_token = default) =>
        Array.AsReadOnly(
            InventoryApplicationPages.ReadPets(
                    application,
                    cancellationToken: cancellation_token)
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
            RarityLevel = snapshot.RarityLevel
        };
        if (pet.FigureString != snapshot.FigureString)
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
    /// Gets the panel a tab declares with <c>//@ui:</c> directives.
    /// </summary>
    /// <remarks>
    /// It holds the values the user entered, the button that started the run, the click and change
    /// handlers that keep the script alive after its body returns, and the sinks that write output
    /// boxes, tables, progress bars, status lines and toasts back to it. Outside panel mode every
    /// getter returns its fallback, <see cref="ScriptUi.Clicked"/> is always <see langword="false"/>,
    /// the writers do nothing, the handlers are never called and <see cref="ScriptUi.Confirm"/> and
    /// <see cref="ScriptUi.Prompt"/> answer at once rather than wait.
    /// </remarks>
    public ScriptUi Ui { get; }

    /// <summary>
    /// Gets the live tracker of the current room session.
    /// </summary>
    /// <remarks>
    /// It covers room identity, entry and exit state, avatars, furni, the floor plan and the
    /// room events. It is present even when the user is outside a room, in which case its
    /// collections are empty and <see cref="RoomManager.RoomId"/> is 0.
    /// </remarks>
    public RoomManager Room => Game.Room;

    /// <summary>
    /// Gets a snapshot of the local user's profile state.
    /// </summary>
    /// <remarks>
    /// Every read builds a new <see cref="ProfileStateView"/> from the application runtime. The
    /// identity is <see langword="null"/> until the user data has been received.
    /// </remarks>
    public ProfileStateView Profile => ReadProfileState();

    /// <summary>Gets the tracker of the badges owned and the badge slots currently equipped.</summary>
    public BadgeInventoryManager BadgeInventory => Game.Badges;

    /// <summary>
    /// Gets a snapshot of the trade state.
    /// </summary>
    /// <remarks>
    /// Every read builds a new <see cref="TradeStateView"/> from the application runtime.
    /// <see cref="TradeStateView.Active"/> is <see langword="null"/> when no trade is open.
    /// </remarks>
    public TradeStateView Trade => ReadTradeState();

    /// <summary>Gets the tracker of quest and campaign state.</summary>
    public QuestManager Quests => Game.Quests;

    /// <summary>
    /// Gets a snapshot of the marketplace state with the first page of up to 100 cached entries.
    /// </summary>
    /// <remarks>
    /// Every read builds a new view. Use <see cref="GetMarketplaceStatePage(int, int)"/> to page
    /// through larger results.
    /// </remarks>
    public MarketplaceStateView Marketplace => GetMarketplaceStatePage();

    /// <summary>Gets the tracker of the crafting (alchemy) session state.</summary>
    public CraftingManager Crafting => Game.Crafting;

    /// <summary>Gets the tracker of received gifts and gift opening results.</summary>
    public GiftManager Gifts => Game.Gifts;

    /// <summary>Gets the tracker of Habbo Club and subscription state for the local user.</summary>
    public SubscriptionManager Subscriptions => Game.Subscriptions;

    /// <summary>
    /// Gets the tracker of group forum state such as thread lists, threads and moderation results.
    /// </summary>
    public ForumManager Forums => Game.Forums;

    /// <summary>
    /// Gets the cancellation token that is canceled when the script is stopped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pass it to every awaited call that accepts one. Awaiting it, or calling
    /// <see cref="Sleep(int)"/> or <see cref="Delay(int)"/>, throws
    /// <see cref="OperationCanceledException"/> once the script is asked to stop.
    /// </para>
    /// <para>
    /// Inside a script body this resolves to the ambient per-run token; outside a run it falls
    /// back to the token the globals were constructed with.
    /// </para>
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
    /// Unsubscribes every handler this instance is still holding.
    /// </summary>
    /// <remarks>
    /// The host calls it when the script run ends. After disposal no new subscriptions or
    /// background tasks are accepted.
    /// </remarks>
    void IDisposable.Dispose() => Close();

    internal void Close()
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
    /// Waits for the background work of the script to finish.
    /// </summary>
    /// <remarks>
    /// It covers tasks started by <see cref="RunTask(Action)"/> and event handlers that are still
    /// running. The host calls it during shutdown; scripts rarely need it. Once the script has
    /// been stopped, no new background work is accepted.
    /// </remarks>
    /// <param name="timeoutMs">The time to wait, in milliseconds.</param>
    /// <returns>
    /// <see langword="true"/> when every background task completed (or none was running);
    /// <see langword="false"/> when the timeout elapsed first. It does not throw on timeout.
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
    /// Gets whether the interceptor has an active hotel session.
    /// </summary>
    /// <remarks>
    /// Sending a raw packet while it is <see langword="false"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </remarks>
    public bool IsConnected => _interceptor.IsConnected;

    /// <summary>
    /// Gets the active hotel session, or <see langword="null"/> when there is none.
    /// </summary>
    /// <remarks>
    /// The session carries the host, port, hotel version and client identifier of the intercepted
    /// connection.
    /// </remarks>
    public Session? Session => _interceptor.Session;

    /// <summary>
    /// Gets the local user's account data, or <see langword="null"/> until the server has sent it.
    /// </summary>
    /// <remarks>
    /// It holds the id, name, figure, gender, motto, respect counters and account flags. Every
    /// read builds a new <see cref="UserData"/> from <see cref="Profile"/>, so the returned object
    /// does not change afterwards.
    /// </remarks>
    public UserData? SelfProfile => LegacyProfile(Profile.Identity);

    /// <summary>
    /// Gets the local user's avatar in the current room, or <see langword="null"/> when it is not
    /// in the room.
    /// </summary>
    /// <remarks>
    /// It is also <see langword="null"/> before the avatar list has loaded or before the own user
    /// id is known. The object is live: its position, dance, effect and idle state are updated in
    /// place as packets arrive.
    /// </remarks>
    public User? SelfAvatar => Room.Self as User;

    /// <summary>
    /// Gets every avatar currently in the room, including users, bots and pets.
    /// </summary>
    /// <remarks>
    /// A snapshot is taken on each read, so the sequence does not change while it is enumerated,
    /// but the <see cref="Avatar"/> objects in it are live and keep updating. It is empty when
    /// outside a room or before the avatar list has arrived.
    /// </remarks>
    public IEnumerable<Avatar> Avatars => Room.Avatars;

    /// <summary>
    /// Gets the users in the room, including the local user.
    /// </summary>
    /// <remarks>
    /// It is <see cref="Avatars"/> filtered to <see cref="User"/>. A snapshot is taken on each
    /// read; the elements are live.
    /// </remarks>
    public IEnumerable<User> Users => Room.Avatars.OfType<User>();

    /// <summary>
    /// Gets the pets in the room.
    /// </summary>
    /// <remarks>
    /// It is <see cref="Avatars"/> filtered to <see cref="Pet"/>. A snapshot is taken on each
    /// read; the elements are live.
    /// </remarks>
    public IEnumerable<Pet> Pets => Room.Avatars.OfType<Pet>();

    /// <summary>
    /// Gets the bots in the room.
    /// </summary>
    /// <remarks>
    /// It is <see cref="Avatars"/> filtered to <see cref="Bot"/>. A snapshot is taken on each
    /// read; the elements are live.
    /// </remarks>
    public IEnumerable<Bot> Bots => Room.Avatars.OfType<Bot>();

    /// <summary>
    /// Gets every floor item currently placed in the room.
    /// </summary>
    /// <remarks>
    /// A snapshot is taken on each read; the <see cref="FloorItem"/> objects are live and their
    /// location and state keep updating. It is empty until the server has sent the object list.
    /// Check <see cref="RoomManager.FloorItemsAreLoaded"/> to tell an empty room from one that
    /// has not loaded.
    /// </remarks>
    public IEnumerable<FloorItem> FloorItems => Room.FloorItems;

    /// <summary>
    /// Gets every wall item currently placed in the room.
    /// </summary>
    /// <remarks>
    /// It follows the same rules as <see cref="FloorItems"/>. Check
    /// <see cref="RoomManager.WallItemsAreLoaded"/> to tell an empty room from one that has not
    /// loaded.
    /// </remarks>
    public IEnumerable<WallItem> WallItems => Room.WallItems;

    /// <summary>
    /// Gets every item in the room, floor items followed by wall items, as one sequence of the
    /// shared base type.
    /// </summary>
    /// <remarks>Concatenates <see cref="FloorItems"/> and <see cref="WallItems"/>.</remarks>
    public IEnumerable<Furni> Furni => FloorItems.Cast<Furni>().Concat(WallItems);

    /// <summary>
    /// Gets the furni currently held in the inventory.
    /// </summary>
    /// <remarks>
    /// It is empty until the inventory has been requested; call
    /// <see cref="EnsureInventoryLoaded"/> first, or check <see cref="IsInventoryLoaded"/>. Every
    /// read builds a new snapshot.
    /// </remarks>
    public IEnumerable<InventoryItem> InventoryItems => ReadInventoryItems(_application, Ct);

    /// <summary>
    /// Gets the pets currently held in the inventory.
    /// </summary>
    /// <remarks>
    /// It is empty until the pet inventory has been requested; call
    /// <see cref="EnsurePetInventoryLoaded"/> first, or check <see cref="IsPetInventoryLoaded"/>.
    /// Every read builds a new snapshot.
    /// </remarks>
    public IEnumerable<InventoryPet> InventoryPets => ReadInventoryPetModels(_application, Ct);

    /// <summary>
    /// Gets the friend list.
    /// </summary>
    /// <remarks>
    /// It is empty until the friend list has been received; call
    /// <see cref="EnsureFriendsLoaded"/> first, or check <see cref="IsFriendsLoaded"/>. A
    /// snapshot is taken on each read.
    /// </remarks>
    public IEnumerable<Friend> Friends => Game.Friends.Friends;

    /// <summary>
    /// Gets the first avatar standing on the given tile.
    /// </summary>
    /// <remarks>
    /// Only the tile the avatar occupies is considered, not the tiles a walk animation passes
    /// over.
    /// </remarks>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    /// <returns>The avatar, or <see langword="null"/> when no avatar stands on the tile.</returns>
    public Avatar? AvatarAt(int x, int y) => Room.Avatars.FirstOrDefault(a => a.X == x && a.Y == y);

    /// <summary>
    /// Finds an entry in the friend list by name, ignoring case.
    /// </summary>
    /// <param name="name">The friend's name.</param>
    /// <returns>
    /// The friend, or <see langword="null"/> when there is no such friend, which is also the
    /// result when the friend list has not been loaded yet.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    public Friend? FindFriend(string name) => Game.Friends.FriendByName(name);

    /// <summary>
    /// Gets whether the named user is in the friend list, ignoring case.
    /// </summary>
    /// <remarks>
    /// It returns <see langword="false"/> when the friend list has not been loaded yet, so call
    /// <see cref="EnsureFriendsLoaded"/> first when the answer must be authoritative.
    /// </remarks>
    /// <param name="name">The user name to look for.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    public bool IsFriend(string name) => Game.Friends.IsFriend(name);

    /// <summary>Gets whether a user is on the local user's friend list.</summary>
    /// <param name="id">The user's account id.</param>
    /// <returns>
    /// <see langword="true"/> when the friend list holds that id. The friend list has to have been
    /// received; before that the result is always <see langword="false"/>.
    /// </returns>
    public bool IsFriend(Id id) => Game.Friends.FriendById(id) is not null;

    /// <summary>Gets whether a user in the room is on the local user's friend list.</summary>
    /// <param name="user">The user; only its id is used.</param>
    /// <returns><see langword="true"/> when the friend list holds that user.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <see langword="null"/>.</exception>
    public bool IsFriend(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return IsFriend(user.Id);
    }

    /// <summary>
    /// Writes a line to the script's output log.
    /// </summary>
    /// <remarks>
    /// Values are rendered with <see cref="object.ToString"/>; <see langword="null"/> logs an
    /// empty line.
    /// </remarks>
    /// <param name="message">The value to write.</param>
    public void Log(object? message) => _log(message?.ToString() ?? "");

    /// <summary>
    /// Waits asynchronously for the given number of milliseconds, observing script cancellation.
    /// </summary>
    /// <param name="milliseconds">The time to wait, in milliseconds.</param>
    /// <returns>A task that completes after the delay.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped while waiting.</exception>
    public Task Delay(int milliseconds) => Task.Delay(milliseconds, Ct);

    /// <summary>
    /// Waits asynchronously for the given interval, observing script cancellation.
    /// </summary>
    /// <param name="duration">The time to wait.</param>
    /// <returns>A task that completes after the delay.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped while waiting.</exception>
    public Task Delay(TimeSpan duration) => Task.Delay(duration, Ct);

    /// <summary>
    /// Sends a raw packet to the server or the client, depending on the direction of its header.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Send(IPacket packet) => _interceptor.Send(packet);

    /// <summary>
    /// Sends an outgoing message to the server by its message name.
    /// </summary>
    /// <remarks>
    /// Each value is written in order: <see cref="int"/>, <see cref="string"/>,
    /// <see cref="bool"/>, <see cref="short"/>, <see cref="long"/>, <see cref="byte"/>,
    /// <see cref="float"/>, <see cref="double"/>, <see cref="char"/>, <see cref="Id"/>,
    /// <see cref="Length"/> and <see cref="IComposer"/> values are supported.
    /// </remarks>
    /// <param name="name">The outgoing message name, resolved against the active message catalog.</param>
    /// <param name="values">The values to write into the packet body.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the message name cannot be resolved, or there is no active hotel session.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when a value has an unsupported type.</exception>
    public void SendToServer(string name, params object[] values) => SendNamed(MessageDirection.Out, name, values);

    /// <summary>
    /// Sends an outgoing message to the server by its semantic message key.
    /// </summary>
    /// <remarks>
    /// The key resolves to a single header in the active catalog, as in
    /// <see cref="SendToServer{T}(MessageKey, T)"/>, and the values are written as in
    /// <see cref="SendToServer(string, object[])"/>.
    /// </remarks>
    /// <param name="key">The semantic key of an outgoing message.</param>
    /// <param name="values">The values to write into the packet body.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the key is empty, unknown, not an outgoing message, or has no header in the active catalog,
    /// or there is no active hotel session.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when a value has an unsupported type.</exception>
    public void SendToServer(MessageKey key, params object[] values) => SendNamed(MessageDirection.Out, key, values);

    /// <summary>
    /// Sends an outgoing message to the server, composed from a message model.
    /// </summary>
    /// <typeparam name="T">The message model type.</typeparam>
    /// <param name="key">The semantic key of an outgoing message.</param>
    /// <param name="message">The message model that writes the packet body.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the key is empty, unknown, not an outgoing message, or has no header in the active catalog,
    /// or there is no active hotel session.
    /// </exception>
    public void SendToServer<T>(MessageKey key, T message) where T : IComposer =>
        SendComposed(MessageDirection.Out, key, message);

    /// <summary>
    /// Sends an incoming message to the game client by its message name, as if the server had
    /// sent it.
    /// </summary>
    /// <remarks>
    /// The values are written as in <see cref="SendToServer(string, object[])"/>.
    /// </remarks>
    /// <param name="name">The incoming message name, resolved against the active message catalog.</param>
    /// <param name="values">The values to write into the packet body.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the message name cannot be resolved, or there is no active hotel session.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when a value has an unsupported type.</exception>
    public void SendToClient(string name, params object[] values) => SendNamed(MessageDirection.In, name, values);

    /// <summary>
    /// Sends an incoming message to the game client by its semantic message key, as if the server
    /// had sent it.
    /// </summary>
    /// <remarks>
    /// The key resolves to a single header in the active catalog, as in
    /// <see cref="SendToClient{T}(MessageKey, T)"/>, and the values are written as in
    /// <see cref="SendToServer(string, object[])"/>. The server does not receive the message.
    /// </remarks>
    /// <param name="key">The semantic key of an incoming message.</param>
    /// <param name="values">The values to write into the packet body.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the key is empty, unknown, not an incoming message, or has no header in the active catalog,
    /// or there is no active hotel session.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when a value has an unsupported type.</exception>
    public void SendToClient(MessageKey key, params object[] values) => SendNamed(MessageDirection.In, key, values);

    /// <summary>
    /// Sends an incoming message to the game client, composed from a message model, as if the
    /// server had sent it.
    /// </summary>
    /// <remarks>
    /// The server does not receive the message.
    /// </remarks>
    /// <typeparam name="T">The message model type.</typeparam>
    /// <param name="key">The semantic key of an incoming message.</param>
    /// <param name="message">The message model that writes the packet body.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the key is empty, unknown, not an incoming message, or has no header in the active catalog,
    /// or there is no active hotel session.
    /// </exception>
    public void SendToClient<T>(MessageKey key, T message) where T : IComposer =>
        SendComposed(MessageDirection.In, key, message);

    private void SendToClient<T>(MessageContract<T> contract, T message)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(message);

        if (!_interceptor.Messages.TryGetHeader(contract.Key, out Header header))
            throw new InvalidOperationException($"Unknown incoming semantic message '{contract.Key.Value}'.");

        using Packet packet = _interceptor.Messages.CreatePacket(header);
        PacketWriter writer = packet.Writer();
        contract.Compose(message, in writer);
        _interceptor.Send(packet);
    }

    /// <summary>
    /// Registers a handler that runs for every packet with the given header.
    /// </summary>
    /// <param name="header">The exact header to intercept, which also fixes the direction.</param>
    /// <param name="handler">
    /// The handler to call with each matching intercept. Call <see cref="Intercept.Block"/>
    /// inside it to stop the packet from reaching its destination, or replace
    /// <see cref="Intercept.Packet"/> to rewrite it. An exception thrown by the handler does not
    /// stop the other handlers for the same header; it is reported as a script error.
    /// </param>
    /// <returns>
    /// A handle that removes the handler when disposed. It is also disposed automatically when
    /// the script stops.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnIntercept(Header header, Action<Intercept> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_interceptor.Intercept(
            header,
            Guarded(handler)));
    }

    /// <summary>
    /// Registers a handler that runs for every packet matching the identifier's client,
    /// direction and message name.
    /// </summary>
    /// <remarks>
    /// While the identifier cannot be resolved against the active message catalog, the handler
    /// is bound to nothing and does not fire. It is resolved again whenever the catalog changes.
    /// A name that is neither in the message registry nor in the active catalog writes one warning
    /// line to the script output.
    /// </remarks>
    /// <param name="identifier">The direction and message name to intercept.</param>
    /// <param name="handler">
    /// The handler to call with each matching intercept. It can block or rewrite the packet.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identifier"/> has no message name or no direction.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnIntercept(Identifier identifier, Action<Intercept> handler) =>
        InterceptMessage(identifier, handler);

    /// <summary>
    /// Registers a handler that runs for every incoming packet of the named message.
    /// </summary>
    /// <remarks>
    /// While the name cannot be resolved against the active message catalog, the handler is
    /// bound to nothing and does not fire. It is resolved again whenever the catalog changes.
    /// A name that is neither in the message registry nor in the active catalog writes one warning
    /// line to the script output. Only packets that pass through the interceptor reach it; packets
    /// that scripts or QX send themselves do not.
    /// </remarks>
    /// <param name="name">The incoming message name.</param>
    /// <param name="handler">
    /// The handler to call with each matching intercept. It can block or rewrite the packet.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnIn(string name, Action<Intercept> handler) =>
        InterceptMessage(new Identifier(MessageDirection.In, name), handler);

    /// <summary>
    /// Registers a handler that runs for every outgoing packet of the named message.
    /// </summary>
    /// <remarks>
    /// While the name cannot be resolved against the active message catalog, the handler is
    /// bound to nothing and does not fire. It is resolved again whenever the catalog changes.
    /// A name that is neither in the message registry nor in the active catalog writes one warning
    /// line to the script output. Only packets that pass through the interceptor reach it; packets
    /// that scripts or QX send themselves do not.
    /// </remarks>
    /// <param name="name">The outgoing message name.</param>
    /// <param name="handler">
    /// The handler to call with each matching intercept. It can block or rewrite the packet.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnOut(string name, Action<Intercept> handler) =>
        InterceptMessage(new Identifier(MessageDirection.Out, name), handler);

    /// <summary>
    /// Registers a handler that runs for every incoming packet of the named message, parsed into
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// The packet is copied before parsing, so blocking or rewriting is not possible from this
    /// overload. Use <see cref="OnIn(string, Action{Intercept})"/> or
    /// <see cref="OnIn{T}(string, Action{T, Intercept})"/> for that. A packet that does not
    /// parse cleanly into <typeparamref name="T"/>, or leaves trailing bytes, raises an
    /// <see cref="InvalidOperationException"/> inside the dispatch, which is reported as a script
    /// error. A name is handled as in <see cref="OnIn(string, Action{Intercept})"/>.
    /// </remarks>
    /// <typeparam name="T">The message model to parse the packet as.</typeparam>
    /// <param name="name">The incoming message name.</param>
    /// <param name="handler">The handler to call with each parsed message.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty, or <typeparamref name="T"/> is a QX model and the
    /// message's contract parses it into another model.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnIn<T>(string name, Action<T> handler) where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(handler);
        return InterceptMessage(
            new Identifier(ModelDirections<T>(MessageDirection.In, name), name),
            intercept => handler(ParseCopy<T>(name, intercept.Packet)));
    }

    /// <summary>
    /// Registers a handler that runs for every incoming packet of the named message, with both
    /// the parsed message and the intercept.
    /// </summary>
    /// <remarks>
    /// The handler can read the typed message and still block the packet with
    /// <see cref="Intercept.Block"/>. The message is parsed from a copy of the packet. A name is
    /// handled as in <see cref="OnIn(string, Action{Intercept})"/>.
    /// </remarks>
    /// <typeparam name="T">The message model to parse the packet as.</typeparam>
    /// <param name="name">The incoming message name.</param>
    /// <param name="handler">The handler to call with each parsed message and its intercept.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty, or <typeparamref name="T"/> is a QX model and the
    /// message's contract parses it into another model.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnIn<T>(string name, Action<T, Intercept> handler) where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(handler);
        return InterceptMessage(
            new Identifier(ModelDirections<T>(MessageDirection.In, name), name),
            intercept => handler(ParseCopy<T>(name, intercept.Packet), intercept));
    }

    private IDisposable OnIn<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T> =>
        Track(_interceptor.Intercept(
            contract.Key,
            Guarded<Intercept>(intercept => handler(ParseCopy(contract, intercept.Packet)))));

    /// <summary>
    /// Registers a handler that runs for every outgoing packet of the named message, parsed into
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// The packet is copied before parsing, so the original cannot be blocked or rewritten from
    /// this overload. A packet that does not parse cleanly into <typeparamref name="T"/>, or
    /// leaves trailing bytes, raises an <see cref="InvalidOperationException"/> inside the
    /// dispatch, which is reported as a script error. A name is handled as in
    /// <see cref="OnOut(string, Action{Intercept})"/>.
    /// </remarks>
    /// <typeparam name="T">The message model to parse the packet as.</typeparam>
    /// <param name="name">The outgoing message name.</param>
    /// <param name="handler">The handler to call with each parsed message.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty, or <typeparamref name="T"/> is a QX model and the
    /// message's contract parses it into another model.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnOut<T>(string name, Action<T> handler) where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(handler);
        return InterceptMessage(
            new Identifier(ModelDirections<T>(MessageDirection.Out, name), name),
            intercept => handler(ParseCopy<T>(name, intercept.Packet)));
    }

    /// <summary>
    /// Registers a handler that runs for every outgoing packet of the named message, with both
    /// the parsed message and the intercept.
    /// </summary>
    /// <remarks>
    /// The handler can read the typed message and still block the packet with
    /// <see cref="Intercept.Block"/>. This is how a click in the room can be turned into a tile
    /// pick instead of a walk. A name is handled as in <see cref="OnOut(string, Action{Intercept})"/>.
    /// </remarks>
    /// <typeparam name="T">The message model to parse the packet as.</typeparam>
    /// <param name="name">The outgoing message name.</param>
    /// <param name="handler">The handler to call with each parsed message and its intercept.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty, or <typeparamref name="T"/> is a QX model and the
    /// message's contract parses it into another model.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnOut<T>(string name, Action<T, Intercept> handler) where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(handler);
        return InterceptMessage(
            new Identifier(ModelDirections<T>(MessageDirection.Out, name), name),
            intercept => handler(ParseCopy<T>(name, intercept.Packet), intercept));
    }

    /// <summary>
    /// Waits for the next packet with the given message name and parses it into
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The packet is not blocked and still reaches its destination.
    /// </para>
    /// <para>
    /// When <typeparamref name="T"/> is a QX model, only the directions whose contract parses the
    /// message into <typeparamref name="T"/> are watched, so
    /// <c>ReceiveAsync&lt;AvatarChat&gt;("Chat")</c> waits for the incoming chat and never for the
    /// script's own outgoing one; when no contract does, the directions without a contract are
    /// watched. A model the script defines watches both directions. An unknown name never matches
    /// and writes a warning line to the script output.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The message model to parse the packet as.</typeparam>
    /// <param name="name">The message name.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The parsed message.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or carries an <c>in:</c> or <c>out:</c> prefix,
    /// or <typeparamref name="T"/> is a QX model and the message's contracts parse it into other models.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the timeout elapsed, or the script was stopped, before a matching packet arrived.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the packet did not parse cleanly into <typeparamref name="T"/> or left trailing bytes.
    /// </exception>
    public async Task<T> ReceiveAsync<T>(string name, int timeoutMs = 10000) where T : IParserComposer<T>
    {
        Identifier identifier = ReceiveIdentifier(name);
        using IPacket packet = await Capture(
            [identifier with { Direction = ModelDirections<T>(MessageDirection.Both, name) }],
            timeoutMs,
            false);
        PacketReader reader = packet.Reader();
        T message = reader.Parse<T>();
        if (reader.Available != 0)
            throw new InvalidOperationException($"Message '{name}' contains {reader.Available} unparsed bytes for model '{typeof(T).Name}'.");
        return message;
    }

    /// <summary>
    /// Registers a handler that runs for every chat message seen in the room.
    /// </summary>
    /// <remarks>
    /// It covers talk, shout and whisper, from users, bots and pets alike. The chat carries the
    /// speaker's room index rather than their name.
    /// </remarks>
    /// <param name="handler">The handler to call with each chat message.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnChat(Action<AvatarChat> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Action<RoomChatEntry> guarded = Guarded<RoomChatEntry>(entry => handler(entry.Chat));
        return Track(_application.Subscribe(
            ApplicationMemberIds.RoomChatReceived,
            guarded));
    }

    /// <summary>
    /// Registers a handler that runs for every chat message in the room, with the speaking avatar
    /// resolved.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the speaking avatar and the chat. The avatar is
    /// <see langword="null"/> when the chat's room index is not (or no longer) in the avatar
    /// list, or the chat belongs to a previous room session.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
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
        return Track(_application.Subscribe(
            ApplicationMemberIds.RoomChatReceived,
            guarded));
    }

    /// <summary>
    /// Waits for the next packet with the given message name, in either direction, and returns
    /// a copy of it.
    /// </summary>
    /// <remarks>
    /// The original is not blocked and still reaches its destination. An unknown name never
    /// matches and writes a warning line to the script output.
    /// </remarks>
    /// <param name="name">The message name, resolved against both directions.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>
    /// A copy of the packet, positioned at the start. The caller owns it and should dispose it.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or carries an <c>in:</c> or <c>out:</c> prefix.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the timeout elapsed, or the script was stopped, before a matching packet arrived.
    /// </exception>
    public Task<IPacket> ReceiveAsync(string name, int timeoutMs = 10000) =>
        CaptureAny([name], timeoutMs, false);

    // High-level actions (field orders verified against the decompiled Flash composers).

    /// <summary>
    /// Says a message in the room, audible to everyone nearby.
    /// </summary>
    /// <remarks>
    /// It does not wait for the server to echo the chat back, and gives no indication when the
    /// server drops it for flood control or filtering.
    /// </remarks>
    /// <param name="message">The text to say.</param>
    /// <param name="bubble">
    /// The chat bubble style id; 0 is the account's default bubble. Styles beyond the default
    /// set require the corresponding club or item.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or no ready room.</exception>
    public void Talk(string message, int bubble = 0) =>
        _application.Invoke<RoomChatTalkRequest, RoomChatSendResult>(
            ApplicationMemberIds.RoomChatTalk,
            new RoomChatTalkRequest(message, bubble),
            Ct);

    /// <summary>
    /// Shouts a message, which reaches the whole room instead of only nearby avatars.
    /// </summary>
    /// <remarks>
    /// It follows the same rules as <see cref="Talk"/>.
    /// </remarks>
    /// <param name="message">The text to shout.</param>
    /// <param name="bubble">The chat bubble style id; 0 is the account's default bubble.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or no ready room.</exception>
    public void Shout(string message, int bubble = 0) =>
        _application.Invoke<RoomChatShoutRequest, RoomChatSendResult>(
            ApplicationMemberIds.RoomChatShout,
            new RoomChatShoutRequest(message, bubble),
            Ct);

    /// <summary>
    /// Whispers a message to one user in the room.
    /// </summary>
    /// <remarks>
    /// It follows the same rules as <see cref="Talk"/>: nothing confirms that the recipient
    /// received it.
    /// </remarks>
    /// <param name="recipient">The name of the user in the current room.</param>
    /// <param name="message">The text to whisper.</param>
    /// <param name="bubble">The chat bubble style id; 0 is the account's default bubble.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="recipient"/> or <paramref name="message"/> is empty or white space.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or no ready room.</exception>
    public void Whisper(string recipient, string message, int bubble = 0) =>
    _application.Invoke<RoomChatWhisperRequest, RoomChatWhisperResult>(
        ApplicationMemberIds.RoomChatWhisper,
        new RoomChatWhisperRequest(recipient, message, bubble),
        Ct);

    /// <summary>
    /// Requests a walk to the given tile.
    /// </summary>
    /// <remarks>
    /// The server computes the path and may refuse or stop short; no completion is reported.
    /// Subscribe to <see cref="OnAvatarMoved"/> on the own avatar to observe the actual movement.
    /// </remarks>
    /// <param name="x">The target tile x coordinate.</param>
    /// <param name="y">The target tile y coordinate.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Walk(int x, int y) =>
        _application.Invoke<RoomAvatarWalkRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarWalk,
            new RoomAvatarWalkRequest(x, y),
            Ct);

    /// <summary>Requests a walk to the given tile, as <see cref="Walk(int, int)"/> does.</summary>
    /// <remarks>A <see cref="Tile"/> converts to its point, so its height is ignored.</remarks>
    /// <param name="location">The target tile.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Walk(Point location) => Walk(location.X, location.Y);

    /// <summary>
    /// Requests a walk toward the tile the given avatar currently occupies.
    /// </summary>
    /// <remarks>
    /// Since that tile is taken, the server normally stops on an adjacent tile.
    /// </remarks>
    /// <param name="avatar">The avatar to walk toward.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="avatar"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Walk(Avatar avatar)
    {
        ArgumentNullException.ThrowIfNull(avatar);
        Walk(avatar.X, avatar.Y);
    }

    /// <summary>
    /// Turns the avatar to face the given tile without moving.
    /// </summary>
    /// <remarks>
    /// The server ignores it while the avatar is walking.
    /// </remarks>
    /// <param name="x">The tile x coordinate to face.</param>
    /// <param name="y">The tile y coordinate to face.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void LookTo(int x, int y) =>
        _application.Invoke<RoomAvatarLookRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarLook,
            new RoomAvatarLookRequest(x, y),
            Ct);

    /// <summary>Turns the avatar to face the given tile, as <see cref="LookTo(int, int)"/> does.</summary>
    /// <param name="location">The tile to face.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void LookTo(Point location) => LookTo(location.X, location.Y);

    /// <summary>Turns the avatar to face the tile the given avatar currently occupies.</summary>
    /// <param name="avatar">The avatar to face.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="avatar"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void LookTo(Avatar avatar)
    {
        ArgumentNullException.ThrowIfNull(avatar);
        LookTo(avatar.X, avatar.Y);
    }

    /// <summary>
    /// Turns the avatar to one of the eight compass directions.
    /// </summary>
    /// <remarks>
    /// The hotel has no "face direction" message, so the call aims at a far-off tile in that
    /// direction with <see cref="LookTo(int, int)"/> and lets the server work the facing out.
    /// </remarks>
    /// <param name="direction">
    /// 0 north, 1 north-east, 2 east, 3 south-east, 4 south, 5 south-west, 6 west, 7 north-west.
    /// Values outside 0-7 wrap, including negative ones.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Turn(int direction) => LookTo(DirectionTarget(direction));

    /// <summary>Turns the avatar to one of the eight compass directions.</summary>
    /// <param name="direction">The compass direction.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Turn(Direction direction) => Turn((int)direction);

    private static Point DirectionTarget(int direction)
    {
        int normalized = ((direction % 8) + 8) % 8;
        return normalized switch
        {
            0 => new Point(-1000, -10000),
            1 => new Point(1000, -10000),
            2 => new Point(10000, -1000),
            3 => new Point(10000, 1000),
            4 => new Point(1000, 10000),
            5 => new Point(-1000, 10000),
            6 => new Point(-10000, 1000),
            _ => new Point(-10000, -1000)
        };
    }

    /// <summary>
    /// Starts dancing.
    /// </summary>
    /// <remarks>
    /// The server ignores it while the avatar is sitting or lying, and rejects club-only styles
    /// for accounts without a subscription.
    /// </remarks>
    /// <param name="style">
    /// The dance style. 0 stops dancing; the client's dance menu offers styles 1 to 4, of which
    /// only 1 is available without Habbo Club.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Dance(int style = 1) =>
        _application.Invoke<RoomAvatarDanceRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarDance,
            new RoomAvatarDanceRequest(style),
            Ct);

    /// <summary>Stops dancing.</summary>
    /// <remarks>Equivalent to <c>Dance(0)</c>.</remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void StopDancing() => Dance(0);

    /// <summary>
    /// Plays an avatar expression.
    /// </summary>
    /// <param name="type">
    /// The expression id: 0 clears the current expression (and wakes an idle avatar), 1 wave,
    /// 2 blow a kiss, 3 laugh, 4 cry, 5 go idle, 6 jump, 7 thumbs up.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Expression(int type) =>
        _application.Invoke<RoomAvatarExpressionRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarExpression,
            new RoomAvatarExpressionRequest(type),
            Ct);

    /// <summary>Waves.</summary>
    /// <remarks>Equivalent to <c>Expression(1)</c>.</remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Wave() => Expression(1);

    /// <summary>
    /// Sits down on the current tile.
    /// </summary>
    /// <remarks>
    /// The server ignores it when the avatar is standing on furni that dictates its own posture.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Sit() =>
        _application.Invoke<RoomAvatarPostureRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarPosture,
            new RoomAvatarPostureRequest(1),
            Ct);

    /// <summary>Stands up from a sitting or lying posture.</summary>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Stand() =>
        _application.Invoke<RoomAvatarPostureRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarPosture,
            new RoomAvatarPostureRequest(0),
            Ct);

    /// <summary>
    /// Uses (clicks) a floor item.
    /// </summary>
    /// <remarks>
    /// No result is reported, and the server ignores it when the item is not interactive or the
    /// user lacks rights.
    /// </remarks>
    /// <param name="id">The item's room id.</param>
    /// <param name="state">
    /// The interaction slot to trigger. 0 is the item's normal click action; multi-state furni
    /// use higher values for their additional actions.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void UseFloorItem(Id id, int state = 0) =>
        _application.Invoke<RoomFloorItemUseRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemFloorUse,
            new RoomFloorItemUseRequest(id, state),
            Ct);

    /// <summary>
    /// Uses (clicks) a wall item.
    /// </summary>
    /// <remarks>
    /// It follows the same rules as <see cref="UseFloorItem(Id, int)"/>.
    /// </remarks>
    /// <param name="id">The item's room id.</param>
    /// <param name="state">The interaction slot to trigger; 0 is the normal click action.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void UseWallItem(Id id, int state = 0) =>
        _application.Invoke<RoomWallItemUseRequest, RoomItemDispatchResult>(
            ApplicationMemberIds.RoomItemWallUse,
            new RoomWallItemUseRequest(id, state),
            Ct);

    /// <summary>Uses (clicks) a floor item, as <see cref="UseFloorItem(Id, int)"/> does.</summary>
    /// <param name="item">The item; only its id is used.</param>
    /// <param name="state">The interaction slot to trigger; 0 is the normal click action.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void UseFloorItem(FloorItem item, int state = 0)
    {
        ArgumentNullException.ThrowIfNull(item);
        UseFloorItem(item.Id, state);
    }

    /// <summary>Uses (clicks) a wall item, as <see cref="UseWallItem(Id, int)"/> does.</summary>
    /// <param name="item">The item; only its id is used.</param>
    /// <param name="state">The interaction slot to trigger; 0 is the normal click action.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void UseWallItem(WallItem item, int state = 0)
    {
        ArgumentNullException.ThrowIfNull(item);
        UseWallItem(item.Id, state);
    }

    /// <summary>
    /// Uses (clicks) a room item, picking the floor or wall message from the item's runtime type.
    /// </summary>
    /// <param name="item">The item to use.</param>
    /// <param name="state">
    /// The interaction slot to trigger. 0 is the item's normal click action; multi-state furni
    /// use higher values to switch to a specific state instead of cycling.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the item is neither a floor nor a wall item.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void UseFurni(Furni item, int state = 0)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is FloorItem floor_item)
            UseFloorItem(floor_item.Id, state);
        else if (item is WallItem wall_item)
            UseWallItem(wall_item.Id, state);
        else
            throw new ArgumentException("Unsupported furniture type.", nameof(item));
    }

    /// <summary>
    /// Moves a floor item that is already placed in the room to a new tile and rotation.
    /// </summary>
    /// <remarks>
    /// It requires room rights; the server drops the request otherwise, and does not report a
    /// rejection when the target tile is occupied.
    /// </remarks>
    /// <param name="id">The item's room id.</param>
    /// <param name="x">The target tile x coordinate.</param>
    /// <param name="y">The target tile y coordinate.</param>
    /// <param name="direction">The rotation, in eighths of a turn (0 to 7).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a coordinate is negative or <paramref name="direction"/> is outside 0 to 7.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no active hotel session or ready room, or the item is not in the room.
    /// </exception>
    public void MoveFloorItem(Id id, int x, int y, int direction) =>
        _application.Invoke<RoomPlacementFloorMoveRequest, RoomPlacementDispatchReceipt>(
            ApplicationMemberIds.RoomPlacementFloorMove,
            new RoomPlacementFloorMoveRequest(
                id,
                new RoomPlacementFloorPosition(x, y, direction)),
            Ct);

    /// <summary>Moves a floor item in the room to another tile and rotation.</summary>
    /// <param name="id">The item's room id.</param>
    /// <param name="location">The target tile.</param>
    /// <param name="direction">The rotation, in eighths of a turn (0 to 7).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a coordinate is negative or <paramref name="direction"/> is outside 0 to 7.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no active hotel session or ready room, or the item is not in the room.
    /// </exception>
    public void MoveFloorItem(Id id, Point location, int direction) =>
        MoveFloorItem(id, location.X, location.Y, direction);

    /// <summary>Moves a floor item already in the room to another tile, keeping or changing its rotation.</summary>
    /// <param name="item">The item to move.</param>
    /// <param name="location">The target tile.</param>
    /// <param name="direction">
    /// The item's new rotation, 0-7, or <see langword="null"/> to keep the rotation
    /// <paramref name="item"/> holds.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a coordinate is negative or <paramref name="direction"/> is outside 0 to 7.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no active hotel session or ready room, or the item is not in the room.
    /// </exception>
    public void MoveFloorItem(FloorItem item, Point location, int? direction = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        MoveFloorItem(item.Id, location, direction ?? item.Direction);
    }

    /// <summary>
    /// Holds up a sign above the avatar for a few seconds.
    /// </summary>
    /// <param name="type">
    /// The sign to show: 0 to 10 are the numbered signs, 11 a heart, 12 a skull, and 13 to 17
    /// the remaining picture signs.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void Sign(int type) =>
        _application.Invoke<RoomAvatarSignRequest, RoomAvatarDispatchResult>(
            ApplicationMemberIds.RoomAvatarSign,
            new RoomAvatarSignRequest(type),
            Ct);

    /// <summary>
    /// Leaves the current room.
    /// </summary>
    /// <remarks>
    /// Subscribe to <see cref="OnLeftRoom(Action)"/> to know when the room session has actually
    /// ended.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void LeaveRoom() =>
        _application.Invoke<RoomLeaveRequest, RoomLifecycleDispatchResult>(
            ApplicationMemberIds.RoomLeave,
            new RoomLeaveRequest(),
            Ct);

    /// <summary>
    /// Asks another user in the room to open a trade.
    /// </summary>
    /// <remarks>
    /// The trade opens only if they accept and the room's trade mode allows it. Subscribe to
    /// <see cref="OnTradeOpened"/> and <see cref="OnTradeOpenFailed"/> for the outcome.
    /// </remarks>
    /// <param name="userIndex">The other user's room index, not their user id.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="userIndex"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no active hotel session or ready room, the index is not another user in the room,
    /// or a trade is already open.
    /// </exception>
    public void OpenTrade(int userIndex) => OpenTrade(userIndex, null);

    private void OpenTrade(int user_index, Id? expected_user_id)
    {
        TradeStateView trade = ReadTradeState();
        _application.Invoke<TradeOpenRequest, TradeDispatchResult>(
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

    /// <summary>Asks the given user to open a trade, as <see cref="OpenTrade(int)"/> does.</summary>
    /// <param name="user">The user in the current room to trade with.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no active hotel session or ready room, the user's room index no longer belongs
    /// to that user, the user is the local user, or a trade is already open.
    /// </exception>
    public void OpenTrade(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        OpenTrade(user.Index, user.Id);
    }

    /// <summary>Adds a single inventory item to the open trade offer.</summary>
    /// <param name="itemId">The inventory item id.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="itemId"/> is 0 or outside the 32-bit range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no trade is open in the offer phase.</exception>
    public void OfferTradeItem(Id itemId) => OfferTradeItems(itemId);

    /// <summary>
    /// Adds several inventory items to the open trade offer in one message.
    /// </summary>
    /// <remarks>
    /// Adding items resets both sides' acceptance.
    /// </remarks>
    /// <param name="itemIds">The distinct inventory item ids to offer.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="itemIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the list is empty, or an id is 0 or outside the 32-bit range.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the list contains the same id twice.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no trade is open in the offer phase.</exception>
    public void OfferTradeItems(params Id[] itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        TradeStateView trade = ReadTradeState();
        _application.Invoke<TradeItemsAddRequest, TradeDispatchResult>(
            ApplicationMemberIds.TradeItemsAdd,
            new TradeItemsAddRequest(
                itemIds,
                trade.SessionGeneration,
                trade.Revision,
                trade.LatestEpoch),
            Ct);
    }

    /// <summary>Removes an item from the own trade offer, resetting both sides' acceptance.</summary>
    /// <param name="itemId">The inventory item id to remove from the offer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="itemId"/> is 0 or outside the 32-bit range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no trade is open in the offer phase.</exception>
    public void RemoveTradeItem(Id itemId)
    {
        TradeStateView trade = ReadTradeState();
        _application.Invoke<TradeItemRemoveRequest, TradeDispatchResult>(
            ApplicationMemberIds.TradeItemRemove,
            new TradeItemRemoveRequest(
                itemId,
                trade.SessionGeneration,
                trade.Revision,
                trade.LatestEpoch),
            Ct);
    }

    /// <summary>Adds a single inventory item to the open trade offer.</summary>
    /// <param name="item">The inventory item; only its item id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the item id is 0 or outside the 32-bit range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no trade is open in the offer phase.</exception>
    public void OfferTradeItem(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        OfferTradeItem(item.ItemId);
    }

    /// <summary>Adds several inventory items to the open trade offer in one message, skipping null entries.</summary>
    /// <param name="items">The inventory items; only their item ids are used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when no item is left to offer, or an item id is 0 or outside the 32-bit range.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the same item is listed twice.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no trade is open in the offer phase.</exception>
    public void OfferTradeItems(IEnumerable<InventoryItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        OfferTradeItems(items
            .Where(item => item is not null)
            .Select(item => item.ItemId)
            .ToArray());
    }

    /// <summary>Removes an item from the own trade offer, resetting both sides' acceptance.</summary>
    /// <param name="item">The inventory item; only its item id is used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the item id is 0 or outside the 32-bit range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no trade is open in the offer phase.</exception>
    public void RemoveTradeItem(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        RemoveTradeItem(item.ItemId);
    }

    /// <summary>
    /// Accepts the current trade offer.
    /// </summary>
    /// <remarks>
    /// This is the first of the two confirmation steps; the trade still needs
    /// <see cref="ConfirmTrade"/> from both sides afterwards.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no trade is open in the offer phase, the local user cannot trade, or the required silver
    /// fee has not been reached.
    /// </exception>
    public void AcceptTrade() => SendTradeCommand(ApplicationMemberIds.TradeAccept);

    /// <summary>Withdraws a previous <see cref="AcceptTrade"/>, returning the trade to the offer phase.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no trade is open.</exception>
    public void UnacceptTrade() => SendTradeCommand(ApplicationMemberIds.TradeUnaccept);

    /// <summary>
    /// Confirms the trade in the final phase, after both sides have accepted.
    /// </summary>
    /// <remarks>
    /// The trade completes once both sides have confirmed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no trade is waiting for confirmation, the local user cannot trade, or the required silver
    /// fee has not been reached.
    /// </exception>
    public void ConfirmTrade() => SendTradeCommand(ApplicationMemberIds.TradeConfirm);

    /// <summary>Cancels the trade for both participants.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no trade is open.</exception>
    public void CancelTrade() => SendTradeCommand(ApplicationMemberIds.TradeClose);

    private Packet NewPacket(MessageDirection direction, string name)
    {
        var identifier = new Identifier(direction, name);
        if (!_interceptor.Messages.TryGetHeader(identifier, out Header header))
            throw new InvalidOperationException($"Unknown {(direction == MessageDirection.Out ? "outgoing" : "incoming")} message '{name}'.");
        return _interceptor.Messages.CreatePacket(header);
    }

    private void SendNamed(MessageDirection direction, string name, object[] values)
    {
        using Packet packet = NewPacket(direction, name);
        packet.Writer().WriteValues(values);
        _interceptor.Send(packet);
    }

    private void SendNamed(MessageDirection direction, MessageKey key, object[] values)
    {
        using Packet packet = NewPacket(direction, key);
        packet.Writer().WriteValues(values);
        _interceptor.Send(packet);
    }

    private void SendComposed<T>(MessageDirection direction, MessageKey key, T message) where T : IComposer
    {
        ArgumentNullException.ThrowIfNull(message);
        using Packet packet = NewPacket(direction, key);
        packet.Writer().Compose(message);
        _interceptor.Send(packet);
    }

    private Packet NewPacket(MessageDirection direction, MessageKey key)
    {
        if (key.IsEmpty ||
            !_interceptor.Messages.Registry.TryGet(key, out MessageDescriptor? descriptor) ||
            descriptor.Direction != direction)
        {
            throw new InvalidOperationException(
                $"Unknown {(direction == MessageDirection.Out ? "outgoing" : "incoming")} semantic message '{key.Value}'.");
        }
        if (!_interceptor.Messages.TryGetHeader(key, out Header header))
            throw new InvalidOperationException($"Message '{key.Value}' has no header in the active catalog.");
        return _interceptor.Messages.CreatePacket(header);
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
