namespace Qx.Model.Wired;

/// <summary>0 contains keyword; 1 exact match; 2 all text</summary>
public enum WiredMatchModeOption
{
    /// <summary>ContainsKeyword.</summary>
    ContainsKeyword = 0,
    /// <summary>ExactMatch.</summary>
    ExactMatch = 1,
    /// <summary>AllText.</summary>
    AllText = 2
}

/// <summary>0 any, 1 red, 2 green, 3 blue, 4 yellow</summary>
public enum WiredTeamOption
{
    /// <summary>Any.</summary>
    Any = 0,
    /// <summary>Red.</summary>
    Red = 1,
    /// <summary>Green.</summary>
    Green = 2,
    /// <summary>Blue.</summary>
    Blue = 3,
    /// <summary>Yellow.</summary>
    Yellow = 4
}

/// <summary>0 wave, 1 blow, 2 laugh, 3 respect, 4 awake, 5 sleep, 6 sit, 7 stand, 8 lay, 10 sign, 11 dance, 67 client action named 67</summary>
public enum WiredActionOption
{
    /// <summary>Wave.</summary>
    Wave = 0,
    /// <summary>Blow.</summary>
    Blow = 1,
    /// <summary>Laugh.</summary>
    Laugh = 2,
    /// <summary>Respect.</summary>
    Respect = 3,
    /// <summary>Awake.</summary>
    Awake = 4,
    /// <summary>Sleep.</summary>
    Sleep = 5,
    /// <summary>Sit.</summary>
    Sit = 6,
    /// <summary>Stand.</summary>
    Stand = 7,
    /// <summary>Lay.</summary>
    Lay = 8,
    /// <summary>Sign.</summary>
    Sign = 10,
    /// <summary>Dance.</summary>
    Dance = 11,
    /// <summary>Action67.</summary>
    Action67 = 67
}

/// <summary>0 all states (state_trigger.0), 1 current state (state_trigger.1)</summary>
public enum WiredStateModeOption
{
    /// <summary>AllStates.</summary>
    AllStates = 0,
    /// <summary>CurrentState.</summary>
    CurrentState = 1
}

/// <summary>Bit 0 increased, bit 1 decreased, bit 2 unchanged</summary>
[Flags]
public enum WiredChangeMaskFlags
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>Increased.</summary>
    Increased = 1,
    /// <summary>Decreased.</summary>
    Decreased = 2,
    /// <summary>Unchanged.</summary>
    Unchanged = 4
}

/// <summary>Bit 0 this room, bit 1 another room, bit 2 inspection, bit 3 external; -1 when every enabled option is selected</summary>
[Flags]
public enum WiredOriginMaskFlags
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>ThisRoom.</summary>
    ThisRoom = 1,
    /// <summary>AnotherRoom.</summary>
    AnotherRoom = 2,
    /// <summary>Inspection.</summary>
    Inspection = 4,
    /// <summary>External.</summary>
    External = 8,
    /// <summary>All.</summary>
    All = -1
}

/// <summary>0 next state, 1 previous state</summary>
public enum WiredToggleTypeOption
{
    /// <summary>NextState.</summary>
    NextState = 0,
    /// <summary>PreviousState.</summary>
    PreviousState = 1
}

/// <summary>0 none; 4 upper-right; 8 right; 5 lower-right; 9 down; 6 lower-left; 10 left; 7 upper-left; 11 up; 2 either upper-left/lower-right; 3 either upper-right/lower-left; 1 random</summary>
public enum WiredMovementOption
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>UpperRight.</summary>
    UpperRight = 4,
    /// <summary>Right.</summary>
    Right = 8,
    /// <summary>LowerRight.</summary>
    LowerRight = 5,
    /// <summary>Down.</summary>
    Down = 9,
    /// <summary>LowerLeft.</summary>
    LowerLeft = 6,
    /// <summary>Left.</summary>
    Left = 10,
    /// <summary>UpperLeft.</summary>
    UpperLeft = 7,
    /// <summary>Up.</summary>
    Up = 11,
    /// <summary>EitherUpperLeftLowerRight.</summary>
    EitherUpperLeftLowerRight = 2,
    /// <summary>EitherUpperRightLowerLeft.</summary>
    EitherUpperRightLowerLeft = 3,
    /// <summary>Random.</summary>
    Random = 1
}

/// <summary>0 none, 1 clockwise, 2 counterclockwise, 3 random</summary>
public enum WiredRotationOption
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>Clockwise.</summary>
    Clockwise = 1,
    /// <summary>Counterclockwise.</summary>
    Counterclockwise = 2,
    /// <summary>Random.</summary>
    Random = 3
}

/// <summary>0 only the user, 1 everyone</summary>
public enum WiredVisibilityOption
{
    /// <summary>OnlyTheUser.</summary>
    OnlyTheUser = 0,
    /// <summary>Everyone.</summary>
    Everyone = 1
}

/// <summary>-1 user&#x27;s preference, 0 wide, 1 normal, 2 thin</summary>
public enum WiredBubbleWidthOption
{
    /// <summary>UserPreference.</summary>
    UserPreference = -1,
    /// <summary>Wide.</summary>
    Wide = 0,
    /// <summary>Normal.</summary>
    Normal = 1,
    /// <summary>Thin.</summary>
    Thin = 2
}

/// <summary>1 red, 2 green, 3 blue, 4 yellow</summary>
public enum WiredJoinTeamActionTeamOption
{
    /// <summary>Red.</summary>
    Red = 1,
    /// <summary>Green.</summary>
    Green = 2,
    /// <summary>Blue.</summary>
    Blue = 3,
    /// <summary>Yellow.</summary>
    Yellow = 4
}

/// <summary>0 Wired, 1 Battle Banzai, 2 Freeze</summary>
public enum WiredGameTypeOption
{
    /// <summary>Wired.</summary>
    Wired = 0,
    /// <summary>BattleBanzai.</summary>
    BattleBanzai = 1,
    /// <summary>Freeze.</summary>
    Freeze = 2
}

/// <summary>0 upper-right, 1 right, 2 lower-right, 3 down, 4 lower-left, 5 left, 6 upper-left, 7 up (screen directions from embedded move_0..7 icons)</summary>
public enum WiredDirectionOption
{
    /// <summary>UpperRight.</summary>
    UpperRight = 0,
    /// <summary>Right.</summary>
    Right = 1,
    /// <summary>LowerRight.</summary>
    LowerRight = 2,
    /// <summary>Down.</summary>
    Down = 3,
    /// <summary>LowerLeft.</summary>
    LowerLeft = 4,
    /// <summary>Left.</summary>
    Left = 5,
    /// <summary>UpperLeft.</summary>
    UpperLeft = 6,
    /// <summary>Up.</summary>
    Up = 7
}

/// <summary>0 once, 1 every N days, 2 every N hours, 3 every N minutes</summary>
public enum WiredIntervalTypeOption
{
    /// <summary>Once.</summary>
    Once = 0,
    /// <summary>EveryNDays.</summary>
    EveryNDays = 1,
    /// <summary>EveryNHours.</summary>
    EveryNHours = 2,
    /// <summary>EveryNMinutes.</summary>
    EveryNMinutes = 3
}

/// <summary>0 talk, 1 shout</summary>
public enum WiredChatModeOption
{
    /// <summary>Talk.</summary>
    Talk = 0,
    /// <summary>Shout.</summary>
    Shout = 1
}

/// <summary>0 stop, 1 start</summary>
public enum WiredFollowingOption
{
    /// <summary>Stop.</summary>
    Stop = 0,
    /// <summary>Start.</summary>
    Start = 1
}

/// <summary>0 talk, 1 whisper</summary>
public enum WiredBotTalkDirectToAvtrActionChatModeOption
{
    /// <summary>Talk.</summary>
    Talk = 0,
    /// <summary>Whisper.</summary>
    Whisper = 1
}

/// <summary>0 start, 1 stop, 2 reset, 3 pause, 4 resume</summary>
public enum WiredClockControlOption
{
    /// <summary>Start.</summary>
    Start = 0,
    /// <summary>Stop.</summary>
    Stop = 1,
    /// <summary>Reset.</summary>
    Reset = 2,
    /// <summary>Pause.</summary>
    Pause = 3,
    /// <summary>Resume.</summary>
    Resume = 4
}

/// <summary>0 increase, 1 decrease, 2 set value</summary>
public enum WiredOperationOption
{
    /// <summary>Increase.</summary>
    Increase = 0,
    /// <summary>Decrease.</summary>
    Decrease = 1,
    /// <summary>SetValue.</summary>
    SetValue = 2
}

/// <summary>0 assign, 1 add, 2 subtract, 3 multiply, 4 divide, 5 power, 6 modulo, 40 minimum, 41 maximum, 50 random upper bound, 60 absolute, 100 AND, 101 OR, 102 XOR, 103 NOT, 104 left shift, 105 right shift, 110 bit count; 111..122 also offered but absent from dated localization</summary>
public enum WiredChangeVariableActionOperationOption
{
    /// <summary>Assign.</summary>
    Assign = 0,
    /// <summary>Add.</summary>
    Add = 1,
    /// <summary>Subtract.</summary>
    Subtract = 2,
    /// <summary>Multiply.</summary>
    Multiply = 3,
    /// <summary>Divide.</summary>
    Divide = 4,
    /// <summary>Power.</summary>
    Power = 5,
    /// <summary>Modulo.</summary>
    Modulo = 6,
    /// <summary>Minimum.</summary>
    Minimum = 40,
    /// <summary>Maximum.</summary>
    Maximum = 41,
    /// <summary>RandomUpperBound.</summary>
    RandomUpperBound = 50,
    /// <summary>Absolute.</summary>
    Absolute = 60,
    /// <summary>And.</summary>
    And = 100,
    /// <summary>Or.</summary>
    Or = 101,
    /// <summary>Xor.</summary>
    Xor = 102,
    /// <summary>Not.</summary>
    Not = 103,
    /// <summary>LeftShift.</summary>
    LeftShift = 104,
    /// <summary>RightShift.</summary>
    RightShift = 105,
    /// <summary>BitCount.</summary>
    BitCount = 110
}

/// <summary>-1 none; 0 upper-right, 1 right, 2 lower-right, 3 down, 4 lower-left, 5 left, 6 upper-left, 7 up (screen directions from embedded move_0..7 icons)</summary>
public enum WiredMoveUserActionMovementOption
{
    /// <summary>None.</summary>
    None = -1,
    /// <summary>UpperRight.</summary>
    UpperRight = 0,
    /// <summary>Right.</summary>
    Right = 1,
    /// <summary>LowerRight.</summary>
    LowerRight = 2,
    /// <summary>Down.</summary>
    Down = 3,
    /// <summary>LowerLeft.</summary>
    LowerLeft = 4,
    /// <summary>Left.</summary>
    Left = 5,
    /// <summary>UpperLeft.</summary>
    UpperLeft = 6,
    /// <summary>Up.</summary>
    Up = 7
}

/// <summary>-1 none; 0..7 use corresponding move icons; 9 clockwise, 10 counterclockwise</summary>
public enum WiredMoveUserActionRotationOption
{
    /// <summary>None.</summary>
    None = -1,
    /// <summary>UpperRight.</summary>
    UpperRight = 0,
    /// <summary>Right.</summary>
    Right = 1,
    /// <summary>LowerRight.</summary>
    LowerRight = 2,
    /// <summary>Down.</summary>
    Down = 3,
    /// <summary>LowerLeft.</summary>
    LowerLeft = 4,
    /// <summary>Left.</summary>
    Left = 5,
    /// <summary>UpperLeft.</summary>
    UpperLeft = 6,
    /// <summary>Up.</summary>
    Up = 7,
    /// <summary>Clockwise.</summary>
    Clockwise = 9,
    /// <summary>Counterclockwise.</summary>
    Counterclockwise = 10
}

/// <summary>0 keep walking if moved closer to target, 1 keep walking, 2 stop walking</summary>
public enum WiredWalkingModeOption
{
    /// <summary>KeepWalkingIfMovedCloserToTarget.</summary>
    KeepWalkingIfMovedCloserToTarget = 0,
    /// <summary>KeepWalking.</summary>
    KeepWalking = 1,
    /// <summary>StopWalking.</summary>
    StopWalking = 2
}

/// <summary>0 specified amount, 1 all</summary>
public enum WiredRewardingModeOption
{
    /// <summary>SpecifiedAmount.</summary>
    SpecifiedAmount = 0,
    /// <summary>All.</summary>
    All = 1
}

/// <summary>0 random, 1 first in first out, 2 last in first out</summary>
public enum WiredIterationModeOption
{
    /// <summary>Random.</summary>
    Random = 0,
    /// <summary>FirstInFirstOut.</summary>
    FirstInFirstOut = 1,
    /// <summary>LastInFirstOut.</summary>
    LastInFirstOut = 2
}

/// <summary>0 normal, 1 multiplier, 2 auto-multiplier</summary>
public enum WiredTransactionModeOption
{
    /// <summary>Normal.</summary>
    Normal = 0,
    /// <summary>Multiplier.</summary>
    Multiplier = 1,
    /// <summary>AutoMultiplier.</summary>
    AutoMultiplier = 2
}

/// <summary>0 specified contract, 1 any ongoing transaction</summary>
public enum WiredMatchCriteriaOption
{
    /// <summary>SpecifiedContract.</summary>
    SpecifiedContract = 0,
    /// <summary>AnyOngoingTransaction.</summary>
    AnyOngoingTransaction = 1
}

/// <summary>0 set progress, 1 add to existing progress</summary>
public enum WiredProgressionModeOption
{
    /// <summary>SetProgress.</summary>
    SetProgress = 0,
    /// <summary>AddToExistingProgress.</summary>
    AddToExistingProgress = 1
}

/// <summary>0 default, 1 click user but walk behind, 2 pass through user</summary>
public enum WiredUserClickOption
{
    /// <summary>Default.</summary>
    Default = 0,
    /// <summary>ClickUserButWalkBehind.</summary>
    ClickUserButWalkBehind = 1,
    /// <summary>PassThroughUser.</summary>
    PassThroughUser = 2
}

/// <summary>0 default, 1 pass through furniture</summary>
public enum WiredFurnitureClickOption
{
    /// <summary>Default.</summary>
    Default = 0,
    /// <summary>PassThroughFurniture.</summary>
    PassThroughFurniture = 1
}

/// <summary>0 source location, 1 custom location</summary>
public enum WiredLocationModeOption
{
    /// <summary>SourceLocation.</summary>
    SourceLocation = 0,
    /// <summary>CustomLocation.</summary>
    CustomLocation = 1
}

/// <summary>0 on top of target location, 1 source altitude, 2 custom altitude</summary>
public enum WiredAltitudeModeOption
{
    /// <summary>OnTopOfTargetLocation.</summary>
    OnTopOfTargetLocation = 0,
    /// <summary>SourceAltitude.</summary>
    SourceAltitude = 1,
    /// <summary>CustomAltitude.</summary>
    CustomAltitude = 2
}

/// <summary>0 any selected furniture has an avatar (requireall.2), 1 all selected furniture have avatars (requireall.3)</summary>
public enum WiredRequireAllOption
{
    /// <summary>AnySelectedFurnitureHasAnAvatar.</summary>
    AnySelectedFurnitureHasAnAvatar = 0,
    /// <summary>AllSelectedFurnitureHaveAvatars.</summary>
    AllSelectedFurnitureHaveAvatars = 1
}

/// <summary>1 any selected furniture has no avatar (not_requireall.2), 0 all selected furniture have no avatars (not_requireall.3)</summary>
public enum WiredRequireAnyWithoutAvatarOption
{
    /// <summary>AnySelectedFurnitureHasNoAvatar.</summary>
    AnySelectedFurnitureHasNoAvatar = 1,
    /// <summary>AllSelectedFurnitureHaveNoAvatars.</summary>
    AllSelectedFurnitureHaveNoAvatars = 0
}

/// <summary>0 any team, 1 red, 2 green, 3 blue, 4 yellow</summary>
public enum WiredActorIsInTeamConditionTeamOption
{
    /// <summary>AnyTeam.</summary>
    AnyTeam = 0,
    /// <summary>Red.</summary>
    Red = 1,
    /// <summary>Green.</summary>
    Green = 2,
    /// <summary>Blue.</summary>
    Blue = 3,
    /// <summary>Yellow.</summary>
    Yellow = 4
}

/// <summary>0 any selected furniture has furniture on it (requireall.0), 1 all selected furniture have furniture on them (requireall.1)</summary>
public enum WiredHasStackedFurnisConditionRequireAllOption
{
    /// <summary>AnySelectedFurnitureHasFurnitureOnIt.</summary>
    AnySelectedFurnitureHasFurnitureOnIt = 0,
    /// <summary>AllSelectedFurnitureHaveFurnitureOnThem.</summary>
    AllSelectedFurnitureHaveFurnitureOnThem = 1
}

/// <summary>0 one or more selected furniture have no furniture on them (not_requireall.0), 1 all selected furniture have none (not_requireall.1)</summary>
public enum WiredNotHasStackedFurnisConditionRequireAllOption
{
    /// <summary>OneOrMoreSelectedFurnitureHaveNoFurnitureOnThem.</summary>
    OneOrMoreSelectedFurnitureHaveNoFurnitureOnThem = 0,
    /// <summary>AllSelectedFurnitureHaveNone.</summary>
    AllSelectedFurnitureHaveNone = 1
}

/// <summary>1 Habbo, 2 pet, 4 bot</summary>
public enum WiredUserTypeOption
{
    /// <summary>Habbo.</summary>
    Habbo = 1,
    /// <summary>Pet.</summary>
    Pet = 2,
    /// <summary>Bot.</summary>
    Bot = 4
}

/// <summary>Bits 0..6 Monday..Sunday; time.weekday.1..7</summary>
[Flags]
public enum WiredWeekdayMaskFlags
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>Monday.</summary>
    Monday = 1,
    /// <summary>Tuesday.</summary>
    Tuesday = 2,
    /// <summary>Wednesday.</summary>
    Wednesday = 4,
    /// <summary>Thursday.</summary>
    Thursday = 8,
    /// <summary>Friday.</summary>
    Friday = 16,
    /// <summary>Saturday.</summary>
    Saturday = 32,
    /// <summary>Sunday.</summary>
    Sunday = 64
}

/// <summary>Bits 0..11 January..December; time.month.1..12</summary>
[Flags]
public enum WiredMonthMaskFlags
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>January.</summary>
    January = 1,
    /// <summary>February.</summary>
    February = 2,
    /// <summary>March.</summary>
    March = 4,
    /// <summary>April.</summary>
    April = 8,
    /// <summary>May.</summary>
    May = 16,
    /// <summary>June.</summary>
    June = 32,
    /// <summary>July.</summary>
    July = 64,
    /// <summary>August.</summary>
    August = 128,
    /// <summary>September.</summary>
    September = 256,
    /// <summary>October.</summary>
    October = 512,
    /// <summary>November.</summary>
    November = 1024,
    /// <summary>December.</summary>
    December = 2048
}

/// <summary>0 triggerer&#x27;s team (team.triggerer), 1 red, 2 green, 3 blue, 4 yellow</summary>
public enum WiredTeamIsWinningConditionTeamOption
{
    /// <summary>TriggererTeam.</summary>
    TriggererTeam = 0,
    /// <summary>Red.</summary>
    Red = 1,
    /// <summary>Green.</summary>
    Green = 2,
    /// <summary>Blue.</summary>
    Blue = 3,
    /// <summary>Yellow.</summary>
    Yellow = 4
}

/// <summary>0 first, 1 second, 2 third, 3 fourth; placement.1..4</summary>
public enum WiredPlacementOption
{
    /// <summary>First.</summary>
    First = 0,
    /// <summary>Second.</summary>
    Second = 1,
    /// <summary>Third.</summary>
    Third = 2,
    /// <summary>Fourth.</summary>
    Fourth = 3
}

/// <summary>0 &lt;, 1 =, 2 &gt;</summary>
public enum WiredComparisonOption
{
    /// <summary>LessThan.</summary>
    LessThan = 0,
    /// <summary>Equal.</summary>
    Equal = 1,
    /// <summary>GreaterThan.</summary>
    GreaterThan = 2
}

/// <summary>Bits 0..7 correspond to move_0..move_7 icons; other bits are dropped</summary>
[Flags]
public enum WiredDirectionMaskFlags
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>UpperRight.</summary>
    UpperRight = 1,
    /// <summary>Right.</summary>
    Right = 2,
    /// <summary>LowerRight.</summary>
    LowerRight = 4,
    /// <summary>Down.</summary>
    Down = 8,
    /// <summary>LowerLeft.</summary>
    LowerLeft = 16,
    /// <summary>Left.</summary>
    Left = 32,
    /// <summary>UpperLeft.</summary>
    UpperLeft = 64,
    /// <summary>Up.</summary>
    Up = 128
}

/// <summary>0 &lt;, 1 =, 2 &gt;, 3 &lt;=, 4 !=, 5 &gt;=; literal symbol labels</summary>
public enum WiredVariableValueConditionComparisonOption
{
    /// <summary>LessThan.</summary>
    LessThan = 0,
    /// <summary>Equal.</summary>
    Equal = 1,
    /// <summary>GreaterThan.</summary>
    GreaterThan = 2,
    /// <summary>LessThanOrEqual.</summary>
    LessThanOrEqual = 3,
    /// <summary>NotEqual.</summary>
    NotEqual = 4,
    /// <summary>GreaterThanOrEqual.</summary>
    GreaterThanOrEqual = 5
}

/// <summary>0 lower than, 2 higher than; no equals option</summary>
public enum WiredVariableAgeConditionComparisonOption
{
    /// <summary>LowerThan.</summary>
    LowerThan = 0,
    /// <summary>HigherThan.</summary>
    HigherThan = 2
}

/// <summary>0 creation time, 1 last update time</summary>
public enum WiredAgeKindOption
{
    /// <summary>CreationTime.</summary>
    CreationTime = 0,
    /// <summary>LastUpdateTime.</summary>
    LastUpdateTime = 1
}

/// <summary>0 milliseconds, 1 seconds, 2 minutes, 3 hours, 4 days, 5 weeks, 6 months, 7 years</summary>
public enum WiredDurationUnitOption
{
    /// <summary>Milliseconds.</summary>
    Milliseconds = 0,
    /// <summary>Seconds.</summary>
    Seconds = 1,
    /// <summary>Minutes.</summary>
    Minutes = 2,
    /// <summary>Hours.</summary>
    Hours = 3,
    /// <summary>Days.</summary>
    Days = 4,
    /// <summary>Weeks.</summary>
    Weeks = 5,
    /// <summary>Months.</summary>
    Months = 6,
    /// <summary>Years.</summary>
    Years = 7
}

/// <summary>0 above, 1 below, 2 same height, 3 all furniture on tile; labels onfurni.N</summary>
public enum WiredRelationOption
{
    /// <summary>Above.</summary>
    Above = 0,
    /// <summary>Below.</summary>
    Below = 1,
    /// <summary>SameHeight.</summary>
    SameHeight = 2,
    /// <summary>AllFurnitureOnTile.</summary>
    AllFurnitureOnTile = 3
}

/// <summary>0 lower than, 1 equals, 2 higher than</summary>
public enum WiredFurniWithAltitudeSelectorComparisonOption
{
    /// <summary>LowerThan.</summary>
    LowerThan = 0,
    /// <summary>Equals.</summary>
    Equals = 1,
    /// <summary>HigherThan.</summary>
    HigherThan = 2
}

/// <summary>0 variable presence only, 1 constant comparison, 2 variable comparison</summary>
public enum WiredValueFilterOption
{
    /// <summary>VariablePresenceOnly.</summary>
    VariablePresenceOnly = 0,
    /// <summary>ConstantComparison.</summary>
    ConstantComparison = 1,
    /// <summary>VariableComparison.</summary>
    VariableComparison = 2
}

/// <summary>0 union, 1 intersection</summary>
public enum WiredSetOperationOption
{
    /// <summary>Union.</summary>
    Union = 0,
    /// <summary>Intersection.</summary>
    Intersection = 1
}

/// <summary>0 all, 1 at least one, 2 not all, 3 none, -1 numeric comparison</summary>
public enum WiredModeOption
{
    /// <summary>All.</summary>
    All = 0,
    /// <summary>AtLeastOne.</summary>
    AtLeastOne = 1,
    /// <summary>NotAll.</summary>
    NotAll = 2,
    /// <summary>None.</summary>
    None = 3,
    /// <summary>NumericComparison.</summary>
    NumericComparison = -1
}

/// <summary>0 less than, 1 exactly, 2 more than; used only with mode -1</summary>
public enum WiredConditionEvaluationAddonComparisonOption
{
    /// <summary>LessThan.</summary>
    LessThan = 0,
    /// <summary>Exactly.</summary>
    Exactly = 1,
    /// <summary>MoreThan.</summary>
    MoreThan = 2
}

/// <summary>0 standing directly on moving furniture, 1 standing on same tile</summary>
public enum WiredCarryModeOption
{
    /// <summary>StandingDirectlyOnMovingFurniture.</summary>
    StandingDirectlyOnMovingFurniture = 0,
    /// <summary>StandingOnSameTile.</summary>
    StandingOnSameTile = 1
}

/// <summary>0 highest value, 1 lowest value, 2 oldest creation, 3 latest creation, 4 oldest update, 5 latest update; variables.sort_by.0..5</summary>
public enum WiredSortOrderOption
{
    /// <summary>HighestValue.</summary>
    HighestValue = 0,
    /// <summary>LowestValue.</summary>
    LowestValue = 1,
    /// <summary>OldestCreation.</summary>
    OldestCreation = 2,
    /// <summary>LatestCreation.</summary>
    LatestCreation = 3,
    /// <summary>OldestUpdate.</summary>
    OldestUpdate = 4,
    /// <summary>LatestUpdate.</summary>
    LatestUpdate = 5
}

/// <summary>0 all items in chest, 1 only previewed items</summary>
public enum WiredScanningModeOption
{
    /// <summary>AllItemsInChest.</summary>
    AllItemsInChest = 0,
    /// <summary>OnlyPreviewedItems.</summary>
    OnlyPreviewedItems = 1
}

/// <summary>0 credits, 1 furniture</summary>
public enum WiredPaymentTypeOption
{
    /// <summary>Credits.</summary>
    Credits = 0,
    /// <summary>Furniture.</summary>
    Furniture = 1
}

/// <summary>0 eight directions straight, 1 eight directions diffuse, 2 four directions option 1, 3 four directions option 2</summary>
public enum WiredDirectionSystemOption
{
    /// <summary>EightDirectionsStraight.</summary>
    EightDirectionsStraight = 0,
    /// <summary>EightDirectionsDiffuse.</summary>
    EightDirectionsDiffuse = 1,
    /// <summary>FourDirectionsOption1.</summary>
    FourDirectionsOption1 = 2,
    /// <summary>FourDirectionsOption2.</summary>
    FourDirectionsOption2 = 3
}

/// <summary>Bits 0 tiles_travelled, 1 user_collisions, 2 furni_collisions, 3 position.x, 4 position.y, 5 position.altitude, 6 is_travelling; names prefixed animation.</summary>
[Flags]
public enum WiredInternalVariablesFlags
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>TilesTravelled.</summary>
    TilesTravelled = 1,
    /// <summary>UserCollisions.</summary>
    UserCollisions = 2,
    /// <summary>FurnitureCollisions.</summary>
    FurnitureCollisions = 4,
    /// <summary>PositionX.</summary>
    PositionX = 8,
    /// <summary>PositionY.</summary>
    PositionY = 16,
    /// <summary>Altitude.</summary>
    Altitude = 32,
    /// <summary>IsTravelling.</summary>
    IsTravelling = 64
}

/// <summary>0 normal start-to-target, 1 overshoot X tiles, 2 always shoot X tiles</summary>
public enum WiredDistanceModeOption
{
    /// <summary>NormalStartToTarget.</summary>
    NormalStartToTarget = 0,
    /// <summary>OvershootXTiles.</summary>
    OvershootXTiles = 1,
    /// <summary>AlwaysShootXTiles.</summary>
    AlwaysShootXTiles = 2
}

/// <summary>Bits 0 current_level, 1 current_xp, 2 progress, 3 progress_percentage, 4 xp_required, 5 xp_remaining, 6 is_maxed, 7 max_level</summary>
[Flags]
public enum WiredSubvariableMaskFlags
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>CurrentLevel.</summary>
    CurrentLevel = 1,
    /// <summary>CurrentXp.</summary>
    CurrentXp = 2,
    /// <summary>Progress.</summary>
    Progress = 4,
    /// <summary>ProgressPercentage.</summary>
    ProgressPercentage = 8,
    /// <summary>XpRequired.</summary>
    XpRequired = 16,
    /// <summary>XpRemaining.</summary>
    XpRemaining = 32,
    /// <summary>IsMaxed.</summary>
    IsMaxed = 64,
    /// <summary>MaxLevel.</summary>
    MaxLevel = 128
}

/// <summary>0 manual interpolation, 1 linear, 2 exponential</summary>
public enum WiredVariableLevelUpAddonModeOption
{
    /// <summary>ManualInterpolation.</summary>
    ManualInterpolation = 0,
    /// <summary>Linear.</summary>
    Linear = 1,
    /// <summary>Exponential.</summary>
    Exponential = 2
}

/// <summary>Bits 1 milliseconds_of_seconds, 2 seconds_of_minute, 3 minute_of_hour, 4 hour_of_day, 5 day_of_week, 6 day_of_month, 7 day_of_year, 8 week_of_year, 9 month_of_year, 10 year; 20 millisecond, 21 second, 22 minute, 23 hour, 24 day, 25 week, 26 month</summary>
[Flags]
public enum WiredVariableTimeUtilAddonSubvariableMaskFlags
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>MillisecondsOfSecond.</summary>
    MillisecondsOfSecond = 2,
    /// <summary>SecondsOfMinute.</summary>
    SecondsOfMinute = 4,
    /// <summary>MinuteOfHour.</summary>
    MinuteOfHour = 8,
    /// <summary>HourOfDay.</summary>
    HourOfDay = 16,
    /// <summary>DayOfWeek.</summary>
    DayOfWeek = 32,
    /// <summary>DayOfMonth.</summary>
    DayOfMonth = 64,
    /// <summary>DayOfYear.</summary>
    DayOfYear = 128,
    /// <summary>WeekOfYear.</summary>
    WeekOfYear = 256,
    /// <summary>MonthOfYear.</summary>
    MonthOfYear = 512,
    /// <summary>Year.</summary>
    Year = 1024,
    /// <summary>Millisecond.</summary>
    Millisecond = 1048576,
    /// <summary>Second.</summary>
    Second = 2097152,
    /// <summary>Minute.</summary>
    Minute = 4194304,
    /// <summary>Hour.</summary>
    Hour = 8388608,
    /// <summary>Day.</summary>
    Day = 16777216,
    /// <summary>Week.</summary>
    Week = 33554432,
    /// <summary>Month.</summary>
    Month = 67108864
}

/// <summary>0 value, 1 creation time, 2 last update time; time_util.mode.0..2</summary>
public enum WiredTimeSourceOption
{
    /// <summary>Value.</summary>
    Value = 0,
    /// <summary>CreationTime.</summary>
    CreationTime = 1,
    /// <summary>LastUpdateTime.</summary>
    LastUpdateTime = 2
}

/// <summary>0 only the user, 1 same game team, 2 everyone, 3 users with variable, 4 users with variable equal to selected value</summary>
public enum WiredAudienceOption
{
    /// <summary>OnlyTheUser.</summary>
    OnlyTheUser = 0,
    /// <summary>SameGameTeam.</summary>
    SameGameTeam = 1,
    /// <summary>Everyone.</summary>
    Everyone = 2,
    /// <summary>UsersWithVariable.</summary>
    UsersWithVariable = 3,
    /// <summary>UsersWithVariableEqualToSelectedValue.</summary>
    UsersWithVariableEqualToSelectedValue = 4
}

/// <summary>0 always, 1 when variable updates, 2 on mouse hover; key show_mode.never is localized as Visible on mouse hover</summary>
public enum WiredShowModeOption
{
    /// <summary>Always.</summary>
    Always = 0,
    /// <summary>WhenVariableUpdates.</summary>
    WhenVariableUpdates = 1,
    /// <summary>OnMouseHover.</summary>
    OnMouseHover = 2
}

/// <summary>Bits 0 created, 1 increased, 2 decreased, 3 unchanged; clamped numerically to 0..15</summary>
[Flags]
public enum WiredUpdateMaskFlags
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>Created.</summary>
    Created = 1,
    /// <summary>Increased.</summary>
    Increased = 2,
    /// <summary>Decreased.</summary>
    Decreased = 4,
    /// <summary>Unchanged.</summary>
    Unchanged = 8
}

/// <summary>0 left, 1 right, 2 both sides; invalid becomes 0</summary>
public enum WiredIconAlignmentOption
{
    /// <summary>Left.</summary>
    Left = 0,
    /// <summary>Right.</summary>
    Right = 1,
    /// <summary>BothSides.</summary>
    BothSides = 2
}

/// <summary>0 fixed value, 1 reference from another room</summary>
public enum WiredGlobalPlaceholderAddonModeOption
{
    /// <summary>FixedValue.</summary>
    FixedValue = 0,
    /// <summary>ReferenceFromAnotherRoom.</summary>
    ReferenceFromAnotherRoom = 1
}

/// <summary>1 while room active, 10 permanent</summary>
public enum WiredAvailabilityOption
{
    /// <summary>WhileRoomActive.</summary>
    WhileRoomActive = 1,
    /// <summary>Permanent.</summary>
    Permanent = 10
}

/// <summary>0 while user in room, 10 permanent, 11 permanent shared across rooms</summary>
public enum WiredUserVariableAvailabilityOption
{
    /// <summary>WhileUserInRoom.</summary>
    WhileUserInRoom = 0,
    /// <summary>Permanent.</summary>
    Permanent = 10,
    /// <summary>PermanentSharedAcrossRooms.</summary>
    PermanentSharedAcrossRooms = 11
}

/// <summary>1 while room active, 10 permanent, 11 permanent shared across rooms</summary>
public enum WiredGlobalVariableAvailabilityOption
{
    /// <summary>WhileRoomActive.</summary>
    WhileRoomActive = 1,
    /// <summary>Permanent.</summary>
    Permanent = 10,
    /// <summary>PermanentSharedAcrossRooms.</summary>
    PermanentSharedAcrossRooms = 11
}
