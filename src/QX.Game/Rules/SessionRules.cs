using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Qx;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Rules;

/// <summary>What clicking somebody in the room does.</summary>
public enum ClickAction
{
    /// <summary>Nothing; a click is just a click.</summary>
    None,

    Mute,
    Kick,
    Ban,

    /// <summary>Ban and unban at once, which puts them out with no kick notice.</summary>
    Bounce
}

/// <summary>
/// Standing rules applied to the traffic of the live session.
/// </summary>
/// <remarks>
/// <para>
/// Each one is a small, always-on interception: swallow a packet, rewrite one, or answer one. They
/// live here rather than in a script because they are settings, not programs — nobody wants to keep
/// a script running just to stop the client turning them round.
/// </para>
/// <para>
/// Every rule binds by message <em>name</em>, never by header id, so the same rule works on Flash
/// and on Unity. Where the two clients disagree about what a message is called, the name map
/// translates; where a rule is genuinely impossible on one of them it is not offered at all rather
/// than failing quietly.
/// </para>
/// </remarks>
public sealed partial class SessionRules : IDisposable
{
    private sealed record Document
    {
        public bool AntiIdle { get; init; }
        public bool BlockTrades { get; init; }
        public bool LetFriendsIn { get; init; }
        public bool NoWalk { get; init; }
        public bool NoTurn { get; init; }
        public bool AlwaysShout { get; init; }
        public bool NoTyping { get; init; }
        public bool MuteBots { get; init; }
        public bool MutePets { get; init; }
        public bool MuteWired { get; init; }
        public bool PreventFurniUse { get; init; }
        public bool MuteAll { get; init; }
        public bool MuteRespects { get; init; }
        public bool BlockRoomAds { get; init; }
        public bool BlockClubGifts { get; init; }
        public bool BlockNotifications { get; init; }
        public bool DropHandItems { get; init; }
        public bool BlockRoomInvites { get; init; }
        public bool BlockFriendRequests { get; init; }
        public bool AutoAcceptFriendRequests { get; init; }
        public int AntiIdleSeconds { get; init; } = 60;
        public bool AntiIdleOut { get; init; }
        public bool TurnOnReselect { get; init; }
        public bool TurnTowardsClickedTile { get; init; }
        public ClickAction ClickTo { get; init; }
        public int ClickMuteMinutes { get; init; } = 5;
        public BanLength ClickBanLength { get; init; }
        public bool ClickExcludesFriends { get; init; } = true;
        public bool RememberPasswords { get; init; }
        public bool FlattenFloor { get; init; }
        public bool HideAvatars { get; init; }
        public bool MutePetCommands { get; init; }
        public bool ShowRespectCount { get; init; }
        public bool? ShiftClickShowsInfo { get; init; }
        public bool? ShiftClickHides { get; init; }
        public bool? ShiftClickFindsLink { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool CtrlClickShowsInfo { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool CtrlClickHides { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool CtrlClickFindsLink { get; init; }
        public bool ReturnHandItems { get; init; }
        public bool KeepDirection { get; init; }
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(Document))]
    private sealed partial class JsonContext : JsonSerializerContext;

    private readonly IInterceptor _interceptor;
    private readonly IApplicationRuntime _application;
    private readonly Func<bool> _shift_pressed;
    private readonly string _path;
    private readonly List<IDisposable> _bindings = [];
    private Timer? _idle;
    private bool _let_friends_in;
    private bool _click_excludes_friends = true;
    private ClickAction _click_to;
    private int _anti_idle_seconds = 60;
    private bool _bound;
    private int _last_respecter_index = -1;
    private DateTimeOffset _last_respect;

    /// <summary>
    /// The mirrored session, for the rules that need to know more than the packet says.
    /// </summary>
    /// <remarks>
    /// Chat carries a room index rather than a name, and a doorbell carries a name rather than
    /// whether you know them, so muting and letting friends in are impossible without it.
    /// </remarks>
    public GameState Game { get; }

    public SessionRules(
        IInterceptor interceptor,
        GameState game,
        IApplicationRuntime application,
        string path,
        Func<bool>? shift_pressed = null)
    {
        _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
        _application = application ?? throw new ArgumentNullException(nameof(application));
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _shift_pressed = shift_pressed ?? (() => false);
        Load();
        Game = game ?? throw new ArgumentNullException(nameof(game));
        RequestFriends();
    }

    public bool AntiIdle { get; set; }
    public bool BlockTrades { get; set; }
    public bool LetFriendsIn
    {
        get => _let_friends_in;
        set
        {
            _let_friends_in = value;
            RequestFriends();
        }
    }
    public bool NoWalk { get; set; }
    public bool NoTurn { get; set; }
    public bool AlwaysShout { get; set; }
    public bool NoTyping { get; set; }
    public bool MuteBots { get; set; }
    public bool MutePets { get; set; }
    public bool MuteWired { get; set; }
    public bool PreventFurniUse { get; set; }

    /// <summary>Nothing anyone says reaches the screen, whoever they are.</summary>
    public bool MuteAll { get; set; }

    /// <summary>The bubbles that say who respected whom, and who scratched which pet.</summary>
    public bool MuteRespects { get; set; }

    /// <summary>The advertisement a room shows on entry.</summary>
    public bool BlockRoomAds { get; set; }

    /// <summary>The club gift offer that reappears every time the hotel feels like it.</summary>
    public bool BlockClubGifts { get; set; }

    /// <summary>Every hotel notice, including the ones with a dialog attached.</summary>
    public bool BlockNotifications { get; set; }

    /// <summary>Anything handed to you goes straight back on the floor.</summary>
    public bool DropHandItems { get; set; }

    /// <summary>Invitations to other rooms never reach the screen.</summary>
    public bool BlockRoomInvites { get; set; }

    /// <summary>Friend requests never reach the screen.</summary>
    public bool BlockFriendRequests { get; set; }

    /// <summary>
    /// Every friend request is accepted the moment it arrives.
    /// </summary>
    /// <remarks>
    /// Ignored while requests are being blocked: accepting something you refused to look at is a
    /// contradiction, and the two switches say so rather than fighting.
    /// </remarks>
    public bool AutoAcceptFriendRequests { get; set; }





    /// <summary>
    /// Lets you idle, but not be put out of the room for it.
    /// </summary>
    /// <remarks>
    /// The opposite trade from anti-idle: your avatar is allowed to fall asleep, and only the
    /// moment the hotel would act on it is answered. Cheaper on the wire than a gesture every
    /// minute, and it leaves you looking idle to everybody else, which is often the point.
    /// </remarks>
    public bool AntiIdleOut { get; set; }


    /// <summary>Turning is allowed again when you click the same person twice.</summary>
    public bool TurnOnReselect { get; set; }

    /// <summary>A blocked walk still turns you to face the tile you clicked.</summary>
    public bool TurnTowardsClickedTile { get; set; }


    /// <summary>What clicking somebody in the room does, if anything.</summary>
    public ClickAction ClickTo
    {
        get => _click_to;
        set
        {
            _click_to = value;
            RequestFriends();
        }
    }

    /// <summary>How long a click-to mute lasts, in minutes.</summary>
    public int ClickMuteMinutes { get; set; } = 5;

    /// <summary>How long a click-to ban lasts.</summary>
    public BanLength ClickBanLength { get; set; } = BanLength.Hour;

    /// <summary>Friends are never the target of a click-to action.</summary>
    public bool ClickExcludesFriends
    {
        get => _click_excludes_friends;
        set
        {
            _click_excludes_friends = value;
            RequestFriends();
        }
    }


    /// <summary>Room passwords are remembered and offered again without being retyped.</summary>
    public bool RememberPasswords { get; set; }

    /// <summary>Every tile is reported at the same height, so nothing looks raised.</summary>
    public bool FlattenFloor { get; set; }

    /// <summary>Nobody is drawn in the room at all.</summary>
    public bool HideAvatars { get; set; }


    /// <summary>A pet's own commands, which are chat the pet did not choose to say.</summary>
    public bool MutePetCommands { get; set; }

    /// <summary>Adds the running total to that line.</summary>
    public bool ShowRespectCount { get; set; }


    /// <summary>Holding shift and clicking furni says what it is instead of using it.</summary>
    public bool ShiftClickShowsInfo { get; set; }

    /// <summary>Holding shift and clicking furni takes it off the screen.</summary>
    public bool ShiftClickHides { get; set; }

    /// <summary>Holding shift and clicking a teleport says which one it is paired with.</summary>
    public bool ShiftClickFindsLink { get; set; }


    /// <summary>Anything handed to you goes back to whoever gave it.</summary>
    public bool ReturnHandItems { get; set; }

    /// <summary>
    /// Turns you back to where you were facing after somebody hands you something.
    /// </summary>
    /// <remarks>
    /// Being handed an item turns you towards whoever handed it, which is a nuisance while you are
    /// standing somewhere deliberately. The direction is read before the turn arrives and sent
    /// straight back, so the avatar swings and returns rather than staying where it was put.
    /// </remarks>
    public bool KeepDirection { get; set; }

    /// <summary>
    /// How often the anti-idle gesture goes out.
    /// </summary>
    /// <remarks>
    /// A minute is well inside the hotel's own patience and cheap. Slower is quieter on the wire;
    /// faster is pointless.
    /// </remarks>
    public int AntiIdleSeconds
    {
        get => _anti_idle_seconds;
        set
        {
            if (_anti_idle_seconds == value)
                return;
            _anti_idle_seconds = value;
            if (_bound)
                AntiIdleLoop();
        }
    }

    /// <summary>How many rules are switched on, for the line that says whether anything is happening.</summary>
    public int Active =>
        (AntiIdle ? 1 : 0) + (BlockTrades ? 1 : 0) + (LetFriendsIn ? 1 : 0) +
        (NoWalk ? 1 : 0) + (NoTurn ? 1 : 0) + (AlwaysShout ? 1 : 0) + (NoTyping ? 1 : 0) +
        (MuteBots ? 1 : 0) + (MutePets ? 1 : 0) + (MuteWired ? 1 : 0) + (PreventFurniUse ? 1 : 0) +
        (MuteAll ? 1 : 0) + (MuteRespects ? 1 : 0) + (BlockRoomAds ? 1 : 0) +
        (BlockClubGifts ? 1 : 0) + (BlockNotifications ? 1 : 0) + (DropHandItems ? 1 : 0) +
        (BlockRoomInvites ? 1 : 0) + (BlockFriendRequests ? 1 : 0) +
        (AutoAcceptFriendRequests ? 1 : 0) +
        (AntiIdleOut ? 1 : 0) + (TurnTowardsClickedTile ? 1 : 0) +
        (ClickTo is not ClickAction.None ? 1 : 0) + (RememberPasswords ? 1 : 0) +
        (FlattenFloor ? 1 : 0) + (HideAvatars ? 1 : 0) + (MutePetCommands ? 1 : 0) +
        (ShowRespectCount ? 1 : 0) +
        (ShiftClickShowsInfo ? 1 : 0) + (ShiftClickHides ? 1 : 0) + (ShiftClickFindsLink ? 1 : 0) +
        (ReturnHandItems ? 1 : 0) + (KeepDirection ? 1 : 0);

    public string ClientName
    {
        get
        {
            ClientType? client = _interceptor.Session?.Client;
            return client switch
            {
                null or ClientType.None => "not connected",
                ClientType.Flash => "Flash",
                ClientType.Unity => "Unity",
                _ => throw new UnsupportedClientException(client.Value)
            };
        }
    }

    /// <summary>
    /// Binds every rule once, for the whole run.
    /// </summary>
    /// <remarks>
    /// Bound once and consulted per packet rather than bound and unbound as switches move: the
    /// interceptors are keyed by name and rebinding them on every toggle would race the read loop.
    /// A rule that is off costs one boolean read.
    /// </remarks>
    public void Bind()
    {
        Unbind();
        _bound = true;

        // A blocked walk can still turn you to face where you clicked, which is the difference
        // between standing still and standing still facing the wrong way.
        Bind(MessageContracts.Room.Movement.Walk.Key, intercept =>
        {
            if (!NoWalk)
                return;

            intercept.Block();
            if (!TurnTowardsClickedTile)
                return;

            WalkRequest request;
            try
            {
                PacketReader reader = intercept.Packet.Reader();
                request = MessageContracts.Room.Movement.Walk.Parse(in reader);
                if (reader.Available != 0)
                    return;
            }
            catch
            {
                return;
            }

            Try(() => Game?.RoomAvatarOperations?.Look(
                new Application.RoomAvatarLookRequest(request.X, request.Y)));
        });

        // Clicking somebody in the room is a turn-towards on the wire, so this is the only place a
        // click on a person can be recognised at all. The tile is turned back into whoever is
        // standing on it; a click on empty floor is left alone.
        OutOf(MessageContracts.Room.Movement.LookTo, (request, intercept) =>
        {
            var tile = new Point(request.X, request.Y);
            ClickAction action = ClickTo;
            (User? target, long room_generation) target_scope = Game.Room.Capture(room =>
                (room.Avatars
                    .OfType<User>()
                    .FirstOrDefault(user => user.X == tile.X && user.Y == tile.Y),
                room.Generation));
            User? target = target_scope.target;

            if (action is not ClickAction.None && target is not null &&
                !IsSelf(target) && !Spared(target))
            {
                int mute_minutes = ClickMuteMinutes;
                BanLength ban_length = ClickBanLength;
                if (ActOn(
                    target,
                    target_scope.room_generation,
                    action,
                    mute_minutes,
                    ban_length))
                {
                    intercept.Block();
                    return;
                }
            }

            if (!NoTurn)
                return;

            // Turning is allowed again on the second click on the same person, which is the gesture
            // people use deliberately when they do mean to face somebody.
            bool again = TurnOnReselect && target is not null && target.Id == _lastClicked;
            _lastClicked = target?.Id ?? 0;

            if (!again)
                intercept.Block();
        });

        Bind(MessageContracts.Room.Typing.Start.Key, intercept =>
        {
            if (NoTyping)
                intercept.Block();
        });

        OutOf(MessageContracts.Room.FloorItemUse, (message, intercept) =>
        {
            if (ShiftClickedFurni(intercept, message.ItemId))
                return;
            if (PreventFurniUse)
                intercept.Block();
        });

        OutOf(MessageContracts.Room.WallItemUse, (message, intercept) =>
        {
            if (ShiftClickedFurni(intercept, message.ItemId))
                return;
            if (PreventFurniUse)
                intercept.Block();
        });

        Bind(MessageContracts.Room.Chat.TalkSend.Key, intercept =>
        {
            if (!AlwaysShout)
                return;

            TalkRequest message;
            try
            {
                PacketReader reader = intercept.Packet.Reader();
                message = MessageContracts.Room.Chat.TalkSend.Parse(in reader);
                if (reader.Available != 0)
                    return;
            }
            catch
            {
                return;
            }

            if (!_interceptor.Messages.TryGetHeader(
                    MessageContracts.Room.Chat.ShoutSend.Key,
                    out Header shout))
                return;

            var louder = new Packet(shout, intercept.Packet.Client)
            {
                Context = intercept.Packet.Context
            };
            try
            {
                PacketWriter writer = louder.Writer();
                MessageContracts.Room.Chat.ShoutSend.Compose(
                    new ShoutRequest(message.Text, message.BubbleStyle),
                    in writer);
            }
            catch
            {
                louder.Dispose();
                return;
            }
            intercept.Packet = louder;
        });

        // Someone at the door who is on your friend list is let in without you having to be at the
        // keyboard. Anyone else still rings and waits.
        In(MessageContracts.Room.Access.Doorbell, ring =>
        {
            if (!LetFriendsIn || ring.UserName.Length == 0)
                return;
            _ = LetFriendInAsync(ring);
        });

        Bind(MessageContracts.Trade.Opened.Key, intercept =>
        {
            if (!BlockTrades)
                return;
            try
            {
                TradeStateView state = _application.Invoke<TradeStateRequest, TradeStateView>(
                    ApplicationMemberIds.TradeState,
                    new TradeStateRequest());
                if (state.Active is { } trade)
                {
                    _application.Invoke<TradeCommandRequest, TradeDispatchResult>(
                        ApplicationMemberIds.TradeClose,
                        new TradeCommandRequest(
                            state.SessionGeneration,
                            state.Revision,
                            trade.Epoch));
                }
            }
            catch
            {
            }
            intercept.Block();
        });

        MuteChat(MessageContracts.Room.Chat.Talk);
        MuteChat(MessageContracts.Room.Chat.Shout);
        MuteChat(MessageContracts.Room.Chat.Whisper);

        Swallow(MessageKeys.Gifts.ClubNotification, () => BlockClubGifts);
        Swallow(MessageKeys.Notifications.Dialog, () => BlockNotifications);
        Swallow(MessageKeys.Room.Occupants.Pet.Respect, () => MuteRespects);
        Swallow(MessageKeys.Friends.RoomInvite, () => BlockRoomInvites);
        Swallow(MessageKeys.Room.Advertisement, () => BlockRoomAds);
        Bind(MessageContracts.Room.Occupants.Snapshot.Key, intercept =>
        {
            if (!HideAvatars)
                return;

            var packet = new Packet(intercept.Packet.Header, intercept.Packet.Client)
            {
                Context = intercept.Packet.Context
            };
            packet.Writer().WriteLength(0);
            intercept.Packet = packet;
        });

        // Every tile reported at the same height. The room itself is untouched; only what this
        // client is told about it changes, so nobody else can see any of this.
        Bind(MessageContracts.Room.Environment.FloorPlan.Key, intercept =>
        {
            if (!FlattenFloor)
                return;

            Try(() =>
            {
                PacketReader reader = intercept.Packet.Reader();
                bool legacy_scale = reader.ReadBool();
                int wall_height = reader.ReadInt();
                string map = reader.ReadString();
                ReadOnlySpan<byte> tail = reader.ReadSpan(reader.Available).ToArray();
                var packet = new Packet(intercept.Packet.Header, intercept.Packet.Client)
                {
                    Context = intercept.Packet.Context
                };
                PacketWriter writer = packet.Writer();
                writer.WriteBool(legacy_scale);
                writer.WriteInt(wall_height);
                writer.WriteString(Levelled(map));
                writer.WriteSpan(tail);
                intercept.Packet = packet;
            });
        });

        In<Heightmap>(MessageKeys.Room.Heightmap.Snapshot, (map, intercept) =>
        {
            if (!FlattenFloor)
                return;

            Try(() =>
            {
                var flat = new Heightmap(map.Width, [.. Flattened(map)]);
                var packet = new Packet(intercept.Packet.Header, intercept.Packet.Client)
                {
                    Context = intercept.Packet.Context
                };
                packet.Writer().Compose(flat);
                intercept.Packet = packet;
            });
        });

        // The password you typed, kept against the room you typed it for. Watched on the way out
        // rather than asked for: the hotel never sends it back, so the only chance to learn it is
        // the moment it goes past.
        OutOf(MessageContracts.Room.Access.OpenRequest, (entry, intercept) =>
        {
            if (!RememberPasswords)
                return;

            if (entry.Password.Length > 0)
            {
                _passwords[(long)entry.RoomId] = entry.Password;
                return;
            }
            if (!_passwords.TryGetValue((long)entry.RoomId, out string? password))
                return;

            var packet = new Packet(intercept.Packet.Header, intercept.Packet.Client)
            {
                Context = intercept.Packet.Context
            };
            packet.Writer().Compose(entry with { Password = password });
            intercept.Packet = packet;
        });

        In(MessageContracts.Room.Occupants.Action.Expression, action =>
        {
            if (action.Action != 7)
                return;

            _last_respecter_index = action.Index;
            _last_respect = DateTimeOffset.UtcNow;
        });

        Bind(MessageContracts.Room.Occupants.Respect.Key, intercept =>
        {
            if (MuteRespects)
            {
                intercept.Block();
                return;
            }
            if (!ShowRespectCount)
                return;

            Try(() =>
            {
                PacketReader reader = intercept.Packet.Reader();
                RespectNotification message = MessageContracts.Room.Occupants.Respect.Parse(in reader);
                if (reader.Available != 0)
                    return;

                string respected = Game?.Room.AvatarById(message.RespectedUserId)?.Name
                    ?? $"#{message.RespectedUserId}";
                Avatar? source = DateTimeOffset.UtcNow - _last_respect < TimeSpan.FromMilliseconds(750)
                    ? Game?.Room.AvatarByIndex(_last_respecter_index)
                    : null;
                _last_respecter_index = -1;

                Say(RespectLine(respected, source?.Name, message.TotalRespect));
            });
        });

        // Something handed to you goes back on the floor, or back to whoever gave it, and you go
        // back to facing whichever way you were.
        In(MessageContracts.Room.HandItem.Received, (received, intercept) =>
        {
            if (!DropHandItems && !ReturnHandItems && !KeepDirection)
                return;

            Try(() =>
            {
                // Read here and sent later: the direction is still the old one at this moment,
                // because the turn towards the giver has not been handled yet, and asking to turn
                // back before it happens would only be undone by it.
                if (KeepDirection && FacedTile() is { } facing)
                    TurnBackTo(facing);

                if (DropHandItems)
                {
                    Game?.RoomControlOperations?.DropHandItem(new RoomHandItemDropRequest());
                    return;
                }

                if (!ReturnHandItems)
                    return;

                if (Game?.Room.AvatarById(received.GiverId) is { } avatar)
                {
                    Game.RoomControlOperations?.PassHandItem(
                        new RoomHandItemPassRequest(avatar.Id));
                }
            });
        });
        Swallow(MessageKeys.Friends.FriendRequestReceived, () => BlockFriendRequests);

        // A request answered before you have seen it. Blocked requests are left alone: accepting
        // something you refused to look at is a contradiction.
        In(MessageContracts.Friends.FriendRequestReceived, request =>
        {
            Session? expected_session = _interceptor.Session;
            if (!AutoAcceptFriendRequests ||
                BlockFriendRequests ||
                expected_session is null ||
                Game is not { } game)
            {
                return;
            }
            game.FriendOperations?.AcceptRequests(
                new FriendRequestIdsRequest([request.RequestId]),
                expected_session,
                default);
        });

        // Left to idle, but woken the instant the hotel says so. The cheaper half of anti-idle:
        // nothing goes out until something needs to.
        In(MessageContracts.Room.Occupants.Action.Sleep, _ =>
        {
            if (AntiIdleOut && !AntiIdle)
            {
                Try(() => Game?.RoomAvatarOperations?.Expression(
                    new Application.RoomAvatarExpressionRequest(0)));
            }
        });

        AntiIdleLoop();
    }



    /// <summary>Who was clicked last, so a second click on the same person can be told apart.</summary>
    private Id _lastClicked;

    /// <summary>Room passwords, kept for as long as the session lasts and never written down.</summary>
    private readonly Dictionary<long, string> _passwords = [];

    /// <summary>
    /// Runs something that touches the wire, and swallows what goes wrong.
    /// </summary>
    /// <remarks>
    /// These run inside the read loop. An exception here would take the packet down with it, and a
    /// rule failing is never worth losing traffic over.
    /// </remarks>
    private static void Try(Action work)
    {
        try
        {
            work();
        }
        catch
        {
        }
    }

    private bool IsSelf(Avatar avatar) => Game?.Profile.UserData?.Id == avatar.Id;

    /// <summary>
    /// Asks to face a tile again once the hotel has finished turning you away from it.
    /// </summary>
    /// <remarks>
    /// A short wait rather than an immediate answer: the turn arrives a moment after the item does,
    /// and a request sent before it would simply be overwritten by it. Long enough to land second,
    /// short enough that the avatar looks like it glanced and went back.
    /// </remarks>
    private void TurnBackTo((int X, int Y) tile) => _ = TurnBackToAsync(tile);

    private async Task TurnBackToAsync((int X, int Y) tile)
    {
        await Task.Delay(400).ConfigureAwait(false);
        Try(() => Game?.RoomAvatarOperations?.Look(
            new Application.RoomAvatarLookRequest(tile.X, tile.Y)));
    }

    /// <summary>
    /// The tile one step ahead of where you are facing.
    /// </summary>
    /// <remarks>
    /// There is no message for "face this way": turning is asked for by naming a tile and letting
    /// the client work out the direction, so a direction has to be turned back into a tile to be
    /// asked for at all. Eight directions from north, clockwise, as the hotel numbers them.
    /// </remarks>
    private (int X, int Y)? FacedTile()
    {
        if (Game?.Room.Avatars.FirstOrDefault(IsSelf) is not { } me)
            return null;

        (int x, int y) = me.Direction switch
        {
            0 => (0, -1),
            1 => (1, -1),
            2 => (1, 0),
            3 => (1, 1),
            4 => (0, 1),
            5 => (-1, 1),
            6 => (-1, 0),
            7 => (-1, -1),
            _ => (0, 0)
        };

        return (x, y) is (0, 0) ? null : (me.X + x, me.Y + y);
    }

    /// <summary>Whether somebody is spared from a click-to action for being a friend.</summary>
    private bool Spared(Avatar avatar)
    {
        if (!ClickExcludesFriends)
            return false;
        if (Game?.Friends is not { } friends || !friends.IsLoaded)
        {
            RequestFriends();
            return true;
        }
        return friends.Friends.Any(friend => friend.Id == avatar.Id);
    }

    private void RequestFriends()
    {
        if (Game is not { } game || !LetFriendsIn && !(ClickExcludesFriends && ClickTo is not ClickAction.None))
            return;
        _ = LoadFriendsAsync(game);
    }

    private static async Task LoadFriendsAsync(GameState game)
    {
        try
        {
            if (game.FriendOperations is { } operations)
                await operations.EnsureLoadedAsync(10000, default).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task LetFriendInAsync(Doorbell ring)
    {
        if (Game is not { } game)
            return;
        try
        {
            IReadOnlyCollection<Friend> friends;
            if (game.Friends.IsLoaded)
            {
                friends = game.Friends.Friends;
            }
            else if (game.FriendOperations is { } operations)
            {
                friends = await operations.EnsureLoadedAsync(10000, default).ConfigureAwait(false);
            }
            else
            {
                return;
            }
            if (LetFriendsIn && FriendAtDoor(ring, friends))
                game.RoomControlOperations?.AnswerDoorbell(
                    new RoomDoorbellAnswerRequest(ring.UserName));
        }
        catch
        {
        }
    }

    /// <summary>Does to somebody whatever clicking them is set to do.</summary>
    private bool ActOn(
        User target,
        long room_generation,
        ClickAction action,
        int mute_minutes,
        BanLength ban_length)
    {
        try
        {
            RoomModerationStateView state = _application.Invoke<
                RoomModerationStateRequest,
                RoomModerationStateView>(
                    ApplicationMemberIds.RoomModerationState,
                    new RoomModerationStateRequest());
            if (!state.RoomReady ||
                state.RoomId <= 0 ||
                state.RoomGeneration != room_generation)
            {
                return false;
            }
            RoomModerationDispatchResult result;
            switch (action)
            {
                case ClickAction.Mute:
                    result = _application.Invoke<
                        RoomModerationMuteRequest,
                        RoomModerationDispatchResult>(
                            ApplicationMemberIds.RoomModerationMute,
                            new RoomModerationMuteRequest(
                                target.Id,
                                mute_minutes,
                                state.SessionGeneration,
                                state.RoomId,
                                room_generation,
                                target.Index));
                    break;
                case ClickAction.Kick:
                    result = _application.Invoke<
                        RoomModerationTargetRequest,
                        RoomModerationDispatchResult>(
                            ApplicationMemberIds.RoomModerationKick,
                            new RoomModerationTargetRequest(
                                target.Id,
                                state.SessionGeneration,
                                state.RoomId,
                                room_generation,
                                target.Index));
                    break;
                case ClickAction.Ban:
                    result = _application.Invoke<
                        RoomModerationBanRequest,
                        RoomModerationDispatchResult>(
                            ApplicationMemberIds.RoomModerationBan,
                            new RoomModerationBanRequest(
                                target.Id,
                                ban_length,
                                state.SessionGeneration,
                                state.RoomId,
                                room_generation,
                                target.Index));
                    break;
                case ClickAction.Bounce:
                    result = _application.Invoke<
                        RoomModerationTargetRequest,
                        RoomModerationDispatchResult>(
                            ApplicationMemberIds.RoomModerationBounce,
                            new RoomModerationTargetRequest(
                                target.Id,
                                state.SessionGeneration,
                                state.RoomId,
                                room_generation,
                                target.Index));
                    break;
                default:
                    return false;
            }
            return result.MessagesDispatched > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Answers a shift-click on furni instead of using it.
    /// </summary>
    /// <remarks>
    /// The modifier is read from the keyboard rather than from the packet, because nothing about a
    /// use message says how it was clicked. Returns whether the click was answered, so the caller
    /// knows not to fall through to preventing it.
    /// </remarks>
    private bool ShiftClickedFurni(Intercept intercept, Id id)
    {
        if (!ShiftClickShowsInfo && !ShiftClickHides && !ShiftClickFindsLink)
            return false;
        if (!_shift_pressed())
            return false;
        if (Game is not { } game)
            return false;

        Furni? item = game.Room.FloorItems.FirstOrDefault(one => one.Id == id)
            ?? (Furni?)game.Room.WallItems.FirstOrDefault(one => one.Id == id);
        if (item is null)
            return false;

        intercept.Block();

        if (ShiftClickHides)
        {
            game.RoomActions.Hide(item);
            return true;
        }

        if (ShiftClickFindsLink && item is FloorItem tele && Linked(game, tele) is { } other)
        {
            Say($"That teleport is paired with the one at {other.X}, {other.Y}");
            return true;
        }

        if (ShiftClickShowsInfo)
        {
            FurniInfo? info = game.GameData.Furni?.GetInfo(item);
            string name = info?.Name is { Length: > 0 } named ? named : $"kind {item.Kind}";
            Say($"{name} · {info?.Identifier ?? "unknown"} · id {item.Id}");
        }

        return true;
    }

    /// <summary>
    /// The other end of a teleport.
    /// </summary>
    /// <remarks>
    /// A pair carries each other's identifier in its own data, which is the only thing in the room
    /// that says which two belong together.
    /// </remarks>
    private static FloorItem? Linked(GameState game, FloorItem tele)
    {
        if (tele.Extra <= 0)
            return null;

        return game.Room.FloorItems.FirstOrDefault(one => one.Id == (Id)tele.Extra && one.Id != tele.Id);
    }

    private static string Levelled(string map)
    {
        char[] tiles = map.ToCharArray();
        for (int index = 0; index < tiles.Length; index++)
        {
            if (tiles[index] != 'x' && tiles[index] != 'X' && !char.IsWhiteSpace(tiles[index]))
                tiles[index] = '0';
        }
        return new string(tiles);
    }

    /// <summary>Every walkable tile at the same height, and the blocked ones left blocked.</summary>
    private static IEnumerable<short> Flattened(Heightmap map)
    {
        foreach (HeightmapTile tile in map.Tiles)
            yield return tile.IsFree ? (short)0 : (short)-1;
    }

    internal static bool FriendAtDoor(Doorbell ring, IEnumerable<Friend> friends)
    {
        if (ring.UnityUserId is { } user_id)
            return friends.Any(friend => friend.Id == user_id);
        return friends.Any(friend =>
            string.Equals(friend.Name, ring.UserName, StringComparison.OrdinalIgnoreCase));
    }

    internal static string RespectLine(string respected, string? respecter, int total)
    {
        string line = respecter is { Length: > 0 }
            ? $"{respecter} respected {respected}"
            : $"{respected} was respected";
        return total >= 0 ? $"{line} ({total} total)" : line;
    }

    /// <summary>
    /// Puts a line in front of you that only you can see.
    /// </summary>
    /// <remarks>
    /// Written to the client as a whisper from yourself, so nothing is said in the room and nobody
    /// else is sent anything at all.
    /// </remarks>
    private void Say(string line)
    {
        if (Game?.Room.Avatars.FirstOrDefault(IsSelf) is not { } me)
            return;

        Try(() => Game.People.Find(me, line));
    }

    /// <summary>Binds one outgoing message and parses it into a model.</summary>
    private void OutOf<T>(string name, Action<T> handler) where T : IParserComposer<T> =>
        OutOf<T>(name, (message, _) => handler(message));

    private void OutOf<T>(string name, Action<T, Intercept> handler) where T : IParserComposer<T> =>
        Bind(Direction.Out, name, intercept =>
        {
            T message;
            try
            {
                message = intercept.Packet.Reader().Parse<T>();
            }
            catch
            {
                return;
            }

            handler(message, intercept);
        });

    private void OutOf<T>(MessageContract<T> contract, Action<T, Intercept> handler)
        where T : IParserComposer<T> =>
        Bind(contract.Key, intercept =>
        {
            T message;
            try
            {
                PacketReader reader = intercept.Packet.Reader();
                message = contract.Parse(in reader);
                if (reader.Available != 0)
                    return;
            }
            catch
            {
                return;
            }

            handler(message, intercept);
        });

    /// <summary>Swallows one incoming message whenever the switch behind it is on.</summary>
    private void Swallow(string name, Func<bool> when) =>
        Bind(Direction.In, name, intercept =>
        {
            if (when())
                intercept.Block();
        });

    private void Swallow(MessageKey key, Func<bool> when) =>
        Bind(key, intercept =>
        {
            if (when())
                intercept.Block();
        });

    /// <summary>
    /// Drops chat from whatever is switched off, before it reaches the screen.
    /// </summary>
    /// <remarks>
    /// The packet says who is speaking only by room index, so what is speaking has to be looked up
    /// in the room. Wired has no avatar at all — it speaks through the room itself, which is the
    /// index the room never handed out.
    /// </remarks>
    private void MuteChat(MessageContract<AvatarChat> contract) =>
        In(contract, (chat, intercept) =>
        {
            if (MuteAll)
            {
                intercept.Block();
                return;
            }

            if (!MuteBots && !MutePets && !MuteWired)
                return;

            Avatar? speaker = Game?.Room.AvatarByIndex(chat.Index);
            bool muted = speaker switch
            {
                Bot => MuteBots,
                Pet pet => MutePets || (MutePetCommands && IsCommand(pet, chat.Message)),
                null => MuteWired,
                _ => false
            };

            if (muted)
                intercept.Block();
        });

    /// <summary>
    /// Whether a line from a pet is one of its commands rather than something it said.
    /// </summary>
    /// <remarks>
    /// A command is echoed back with the pet's own name in front of it, which is the only thing
    /// separating "sit" from a pet saying something of its own accord.
    /// </remarks>
    private static bool IsCommand(Pet pet, string line) =>
        pet.Name.Length > 0 &&
        line.StartsWith(pet.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Keeps the session awake.
    /// </summary>
    /// <remarks>
    /// The hotel decides you have gone away after a quiet stretch and, later, disconnects you. A
    /// gesture is the cheapest thing that counts as being at the keyboard and the only one that
    /// does not move you, say anything or touch what is in the room.
    /// </remarks>
    private void AntiIdleLoop()
    {
        _idle?.Dispose();
        _idle = new Timer(
            _ =>
            {
                if (!AntiIdle || Game is null)
                    return;
                try
                {
                    Game.RoomAvatarOperations?.Expression(
                        new Application.RoomAvatarExpressionRequest(0));
                }
                catch
                {
                    // Between sessions there is nothing to send to. The next tick will find one.
                }
            },
            null,
            TimeSpan.FromSeconds(AntiIdleSeconds),
            TimeSpan.FromSeconds(AntiIdleSeconds));
    }

    public void Unbind()
    {
        _bound = false;
        _idle?.Dispose();
        _idle = null;
        foreach (IDisposable binding in _bindings)
            binding.Dispose();
        _bindings.Clear();
    }

    public void Dispose() => Unbind();

    /// <summary>
    /// Binds one outgoing message by name.
    /// </summary>
    /// <remarks>
    /// <see cref="ClientType.None"/> so the name is resolved against whichever client is connected;
    /// the map translates a Flash spelling to the Unity header and back. Binding a header id here
    /// would work on one client and silently do nothing on the other.
    /// </remarks>
    private void Out(string name, Action<Intercept> handler) => Bind(Direction.Out, name, handler);

    private void In(string name, Action handler) =>
        Bind(Direction.In, name, _ => handler());

    private void In<T>(string name, Action<T> handler) where T : IParserComposer<T> =>
        In<T>(name, (message, _) => handler(message));

    private void In<T>(string name, Action<T, Intercept> handler) where T : IParserComposer<T> =>
        Bind(Direction.In, name, Parsed(handler));

    private void In<T>(MessageKey key, Action<T, Intercept> handler) where T : IParserComposer<T> =>
        Bind(key, Parsed(handler));

    private void In<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T> =>
        In(contract, (message, _) => handler(message));

    private void In<T>(MessageContract<T> contract, Action<T, Intercept> handler)
        where T : IParserComposer<T> =>
        Bind(contract.Key, intercept =>
        {
            try
            {
                PacketReader reader = intercept.Packet.Reader();
                T message = contract.Parse(in reader);
                if (reader.Available == 0)
                    handler(message, intercept);
            }
            catch
            {
            }
        });

    private static Action<Intercept> Parsed<T>(Action<T, Intercept> handler) where T : IParserComposer<T> =>
        intercept =>
        {
            T message;
            try
            {
                message = intercept.Packet.Reader().Parse<T>();
            }
            catch
            {
                return;
            }

            handler(message, intercept);
        };

    private void Bind(Direction direction, string name, Action<Intercept> handler)
        => Bind(ClientType.None, direction, name, handler);

    private void Bind(MessageKey key, Action<Intercept> handler)
    {
        try
        {
            _bindings.Add(_interceptor.Intercept(key, handler));
        }
        catch
        {
        }
    }

    private void Bind(ClientType client, Direction direction, string name, Action<Intercept> handler)
    {
        try
        {
            _bindings.Add(_interceptor.Intercept(
                new Identifier(client, direction, name),
                handler));
        }
        catch
        {
            // A build that does not carry the message simply does not get the rule. Better a switch
            // that does nothing than a session that will not start.
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;

            if (JsonSerializer.Deserialize(File.ReadAllText(_path), JsonContext.Default.Document) is not { } document)
                return;

            AntiIdle = document.AntiIdle;
            BlockTrades = document.BlockTrades;
            LetFriendsIn = document.LetFriendsIn;
            NoWalk = document.NoWalk;
            NoTurn = document.NoTurn;
            AlwaysShout = document.AlwaysShout;
            NoTyping = document.NoTyping;
            MuteBots = document.MuteBots;
            MutePets = document.MutePets;
            MuteWired = document.MuteWired;
            PreventFurniUse = document.PreventFurniUse;
            MuteAll = document.MuteAll;
            MuteRespects = document.MuteRespects;
            BlockRoomAds = document.BlockRoomAds;
            BlockClubGifts = document.BlockClubGifts;
            BlockNotifications = document.BlockNotifications;
            DropHandItems = document.DropHandItems;
            BlockRoomInvites = document.BlockRoomInvites;
            BlockFriendRequests = document.BlockFriendRequests;
            AutoAcceptFriendRequests = document.AutoAcceptFriendRequests;
            AntiIdleSeconds = document.AntiIdleSeconds is >= 15 and <= 900 ? document.AntiIdleSeconds : 60;
            AntiIdleOut = document.AntiIdleOut;
            TurnOnReselect = document.TurnOnReselect;
            TurnTowardsClickedTile = document.TurnTowardsClickedTile;
            ClickTo = document.ClickTo;
            ClickMuteMinutes = document.ClickMuteMinutes is >= 1 and <= 1440 ? document.ClickMuteMinutes : 5;
            ClickBanLength = document.ClickBanLength;
            ClickExcludesFriends = document.ClickExcludesFriends;
            RememberPasswords = document.RememberPasswords;
            FlattenFloor = document.FlattenFloor;
            HideAvatars = document.HideAvatars;
            MutePetCommands = document.MutePetCommands;
            ShowRespectCount = document.ShowRespectCount;
            ShiftClickShowsInfo = document.ShiftClickShowsInfo ?? document.CtrlClickShowsInfo;
            ShiftClickHides = document.ShiftClickHides ?? document.CtrlClickHides;
            ShiftClickFindsLink = document.ShiftClickFindsLink ?? document.CtrlClickFindsLink;
            ReturnHandItems = document.ReturnHandItems;
            KeepDirection = document.KeepDirection;
        }
        catch
        {
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new Document
            {
                AntiIdle = AntiIdle,
                BlockTrades = BlockTrades,
                LetFriendsIn = LetFriendsIn,
                NoWalk = NoWalk,
                NoTurn = NoTurn,
                AlwaysShout = AlwaysShout,
                NoTyping = NoTyping,
                MuteBots = MuteBots,
                MutePets = MutePets,
                MuteWired = MuteWired,
                PreventFurniUse = PreventFurniUse,
                MuteAll = MuteAll,
                MuteRespects = MuteRespects,
                BlockRoomAds = BlockRoomAds,
                BlockClubGifts = BlockClubGifts,
                BlockNotifications = BlockNotifications,
                DropHandItems = DropHandItems,
                BlockRoomInvites = BlockRoomInvites,
                BlockFriendRequests = BlockFriendRequests,
                AutoAcceptFriendRequests = AutoAcceptFriendRequests,
                AntiIdleSeconds = AntiIdleSeconds,
                AntiIdleOut = AntiIdleOut,
                TurnOnReselect = TurnOnReselect,
                TurnTowardsClickedTile = TurnTowardsClickedTile,
                ClickTo = ClickTo,
                ClickMuteMinutes = ClickMuteMinutes,
                ClickBanLength = ClickBanLength,
                ClickExcludesFriends = ClickExcludesFriends,
                RememberPasswords = RememberPasswords,
                FlattenFloor = FlattenFloor,
                HideAvatars = HideAvatars,
                MutePetCommands = MutePetCommands,
                ShowRespectCount = ShowRespectCount,
                ShiftClickShowsInfo = ShiftClickShowsInfo,
                ShiftClickHides = ShiftClickHides,
                ShiftClickFindsLink = ShiftClickFindsLink,
                ReturnHandItems = ReturnHandItems,
                KeepDirection = KeepDirection
            }, JsonContext.Default.Document));
        }
        catch
        {
        }
    }
}
