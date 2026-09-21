using Qx.Game;
using Qx.Game.Rules;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Game;

namespace Qx.Presentation.ViewModels.General;

public sealed class GeneralViewModel : PageViewModel
{
    public const string ShiftClickUnavailable =
        "Not available on this system: QX cannot tell whether shift is held while the game has focus.";

    public const string MarketplaceUnavailable =
        "Not available yet.";

    readonly SessionRules _rules;
    readonly IReadOnlyList<GeneralRowViewModel> _rows;
    bool _filling;

    public GeneralViewModel(DesktopRuntime runtime, IGameGateway game, IKeyboardState keyboard)
        : base(PageKey.General)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(keyboard);
        _rules = runtime.Rules;
        FirstColumn = [MeSection(Apply), MovementSection(Apply), RoomSection(Apply), PeopleSection(Apply)];
        SecondColumn = [ChatSection(Apply), HandItemsSection(Apply), FurniSection(Apply, keyboard.IsSupported), BlockingSection(Apply)];
        Sections = [.. FirstColumn, .. SecondColumn];
        _rows = [.. Sections.SelectMany(static section => section.Rows)];
        Refresh();
        game.SessionChanged += Refresh;
        Own(() => game.SessionChanged -= Refresh);
        State.ShowReady();
    }

    public IReadOnlyList<GeneralSectionViewModel> Sections { get; }

    public IReadOnlyList<GeneralSectionViewModel> FirstColumn { get; }

    public IReadOnlyList<GeneralSectionViewModel> SecondColumn { get; }

    protected override Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        Refresh();
        return Task.CompletedTask;
    }

    static GeneralSwitchViewModel Rule(
        string label,
        string description,
        Func<SessionRules, bool> read,
        Action<SessionRules, bool> write,
        Action changed,
        Func<SessionRules, bool>? available = null) =>
        new(label, description, read, write, changed, available);

    static GeneralSectionViewModel MeSection(Action changed) =>
        new("Me", "", [
            Rule(
                "Anti-idle",
                "Keeps you from going idle",
                static rules => rules.AntiIdle,
                static (rules, on) => rules.AntiIdle = on,
                changed),
            Rule(
                "Anti-idle-out",
                "Lets you idle, and answers only the moment the hotel would put you out",
                static rules => rules.AntiIdleOut,
                static (rules, on) => rules.AntiIdleOut = on,
                changed,
                static rules => !rules.AntiIdle),
            Rule(
                "Anti-trade",
                "Closes a trade the moment somebody else opens one",
                static rules => rules.BlockTrades,
                static (rules, on) => rules.BlockTrades = on,
                changed)
        ]);

    static GeneralSectionViewModel MovementSection(Action changed) =>
        new("Movement", "", [
            Rule(
                "No turn",
                "Clicking somebody no longer turns you towards them",
                static rules => rules.NoTurn,
                static (rules, on) => rules.NoTurn = on,
                changed),
            Rule(
                "Except when re-selecting a user",
                "",
                static rules => rules.TurnOnReselect,
                static (rules, on) => rules.TurnOnReselect = on,
                changed,
                static rules => rules.NoTurn),
            Rule(
                "No walk",
                "",
                static rules => rules.NoWalk,
                static (rules, on) => rules.NoWalk = on,
                changed),
            Rule(
                "Turn towards the tile clicked",
                "",
                static rules => rules.TurnTowardsClickedTile,
                static (rules, on) => rules.TurnTowardsClickedTile = on,
                changed,
                static rules => rules.NoWalk)
        ]);

    static GeneralSectionViewModel RoomSection(Action changed) =>
        new("Room", "", [
            Rule(
                "Hide all avatars",
                "Takes effect on the next room you enter",
                static rules => rules.HideAvatars,
                static (rules, on) => rules.HideAvatars = on,
                changed),
            Rule(
                "Flatten floor plan",
                "Every walkable tile drawn level. Takes effect on the next room",
                static rules => rules.FlattenFloor,
                static (rules, on) => rules.FlattenFloor = on,
                changed),
            Rule(
                "Block room adverts",
                "",
                static rules => rules.BlockRoomAds,
                static (rules, on) => rules.BlockRoomAds = on,
                changed),
            Rule(
                "Block room invitations",
                "",
                static rules => rules.BlockRoomInvites,
                static (rules, on) => rules.BlockRoomInvites = on,
                changed),
            Rule(
                "Remember room passwords",
                "Saved per hotel and room, and sent again the next time you enter",
                static rules => rules.RememberPasswords,
                static (rules, on) => rules.RememberPasswords = on,
                changed),
            Rule(
                "Let friends in",
                "",
                static rules => rules.LetFriendsIn,
                static (rules, on) => rules.LetFriendsIn = on,
                changed)
        ]);

    static GeneralSectionViewModel PeopleSection(Action changed) =>
        new("People", "What clicking somebody does instead of turning towards them.", [
            new GeneralChoiceViewModel(
                "Clicking somebody",
                "",
                [
                    new GeneralOption("Nothing"),
                    new GeneralOption("Kick"),
                    new GeneralOption("Bounce", "Ban and unban at once, which puts them out with no kick notice"),
                    new GeneralOption("Mute"),
                    new GeneralOption("Ban")
                ],
                static rules => rules.ClickTo switch
                {
                    ClickAction.Kick => 1,
                    ClickAction.Bounce => 2,
                    ClickAction.Mute => 3,
                    ClickAction.Ban => 4,
                    _ => 0
                },
                static (rules, index) => rules.ClickTo = index switch
                {
                    1 => ClickAction.Kick,
                    2 => ClickAction.Bounce,
                    3 => ClickAction.Mute,
                    4 => ClickAction.Ban,
                    _ => ClickAction.None
                },
                changed),
            new GeneralNumberViewModel(
                "Mute for",
                "minutes",
                1,
                1440,
                "Click-to mute length in minutes",
                static rules => rules.ClickMuteMinutes,
                static (rules, minutes) => rules.ClickMuteMinutes = minutes,
                changed,
                static rules => rules.ClickTo is ClickAction.Mute),
            new GeneralChoiceViewModel(
                "Ban for",
                "",
                [new GeneralOption("an hour"), new GeneralOption("a day"), new GeneralOption("ever")],
                static rules => rules.ClickBanLength switch
                {
                    BanLength.Day => 1,
                    BanLength.Permanent => 2,
                    _ => 0
                },
                static (rules, index) => rules.ClickBanLength = index switch
                {
                    1 => BanLength.Day,
                    2 => BanLength.Permanent,
                    _ => BanLength.Hour
                },
                changed,
                static rules => rules.ClickTo is ClickAction.Ban),
            Rule(
                "Never do this to friends",
                "",
                static rules => rules.ClickExcludesFriends,
                static (rules, on) => rules.ClickExcludesFriends = on,
                changed)
        ]);

    static GeneralSectionViewModel ChatSection(Action changed) =>
        new("Chat", "", [
            Rule(
                "No typing indicator",
                "",
                static rules => rules.NoTyping,
                static (rules, on) => rules.NoTyping = on,
                changed),
            Rule(
                "Always shout",
                "",
                static rules => rules.AlwaysShout,
                static (rules, on) => rules.AlwaysShout = on,
                changed),
            Rule(
                "Mute everything",
                "",
                static rules => rules.MuteAll,
                static (rules, on) => rules.MuteAll = on,
                changed),
            Rule(
                "Mute bots",
                "",
                static rules => rules.MuteBots,
                static (rules, on) => rules.MuteBots = on,
                changed,
                static rules => !rules.MuteAll),
            Rule(
                "Mute pets",
                "",
                static rules => rules.MutePets,
                static (rules, on) => rules.MutePets = on,
                changed,
                static rules => !rules.MuteAll),
            Rule(
                "Mute pet commands",
                "",
                static rules => rules.MutePetCommands,
                static (rules, on) => rules.MutePetCommands = on,
                changed,
                static rules => !rules.MuteAll),
            Rule(
                "Mute wired messages",
                "",
                static rules => rules.MuteWired,
                static (rules, on) => rules.MuteWired = on,
                changed,
                static rules => !rules.MuteAll),
            Rule(
                "Mute respects and scratches",
                "",
                static rules => rules.MuteRespects,
                static (rules, on) => rules.MuteRespects = on,
                changed,
                static rules => !rules.MuteAll),
            Rule(
                "Say who respected whom and their total",
                "",
                static rules => rules.ShowRespectCount,
                static (rules, on) => rules.ShowRespectCount = on,
                changed,
                static rules => !rules.MuteAll && !rules.MuteRespects)
        ]);

    static GeneralSectionViewModel FurniSection(Action changed, bool shift_supported)
    {
        string note = shift_supported ? "" : ShiftClickUnavailable;
        return new GeneralSectionViewModel("Furni", "", [
            Rule(
                "Prevent using furni",
                "",
                static rules => rules.PreventFurniUse,
                static (rules, on) => rules.PreventFurniUse = on,
                changed),
            Rule(
                "Shift-click to show info",
                note,
                static rules => rules.ShiftClickShowsInfo,
                static (rules, on) => rules.ShiftClickShowsInfo = on,
                changed,
                _ => shift_supported),
            Rule(
                "Shift-click to hide",
                note,
                static rules => rules.ShiftClickHides,
                static (rules, on) => rules.ShiftClickHides = on,
                changed,
                _ => shift_supported),
            Rule(
                "Shift-click to find a teleport's pair",
                note,
                static rules => rules.ShiftClickFindsLink,
                static (rules, on) => rules.ShiftClickFindsLink = on,
                changed,
                _ => shift_supported),
            GeneralSwitchViewModel.Unavailable("Shift-click to fetch marketplace stats", MarketplaceUnavailable, changed)
        ]);
    }

    static GeneralSectionViewModel HandItemsSection(Action changed) =>
        new("Hand items", "Drinks and other items handed to you", [
            new GeneralChoiceViewModel(
                "When someone hands me an item",
                "Return passes it straight back to the person who gave it to you",
                [new("Keep"), new("Return"), new("Drop")],
                static rules => rules.DropHandItems ? 2 : rules.ReturnHandItems ? 1 : 0,
                static (rules, choice) =>
                {
                    rules.ReturnHandItems = choice == 1;
                    rules.DropHandItems = choice == 2;
                },
                changed),
            Rule(
                "Keep facing the way I was when handed something",
                "Being handed something turns you towards whoever handed it. This turns you straight back",
                static rules => rules.KeepDirection,
                static (rules, on) => rules.KeepDirection = on,
                changed)
        ]);

    static GeneralSectionViewModel BlockingSection(Action changed) =>
        new("Blocking", "", [
            Rule(
                "Block club gift notification",
                "",
                static rules => rules.BlockClubGifts,
                static (rules, on) => rules.BlockClubGifts = on,
                changed),
            Rule(
                "Block hotel notices",
                "",
                static rules => rules.BlockNotifications,
                static (rules, on) => rules.BlockNotifications = on,
                changed),
            Rule(
                "Block friend requests",
                "",
                static rules => rules.BlockFriendRequests,
                static (rules, on) => rules.BlockFriendRequests = on,
                changed),
            Rule(
                "Accept every friend request as it arrives",
                "",
                static rules => rules.AutoAcceptFriendRequests,
                static (rules, on) => rules.AutoAcceptFriendRequests = on,
                changed,
                static rules => !rules.BlockFriendRequests)
        ]);

    void Refresh()
    {
        _filling = true;
        try
        {
            foreach (GeneralRowViewModel row in _rows)
                row.Read(_rules);
        }
        finally
        {
            _filling = false;
        }
        foreach (GeneralRowViewModel row in _rows)
            row.Gate(_rules);
    }

    void Apply()
    {
        if (_filling)
            return;
        foreach (GeneralRowViewModel row in _rows)
            row.Write(_rules);
        _rules.Save();
        Refresh();
    }
}
