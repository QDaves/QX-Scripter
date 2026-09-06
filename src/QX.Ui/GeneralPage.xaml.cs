using System.Windows;
using System.Windows.Controls;
using Qx.Game;
using Qx.Game.Rules;

namespace Qx.Ui;

/// <summary>
/// Switches that act on the live session.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these is a rule applied to traffic as it passes, so they need somewhere to be
/// applied from. That is <see cref="SessionRules"/>; this page only shows their state and hands
/// changes over to it.
/// </para>
/// <para>
/// Two are shown switched off and cannot be turned on: clicking through users and drawing yourself
/// over furni are both decided inside the client while it renders, and never appear on the wire.
/// Offering them anyway and doing nothing would be worse than saying so.
/// </para>
/// </remarks>
public partial class GeneralPage : GamePage
{
    private SessionRules? _rules;
    private bool _filling;

    public GeneralPage()
    {
        InitializeComponent();
    }

    /// <summary>Set by the window, which owns the rules for the whole session.</summary>
    public SessionRules? Rules
    {
        get => _rules;
        set
        {
            _rules = value;
            Refresh();
        }
    }

    public override void Refresh()
    {
        if (_rules is null)
            return;

        // Guarded, because assigning IsChecked raises the same events a click does and would write
        // the value straight back while the page is only reading it out.
        _filling = true;
        try
        {
            AntiIdle.IsChecked = _rules.AntiIdle;
            AntiIdleOut.IsChecked = _rules.AntiIdleOut;
            AntiTrade.IsChecked = _rules.BlockTrades;
            BlockRoomAds.IsChecked = _rules.BlockRoomAds;
            BlockRoomInvites.IsChecked = _rules.BlockRoomInvites;
            HideAvatars.IsChecked = _rules.HideAvatars;
            FlattenFloor.IsChecked = _rules.FlattenFloor;
            IdleSeconds.Text = _rules.AntiIdleSeconds.ToString();

            NoTurn.IsChecked = _rules.NoTurn;
            TurnOnReselect.IsChecked = _rules.TurnOnReselect;
            NoWalk.IsChecked = _rules.NoWalk;
            TurnTowardsTile.IsChecked = _rules.TurnTowardsClickedTile;

            ClickNothing.IsChecked = _rules.ClickTo is ClickAction.None;
            ClickMute.IsChecked = _rules.ClickTo is ClickAction.Mute;
            ClickKick.IsChecked = _rules.ClickTo is ClickAction.Kick;
            ClickBan.IsChecked = _rules.ClickTo is ClickAction.Ban;
            ClickBounce.IsChecked = _rules.ClickTo is ClickAction.Bounce;
            BanHour.IsChecked = _rules.ClickBanLength is BanLength.Hour;
            BanDay.IsChecked = _rules.ClickBanLength is BanLength.Day;
            BanPerm.IsChecked = _rules.ClickBanLength is BanLength.Permanent;
            MuteMinutes.Text = _rules.ClickMuteMinutes.ToString();
            ClickExcludeFriends.IsChecked = _rules.ClickExcludesFriends;

            RememberPasswords.IsChecked = _rules.RememberPasswords;
            AutoAcceptDoorbell.IsChecked = _rules.LetFriendsIn;

            NoTyping.IsChecked = _rules.NoTyping;
            AlwaysShout.IsChecked = _rules.AlwaysShout;
            MuteAll.IsChecked = _rules.MuteAll;
            MuteBots.IsChecked = _rules.MuteBots;
            MutePets.IsChecked = _rules.MutePets;
            MutePetCommands.IsChecked = _rules.MutePetCommands;
            MuteWired.IsChecked = _rules.MuteWired;
            MuteRespects.IsChecked = _rules.MuteRespects;
            ShowRespectCount.IsChecked = _rules.ShowRespectCount;

            PreventUse.IsChecked = _rules.PreventFurniUse;
            ShiftClickInfo.IsChecked = _rules.ShiftClickShowsInfo;
            ShiftClickHide.IsChecked = _rules.ShiftClickHides;
            ShiftClickLink.IsChecked = _rules.ShiftClickFindsLink;

            DropHandItems.IsChecked = _rules.DropHandItems;
            ReturnHandItems.IsChecked = _rules.ReturnHandItems;
            KeepDirection.IsChecked = _rules.KeepDirection;

            BlockClubGifts.IsChecked = _rules.BlockClubGifts;
            BlockNotifications.IsChecked = _rules.BlockNotifications;
            BlockFriendRequests.IsChecked = _rules.BlockFriendRequests;
            AutoAcceptFriendRequests.IsChecked = _rules.AutoAcceptFriendRequests;
        }
        finally
        {
            _filling = false;
        }

        // The one thing that belongs on a page of switches is how many of them are doing something.
        // Which client is connected moved to settings, where facts about the setup live.
        Subheading.Text = _rules.Active == 0
            ? "Nothing is being changed. Every switch here acts on the live session."
            : $"{_rules.Active} {(_rules.Active == 1 ? "rule is" : "rules are")} acting on this session.";

        ShowDependencies();
    }

    /// <summary>
    /// Greys out whatever the current choices make meaningless.
    /// </summary>
    /// <remarks>
    /// Switches that contradict each other say so here rather than fighting quietly at the far end
    /// of the wire, where the loser is whichever rule happened to be bound second.
    /// </remarks>
    private void ShowDependencies()
    {
        if (_rules is null)
            return;

        foreach (CheckBox box in new[] { MuteBots, MutePets, MutePetCommands, MuteWired, MuteRespects })
            box.IsEnabled = _rules.MuteAll is false;

        TurnOnReselect.IsEnabled = _rules.NoTurn;
        TurnTowardsTile.IsEnabled = _rules.NoWalk;
        MuteMinutes.IsEnabled = _rules.ClickTo is ClickAction.Mute;

        // Either the respects are muted or they are described; both at once is a contradiction.
        ShowRespectCount.IsEnabled = !_rules.MuteAll && !_rules.MuteRespects;

        // Anti-idle sends a gesture, so it cannot run with gestures switched off, and the two idle
        // rules solve the same problem in opposite ways.
        AntiIdleOut.IsEnabled = _rules.AntiIdle is false;

        AutoAcceptFriendRequests.IsEnabled = _rules.BlockFriendRequests is false;

        // Handing it back and dropping it are two answers to the same event.
        ReturnHandItems.IsEnabled = _rules.DropHandItems is false;
    }

    private void SwitchChanged(object sender, RoutedEventArgs e)
    {
        if (_filling || _rules is null)
            return;

        _rules.AntiIdle = AntiIdle.IsChecked == true;
        _rules.AntiIdleOut = AntiIdleOut.IsChecked == true;
        _rules.BlockTrades = AntiTrade.IsChecked == true;
        _rules.BlockRoomAds = BlockRoomAds.IsChecked == true;
        _rules.BlockRoomInvites = BlockRoomInvites.IsChecked == true;
        _rules.HideAvatars = HideAvatars.IsChecked == true;
        _rules.FlattenFloor = FlattenFloor.IsChecked == true;

        _rules.NoTurn = NoTurn.IsChecked == true;
        _rules.TurnOnReselect = TurnOnReselect.IsChecked == true;
        _rules.NoWalk = NoWalk.IsChecked == true;
        _rules.TurnTowardsClickedTile = TurnTowardsTile.IsChecked == true;

        _rules.ClickTo =
            ClickMute.IsChecked == true ? ClickAction.Mute :
            ClickKick.IsChecked == true ? ClickAction.Kick :
            ClickBan.IsChecked == true ? ClickAction.Ban :
            ClickBounce.IsChecked == true ? ClickAction.Bounce :
            ClickAction.None;
        _rules.ClickBanLength =
            BanDay.IsChecked == true ? BanLength.Day :
            BanPerm.IsChecked == true ? BanLength.Permanent :
            BanLength.Hour;
        _rules.ClickExcludesFriends = ClickExcludeFriends.IsChecked == true;

        _rules.RememberPasswords = RememberPasswords.IsChecked == true;
        _rules.LetFriendsIn = AutoAcceptDoorbell.IsChecked == true;

        _rules.NoTyping = NoTyping.IsChecked == true;
        _rules.AlwaysShout = AlwaysShout.IsChecked == true;
        _rules.MuteAll = MuteAll.IsChecked == true;
        _rules.MuteBots = MuteBots.IsChecked == true;
        _rules.MutePets = MutePets.IsChecked == true;
        _rules.MutePetCommands = MutePetCommands.IsChecked == true;
        _rules.MuteWired = MuteWired.IsChecked == true;
        _rules.MuteRespects = MuteRespects.IsChecked == true;
        _rules.ShowRespectCount = ShowRespectCount.IsChecked == true;

        _rules.PreventFurniUse = PreventUse.IsChecked == true;
        _rules.ShiftClickShowsInfo = ShiftClickInfo.IsChecked == true;
        _rules.ShiftClickHides = ShiftClickHide.IsChecked == true;
        _rules.ShiftClickFindsLink = ShiftClickLink.IsChecked == true;

        _rules.DropHandItems = DropHandItems.IsChecked == true;
        _rules.ReturnHandItems = ReturnHandItems.IsChecked == true;
        _rules.KeepDirection = KeepDirection.IsChecked == true;

        _rules.BlockClubGifts = BlockClubGifts.IsChecked == true;
        _rules.BlockNotifications = BlockNotifications.IsChecked == true;
        _rules.BlockFriendRequests = BlockFriendRequests.IsChecked == true;
        _rules.AutoAcceptFriendRequests = AutoAcceptFriendRequests.IsChecked == true;

        _rules.Save();
        Refresh();
    }

    /// <summary>
    /// Reads the anti-idle interval back, holding it to what the hotel would thank us for.
    /// </summary>
    /// <remarks>
    /// Below fifteen seconds is a flood and above fifteen minutes is past the point the hotel has
    /// already decided you are away, so both ends are held rather than reported as an error.
    /// </remarks>
    private void IdleSecondsChanged(object sender, RoutedEventArgs e)
    {
        if (_filling || _rules is null)
            return;

        int seconds = int.TryParse(IdleSeconds.Text, out int typed)
            ? Math.Clamp(typed, 15, 900)
            : _rules.AntiIdleSeconds;

        if (seconds != _rules.AntiIdleSeconds)
        {
            _rules.AntiIdleSeconds = seconds;
            _rules.Save();
        }

        Refresh();
    }

    private void MuteMinutesChanged(object sender, RoutedEventArgs e)
    {
        if (_filling || _rules is null)
            return;

        _rules.ClickMuteMinutes = int.TryParse(MuteMinutes.Text, out int typed)
            ? Math.Clamp(typed, 1, 1440)
            : _rules.ClickMuteMinutes;

        _rules.Save();
        Refresh();
    }
}
