namespace Qx.Model.Wired;

/// <summary>Typed settings for AvatarSaysSomething (trigger code 0).</summary>
public sealed class WiredAvatarSaysSomethingTriggerForm : WiredForm
{
    internal WiredAvatarSaysSomethingTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets owner only. 0=false, 1=true; any nonzero incoming value selects the checkbox</summary>
    /// <remarks>Client label: wiredfurni.params.chat.onlyowner. None</remarks>
    public bool OwnerOnly
    {
        get => GetField("owner_only").Boolean.GetValueOrDefault();
        set => SetField("owner_only", new(Boolean: value));
    }

    /// <summary>Gets or sets match mode. 0 contains keyword; 1 exact match; 2 all text</summary>
    /// <remarks>Client label: wiredfurni.params.chattriggertype. None</remarks>
    public WiredMatchModeOption MatchMode
    {
        get => (WiredMatchModeOption)GetField("match_mode").Integer.GetValueOrDefault();
        set => SetField("match_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets hide message. 0=false, 1=true; any nonzero incoming value selects the checkbox</summary>
    /// <remarks>Client label: wiredfurni.params.chat.hide. None</remarks>
    public bool HideMessage
    {
        get => GetField("hide_message").Boolean.GetValueOrDefault();
        set => SetField("hide_message", new(Boolean: value));
    }

    /// <summary>Gets or sets text. String, UI maximum 1000 characters</summary>
    /// <remarks>Client label: wiredfurni.params.whatissaid. Text section disabled when match_mode=2; writer still returns its text</remarks>
    public string Text
    {
        get => GetField("text").Text!;
        set => SetField("text", new(Text: value));
    }
}

/// <summary>Typed settings for WalksOnFurniture (trigger code 1).</summary>
public sealed class WiredWalksOnFurnitureTriggerForm : WiredForm
{
    internal WiredWalksOnFurnitureTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for WalksOffFurniture (trigger code 2).</summary>
public sealed class WiredWalksOffFurnitureTriggerForm : WiredForm
{
    internal WiredWalksOffFurnitureTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for TriggerOnce (trigger code 3).</summary>
public sealed class WiredTriggerOnceTriggerForm : WiredForm
{
    internal WiredTriggerOnceTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets delay pulses. 1..1200 in steps of 1; two pulses per second</summary>
    /// <remarks>Client label: wiredfurni.params.settime2. None</remarks>
    public int DelayPulses
    {
        get => GetField("delay_pulses").Integer.GetValueOrDefault();
        set => SetField("delay_pulses", new(Integer: value));
    }
}

/// <summary>Typed settings for UseStuff (trigger code 4).</summary>
public sealed class WiredUseStuffTriggerForm : WiredForm
{
    internal WiredUseStuffTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for TriggerPeriodically (trigger code 6).</summary>
public sealed class WiredTriggerPeriodicallyTriggerForm : WiredForm
{
    internal WiredTriggerPeriodicallyTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets interval pulses. 1..120 in steps of 1; two pulses per second</summary>
    /// <remarks>Client label: wiredfurni.params.settime3. None</remarks>
    public int IntervalPulses
    {
        get => GetField("interval_pulses").Integer.GetValueOrDefault();
        set => SetField("interval_pulses", new(Integer: value));
    }
}

/// <summary>Typed settings for AvatarEntersRoom (trigger code 7).</summary>
public sealed class WiredAvatarEntersRoomTriggerForm : WiredForm
{
    internal WiredAvatarEntersRoomTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for GameStarts (trigger code 8).</summary>
public sealed class WiredGameStartsTriggerForm : WiredForm
{
    internal WiredGameStartsTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for GameEnds (trigger code 9).</summary>
public sealed class WiredGameEndsTriggerForm : WiredForm
{
    internal WiredGameEndsTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for ScoreAchieved (trigger code 10).</summary>
public sealed class WiredScoreAchievedTriggerForm : WiredForm
{
    internal WiredScoreAchievedTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets score. 1..1000 in steps of 1</summary>
    /// <remarks>Client label: wiredfurni.params.setscore2. None</remarks>
    public int Score
    {
        get => GetField("score").Integer.GetValueOrDefault();
        set => SetField("score", new(Integer: value));
    }

    /// <summary>Gets or sets team. 0 any, 1 red, 2 green, 3 blue, 4 yellow</summary>
    /// <remarks>Client label: wiredfurni.params.team. None</remarks>
    public WiredTeamOption Team
    {
        get => (WiredTeamOption)GetField("team").Integer.GetValueOrDefault();
        set => SetField("team", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for AvatarCaught (trigger code 11).</summary>
public sealed class WiredAvatarCaughtTriggerForm : WiredForm
{
    internal WiredAvatarCaughtTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for PeriodicLong (trigger code 12).</summary>
public sealed class WiredPeriodicLongTriggerForm : WiredForm
{
    internal WiredPeriodicLongTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets interval units. 1..120 in steps of 1; each stored unit is five seconds</summary>
    /// <remarks>Client label: wiredfurni.params.settime3. None</remarks>
    public int IntervalUnits
    {
        get => GetField("interval_units").Integer.GetValueOrDefault();
        set => SetField("interval_units", new(Integer: value));
    }
}

/// <summary>Typed settings for BotDestinationReached (trigger code 13).</summary>
public sealed class WiredBotDestinationReachedTriggerForm : WiredForm
{
    internal WiredBotDestinationReachedTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets bot name. String, UI maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.bot.name. None</remarks>
    public string BotName
    {
        get => GetField("bot_name").Text!;
        set => SetField("bot_name", new(Text: value));
    }
}

/// <summary>Typed settings for BotAvatarReached (trigger code 14).</summary>
public sealed class WiredBotAvatarReachedTriggerForm : WiredForm
{
    internal WiredBotAvatarReachedTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets bot name. String, UI maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.bot.name. None</remarks>
    public string BotName
    {
        get => GetField("bot_name").Text!;
        set => SetField("bot_name", new(Text: value));
    }
}

/// <summary>Typed settings for ClockReachTime (trigger code 15).</summary>
public sealed class WiredClockReachTimeTriggerForm : WiredForm
{
    internal WiredClockReachTimeTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets seconds. 0..59, integer component of the seconds slider</summary>
    /// <remarks>Client label: wiredfurni.params.clock_seconds_elapsed. None</remarks>
    public int Seconds
    {
        get => GetField("seconds").Integer.GetValueOrDefault();
        set => SetField("seconds", new(Integer: value));
    }

    /// <summary>Gets or sets minutes. 0..99</summary>
    /// <remarks>Client label: wiredfurni.params.clock_minutes_elapsed. None</remarks>
    public int Minutes
    {
        get => GetField("minutes").Integer.GetValueOrDefault();
        set => SetField("minutes", new(Integer: value));
    }

    /// <summary>Gets or sets half second. 0 or 1</summary>
    /// <remarks>Client label: wiredfurni.params.clock_seconds_elapsed. None</remarks>
    public int HalfSecond
    {
        get => GetField("half_second").Integer.GetValueOrDefault();
        set => SetField("half_second", new(Integer: value));
    }
}

/// <summary>Typed settings for UserPerformsAction (trigger code 16).</summary>
public sealed class WiredUserPerformsActionTriggerForm : WiredForm
{
    internal WiredUserPerformsActionTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets action. 0 wave, 1 blow, 2 laugh, 3 respect, 4 awake, 5 sleep, 6 sit, 7 stand, 8 lay, 10 sign, 11 dance, 67 client action named 67</summary>
    /// <remarks>Client label: wiredfurni.params.action_selection. None</remarks>
    public WiredActionOption Action
    {
        get => (WiredActionOption)GetField("action").Integer.GetValueOrDefault();
        set => SetField("action", new(Integer: (int)value));
    }

    /// <summary>Gets or sets action filter. Empty = no extra filter; sign 10 uses decimal 0..17; dance 11 uses &#x27;dance N&#x27; with N=1..4</summary>
    /// <remarks>Client label: wiredfurni.params.sign_filter / wiredfurni.params.dance_filter. Only actions 10 and 11 expose extra controls</remarks>
    public string ActionFilter
    {
        get => GetField("action_filter").Text!;
        set => SetField("action_filter", new(Text: value));
    }
}

/// <summary>Typed settings for ReceiveSignal (trigger code 17).</summary>
public sealed class WiredReceiveSignalTriggerForm : WiredForm
{
    internal WiredReceiveSignalTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for AvatarClicksFurni (trigger code 18).</summary>
public sealed class WiredAvatarClicksFurniTriggerForm : WiredForm
{
    internal WiredAvatarClicksFurniTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for PeriodicShort (trigger code 19).</summary>
public sealed class WiredPeriodicShortTriggerForm : WiredForm
{
    internal WiredPeriodicShortTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets interval units. 1..10 in steps of 1; each stored unit is 50 milliseconds</summary>
    /// <remarks>Client label: wiredfurni.params.setshorttime. None</remarks>
    public int IntervalUnits
    {
        get => GetField("interval_units").Integer.GetValueOrDefault();
        set => SetField("interval_units", new(Integer: value));
    }
}

/// <summary>Typed settings for StateChange (trigger code 20).</summary>
public sealed class WiredStateChangeTriggerForm : WiredForm
{
    internal WiredStateChangeTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets state mode. 0 all states (state_trigger.0), 1 current state (state_trigger.1)</summary>
    /// <remarks>Client label: wiredfurni.params.select_options. None</remarks>
    public WiredStateModeOption StateMode
    {
        get => (WiredStateModeOption)GetField("state_mode").Integer.GetValueOrDefault();
        set => SetField("state_mode", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for ClickTile (trigger code 21).</summary>
public sealed class WiredClickTileTriggerForm : WiredForm
{
    internal WiredClickTileTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for VariableUpdate (trigger code 22).</summary>
public sealed class WiredVariableUpdateTriggerForm : WiredForm
{
    internal WiredVariableUpdateTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets variable id. Variable with canInterceptChanges=true</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string VariableId
    {
        get => GetField("variable_id").Text!;
        set => SetField("variable_id", new(Text: value));
    }

    /// <summary>Gets or sets created. 0=false, 1=true</summary>
    /// <remarks>Client label: wiredfurni.params.variables.trigger_options.0. None</remarks>
    public bool Created
    {
        get => GetField("created").Boolean.GetValueOrDefault();
        set => SetField("created", new(Boolean: value));
    }

    /// <summary>Gets or sets value changed. 0=false, 1=true</summary>
    /// <remarks>Client label: wiredfurni.params.variables.trigger_options.1. None</remarks>
    public bool ValueChanged
    {
        get => GetField("value_changed").Boolean.GetValueOrDefault();
        set => SetField("value_changed", new(Boolean: value));
    }

    /// <summary>Gets or sets deleted. 0=false, 1=true</summary>
    /// <remarks>Client label: wiredfurni.params.variables.trigger_options.2. None</remarks>
    public bool Deleted
    {
        get => GetField("deleted").Boolean.GetValueOrDefault();
        set => SetField("deleted", new(Boolean: value));
    }

    /// <summary>Gets or sets change mask. Bit 0 increased, bit 1 decreased, bit 2 unchanged</summary>
    /// <remarks>Client label: wiredfurni.params.variables.trigger_options.1. None</remarks>
    public WiredChangeMaskFlags ChangeMask
    {
        get => (WiredChangeMaskFlags)GetField("change_mask").Integer.GetValueOrDefault();
        set => SetField("change_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets origin mask. Bit 0 this room, bit 1 another room, bit 2 inspection, bit 3 external; -1 when every enabled option is selected</summary>
    /// <remarks>Client label: wiredfurni.params.variables.trigger_origin. None</remarks>
    public WiredOriginMaskFlags OriginMask
    {
        get => (WiredOriginMaskFlags)GetField("origin_mask").Integer.GetValueOrDefault();
        set => SetField("origin_mask", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for AvatarLeavesRoom (trigger code 23).</summary>
public sealed class WiredAvatarLeavesRoomTriggerForm : WiredForm
{
    internal WiredAvatarLeavesRoomTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for UserClicksUser (trigger code 24).</summary>
public sealed class WiredUserClicksUserTriggerForm : WiredForm
{
    internal WiredUserClicksUserTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets suppress avatar menu. 0=false, 1=true</summary>
    /// <remarks>Client label: wiredfurni.params.click_user.block_menu_open. None</remarks>
    public bool SuppressAvatarMenu
    {
        get => GetField("suppress_avatar_menu").Boolean.GetValueOrDefault();
        set => SetField("suppress_avatar_menu", new(Boolean: value));
    }

    /// <summary>Gets or sets suppress rotation. 0=false, 1=true</summary>
    /// <remarks>Client label: wiredfurni.params.click_user.do_not_rotate. None</remarks>
    public bool SuppressRotation
    {
        get => GetField("suppress_rotation").Boolean.GetValueOrDefault();
        set => SetField("suppress_rotation", new(Boolean: value));
    }
}

/// <summary>Typed settings for TransactionCompleted (trigger code 25).</summary>
public sealed class WiredTransactionCompletedTriggerForm : WiredForm
{
    internal WiredTransactionCompletedTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for TransactionFailed (trigger code 26).</summary>
public sealed class WiredTransactionFailedTriggerForm : WiredForm
{
    internal WiredTransactionFailedTriggerForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for ToggleFurniState (action code 0).</summary>
public sealed class WiredToggleFurniStateActionForm : WiredForm
{
    internal WiredToggleFurniStateActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets toggle type. 0 next state, 1 previous state</summary>
    /// <remarks>Client label: wiredfurni.params.toggletype_selection. None</remarks>
    public WiredToggleTypeOption ToggleType
    {
        get => (WiredToggleTypeOption)GetField("toggle_type").Integer.GetValueOrDefault();
        set => SetField("toggle_type", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for Reset (action code 1).</summary>
public sealed class WiredResetActionForm : WiredForm
{
    internal WiredResetActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for MatchSnapshot (action code 3).</summary>
public sealed class WiredMatchSnapshotActionForm : WiredForm
{
    internal WiredMatchSnapshotActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets state. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.condition.state. None</remarks>
    public bool State
    {
        get => GetField("state").Boolean.GetValueOrDefault();
        set => SetField("state", new(Boolean: value));
    }

    /// <summary>Gets or sets direction. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.condition.direction. None</remarks>
    public bool Direction
    {
        get => GetField("direction").Boolean.GetValueOrDefault();
        set => SetField("direction", new(Boolean: value));
    }

    /// <summary>Gets or sets position. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.condition.position. None</remarks>
    public bool Position
    {
        get => GetField("position").Boolean.GetValueOrDefault();
        set => SetField("position", new(Boolean: value));
    }

    /// <summary>Gets or sets altitude. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.condition.altitude. None</remarks>
    public bool Altitude
    {
        get => GetField("altitude").Boolean.GetValueOrDefault();
        set => SetField("altitude", new(Boolean: value));
    }
}

/// <summary>Typed settings for MoveFurni (action code 4).</summary>
public sealed class WiredMoveFurniActionForm : WiredForm
{
    internal WiredMoveFurniActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets movement. 0 none; 4 upper-right; 8 right; 5 lower-right; 9 down; 6 lower-left; 10 left; 7 upper-left; 11 up; 2 either upper-left/lower-right; 3 either upper-right/lower-left; 1 random</summary>
    /// <remarks>Client label: wiredfurni.params.movefurni. None</remarks>
    public WiredMovementOption Movement
    {
        get => (WiredMovementOption)GetField("movement").Integer.GetValueOrDefault();
        set => SetField("movement", new(Integer: (int)value));
    }

    /// <summary>Gets or sets rotation. 0 none, 1 clockwise, 2 counterclockwise, 3 random</summary>
    /// <remarks>Client label: wiredfurni.params.rotatefurni. None</remarks>
    public WiredRotationOption Rotation
    {
        get => (WiredRotationOption)GetField("rotation").Integer.GetValueOrDefault();
        set => SetField("rotation", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for GiveScore (action code 6).</summary>
public sealed class WiredGiveScoreActionForm : WiredForm
{
    internal WiredGiveScoreActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets points. Signed points; magnitude slider 1..1000. Negative means remove points</summary>
    /// <remarks>Client label: wiredfurni.params.setpoints2 / wiredfurni.params.points_operation. None</remarks>
    public int Points
    {
        get => GetField("points").Integer.GetValueOrDefault();
        set => SetField("points", new(Integer: value));
    }

    /// <summary>Gets or sets per game limit. 0 unlimited; 1..10 finite limit</summary>
    /// <remarks>Client label: wiredfurni.params.settimesingame. None</remarks>
    public int PerGameLimit
    {
        get => GetField("per_game_limit").Integer.GetValueOrDefault();
        set => SetField("per_game_limit", new(Integer: value));
    }
}

/// <summary>Typed settings for Chat (action code 7).</summary>
public sealed class WiredChatActionForm : WiredForm
{
    internal WiredChatActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets message. String, UI maximum 200 characters and 8 lines</summary>
    /// <remarks>Client label: wiredfurni.params.message. None</remarks>
    public string Message
    {
        get => GetField("message").Text!;
        set => SetField("message", new(Text: value));
    }

    /// <summary>Gets or sets visibility. 0 only the user, 1 everyone</summary>
    /// <remarks>Client label: wiredfurni.params.show_message.visibility_selection.title. None</remarks>
    public WiredVisibilityOption Visibility
    {
        get => (WiredVisibilityOption)GetField("visibility").Integer.GetValueOrDefault();
        set => SetField("visibility", new(Integer: (int)value));
    }

    /// <summary>Gets or sets style. 34, 200, 201, 202, 210, 211, 212, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 250, 251, 252</summary>
    /// <remarks>Client label: wiredfurni.params.show_message.style_selection.title. None</remarks>
    public int Style
    {
        get => GetField("style").Integer.GetValueOrDefault();
        set => SetField("style", new(Integer: value));
    }

    /// <summary>Gets or sets bubble width. -1 user&#x27;s preference, 0 wide, 1 normal, 2 thin</summary>
    /// <remarks>Client label: wiredfurni.params.show_message.bubble_width.title. None</remarks>
    public WiredBubbleWidthOption BubbleWidth
    {
        get => (WiredBubbleWidthOption)GetField("bubble_width").Integer.GetValueOrDefault();
        set => SetField("bubble_width", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for TeleportToFurniture (action code 8).</summary>
public sealed class WiredTeleportToFurnitureActionForm : WiredForm
{
    internal WiredTeleportToFurnitureActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets fast teleport. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.teleport.options.0. None</remarks>
    public bool FastTeleport
    {
        get => GetField("fast_teleport").Boolean.GetValueOrDefault();
        set => SetField("fast_teleport", new(Boolean: value));
    }
}

/// <summary>Typed settings for JoinTeam (action code 9).</summary>
public sealed class WiredJoinTeamActionForm : WiredForm
{
    internal WiredJoinTeamActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets team. 1 red, 2 green, 3 blue, 4 yellow</summary>
    /// <remarks>Client label: wiredfurni.params.team. None</remarks>
    public WiredJoinTeamActionTeamOption Team
    {
        get => (WiredJoinTeamActionTeamOption)GetField("team").Integer.GetValueOrDefault();
        set => SetField("team", new(Integer: (int)value));
    }

    /// <summary>Gets or sets game type. 0 Wired, 1 Battle Banzai, 2 Freeze</summary>
    /// <remarks>Client label: wiredfurni.params.choose_type / wiredfurni.params.team_type.N. None</remarks>
    public WiredGameTypeOption GameType
    {
        get => (WiredGameTypeOption)GetField("game_type").Integer.GetValueOrDefault();
        set => SetField("game_type", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for LeaveTeam (action code 10).</summary>
public sealed class WiredLeaveTeamActionForm : WiredForm
{
    internal WiredLeaveTeamActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for Chase (action code 11).</summary>
public sealed class WiredChaseActionForm : WiredForm
{
    internal WiredChaseActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for Flee (action code 12).</summary>
public sealed class WiredFleeActionForm : WiredForm
{
    internal WiredFleeActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for MoveToDirection (action code 13).</summary>
public sealed class WiredMoveToDirectionActionForm : WiredForm
{
    internal WiredMoveToDirectionActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets direction. 0 upper-right, 1 right, 2 lower-right, 3 down, 4 lower-left, 5 left, 6 upper-left, 7 up (screen directions from embedded move_0..7 icons)</summary>
    /// <remarks>Client label: wiredfurni.params.startdir. None</remarks>
    public WiredDirectionOption Direction
    {
        get => (WiredDirectionOption)GetField("direction").Integer.GetValueOrDefault();
        set => SetField("direction", new(Integer: (int)value));
    }

    /// <summary>Gets or sets blocked turn. 0..6; labels wiredfurni.params.turn.N</summary>
    /// <remarks>Client label: wiredfurni.params.turn. None</remarks>
    public int BlockedTurn
    {
        get => GetField("blocked_turn").Integer.GetValueOrDefault();
        set => SetField("blocked_turn", new(Integer: value));
    }

    /// <summary>Gets or sets block on user. 0=false, 1=true; any nonzero incoming value selects the checkbox</summary>
    /// <remarks>Client label: wiredfurni.params.user_collide.0. None</remarks>
    public bool BlockOnUser
    {
        get => GetField("block_on_user").Boolean.GetValueOrDefault();
        set => SetField("block_on_user", new(Boolean: value));
    }
}

/// <summary>Typed settings for GiveScoreToPredefinedTeam (action code 14).</summary>
public sealed class WiredGiveScoreToPredefinedTeamActionForm : WiredForm
{
    internal WiredGiveScoreToPredefinedTeamActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets points. Signed points; magnitude slider 1..1000. Negative means remove points</summary>
    /// <remarks>Client label: wiredfurni.params.setpoints2 / wiredfurni.params.points_operation. None</remarks>
    public int Points
    {
        get => GetField("points").Integer.GetValueOrDefault();
        set => SetField("points", new(Integer: value));
    }

    /// <summary>Gets or sets per game limit. 0 unlimited; 1..10 finite limit</summary>
    /// <remarks>Client label: wiredfurni.params.settimesingame. None</remarks>
    public int PerGameLimit
    {
        get => GetField("per_game_limit").Integer.GetValueOrDefault();
        set => SetField("per_game_limit", new(Integer: value));
    }

    /// <summary>Gets or sets team. 1 red, 2 green, 3 blue, 4 yellow</summary>
    /// <remarks>Client label: wiredfurni.params.team. None</remarks>
    public WiredJoinTeamActionTeamOption Team
    {
        get => (WiredJoinTeamActionTeamOption)GetField("team").Integer.GetValueOrDefault();
        set => SetField("team", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for ToggleToRandomState (action code 15).</summary>
public sealed class WiredToggleToRandomStateActionForm : WiredForm
{
    internal WiredToggleToRandomStateActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for MoveFurniTo (action code 16).</summary>
public sealed class WiredMoveFurniToActionForm : WiredForm
{
    internal WiredMoveFurniToActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets movement. 0, 2, 4, 6; labels movefurni.N (2/4/6 are absent in dated hotel texts)</summary>
    /// <remarks>Client label: wiredfurni.params.movefurni. None</remarks>
    public int Movement
    {
        get => GetField("movement").Integer.GetValueOrDefault();
        set => SetField("movement", new(Integer: value));
    }

    /// <summary>Gets or sets spacing tiles. 1..5 integer tiles</summary>
    /// <remarks>Client label: wiredfurni.params.emptytiles. None</remarks>
    public int SpacingTiles
    {
        get => GetField("spacing_tiles").Integer.GetValueOrDefault();
        set => SetField("spacing_tiles", new(Integer: value));
    }
}

/// <summary>Typed settings for GiveReward (action code 17).</summary>
public sealed class WiredGiveRewardActionForm : WiredForm
{
    internal WiredGiveRewardActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets interval type. 0 once, 1 every N days, 2 every N hours, 3 every N minutes</summary>
    /// <remarks>Client label: Literal UI reward interval. None</remarks>
    public WiredIntervalTypeOption IntervalType
    {
        get => (WiredIntervalTypeOption)GetField("interval_type").Integer.GetValueOrDefault();
        set => SetField("interval_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets unique rewards. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: Literal UI unique prize. None</remarks>
    public bool UniqueRewards
    {
        get => GetField("unique_rewards").Boolean.GetValueOrDefault();
        set => SetField("unique_rewards", new(Boolean: value));
    }

    /// <summary>Gets or sets total limit. 0 disabled; 1..1000 when enabled</summary>
    /// <remarks>Client label: wiredfurni.params.prizelimit. None</remarks>
    public int TotalLimit
    {
        get => GetField("total_limit").Integer.GetValueOrDefault();
        set => SetField("total_limit", new(Integer: value));
    }

    /// <summary>Gets or sets interval amount. 1..9999 in UI; writer clamps lower bound to 1</summary>
    /// <remarks>Client label: Literal UI reward interval. Input disabled for interval_type=0 but still written</remarks>
    public int IntervalAmount
    {
        get => GetField("interval_amount").Integer.GetValueOrDefault();
        set => SetField("interval_amount", new(Integer: value));
    }

    /// <summary>Gets or sets rewards. Up to 20 semicolon-separated rows: type,code,probability. Type 0 badge, 1 product; code maximum 100 characters</summary>
    /// <remarks>Client label: Literal UI reward table. None</remarks>
    public IReadOnlyList<WiredRewardEntry> Rewards
    {
        get => GetField("rewards").Rewards!;
        set => SetField("rewards", new(Rewards: value));
    }
}

/// <summary>Typed settings for CallAnotherStack (action code 18).</summary>
public sealed class WiredCallAnotherStackActionForm : WiredForm
{
    internal WiredCallAnotherStackActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for KickFromRoom (action code 19).</summary>
public sealed class WiredKickFromRoomActionForm : WiredForm
{
    internal WiredKickFromRoomActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets message. String, maximum 100 characters</summary>
    /// <remarks>Client label: wiredfurni.params.message. None</remarks>
    public string Message
    {
        get => GetField("message").Text!;
        set => SetField("message", new(Text: value));
    }
}

/// <summary>Typed settings for MuteUser (action code 20).</summary>
public sealed class WiredMuteUserActionForm : WiredForm
{
    internal WiredMuteUserActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets message. String, maximum 100 characters</summary>
    /// <remarks>Client label: wiredfurni.params.message. None</remarks>
    public string Message
    {
        get => GetField("message").Text!;
        set => SetField("message", new(Text: value));
    }

    /// <summary>Gets or sets minutes. 0..10</summary>
    /// <remarks>Client label: wiredfurni.params.length.minutes. None</remarks>
    public int Minutes
    {
        get => GetField("minutes").Integer.GetValueOrDefault();
        set => SetField("minutes", new(Integer: value));
    }
}

/// <summary>Typed settings for BotTeleport (action code 21).</summary>
public sealed class WiredBotTeleportActionForm : WiredForm
{
    internal WiredBotTeleportActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets bot name. String, maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.bot.name. None</remarks>
    public string BotName
    {
        get => GetField("bot_name").Text!;
        set => SetField("bot_name", new(Text: value));
    }
}

/// <summary>Typed settings for BotMove (action code 22).</summary>
public sealed class WiredBotMoveActionForm : WiredForm
{
    internal WiredBotMoveActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets bot name. String, maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.bot.name. None</remarks>
    public string BotName
    {
        get => GetField("bot_name").Text!;
        set => SetField("bot_name", new(Text: value));
    }
}

/// <summary>Typed settings for BotTalk (action code 23).</summary>
public sealed class WiredBotTalkActionForm : WiredForm
{
    internal WiredBotTalkActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets bot name. Maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.bot.name. None</remarks>
    public string BotName
    {
        get => GetField("bot_name").Text!;
        set => SetField("bot_name", new(Text: value));
    }

    /// <summary>Gets or sets message. Maximum 200 characters and 8 lines</summary>
    /// <remarks>Client label: wiredfurni.params.message. None</remarks>
    public string Message
    {
        get => GetField("message").Text!;
        set => SetField("message", new(Text: value));
    }

    /// <summary>Gets or sets chat mode. 0 talk, 1 shout</summary>
    /// <remarks>Client label: wiredfurni.params.talk / wiredfurni.params.shout. None</remarks>
    public WiredChatModeOption ChatMode
    {
        get => (WiredChatModeOption)GetField("chat_mode").Integer.GetValueOrDefault();
        set => SetField("chat_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets bubble width. -1 user&#x27;s preference, 0 wide, 1 normal, 2 thin</summary>
    /// <remarks>Client label: wiredfurni.params.show_message.bubble_width.title. None</remarks>
    public WiredBubbleWidthOption BubbleWidth
    {
        get => (WiredBubbleWidthOption)GetField("bubble_width").Integer.GetValueOrDefault();
        set => SetField("bubble_width", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for BotGiveHandItem (action code 24).</summary>
public sealed class WiredBotGiveHandItemActionForm : WiredForm
{
    internal WiredBotGiveHandItemActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets optional bot name. Empty disables bot usage; otherwise maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.bot.name / wiredfurni.params.bot.usage. None</remarks>
    public string OptionalBotName
    {
        get => GetField("optional_bot_name").Text!;
        set => SetField("optional_bot_name", new(Text: value));
    }

    /// <summary>Gets or sets hand item. Initial choices 0,2,5,7,8,9,10,27,1126,1127,1128. Received/captured nonnegative IDs are added dynamically; selected -1 writes 0</summary>
    /// <remarks>Client label: wiredfurni.params.handitem. None</remarks>
    public int HandItem
    {
        get => GetField("hand_item").Integer.GetValueOrDefault();
        set => SetField("hand_item", new(Integer: value));
    }
}

/// <summary>Typed settings for BotFollowAvatar (action code 25).</summary>
public sealed class WiredBotFollowAvatarActionForm : WiredForm
{
    internal WiredBotFollowAvatarActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets bot name. Maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.bot.name. None</remarks>
    public string BotName
    {
        get => GetField("bot_name").Text!;
        set => SetField("bot_name", new(Text: value));
    }

    /// <summary>Gets or sets following. 0 stop, 1 start</summary>
    /// <remarks>Client label: wiredfurni.params.start.following / wiredfurni.params.stop.following. None</remarks>
    public WiredFollowingOption Following
    {
        get => (WiredFollowingOption)GetField("following").Integer.GetValueOrDefault();
        set => SetField("following", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for BotChangeFigure (action code 26).</summary>
public sealed class WiredBotChangeFigureActionForm : WiredForm
{
    internal WiredBotChangeFigureActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets bot name. Maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.bot.name. None</remarks>
    public string BotName
    {
        get => GetField("bot_name").Text!;
        set => SetField("bot_name", new(Text: value));
    }

    /// <summary>Gets or sets figure. Figure string copied from local sessionDataManager.figure</summary>
    /// <remarks>Client label: wiredfurni.params.capture.figure. None</remarks>
    public string Figure
    {
        get => GetField("figure").Text!;
        set => SetField("figure", new(Text: value));
    }
}

/// <summary>Typed settings for BotTalkDirectToAvtr (action code 27).</summary>
public sealed class WiredBotTalkDirectToAvtrActionForm : WiredForm
{
    internal WiredBotTalkDirectToAvtrActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets bot name. Maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.bot.name. None</remarks>
    public string BotName
    {
        get => GetField("bot_name").Text!;
        set => SetField("bot_name", new(Text: value));
    }

    /// <summary>Gets or sets message. Maximum 200 characters and 8 lines</summary>
    /// <remarks>Client label: wiredfurni.params.message. None</remarks>
    public string Message
    {
        get => GetField("message").Text!;
        set => SetField("message", new(Text: value));
    }

    /// <summary>Gets or sets chat mode. 0 talk, 1 whisper</summary>
    /// <remarks>Client label: wiredfurni.params.talk / wiredfurni.params.whisper. None</remarks>
    public WiredBotTalkDirectToAvtrActionChatModeOption ChatMode
    {
        get => (WiredBotTalkDirectToAvtrActionChatModeOption)GetField("chat_mode").Integer.GetValueOrDefault();
        set => SetField("chat_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets bubble width. -1 user&#x27;s preference, 0 wide, 1 normal, 2 thin</summary>
    /// <remarks>Client label: wiredfurni.params.show_message.bubble_width.title. None</remarks>
    public WiredBubbleWidthOption BubbleWidth
    {
        get => (WiredBubbleWidthOption)GetField("bubble_width").Integer.GetValueOrDefault();
        set => SetField("bubble_width", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for ControlClock (action code 28).</summary>
public sealed class WiredControlClockActionForm : WiredForm
{
    internal WiredControlClockActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets clock control. 0 start, 1 stop, 2 reset, 3 pause, 4 resume</summary>
    /// <remarks>Client label: wiredfurni.params.clock_control. None</remarks>
    public WiredClockControlOption ClockControl
    {
        get => (WiredClockControlOption)GetField("clock_control").Integer.GetValueOrDefault();
        set => SetField("clock_control", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for SetFurniAltitude (action code 29).</summary>
public sealed class WiredSetFurniAltitudeActionForm : WiredForm
{
    internal WiredSetFurniAltitudeActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets altitude hundredths. 0..8000, steps of 1; display divides by 100</summary>
    /// <remarks>Client label: wiredfurni.params.setaltitude. None</remarks>
    public int AltitudeHundredths
    {
        get => GetField("altitude_hundredths").Integer.GetValueOrDefault();
        set => SetField("altitude_hundredths", new(Integer: value));
    }

    /// <summary>Gets or sets operation. 0 increase, 1 decrease, 2 set value</summary>
    /// <remarks>Client label: wiredfurni.params.choose_type. None</remarks>
    public WiredOperationOption Operation
    {
        get => (WiredOperationOption)GetField("operation").Integer.GetValueOrDefault();
        set => SetField("operation", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for SendSignal (action code 30).</summary>
public sealed class WiredSendSignalActionForm : WiredForm
{
    internal WiredSendSignalActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets split furniture. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.signal.split_furni. None</remarks>
    public bool SplitFurniture
    {
        get => GetField("split_furniture").Boolean.GetValueOrDefault();
        set => SetField("split_furniture", new(Boolean: value));
    }

    /// <summary>Gets or sets split users. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.signal.split_users. None</remarks>
    public bool SplitUsers
    {
        get => GetField("split_users").Boolean.GetValueOrDefault();
        set => SetField("split_users", new(Boolean: value));
    }
}

/// <summary>Typed settings for FreezeUser (action code 31).</summary>
public sealed class WiredFreezeUserActionForm : WiredForm
{
    internal WiredFreezeUserActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets freeze effect. 0..4; labels wiredfurni.params.freeze.effect.N</summary>
    /// <remarks>Client label: wiredfurni.params.freeze.effect_selection. None</remarks>
    public int FreezeEffect
    {
        get => GetField("freeze_effect").Integer.GetValueOrDefault();
        set => SetField("freeze_effect", new(Integer: value));
    }

    /// <summary>Gets or sets cancel on teleport. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.freeze.cancel_on_teleport. None</remarks>
    public bool CancelOnTeleport
    {
        get => GetField("cancel_on_teleport").Boolean.GetValueOrDefault();
        set => SetField("cancel_on_teleport", new(Boolean: value));
    }
}

/// <summary>Typed settings for UnfreezeUser (action code 32).</summary>
public sealed class WiredUnfreezeUserActionForm : WiredForm
{
    internal WiredUnfreezeUserActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for RelativeFurniMove (action code 33).</summary>
public sealed class WiredRelativeFurniMoveActionForm : WiredForm
{
    internal WiredRelativeFurniMoveActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets x offset. -20..20 tiles</summary>
    /// <remarks>Client label: wiredfurni.params.movement.horizontal.selection. None</remarks>
    public int XOffset
    {
        get => GetField("x_offset").Integer.GetValueOrDefault();
        set => SetField("x_offset", new(Integer: value));
    }

    /// <summary>Gets or sets y offset. -20..20 tiles</summary>
    /// <remarks>Client label: wiredfurni.params.movement.vertical.selection. None</remarks>
    public int YOffset
    {
        get => GetField("y_offset").Integer.GetValueOrDefault();
        set => SetField("y_offset", new(Integer: value));
    }
}

/// <summary>Typed settings for MoveFurniToFurni (action code 34).</summary>
public sealed class WiredMoveFurniToFurniActionForm : WiredForm
{
    internal WiredMoveFurniToFurniActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for MoveFurniToUser (action code 35).</summary>
public sealed class WiredMoveFurniToUserActionForm : WiredForm
{
    internal WiredMoveFurniToUserActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for AdjustClock (action code 38).</summary>
public sealed class WiredAdjustClockActionForm : WiredForm
{
    internal WiredAdjustClockActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets seconds. 0..59</summary>
    /// <remarks>Client label: wiredfurni.params.clock_seconds. None</remarks>
    public int Seconds
    {
        get => GetField("seconds").Integer.GetValueOrDefault();
        set => SetField("seconds", new(Integer: value));
    }

    /// <summary>Gets or sets minutes. 0..99</summary>
    /// <remarks>Client label: wiredfurni.params.clock_minutes. None</remarks>
    public int Minutes
    {
        get => GetField("minutes").Integer.GetValueOrDefault();
        set => SetField("minutes", new(Integer: value));
    }

    /// <summary>Gets or sets half second. 0 or 1</summary>
    /// <remarks>Client label: wiredfurni.params.clock_seconds. None</remarks>
    public int HalfSecond
    {
        get => GetField("half_second").Integer.GetValueOrDefault();
        set => SetField("half_second", new(Integer: value));
    }

    /// <summary>Gets or sets operation. 0 increase, 1 decrease, 2 set value</summary>
    /// <remarks>Client label: wiredfurni.params.choose_type. None</remarks>
    public WiredOperationOption Operation
    {
        get => (WiredOperationOption)GetField("operation").Integer.GetValueOrDefault();
        set => SetField("operation", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for GiveVariable (action code 39).</summary>
public sealed class WiredGiveVariableActionForm : WiredForm
{
    internal WiredGiveVariableActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets target. 0 furniture, 1 users, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_destination. None</remarks>
    public WiredSourceDomain Target
    {
        get => (WiredSourceDomain)GetField("target").Integer.GetValueOrDefault();
        set => SetField("target", new(Integer: (int)value));
    }

    /// <summary>Gets initial value high. Sign extension: -1 for negative low word, otherwise 0; not read by onEditStart</summary>
    /// <remarks>Client label: wiredfurni.params.variables.value_settings.initial_value. None</remarks>
    public int InitialValueHigh
    {
        get => GetField("initial_value_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets initial value. Signed 32-bit integer</summary>
    /// <remarks>Client label: wiredfurni.params.variables.value_settings.initial_value. Open replaces value with 0 when selected variable cannot write values; section disabled when variable has no value</remarks>
    public int InitialValue
    {
        get => GetField("initial_value").Integer.GetValueOrDefault();
        set => SetField("initial_value", new(Integer: value));
    }

    /// <summary>Gets or sets override existing. 0=false, 1=true; open accepts any nonzero</summary>
    /// <remarks>Client label: wiredfurni.params.variables.value_settings.override_existing. None</remarks>
    public bool OverrideExisting
    {
        get => GetField("override_existing").Boolean.GetValueOrDefault();
        set => SetField("override_existing", new(Boolean: value));
    }

    /// <summary>Gets or sets variable. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; canCreateAndDelete required</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Variable
    {
        get => GetField("variable").Text!;
        set => SetField("variable", new(Text: value));
    }
}

/// <summary>Typed settings for RemoveVariable (action code 40).</summary>
public sealed class WiredRemoveVariableActionForm : WiredForm
{
    internal WiredRemoveVariableActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets target. 0 furniture, 1 users, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables. None</remarks>
    public WiredSourceDomain Target
    {
        get => (WiredSourceDomain)GetField("target").Integer.GetValueOrDefault();
        set => SetField("target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets variable. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; canCreateAndDelete required</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Variable
    {
        get => GetField("variable").Text!;
        set => SetField("variable", new(Text: value));
    }
}

/// <summary>Typed settings for ChangeVariable (action code 41).</summary>
public sealed class WiredChangeVariableActionForm : WiredForm
{
    internal WiredChangeVariableActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets destination target. 0 furniture, 1 users, -10 global, -20 context; only options offered by this form apply</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_destination. None</remarks>
    public WiredSourceDomain DestinationTarget
    {
        get => (WiredSourceDomain)GetField("destination_target").Integer.GetValueOrDefault();
        set => SetField("destination_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets operation. 0 assign, 1 add, 2 subtract, 3 multiply, 4 divide, 5 power, 6 modulo, 40 minimum, 41 maximum, 50 random upper bound, 60 absolute, 100 AND, 101 OR, 102 XOR, 103 NOT, 104 left shift, 105 right shift, 110 bit count; 111..122 also offered but absent from dated localization</summary>
    /// <remarks>Client label: wiredfurni.params.variables.operation. None</remarks>
    public WiredChangeVariableActionOperationOption Operation
    {
        get => (WiredChangeVariableActionOperationOption)GetField("operation").Integer.GetValueOrDefault();
        set => SetField("operation", new(Integer: (int)value));
    }

    /// <summary>Gets or sets operand kind. 0 constant, 1 variable; ValueOrVariableSection</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. Forced to 0 on save for operations 60, 103, 110</remarks>
    public WiredValueSource OperandKind
    {
        get => (WiredValueSource)GetField("operand_kind").Integer.GetValueOrDefault();
        set => SetField("operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets operand high. Sign extension of index 4; not read by open</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public int OperandHigh
    {
        get => GetField("operand_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets operand value. Signed 32-bit integer</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public int OperandValue
    {
        get => GetField("operand_value").Integer.GetValueOrDefault();
        set => SetField("operand_value", new(Integer: value));
    }

    /// <summary>Gets or sets operand target. 0 furniture, 1 users, -10 global, -20 context; only options offered by this form apply</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain OperandTarget
    {
        get => (WiredSourceDomain)GetField("operand_target").Integer.GetValueOrDefault();
        set => SetField("operand_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets destination. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; canWriteValue required</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Destination
    {
        get => GetField("destination").Text!;
        set => SetField("destination", new(Text: value));
    }

    /// <summary>Gets or sets operand. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Operand
    {
        get => GetField("operand").Text!;
        set => SetField("operand", new(Text: value));
    }

    /// <summary>Gets or sets the complete OperandReference control atomically.</summary>
    public WiredValueReference OperandReference
    {
        get => ReadReference("operand_kind", "operand_value", "operand_target", "operand");
        set => WriteReference("operand_kind", "operand_value", "operand_target", "operand", value);
    }
}

/// <summary>Typed settings for MoveUser (action code 42).</summary>
public sealed class WiredMoveUserActionForm : WiredForm
{
    internal WiredMoveUserActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets movement. -1 none; 0 upper-right, 1 right, 2 lower-right, 3 down, 4 lower-left, 5 left, 6 upper-left, 7 up (screen directions from embedded move_0..7 icons)</summary>
    /// <remarks>Client label: wiredfurni.params.moveuser. None</remarks>
    public WiredMoveUserActionMovementOption Movement
    {
        get => (WiredMoveUserActionMovementOption)GetField("movement").Integer.GetValueOrDefault();
        set => SetField("movement", new(Integer: (int)value));
    }

    /// <summary>Gets or sets rotation. -1 none; 0..7 use corresponding move icons; 9 clockwise, 10 counterclockwise</summary>
    /// <remarks>Client label: wiredfurni.params.rotateuser. None</remarks>
    public WiredMoveUserActionRotationOption Rotation
    {
        get => (WiredMoveUserActionRotationOption)GetField("rotation").Integer.GetValueOrDefault();
        set => SetField("rotation", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for MoveUserToFurni (action code 43).</summary>
public sealed class WiredMoveUserToFurniActionForm : WiredForm
{
    internal WiredMoveUserToFurniActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets walking mode. 0 keep walking if moved closer to target, 1 keep walking, 2 stop walking</summary>
    /// <remarks>Client label: wiredfurni.params.user_move.walkmode. None</remarks>
    public WiredWalkingModeOption WalkingMode
    {
        get => (WiredWalkingModeOption)GetField("walking_mode").Integer.GetValueOrDefault();
        set => SetField("walking_mode", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for TeleportToRoom (action code 44).</summary>
public sealed class WiredTeleportToRoomActionForm : WiredForm
{
    internal WiredTeleportToRoomActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for GiveCurrencyFromChest (action code 45).</summary>
public sealed class WiredGiveCurrencyFromChestActionForm : WiredForm
{
    internal WiredGiveCurrencyFromChestActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets rewarding mode. 0 specified amount, 1 all</summary>
    /// <remarks>Client label: wiredfurni.params.rewarding_mode. None</remarks>
    public WiredRewardingModeOption RewardingMode
    {
        get => (WiredRewardingModeOption)GetField("rewarding_mode").Integer.GetValueOrDefault();
        set => SetField("rewarding_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets amount. 1..2147483647</summary>
    /// <remarks>Client label: wiredfurni.params.amount_to_give. None</remarks>
    public int Amount
    {
        get => GetField("amount").Integer.GetValueOrDefault();
        set => SetField("amount", new(Integer: value));
    }

    /// <summary>Gets or sets amount kind. 0 constant, 1 variable; ValueOrVariableSection</summary>
    /// <remarks>Client label: wiredfurni.params.amount_to_give. None</remarks>
    public WiredValueSource AmountKind
    {
        get => (WiredValueSource)GetField("amount_kind").Integer.GetValueOrDefault();
        set => SetField("amount_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets amount target. 0 furniture, 1 users, -10 global, -20 context; only options offered by this form apply</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain AmountTarget
    {
        get => (WiredSourceDomain)GetField("amount_target").Integer.GetValueOrDefault();
        set => SetField("amount_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show popup. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.reward_contract.reward_popup.show_by_default. None</remarks>
    public bool ShowPopup
    {
        get => GetField("show_popup").Boolean.GetValueOrDefault();
        set => SetField("show_popup", new(Boolean: value));
    }

    /// <summary>Gets or sets amount variable. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.amount_to_give. None</remarks>
    public string AmountVariable
    {
        get => GetField("amount_variable").Text!;
        set => SetField("amount_variable", new(Text: value));
    }

    /// <summary>Gets or sets popup text. Maximum 200 characters and 3 lines</summary>
    /// <remarks>Client label: wiredfurni.reward_contract.reward_popup.text.tooltip. None</remarks>
    public string PopupText
    {
        get => GetField("popup_text").Text!;
        set => SetField("popup_text", new(Text: value));
    }

    /// <summary>Gets or sets earnings category. 11 games, 13 agency</summary>
    /// <remarks>Client label: wiredfurni.params.earnings_category. None</remarks>
    public Qx.Model.Messages.Incoming.EarningCategory EarningsCategory
    {
        get => (Qx.Model.Messages.Incoming.EarningCategory)GetField("earnings_category").Integer.GetValueOrDefault();
        set => SetField("earnings_category", new(Integer: (int)value));
    }

    /// <summary>Gets or sets the complete AmountReference control atomically.</summary>
    public WiredValueReference AmountReference
    {
        get => ReadReference("amount_kind", "amount", "amount_target", "amount_variable");
        set => WriteReference("amount_kind", "amount", "amount_target", "amount_variable", value);
    }
}

/// <summary>Typed settings for GiveFurniFromChest (action code 46).</summary>
public sealed class WiredGiveFurniFromChestActionForm : WiredForm
{
    internal WiredGiveFurniFromChestActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets rewarding mode. 0 specified amount, 1 all</summary>
    /// <remarks>Client label: wiredfurni.params.rewarding_mode. None</remarks>
    public WiredRewardingModeOption RewardingMode
    {
        get => (WiredRewardingModeOption)GetField("rewarding_mode").Integer.GetValueOrDefault();
        set => SetField("rewarding_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets amount. 1..2147483647</summary>
    /// <remarks>Client label: wiredfurni.params.amount_to_give. None</remarks>
    public int Amount
    {
        get => GetField("amount").Integer.GetValueOrDefault();
        set => SetField("amount", new(Integer: value));
    }

    /// <summary>Gets or sets amount kind. 0 constant, 1 variable; ValueOrVariableSection</summary>
    /// <remarks>Client label: wiredfurni.params.amount_to_give. None</remarks>
    public WiredValueSource AmountKind
    {
        get => (WiredValueSource)GetField("amount_kind").Integer.GetValueOrDefault();
        set => SetField("amount_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets amount target. 0 furniture, 1 users, -10 global, -20 context; only options offered by this form apply</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain AmountTarget
    {
        get => (WiredSourceDomain)GetField("amount_target").Integer.GetValueOrDefault();
        set => SetField("amount_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show popup. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.reward_contract.reward_popup.show_by_default. None</remarks>
    public bool ShowPopup
    {
        get => GetField("show_popup").Boolean.GetValueOrDefault();
        set => SetField("show_popup", new(Boolean: value));
    }

    /// <summary>Gets or sets amount variable. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.amount_to_give. None</remarks>
    public string AmountVariable
    {
        get => GetField("amount_variable").Text!;
        set => SetField("amount_variable", new(Text: value));
    }

    /// <summary>Gets or sets popup text. Maximum 200 characters and 3 lines</summary>
    /// <remarks>Client label: wiredfurni.reward_contract.reward_popup.text.tooltip. None</remarks>
    public string PopupText
    {
        get => GetField("popup_text").Text!;
        set => SetField("popup_text", new(Text: value));
    }

    /// <summary>Gets or sets iteration mode. 0 random, 1 first in first out, 2 last in first out</summary>
    /// <remarks>Client label: wiredfurni.params.chest_iteration_type. Disabled when rewarding_mode=1; still serialized</remarks>
    public WiredIterationModeOption IterationMode
    {
        get => (WiredIterationModeOption)GetField("iteration_mode").Integer.GetValueOrDefault();
        set => SetField("iteration_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets the complete AmountReference control atomically.</summary>
    public WiredValueReference AmountReference
    {
        get => ReadReference("amount_kind", "amount", "amount_target", "amount_variable");
        set => WriteReference("amount_kind", "amount", "amount_target", "amount_variable", value);
    }
}

/// <summary>Typed settings for InitiateTransaction (action code 47).</summary>
public sealed class WiredInitiateTransactionActionForm : WiredForm
{
    internal WiredInitiateTransactionActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets transaction mode. 0 normal, 1 multiplier, 2 auto-multiplier</summary>
    /// <remarks>Client label: wiredfurni.params.contract.mode. None</remarks>
    public WiredTransactionModeOption TransactionMode
    {
        get => (WiredTransactionModeOption)GetField("transaction_mode").Integer.GetValueOrDefault();
        set => SetField("transaction_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets multiplier. 1..500</summary>
    /// <remarks>Client label: wiredfurni.params.contract.multiplier_selection. Section disabled in normal mode; caption changes to auto-multiplier limit for mode 2</remarks>
    public int Multiplier
    {
        get => GetField("multiplier").Integer.GetValueOrDefault();
        set => SetField("multiplier", new(Integer: value));
    }

    /// <summary>Gets or sets multiplier kind. 0 constant, 1 variable; ValueOrVariableSection</summary>
    /// <remarks>Client label: wiredfurni.params.contract.multiplier_selection. None</remarks>
    public WiredValueSource MultiplierKind
    {
        get => (WiredValueSource)GetField("multiplier_kind").Integer.GetValueOrDefault();
        set => SetField("multiplier_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets multiplier target. 0 furniture, 1 users, -10 global, -20 context; only options offered by this form apply</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain MultiplierTarget
    {
        get => (WiredSourceDomain)GetField("multiplier_target").Integer.GetValueOrDefault();
        set => SetField("multiplier_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets timeout enabled. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.contract.timeout.desc. None</remarks>
    public bool TimeoutEnabled
    {
        get => GetField("timeout_enabled").Boolean.GetValueOrDefault();
        set => SetField("timeout_enabled", new(Boolean: value));
    }

    /// <summary>Gets or sets timeout seconds. 30..3600</summary>
    /// <remarks>Client label: wiredfurni.params.contract.timeout.selection. None</remarks>
    public int TimeoutSeconds
    {
        get => GetField("timeout_seconds").Integer.GetValueOrDefault();
        set => SetField("timeout_seconds", new(Integer: value));
    }

    /// <summary>Gets or sets multiplier variable. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.contract.multiplier_selection. None</remarks>
    public string MultiplierVariable
    {
        get => GetField("multiplier_variable").Text!;
        set => SetField("multiplier_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete MultiplierReference control atomically.</summary>
    public WiredValueReference MultiplierReference
    {
        get => ReadReference("multiplier_kind", "multiplier", "multiplier_target", "multiplier_variable");
        set => WriteReference("multiplier_kind", "multiplier", "multiplier_target", "multiplier_variable", value);
    }
}

/// <summary>Typed settings for CancelTransaction (action code 48).</summary>
public sealed class WiredCancelTransactionActionForm : WiredForm
{
    internal WiredCancelTransactionActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets match criteria. 0 specified contract, 1 any ongoing transaction</summary>
    /// <remarks>Client label: wiredfurni.params.cancel_transaction.match_criteria. None</remarks>
    public WiredMatchCriteriaOption MatchCriteria
    {
        get => (WiredMatchCriteriaOption)GetField("match_criteria").Integer.GetValueOrDefault();
        set => SetField("match_criteria", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for Log (action code 49).</summary>
public sealed class WiredLogActionForm : WiredForm
{
    internal WiredLogActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets log level. 0,1,2,3; corresponding numeric localization keys absent in dated hotel texts</summary>
    /// <remarks>Client label: wiredfurni.params.write_to_logs.log_level.title. None</remarks>
    public int LogLevel
    {
        get => GetField("log_level").Integer.GetValueOrDefault();
        set => SetField("log_level", new(Integer: value));
    }

    /// <summary>Gets or sets message. Maximum 400 characters</summary>
    /// <remarks>Client label: wiredfurni.params.write_to_logs.log_message.title. None</remarks>
    public string Message
    {
        get => GetField("message").Text!;
        set => SetField("message", new(Text: value));
    }
}

/// <summary>Typed settings for ProgressAchievement (action code 51).</summary>
public sealed class WiredProgressAchievementActionForm : WiredForm
{
    internal WiredProgressAchievementActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets progression mode. 0 set progress, 1 add to existing progress</summary>
    /// <remarks>Client label: wiredfurni.params.progress_achievement.mode. None</remarks>
    public WiredProgressionModeOption ProgressionMode
    {
        get => (WiredProgressionModeOption)GetField("progression_mode").Integer.GetValueOrDefault();
        set => SetField("progression_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets score kind. 0 constant, 1 variable; ValueOrVariableSection</summary>
    /// <remarks>Client label: wiredfurni.params.progress_achievement.score. None</remarks>
    public WiredValueSource ScoreKind
    {
        get => (WiredValueSource)GetField("score_kind").Integer.GetValueOrDefault();
        set => SetField("score_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets score. 0..2147483647</summary>
    /// <remarks>Client label: wiredfurni.params.progress_achievement.score. None</remarks>
    public int Score
    {
        get => GetField("score").Integer.GetValueOrDefault();
        set => SetField("score", new(Integer: value));
    }

    /// <summary>Gets or sets score target. 0 furniture, 1 users, -10 global, -20 context; only options offered by this form apply</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ScoreTarget
    {
        get => (WiredSourceDomain)GetField("score_target").Integer.GetValueOrDefault();
        set => SetField("score_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets score variable. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.progress_achievement.score. None</remarks>
    public string ScoreVariable
    {
        get => GetField("score_variable").Text!;
        set => SetField("score_variable", new(Text: value));
    }

    /// <summary>Gets or sets achievement name. Name selected from roomEvents.achievementsInRoom; unmatched received name selects -1</summary>
    /// <remarks>Client label: wiredfurni.params.progress_achievement.name. None</remarks>
    public string AchievementName
    {
        get => GetField("achievement_name").Text!;
        set => SetField("achievement_name", new(Text: value));
    }

    /// <summary>Gets or sets the complete ScoreReference control atomically.</summary>
    public WiredValueReference ScoreReference
    {
        get => ReadReference("score_kind", "score", "score_target", "score_variable");
        set => WriteReference("score_kind", "score", "score_target", "score_variable", value);
    }
}

/// <summary>Typed settings for GiveEffect (action code 52).</summary>
public sealed class WiredGiveEffectActionForm : WiredForm
{
    internal WiredGiveEffectActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets effect id. 0..10000</summary>
    /// <remarks>Client label: wiredfurni.params.give_effect.id. None</remarks>
    public int EffectId
    {
        get => GetField("effect_id").Integer.GetValueOrDefault();
        set => SetField("effect_id", new(Integer: value));
    }

    /// <summary>Gets or sets priority. 0..2</summary>
    /// <remarks>Client label: wiredfurni.params.give_effect.priority. None</remarks>
    public int Priority
    {
        get => GetField("priority").Integer.GetValueOrDefault();
        set => SetField("priority", new(Integer: value));
    }

    /// <summary>Gets or sets effect type. 0 or 1; open maps only exact 1 to option 1; corresponding localization keys absent in dated hotel texts</summary>
    /// <remarks>Client label: wiredfurni.params.give_effect.type. None</remarks>
    public int EffectType
    {
        get => GetField("effect_type").Integer.GetValueOrDefault();
        set => SetField("effect_type", new(Integer: value));
    }
}

/// <summary>Typed settings for OverrideHeight (action code 53).</summary>
public sealed class WiredOverrideHeightActionForm : WiredForm
{
    internal WiredOverrideHeightActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets height. 0..8000; converter echoes raw number, so no unit inferred</summary>
    /// <remarks>Client label: wiredfurni.params.override_height.height. Disabled for type=1 but still written</remarks>
    public int Height
    {
        get => GetField("height").Integer.GetValueOrDefault();
        set => SetField("height", new(Integer: value));
    }

    /// <summary>Gets or sets height type. 0 or 1; open maps only exact 1 to option 1; localization absent</summary>
    /// <remarks>Client label: wiredfurni.params.override_height.type. None</remarks>
    public int HeightType
    {
        get => GetField("height_type").Integer.GetValueOrDefault();
        set => SetField("height_type", new(Integer: value));
    }
}

/// <summary>Typed settings for ClickConfiguration (action code 54).</summary>
public sealed class WiredClickConfigurationActionForm : WiredForm
{
    internal WiredClickConfigurationActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets user click. 0 default, 1 click user but walk behind, 2 pass through user</summary>
    /// <remarks>Client label: wiredfurni.params.click_settings.user. None</remarks>
    public WiredUserClickOption UserClick
    {
        get => (WiredUserClickOption)GetField("user_click").Integer.GetValueOrDefault();
        set => SetField("user_click", new(Integer: (int)value));
    }

    /// <summary>Gets or sets furniture click. 0 default, 1 pass through furniture</summary>
    /// <remarks>Client label: wiredfurni.params.click_settings.furni. None</remarks>
    public WiredFurnitureClickOption FurnitureClick
    {
        get => (WiredFurnitureClickOption)GetField("furniture_click").Integer.GetValueOrDefault();
        set => SetField("furniture_click", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for PlaceFurni (action code 55).</summary>
public sealed class WiredPlaceFurniActionForm : WiredForm
{
    internal WiredPlaceFurniActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets custom target is user. 0 furniture, 1 user; exact-1 Boolean on open</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.custom_target. None</remarks>
    public bool CustomTargetIsUser
    {
        get => GetField("custom_target_is_user").Boolean.GetValueOrDefault();
        set => SetField("custom_target_is_user", new(Boolean: value));
    }

    /// <summary>Gets or sets location mode. 0 source location, 1 custom location</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.target_location. None</remarks>
    public WiredLocationModeOption LocationMode
    {
        get => (WiredLocationModeOption)GetField("location_mode").Integer.GetValueOrDefault();
        set => SetField("location_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets altitude mode. 0 on top of target location, 1 source altitude, 2 custom altitude</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.target_altitude. None</remarks>
    public WiredAltitudeModeOption AltitudeMode
    {
        get => (WiredAltitudeModeOption)GetField("altitude_mode").Integer.GetValueOrDefault();
        set => SetField("altitude_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets x offset. -64..64</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.offsets.x. None</remarks>
    public int XOffset
    {
        get => GetField("x_offset").Integer.GetValueOrDefault();
        set => SetField("x_offset", new(Integer: value));
    }

    /// <summary>Gets or sets y offset. -64..64</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.offsets.y. None</remarks>
    public int YOffset
    {
        get => GetField("y_offset").Integer.GetValueOrDefault();
        set => SetField("y_offset", new(Integer: value));
    }

    /// <summary>Gets or sets altitude offset. -8000..8000</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.offsets.altitude. None</remarks>
    public int AltitudeOffset
    {
        get => GetField("altitude_offset").Integer.GetValueOrDefault();
        set => SetField("altitude_offset", new(Integer: value));
    }

    /// <summary>Gets or sets spawn with variable. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.spawn_with_variable. None</remarks>
    public bool SpawnWithVariable
    {
        get => GetField("spawn_with_variable").Boolean.GetValueOrDefault();
        set => SetField("spawn_with_variable", new(Boolean: value));
    }

    /// <summary>Gets or sets spawn value kind. 0 constant, 1 variable; ValueOrVariableSection</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.spawn_with_value. None</remarks>
    public WiredValueSource SpawnValueKind
    {
        get => (WiredValueSource)GetField("spawn_value_kind").Integer.GetValueOrDefault();
        set => SetField("spawn_value_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets spawn value. Signed 32-bit integer</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.spawn_with_value. None</remarks>
    public int SpawnValue
    {
        get => GetField("spawn_value").Integer.GetValueOrDefault();
        set => SetField("spawn_value", new(Integer: value));
    }

    /// <summary>Gets or sets spawn value target. 0 furniture, 1 users, -10 global, -20 context; only options offered by this form apply</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain SpawnValueTarget
    {
        get => (WiredSourceDomain)GetField("spawn_value_target").Integer.GetValueOrDefault();
        set => SetField("spawn_value_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets spawn variable. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; furniture target, canCreateAndDelete, type 0 required</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.spawn_with_variable. None</remarks>
    public string SpawnVariable
    {
        get => GetField("spawn_variable").Text!;
        set => SetField("spawn_variable", new(Text: value));
    }

    /// <summary>Gets or sets spawn value variable. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.spawn_with_value. None</remarks>
    public string SpawnValueVariable
    {
        get => GetField("spawn_value_variable").Text!;
        set => SetField("spawn_value_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete SpawnValueReference control atomically.</summary>
    public WiredValueReference SpawnValueReference
    {
        get => ReadReference("spawn_value_kind", "spawn_value", "spawn_value_target", "spawn_value_variable");
        set => WriteReference("spawn_value_kind", "spawn_value", "spawn_value_target", "spawn_value_variable", value);
    }
}

/// <summary>Typed settings for RemoveFurni (action code 56).</summary>
public sealed class WiredRemoveFurniActionForm : WiredForm
{
    internal WiredRemoveFurniActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for MoveAsGroup (action code 57).</summary>
public sealed class WiredMoveAsGroupActionForm : WiredForm
{
    internal WiredMoveAsGroupActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets target is user. 0 furniture, 1 user; exact-1 Boolean on open</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.target_location. None</remarks>
    public bool TargetIsUser
    {
        get => GetField("target_is_user").Boolean.GetValueOrDefault();
        set => SetField("target_is_user", new(Boolean: value));
    }

    /// <summary>Gets or sets x offset. -64..64</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.offsets.x. None</remarks>
    public int XOffset
    {
        get => GetField("x_offset").Integer.GetValueOrDefault();
        set => SetField("x_offset", new(Integer: value));
    }

    /// <summary>Gets or sets y offset. -64..64</summary>
    /// <remarks>Client label: wiredfurni.params.place_furni.offsets.y. None</remarks>
    public int YOffset
    {
        get => GetField("y_offset").Integer.GetValueOrDefault();
        set => SetField("y_offset", new(Integer: value));
    }
}

/// <summary>Typed settings for ProgressRewardTrack (action code 58).</summary>
public sealed class WiredProgressRewardTrackActionForm : WiredForm
{
    internal WiredProgressRewardTrackActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets add to existing score. 0=false, 1=true; incoming getBoolean compares exactly with 1</summary>
    /// <remarks>Client label: wiredfurni.params.reward_track.add_to_existing_score. None</remarks>
    public bool AddToExistingScore
    {
        get => GetField("add_to_existing_score").Boolean.GetValueOrDefault();
        set => SetField("add_to_existing_score", new(Boolean: value));
    }

    /// <summary>Gets or sets score kind. 0 constant, 1 variable; ValueOrVariableSection</summary>
    /// <remarks>Client label: wiredfurni.params.reward_track.score. None</remarks>
    public WiredValueSource ScoreKind
    {
        get => (WiredValueSource)GetField("score_kind").Integer.GetValueOrDefault();
        set => SetField("score_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets score. 1..2147483647</summary>
    /// <remarks>Client label: wiredfurni.params.reward_track.score. None</remarks>
    public int Score
    {
        get => GetField("score").Integer.GetValueOrDefault();
        set => SetField("score", new(Integer: value));
    }

    /// <summary>Gets or sets score target. 0 furniture, 1 users, -10 global, -20 context; only options offered by this form apply</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ScoreTarget
    {
        get => (WiredSourceDomain)GetField("score_target").Integer.GetValueOrDefault();
        set => SetField("score_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets score variable. Selected variable ID; &#x27;n&#x27; is the no-variable sentinel; hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.reward_track.score. None</remarks>
    public string ScoreVariable
    {
        get => GetField("score_variable").Text!;
        set => SetField("score_variable", new(Text: value));
    }

    /// <summary>Gets or sets track id. Maximum 100 characters; input restricted to a-zA-Z0-9_</summary>
    /// <remarks>Client label: wiredfurni.params.reward_track.track_id. None</remarks>
    public string TrackId
    {
        get => GetField("track_id").Text!;
        set => SetField("track_id", new(Text: value));
    }

    /// <summary>Gets or sets task id. Maximum 100 characters; input restricted to a-zA-Z0-9_</summary>
    /// <remarks>Client label: wiredfurni.params.reward_track.task_id. None</remarks>
    public string TaskId
    {
        get => GetField("task_id").Text!;
        set => SetField("task_id", new(Text: value));
    }

    /// <summary>Gets or sets the complete ScoreReference control atomically.</summary>
    public WiredValueReference ScoreReference
    {
        get => ReadReference("score_kind", "score", "score_target", "score_variable");
        set => WriteReference("score_kind", "score", "score_target", "score_variable", value);
    }
}

/// <summary>Typed settings for ResetRewardTrack (action code 59).</summary>
public sealed class WiredResetRewardTrackActionForm : WiredForm
{
    internal WiredResetRewardTrackActionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets track id. Maximum 100 characters; input excludes tab</summary>
    /// <remarks>Client label: wiredfurni.params.reward_track.track_id. None</remarks>
    public string TrackId
    {
        get => GetField("track_id").Text!;
        set => SetField("track_id", new(Text: value));
    }
}

/// <summary>Typed settings for TriggererIsOnFurni (condition code 2).</summary>
public sealed class WiredTriggererIsOnFurniConditionForm : WiredForm
{
    internal WiredTriggererIsOnFurniConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for FurnisHaveAvatars (condition code 1).</summary>
public sealed class WiredFurnisHaveAvatarsConditionForm : WiredForm
{
    internal WiredFurnisHaveAvatarsConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets require all. 0 any selected furniture has an avatar (requireall.2), 1 all selected furniture have avatars (requireall.3)</summary>
    /// <remarks>Client label: wiredfurni.params.requireall. None</remarks>
    public WiredRequireAllOption RequireAll
    {
        get => (WiredRequireAllOption)GetField("require_all").Integer.GetValueOrDefault();
        set => SetField("require_all", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for NotFurnisHaveAvatars (condition code 14).</summary>
public sealed class WiredNotFurnisHaveAvatarsConditionForm : WiredForm
{
    internal WiredNotFurnisHaveAvatarsConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets require any without avatar. 1 any selected furniture has no avatar (not_requireall.2), 0 all selected furniture have no avatars (not_requireall.3)</summary>
    /// <remarks>Client label: wiredfurni.params.requireall. None</remarks>
    public WiredRequireAnyWithoutAvatarOption RequireAnyWithoutAvatar
    {
        get => (WiredRequireAnyWithoutAvatarOption)GetField("require_any_without_avatar").Integer.GetValueOrDefault();
        set => SetField("require_any_without_avatar", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for StatesMatch (condition code 0).</summary>
public sealed class WiredStatesMatchConditionForm : WiredForm
{
    internal WiredStatesMatchConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets match state. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.condition.state. None</remarks>
    public bool MatchState
    {
        get => GetField("match_state").Boolean.GetValueOrDefault();
        set => SetField("match_state", new(Boolean: value));
    }

    /// <summary>Gets or sets match direction. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.condition.direction. None</remarks>
    public bool MatchDirection
    {
        get => GetField("match_direction").Boolean.GetValueOrDefault();
        set => SetField("match_direction", new(Boolean: value));
    }

    /// <summary>Gets or sets match position. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.condition.position. None</remarks>
    public bool MatchPosition
    {
        get => GetField("match_position").Boolean.GetValueOrDefault();
        set => SetField("match_position", new(Boolean: value));
    }

    /// <summary>Gets or sets match altitude. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.condition.altitude. None</remarks>
    public bool MatchAltitude
    {
        get => GetField("match_altitude").Boolean.GetValueOrDefault();
        set => SetField("match_altitude", new(Boolean: value));
    }
}

/// <summary>Typed settings for TimeElapsedMore (condition code 3).</summary>
public sealed class WiredTimeElapsedMoreConditionForm : WiredForm
{
    internal WiredTimeElapsedMoreConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets time threshold. UI pulses + 1; UI pulses 1..1200, thus emitted value 2..1201</summary>
    /// <remarks>Client label: wiredfurni.params.allowafter2. None</remarks>
    public int TimeThreshold
    {
        get => GetField("time_threshold").Integer.GetValueOrDefault();
        set => SetField("time_threshold", new(Integer: value));
    }
}

/// <summary>Typed settings for TimeElapsedLess (condition code 4).</summary>
public sealed class WiredTimeElapsedLessConditionForm : WiredForm
{
    internal WiredTimeElapsedLessConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets time threshold. UI pulses + 1; UI pulses 1..1200, thus emitted value 2..1201</summary>
    /// <remarks>Client label: wiredfurni.params.allowbefore2. None</remarks>
    public int TimeThreshold
    {
        get => GetField("time_threshold").Integer.GetValueOrDefault();
        set => SetField("time_threshold", new(Integer: value));
    }
}

/// <summary>Typed settings for UserCountIn (condition code 5).</summary>
public sealed class WiredUserCountInConditionForm : WiredForm
{
    internal WiredUserCountInConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets minimum users. 0..125</summary>
    /// <remarks>Client label: wiredfurni.params.usercountmin. None</remarks>
    public int MinimumUsers
    {
        get => GetField("minimum_users").Integer.GetValueOrDefault();
        set => SetField("minimum_users", new(Integer: value));
    }

    /// <summary>Gets or sets maximum users. 0..125</summary>
    /// <remarks>Client label: wiredfurni.params.usercountmax. None</remarks>
    public int MaximumUsers
    {
        get => GetField("maximum_users").Integer.GetValueOrDefault();
        set => SetField("maximum_users", new(Integer: value));
    }
}

/// <summary>Typed settings for ActorIsInTeam (condition code 6).</summary>
public sealed class WiredActorIsInTeamConditionForm : WiredForm
{
    internal WiredActorIsInTeamConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets team. 0 any team, 1 red, 2 green, 3 blue, 4 yellow</summary>
    /// <remarks>Client label: wiredfurni.params.team. None</remarks>
    public WiredActorIsInTeamConditionTeamOption Team
    {
        get => (WiredActorIsInTeamConditionTeamOption)GetField("team").Integer.GetValueOrDefault();
        set => SetField("team", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for HasStackedFurnis (condition code 7).</summary>
public sealed class WiredHasStackedFurnisConditionForm : WiredForm
{
    internal WiredHasStackedFurnisConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets require all. 0 any selected furniture has furniture on it (requireall.0), 1 all selected furniture have furniture on them (requireall.1)</summary>
    /// <remarks>Client label: wiredfurni.params.requireall. None</remarks>
    public WiredHasStackedFurnisConditionRequireAllOption RequireAll
    {
        get => (WiredHasStackedFurnisConditionRequireAllOption)GetField("require_all").Integer.GetValueOrDefault();
        set => SetField("require_all", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for StuffTypeMatches (condition code 8).</summary>
public sealed class WiredStuffTypeMatchesConditionForm : WiredForm
{
    internal WiredStuffTypeMatchesConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for ActorIsGroupMember (condition code 10).</summary>
public sealed class WiredActorIsGroupMemberConditionForm : WiredForm
{
    internal WiredActorIsGroupMemberConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets group id. Empty = current group; otherwise decimal selected group ID</summary>
    /// <remarks>Client label: wiredfurni.params.groupselection. None</remarks>
    public string GroupId
    {
        get => GetField("group_id").Text!;
        set => SetField("group_id", new(Text: value));
    }
}

/// <summary>Typed settings for ActorIsWearingBadge (condition code 11).</summary>
public sealed class WiredActorIsWearingBadgeConditionForm : WiredForm
{
    internal WiredActorIsWearingBadgeConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets badge code. Text, maximum 1000 characters</summary>
    /// <remarks>Client label: wiredfurni.tooltip.badgecode. None</remarks>
    public string BadgeCode
    {
        get => GetField("badge_code").Text!;
        set => SetField("badge_code", new(Text: value));
    }
}

/// <summary>Typed settings for ActorIsWearingEffect (condition code 12).</summary>
public sealed class WiredActorIsWearingEffectConditionForm : WiredForm
{
    internal WiredActorIsWearingEffectConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets effect id. Signed 32-bit integer</summary>
    /// <remarks>Client label: wiredfurni.tooltip.effectid. None</remarks>
    public int EffectId
    {
        get => GetField("effect_id").Integer.GetValueOrDefault();
        set => SetField("effect_id", new(Integer: value));
    }
}

/// <summary>Typed settings for NotHasStackedFurnis (condition code 18).</summary>
public sealed class WiredNotHasStackedFurnisConditionForm : WiredForm
{
    internal WiredNotHasStackedFurnisConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets require all. 0 one or more selected furniture have no furniture on them (not_requireall.0), 1 all selected furniture have none (not_requireall.1)</summary>
    /// <remarks>Client label: wiredfurni.params.requireall. None</remarks>
    public WiredNotHasStackedFurnisConditionRequireAllOption RequireAll
    {
        get => (WiredNotHasStackedFurnisConditionRequireAllOption)GetField("require_all").Integer.GetValueOrDefault();
        set => SetField("require_all", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for DateRangeActive (condition code 24).</summary>
public sealed class WiredDateRangeActiveConditionForm : WiredForm
{
    internal WiredDateRangeActiveConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets start timestamp. Signed 32-bit seconds; optional if start cannot be parsed</summary>
    /// <remarks>Client label: wiredfurni.params.startdate. None</remarks>
    public int StartTimestamp
    {
        get => GetField("start_timestamp").Integer.GetValueOrDefault();
        set => SetField("start_timestamp", new(Integer: value));
    }

    /// <summary>Gets or sets end timestamp. Signed 32-bit seconds; optional if end cannot be parsed</summary>
    /// <remarks>Client label: wiredfurni.params.enddate. None</remarks>
    public int EndTimestamp
    {
        get => GetField("end_timestamp").Integer.GetValueOrDefault();
        set => SetField("end_timestamp", new(Integer: value));
    }
}

/// <summary>Typed settings for ActorHasHanditem (condition code 25).</summary>
public sealed class WiredActorHasHanditemConditionForm : WiredForm
{
    internal WiredActorHasHanditemConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets hand item. Initial choices 0,2,5,7,8,9,10,27; received/captured nonnegative IDs added dynamically; selected -1 writes 0</summary>
    /// <remarks>Client label: wiredfurni.params.handitem. None</remarks>
    public int HandItem
    {
        get => GetField("hand_item").Integer.GetValueOrDefault();
        set => SetField("hand_item", new(Integer: value));
    }
}

/// <summary>Typed settings for TriggererMatches (condition code 26).</summary>
public sealed class WiredTriggererMatchesConditionForm : WiredForm
{
    internal WiredTriggererMatchesConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets user type. 1 Habbo, 2 pet, 4 bot</summary>
    /// <remarks>Client label: wiredfurni.params.usertype. None</remarks>
    public WiredUserTypeOption UserType
    {
        get => (WiredUserTypeOption)GetField("user_type").Integer.GetValueOrDefault();
        set => SetField("user_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets name. Empty any user, otherwise specific name; maximum 32 characters</summary>
    /// <remarks>Client label: wiredfurni.params.picktriggerer. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }
}

/// <summary>Typed settings for TimeMatches (condition code 28).</summary>
public sealed class WiredTimeMatchesConditionForm : WiredForm
{
    internal WiredTimeMatchesConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets use second. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.time.second_selection. None</remarks>
    public bool UseSecond
    {
        get => GetField("use_second").Boolean.GetValueOrDefault();
        set => SetField("use_second", new(Boolean: value));
    }

    /// <summary>Gets or sets use minute. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.time.minute_selection. None</remarks>
    public bool UseMinute
    {
        get => GetField("use_minute").Boolean.GetValueOrDefault();
        set => SetField("use_minute", new(Boolean: value));
    }

    /// <summary>Gets or sets use hour. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.time.hour_selection. None</remarks>
    public bool UseHour
    {
        get => GetField("use_hour").Boolean.GetValueOrDefault();
        set => SetField("use_hour", new(Boolean: value));
    }

    /// <summary>Gets or sets second minimum. 0..59; disabled default 0</summary>
    /// <remarks>Client label: wiredfurni.params.time.second_selection. None</remarks>
    public int SecondMinimum
    {
        get => GetField("second_minimum").Integer.GetValueOrDefault();
        set => SetField("second_minimum", new(Integer: value));
    }

    /// <summary>Gets or sets second maximum. 0..59; disabled default 0</summary>
    /// <remarks>Client label: wiredfurni.params.time.second_selection. None</remarks>
    public int SecondMaximum
    {
        get => GetField("second_maximum").Integer.GetValueOrDefault();
        set => SetField("second_maximum", new(Integer: value));
    }

    /// <summary>Gets or sets minute minimum. 0..59; disabled default 0</summary>
    /// <remarks>Client label: wiredfurni.params.time.minute_selection. None</remarks>
    public int MinuteMinimum
    {
        get => GetField("minute_minimum").Integer.GetValueOrDefault();
        set => SetField("minute_minimum", new(Integer: value));
    }

    /// <summary>Gets or sets minute maximum. 0..59; disabled default 0</summary>
    /// <remarks>Client label: wiredfurni.params.time.minute_selection. None</remarks>
    public int MinuteMaximum
    {
        get => GetField("minute_maximum").Integer.GetValueOrDefault();
        set => SetField("minute_maximum", new(Integer: value));
    }

    /// <summary>Gets or sets hour minimum. 0..23; disabled default 0</summary>
    /// <remarks>Client label: wiredfurni.params.time.hour_selection. None</remarks>
    public int HourMinimum
    {
        get => GetField("hour_minimum").Integer.GetValueOrDefault();
        set => SetField("hour_minimum", new(Integer: value));
    }

    /// <summary>Gets or sets hour maximum. 0..23; disabled default 0</summary>
    /// <remarks>Client label: wiredfurni.params.time.hour_selection. None</remarks>
    public int HourMaximum
    {
        get => GetField("hour_maximum").Integer.GetValueOrDefault();
        set => SetField("hour_maximum", new(Integer: value));
    }

    /// <summary>Gets or sets timezone. Timezone string from wired.timezones, plus preferred received/cached timezone</summary>
    /// <remarks>Client label: wiredfurni.params.time.timezone_selection. None</remarks>
    public string Timezone
    {
        get => GetField("timezone").Text!;
        set => SetField("timezone", new(Text: value));
    }
}

/// <summary>Typed settings for DateMatches (condition code 29).</summary>
public sealed class WiredDateMatchesConditionForm : WiredForm
{
    internal WiredDateMatchesConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets use day. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.time.day_selection. None</remarks>
    public bool UseDay
    {
        get => GetField("use_day").Boolean.GetValueOrDefault();
        set => SetField("use_day", new(Boolean: value));
    }

    /// <summary>Gets or sets use year. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.time.year_selection. None</remarks>
    public bool UseYear
    {
        get => GetField("use_year").Boolean.GetValueOrDefault();
        set => SetField("use_year", new(Boolean: value));
    }

    /// <summary>Gets or sets weekday mask. Bits 0..6 Monday..Sunday; time.weekday.1..7</summary>
    /// <remarks>Client label: wiredfurni.params.time.weekday_selection. None</remarks>
    public WiredWeekdayMaskFlags WeekdayMask
    {
        get => (WiredWeekdayMaskFlags)GetField("weekday_mask").Integer.GetValueOrDefault();
        set => SetField("weekday_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets day minimum. 1..31; disabled default 1</summary>
    /// <remarks>Client label: wiredfurni.params.time.day_selection. None</remarks>
    public int DayMinimum
    {
        get => GetField("day_minimum").Integer.GetValueOrDefault();
        set => SetField("day_minimum", new(Integer: value));
    }

    /// <summary>Gets or sets day maximum. 1..31; disabled default 1</summary>
    /// <remarks>Client label: wiredfurni.params.time.day_selection. None</remarks>
    public int DayMaximum
    {
        get => GetField("day_maximum").Integer.GetValueOrDefault();
        set => SetField("day_maximum", new(Integer: value));
    }

    /// <summary>Gets or sets month mask. Bits 0..11 January..December; time.month.1..12</summary>
    /// <remarks>Client label: wiredfurni.params.time.month_selection. None</remarks>
    public WiredMonthMaskFlags MonthMask
    {
        get => (WiredMonthMaskFlags)GetField("month_mask").Integer.GetValueOrDefault();
        set => SetField("month_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets year minimum. 0..9999; disabled default 0</summary>
    /// <remarks>Client label: wiredfurni.params.time.year_selection. None</remarks>
    public int YearMinimum
    {
        get => GetField("year_minimum").Integer.GetValueOrDefault();
        set => SetField("year_minimum", new(Integer: value));
    }

    /// <summary>Gets or sets year maximum. 0..9999; disabled default 0</summary>
    /// <remarks>Client label: wiredfurni.params.time.year_selection. None</remarks>
    public int YearMaximum
    {
        get => GetField("year_maximum").Integer.GetValueOrDefault();
        set => SetField("year_maximum", new(Integer: value));
    }

    /// <summary>Gets or sets timezone. Timezone string from wired.timezones, plus preferred received/cached timezone</summary>
    /// <remarks>Client label: wiredfurni.params.time.timezone_selection. None</remarks>
    public string Timezone
    {
        get => GetField("timezone").Text!;
        set => SetField("timezone", new(Text: value));
    }
}

/// <summary>Typed settings for TeamIsWinning (condition code 31).</summary>
public sealed class WiredTeamIsWinningConditionForm : WiredForm
{
    internal WiredTeamIsWinningConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets team. 0 triggerer&#x27;s team (team.triggerer), 1 red, 2 green, 3 blue, 4 yellow</summary>
    /// <remarks>Client label: wiredfurni.params.team. None</remarks>
    public WiredTeamIsWinningConditionTeamOption Team
    {
        get => (WiredTeamIsWinningConditionTeamOption)GetField("team").Integer.GetValueOrDefault();
        set => SetField("team", new(Integer: (int)value));
    }

    /// <summary>Gets or sets placement. 0 first, 1 second, 2 third, 3 fourth; placement.1..4</summary>
    /// <remarks>Client label: wiredfurni.params.placement_selection. None</remarks>
    public WiredPlacementOption Placement
    {
        get => (WiredPlacementOption)GetField("placement").Integer.GetValueOrDefault();
        set => SetField("placement", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for PerformingAction (condition code 32).</summary>
public sealed class WiredPerformingActionConditionForm : WiredForm
{
    internal WiredPerformingActionConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets action. 0 wave, 1 blow, 2 laugh, 3 respect, 4 awake, 5 sleep, 6 sit, 7 stand, 8 lay, 10 sign, 11 dance, 67 client action named 67</summary>
    /// <remarks>Client label: wiredfurni.params.action_selection. None</remarks>
    public WiredActionOption Action
    {
        get => (WiredActionOption)GetField("action").Integer.GetValueOrDefault();
        set => SetField("action", new(Integer: (int)value));
    }

    /// <summary>Gets or sets action filter. Empty = no extra filter; sign 10 uses decimal 0..17; dance 11 uses &#x27;dance N&#x27; with N=1..4</summary>
    /// <remarks>Client label: wiredfurni.params.sign_filter / wiredfurni.params.dance_filter. Only actions 10 and 11 expose extra controls</remarks>
    public string ActionFilter
    {
        get => GetField("action_filter").Text!;
        set => SetField("action_filter", new(Text: value));
    }
}

/// <summary>Typed settings for TeamHasScore (condition code 34).</summary>
public sealed class WiredTeamHasScoreConditionForm : WiredForm
{
    internal WiredTeamHasScoreConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets team. 0 triggerer&#x27;s team (team.triggerer), 1 red, 2 green, 3 blue, 4 yellow</summary>
    /// <remarks>Client label: wiredfurni.params.team. None</remarks>
    public WiredTeamIsWinningConditionTeamOption Team
    {
        get => (WiredTeamIsWinningConditionTeamOption)GetField("team").Integer.GetValueOrDefault();
        set => SetField("team", new(Integer: (int)value));
    }

    /// <summary>Gets or sets score. 0..1000</summary>
    /// <remarks>Client label: wiredfurni.params.setscore2. None</remarks>
    public int Score
    {
        get => GetField("score").Integer.GetValueOrDefault();
        set => SetField("score", new(Integer: value));
    }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredComparisonOption Comparison
    {
        get => (WiredComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for ClockTimeMatches (condition code 35).</summary>
public sealed class WiredClockTimeMatchesConditionForm : WiredForm
{
    internal WiredClockTimeMatchesConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets seconds. 0..59</summary>
    /// <remarks>Client label: wiredfurni.params.clock_seconds_elapsed. None</remarks>
    public int Seconds
    {
        get => GetField("seconds").Integer.GetValueOrDefault();
        set => SetField("seconds", new(Integer: value));
    }

    /// <summary>Gets or sets minutes. 0..99</summary>
    /// <remarks>Client label: wiredfurni.params.clock_minutes_elapsed. None</remarks>
    public int Minutes
    {
        get => GetField("minutes").Integer.GetValueOrDefault();
        set => SetField("minutes", new(Integer: value));
    }

    /// <summary>Gets or sets half second. 0 or 1</summary>
    /// <remarks>Client label: wiredfurni.params.clock_seconds_elapsed. None</remarks>
    public int HalfSecond
    {
        get => GetField("half_second").Integer.GetValueOrDefault();
        set => SetField("half_second", new(Integer: value));
    }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredComparisonOption Comparison
    {
        get => (WiredComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for FurniHasAltitude (condition code 36).</summary>
public sealed class WiredFurniHasAltitudeConditionForm : WiredForm
{
    internal WiredFurniHasAltitudeConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets altitude hundredths. 0..8000; display divides by 100</summary>
    /// <remarks>Client label: wiredfurni.params.setaltitude. None</remarks>
    public int AltitudeHundredths
    {
        get => GetField("altitude_hundredths").Integer.GetValueOrDefault();
        set => SetField("altitude_hundredths", new(Integer: value));
    }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredComparisonOption Comparison
    {
        get => (WiredComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for UserDirection (condition code 37).</summary>
public sealed class WiredUserDirectionConditionForm : WiredForm
{
    internal WiredUserDirectionConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets direction mask. Bits 0..7 correspond to move_0..move_7 icons; other bits are dropped</summary>
    /// <remarks>Client label: wiredfurni.params.direction_selection. None</remarks>
    public WiredDirectionMaskFlags DirectionMask
    {
        get => (WiredDirectionMaskFlags)GetField("direction_mask").Integer.GetValueOrDefault();
        set => SetField("direction_mask", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for InputSourceQuantity (condition code 38).</summary>
public sealed class WiredInputSourceQuantityConditionForm : WiredForm
{
    internal WiredInputSourceQuantityConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets reference is user. 0 furniture, 1 user; incoming value must equal 1</summary>
    /// <remarks>Client label: Literal merged source selector. None</remarks>
    public bool ReferenceIsUser
    {
        get => GetField("reference_is_user").Boolean.GetValueOrDefault();
        set => SetField("reference_is_user", new(Boolean: value));
    }

    /// <summary>Gets or sets amount. 0..100</summary>
    /// <remarks>Client label: wiredfurni.params.setamount2. None</remarks>
    public int Amount
    {
        get => GetField("amount").Integer.GetValueOrDefault();
        set => SetField("amount", new(Integer: value));
    }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredComparisonOption Comparison
    {
        get => (WiredComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for CanPerformMove (condition code 39).</summary>
public sealed class WiredCanPerformMoveConditionForm : WiredForm
{
    internal WiredCanPerformMoveConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for HasVariable (condition code 40).</summary>
public sealed class WiredHasVariableConditionForm : WiredForm
{
    internal WiredHasVariableConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets target. 0 furniture, 1 user, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables. None</remarks>
    public WiredSourceDomain Target
    {
        get => (WiredSourceDomain)GetField("target").Integer.GetValueOrDefault();
        set => SetField("target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets variable. Variable must not be alwaysAvailable</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Variable
    {
        get => GetField("variable").Text!;
        set => SetField("variable", new(Text: value));
    }
}

/// <summary>Typed settings for VariableValue (condition code 42).</summary>
public sealed class WiredVariableValueConditionForm : WiredForm
{
    internal WiredVariableValueConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables. None</remarks>
    public WiredSourceDomain Target
    {
        get => (WiredSourceDomain)GetField("target").Integer.GetValueOrDefault();
        set => SetField("target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;, 3 &lt;=, 4 !=, 5 &gt;=; literal symbol labels</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredVariableValueConditionComparisonOption Comparison
    {
        get => (WiredVariableValueConditionComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }

    /// <summary>Gets or sets operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public WiredValueSource OperandKind
    {
        get => (WiredValueSource)GetField("operand_kind").Integer.GetValueOrDefault();
        set => SetField("operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets reference high. Sign extension of index 4; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public int ReferenceHigh
    {
        get => GetField("reference_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets reference value. Signed 32-bit integer</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public int ReferenceValue
    {
        get => GetField("reference_value").Integer.GetValueOrDefault();
        set => SetField("reference_value", new(Integer: value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets variable. hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Variable
    {
        get => GetField("variable").Text!;
        set => SetField("variable", new(Text: value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete OperandReference control atomically.</summary>
    public WiredValueReference OperandReference
    {
        get => ReadReference("operand_kind", "reference_value", "reference_target", "reference_variable");
        set => WriteReference("operand_kind", "reference_value", "reference_target", "reference_variable", value);
    }
}

/// <summary>Typed settings for VariableAge (condition code 43).</summary>
public sealed class WiredVariableAgeConditionForm : WiredForm
{
    internal WiredVariableAgeConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables. None</remarks>
    public WiredSourceDomain Target
    {
        get => (WiredSourceDomain)GetField("target").Integer.GetValueOrDefault();
        set => SetField("target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets comparison. 0 lower than, 2 higher than; no equals option</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredVariableAgeConditionComparisonOption Comparison
    {
        get => (WiredVariableAgeConditionComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }

    /// <summary>Gets or sets age kind. 0 creation time, 1 last update time</summary>
    /// <remarks>Client label: wiredfurni.params.variables.compare_value. None</remarks>
    public WiredAgeKindOption AgeKind
    {
        get => (WiredAgeKindOption)GetField("age_kind").Integer.GetValueOrDefault();
        set => SetField("age_kind", new(Integer: (int)value));
    }

    /// <summary>Gets duration high. Sign extension of index 4; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variables.duration. None</remarks>
    public int DurationHigh
    {
        get => GetField("duration_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets duration. Signed 32-bit integer</summary>
    /// <remarks>Client label: wiredfurni.params.variables.duration. None</remarks>
    public int Duration
    {
        get => GetField("duration").Integer.GetValueOrDefault();
        set => SetField("duration", new(Integer: value));
    }

    /// <summary>Gets or sets duration unit. 0 milliseconds, 1 seconds, 2 minutes, 3 hours, 4 days, 5 weeks, 6 months, 7 years</summary>
    /// <remarks>Client label: wiredfurni.params.variables.duration. None</remarks>
    public WiredDurationUnitOption DurationUnit
    {
        get => (WiredDurationUnitOption)GetField("duration_unit").Integer.GetValueOrDefault();
        set => SetField("duration_unit", new(Integer: (int)value));
    }

    /// <summary>Gets or sets variable. Requires canReadCreationTime OR canReadLastUpdateTime</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Variable
    {
        get => GetField("variable").Text!;
        set => SetField("variable", new(Text: value));
    }
}

/// <summary>Typed settings for UserLevel (condition code 44).</summary>
public sealed class WiredUserLevelConditionForm : WiredForm
{
    internal WiredUserLevelConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets level. 1..30</summary>
    /// <remarks>Client label: wiredfurni.params.level_selection. None</remarks>
    public int Level
    {
        get => GetField("level").Integer.GetValueOrDefault();
        set => SetField("level", new(Integer: value));
    }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredComparisonOption Comparison
    {
        get => (WiredComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for ChestHasItems (condition code 45).</summary>
public sealed class WiredChestHasItemsConditionForm : WiredForm
{
    internal WiredChestHasItemsConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets amount. 0..1000000</summary>
    /// <remarks>Client label: wiredfurni.params.chest_compare_amount. None</remarks>
    public int Amount
    {
        get => GetField("amount").Integer.GetValueOrDefault();
        set => SetField("amount", new(Integer: value));
    }

    /// <summary>Gets or sets operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.chest_compare_amount. None</remarks>
    public WiredValueSource OperandKind
    {
        get => (WiredValueSource)GetField("operand_kind").Integer.GetValueOrDefault();
        set => SetField("operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;, 3 &lt;=, 4 !=, 5 &gt;=; literal symbol labels</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredVariableValueConditionComparisonOption Comparison
    {
        get => (WiredVariableValueConditionComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.chest_compare_amount. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete AmountReference control atomically.</summary>
    public WiredValueReference AmountReference
    {
        get => ReadReference("operand_kind", "amount", "reference_target", "reference_variable");
        set => WriteReference("operand_kind", "amount", "reference_target", "reference_variable", value);
    }
}

/// <summary>Typed settings for ChestHasItemTypes (condition code 46).</summary>
public sealed class WiredChestHasItemTypesConditionForm : WiredForm
{
    internal WiredChestHasItemTypesConditionForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets amount. 0..1000000</summary>
    /// <remarks>Client label: wiredfurni.params.chest_compare_amount. None</remarks>
    public int Amount
    {
        get => GetField("amount").Integer.GetValueOrDefault();
        set => SetField("amount", new(Integer: value));
    }

    /// <summary>Gets or sets operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.chest_compare_amount. None</remarks>
    public WiredValueSource OperandKind
    {
        get => (WiredValueSource)GetField("operand_kind").Integer.GetValueOrDefault();
        set => SetField("operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;, 3 &lt;=, 4 !=, 5 &gt;=; literal symbol labels</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredVariableValueConditionComparisonOption Comparison
    {
        get => (WiredVariableValueConditionComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.chest_compare_amount. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete AmountReference control atomically.</summary>
    public WiredValueReference AmountReference
    {
        get => ReadReference("operand_kind", "amount", "reference_target", "reference_variable");
        set => WriteReference("operand_kind", "amount", "reference_target", "reference_variable", value);
    }
}

/// <summary>Typed settings for FurniByType (selector code 0).</summary>
public sealed class WiredFurniByTypeSelectorForm : WiredForm
{
    internal WiredFurniByTypeSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets match state. 0=false, 1=true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.state_match. None</remarks>
    public bool MatchState
    {
        get => GetField("match_state").Boolean.GetValueOrDefault();
        set => SetField("match_state", new(Boolean: value));
    }
}

/// <summary>Typed settings for PickedFurniture (selector code 1).</summary>
public sealed class WiredPickedFurnitureSelectorForm : WiredForm
{
    internal WiredPickedFurnitureSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for UsersByType (selector code 2).</summary>
public sealed class WiredUsersByTypeSelectorForm : WiredForm
{
    internal WiredUsersByTypeSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets user type. 1 Habbo, 2 pet, 4 bot</summary>
    /// <remarks>Client label: wiredfurni.params.usertype. None</remarks>
    public WiredUserTypeOption UserType
    {
        get => (WiredUserTypeOption)GetField("user_type").Integer.GetValueOrDefault();
        set => SetField("user_type", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for UsersInTeam (selector code 3).</summary>
public sealed class WiredUsersInTeamSelectorForm : WiredForm
{
    internal WiredUsersInTeamSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets team. 0 any team, 1 red, 2 green, 3 blue, 4 yellow</summary>
    /// <remarks>Client label: wiredfurni.params.team. None</remarks>
    public WiredActorIsInTeamConditionTeamOption Team
    {
        get => (WiredActorIsInTeamConditionTeamOption)GetField("team").Integer.GetValueOrDefault();
        set => SetField("team", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for FurniOnFurni (selector code 4).</summary>
public sealed class WiredFurniOnFurniSelectorForm : WiredForm
{
    internal WiredFurniOnFurniSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets relation. 0 above, 1 below, 2 same height, 3 all furniture on tile; labels onfurni.N</summary>
    /// <remarks>Client label: wiredfurni.params.selection_type. None</remarks>
    public WiredRelationOption Relation
    {
        get => (WiredRelationOption)GetField("relation").Integer.GetValueOrDefault();
        set => SetField("relation", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for FurniFromSignal (selector code 5).</summary>
public sealed class WiredFurniFromSignalSelectorForm : WiredForm
{
    internal WiredFurniFromSignalSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for FurnitureInNeighborhood (selector code 6).</summary>
public sealed class WiredFurnitureInNeighborhoodSelectorForm : WiredForm
{
    internal WiredFurnitureInNeighborhoodSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets reference is user. 0 furniture, 1 user; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.neighborhood. None</remarks>
    public bool ReferenceIsUser
    {
        get => GetField("reference_is_user").Boolean.GetValueOrDefault();
        set => SetField("reference_is_user", new(Boolean: value));
    }

    /// <summary>Gets or sets root x. -64..64</summary>
    /// <remarks>Client label: Literal x:. None</remarks>
    public int RootX
    {
        get => GetField("root_x").Integer.GetValueOrDefault();
        set => SetField("root_x", new(Integer: value));
    }

    /// <summary>Gets or sets root y. -64..64</summary>
    /// <remarks>Client label: Literal y:. None</remarks>
    public int RootY
    {
        get => GetField("root_y").Integer.GetValueOrDefault();
        set => SetField("root_y", new(Integer: value));
    }

    /// <summary>Gets or sets tile mask. 441 tiles in a 21x21 square; 14 signed 32-bit words, low bit first, spiral order</summary>
    /// <remarks>Client label: wiredfurni.params.neighborhood_selection. None</remarks>
    public IReadOnlyList<WiredTileOffset> TileMask
    {
        get => GetField("tile_mask").Tiles!;
        set => SetField("tile_mask", new(Tiles: value));
    }
}

/// <summary>Typed settings for FurnitureInArea (selector code 7).</summary>
public sealed class WiredFurnitureInAreaSelectorForm : WiredForm
{
    internal WiredFurnitureInAreaSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets x. Room-area selection X</summary>
    /// <remarks>Client label: wiredfurni.params.area_selection. None</remarks>
    public int X
    {
        get => GetField("x").Integer.GetValueOrDefault();
        set => SetField("x", new(Integer: value));
    }

    /// <summary>Gets or sets y. Room-area selection Y</summary>
    /// <remarks>Client label: wiredfurni.params.area_selection. None</remarks>
    public int Y
    {
        get => GetField("y").Integer.GetValueOrDefault();
        set => SetField("y", new(Integer: value));
    }

    /// <summary>Gets or sets width. Selected rectangle width</summary>
    /// <remarks>Client label: wiredfurni.params.area_selection. None</remarks>
    public int Width
    {
        get => GetField("width").Integer.GetValueOrDefault();
        set => SetField("width", new(Integer: value));
    }

    /// <summary>Gets or sets height. Selected rectangle height</summary>
    /// <remarks>Client label: wiredfurni.params.area_selection. None</remarks>
    public int Height
    {
        get => GetField("height").Integer.GetValueOrDefault();
        set => SetField("height", new(Integer: value));
    }
}

/// <summary>Typed settings for UsersOnFurni (selector code 8).</summary>
public sealed class WiredUsersOnFurniSelectorForm : WiredForm
{
    internal WiredUsersOnFurniSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for UsersPerformingAction (selector code 9).</summary>
public sealed class WiredUsersPerformingActionSelectorForm : WiredForm
{
    internal WiredUsersPerformingActionSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets action. 0 wave, 1 blow, 2 laugh, 3 respect, 4 awake, 5 sleep, 6 sit, 7 stand, 8 lay, 10 sign, 11 dance, 67 client action named 67</summary>
    /// <remarks>Client label: wiredfurni.params.action_selection. None</remarks>
    public WiredActionOption Action
    {
        get => (WiredActionOption)GetField("action").Integer.GetValueOrDefault();
        set => SetField("action", new(Integer: (int)value));
    }

    /// <summary>Gets or sets action filter. Empty = no extra filter; sign 10 uses decimal 0..17; dance 11 uses &#x27;dance N&#x27; with N=1..4</summary>
    /// <remarks>Client label: wiredfurni.params.sign_filter / wiredfurni.params.dance_filter. Only actions 10 and 11 expose extra controls</remarks>
    public string ActionFilter
    {
        get => GetField("action_filter").Text!;
        set => SetField("action_filter", new(Text: value));
    }
}

/// <summary>Typed settings for UsersFromSignal (selector code 10).</summary>
public sealed class WiredUsersFromSignalSelectorForm : WiredForm
{
    internal WiredUsersFromSignalSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for UsersByName (selector code 11).</summary>
public sealed class WiredUsersByNameSelectorForm : WiredForm
{
    internal WiredUsersByNameSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets usernames. Tab-separated names; UI maximum 1000 characters and 20 lines</summary>
    /// <remarks>Client label: wiredfurni.params.enter_names. None</remarks>
    public string Usernames
    {
        get => GetField("usernames").Text!;
        set => SetField("usernames", new(Text: value));
    }
}

/// <summary>Typed settings for UsersInNeighborhood (selector code 12).</summary>
public sealed class WiredUsersInNeighborhoodSelectorForm : WiredForm
{
    internal WiredUsersInNeighborhoodSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets reference is user. 0 furniture, 1 user; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.neighborhood. None</remarks>
    public bool ReferenceIsUser
    {
        get => GetField("reference_is_user").Boolean.GetValueOrDefault();
        set => SetField("reference_is_user", new(Boolean: value));
    }

    /// <summary>Gets or sets root x. -64..64</summary>
    /// <remarks>Client label: Literal x:. None</remarks>
    public int RootX
    {
        get => GetField("root_x").Integer.GetValueOrDefault();
        set => SetField("root_x", new(Integer: value));
    }

    /// <summary>Gets or sets root y. -64..64</summary>
    /// <remarks>Client label: Literal y:. None</remarks>
    public int RootY
    {
        get => GetField("root_y").Integer.GetValueOrDefault();
        set => SetField("root_y", new(Integer: value));
    }

    /// <summary>Gets or sets tile mask. 441 tiles in a 21x21 square; 14 signed 32-bit words, low bit first, spiral order</summary>
    /// <remarks>Client label: wiredfurni.params.neighborhood_selection. None</remarks>
    public IReadOnlyList<WiredTileOffset> TileMask
    {
        get => GetField("tile_mask").Tiles!;
        set => SetField("tile_mask", new(Tiles: value));
    }
}

/// <summary>Typed settings for UsersInArea (selector code 13).</summary>
public sealed class WiredUsersInAreaSelectorForm : WiredForm
{
    internal WiredUsersInAreaSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets x. Room-area selection X</summary>
    /// <remarks>Client label: wiredfurni.params.area_selection. None</remarks>
    public int X
    {
        get => GetField("x").Integer.GetValueOrDefault();
        set => SetField("x", new(Integer: value));
    }

    /// <summary>Gets or sets y. Room-area selection Y</summary>
    /// <remarks>Client label: wiredfurni.params.area_selection. None</remarks>
    public int Y
    {
        get => GetField("y").Integer.GetValueOrDefault();
        set => SetField("y", new(Integer: value));
    }

    /// <summary>Gets or sets width. Selected rectangle width</summary>
    /// <remarks>Client label: wiredfurni.params.area_selection. None</remarks>
    public int Width
    {
        get => GetField("width").Integer.GetValueOrDefault();
        set => SetField("width", new(Integer: value));
    }

    /// <summary>Gets or sets height. Selected rectangle height</summary>
    /// <remarks>Client label: wiredfurni.params.area_selection. None</remarks>
    public int Height
    {
        get => GetField("height").Integer.GetValueOrDefault();
        set => SetField("height", new(Integer: value));
    }
}

/// <summary>Typed settings for UsersWithHanditem (selector code 14).</summary>
public sealed class WiredUsersWithHanditemSelectorForm : WiredForm
{
    internal WiredUsersWithHanditemSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets hand item. Initial choices 0,2,5,7,8,9,10,27; received/captured nonnegative IDs added dynamically; selected -1 writes 0</summary>
    /// <remarks>Client label: wiredfurni.params.handitem. None</remarks>
    public int HandItem
    {
        get => GetField("hand_item").Integer.GetValueOrDefault();
        set => SetField("hand_item", new(Integer: value));
    }
}

/// <summary>Typed settings for UsersInGroup (selector code 15).</summary>
public sealed class WiredUsersInGroupSelectorForm : WiredForm
{
    internal WiredUsersInGroupSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets group id. Empty = current group; otherwise decimal selected group ID</summary>
    /// <remarks>Client label: wiredfurni.params.groupselection. None</remarks>
    public string GroupId
    {
        get => GetField("group_id").Text!;
        set => SetField("group_id", new(Text: value));
    }
}

/// <summary>Typed settings for FurniWithAltitude (selector code 16).</summary>
public sealed class WiredFurniWithAltitudeSelectorForm : WiredForm
{
    internal WiredFurniWithAltitudeSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets altitude hundredths. 0..8000; display divides by 100</summary>
    /// <remarks>Client label: wiredfurni.params.setaltitude. None</remarks>
    public int AltitudeHundredths
    {
        get => GetField("altitude_hundredths").Integer.GetValueOrDefault();
        set => SetField("altitude_hundredths", new(Integer: value));
    }

    /// <summary>Gets or sets comparison. 0 lower than, 1 equals, 2 higher than</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredFurniWithAltitudeSelectorComparisonOption Comparison
    {
        get => (WiredFurniWithAltitudeSelectorComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for FurnitureWithVariable (selector code 17).</summary>
public sealed class WiredFurnitureWithVariableSelectorForm : WiredForm
{
    internal WiredFurnitureWithVariableSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;, 3 &lt;=, 4 !=, 5 &gt;=; symbol labels are literal</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredVariableValueConditionComparisonOption Comparison
    {
        get => (WiredVariableValueConditionComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }

    /// <summary>Gets or sets value filter. 0 variable presence only, 1 constant comparison, 2 variable comparison</summary>
    /// <remarks>Client label: wiredfurni.params.variables.value_settings.select_by_value. None</remarks>
    public WiredValueFilterOption ValueFilter
    {
        get => (WiredValueFilterOption)GetField("value_filter").Integer.GetValueOrDefault();
        set => SetField("value_filter", new(Integer: (int)value));
    }

    /// <summary>Gets reference high. Sign extension of index 3; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public int ReferenceHigh
    {
        get => GetField("reference_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets reference value. Signed 32-bit integer</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public int ReferenceValue
    {
        get => GetField("reference_value").Integer.GetValueOrDefault();
        set => SetField("reference_value", new(Integer: value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets selected variable. Any variable with matching furniture target; no additional capability filter</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string SelectedVariable
    {
        get => GetField("selected_variable").Text!;
        set => SetField("selected_variable", new(Text: value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; &#x27;n&#x27; is no-variable sentinel</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }
}

/// <summary>Typed settings for UsersWithVariable (selector code 18).</summary>
public sealed class WiredUsersWithVariableSelectorForm : WiredForm
{
    internal WiredUsersWithVariableSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets comparison. 0 &lt;, 1 =, 2 &gt;, 3 &lt;=, 4 !=, 5 &gt;=; symbol labels are literal</summary>
    /// <remarks>Client label: wiredfurni.params.comparison_selection. None</remarks>
    public WiredVariableValueConditionComparisonOption Comparison
    {
        get => (WiredVariableValueConditionComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }

    /// <summary>Gets or sets value filter. 0 variable presence only, 1 constant comparison, 2 variable comparison</summary>
    /// <remarks>Client label: wiredfurni.params.variables.value_settings.select_by_value. None</remarks>
    public WiredValueFilterOption ValueFilter
    {
        get => (WiredValueFilterOption)GetField("value_filter").Integer.GetValueOrDefault();
        set => SetField("value_filter", new(Integer: (int)value));
    }

    /// <summary>Gets reference high. Sign extension of index 3; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public int ReferenceHigh
    {
        get => GetField("reference_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets reference value. Signed 32-bit integer</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public int ReferenceValue
    {
        get => GetField("reference_value").Integer.GetValueOrDefault();
        set => SetField("reference_value", new(Integer: value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets selected variable. Any variable with matching user target; no additional capability filter</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string SelectedVariable
    {
        get => GetField("selected_variable").Text!;
        set => SetField("selected_variable", new(Text: value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; &#x27;n&#x27; is no-variable sentinel</summary>
    /// <remarks>Client label: wiredfurni.params.variables.reference_value. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }
}

/// <summary>Typed settings for RemoteSelector (selector code 19).</summary>
public sealed class WiredRemoteSelectorForm : WiredForm
{
    internal WiredRemoteSelectorForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets set operation. 0 union, 1 intersection</summary>
    /// <remarks>Client label: wiredfurni.params.remote_selection.type. None</remarks>
    public WiredSetOperationOption SetOperation
    {
        get => (WiredSetOperationOption)GetField("set_operation").Integer.GetValueOrDefault();
        set => SetField("set_operation", new(Integer: (int)value));
    }

    /// <summary>Gets or sets random stack count. 0 all stacks, 1..2147483647 random stack count</summary>
    /// <remarks>Client label: wiredfurni.params.remote_selection.filter. None</remarks>
    public int RandomStackCount
    {
        get => GetField("random_stack_count").Integer.GetValueOrDefault();
        set => SetField("random_stack_count", new(Integer: value));
    }
}

/// <summary>Typed settings for ConditionEvaluation (addon code 0).</summary>
public sealed class WiredConditionEvaluationAddonForm : WiredForm
{
    internal WiredConditionEvaluationAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets mode. 0 all, 1 at least one, 2 not all, 3 none, -1 numeric comparison</summary>
    /// <remarks>Client label: wiredfurni.params.eval_mode. None</remarks>
    public WiredModeOption Mode
    {
        get => (WiredModeOption)GetField("mode").Integer.GetValueOrDefault();
        set => SetField("mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets comparison. 0 less than, 1 exactly, 2 more than; used only with mode -1</summary>
    /// <remarks>Client label: wiredfurni.params.eval_mode.cmp.0. None</remarks>
    public WiredConditionEvaluationAddonComparisonOption Comparison
    {
        get => (WiredConditionEvaluationAddonComparisonOption)GetField("comparison").Integer.GetValueOrDefault();
        set => SetField("comparison", new(Integer: (int)value));
    }

    /// <summary>Gets or sets amount. 0..1000; used only with mode -1</summary>
    /// <remarks>Client label: wiredfurni.params.eval_mode. None</remarks>
    public int Amount
    {
        get => GetField("amount").Integer.GetValueOrDefault();
        set => SetField("amount", new(Integer: value));
    }
}

/// <summary>Typed settings for Random (addon code 1).</summary>
public sealed class WiredRandomAddonForm : WiredForm
{
    internal WiredRandomAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets skip recent. 0..100 previous executions</summary>
    /// <remarks>Client label: wiredfurni.params.skipactions. None</remarks>
    public int SkipRecent
    {
        get => GetField("skip_recent").Integer.GetValueOrDefault();
        set => SetField("skip_recent", new(Integer: value));
    }

    /// <summary>Gets or sets pick count. 1..100 effects</summary>
    /// <remarks>Client label: wiredfurni.params.pickamount. None</remarks>
    public int PickCount
    {
        get => GetField("pick_count").Integer.GetValueOrDefault();
        set => SetField("pick_count", new(Integer: value));
    }
}

/// <summary>Typed settings for Unseen (addon code 2).</summary>
public sealed class WiredUnseenAddonForm : WiredForm
{
    internal WiredUnseenAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for ExecutionLimit (addon code 5).</summary>
public sealed class WiredExecutionLimitAddonForm : WiredForm
{
    internal WiredExecutionLimitAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets execution count. 1..100</summary>
    /// <remarks>Client label: wiredfurni.params.setexecutions. None</remarks>
    public int ExecutionCount
    {
        get => GetField("execution_count").Integer.GetValueOrDefault();
        set => SetField("execution_count", new(Integer: value));
    }

    /// <summary>Gets or sets window pulses. 1..20 half-second pulses; displayed seconds = value/2</summary>
    /// <remarks>Client label: wiredfurni.params.settimewindow. None</remarks>
    public int WindowPulses
    {
        get => GetField("window_pulses").Integer.GetValueOrDefault();
        set => SetField("window_pulses", new(Integer: value));
    }
}

/// <summary>Typed settings for NoMoveAnimation (addon code 6).</summary>
public sealed class WiredNoMoveAnimationAddonForm : WiredForm
{
    internal WiredNoMoveAnimationAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for MovementPhysics (addon code 7).</summary>
public sealed class WiredMovementPhysicsAddonForm : WiredForm
{
    internal WiredMovementPhysicsAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets keep altitude. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.movephysics.keep_altitude. None</remarks>
    public bool KeepAltitude
    {
        get => GetField("keep_altitude").Boolean.GetValueOrDefault();
        set => SetField("keep_altitude", new(Boolean: value));
    }

    /// <summary>Gets or sets move through furni. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.movephysics.move_through_furni. None</remarks>
    public bool MoveThroughFurni
    {
        get => GetField("move_through_furni").Boolean.GetValueOrDefault();
        set => SetField("move_through_furni", new(Boolean: value));
    }

    /// <summary>Gets or sets move through users. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.movephysics.move_through_users. None</remarks>
    public bool MoveThroughUsers
    {
        get => GetField("move_through_users").Boolean.GetValueOrDefault();
        set => SetField("move_through_users", new(Boolean: value));
    }

    /// <summary>Gets or sets block by furni. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.movephysics.block_by_furni. None</remarks>
    public bool BlockByFurni
    {
        get => GetField("block_by_furni").Boolean.GetValueOrDefault();
        set => SetField("block_by_furni", new(Boolean: value));
    }
}

/// <summary>Typed settings for CarryUsers (addon code 8).</summary>
public sealed class WiredCarryUsersAddonForm : WiredForm
{
    internal WiredCarryUsersAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets carry mode. 0 standing directly on moving furniture, 1 standing on same tile</summary>
    /// <remarks>Client label: wiredfurni.params.carry_mode. None</remarks>
    public WiredCarryModeOption CarryMode
    {
        get => (WiredCarryModeOption)GetField("carry_mode").Integer.GetValueOrDefault();
        set => SetField("carry_mode", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for AnimationTime (addon code 9).</summary>
public sealed class WiredAnimationTimeAddonForm : WiredForm
{
    internal WiredAnimationTimeAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets animation milliseconds. 50..2000, step 50</summary>
    /// <remarks>Client label: wiredfurni.params.setanimationtime2. None</remarks>
    public int AnimationMilliseconds
    {
        get => GetField("animation_milliseconds").Integer.GetValueOrDefault();
        set => SetField("animation_milliseconds", new(Integer: value));
    }
}

/// <summary>Typed settings for FurniSelectorFilter (addon code 10).</summary>
public sealed class WiredFurniSelectorFilterAddonForm : WiredForm
{
    internal WiredFurniSelectorFilterAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets filter amount. 1..1000</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public int FilterAmount
    {
        get => GetField("filter_amount").Integer.GetValueOrDefault();
        set => SetField("filter_amount", new(Integer: value));
    }

    /// <summary>Gets or sets operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public WiredValueSource OperandKind
    {
        get => (WiredValueSource)GetField("operand_kind").Integer.GetValueOrDefault();
        set => SetField("operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete AmountReference control atomically.</summary>
    public WiredValueReference AmountReference
    {
        get => ReadReference("operand_kind", "filter_amount", "reference_target", "reference_variable");
        set => WriteReference("operand_kind", "filter_amount", "reference_target", "reference_variable", value);
    }
}

/// <summary>Typed settings for UserSelectorFilter (addon code 11).</summary>
public sealed class WiredUserSelectorFilterAddonForm : WiredForm
{
    internal WiredUserSelectorFilterAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets filter amount. 1..1000</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public int FilterAmount
    {
        get => GetField("filter_amount").Integer.GetValueOrDefault();
        set => SetField("filter_amount", new(Integer: value));
    }

    /// <summary>Gets or sets operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public WiredValueSource OperandKind
    {
        get => (WiredValueSource)GetField("operand_kind").Integer.GetValueOrDefault();
        set => SetField("operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete AmountReference control atomically.</summary>
    public WiredValueReference AmountReference
    {
        get => ReadReference("operand_kind", "filter_amount", "reference_target", "reference_variable");
        set => WriteReference("operand_kind", "filter_amount", "reference_target", "reference_variable", value);
    }
}

/// <summary>Typed settings for FurniVariableFilter (addon code 12).</summary>
public sealed class WiredFurniVariableFilterAddonForm : WiredForm
{
    internal WiredFurniVariableFilterAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets filter amount. 1..1000</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public int FilterAmount
    {
        get => GetField("filter_amount").Integer.GetValueOrDefault();
        set => SetField("filter_amount", new(Integer: value));
    }

    /// <summary>Gets or sets sort order. 0 highest value, 1 lowest value, 2 oldest creation, 3 latest creation, 4 oldest update, 5 latest update; variables.sort_by.0..5</summary>
    /// <remarks>Client label: wiredfurni.params.variables.sort_by. None</remarks>
    public WiredSortOrderOption SortOrder
    {
        get => (WiredSortOrderOption)GetField("sort_order").Integer.GetValueOrDefault();
        set => SetField("sort_order", new(Integer: (int)value));
    }

    /// <summary>Gets or sets operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public WiredValueSource OperandKind
    {
        get => (WiredValueSource)GetField("operand_kind").Integer.GetValueOrDefault();
        set => SetField("operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets sort variable. Furniture target; requires hasValue OR canReadCreationTime OR canReadLastUpdateTime</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string SortVariable
    {
        get => GetField("sort_variable").Text!;
        set => SetField("sort_variable", new(Text: value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete AmountReference control atomically.</summary>
    public WiredValueReference AmountReference
    {
        get => ReadReference("operand_kind", "filter_amount", "reference_target", "reference_variable");
        set => WriteReference("operand_kind", "filter_amount", "reference_target", "reference_variable", value);
    }
}

/// <summary>Typed settings for UserVariableFilter (addon code 13).</summary>
public sealed class WiredUserVariableFilterAddonForm : WiredForm
{
    internal WiredUserVariableFilterAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets filter amount. 1..1000</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public int FilterAmount
    {
        get => GetField("filter_amount").Integer.GetValueOrDefault();
        set => SetField("filter_amount", new(Integer: value));
    }

    /// <summary>Gets or sets sort order. 0 highest value, 1 lowest value, 2 oldest creation, 3 latest creation, 4 oldest update, 5 latest update; variables.sort_by.0..5</summary>
    /// <remarks>Client label: wiredfurni.params.variables.sort_by. None</remarks>
    public WiredSortOrderOption SortOrder
    {
        get => (WiredSortOrderOption)GetField("sort_order").Integer.GetValueOrDefault();
        set => SetField("sort_order", new(Integer: (int)value));
    }

    /// <summary>Gets or sets operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public WiredValueSource OperandKind
    {
        get => (WiredValueSource)GetField("operand_kind").Integer.GetValueOrDefault();
        set => SetField("operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets sort variable. User target; requires hasValue OR canReadCreationTime OR canReadLastUpdateTime</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string SortVariable
    {
        get => GetField("sort_variable").Text!;
        set => SetField("sort_variable", new(Text: value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.setfilter. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete AmountReference control atomically.</summary>
    public WiredValueReference AmountReference
    {
        get => ReadReference("operand_kind", "filter_amount", "reference_target", "reference_variable");
        set => WriteReference("operand_kind", "filter_amount", "reference_target", "reference_variable", value);
    }
}

/// <summary>Typed settings for UsernamePlaceholder (addon code 14).</summary>
public sealed class WiredUsernamePlaceholderAddonForm : WiredForm
{
    internal WiredUsernamePlaceholderAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets show multiple. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.texts.placeholder_type. None</remarks>
    public bool ShowMultiple
    {
        get => GetField("show_multiple").Boolean.GetValueOrDefault();
        set => SetField("show_multiple", new(Boolean: value));
    }

    /// <summary>Gets or sets placeholder name. First tab-separated component; maximum 32 characters, a-z A-Z 0-9 underscore or space; spaces become underscores and output lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.texts.placeholder_name. None</remarks>
    public string PlaceholderName
    {
        get => GetField("placeholder_name").Text!;
        set => SetField("placeholder_name", new(Text: value));
    }

    /// <summary>Gets or sets delimiter. Optional second tab-separated component; maximum 5 characters</summary>
    /// <remarks>Client label: wiredfurni.params.texts.select_delimiter. None</remarks>
    public string Delimiter
    {
        get => GetField("delimiter").Text!;
        set => SetField("delimiter", new(Text: value));
    }
}

/// <summary>Typed settings for VariablePlaceholder (addon code 15).</summary>
public sealed class WiredVariablePlaceholderAddonForm : WiredForm
{
    internal WiredVariablePlaceholderAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets show multiple. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.texts.placeholder_type. None</remarks>
    public bool ShowMultiple
    {
        get => GetField("show_multiple").Boolean.GetValueOrDefault();
        set => SetField("show_multiple", new(Boolean: value));
    }

    /// <summary>Gets or sets target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables. None</remarks>
    public WiredSourceDomain Target
    {
        get => (WiredSourceDomain)GetField("target").Integer.GetValueOrDefault();
        set => SetField("target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets text mode. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.texts.variable_display_type. None</remarks>
    public bool TextMode
    {
        get => GetField("text_mode").Boolean.GetValueOrDefault();
        set => SetField("text_mode", new(Boolean: value));
    }

    /// <summary>Gets or sets placeholder name. First tab-separated component; maximum 32 characters, a-z A-Z 0-9 underscore or space; spaces become underscores and output lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.texts.placeholder_name. None</remarks>
    public string PlaceholderName
    {
        get => GetField("placeholder_name").Text!;
        set => SetField("placeholder_name", new(Text: value));
    }

    /// <summary>Gets or sets delimiter. Optional second tab-separated component; maximum 5 characters</summary>
    /// <remarks>Client label: wiredfurni.params.texts.select_delimiter. None</remarks>
    public string Delimiter
    {
        get => GetField("delimiter").Text!;
        set => SetField("delimiter", new(Text: value));
    }

    /// <summary>Gets or sets variable. hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Variable
    {
        get => GetField("variable").Text!;
        set => SetField("variable", new(Text: value));
    }
}

/// <summary>Typed settings for VariableCapturer (addon code 16).</summary>
public sealed class WiredVariableCapturerAddonForm : WiredForm
{
    internal WiredVariableCapturerAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets text mode. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.texts.variable_input_type. None</remarks>
    public bool TextMode
    {
        get => GetField("text_mode").Boolean.GetValueOrDefault();
        set => SetField("text_mode", new(Boolean: value));
    }

    /// <summary>Gets or sets capture name. First tab-separated component; maximum 32 characters, a-z A-Z 0-9 underscore or space; spaces become underscores and output lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.texts.capturer_name. None</remarks>
    public string CaptureName
    {
        get => GetField("capture_name").Text!;
        set => SetField("capture_name", new(Text: value));
    }

    /// <summary>Gets or sets variable. Context target only; requires hasValue AND canCreateAndDelete AND canWriteValue</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Variable
    {
        get => GetField("variable").Text!;
        set => SetField("variable", new(Text: value));
    }
}

/// <summary>Typed settings for ExecuteInOrder (addon code 17).</summary>
public sealed class WiredExecuteInOrderAddonForm : WiredForm
{
    internal WiredExecuteInOrderAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }
}

/// <summary>Typed settings for ChestItemTypeScanner (addon code 18).</summary>
public sealed class WiredChestItemTypeScannerAddonForm : WiredForm
{
    internal WiredChestItemTypeScannerAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets scanning mode. 0 all items in chest, 1 only previewed items</summary>
    /// <remarks>Client label: wiredfurni.params.chest_item_type_scanner. None</remarks>
    public WiredScanningModeOption ScanningMode
    {
        get => (WiredScanningModeOption)GetField("scanning_mode").Integer.GetValueOrDefault();
        set => SetField("scanning_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets variable. Context target only; requires hasValue AND canCreateAndDelete AND canWriteValue</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string Variable
    {
        get => GetField("variable").Text!;
        set => SetField("variable", new(Text: value));
    }
}

/// <summary>Typed settings for FurniNamePlaceholder (addon code 19).</summary>
public sealed class WiredFurniNamePlaceholderAddonForm : WiredForm
{
    internal WiredFurniNamePlaceholderAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets show multiple. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.texts.placeholder_type. None</remarks>
    public bool ShowMultiple
    {
        get => GetField("show_multiple").Boolean.GetValueOrDefault();
        set => SetField("show_multiple", new(Boolean: value));
    }

    /// <summary>Gets or sets placeholder name. First tab-separated component; maximum 32 characters, a-z A-Z 0-9 underscore or space; spaces become underscores and output lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.texts.placeholder_name. None</remarks>
    public string PlaceholderName
    {
        get => GetField("placeholder_name").Text!;
        set => SetField("placeholder_name", new(Text: value));
    }

    /// <summary>Gets or sets delimiter. Optional second tab-separated component; maximum 5 characters</summary>
    /// <remarks>Client label: wiredfurni.params.texts.select_delimiter. None</remarks>
    public string Delimiter
    {
        get => GetField("delimiter").Text!;
        set => SetField("delimiter", new(Text: value));
    }
}

/// <summary>Typed settings for CustomContract (addon code 20).</summary>
public sealed class WiredCustomContractAddonForm : WiredForm
{
    internal WiredCustomContractAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets payment enabled. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.enable_payment. None</remarks>
    public bool PaymentEnabled
    {
        get => GetField("payment_enabled").Boolean.GetValueOrDefault();
        set => SetField("payment_enabled", new(Boolean: value));
    }

    /// <summary>Gets or sets payment type. 0 credits, 1 furniture</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.element_type_selection. None</remarks>
    public WiredPaymentTypeOption PaymentType
    {
        get => (WiredPaymentTypeOption)GetField("payment_type").Integer.GetValueOrDefault();
        set => SetField("payment_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets payment operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.amount_selection. None</remarks>
    public WiredValueSource PaymentOperandKind
    {
        get => (WiredValueSource)GetField("payment_operand_kind").Integer.GetValueOrDefault();
        set => SetField("payment_operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets payment amount. 1..100000</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.amount_selection. None</remarks>
    public int PaymentAmount
    {
        get => GetField("payment_amount").Integer.GetValueOrDefault();
        set => SetField("payment_amount", new(Integer: value));
    }

    /// <summary>Gets or sets payment target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference_payment. None</remarks>
    public WiredSourceDomain PaymentTarget
    {
        get => (WiredSourceDomain)GetField("payment_target").Integer.GetValueOrDefault();
        set => SetField("payment_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets payment variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.amount_selection. None</remarks>
    public string PaymentVariable
    {
        get => GetField("payment_variable").Text!;
        set => SetField("payment_variable", new(Text: value));
    }

    /// <summary>Gets or sets reward enabled. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.enable_reward. None</remarks>
    public bool RewardEnabled
    {
        get => GetField("reward_enabled").Boolean.GetValueOrDefault();
        set => SetField("reward_enabled", new(Boolean: value));
    }

    /// <summary>Gets or sets reward type. 0 credits, 1 furniture</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.element_type_selection. None</remarks>
    public WiredPaymentTypeOption RewardType
    {
        get => (WiredPaymentTypeOption)GetField("reward_type").Integer.GetValueOrDefault();
        set => SetField("reward_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reward operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.amount_selection. None</remarks>
    public WiredValueSource RewardOperandKind
    {
        get => (WiredValueSource)GetField("reward_operand_kind").Integer.GetValueOrDefault();
        set => SetField("reward_operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reward amount. 1..100000</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.amount_selection. None</remarks>
    public int RewardAmount
    {
        get => GetField("reward_amount").Integer.GetValueOrDefault();
        set => SetField("reward_amount", new(Integer: value));
    }

    /// <summary>Gets or sets reward target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference_reward. None</remarks>
    public WiredSourceDomain RewardTarget
    {
        get => (WiredSourceDomain)GetField("reward_target").Integer.GetValueOrDefault();
        set => SetField("reward_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reward variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.custom_contract.amount_selection. None</remarks>
    public string RewardVariable
    {
        get => GetField("reward_variable").Text!;
        set => SetField("reward_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete PaymentReference control atomically.</summary>
    public WiredValueReference PaymentReference
    {
        get => ReadReference("payment_operand_kind", "payment_amount", "payment_target", "payment_variable");
        set => WriteReference("payment_operand_kind", "payment_amount", "payment_target", "payment_variable", value);
    }

    /// <summary>Gets or sets the complete RewardReference control atomically.</summary>
    public WiredValueReference RewardReference
    {
        get => ReadReference("reward_operand_kind", "reward_amount", "reward_target", "reward_variable");
        set => WriteReference("reward_operand_kind", "reward_amount", "reward_target", "reward_variable", value);
    }
}

/// <summary>Typed settings for Projectile (addon code 21).</summary>
public sealed class WiredProjectileAddonForm : WiredForm
{
    internal WiredProjectileAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets rotate projectile. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.new_direction_enabled. None</remarks>
    public bool RotateProjectile
    {
        get => GetField("rotate_projectile").Boolean.GetValueOrDefault();
        set => SetField("rotate_projectile", new(Boolean: value));
    }

    /// <summary>Gets or sets direction system. 0 eight directions straight, 1 eight directions diffuse, 2 four directions option 1, 3 four directions option 2</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.directional_system. None</remarks>
    public WiredDirectionSystemOption DirectionSystem
    {
        get => (WiredDirectionSystemOption)GetField("direction_system").Integer.GetValueOrDefault();
        set => SetField("direction_system", new(Integer: (int)value));
    }

    /// <summary>Gets or sets scale time. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.override_animation_time. None</remarks>
    public bool ScaleTime
    {
        get => GetField("scale_time").Boolean.GetValueOrDefault();
        set => SetField("scale_time", new(Boolean: value));
    }

    /// <summary>Gets or sets time operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.time_per_tile. None</remarks>
    public WiredValueSource TimeOperandKind
    {
        get => (WiredValueSource)GetField("time_operand_kind").Integer.GetValueOrDefault();
        set => SetField("time_operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets time per tile. 1..100000 milliseconds</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.time_per_tile. None</remarks>
    public int TimePerTile
    {
        get => GetField("time_per_tile").Integer.GetValueOrDefault();
        set => SetField("time_per_tile", new(Integer: value));
    }

    /// <summary>Gets or sets time target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.time_per_tile. None</remarks>
    public WiredSourceDomain TimeTarget
    {
        get => (WiredSourceDomain)GetField("time_target").Integer.GetValueOrDefault();
        set => SetField("time_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets distance x. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.distance_x. None</remarks>
    public bool DistanceX
    {
        get => GetField("distance_x").Boolean.GetValueOrDefault();
        set => SetField("distance_x", new(Boolean: value));
    }

    /// <summary>Gets or sets distance y. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.distance_y. None</remarks>
    public bool DistanceY
    {
        get => GetField("distance_y").Boolean.GetValueOrDefault();
        set => SetField("distance_y", new(Boolean: value));
    }

    /// <summary>Gets or sets distance z. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.distance_z. None</remarks>
    public bool DistanceZ
    {
        get => GetField("distance_z").Boolean.GetValueOrDefault();
        set => SetField("distance_z", new(Boolean: value));
    }

    /// <summary>Gets or sets speed increase. 0..100000 milliseconds</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.increase_speed. None</remarks>
    public int SpeedIncrease
    {
        get => GetField("speed_increase").Integer.GetValueOrDefault();
        set => SetField("speed_increase", new(Integer: value));
    }

    /// <summary>Gets or sets rotation offset. 0..7</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.rotation_offset. None</remarks>
    public int RotationOffset
    {
        get => GetField("rotation_offset").Integer.GetValueOrDefault();
        set => SetField("rotation_offset", new(Integer: value));
    }

    /// <summary>Gets or sets internal variables. Bits 0 tiles_travelled, 1 user_collisions, 2 furni_collisions, 3 position.x, 4 position.y, 5 position.altitude, 6 is_travelling; names prefixed animation.</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.projectile.variables. None</remarks>
    public WiredInternalVariablesFlags InternalVariables
    {
        get => (WiredInternalVariablesFlags)GetField("internal_variables").Integer.GetValueOrDefault();
        set => SetField("internal_variables", new(Integer: (int)value));
    }

    /// <summary>Gets or sets rotate shooter. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.change_shooter_direction. None</remarks>
    public bool RotateShooter
    {
        get => GetField("rotate_shooter").Boolean.GetValueOrDefault();
        set => SetField("rotate_shooter", new(Boolean: value));
    }

    /// <summary>Gets or sets bunny hop. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.bunny_hop. None</remarks>
    public bool BunnyHop
    {
        get => GetField("bunny_hop").Boolean.GetValueOrDefault();
        set => SetField("bunny_hop", new(Boolean: value));
    }

    /// <summary>Gets or sets distance mode. 0 normal start-to-target, 1 overshoot X tiles, 2 always shoot X tiles</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.animation_trajectory.distance. None</remarks>
    public WiredDistanceModeOption DistanceMode
    {
        get => (WiredDistanceModeOption)GetField("distance_mode").Integer.GetValueOrDefault();
        set => SetField("distance_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets distance operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.animation_trajectory.distance_selection. None</remarks>
    public WiredValueSource DistanceOperandKind
    {
        get => (WiredValueSource)GetField("distance_operand_kind").Integer.GetValueOrDefault();
        set => SetField("distance_operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets distance. -64..64 tiles</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.animation_trajectory.distance_selection. None</remarks>
    public int Distance
    {
        get => GetField("distance").Integer.GetValueOrDefault();
        set => SetField("distance", new(Integer: value));
    }

    /// <summary>Gets or sets distance target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.animation_trajectory.distance_selection. None</remarks>
    public WiredSourceDomain DistanceTarget
    {
        get => (WiredSourceDomain)GetField("distance_target").Integer.GetValueOrDefault();
        set => SetField("distance_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets curve strength. -1000..1000; zero straight, nonzero curved</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.animation_trajectory.trajectory.1.extra. None</remarks>
    public int CurveStrength
    {
        get => GetField("curve_strength").Integer.GetValueOrDefault();
        set => SetField("curve_strength", new(Integer: value));
    }

    /// <summary>Gets or sets time variable. hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.time_per_tile. None</remarks>
    public string TimeVariable
    {
        get => GetField("time_variable").Text!;
        set => SetField("time_variable", new(Text: value));
    }

    /// <summary>Gets or sets distance variable. hasValue required</summary>
    /// <remarks>Client label: wiredfurni.params.projectile.animation_trajectory.distance_selection. None</remarks>
    public string DistanceVariable
    {
        get => GetField("distance_variable").Text!;
        set => SetField("distance_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete TravelTimeReference control atomically.</summary>
    public WiredValueReference TravelTimeReference
    {
        get => ReadReference("time_operand_kind", "time_per_tile", "time_target", "time_variable");
        set => WriteReference("time_operand_kind", "time_per_tile", "time_target", "time_variable", value);
    }

    /// <summary>Gets or sets the complete DistanceReference control atomically.</summary>
    public WiredValueReference DistanceReference
    {
        get => ReadReference("distance_operand_kind", "distance", "distance_target", "distance_variable");
        set => WriteReference("distance_operand_kind", "distance", "distance_target", "distance_variable", value);
    }
}

/// <summary>Typed settings for JumpStrength (addon code 22).</summary>
public sealed class WiredJumpStrengthAddonForm : WiredForm
{
    internal WiredJumpStrengthAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets operand kind. 0 constant, 1 variable</summary>
    /// <remarks>Client label: wiredfurni.params.jump_strength. None</remarks>
    public WiredValueSource OperandKind
    {
        get => (WiredValueSource)GetField("operand_kind").Integer.GetValueOrDefault();
        set => SetField("operand_kind", new(Integer: (int)value));
    }

    /// <summary>Gets or sets strength. -1000..1000</summary>
    /// <remarks>Client label: wiredfurni.params.jump_strength. None</remarks>
    public int Strength
    {
        get => GetField("strength").Integer.GetValueOrDefault();
        set => SetField("strength", new(Integer: value));
    }

    /// <summary>Gets or sets reference target. 0 furniture, 1 user, -10 global, -20 context</summary>
    /// <remarks>Client label: wiredfurni.params.sources.merged.title.variables_reference. None</remarks>
    public WiredSourceDomain ReferenceTarget
    {
        get => (WiredSourceDomain)GetField("reference_target").Integer.GetValueOrDefault();
        set => SetField("reference_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets reference variable. hasValue required; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.jump_strength. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }

    /// <summary>Gets or sets the complete StrengthReference control atomically.</summary>
    public WiredValueReference StrengthReference
    {
        get => ReadReference("operand_kind", "strength", "reference_target", "reference_variable");
        set => WriteReference("operand_kind", "strength", "reference_target", "reference_variable", value);
    }
}

/// <summary>Typed settings for VariableTextConverter (addon code 1000).</summary>
public sealed class WiredVariableTextConverterAddonForm : WiredForm
{
    internal WiredVariableTextConverterAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets text mapping. Maximum 1000 characters; caption shows lines value=text</summary>
    /// <remarks>Client label: wiredfurni.params.variables.connect_text.title. None</remarks>
    public string TextMapping
    {
        get => GetField("text_mapping").Text!;
        set => SetField("text_mapping", new(Text: value));
    }
}

/// <summary>Typed settings for VariableLevelUp (addon code 1001).</summary>
public sealed class WiredVariableLevelUpAddonForm : WiredForm
{
    internal WiredVariableLevelUpAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets subvariable mask. Bits 0 current_level, 1 current_xp, 2 progress, 3 progress_percentage, 4 xp_required, 5 xp_remaining, 6 is_maxed, 7 max_level</summary>
    /// <remarks>Client label: wiredfurni.params.create_subvariables. None</remarks>
    public WiredSubvariableMaskFlags SubvariableMask
    {
        get => (WiredSubvariableMaskFlags)GetField("subvariable_mask").Integer.GetValueOrDefault();
        set => SetField("subvariable_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets mode. 0 manual interpolation, 1 linear, 2 exponential</summary>
    /// <remarks>Client label: wiredfurni.params.levelup.mode. None</remarks>
    public WiredVariableLevelUpAddonModeOption Mode
    {
        get => (WiredVariableLevelUpAddonModeOption)GetField("mode").Integer.GetValueOrDefault();
        set => SetField("mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets step or first xp. Mode 1 step size or mode 2 first-level XP; 1..100000</summary>
    /// <remarks>Client label: wiredfurni.params.levelup.step_size. Absent in manual mode</remarks>
    public int StepOrFirstXp
    {
        get => GetField("step_or_first_xp").Integer.GetValueOrDefault();
        set => SetField("step_or_first_xp", new(Integer: value));
    }

    /// <summary>Gets or sets maximum or increase. Mode 1 max level 2..100000; mode 2 increase factor 1..100000 (levelup.increase_factor)</summary>
    /// <remarks>Client label: wiredfurni.params.levelup.max_level. Absent in manual mode</remarks>
    public int MaximumOrIncrease
    {
        get => GetField("maximum_or_increase").Integer.GetValueOrDefault();
        set => SetField("maximum_or_increase", new(Integer: value));
    }

    /// <summary>Gets or sets maximum level. 2..100000</summary>
    /// <remarks>Client label: wiredfurni.params.levelup.max_level. Present only in exponential mode</remarks>
    public int MaximumLevel
    {
        get => GetField("maximum_level").Integer.GetValueOrDefault();
        set => SetField("maximum_level", new(Integer: value));
    }

    /// <summary>Gets or sets level mapping. Manual mode only; maximum 1000 characters, restricted digits, equals and CR; otherwise empty</summary>
    /// <remarks>Client label: wiredfurni.params.levelup.interpolation_placeholder. None</remarks>
    public string LevelMapping
    {
        get => GetField("level_mapping").Text!;
        set => SetField("level_mapping", new(Text: value));
    }
}

/// <summary>Typed settings for VariableTimeUtil (addon code 1002).</summary>
public sealed class WiredVariableTimeUtilAddonForm : WiredForm
{
    internal WiredVariableTimeUtilAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets subvariable mask. Bits 1 milliseconds_of_seconds, 2 seconds_of_minute, 3 minute_of_hour, 4 hour_of_day, 5 day_of_week, 6 day_of_month, 7 day_of_year, 8 week_of_year, 9 month_of_year, 10 year; 20 millisecond, 21 second, 22 minute, 23 hour, 24 day, 25 week, 26 month</summary>
    /// <remarks>Client label: wiredfurni.params.create_subvariables. None</remarks>
    public WiredVariableTimeUtilAddonSubvariableMaskFlags SubvariableMask
    {
        get => (WiredVariableTimeUtilAddonSubvariableMaskFlags)GetField("subvariable_mask").Integer.GetValueOrDefault();
        set => SetField("subvariable_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets time source. 0 value, 1 creation time, 2 last update time; time_util.mode.0..2</summary>
    /// <remarks>Client label: wiredfurni.params.choose_type. None</remarks>
    public WiredTimeSourceOption TimeSource
    {
        get => (WiredTimeSourceOption)GetField("time_source").Integer.GetValueOrDefault();
        set => SetField("time_source", new(Integer: (int)value));
    }
}

/// <summary>Typed settings for VariableFxHealthPoints (addon code 1200).</summary>
public sealed class WiredVariableFxHealthPointsAddonForm : WiredForm
{
    internal WiredVariableFxHealthPointsAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets source type. 0 furniture, 1 user; invalid becomes 1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public WiredSourceDomain SourceType
    {
        get => (WiredSourceDomain)GetField("source_type").Integer.GetValueOrDefault();
        set => SetField("source_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets audience. 0 only the user, 1 same game team, 2 everyone, 3 users with variable, 4 users with variable equal to selected value</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.audience. None</remarks>
    public WiredAudienceOption Audience
    {
        get => (WiredAudienceOption)GetField("audience").Integer.GetValueOrDefault();
        set => SetField("audience", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show mode. 0 always, 1 when variable updates, 2 on mouse hover; key show_mode.never is localized as Visible on mouse hover</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_mode.always. None</remarks>
    public WiredShowModeOption ShowMode
    {
        get => (WiredShowModeOption)GetField("show_mode").Integer.GetValueOrDefault();
        set => SetField("show_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets update mask. Bits 0 created, 1 increased, 2 decreased, 3 unchanged; clamped numerically to 0..15</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.update_mask.1. None</remarks>
    public WiredUpdateMaskFlags UpdateMask
    {
        get => (WiredUpdateMaskFlags)GetField("update_mask").Integer.GetValueOrDefault();
        set => SetField("update_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show on hover. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.mouse_hover. None</remarks>
    public bool ShowOnHover
    {
        get => GetField("show_on_hover").Boolean.GetValueOrDefault();
        set => SetField("show_on_hover", new(Boolean: value));
    }

    /// <summary>Gets or sets duration milliseconds. 1500..20000; clamped; number widget initially 3000</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_duration. None</remarks>
    public int DurationMilliseconds
    {
        get => GetField("duration_milliseconds").Integer.GetValueOrDefault();
        set => SetField("duration_milliseconds", new(Integer: value));
    }

    /// <summary>Gets or sets style id. Per-category style ID, see VARIABLE_FX_EDITOR.md; invalid becomes first style</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public int StyleId
    {
        get => GetField("style_id").Integer.GetValueOrDefault();
        set => SetField("style_id", new(Integer: value));
    }

    /// <summary>Gets or sets color id. Per-style allowed color ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.color. None</remarks>
    public int ColorId
    {
        get => GetField("color_id").Integer.GetValueOrDefault();
        set => SetField("color_id", new(Integer: value));
    }

    /// <summary>Gets or sets width id. Per-style allowed width ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.width. None</remarks>
    public int WidthId
    {
        get => GetField("width_id").Integer.GetValueOrDefault();
        set => SetField("width_id", new(Integer: value));
    }

    /// <summary>Gets or sets renderer id. Per-style renderer ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.renderer. None</remarks>
    public int RendererId
    {
        get => GetField("renderer_id").Integer.GetValueOrDefault();
        set => SetField("renderer_id", new(Integer: value));
    }

    /// <summary>Gets minimum high. Sign extension of index 11; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumHigh
    {
        get => GetField("minimum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets minimum value. Signed int32; number widget initially 0</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumValue
    {
        get => GetField("minimum_value").Integer.GetValueOrDefault();
        set => SetField("minimum_value", new(Integer: value));
    }

    /// <summary>Gets maximum high. Sign extension of index 13; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumHigh
    {
        get => GetField("maximum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets maximum value. Signed int32; number widget initially 100</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumValue
    {
        get => GetField("maximum_value").Integer.GetValueOrDefault();
        set => SetField("maximum_value", new(Integer: value));
    }

    /// <summary>Gets or sets override minimum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public bool OverrideMinimum
    {
        get => GetField("override_minimum").Boolean.GetValueOrDefault();
        set => SetField("override_minimum", new(Boolean: value));
    }

    /// <summary>Gets or sets override maximum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public bool OverrideMaximum
    {
        get => GetField("override_maximum").Boolean.GetValueOrDefault();
        set => SetField("override_maximum", new(Boolean: value));
    }

    /// <summary>Gets or sets minimum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public WiredSourceDomain MinimumTarget
    {
        get => (WiredSourceDomain)GetField("minimum_target").Integer.GetValueOrDefault();
        set => SetField("minimum_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets maximum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public WiredSourceDomain MaximumTarget
    {
        get => (WiredSourceDomain)GetField("maximum_target").Integer.GetValueOrDefault();
        set => SetField("maximum_target", new(Integer: (int)value));
    }

    /// <summary>Gets audience value high. Sign extension of index 19; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValueHigh
    {
        get => GetField("audience_value_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets audience value. Signed int32; default 0; used by audience 4</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValue
    {
        get => GetField("audience_value").Integer.GetValueOrDefault();
        set => SetField("audience_value", new(Integer: value));
    }

    /// <summary>Gets or sets segments. 0 unspecified, 1..100; clamped; forced 0 unless effective renderer is 2,4,13</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.segments. None</remarks>
    public int Segments
    {
        get => GetField("segments").Integer.GetValueOrDefault();
        set => SetField("segments", new(Integer: value));
    }

    /// <summary>Gets or sets minimum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public string MinimumVariable
    {
        get => GetField("minimum_variable").Text!;
        set => SetField("minimum_variable", new(Text: value));
    }

    /// <summary>Gets or sets maximum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public string MaximumVariable
    {
        get => GetField("maximum_variable").Text!;
        set => SetField("maximum_variable", new(Text: value));
    }

    /// <summary>Gets or sets audience variable. User variable; any variable allowed, value filter additionally requires hasValue</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.has_variable. None</remarks>
    public string AudienceVariable
    {
        get => GetField("audience_variable").Text!;
        set => SetField("audience_variable", new(Text: value));
    }
}

/// <summary>Typed settings for VariableFxProgressBar (addon code 1201).</summary>
public sealed class WiredVariableFxProgressBarAddonForm : WiredForm
{
    internal WiredVariableFxProgressBarAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets source type. 0 furniture, 1 user; invalid becomes 1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public WiredSourceDomain SourceType
    {
        get => (WiredSourceDomain)GetField("source_type").Integer.GetValueOrDefault();
        set => SetField("source_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets audience. 0 only the user, 1 same game team, 2 everyone, 3 users with variable, 4 users with variable equal to selected value</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.audience. None</remarks>
    public WiredAudienceOption Audience
    {
        get => (WiredAudienceOption)GetField("audience").Integer.GetValueOrDefault();
        set => SetField("audience", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show mode. 0 always, 1 when variable updates, 2 on mouse hover; key show_mode.never is localized as Visible on mouse hover</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_mode.always. None</remarks>
    public WiredShowModeOption ShowMode
    {
        get => (WiredShowModeOption)GetField("show_mode").Integer.GetValueOrDefault();
        set => SetField("show_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets update mask. Bits 0 created, 1 increased, 2 decreased, 3 unchanged; clamped numerically to 0..15</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.update_mask.1. None</remarks>
    public WiredUpdateMaskFlags UpdateMask
    {
        get => (WiredUpdateMaskFlags)GetField("update_mask").Integer.GetValueOrDefault();
        set => SetField("update_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show on hover. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.mouse_hover. None</remarks>
    public bool ShowOnHover
    {
        get => GetField("show_on_hover").Boolean.GetValueOrDefault();
        set => SetField("show_on_hover", new(Boolean: value));
    }

    /// <summary>Gets or sets duration milliseconds. 1500..20000; clamped; number widget initially 3000</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_duration. None</remarks>
    public int DurationMilliseconds
    {
        get => GetField("duration_milliseconds").Integer.GetValueOrDefault();
        set => SetField("duration_milliseconds", new(Integer: value));
    }

    /// <summary>Gets or sets style id. Per-category style ID, see VARIABLE_FX_EDITOR.md; invalid becomes first style</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public int StyleId
    {
        get => GetField("style_id").Integer.GetValueOrDefault();
        set => SetField("style_id", new(Integer: value));
    }

    /// <summary>Gets or sets color id. Per-style allowed color ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.color. None</remarks>
    public int ColorId
    {
        get => GetField("color_id").Integer.GetValueOrDefault();
        set => SetField("color_id", new(Integer: value));
    }

    /// <summary>Gets or sets width id. Per-style allowed width ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.width. None</remarks>
    public int WidthId
    {
        get => GetField("width_id").Integer.GetValueOrDefault();
        set => SetField("width_id", new(Integer: value));
    }

    /// <summary>Gets or sets renderer id. Per-style renderer ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.renderer. None</remarks>
    public int RendererId
    {
        get => GetField("renderer_id").Integer.GetValueOrDefault();
        set => SetField("renderer_id", new(Integer: value));
    }

    /// <summary>Gets minimum high. Sign extension of index 11; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumHigh
    {
        get => GetField("minimum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets minimum value. Signed int32; number widget initially 0</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumValue
    {
        get => GetField("minimum_value").Integer.GetValueOrDefault();
        set => SetField("minimum_value", new(Integer: value));
    }

    /// <summary>Gets maximum high. Sign extension of index 13; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumHigh
    {
        get => GetField("maximum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets maximum value. Signed int32; number widget initially 100</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumValue
    {
        get => GetField("maximum_value").Integer.GetValueOrDefault();
        set => SetField("maximum_value", new(Integer: value));
    }

    /// <summary>Gets or sets override minimum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public bool OverrideMinimum
    {
        get => GetField("override_minimum").Boolean.GetValueOrDefault();
        set => SetField("override_minimum", new(Boolean: value));
    }

    /// <summary>Gets or sets override maximum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public bool OverrideMaximum
    {
        get => GetField("override_maximum").Boolean.GetValueOrDefault();
        set => SetField("override_maximum", new(Boolean: value));
    }

    /// <summary>Gets or sets minimum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public WiredSourceDomain MinimumTarget
    {
        get => (WiredSourceDomain)GetField("minimum_target").Integer.GetValueOrDefault();
        set => SetField("minimum_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets maximum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public WiredSourceDomain MaximumTarget
    {
        get => (WiredSourceDomain)GetField("maximum_target").Integer.GetValueOrDefault();
        set => SetField("maximum_target", new(Integer: (int)value));
    }

    /// <summary>Gets audience value high. Sign extension of index 19; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValueHigh
    {
        get => GetField("audience_value_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets audience value. Signed int32; default 0; used by audience 4</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValue
    {
        get => GetField("audience_value").Integer.GetValueOrDefault();
        set => SetField("audience_value", new(Integer: value));
    }

    /// <summary>Gets or sets segments. 0 unspecified, 1..100; clamped; forced 0 unless effective renderer is 2,4,13</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.segments. None</remarks>
    public int Segments
    {
        get => GetField("segments").Integer.GetValueOrDefault();
        set => SetField("segments", new(Integer: value));
    }

    /// <summary>Gets or sets minimum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public string MinimumVariable
    {
        get => GetField("minimum_variable").Text!;
        set => SetField("minimum_variable", new(Text: value));
    }

    /// <summary>Gets or sets maximum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public string MaximumVariable
    {
        get => GetField("maximum_variable").Text!;
        set => SetField("maximum_variable", new(Text: value));
    }

    /// <summary>Gets or sets audience variable. User variable; any variable allowed, value filter additionally requires hasValue</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.has_variable. None</remarks>
    public string AudienceVariable
    {
        get => GetField("audience_variable").Text!;
        set => SetField("audience_variable", new(Text: value));
    }
}

/// <summary>Typed settings for VariableFxLevellingProgress (addon code 1202).</summary>
public sealed class WiredVariableFxLevellingProgressAddonForm : WiredForm
{
    internal WiredVariableFxLevellingProgressAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets source type. 0 furniture, 1 user; invalid becomes 1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public WiredSourceDomain SourceType
    {
        get => (WiredSourceDomain)GetField("source_type").Integer.GetValueOrDefault();
        set => SetField("source_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets audience. 0 only the user, 1 same game team, 2 everyone, 3 users with variable, 4 users with variable equal to selected value</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.audience. None</remarks>
    public WiredAudienceOption Audience
    {
        get => (WiredAudienceOption)GetField("audience").Integer.GetValueOrDefault();
        set => SetField("audience", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show mode. 0 always, 1 when variable updates, 2 on mouse hover; key show_mode.never is localized as Visible on mouse hover</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_mode.always. None</remarks>
    public WiredShowModeOption ShowMode
    {
        get => (WiredShowModeOption)GetField("show_mode").Integer.GetValueOrDefault();
        set => SetField("show_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets update mask. Bits 0 created, 1 increased, 2 decreased, 3 unchanged; clamped numerically to 0..15</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.update_mask.1. None</remarks>
    public WiredUpdateMaskFlags UpdateMask
    {
        get => (WiredUpdateMaskFlags)GetField("update_mask").Integer.GetValueOrDefault();
        set => SetField("update_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show on hover. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.mouse_hover. None</remarks>
    public bool ShowOnHover
    {
        get => GetField("show_on_hover").Boolean.GetValueOrDefault();
        set => SetField("show_on_hover", new(Boolean: value));
    }

    /// <summary>Gets or sets duration milliseconds. 1500..20000; clamped; number widget initially 3000</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_duration. None</remarks>
    public int DurationMilliseconds
    {
        get => GetField("duration_milliseconds").Integer.GetValueOrDefault();
        set => SetField("duration_milliseconds", new(Integer: value));
    }

    /// <summary>Gets or sets style id. Per-category style ID, see VARIABLE_FX_EDITOR.md; invalid becomes first style</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public int StyleId
    {
        get => GetField("style_id").Integer.GetValueOrDefault();
        set => SetField("style_id", new(Integer: value));
    }

    /// <summary>Gets or sets color id. Per-style allowed color ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.color. None</remarks>
    public int ColorId
    {
        get => GetField("color_id").Integer.GetValueOrDefault();
        set => SetField("color_id", new(Integer: value));
    }

    /// <summary>Gets or sets width id. Per-style allowed width ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.width. None</remarks>
    public int WidthId
    {
        get => GetField("width_id").Integer.GetValueOrDefault();
        set => SetField("width_id", new(Integer: value));
    }

    /// <summary>Gets or sets renderer id. Per-style renderer ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.renderer. None</remarks>
    public int RendererId
    {
        get => GetField("renderer_id").Integer.GetValueOrDefault();
        set => SetField("renderer_id", new(Integer: value));
    }

    /// <summary>Gets minimum high. Sign extension of index 11; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumHigh
    {
        get => GetField("minimum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets minimum value. Signed int32; number widget initially 0</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumValue
    {
        get => GetField("minimum_value").Integer.GetValueOrDefault();
        set => SetField("minimum_value", new(Integer: value));
    }

    /// <summary>Gets maximum high. Sign extension of index 13; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumHigh
    {
        get => GetField("maximum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets maximum value. Signed int32; number widget initially 100</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumValue
    {
        get => GetField("maximum_value").Integer.GetValueOrDefault();
        set => SetField("maximum_value", new(Integer: value));
    }

    /// <summary>Gets or sets override minimum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public bool OverrideMinimum
    {
        get => GetField("override_minimum").Boolean.GetValueOrDefault();
        set => SetField("override_minimum", new(Boolean: value));
    }

    /// <summary>Gets or sets override maximum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public bool OverrideMaximum
    {
        get => GetField("override_maximum").Boolean.GetValueOrDefault();
        set => SetField("override_maximum", new(Boolean: value));
    }

    /// <summary>Gets or sets minimum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public WiredSourceDomain MinimumTarget
    {
        get => (WiredSourceDomain)GetField("minimum_target").Integer.GetValueOrDefault();
        set => SetField("minimum_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets maximum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public WiredSourceDomain MaximumTarget
    {
        get => (WiredSourceDomain)GetField("maximum_target").Integer.GetValueOrDefault();
        set => SetField("maximum_target", new(Integer: (int)value));
    }

    /// <summary>Gets audience value high. Sign extension of index 19; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValueHigh
    {
        get => GetField("audience_value_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets audience value. Signed int32; default 0; used by audience 4</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValue
    {
        get => GetField("audience_value").Integer.GetValueOrDefault();
        set => SetField("audience_value", new(Integer: value));
    }

    /// <summary>Gets or sets segments. 0 unspecified, 1..100; clamped; forced 0 unless effective renderer is 2,4,13</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.segments. None</remarks>
    public int Segments
    {
        get => GetField("segments").Integer.GetValueOrDefault();
        set => SetField("segments", new(Integer: value));
    }

    /// <summary>Gets or sets minimum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public string MinimumVariable
    {
        get => GetField("minimum_variable").Text!;
        set => SetField("minimum_variable", new(Text: value));
    }

    /// <summary>Gets or sets maximum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public string MaximumVariable
    {
        get => GetField("maximum_variable").Text!;
        set => SetField("maximum_variable", new(Text: value));
    }

    /// <summary>Gets or sets audience variable. User variable; any variable allowed, value filter additionally requires hasValue</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.has_variable. None</remarks>
    public string AudienceVariable
    {
        get => GetField("audience_variable").Text!;
        set => SetField("audience_variable", new(Text: value));
    }

    /// <summary>Gets or sets sub renderer id. Style 0 allows 2,3,4 (fallback 2); style 1 fixes 1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.sub_renderer. None</remarks>
    public int SubRendererId
    {
        get => GetField("sub_renderer_id").Integer.GetValueOrDefault();
        set => SetField("sub_renderer_id", new(Integer: value));
    }
}

/// <summary>Typed settings for VariableFxStatusBar (addon code 1203).</summary>
public sealed class WiredVariableFxStatusBarAddonForm : WiredForm
{
    internal WiredVariableFxStatusBarAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets source type. 0 furniture, 1 user; invalid becomes 1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public WiredSourceDomain SourceType
    {
        get => (WiredSourceDomain)GetField("source_type").Integer.GetValueOrDefault();
        set => SetField("source_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets audience. 0 only the user, 1 same game team, 2 everyone, 3 users with variable, 4 users with variable equal to selected value</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.audience. None</remarks>
    public WiredAudienceOption Audience
    {
        get => (WiredAudienceOption)GetField("audience").Integer.GetValueOrDefault();
        set => SetField("audience", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show mode. 0 always, 1 when variable updates, 2 on mouse hover; key show_mode.never is localized as Visible on mouse hover</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_mode.always. None</remarks>
    public WiredShowModeOption ShowMode
    {
        get => (WiredShowModeOption)GetField("show_mode").Integer.GetValueOrDefault();
        set => SetField("show_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets update mask. Bits 0 created, 1 increased, 2 decreased, 3 unchanged; clamped numerically to 0..15</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.update_mask.1. None</remarks>
    public WiredUpdateMaskFlags UpdateMask
    {
        get => (WiredUpdateMaskFlags)GetField("update_mask").Integer.GetValueOrDefault();
        set => SetField("update_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show on hover. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.mouse_hover. None</remarks>
    public bool ShowOnHover
    {
        get => GetField("show_on_hover").Boolean.GetValueOrDefault();
        set => SetField("show_on_hover", new(Boolean: value));
    }

    /// <summary>Gets or sets duration milliseconds. 1500..20000; clamped; number widget initially 3000</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_duration. None</remarks>
    public int DurationMilliseconds
    {
        get => GetField("duration_milliseconds").Integer.GetValueOrDefault();
        set => SetField("duration_milliseconds", new(Integer: value));
    }

    /// <summary>Gets or sets style id. Per-category style ID, see VARIABLE_FX_EDITOR.md; invalid becomes first style</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public int StyleId
    {
        get => GetField("style_id").Integer.GetValueOrDefault();
        set => SetField("style_id", new(Integer: value));
    }

    /// <summary>Gets or sets color id. Per-style allowed color ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.color. None</remarks>
    public int ColorId
    {
        get => GetField("color_id").Integer.GetValueOrDefault();
        set => SetField("color_id", new(Integer: value));
    }

    /// <summary>Gets or sets width id. Per-style allowed width ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.width. None</remarks>
    public int WidthId
    {
        get => GetField("width_id").Integer.GetValueOrDefault();
        set => SetField("width_id", new(Integer: value));
    }

    /// <summary>Gets or sets renderer id. Per-style renderer ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.renderer. None</remarks>
    public int RendererId
    {
        get => GetField("renderer_id").Integer.GetValueOrDefault();
        set => SetField("renderer_id", new(Integer: value));
    }

    /// <summary>Gets minimum high. Sign extension of index 11; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumHigh
    {
        get => GetField("minimum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets minimum value. Signed int32; number widget initially 0</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumValue
    {
        get => GetField("minimum_value").Integer.GetValueOrDefault();
        set => SetField("minimum_value", new(Integer: value));
    }

    /// <summary>Gets maximum high. Sign extension of index 13; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumHigh
    {
        get => GetField("maximum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets maximum value. Signed int32; number widget initially 100</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumValue
    {
        get => GetField("maximum_value").Integer.GetValueOrDefault();
        set => SetField("maximum_value", new(Integer: value));
    }

    /// <summary>Gets or sets override minimum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public bool OverrideMinimum
    {
        get => GetField("override_minimum").Boolean.GetValueOrDefault();
        set => SetField("override_minimum", new(Boolean: value));
    }

    /// <summary>Gets or sets override maximum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public bool OverrideMaximum
    {
        get => GetField("override_maximum").Boolean.GetValueOrDefault();
        set => SetField("override_maximum", new(Boolean: value));
    }

    /// <summary>Gets or sets minimum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public WiredSourceDomain MinimumTarget
    {
        get => (WiredSourceDomain)GetField("minimum_target").Integer.GetValueOrDefault();
        set => SetField("minimum_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets maximum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public WiredSourceDomain MaximumTarget
    {
        get => (WiredSourceDomain)GetField("maximum_target").Integer.GetValueOrDefault();
        set => SetField("maximum_target", new(Integer: (int)value));
    }

    /// <summary>Gets audience value high. Sign extension of index 19; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValueHigh
    {
        get => GetField("audience_value_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets audience value. Signed int32; default 0; used by audience 4</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValue
    {
        get => GetField("audience_value").Integer.GetValueOrDefault();
        set => SetField("audience_value", new(Integer: value));
    }

    /// <summary>Gets or sets segments. 0 unspecified, 1..100; clamped; forced 0 unless effective renderer is 2,4,13</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.segments. None</remarks>
    public int Segments
    {
        get => GetField("segments").Integer.GetValueOrDefault();
        set => SetField("segments", new(Integer: value));
    }

    /// <summary>Gets or sets minimum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public string MinimumVariable
    {
        get => GetField("minimum_variable").Text!;
        set => SetField("minimum_variable", new(Text: value));
    }

    /// <summary>Gets or sets maximum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public string MaximumVariable
    {
        get => GetField("maximum_variable").Text!;
        set => SetField("maximum_variable", new(Text: value));
    }

    /// <summary>Gets or sets audience variable. User variable; any variable allowed, value filter additionally requires hasValue</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.has_variable. None</remarks>
    public string AudienceVariable
    {
        get => GetField("audience_variable").Text!;
        set => SetField("audience_variable", new(Text: value));
    }
}

/// <summary>Typed settings for VariableFxBossBar (addon code 1204).</summary>
public sealed class WiredVariableFxBossBarAddonForm : WiredForm
{
    internal WiredVariableFxBossBarAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets source type. 0 furniture, 1 user; invalid becomes 1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public WiredSourceDomain SourceType
    {
        get => (WiredSourceDomain)GetField("source_type").Integer.GetValueOrDefault();
        set => SetField("source_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets audience. 0 only the user, 1 same game team, 2 everyone, 3 users with variable, 4 users with variable equal to selected value</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.audience. None</remarks>
    public WiredAudienceOption Audience
    {
        get => (WiredAudienceOption)GetField("audience").Integer.GetValueOrDefault();
        set => SetField("audience", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show mode. 0 always, 1 when variable updates, 2 on mouse hover; key show_mode.never is localized as Visible on mouse hover</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_mode.always. None</remarks>
    public WiredShowModeOption ShowMode
    {
        get => (WiredShowModeOption)GetField("show_mode").Integer.GetValueOrDefault();
        set => SetField("show_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets update mask. Bits 0 created, 1 increased, 2 decreased, 3 unchanged; clamped numerically to 0..15</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.update_mask.1. None</remarks>
    public WiredUpdateMaskFlags UpdateMask
    {
        get => (WiredUpdateMaskFlags)GetField("update_mask").Integer.GetValueOrDefault();
        set => SetField("update_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show on hover. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.mouse_hover. None</remarks>
    public bool ShowOnHover
    {
        get => GetField("show_on_hover").Boolean.GetValueOrDefault();
        set => SetField("show_on_hover", new(Boolean: value));
    }

    /// <summary>Gets or sets duration milliseconds. 1500..20000; clamped; number widget initially 3000</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_duration. None</remarks>
    public int DurationMilliseconds
    {
        get => GetField("duration_milliseconds").Integer.GetValueOrDefault();
        set => SetField("duration_milliseconds", new(Integer: value));
    }

    /// <summary>Gets or sets style id. Per-category style ID, see VARIABLE_FX_EDITOR.md; invalid becomes first style</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public int StyleId
    {
        get => GetField("style_id").Integer.GetValueOrDefault();
        set => SetField("style_id", new(Integer: value));
    }

    /// <summary>Gets or sets color id. Per-style allowed color ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.color. None</remarks>
    public int ColorId
    {
        get => GetField("color_id").Integer.GetValueOrDefault();
        set => SetField("color_id", new(Integer: value));
    }

    /// <summary>Gets or sets width id. Per-style allowed width ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.width. None</remarks>
    public int WidthId
    {
        get => GetField("width_id").Integer.GetValueOrDefault();
        set => SetField("width_id", new(Integer: value));
    }

    /// <summary>Gets or sets renderer id. Per-style renderer ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.renderer. None</remarks>
    public int RendererId
    {
        get => GetField("renderer_id").Integer.GetValueOrDefault();
        set => SetField("renderer_id", new(Integer: value));
    }

    /// <summary>Gets minimum high. Sign extension of index 11; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumHigh
    {
        get => GetField("minimum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets minimum value. Signed int32; number widget initially 0</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumValue
    {
        get => GetField("minimum_value").Integer.GetValueOrDefault();
        set => SetField("minimum_value", new(Integer: value));
    }

    /// <summary>Gets maximum high. Sign extension of index 13; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumHigh
    {
        get => GetField("maximum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets maximum value. Signed int32; number widget initially 100</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumValue
    {
        get => GetField("maximum_value").Integer.GetValueOrDefault();
        set => SetField("maximum_value", new(Integer: value));
    }

    /// <summary>Gets or sets override minimum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public bool OverrideMinimum
    {
        get => GetField("override_minimum").Boolean.GetValueOrDefault();
        set => SetField("override_minimum", new(Boolean: value));
    }

    /// <summary>Gets or sets override maximum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public bool OverrideMaximum
    {
        get => GetField("override_maximum").Boolean.GetValueOrDefault();
        set => SetField("override_maximum", new(Boolean: value));
    }

    /// <summary>Gets or sets minimum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public WiredSourceDomain MinimumTarget
    {
        get => (WiredSourceDomain)GetField("minimum_target").Integer.GetValueOrDefault();
        set => SetField("minimum_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets maximum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public WiredSourceDomain MaximumTarget
    {
        get => (WiredSourceDomain)GetField("maximum_target").Integer.GetValueOrDefault();
        set => SetField("maximum_target", new(Integer: (int)value));
    }

    /// <summary>Gets audience value high. Sign extension of index 19; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValueHigh
    {
        get => GetField("audience_value_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets audience value. Signed int32; default 0; used by audience 4</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValue
    {
        get => GetField("audience_value").Integer.GetValueOrDefault();
        set => SetField("audience_value", new(Integer: value));
    }

    /// <summary>Gets or sets segments. 0 unspecified, 1..100; clamped; forced 0 unless effective renderer is 2,4,13</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.segments. None</remarks>
    public int Segments
    {
        get => GetField("segments").Integer.GetValueOrDefault();
        set => SetField("segments", new(Integer: value));
    }

    /// <summary>Gets or sets minimum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public string MinimumVariable
    {
        get => GetField("minimum_variable").Text!;
        set => SetField("minimum_variable", new(Text: value));
    }

    /// <summary>Gets or sets maximum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public string MaximumVariable
    {
        get => GetField("maximum_variable").Text!;
        set => SetField("maximum_variable", new(Text: value));
    }

    /// <summary>Gets or sets audience variable. User variable; any variable allowed, value filter additionally requires hasValue</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.has_variable. None</remarks>
    public string AudienceVariable
    {
        get => GetField("audience_variable").Text!;
        set => SetField("audience_variable", new(Text: value));
    }
}

/// <summary>Typed settings for VariableFxNumberDisplay (addon code 1205).</summary>
public sealed class WiredVariableFxNumberDisplayAddonForm : WiredForm
{
    internal WiredVariableFxNumberDisplayAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets source type. 0 furniture, 1 user; invalid becomes 1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public WiredSourceDomain SourceType
    {
        get => (WiredSourceDomain)GetField("source_type").Integer.GetValueOrDefault();
        set => SetField("source_type", new(Integer: (int)value));
    }

    /// <summary>Gets or sets audience. 0 only the user, 1 same game team, 2 everyone, 3 users with variable, 4 users with variable equal to selected value</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.audience. None</remarks>
    public WiredAudienceOption Audience
    {
        get => (WiredAudienceOption)GetField("audience").Integer.GetValueOrDefault();
        set => SetField("audience", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show mode. 0 always, 1 when variable updates, 2 on mouse hover; key show_mode.never is localized as Visible on mouse hover</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_mode.always. None</remarks>
    public WiredShowModeOption ShowMode
    {
        get => (WiredShowModeOption)GetField("show_mode").Integer.GetValueOrDefault();
        set => SetField("show_mode", new(Integer: (int)value));
    }

    /// <summary>Gets or sets update mask. Bits 0 created, 1 increased, 2 decreased, 3 unchanged; clamped numerically to 0..15</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.update_mask.1. None</remarks>
    public WiredUpdateMaskFlags UpdateMask
    {
        get => (WiredUpdateMaskFlags)GetField("update_mask").Integer.GetValueOrDefault();
        set => SetField("update_mask", new(Integer: (int)value));
    }

    /// <summary>Gets or sets show on hover. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.mouse_hover. None</remarks>
    public bool ShowOnHover
    {
        get => GetField("show_on_hover").Boolean.GetValueOrDefault();
        set => SetField("show_on_hover", new(Boolean: value));
    }

    /// <summary>Gets or sets duration milliseconds. 1500..20000; clamped; number widget initially 3000</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.show_duration. None</remarks>
    public int DurationMilliseconds
    {
        get => GetField("duration_milliseconds").Integer.GetValueOrDefault();
        set => SetField("duration_milliseconds", new(Integer: value));
    }

    /// <summary>Gets or sets style id. Per-category style ID, see VARIABLE_FX_EDITOR.md; invalid becomes first style</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.style. None</remarks>
    public int StyleId
    {
        get => GetField("style_id").Integer.GetValueOrDefault();
        set => SetField("style_id", new(Integer: value));
    }

    /// <summary>Gets or sets color id. Per-style allowed color ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.color. None</remarks>
    public int ColorId
    {
        get => GetField("color_id").Integer.GetValueOrDefault();
        set => SetField("color_id", new(Integer: value));
    }

    /// <summary>Gets or sets width id. Per-style allowed width ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.width. None</remarks>
    public int WidthId
    {
        get => GetField("width_id").Integer.GetValueOrDefault();
        set => SetField("width_id", new(Integer: value));
    }

    /// <summary>Gets or sets renderer id. Per-style renderer ID; invalid becomes style default</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.renderer. None</remarks>
    public int RendererId
    {
        get => GetField("renderer_id").Integer.GetValueOrDefault();
        set => SetField("renderer_id", new(Integer: value));
    }

    /// <summary>Gets minimum high. Sign extension of index 11; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumHigh
    {
        get => GetField("minimum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets minimum value. Signed int32; number widget initially 0</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MinimumValue
    {
        get => GetField("minimum_value").Integer.GetValueOrDefault();
        set => SetField("minimum_value", new(Integer: value));
    }

    /// <summary>Gets maximum high. Sign extension of index 13; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumHigh
    {
        get => GetField("maximum_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets maximum value. Signed int32; number widget initially 100</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.value_range. None</remarks>
    public int MaximumValue
    {
        get => GetField("maximum_value").Integer.GetValueOrDefault();
        set => SetField("maximum_value", new(Integer: value));
    }

    /// <summary>Gets or sets override minimum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public bool OverrideMinimum
    {
        get => GetField("override_minimum").Boolean.GetValueOrDefault();
        set => SetField("override_minimum", new(Boolean: value));
    }

    /// <summary>Gets or sets override maximum. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public bool OverrideMaximum
    {
        get => GetField("override_maximum").Boolean.GetValueOrDefault();
        set => SetField("override_maximum", new(Boolean: value));
    }

    /// <summary>Gets or sets minimum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public WiredSourceDomain MinimumTarget
    {
        get => (WiredSourceDomain)GetField("minimum_target").Integer.GetValueOrDefault();
        set => SetField("minimum_target", new(Integer: (int)value));
    }

    /// <summary>Gets or sets maximum target. -10 global or same as source_type; all nonglobal values normalized to source_type</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public WiredSourceDomain MaximumTarget
    {
        get => (WiredSourceDomain)GetField("maximum_target").Integer.GetValueOrDefault();
        set => SetField("maximum_target", new(Integer: (int)value));
    }

    /// <summary>Gets audience value high. Sign extension of index 19; ignored by open</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValueHigh
    {
        get => GetField("audience_value_high").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets audience value. Signed int32; default 0; used by audience 4</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.audience_popup.value. None</remarks>
    public int AudienceValue
    {
        get => GetField("audience_value").Integer.GetValueOrDefault();
        set => SetField("audience_value", new(Integer: value));
    }

    /// <summary>Gets or sets segments. 0 unspecified, 1..100; clamped; forced 0 unless effective renderer is 2,4,13</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.segments. None</remarks>
    public int Segments
    {
        get => GetField("segments").Integer.GetValueOrDefault();
        set => SetField("segments", new(Integer: value));
    }

    /// <summary>Gets or sets minimum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_min. None</remarks>
    public string MinimumVariable
    {
        get => GetField("minimum_variable").Text!;
        set => SetField("minimum_variable", new(Text: value));
    }

    /// <summary>Gets or sets maximum variable. hasValue required; source-type target or global</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.advanced.override_max. None</remarks>
    public string MaximumVariable
    {
        get => GetField("maximum_variable").Text!;
        set => SetField("maximum_variable", new(Text: value));
    }

    /// <summary>Gets or sets audience variable. User variable; any variable allowed, value filter additionally requires hasValue</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visibility.has_variable. None</remarks>
    public string AudienceVariable
    {
        get => GetField("audience_variable").Text!;
        set => SetField("audience_variable", new(Text: value));
    }

    /// <summary>Gets or sets icon alignment. 0 left, 1 right, 2 both sides; invalid becomes 0</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.icon_alignment. None</remarks>
    public WiredIconAlignmentOption IconAlignment
    {
        get => (WiredIconAlignmentOption)GetField("icon_alignment").Integer.GetValueOrDefault();
        set => SetField("icon_alignment", new(Integer: (int)value));
    }

    /// <summary>Gets or sets icon. Empty or supported icon ID from VARIABLE_FX_EDITOR.md; unknown received icon selects empty</summary>
    /// <remarks>Client label: wiredfurni.params.variablefx.visualization.icon. None</remarks>
    public string Icon
    {
        get => GetField("icon").Text!;
        set => SetField("icon", new(Text: value));
    }
}

/// <summary>Typed settings for GlobalPlaceholder (addon code 2000).</summary>
public sealed class WiredGlobalPlaceholderAddonForm : WiredForm
{
    internal WiredGlobalPlaceholderAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets mode. 0 fixed value, 1 reference from another room</summary>
    /// <remarks>Client label: wiredfurni.params.choose_type. None</remarks>
    public WiredGlobalPlaceholderAddonModeOption Mode
    {
        get => (WiredGlobalPlaceholderAddonModeOption)GetField("mode").Integer.GetValueOrDefault();
        set => SetField("mode", new(Integer: (int)value));
    }

    /// <summary>Gets reserved. Always writes 0; ignored by open</summary>
    /// <remarks>Client label: Literal no control. None</remarks>
    public int Reserved
    {
        get => GetField("reserved").Integer.GetValueOrDefault();
    }

    /// <summary>Gets or sets room id. 0 fixed-value mode; selected room ID in reference mode</summary>
    /// <remarks>Client label: wiredfurni.params.room_selection. None</remarks>
    public int RoomId
    {
        get => GetField("room_id").Integer.GetValueOrDefault();
        set => SetField("room_id", new(Integer: value));
    }

    /// <summary>Gets or sets placeholder name. First tab-separated component; maximum 32 characters, a-z A-Z 0-9 underscore or space; spaces become underscores and output lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.texts.placeholder_name. None</remarks>
    public string PlaceholderName
    {
        get => GetField("placeholder_name").Text!;
        set => SetField("placeholder_name", new(Text: value));
    }

    /// <summary>Gets or sets value or reference. Fixed text maximum 100 characters or selected shared placeholder name</summary>
    /// <remarks>Client label: wiredfurni.params.placeholder_selection. None</remarks>
    public string ValueOrReference
    {
        get => GetField("value_or_reference").Text!;
        set => SetField("value_or_reference", new(Text: value));
    }
}

/// <summary>Typed settings for AchievementEnabler (addon code 2001).</summary>
public sealed class WiredAchievementEnablerAddonForm : WiredForm
{
    internal WiredAchievementEnablerAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets achievements. Maximum 2000 characters; caption requests achievement names without ACH_WF_, one per line</summary>
    /// <remarks>Client label: wiredfurni.params.achievement_enabler. None</remarks>
    public string Achievements
    {
        get => GetField("achievements").Text!;
        set => SetField("achievements", new(Text: value));
    }
}

/// <summary>Typed settings for VariablesWebApi (addon code 2002).</summary>
public sealed class WiredVariablesWebApiAddonForm : WiredForm
{
    internal WiredVariablesWebApiAddonForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets allow mass deletion. 0 false, 1 true; incoming value must equal 1</summary>
    /// <remarks>Client label: wiredfurni.params.web_api.permissions.bulk_delete. None</remarks>
    public bool AllowMassDeletion
    {
        get => GetField("allow_mass_deletion").Boolean.GetValueOrDefault();
        set => SetField("allow_mass_deletion", new(Boolean: value));
    }

    /// <summary>Gets or sets read key. Read API key; first tab-separated component</summary>
    /// <remarks>Client label: wiredfurni.params.web_api.read.title. None</remarks>
    public string ReadKey
    {
        get => GetField("read_key").Text!;
        set => SetField("read_key", new(Text: value));
    }

    /// <summary>Gets or sets write key. Write API key; second tab-separated component</summary>
    /// <remarks>Client label: wiredfurni.params.web_api.write.title. None</remarks>
    public string WriteKey
    {
        get => GetField("write_key").Text!;
        set => SetField("write_key", new(Text: value));
    }
}

/// <summary>Typed settings for Furniture (variable code 0).</summary>
public sealed class WiredFurnitureVariableForm : WiredForm
{
    internal WiredFurnitureVariableForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets has value. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variables.settings.has_value. None</remarks>
    public bool HasValue
    {
        get => GetField("has_value").Boolean.GetValueOrDefault();
        set => SetField("has_value", new(Boolean: value));
    }

    /// <summary>Gets or sets availability. 1 while room active, 10 permanent</summary>
    /// <remarks>Client label: wiredfurni.params.variables.availability. None</remarks>
    public WiredAvailabilityOption Availability
    {
        get => (WiredAvailabilityOption)GetField("availability").Integer.GetValueOrDefault();
        set => SetField("availability", new(Integer: (int)value));
    }

    /// <summary>Gets or sets name. Maximum 40 characters; spaces become underscores and name is lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_name. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }
}

/// <summary>Typed settings for UserVariable (variable code 1).</summary>
public sealed class WiredUserVariableForm : WiredForm
{
    internal WiredUserVariableForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets availability. 0 while user in room, 10 permanent, 11 permanent shared across rooms</summary>
    /// <remarks>Client label: wiredfurni.params.variables.availability. None</remarks>
    public WiredUserVariableAvailabilityOption Availability
    {
        get => (WiredUserVariableAvailabilityOption)GetField("availability").Integer.GetValueOrDefault();
        set => SetField("availability", new(Integer: (int)value));
    }

    /// <summary>Gets or sets has value. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variables.settings.has_value. None</remarks>
    public bool HasValue
    {
        get => GetField("has_value").Boolean.GetValueOrDefault();
        set => SetField("has_value", new(Boolean: value));
    }

    /// <summary>Gets or sets name. Maximum 40 characters; spaces become underscores and name is lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_name. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }
}

/// <summary>Typed settings for GlobalVariable (variable code 2).</summary>
public sealed class WiredGlobalVariableForm : WiredForm
{
    internal WiredGlobalVariableForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets availability. 1 while room active, 10 permanent, 11 permanent shared across rooms</summary>
    /// <remarks>Client label: wiredfurni.params.variables.availability. None</remarks>
    public WiredGlobalVariableAvailabilityOption Availability
    {
        get => (WiredGlobalVariableAvailabilityOption)GetField("availability").Integer.GetValueOrDefault();
        set => SetField("availability", new(Integer: (int)value));
    }

    /// <summary>Gets or sets name. Maximum 40 characters; spaces become underscores and name is lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_name. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }
}

/// <summary>Typed settings for ContextVariable (variable code 3).</summary>
public sealed class WiredContextVariableForm : WiredForm
{
    internal WiredContextVariableForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets has value. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variables.settings.has_value. None</remarks>
    public bool HasValue
    {
        get => GetField("has_value").Boolean.GetValueOrDefault();
        set => SetField("has_value", new(Boolean: value));
    }

    /// <summary>Gets or sets name. Maximum 40 characters; spaces become underscores and name is lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_name. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }
}

/// <summary>Typed settings for ReferenceVariable (variable code 4).</summary>
public sealed class WiredReferenceVariableForm : WiredForm
{
    internal WiredReferenceVariableForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets read only. 0 false, nonzero true on open; saves 0/1</summary>
    /// <remarks>Client label: wiredfurni.params.variables.settings.read_only. None</remarks>
    public bool ReadOnly
    {
        get => GetField("read_only").Boolean.GetValueOrDefault();
        set => SetField("read_only", new(Boolean: value));
    }

    /// <summary>Gets or sets name. Maximum 40 characters; spaces become underscores and name is lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_name. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }

    /// <summary>Gets or sets reference variable. Shared variable ID; n if list absent or selection invalid</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_ref_selection. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }
}

/// <summary>Typed settings for Quest (variable code 5).</summary>
public sealed class WiredQuestVariableForm : WiredForm
{
    internal WiredQuestVariableForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets name. Maximum 40 characters; spaces become underscores and name is lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_name. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }

    /// <summary>Gets or sets code. Text, maximum 500 characters</summary>
    /// <remarks>Client label: wiredfurni.params.variables.quest_name. None</remarks>
    public string Code
    {
        get => GetField("code").Text!;
        set => SetField("code", new(Text: value));
    }
}

/// <summary>Typed settings for QuestChain (variable code 6).</summary>
public sealed class WiredQuestChainVariableForm : WiredForm
{
    internal WiredQuestChainVariableForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets name. Maximum 40 characters; spaces become underscores and name is lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_name. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }

    /// <summary>Gets or sets code. Text, maximum 500 characters</summary>
    /// <remarks>Client label: wiredfurni.params.variables.quest_chain_name. None</remarks>
    public string Code
    {
        get => GetField("code").Text!;
        set => SetField("code", new(Text: value));
    }
}

/// <summary>Typed settings for EchoVariable (variable code 7).</summary>
public sealed class WiredEchoVariableForm : WiredForm
{
    internal WiredEchoVariableForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets name. Maximum 40 characters; spaces become underscores and name is lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_name. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }

    /// <summary>Gets or sets reference variable. Requires variableType != 0; n means no variable</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_selection. None</remarks>
    public string ReferenceVariable
    {
        get => GetField("reference_variable").Text!;
        set => SetField("reference_variable", new(Text: value));
    }
}

/// <summary>Typed settings for DailyTask (variable code 8).</summary>
public sealed class WiredDailyTaskVariableForm : WiredForm
{
    internal WiredDailyTaskVariableForm(WiredConfig configuration, WiredFormLayout layout) : base(configuration, layout) { }

    /// <summary>Gets or sets name. Maximum 40 characters; spaces become underscores and name is lowercased</summary>
    /// <remarks>Client label: wiredfurni.params.variables.variable_name. None</remarks>
    public string Name
    {
        get => GetField("name").Text!;
        set => SetField("name", new(Text: value));
    }

    /// <summary>Gets or sets code. Text, maximum 100 characters</summary>
    /// <remarks>Client label: wiredfurni.params.variables.daily_task_name. None</remarks>
    public string Code
    {
        get => GetField("code").Text!;
        set => SetField("code", new(Text: value));
    }
}
