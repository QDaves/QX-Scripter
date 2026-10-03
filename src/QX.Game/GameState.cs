using Qx.Game.Application;
using Qx.Interception;
using Qx.Model;

namespace Qx.Game;

/// <summary>
/// Represents the mirrored state of the hotel session and holds every state manager that scripts read.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Attach(IInterceptor)"/> connects every manager to the interceptor. On each hotel
/// connection the game data for that hotel is loaded and <see cref="BootstrapTask"/> loads the
/// user's profile and wallet.
/// </para>
/// </remarks>
public sealed class GameState : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _profile_operations_sync = new();
    private readonly object _room_chat_operations_sync = new();
    private readonly object _room_avatar_operations_sync = new();
    private readonly object _room_control_operations_sync = new();
    private readonly object _friend_operations_sync = new();
    private readonly object _remote_people_operations_sync = new();
    private readonly object _wallet_operations_sync = new();
    private readonly object _game_data_sync = new();
    private TaskCompletionSource<IProfileOperations> _profile_operations_ready = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<IWalletOperations> _wallet_operations_ready = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private IInterceptor? _interceptor;
    private Action<Session>? _connected;
    private Action? _game_data_room_loaded;
    private Action? _game_data_achievements_loaded;
    private Action? _game_data_habbicons_loaded;
    private Action? _game_data_leaderboards_loaded;
    private GameDataSessionScope? _game_data_scope;
    private IProfileOperations? _profile_operations;
    private IRoomChatOperations? _room_chat_operations;
    private IRoomAvatarOperations? _room_avatar_operations;
    private IRoomControlOperations? _room_control_operations;
    private IFriendOperations? _friend_operations;
    private IRemotePeopleOperations? _remote_people_operations;
    private IWalletOperations? _wallet_operations;
    private Task _bootstrap_task = Task.CompletedTask;
    private Exception? _bootstrap_error;
    private long _session_generation;
    private bool _disposed;

    /// <summary>Gets the manager that mirrors the room the user is in.</summary>
    public RoomManager Room { get; } = new();
    /// <summary>Gets the actions the user performs in rooms, such as walking, chatting and using furni.</summary>
    public RoomActions RoomActions { get; } = new();
    internal RoomBanManager RoomBans { get; } = new();
    internal RoomSettingsManager RoomSettings { get; } = new();
    /// <summary>Gets the actions that target users, pets and bots in the current room.</summary>
    public RoomPeopleActions People { get; } = new();

    /// <summary>
    /// Gets the log of users who have been in the current room since the user entered it.
    /// </summary>
    /// <remarks>
    /// No hotel message reports past visitors, so the log is built by watching avatars arrive and
    /// leave. It is cleared when the user leaves the room.
    /// </remarks>
    public RoomVisitorLog Visitors { get; } = new();

    /// <summary>Gets the coordinator that enters rooms and waits for the entry to finish.</summary>
    public RoomEntryCoordinator RoomEntries { get; }
    internal ProfileManager Profile { get; } = new();
    internal InventoryManager Inventory { get; } = new();
    /// <summary>Gets the manager for the user's owned badges and the badges users wear.</summary>
    public BadgeInventoryManager Badges { get; } = new();
    /// <summary>Gets the manager for the user's friend list and private messages.</summary>
    public FriendManager Friends { get; } = new();
    internal TradeManager Trade { get; } = new();
    internal PollManager Polls { get; } = new();
    /// <summary>Gets the broker that sends raw requests and waits for their responses.</summary>
    public RequestBroker Requests { get; } = new();
    /// <summary>Gets the manager for marketplace offers, searches and configuration.</summary>
    public MarketplaceManager Marketplace { get; }

    /// <summary>Gets the service that copies what another avatar is doing onto the user's own avatar.</summary>
    public MimicService Mimic { get; }
    internal EconomyManager Economy { get; } = new();
    /// <summary>Gets the manager for quests.</summary>
    public QuestManager Quests { get; } = new();
    /// <summary>Gets the manager for crafting products, recipes and results.</summary>
    public CraftingManager Crafting { get; } = new();
    /// <summary>Gets the manager for gift wrapping, presents, club gifts and new user gifts.</summary>
    public GiftManager Gifts { get; } = new();
    /// <summary>Gets the manager for club subscription, kickback and Builders Club information.</summary>
    public SubscriptionManager Subscriptions { get; } = new();
    /// <summary>Gets the manager for group forums, threads and messages.</summary>
    public ForumManager Forums { get; } = new();
    /// <summary>Gets the manager for catalog pages and purchases.</summary>
    public CatalogManager Catalog { get; } = new();
    /// <summary>Gets the manager for the wired state of the current room.</summary>
    public WiredManager Wired { get; } = new();
    /// <summary>Gets the manager for daily tasks.</summary>
    public DailyTaskManager DailyTasks { get; } = new();
    /// <summary>Gets the manager for habbicons.</summary>
    public HabbiconManager Habbicons { get; } = new();
    /// <summary>Gets the manager for game leaderboards.</summary>
    public LeaderboardManager Leaderboards { get; } = new();
    /// <summary>Gets the manager for navigator searches and rooms.</summary>
    public NavigatorManager Navigator { get; } = new();
    /// <summary>Gets the manager for the user's earnings status and claims.</summary>
    public EarningsManager Earnings { get; } = new();
    /// <summary>Gets the manager for the user's achievements and achievement score.</summary>
    public AchievementManager Achievements { get; } = new();
    /// <summary>Gets the furni data, product data, external texts and external variables of the connected hotel.</summary>
    /// <remarks>
    /// The data is loaded again for the hotel of each new connection.
    /// </remarks>
    public GameData GameData { get; } = new();
    /// <summary>Gets the task that prepares the current hotel session.</summary>
    /// <remarks>
    /// <para>
    /// A new task starts on each hotel connection. It waits for the interceptor's message catalog,
    /// waits until the session is authenticated or <see cref="AuthenticationGrace"/> passes, and then
    /// loads the user's profile and wallet with a timeout of 10000 milliseconds each.
    /// </para>
    /// <para>
    /// The task does not fault. A failure is stored in <see cref="BootstrapError"/>.
    /// </para>
    /// </remarks>
    public Task BootstrapTask => Volatile.Read(ref _bootstrap_task);
    /// <summary>Gets the error that stopped the bootstrap of the current hotel session, or <see langword="null"/> if there is none.</summary>
    /// <remarks>
    /// Cleared when a new hotel connection starts and when a bootstrap completes.
    /// </remarks>
    public Exception? BootstrapError => Volatile.Read(ref _bootstrap_error);

    /// <summary>Initializes a new instance of the <see cref="GameState"/> class.</summary>
    public GameState()
    {
        RoomEntries = new RoomEntryCoordinator(Room);
        Marketplace = new MarketplaceManager();
        Mimic = new MimicService(this);
    }

    internal IProfileOperations? ProfileOperations => Volatile.Read(ref _profile_operations);
    internal IRoomChatOperations? RoomChatOperations => Volatile.Read(ref _room_chat_operations);
    internal IRoomAvatarOperations? RoomAvatarOperations => Volatile.Read(ref _room_avatar_operations);
    internal IRoomControlOperations? RoomControlOperations => Volatile.Read(ref _room_control_operations);
    internal IFriendOperations? FriendOperations => Volatile.Read(ref _friend_operations);
    internal IRemotePeopleOperations? RemotePeopleOperations =>
        Volatile.Read(ref _remote_people_operations);

    internal void BindProfileOperations(IProfileOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_profile_operations_sync)
        {
            if (_profile_operations is not null)
                throw new InvalidOperationException("Profile operations are already bound.");
            Volatile.Write(ref _profile_operations, operations);
            _profile_operations_ready.TrySetResult(operations);
        }
    }

    internal void BindRoomChatOperations(IRoomChatOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_room_chat_operations_sync)
        {
            if (_room_chat_operations is not null)
                throw new InvalidOperationException("Room-chat operations are already bound.");
            Volatile.Write(ref _room_chat_operations, operations);
        }
    }

    internal void UnbindRoomChatOperations(IRoomChatOperations operations)
    {
        lock (_room_chat_operations_sync)
        {
            if (ReferenceEquals(_room_chat_operations, operations))
                Volatile.Write(ref _room_chat_operations, null);
        }
    }

    internal void BindRoomAvatarOperations(IRoomAvatarOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_room_avatar_operations_sync)
        {
            if (_room_avatar_operations is not null)
                throw new InvalidOperationException("Room-avatar operations are already bound.");
            Volatile.Write(ref _room_avatar_operations, operations);
        }
    }

    internal void UnbindRoomAvatarOperations(IRoomAvatarOperations operations)
    {
        lock (_room_avatar_operations_sync)
        {
            if (ReferenceEquals(_room_avatar_operations, operations))
                Volatile.Write(ref _room_avatar_operations, null);
        }
    }

    internal void BindRoomControlOperations(IRoomControlOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_room_control_operations_sync)
        {
            if (_room_control_operations is not null)
                throw new InvalidOperationException("Room-control operations are already bound.");
            Volatile.Write(ref _room_control_operations, operations);
        }
    }

    internal void UnbindRoomControlOperations(IRoomControlOperations operations)
    {
        lock (_room_control_operations_sync)
        {
            if (ReferenceEquals(_room_control_operations, operations))
                Volatile.Write(ref _room_control_operations, null);
        }
    }

    internal void BindFriendOperations(IFriendOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_friend_operations_sync)
        {
            if (_friend_operations is not null)
                throw new InvalidOperationException("Friend operations are already bound.");
            Volatile.Write(ref _friend_operations, operations);
        }
    }

    internal void UnbindFriendOperations(IFriendOperations operations)
    {
        lock (_friend_operations_sync)
        {
            if (ReferenceEquals(_friend_operations, operations))
                Volatile.Write(ref _friend_operations, null);
        }
    }

    internal void UnbindProfileOperations(IProfileOperations operations)
    {
        lock (_profile_operations_sync)
        {
            if (!ReferenceEquals(_profile_operations, operations))
                return;
            Volatile.Write(ref _profile_operations, null);
            _profile_operations_ready = new TaskCompletionSource<IProfileOperations>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private Task<IProfileOperations> WaitForProfileOperationsAsync(
        CancellationToken cancellation_token)
    {
        lock (_profile_operations_sync)
        {
            return _profile_operations is { } operations
                ? Task.FromResult(operations)
                : _profile_operations_ready.Task.WaitAsync(cancellation_token);
        }
    }

    internal void BindRemotePeopleOperations(IRemotePeopleOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_remote_people_operations_sync)
        {
            if (_remote_people_operations is not null)
                throw new InvalidOperationException("Remote-people operations are already bound.");
            Volatile.Write(ref _remote_people_operations, operations);
        }
    }

    internal void UnbindRemotePeopleOperations(IRemotePeopleOperations operations)
    {
        lock (_remote_people_operations_sync)
        {
            if (!ReferenceEquals(_remote_people_operations, operations))
                return;
            Volatile.Write(ref _remote_people_operations, null);
        }
    }

    internal void BindWalletOperations(IWalletOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_wallet_operations_sync)
        {
            if (_wallet_operations is not null)
                throw new InvalidOperationException("Wallet operations are already bound.");
            Volatile.Write(ref _wallet_operations, operations);
            _wallet_operations_ready.TrySetResult(operations);
        }
    }

    internal void UnbindWalletOperations(IWalletOperations operations)
    {
        lock (_wallet_operations_sync)
        {
            if (!ReferenceEquals(_wallet_operations, operations))
                return;
            Volatile.Write(ref _wallet_operations, null);
            _wallet_operations_ready = new TaskCompletionSource<IWalletOperations>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private Task<IWalletOperations> WaitForWalletOperationsAsync(
        CancellationToken cancellation_token)
    {
        lock (_wallet_operations_sync)
        {
            return _wallet_operations is { } operations
                ? Task.FromResult(operations)
                : _wallet_operations_ready.Task.WaitAsync(cancellation_token);
        }
    }

    /// <summary>Attaches every state manager to an interceptor and starts following its hotel connections.</summary>
    /// <param name="interceptor">The interceptor that carries the hotel traffic.</param>
    /// <remarks>
    /// If the interceptor already has a session, that session is bootstrapped at once. If attaching
    /// fails, the game state is disposed.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the game state has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the game state is already attached.</exception>
    public void Attach(IInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_interceptor is not null)
            throw new InvalidOperationException("The game state is already attached.");

        _interceptor = interceptor;
        try
        {
            Room.GameData = GameData;
            Room.OwnUserId = () => Profile.State.Identity?.Id;
            Profile.RoomUserByIndex = index => Room.AvatarByIndex(index) as User;
            RoomActions.Room = Room;
            RoomActions.OwnUserId = () => Profile.UserData?.Id;
            RoomBans.RoomScope = () => Room.Capture(room => new RoomBanRoomScope(
                room.Generation,
                (Id)room.RoomId,
                room.State is RoomSessionState.Entering or RoomSessionState.Ready));
            RoomSettings.RoomScope = () => Room.Capture(room => new RoomSettingsRoomScope(
                room.State is RoomSessionState.Entering or RoomSessionState.Ready,
                room.Generation,
                (Id)room.RoomId));
            People.Room = Room;
            People.RemotePeopleOperations = () => RemotePeopleOperations;
            Trade.RoomGeneration = () => Room.Generation;
            Visitors.Watch(Room, () => Profile.UserData?.Name);

            Room.Entering += RoomBans.EnterRoom;
            Room.Left += RoomBans.LeaveRoom;
            Room.Entering += RoomSettings.EnterRoom;
            Room.Left += RoomSettings.LeaveRoom;
            Room.Entering += Wired.EnterRoom;
            Room.Left += Wired.LeaveRoom;
            Room.Entering += Trade.EnterRoom;
            Room.Left += Trade.LeaveRoom;
            Room.Attach(interceptor);
            RoomActions.Attach(interceptor);
            RoomBans.Attach(interceptor);
            RoomSettings.Attach(interceptor);
            People.Attach(interceptor);
            RoomEntries.Attach(interceptor);
            Profile.Attach(interceptor);
            Inventory.Attach(interceptor);
            Badges.Attach(interceptor);
            Friends.Attach(interceptor);
            Trade.Attach(interceptor);
            Polls.Attach(interceptor);
            Requests.Attach(interceptor);
            Marketplace.Attach(interceptor);
            Economy.Attach(interceptor);
            Quests.Attach(interceptor);
            Crafting.Attach(interceptor);
            Gifts.Attach(interceptor);
            Subscriptions.Attach(interceptor);
            Forums.Attach(interceptor);
            Catalog.Attach(interceptor);
            Wired.Attach(interceptor);
            DailyTasks.Attach(interceptor);
            Habbicons.Attach(interceptor);
            Leaderboards.Attach(interceptor);
            Navigator.Attach(interceptor);
            Earnings.Attach(interceptor);
            Achievements.Attach(interceptor);

            _game_data_room_loaded = ApplyRoomGameData;
            _game_data_achievements_loaded = ApplyAchievementGameData;
            _game_data_habbicons_loaded = ApplyHabbiconGameData;
            _game_data_leaderboards_loaded = ApplyLeaderboardGameData;
            GameData.Loaded += _game_data_room_loaded;
            GameData.Loaded += _game_data_achievements_loaded;
            GameData.Loaded += _game_data_habbicons_loaded;
            GameData.Loaded += _game_data_leaderboards_loaded;
            _connected = session =>
            {
                long generation = Interlocked.Increment(ref _session_generation);
                Volatile.Write(ref _bootstrap_error, null);
                string web_host = GameData.WebHostFor(session.Host);
                bool adopt_preloaded_game_data;
                lock (_game_data_sync)
                {
                    GameDataSessionScope? previous_scope = Volatile.Read(ref _game_data_scope);
                    GameDataState game_data_state = GameData.State;
                    adopt_preloaded_game_data = game_data_state.Loaded &&
                        string.Equals(
                            game_data_state.WebHost,
                            web_host,
                            StringComparison.OrdinalIgnoreCase);
                    bool reset_game_data_consumers = previous_scope is not null
                        ? !string.Equals(
                            previous_scope.WebHost,
                            web_host,
                            StringComparison.OrdinalIgnoreCase)
                        : !string.IsNullOrEmpty(game_data_state.WebHost) &&
                            !string.Equals(
                                game_data_state.WebHost,
                                web_host,
                                StringComparison.OrdinalIgnoreCase);
                    Volatile.Write(
                        ref _game_data_scope,
                        new GameDataSessionScope(session, generation, web_host));
                    if (reset_game_data_consumers)
                    {
                        if (Achievements.NewAchievementCodes.Count != 0)
                            Achievements.NewAchievementCodes = [];
                        Habbicons.IsEnabled = false;
                        Leaderboards.ViewSize = 8;
                        Leaderboards.WindowSize = 50;
                    }
                }
                _ = GameData.LoadAsync(session.Host, _lifetime.Token);
                if (adopt_preloaded_game_data)
                {
                    ApplyRoomGameData();
                    ApplyAchievementGameData();
                    ApplyHabbiconGameData();
                    ApplyLeaderboardGameData();
                }
                Volatile.Write(
                    ref _bootstrap_task,
                    BootstrapSessionAsync(generation));
            };
            interceptor.Connected += _connected;
            if (interceptor.Session is { } session)
                _connected(session);
        }
        catch
        {
            Close();
            throw;
        }
    }

    /// <summary>Detaches and disposes every state manager and cancels the running bootstrap.</summary>
    void IDisposable.Dispose() => Close();

    private void Close()
    {
        if (_disposed)
            return;
        _disposed = true;
        _lifetime.Cancel();
        if (_game_data_room_loaded is not null)
            GameData.Loaded -= _game_data_room_loaded;
        if (_game_data_achievements_loaded is not null)
            GameData.Loaded -= _game_data_achievements_loaded;
        if (_game_data_habbicons_loaded is not null)
            GameData.Loaded -= _game_data_habbicons_loaded;
        if (_game_data_leaderboards_loaded is not null)
            GameData.Loaded -= _game_data_leaderboards_loaded;
        Room.Entering -= Wired.EnterRoom;
        Room.Left -= Wired.LeaveRoom;
        Room.Entering -= Trade.EnterRoom;
        Room.Left -= Trade.LeaveRoom;
        Room.Entering -= RoomBans.EnterRoom;
        Room.Left -= RoomBans.LeaveRoom;
        Room.Entering -= RoomSettings.EnterRoom;
        Room.Left -= RoomSettings.LeaveRoom;
        Trade.RoomGeneration = null;
        RoomBans.RoomScope = null;
        RoomSettings.RoomScope = null;
        People.RemotePeopleOperations = null;
        if (_interceptor is not null && _connected is not null)
            _interceptor.Connected -= _connected;
        lock (_game_data_sync)
            Volatile.Write(ref _game_data_scope, null);

        lock (_profile_operations_sync)
            Volatile.Write(ref _profile_operations, null);
        lock (_remote_people_operations_sync)
            Volatile.Write(ref _remote_people_operations, null);
        lock (_wallet_operations_sync)
            Volatile.Write(ref _wallet_operations, null);
        Profile.RoomUserByIndex = null;
        RoomEntries.Close();
        RoomActions.Close();
        RoomBans.Close();
        RoomSettings.Close();
        People.Close();
        Room.Close();
        Profile.Close();
        Inventory.Close();
        Badges.Close();
        Friends.Close();
        Trade.Close();
        Polls.Close();
        Marketplace.Close();
        Requests.Close();
        Economy.Close();
        Quests.Close();
        Crafting.Close();
        Gifts.Close();
        Subscriptions.Close();
        Forums.Close();
        Catalog.Close();
        Wired.Close();
        DailyTasks.Close();
        Habbicons.Close();
        Leaderboards.Close();
        Navigator.Close();
        Earnings.Close();
        Achievements.Close();
        _lifetime.Dispose();
    }

    /// <summary>
    /// Gets or sets how long the bootstrap waits for the session to be authenticated before it loads the profile and wallet anyway.
    /// </summary>
    /// <remarks>
    /// The default is 20 seconds. Reaching it usually means the interceptor attached to a session
    /// that was already logged in, so the login messages were never seen. It has to outlast a slow
    /// login, because requests sent during a login make the hotel drop the connection.
    /// </remarks>
    public TimeSpan AuthenticationGrace { get; set; } = TimeSpan.FromSeconds(20);

    private void ApplyRoomGameData()
    {
        lock (_game_data_sync)
        {
            if (TryCurrentGameData(out _))
                Room.EnrichFurni();
        }
    }

    private void ApplyAchievementGameData()
    {
        lock (_game_data_sync)
        {
            if (!TryCurrentGameData(out GameDataState data))
                return;
            Achievements.NewAchievementCodes = data.Variables?
                .List("achievements.new") ?? [];
        }
    }

    private void ApplyHabbiconGameData()
    {
        lock (_game_data_sync)
        {
            if (!TryCurrentGameData(out GameDataState data))
                return;
            Habbicons.IsEnabled = data.Variables?.Flag("habbicons.enabled") ?? false;
        }
    }

    private void ApplyLeaderboardGameData()
    {
        lock (_game_data_sync)
        {
            if (!TryCurrentGameData(out GameDataState data))
                return;
            Leaderboards.ViewSize = data.Variables?
                .Number("games.highscores.viewSize", 8) ?? 8;
            Leaderboards.WindowSize = data.Variables?
                .Number("games.highscores.windowSize", 50) ?? 50;
        }
    }

    private bool TryCurrentGameData(out GameDataState data)
    {
        data = GameData.State;
        GameDataSessionScope? scope = Volatile.Read(ref _game_data_scope);
        Session? session = _interceptor?.Session;
        long generation = Volatile.Read(ref _session_generation);
        return scope is not null &&
            ReferenceEquals(session, scope.Session) &&
            generation == scope.SessionGeneration &&
            data.Loaded &&
            data.LoadGeneration > 0 &&
            string.Equals(data.WebHost, scope.WebHost, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Waits until the session looks authenticated or <see cref="AuthenticationGrace"/> passes.
    /// </summary>
    /// <remarks>
    /// G-Earth reports a connection as soon as the client's socket opens, which is before the hotel
    /// has logged it in. A request sent in that window makes the server drop the connection. A normal
    /// login fills in the profile from the client's own traffic, so usually nothing has to be sent.
    /// </remarks>
    /// <param name="generation">The session generation the bootstrap was started for.</param>
    /// <returns>Whether the session is still the one the bootstrap was started for.</returns>
    private async Task<bool> WaitForAuthenticatedSessionAsync(long generation)
    {
        if (Profile.IsLoaded)
            return true;

        var authenticated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnProfileChanged(ProfileStateUpdate update)
        {
            if (update.State.Loaded)
                authenticated.TrySetResult(true);
        }

        Profile.StateChanged += OnProfileChanged;
        try
        {
            if (Profile.IsLoaded)
                return true;

            using var grace = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            Task expiry = Task.Delay(AuthenticationGrace, grace.Token)
                .ContinueWith(_ => { }, TaskContinuationOptions.ExecuteSynchronously);
            await Task.WhenAny(authenticated.Task, expiry).ConfigureAwait(false);
            grace.Cancel();
            _lifetime.Token.ThrowIfCancellationRequested();
            return Volatile.Read(ref _session_generation) == generation;
        }
        finally
        {
            Profile.StateChanged -= OnProfileChanged;
        }
    }

    private async Task BootstrapSessionAsync(long generation)
    {
        try
        {
            await _interceptor!.WaitForCatalogBuildAsync(_lifetime.Token).ConfigureAwait(false);
            if (!await WaitForAuthenticatedSessionAsync(generation).ConfigureAwait(false))
                return;
            IProfileOperations profile_operations = await WaitForProfileOperationsAsync(
                _lifetime.Token).ConfigureAwait(false);
            IWalletOperations wallet_operations = await WaitForWalletOperationsAsync(
                _lifetime.Token).ConfigureAwait(false);
            await Task.WhenAll(
                profile_operations.EnsureLoadedAsync(10000, _lifetime.Token),
                wallet_operations.EnsureLoadedAsync(10000, _lifetime.Token)).ConfigureAwait(false);
            if (Volatile.Read(ref _session_generation) == generation)
                Volatile.Write(ref _bootstrap_error, null);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            if (Volatile.Read(ref _session_generation) == generation)
                Volatile.Write(ref _bootstrap_error, error);
        }
    }

    private sealed record GameDataSessionScope(
        Session Session,
        long SessionGeneration,
        string WebHost);
}
