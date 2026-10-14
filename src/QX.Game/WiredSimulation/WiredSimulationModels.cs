using Qx.Model;
using Qx.Model.Wired;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.WiredSimulation;

public enum WiredSimulationEventKind
{
    Start, Chat, ClickFurniture, UseFurniture, Walk, Enter, Leave, Signal, ClickUser,
    UserAction, StateChanged, VariableChanged, GameStarts, GameEnds, ScoreChanged,
    BotDestinationReached, BotAvatarReached, AvatarCaught, TransactionCompleted, TransactionFailed, ApiRequest, ClockReached, Hover,
    OfferTrade, AcceptTrade, ConfirmTrade, LinkMessage, LinkDataframe, RoomInitialized, VariableObserved
}

public enum WiredSimulationStatus { Idle, Running, Paused, Completed, Stopped, Faulted }
public enum WiredSimulationSeverity { Information, Warning, Uncertain, Unsupported, Error }

public sealed record WiredSimulationValue(long Value, long CreatedAt = 0, long UpdatedAt = 0, bool IsExact = true,
    bool CreationTimeKnown = true, bool UpdateTimeKnown = true)
{
    public static WiredSimulationValue FromClientDisplay(int value, long? created_at = null, long? updated_at = null) =>
        new(value, created_at ?? 0, updated_at ?? 0, value is not (int.MinValue or int.MaxValue),
            created_at.HasValue, updated_at.HasValue);
}

public sealed record WiredSimulationDerivedVariable(string Parent, long AddonId, int Index, int? TimeSource = null);

public sealed record WiredSimulationTimeUtilitiesProfile(string Timezone, DayOfWeek FirstDayOfWeek, int MinimumDaysInFirstWeek = 1);

public sealed record WiredSimulationHover(long ViewerId, WiredSourceDomain Domain, long EntityId, bool Active = true);

public sealed record WiredSimulationFxState(int ConfigId, string VariableId, WiredSourceDomain Domain,
    long EntityId, long ViewerId, long Value, long? RemainingMilliseconds = null, bool IsInitialize = true);

public sealed record WiredSimulationFxPresentation(IReadOnlyList<WiredSimulationFxState> States,
    IReadOnlyList<WiredSimulationHover> Hovers);

public sealed record WiredSimulationVariable(string Id, string Name, WiredSourceDomain Domain,
    int Availability = 1, bool HasValue = true, bool Writable = true,
    IReadOnlyDictionary<long, string>? Text = null, string? Reference = null,
    WiredSimulationDerivedVariable? Derived = null, string? TextFallback = null, long? DefinitionFurnitureId = null,
    string? DisplayName = null, bool? CanCreateAndDelete = null,
    string? PointerAccessor = null, bool? RequiresExecutionContext = null,
    IReadOnlyList<long>? ObservedHolders = null);

public sealed record WiredSimulationVariableObservation(string VariableId, long UserId,
    WiredSimulationValue? Value, int? Change = null, int? Origin = null);

public sealed record WiredSimulationStoredUserValue(long UserId, string Name, string Figure, WiredSimulationValue Value);

public sealed record WiredSimulationStoredVariable(string VariableId, IReadOnlyList<WiredSimulationStoredUserValue> Values,
    bool Complete = false);

public sealed record WiredSimulationLeaderboardView(long FurnitureId, string Title, string Description,
    WiredLeaderboardLocation Location, WiredSortOrderOption SortMode, IReadOnlyList<WiredLeaderboardColumn> Columns,
    IReadOnlyList<WiredLeaderboardChoice> Choices, string? DefaultVariableId, int PageSize, bool Published);

public sealed record WiredSimulationLeaderboardPage(WiredSimulationLeaderboardView Board, string VariableId, int Page,
    int TotalCount, IReadOnlyList<WiredLeaderboardRow> Rows);

public sealed record WiredSimulationFurnitureState(bool Walkable, double Height,
    bool Stackable = true, AvatarStance Stance = AvatarStance.Stand);

public sealed record WiredSimulationClock(long ElapsedMilliseconds = 0, bool Running = false, bool Initialized = false);

public enum WiredSimulationGamePhase { Stopped, Running, Paused }

public sealed record WiredSimulationScoreUse(long ActionId, int Count, long? UserId = null, int? Team = null);

public sealed record WiredSimulationGame(long ClockId, WiredSimulationGamePhase Phase = WiredSimulationGamePhase.Stopped,
    IReadOnlyList<WiredSimulationScoreUse>? ScoreUses = null, IReadOnlyList<long>? FiredScoreTriggers = null);

public sealed record WiredSimulationTimer(long Value, int TickOffsetMilliseconds, bool ResetPending = false);

public sealed record WiredSimulationTeleportTiming(int ArrivalMilliseconds, int EffectMilliseconds, int EffectId);

public sealed record WiredSimulationTeleportTimings(WiredSimulationTeleportTiming Normal, WiredSimulationTeleportTiming Fast);

public sealed record WiredSimulationBotTeleportClock(int TickMilliseconds, int NextTickOffsetMilliseconds);

public sealed record WiredSimulationBotTiming(WiredSimulationTeleportTiming Teleport, int StepMilliseconds, int NextStepOffsetMilliseconds,
    int FollowRefreshMilliseconds = 500, int FollowArrivalMilliseconds = 1000, int FollowRetryMilliseconds = 1000,
    int? FollowStartOffsetMilliseconds = null, int? DeliveryArrivalMilliseconds = null,
    int? DeliveryStartOffsetMilliseconds = null, WiredSimulationBotTeleportClock? TeleportClock = null);

public sealed record WiredSimulationBotFollow(long UserId, long ActionId, long Revision, bool Arrived = false, Tile? Destination = null,
    bool Stopping = false);

public sealed record WiredSimulationBotFollowChoice(long BotId, long UserId, Tile Destination);

public sealed record WiredSimulationBotDelivery(long UserId, long ActionId, long Revision, int HandItem,
    int TargetX, int TargetY, double TargetZ, Tile Destination);

public sealed record WiredSimulationBotDeliveryChoice(long BotId, long UserId, Tile Destination);

public sealed record WiredSimulationBotMovementStep(long BotId, long TargetId);

public sealed record WiredSimulationBotMovementChoice(long ActionId,
    IReadOnlyList<WiredSimulationBotMovementStep> Steps);

public sealed record WiredSimulationFurnitureTransferStep(long FurnitureId, long TargetId);

public sealed record WiredSimulationFurnitureTransferChoice(long ActionId,
    IReadOnlyList<WiredSimulationFurnitureTransferStep> Steps);

public sealed record WiredSimulationCarrierContactChoice(long ActionId, long UserId);

public sealed record WiredSimulationSpawnValueChoice(long ActionId, WiredSourceDomain Domain, long HolderId);

public sealed record WiredSimulationRandomChoice(long AddonId, IReadOnlyList<long> ActionIds);

public sealed record WiredSimulationRandomHistory(long AddonId, IReadOnlyList<IReadOnlyList<long>> Executions, bool Complete = true);

public sealed record WiredSimulationLimitHistory(long AddonId, IReadOnlyList<long> Activations, bool Complete = true, long ObservedSince = 0,
    IReadOnlyList<long>? ConditionFailures = null);

public sealed record WiredSimulationUserActivity(int? Dance = null, int? Sign = null, bool SignKnown = false,
    long? SignRemainingMilliseconds = null, long SignObservedAt = 0);

public sealed record WiredSimulationFreeze(int PreviousEffect, int EffectId, bool CancelOnTeleport);

public sealed record WiredSimulationProjectileTiming(int TickMilliseconds, int NextTickOffsetMilliseconds);

public sealed record WiredSimulationProjectileState(long ModifierId, WiredSimulationMovement Movement,
    int VariableMask, long ElapsedMilliseconds, int Cell, int X, int Y, int Altitude, bool Travelling,
    IReadOnlyList<long> FurnitureHits, IReadOnlyList<long> UserHits);

public sealed record WiredSimulationChest(bool StoresCoins, int? Coins = null,
    IReadOnlyList<WiredChestStorageSnapshot>? Items = null, bool ItemsComplete = false,
    IReadOnlyList<ChestItemType>? PreviewItems = null, bool? WiredEnabled = null, bool? Locked = null, int? Capacity = null);

public sealed record WiredSimulationTrade(bool Active, long? ContractId = null, bool ChestDeposit = false,
    int? State = null, int? Multiplier = null, WiredTradeInitiate? Initiation = null, long Revision = 0,
    IReadOnlyList<WiredSimulationReservation>? Reservations = null, bool? CanAccept = null,
    long? CreditChestId = null, long? ActionId = null, int? OfferedCredits = null, long? AcceptedAt = null);

public sealed record WiredSimulationTradeInput(long Revision, int? Credits = null);

public sealed record WiredSimulationReservation(long ChestId, IReadOnlyList<int> InventoryIds);

public sealed record WiredSimulationRoomLink(long FurnitureId, long? RoomId = null, Tile? Destination = null,
    int? DepartureMilliseconds = null, int? EffectId = null, bool? Accessible = null);

public sealed record WiredSimulationFurniture(long Id, string Key, string ClassName, int Kind, int X, int Y,
    double Z = 0, int Direction = 0, int State = 0, int Width = 1, int Length = 1,
    double Height = 0, int? StateCount = null, bool Walkable = true, bool Stackable = true,
    bool Temporary = false, bool Hidden = false,
    IReadOnlyDictionary<string, WiredSimulationValue>? Variables = null,
    IReadOnlyDictionary<int, WiredSimulationFurnitureState>? States = null,
    AvatarStance Stance = AvatarStance.Stand, int? FleeDirection = null, WiredSimulationClock? Clock = null,
    int? IntrinsicState = null, string? Name = null, IReadOnlyList<int>? Directions = null,
    WiredSimulationProjectileState? Projectile = null, WiredSimulationChest? Chest = null,
    int? RecolorRgb = null, IReadOnlyList<int>? RecolorPalette = null, WiredContractContents? Contract = null,
    WiredSimulationRoomLink? RoomLink = null, int? IntrinsicType = null);

public sealed record WiredSimulationAchievement(string Name, long Value, string? Code = null);

public sealed record WiredSimulationTrackTask(string Id, long Value, long? ResetValue = null);

public sealed record WiredSimulationRewardTrack(string Id, IReadOnlyList<WiredSimulationTrackTask> Tasks);

public sealed record WiredSimulationUser(long Id, string Name, int X, int Y, double Z = 0,
    int Direction = 0, int Team = 0, int Score = 0, int Level = 0, int Effect = 0,
    int HandItem = 0, bool IsBot = false, bool IsPet = false, bool Frozen = false,
    string Figure = "", int Action = 0, IReadOnlyList<string>? Badges = null,
    IReadOnlyList<long>? Groups = null, IReadOnlyDictionary<string, WiredSimulationValue>? Variables = null,
    AvatarStance Stance = AvatarStance.Stand, double? ActionHeight = null, Tile? NextStep = null,
    WiredSimulationFreeze? Freeze = null, int? Index = null, WiredSimulationBotFollow? Follow = null,
    WiredSimulationBotDelivery? Delivery = null, WiredClickSettings? ClickSettings = null,
    WiredSimulationTrade? Trade = null, WiredSimulationUserActivity? Activity = null,
    IReadOnlyList<long>? WiredContacts = null,
    IReadOnlyList<WiredSimulationAchievement>? Achievements = null,
    IReadOnlyList<WiredSimulationRewardTrack>? RewardTracks = null);

public sealed record WiredSimulationSavedFurniture(long Id, int? State = null, int? Direction = null,
    int? X = null, int? Y = null, double? Z = null);

public sealed record WiredSimulationNode(long FurnitureId, WiredFormCategory Category, int Code,
    IReadOnlyDictionary<string, WiredFormValue> Fields,
    IReadOnlyList<long>? Furniture = null, IReadOnlyList<long>? SecondaryFurniture = null,
    IReadOnlyList<WiredFurnitureSource>? FurnitureSources = null,
    IReadOnlyList<WiredUserSource>? UserSources = null,
    int Delay = 0, int Quantifier = 0, bool Filter = false, bool Invert = false,
    IReadOnlyList<WiredSimulationFurniture>? SavedFurniture = null,
    IReadOnlyDictionary<long, int>? MovementDirections = null, InputSourcesConf? InputSources = null,
    IReadOnlyList<WiredSimulationSavedFurniture>? SavedSnapshot = null)
{
    public static WiredSimulationNode From(WiredForm form) => new((long)form.FurniId,
        form.Definition.Category, form.DefinitionCode,
        copy_fields(form.Definition.Category, form.DefinitionCode, form.Definition.Fields.Select(field => KeyValuePair.Create(field.Name, form.GetField(field.Name)))),
        form.SelectedFurniture.Select(id => (long)id).ToArray(), form.SecondaryFurniture.Select(id => (long)id).ToArray(),
        form.FurnitureSources.ToArray(), form.UserSources.ToArray(), form.ActionDelay ?? 0,
        form.ConditionQuantifier ?? 0, form.SelectorFilter ?? false, form.SelectorInvert ?? false,
        InputSources: new(form.AllowedFurnitureSources.Select(values => (IReadOnlyList<int>)values.Select(value => (int)value).ToArray()).ToArray(),
            form.AllowedUserSources.Select(values => (IReadOnlyList<int>)values.Select(value => (int)value).ToArray()).ToArray(),
            form.DefaultFurnitureSources.Select(value => (int)value).ToArray(), form.DefaultUserSources.Select(value => (int)value).ToArray()));

    internal static IReadOnlyDictionary<string, WiredFormValue> copy_fields(WiredFormCategory category, int code,
        IEnumerable<KeyValuePair<string, WiredFormValue>> fields) => fields.ToDictionary(pair => pair.Key,
            pair => category == WiredFormCategory.Addon && code == 2002 && pair.Key is "read_key" or "write_key"
                ? new WiredFormValue(Text: "") : pair.Value, StringComparer.Ordinal);

    public long Number(string name, long fallback = 0) => Fields.TryGetValue(name, out var value)
        ? value.Integer ?? (value.Boolean is { } flag ? flag ? 1 : 0 : fallback) : fallback;
    public string Text(string name, string fallback = "") => Fields.TryGetValue(name, out var value) ? value.Text ?? fallback : fallback;
    public bool Flag(string name) => Number(name) != 0;
    public string Name => WiredFormRegistry.Find(Category, Code)?.Name ?? "Unknown";
}

public sealed record WiredSimulationRoom(long RoomId, long OwnerId, int Width, int Height,
    IReadOnlyList<WiredSimulationFurniture> Furniture, IReadOnlyList<WiredSimulationUser> Users,
    IReadOnlyList<WiredSimulationNode> Nodes,
    IReadOnlyList<WiredSimulationVariable>? Variables = null,
    IReadOnlyDictionary<string, WiredSimulationValue>? GlobalVariables = null,
    IReadOnlyList<double>? Floor = null, DateTimeOffset? Epoch = null,
    string? Source = null, IReadOnlyList<string>? Incomplete = null, long? GroupId = null,
    IReadOnlyDictionary<int, long>? TeamScores = null, WiredSimulationApiAccess? Api = null,
    int? ClockTickOffsetMilliseconds = null, WiredSimulationTimer? Timer = null,
    WiredSimulationTeleportTimings? TeleportTimings = null, WiredSimulationBotTiming? BotTiming = null,
    string? CalendarTimezone = null, WiredSimulationTimeUtilitiesProfile? TimeUtilities = null,
    WiredSimulationFxPresentation? FxPresentation = null, IReadOnlyList<int>? TeamOrder = null,
    WiredSimulationProjectileTiming? ProjectileTiming = null, WiredSimulationGame? Game = null,
    IReadOnlyList<WiredSimulationStoredVariable>? StoredVariables = null,
    IReadOnlyList<WiredSimulationVisual>? Visuals = null,
    IReadOnlyList<WiredSimulationLinkEndpoint>? Links = null,
    IReadOnlyList<WiredSimulationSharedPlaceholder>? SharedPlaceholders = null,
    IReadOnlyList<WiredSimulationRandomHistory>? RandomHistory = null,
    IReadOnlyList<WiredSimulationLimitHistory>? LimitHistory = null);

public sealed record WiredSimulationRewardOutcome(long ActionId, long UserId, int? RewardIndex);

public sealed record WiredSimulationOptions(int Seed = 1, int MaximumEvents = 100000,
    long MaximumWork = 1000000, long MaximumTimeMilliseconds = 3600000,
    int MaximumTrace = 2000, int MaximumFurniture = 20000,
    int MaximumStackVisitsPerExecution = 100, int MaximumMovesPerExecution = 80,
    int MaximumPendingEvents = 100000, bool RequireKnownOrder = false,
    int VariableBits = 64, int PulseMilliseconds = 500, int ShortPulseMilliseconds = 50,
    int MaximumSelectionDepth = 128, IReadOnlyList<WiredSimulationBotFollowChoice>? BotFollowChoices = null,
    IReadOnlyList<WiredSimulationBotDeliveryChoice>? BotDeliveryChoices = null,
    IReadOnlyList<WiredSimulationFurnitureTransferChoice>? FurnitureTransferChoices = null,
    IReadOnlyList<WiredSimulationSpawnValueChoice>? SpawnValueChoices = null,
    IReadOnlyList<WiredSimulationRandomChoice>? RandomChoices = null,
    IReadOnlyList<WiredSimulationCarrierContactChoice>? CarrierContactChoices = null,
    IReadOnlyList<WiredSimulationBotMovementChoice>? BotMovementChoices = null,
    IReadOnlyList<WiredSimulationRewardOutcome>? RewardOutcomes = null);

public sealed record WiredSimulationEvent(WiredSimulationEventKind Kind, long At = 0, long? UserId = null,
    long? FurnitureId = null, string? Text = null, int? X = null, int? Y = null,
    IReadOnlyList<long>? Furniture = null, IReadOnlyList<long>? Users = null,
    IReadOnlyDictionary<string, long>? Context = null, string? Variable = null,
    long? OldValue = null, long? NewValue = null, int Change = 0, int Origin = 0, int? Team = null,
    WiredSimulationApiRequest? Api = null, WiredSimulationHover? Hover = null, WiredSimulationTradeInput? Trade = null,
    WiredSimulationLinkPayload? Link = null, WiredSimulationUserActivity? Activity = null,
    WiredSimulationVariableObservation? Observation = null);

public sealed record WiredSimulationDiagnostic(WiredSimulationSeverity Severity, string Code,
    string Message, long At, long? FurnitureId = null);

public sealed record WiredSimulationTrace(long Sequence, long At, string Kind, long? FurnitureId,
    string Message, IReadOnlyList<long>? Targets = null, long? Before = null, long? After = null);

public enum WiredSimulationSpeechMode { Talk, Shout, Whisper }

public sealed record WiredSimulationSpeech(long SpeakerId, WiredSimulationSpeechMode Mode,
    int BubbleWidth, bool Private, bool HideUsername = false);

public sealed record WiredSimulationMovement(WiredMovementType Type, Tile Source, Tile Destination,
    int AnimationTime, int Direction, int? HeadDirection = null, bool IsSlide = false,
    int? CurveStrength = null, int? JumpPower = null, int? OvershootDistance = null)
{
    public int? OvershootAnimationTime => OvershootDistance;
}

public sealed record WiredSimulationEffect(long At, string Kind, long FurnitureId,
    IReadOnlyList<long> Targets, string? Text = null, long? Value = null, WiredSimulationSpeech? Speech = null,
    WiredClickSettings? ClickSettings = null, WiredSimulationMovement? Movement = null,
    WiredSimulationChestTransfer? ChestTransfer = null, WiredTradeInitiate? TradeInitiation = null,
    WiredSimulationRoomLink? RoomLink = null, WiredSimulationVisualCommand? Visual = null,
    WiredSimulationLinkPayload? Link = null);

public sealed record WiredSimulationChestTransfer(long ChestId, long UserId,
    IReadOnlyList<WiredChestStorageSnapshot> SourceItems, int Coins = 0, EarningCategory? EarningsCategory = null);

public sealed record WiredSimulationMetrics(long Events, long StackActivations, long Actions,
    long WorkUnits, long VariableReads, long VariableWrites, long SelectorCandidates,
    long Moves, long TemporaryPlacements, long PeakPendingEvents, long PeakWorkPerExecution);

public sealed record WiredSimulationFxView(long ViewerId, bool Visible, bool Hovered, long? VisibleUntil, bool IsInitialize);

public sealed record WiredSimulationVariableFx(VariableFxConfigEntry Config, string VariableId,
    WiredSourceDomain Domain, long EntityId, long Value, long? MinOverride, long? MaxOverride,
    IReadOnlyDictionary<string, string> Extra, IReadOnlyList<long> Audience, IReadOnlyList<WiredSimulationFxView>? Views = null)
{
    public long MinValue => MinOverride ?? Config.DefaultMinValue;
    public long MaxValue => MaxOverride ?? Config.DefaultMaxValue;
}

public sealed record WiredSimulationResult(WiredSimulationStatus Status, long Time,
    WiredSimulationRoom Room, WiredSimulationMetrics Metrics,
    IReadOnlyList<WiredSimulationDiagnostic> Diagnostics, IReadOnlyList<WiredSimulationTrace> Trace,
    IReadOnlyList<WiredSimulationEffect> Effects, int PendingEvents, bool TraceTruncated,
    bool ProvesServerBehavior = false, IReadOnlyList<WiredSimulationApiResponse>? ApiResponses = null,
    IReadOnlyList<WiredSimulationVariableFx>? VariableFx = null,
    IReadOnlyList<WiredSimulationVisualSample>? Visuals = null)
{
    public bool HasUnresolvedRules => Diagnostics.Any(value => value.Severity is WiredSimulationSeverity.Unsupported or WiredSimulationSeverity.Uncertain);
    public bool Passed => Status == WiredSimulationStatus.Completed && Diagnostics.All(value => value.Severity == WiredSimulationSeverity.Information);
}

public sealed record WiredSimulationCoverage(WiredFormCategory Category, int Code, string Name,
    string Support, IReadOnlyList<string> Limits);

public sealed record WiredSimulationAssertion(string Kind, long? EntityId = null, string? Variable = null,
    long? Value = null, int? X = null, int? Y = null, bool? Present = null);

public sealed record WiredSimulationAssertionResult(WiredSimulationAssertion Assertion, bool Passed, string Actual);
