using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Wired;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Qx.Game;

/// <summary>Represents one text connector entry of a wired variable.</summary>
/// <param name="Id">The entry key.</param>
/// <param name="Value">The entry text.</param>
public sealed record WiredTextConnectorSnapshot(Id Id, string Value);

/// <summary>Specifies the kind of wired furni a configuration belongs to.</summary>
public enum WiredConfigurationKind
{
    /// <summary>A wired trigger.</summary>
    Trigger,
    /// <summary>A wired action, also called an effect.</summary>
    Action,
    /// <summary>A wired condition.</summary>
    Condition,
    /// <summary>A wired selector.</summary>
    Selector,
    /// <summary>A wired add-on.</summary>
    Addon,
    /// <summary>A wired variable.</summary>
    Variable
}

/// <summary>Represents the input sources a wired configuration allows and selects by default.</summary>
/// <param name="AllowedFurniSources">The allowed furni source types, one list per furni selection.</param>
/// <param name="AllowedUserSources">The allowed user source types, one list per user selection.</param>
/// <param name="DefaultFurniSources">The default furni source types.</param>
/// <param name="DefaultUserSources">The default user source types.</param>
public sealed record WiredInputSourcesSnapshot(
    IReadOnlyList<IReadOnlyList<int>> AllowedFurniSources,
    IReadOnlyList<IReadOnlyList<int>> AllowedUserSources,
    IReadOnlyList<int> DefaultFurniSources,
    IReadOnlyList<int> DefaultUserSources);

/// <summary>Represents the definition of a wired variable.</summary>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="VariableType">The variable type code.</param>
/// <param name="VariableName">The name of the variable, empty when it has none.</param>
/// <param name="AvailabilityType">
/// The availability type code. Values below 100 are stored, and 10, 11 and 20 are persisted.
/// </param>
/// <param name="VariableTarget">The target code of the variable, such as furni, user or global.</param>
/// <param name="AlwaysAvailable">Whether the variable is always available.</param>
/// <param name="CanCreateAndDelete">Whether the variable can be created on and deleted from its holders.</param>
/// <param name="HasValue">Whether the variable carries a value.</param>
/// <param name="CanWriteValue">Whether the value can be written.</param>
/// <param name="CanInterceptChanges">Whether changes to the variable can be intercepted.</param>
/// <param name="IsInvisible">Whether the variable is invisible.</param>
/// <param name="CanReadCreationTime">Whether the creation time of the variable can be read.</param>
/// <param name="CanReadLastUpdateTime">Whether the last update time of the variable can be read.</param>
/// <param name="TextConnector">
/// The text connector entries, or <see langword="null"/> when the variable has no text connector.
/// </param>
public sealed record WiredVariableSnapshot(
    string VariableId,
    int VariableType,
    string VariableName,
    int AvailabilityType,
    int VariableTarget,
    bool AlwaysAvailable,
    bool CanCreateAndDelete,
    bool HasValue,
    bool CanWriteValue,
    bool CanInterceptChanges,
    bool IsInvisible,
    bool CanReadCreationTime,
    bool CanReadLastUpdateTime,
    IReadOnlyList<WiredTextConnectorSnapshot>? TextConnector);

/// <summary>Represents the definition of a wired variable inside a configuration context.</summary>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="VariableType">The variable type code.</param>
/// <param name="VariableName">The name of the variable, empty when it has none.</param>
/// <param name="AvailabilityType">
/// The availability type code. Values below 100 are stored, and 10, 11 and 20 are persisted.
/// </param>
/// <param name="VariableTarget">The target code of the variable, such as furni, user or global.</param>
/// <param name="AlwaysAvailable">Whether the variable is always available.</param>
/// <param name="CanCreateAndDelete">Whether the variable can be created on and deleted from its holders.</param>
/// <param name="HasValue">Whether the variable carries a value.</param>
/// <param name="CanWriteValue">Whether the value can be written.</param>
/// <param name="CanInterceptChanges">Whether changes to the variable can be intercepted.</param>
/// <param name="IsInvisible">Whether the variable is invisible.</param>
/// <param name="CanReadCreationTime">Whether the creation time of the variable can be read.</param>
/// <param name="CanReadLastUpdateTime">Whether the last update time of the variable can be read.</param>
/// <param name="TextConnectorIds">
/// The keys of the text connector entries, or <see langword="null"/> when the variable has no text connector.
/// </param>
/// <param name="TextConnectorValues">
/// The texts of the text connector entries, in the same order as <paramref name="TextConnectorIds"/>,
/// or <see langword="null"/> when the variable has no text connector.
/// </param>
public sealed record WiredContextVariableSnapshot(
    string VariableId,
    int VariableType,
    string VariableName,
    int AvailabilityType,
    int VariableTarget,
    bool AlwaysAvailable,
    bool CanCreateAndDelete,
    bool HasValue,
    bool CanWriteValue,
    bool CanInterceptChanges,
    bool IsInvisible,
    bool CanReadCreationTime,
    bool CanReadLastUpdateTime,
    IReadOnlyList<Id>? TextConnectorIds,
    IReadOnlyList<string>? TextConnectorValues);

/// <summary>Represents the value of a wired variable held by a furni item or a user.</summary>
/// <param name="ObjectId">The id of the furni item or user that holds the value.</param>
/// <param name="Value">The variable value.</param>
public sealed record WiredObjectValueSnapshot(Id ObjectId, long Value);

/// <summary>Represents a wired variable shared from another room.</summary>
/// <param name="RoomId">The id of the room that shares the variable.</param>
/// <param name="RoomName">The name of the room that shares the variable.</param>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="VariableType">The variable type code.</param>
/// <param name="VariableName">The name of the variable, empty when it has none.</param>
/// <param name="AvailabilityType">
/// The availability type code. Values below 100 are stored, and 10, 11 and 20 are persisted.
/// </param>
/// <param name="VariableTarget">The target code of the variable, such as furni, user or global.</param>
/// <param name="AlwaysAvailable">Whether the variable is always available.</param>
/// <param name="CanCreateAndDelete">Whether the variable can be created on and deleted from its holders.</param>
/// <param name="HasValue">Whether the variable carries a value.</param>
/// <param name="CanWriteValue">Whether the value can be written.</param>
/// <param name="CanInterceptChanges">Whether changes to the variable can be intercepted.</param>
/// <param name="IsInvisible">Whether the variable is invisible.</param>
/// <param name="CanReadCreationTime">Whether the creation time of the variable can be read.</param>
/// <param name="CanReadLastUpdateTime">Whether the last update time of the variable can be read.</param>
/// <param name="TextConnectorIds">
/// The keys of the text connector entries, or <see langword="null"/> when the variable has no text connector.
/// </param>
/// <param name="TextConnectorValues">
/// The texts of the text connector entries, in the same order as <paramref name="TextConnectorIds"/>,
/// or <see langword="null"/> when the variable has no text connector.
/// </param>
public sealed record WiredSharedVariableSnapshot(
    Id RoomId,
    string RoomName,
    string VariableId,
    int VariableType,
    string VariableName,
    int AvailabilityType,
    int VariableTarget,
    bool AlwaysAvailable,
    bool CanCreateAndDelete,
    bool HasValue,
    bool CanWriteValue,
    bool CanInterceptChanges,
    bool IsInvisible,
    bool CanReadCreationTime,
    bool CanReadLastUpdateTime,
    IReadOnlyList<Id>? TextConnectorIds,
    IReadOnlyList<string>? TextConnectorValues);

/// <summary>Represents a wired placeholder shared from another room.</summary>
/// <param name="RoomId">The id of the room that shares the placeholder.</param>
/// <param name="RoomName">The name of the room that shares the placeholder.</param>
/// <param name="PlaceholderName">The name of the placeholder.</param>
public sealed record WiredSharedPlaceholderSnapshot(
    Id RoomId,
    string RoomName,
    string PlaceholderName);

/// <summary>Specifies the kind of value a wired configuration context entry carries.</summary>
public enum WiredContextValueKind
{
    /// <summary>A hash of all variables in the room, sent with tag 0.</summary>
    RoomVariables,
    /// <summary>A furni variable with the furni that hold it, sent with tag 1.</summary>
    FurniVariable,
    /// <summary>A user variable with the users that hold it, sent with tag 2.</summary>
    UserVariable,
    /// <summary>A global variable with its value, sent with tag 3.</summary>
    GlobalVariable,
    /// <summary>The variables other rooms share, sent with tag 4.</summary>
    ReferenceVariables,
    /// <summary>A list of variable definitions, sent with tag 5.</summary>
    RulesetVariables,
    /// <summary>The placeholders other rooms share, sent with tag 6.</summary>
    ReferencePlaceholders
}

/// <summary>Represents one entry of a wired configuration context.</summary>
/// <remarks>
/// Only the members that belong to <paramref name="Kind"/> are set; the others are <see langword="null"/>.
/// </remarks>
/// <param name="Tag">The context tag the hotel sent.</param>
/// <param name="Kind">The kind of value the entry carries.</param>
/// <param name="AllVariablesHash">The hash of all variables in the room, for <see cref="WiredContextValueKind.RoomVariables"/>.</param>
/// <param name="Variable">
/// The variable definition, for <see cref="WiredContextValueKind.FurniVariable"/>,
/// <see cref="WiredContextValueKind.UserVariable"/> and <see cref="WiredContextValueKind.GlobalVariable"/>.
/// </param>
/// <param name="Holders">
/// The furni or users that hold the variable and their values, for
/// <see cref="WiredContextValueKind.FurniVariable"/> and <see cref="WiredContextValueKind.UserVariable"/>.
/// </param>
/// <param name="Value">The value of the global variable, for <see cref="WiredContextValueKind.GlobalVariable"/>.</param>
/// <param name="SharedVariables">The variables other rooms share, for <see cref="WiredContextValueKind.ReferenceVariables"/>.</param>
/// <param name="Variables">The variable definitions, for <see cref="WiredContextValueKind.RulesetVariables"/>.</param>
/// <param name="SharedPlaceholders">
/// The placeholders other rooms share, for <see cref="WiredContextValueKind.ReferencePlaceholders"/>.
/// </param>
public sealed record WiredContextEntrySnapshot(
    int Tag,
    WiredContextValueKind Kind,
    int? AllVariablesHash,
    WiredContextVariableSnapshot? Variable,
    IReadOnlyList<WiredObjectValueSnapshot>? Holders,
    long? Value,
    IReadOnlyList<WiredSharedVariableSnapshot>? SharedVariables,
    IReadOnlyList<WiredContextVariableSnapshot>? Variables,
    IReadOnlyList<WiredSharedPlaceholderSnapshot>? SharedPlaceholders);

/// <summary>Represents the configuration of a wired furni item as the hotel sent it.</summary>
/// <param name="Kind">The kind of wired furni.</param>
/// <param name="FurniLimit">The maximum number of furni that can be selected.</param>
/// <param name="StuffIds">The ids of the selected furni.</param>
/// <param name="StuffIds2">The ids of the furni in the second selection.</param>
/// <param name="StuffTypeId">The furni type id of the wired furni item.</param>
/// <param name="Id">The room id of the wired furni item.</param>
/// <param name="StringParam">The string parameter, with multiple values separated by tabs.</param>
/// <param name="IntParams">The integer parameters.</param>
/// <param name="VariableIds">The ids of the variables the configuration references.</param>
/// <param name="FurniSourceTypes">The furni source types of the configuration.</param>
/// <param name="UserSourceTypes">The user source types of the configuration.</param>
/// <param name="Code">The code that identifies the wired type.</param>
/// <param name="AdvancedMode">Whether the advanced mode is enabled.</param>
/// <param name="InputSources">The allowed and default input sources.</param>
/// <param name="AllowWallFurni">Whether wall items can be selected.</param>
/// <param name="Context">The context entries the hotel sent with the configuration.</param>
/// <param name="DefaultIntParams">The default integer parameters.</param>
/// <param name="DelayInPulses">The delay in pulses for an action; otherwise, <see langword="null"/>.</param>
/// <param name="QuantifierCode">The quantifier code for a condition; otherwise, <see langword="null"/>.</param>
/// <param name="QuantifierType">The quantifier type for a condition; otherwise, <see langword="null"/>.</param>
/// <param name="DefinitionIsInvert">
/// Whether the condition definition is inverted, for a condition; otherwise, <see langword="null"/>.
/// The value is not read from the message, so it is <see langword="false"/> for every condition.
/// </param>
/// <param name="IsFilter">Whether the selector is a filter, for a selector; otherwise, <see langword="null"/>.</param>
/// <param name="IsInvert">
/// Whether the condition or selector is inverted, for a condition or a selector; otherwise, <see langword="null"/>.
/// </param>
public sealed record WiredConfigurationSnapshot(
    WiredConfigurationKind Kind,
    int FurniLimit,
    IReadOnlyList<Id> StuffIds,
    IReadOnlyList<Id> StuffIds2,
    int StuffTypeId,
    Id Id,
    string StringParam,
    IReadOnlyList<int> IntParams,
    IReadOnlyList<string> VariableIds,
    IReadOnlyList<int> FurniSourceTypes,
    IReadOnlyList<int> UserSourceTypes,
    int Code,
    bool AdvancedMode,
    WiredInputSourcesSnapshot InputSources,
    bool AllowWallFurni,
    IReadOnlyList<WiredContextEntrySnapshot> Context,
    IReadOnlyList<int> DefaultIntParams,
    int? DelayInPulses,
    int? QuantifierCode,
    int? QuantifierType,
    bool? DefinitionIsInvert,
    bool? IsFilter,
    bool? IsInvert);

/// <summary>Represents one furni slot inside a wired chest.</summary>
/// <param name="InventoryId">The inventory id of the stored item.</param>
/// <param name="LockState">The lock state code of the item.</param>
/// <param name="TransactionId">The transaction id the hotel sends with the item.</param>
/// <param name="Type">The furni type of the item.</param>
/// <param name="Groupable">Whether the item can be grouped with identical items.</param>
/// <param name="SpecialType">The special type code of the item.</param>
/// <param name="Data">The item's payload.</param>
/// <param name="Extra">The extra value the hotel sends with a floor item; 0 for a wall item.</param>
public sealed record WiredChestStorageSnapshot(
    int InventoryId,
    int LockState,
    long TransactionId,
    ChestItemType Type,
    bool Groupable,
    int SpecialType,
    ItemDataSnapshot Data,
    int Extra);

/// <summary>Represents what is known about the contents of a wired chest.</summary>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="Coins">The coins in the chest, or <see langword="null"/> when the hotel has not sent them.</param>
/// <param name="Items">
/// The items in the chest, merged from the fragments received so far and the later updates, one per inventory id.
/// </param>
/// <param name="ItemsComplete">Whether every fragment of the item list has been received.</param>
/// <param name="ExpectedFragments">The number of fragments the item list is sent in.</param>
/// <param name="ReceivedFragments">The fragment numbers received, in ascending order.</param>
/// <param name="LastUpgradeResult">The result of the last upgrade of the chest, or <see langword="null"/>.</param>
/// <param name="LastPreferencesResult">The result of the last preferences update of the chest, or <see langword="null"/>.</param>
public sealed record WiredChestContentsSnapshot(
    Id ChestId,
    int? Coins,
    IReadOnlyList<WiredChestStorageSnapshot> Items,
    bool ItemsComplete,
    int ExpectedFragments,
    IReadOnlyList<int> ReceivedFragments,
    UpgradeChestResult? LastUpgradeResult,
    ChestPreferencesUpdateSuccess? LastPreferencesResult);

/// <summary>Represents one item offered in a wired trade.</summary>
/// <param name="ItemId">The inventory item id.</param>
/// <param name="Type">Whether the item is a floor or a wall item.</param>
/// <param name="Id">The furni id the hotel sends alongside the inventory item id.</param>
/// <param name="Kind">The furni kind identifier.</param>
/// <param name="Category">The category the item is filed under.</param>
/// <param name="IsGroupable">Whether the item can be grouped with identical items.</param>
/// <param name="Data">The item's payload.</param>
/// <param name="CreationDay">The day of the month the item was created.</param>
/// <param name="CreationMonth">The month the item was created.</param>
/// <param name="CreationYear">The year the item was created.</param>
/// <param name="Extra">The extra value the hotel sends with a floor item; -1 for a wall item.</param>
public sealed record WiredTradeItemSnapshot(
    Id ItemId,
    ItemType Type,
    Id Id,
    int Kind,
    int Category,
    bool IsGroupable,
    ItemDataSnapshot Data,
    int CreationDay,
    int CreationMonth,
    int CreationYear,
    long Extra);

/// <summary>Represents the offers of both users in a wired trade.</summary>
/// <param name="FirstUserId">The id of the first user.</param>
/// <param name="FirstUserItems">The items the first user offers.</param>
/// <param name="FirstUserNumItems">The item count the hotel reports for the first user.</param>
/// <param name="FirstUserNumCredits">The credit count the hotel reports for the first user.</param>
/// <param name="SecondUserId">The id of the second user.</param>
/// <param name="SecondUserItems">The items the second user offers.</param>
/// <param name="SecondUserNumItems">The item count the hotel reports for the second user.</param>
/// <param name="SecondUserNumCredits">The credit count the hotel reports for the second user.</param>
/// <param name="CanAccept">Whether the trade can be accepted.</param>
/// <param name="RequirementsMetCount">The number of times the offer meets its requirements.</param>
public sealed record WiredTradingItemsSnapshot(
    Id FirstUserId,
    IReadOnlyList<WiredTradeItemSnapshot> FirstUserItems,
    int FirstUserNumItems,
    int FirstUserNumCredits,
    Id SecondUserId,
    IReadOnlyList<WiredTradeItemSnapshot> SecondUserItems,
    int SecondUserNumItems,
    int SecondUserNumCredits,
    bool CanAccept,
    int RequirementsMetCount)
{
    /// <summary>Gets or initializes RequirementsMetCount; retained for migration.</summary>
    [Obsolete("Use RequirementsMetCount.")]
    [System.Text.Json.Serialization.JsonIgnore]
    public int Extra { get => RequirementsMetCount; init => RequirementsMetCount = value; }
}

/// <summary>Specifies the status of a wired trade.</summary>
public enum WiredTradeStatus
{
    /// <summary>No wired trade seen in the room.</summary>
    None,
    /// <summary>A trade the hotel started without sending any offers yet.</summary>
    Initiated,
    /// <summary>A trade whose offers the hotel has sent.</summary>
    Active,
    /// <summary>A canceled trade.</summary>
    Cancelled,
    /// <summary>A completed trade.</summary>
    Completed
}

/// <summary>Represents the state of the wired trade in the current room.</summary>
/// <param name="Status">The status of the trade.</param>
/// <param name="Initiation">The message that started the trade, or <see langword="null"/>.</param>
/// <param name="Items">The latest offers of both users, or <see langword="null"/> before the first update.</param>
/// <param name="Cancellation">
/// The cancellation of the trade, or <see langword="null"/> when it has not been canceled since the last update.
/// </param>
/// <param name="LastTransactionSuccess">The last successful wired transaction, or <see langword="null"/>.</param>
/// <param name="LastTransactionFailure">
/// The last failed wired transaction, or <see langword="null"/> when a transaction has succeeded since.
/// </param>
/// <param name="LastNotification">The last trade transaction notification, or <see langword="null"/>.</param>
public sealed record WiredTradeSnapshot(
    WiredTradeStatus Status,
    WiredTradeInitiate? Initiation,
    WiredTradingItemsSnapshot? Items,
    WiredTradeCancelled? Cancellation,
    WiredTransactionSuccess? LastTransactionSuccess,
    WiredTransactionFail? LastTransactionFailure,
    WiredTradeTransactionNotification? LastNotification);

/// <summary>Represents the state of the wired contract in the current room.</summary>
/// <param name="OpenContractId">The id of the open contract, or <see langword="null"/> when none has been opened.</param>
/// <param name="Contents">
/// The contents of the open contract, or <see langword="null"/> when the hotel has not sent them since it was opened.
/// </param>
/// <param name="LastUpdateResult">
/// The result of the last contract update, or <see langword="null"/> when none has arrived since the contract was opened.
/// </param>
public sealed record WiredContractSnapshot(
    int? OpenContractId,
    WiredContractContents? Contents,
    WiredContractUpdateResult? LastUpdateResult);

/// <summary>Represents the wired state of the current room.</summary>
/// <remarks>
/// A snapshot is immutable. <see cref="WiredManager"/> publishes a new one for every change.
/// </remarks>
/// <param name="Generation">The state generation, incremented each time the state is cleared.</param>
/// <param name="Revision">The state revision, incremented on every change.</param>
/// <param name="Permissions">The user's wired permissions in the room, or <see langword="null"/> when not received.</param>
/// <param name="Environment">The wired environment of the room, or <see langword="null"/> when not received.</param>
/// <param name="ClickSettings">The wired click settings of the room, or <see langword="null"/> when not received.</param>
/// <param name="RoomSettings">The wired settings of the room, or <see langword="null"/> when not received.</param>
/// <param name="OpenFurniId">The id of the wired furni item whose configuration was opened last, or <see langword="null"/>.</param>
/// <param name="Configuration">
/// The configuration of the open wired furni item, or <see langword="null"/> when it has not arrived.
/// </param>
/// <param name="LastSaveSucceeded">
/// Whether the last save of the open configuration succeeded, or <see langword="null"/> when none was
/// answered since it was opened.
/// </param>
/// <param name="LastValidationError">
/// The validation error of the last failed save, or <see langword="null"/> when the last save succeeded
/// or none was answered since the configuration was opened.
/// </param>
/// <param name="LastMenuError">The last wired menu error, or <see langword="null"/>.</param>
/// <param name="LastRewardResult">The last wired reward result, or <see langword="null"/>.</param>
/// <param name="LastOpenedChestId">The id of the chest opened last, or <see langword="null"/>.</param>
/// <param name="Chests">The contents of every chest seen in the room, ordered by chest id.</param>
/// <param name="Contract">The state of the wired contract.</param>
/// <param name="Trade">The state of the wired trade.</param>
public sealed record WiredSnapshot(
    long Generation,
    long Revision,
    WiredPermissions? Permissions,
    WiredEnvironment? Environment,
    WiredClickSettings? ClickSettings,
    WiredRoomSettings? RoomSettings,
    Id? OpenFurniId,
    WiredConfigurationSnapshot? Configuration,
    bool? LastSaveSucceeded,
    WiredValidationError? LastValidationError,
    WiredMenuError? LastMenuError,
    WiredRewardResult? LastRewardResult,
    Id? LastOpenedChestId,
    IReadOnlyList<WiredChestContentsSnapshot> Chests,
    WiredContractSnapshot Contract,
    WiredTradeSnapshot Trade)
{
    /// <summary>Gets the empty wired state, with generation and revision 0 and nothing received.</summary>
    public static WiredSnapshot Empty { get; } = new(
        0,
        0,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        [],
        new(null, null, null),
        new(WiredTradeStatus.None, null, null, null, null, null, null));

    /// <summary>Gets whether the user can modify wired in the room.</summary>
    /// <remarks>
    /// <see langword="false"/> when the permissions have not been received.
    /// </remarks>
    public bool CanModify => Permissions?.CanModify is true;
    /// <summary>Gets whether the user can read wired in the room.</summary>
    /// <remarks>
    /// <see langword="false"/> when the permissions have not been received.
    /// </remarks>
    public bool CanRead => Permissions?.CanRead is true;
    /// <summary>Gets the open configuration when it belongs to a trigger; otherwise, <see langword="null"/>.</summary>
    [JsonIgnore]
    public WiredConfigurationSnapshot? Trigger =>
        Configuration?.Kind is WiredConfigurationKind.Trigger ? Configuration : null;
    /// <summary>Gets the open configuration when it belongs to an action; otherwise, <see langword="null"/>.</summary>
    [JsonIgnore]
    public WiredConfigurationSnapshot? Effect =>
        Configuration?.Kind is WiredConfigurationKind.Action ? Configuration : null;
    /// <summary>Gets the open configuration when it belongs to a condition; otherwise, <see langword="null"/>.</summary>
    [JsonIgnore]
    public WiredConfigurationSnapshot? Condition =>
        Configuration?.Kind is WiredConfigurationKind.Condition ? Configuration : null;
    /// <summary>Gets the open configuration when it belongs to a selector; otherwise, <see langword="null"/>.</summary>
    [JsonIgnore]
    public WiredConfigurationSnapshot? Selector =>
        Configuration?.Kind is WiredConfigurationKind.Selector ? Configuration : null;
    /// <summary>Gets the open configuration when it belongs to an add-on; otherwise, <see langword="null"/>.</summary>
    [JsonIgnore]
    public WiredConfigurationSnapshot? Addon =>
        Configuration?.Kind is WiredConfigurationKind.Addon ? Configuration : null;
    /// <summary>Gets the open configuration when it belongs to a variable; otherwise, <see langword="null"/>.</summary>
    [JsonIgnore]
    public WiredConfigurationSnapshot? Variable =>
        Configuration?.Kind is WiredConfigurationKind.Variable ? Configuration : null;

}

internal enum WiredStateChangeKind
{
    Permissions,
    Environment,
    ClickSettings,
    RoomSettings,
    ConfigurationOpened,
    ConfigurationReceived,
    SaveSucceeded,
    ValidationFailed,
    MenuError,
    RewardResult,
    RoomStats,
    RoomLogs,
    ErrorLogs,
    UserClickResult,
    VariablesHash,
    VariablesDifferences,
    VariablesObject,
    VariableHolders,
    PermanentVariables,
    VariableOwners,
    PermanentVariableSetResult,
    ChestOpened,
    ChestCoins,
    ChestItemsChunk,
    ChestItemsUpdated,
    ChestUpgradeResult,
    ChestPreferencesUpdated,
    TransactionSucceeded,
    TransactionFailed,
    TransactionLogs,
    TransactionLogDetails,
    ContractContents,
    ContractOpened,
    ContractUpdateResult,
    TradeInitiated,
    TradeItemsUpdated,
    TradeCancelled,
    TradeCompleted,
    TradeNotification,
    Reset
}

internal sealed record WiredStateUpdate(
    WiredStateChangeKind Kind,
    WiredSnapshot State,
    object? Value);

/// <summary>
/// Manages the wired state of the current room, including the open configuration, chests, contracts and wired trades.
/// </summary>
/// <remarks>
/// <para>
/// Room messages are only recorded while the user is in a room. Room state is cleared when the user enters
/// or leaves a room, and when the hotel connection closes. Account preferences are retained across room changes.
/// </para>
/// <para>
/// All members are safe to call from any thread.
/// </para>
/// </remarks>
public sealed class WiredManager : GameStateManager
{
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Dictionary<Id, ChestState> chests = [];
    private WiredSnapshot snapshot = WiredSnapshot.Empty;
    private AccountPreferences? account_preferences;
    private WiredPermissions? permissions;
    private WiredEnvironment? environment;
    private WiredClickSettings? click_settings;
    private WiredRoomSettings? room_settings;
    private Id? open_furni_id;
    private WiredConfigurationSnapshot? configuration;
    private bool? last_save_succeeded;
    private WiredValidationError? last_validation_error;
    private WiredMenuError? last_menu_error;
    private WiredRewardResult? last_reward_result;
    private Id? last_opened_chest_id;
    private WiredContractSnapshot contract = new(null, null, null);
    private WiredTradeSnapshot trade = new(WiredTradeStatus.None, null, null, null, null, null, null);
    private long generation;
    private long revision;
    private long committed_generation;
    private long reset_generation = -1;
    private bool room_active;

    /// <summary>Gets the current wired state.</summary>
    /// <remarks>
    /// The snapshot is immutable and replaced on every change, so it can be read without locking.
    /// </remarks>
    public WiredSnapshot Snapshot => Volatile.Read(ref snapshot);

    /// <summary>Gets the last account preferences received during this hotel connection, or null before receipt.</summary>
    /// <remarks>These preferences survive room changes and are cleared when the hotel connection closes.</remarks>
    public AccountPreferences? AccountPreferences => Volatile.Read(ref account_preferences);

    internal event Action<WiredStateUpdate>? StateChanged;

    /// <inheritdoc/>
    protected override void OnAttach()
    {
        lock (publication_sync)
        {
            lock (state_sync)
            {
                Volatile.Write(ref account_preferences, null);
                generation++;
                committed_generation = CurrentStateGeneration;
                reset_generation = -1;
                PublishState();
            }
        }
        OnIncoming(MessageContracts.Wired.Account.Preferences, (message, state_generation) =>
        {
            lock (state_sync)
            {
                if (state_generation < committed_generation || state_generation == reset_generation)
                    return;
                committed_generation = state_generation;
                Volatile.Write(ref account_preferences, message);
            }
        });
        OnIncoming(
            MessageContracts.Wired.State.Permissions,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.Permissions,
                message,
                () => permissions = message));
        OnIncoming(
            MessageContracts.Wired.State.Environment,
            (message, state_generation) =>
            {
                WiredEnvironment value = SnapshotOf(message);
                Store(state_generation, WiredStateChangeKind.Environment, value, () => environment = value);
            });
        OnIncoming(
            MessageContracts.Wired.State.ClickSettings,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.ClickSettings,
                message,
                () => click_settings = message));
        OnIncoming(
            MessageContracts.Wired.Room.Settings,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.RoomSettings,
                message,
                () => room_settings = message));
        OnIncoming(
            MessageContracts.Wired.Configuration.Opened,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.ConfigurationOpened,
                message,
                () =>
                {
                    open_furni_id = message.StuffId;
                    configuration = null;
                    last_save_succeeded = null;
                    last_validation_error = null;
                }));
        OnConfiguration(
            MessageContracts.Wired.Configuration.Trigger,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Action,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Condition,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Selector,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Addon,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Variable,
            message => message.Config);
        OnIncoming(
            MessageContracts.Wired.Configuration.SaveSucceeded,
            (_, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.SaveSucceeded,
                new WiredSaveSuccess(),
                () =>
                {
                    last_save_succeeded = true;
                    last_validation_error = null;
                }));
        OnIncoming(
            MessageContracts.Wired.Configuration.ValidationFailed,
            (message, state_generation) =>
            {
                WiredValidationError value = SnapshotOf(message);
                Store(
                    state_generation,
                    WiredStateChangeKind.ValidationFailed,
                    value,
                    () =>
                    {
                        last_save_succeeded = false;
                        last_validation_error = value;
                    });
            });
        OnIncoming(
            MessageContracts.Wired.State.MenuError,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.MenuError,
                message,
                () => last_menu_error = message));
        OnIncoming(
            MessageContracts.Wired.State.RewardResult,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.RewardResult,
                message,
                () => last_reward_result = message));
        Observe(MessageContracts.Wired.Room.Stats, WiredStateChangeKind.RoomStats, SnapshotOf);
        Observe(MessageContracts.Wired.Room.Logs, WiredStateChangeKind.RoomLogs, SnapshotOf);
        Observe(MessageContracts.Wired.ErrorLogs.Snapshot, WiredStateChangeKind.ErrorLogs, SnapshotOf);
        Observe(MessageContracts.Wired.UserClick.Result, WiredStateChangeKind.UserClickResult, static value => value);
        Observe(MessageContracts.Wired.Variables.Hash, WiredStateChangeKind.VariablesHash, static value => value);
        Observe(MessageContracts.Wired.Variables.Differences, WiredStateChangeKind.VariablesDifferences, SnapshotOf);
        Observe(MessageContracts.Wired.Variables.Object, WiredStateChangeKind.VariablesObject, SnapshotOf);
        Observe(MessageContracts.Wired.Variables.Holders, WiredStateChangeKind.VariableHolders, SnapshotOf);
        Observe(MessageContracts.Wired.Variables.Permanent, WiredStateChangeKind.PermanentVariables, SnapshotOf);
        Observe(MessageContracts.Wired.Variables.Owners, WiredStateChangeKind.VariableOwners, SnapshotOf);
        Observe(
            MessageContracts.Wired.Variables.PermanentValueSetResult,
            WiredStateChangeKind.PermanentVariableSetResult,
            static value => value);
        OnIncoming(
            MessageContracts.Wired.Chests.Opened,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.ChestOpened,
                message,
                () => last_opened_chest_id = message.ChestId));
        OnIncoming(MessageContracts.Wired.Chests.Coins, OnChestCoins);
        OnIncoming(MessageContracts.Wired.Chests.ItemsChunk, OnChestItemsChunk);
        OnIncoming(MessageContracts.Wired.Chests.ItemsUpdated, OnChestItemsUpdated);
        OnIncoming(MessageContracts.Wired.Chests.UpgradeResult, OnChestUpgradeResult);
        OnIncoming(MessageContracts.Wired.Chests.PreferencesUpdated, OnChestPreferencesUpdated);
        OnIncoming(MessageContracts.Wired.Transaction.Succeeded, OnTransactionSucceeded);
        OnIncoming(MessageContracts.Wired.Transaction.Failed, OnTransactionFailed);
        Observe(MessageContracts.Wired.Transaction.Logs, WiredStateChangeKind.TransactionLogs, SnapshotOf);
        Observe(MessageContracts.Wired.Transaction.LogDetails, WiredStateChangeKind.TransactionLogDetails, SnapshotOf);
        OnIncoming(MessageContracts.Wired.Contracts.Contents, OnContractContents);
        OnIncoming(MessageContracts.Wired.Contracts.Opened, OnContractOpened);
        OnIncoming(MessageContracts.Wired.Contracts.UpdateResult, OnContractUpdateResult);
        OnIncoming(MessageContracts.Wired.Trade.Initiated, OnTradeInitiated);
        OnIncoming(MessageContracts.Wired.Trade.ItemsUpdated, OnTradeItemsUpdated);
        OnIncoming(MessageContracts.Wired.Trade.Cancelled, OnTradeCancelled);
        OnIncoming(MessageContracts.Wired.Trade.Completed, OnTradeCompleted);
        OnIncoming(MessageContracts.Wired.Trade.Notification, OnTradeNotification);
    }

    /// <inheritdoc/>
    protected override void Reset()
    {
        long state_generation = CurrentStateGeneration;
        lock (publication_sync)
        {
            WiredSnapshot updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation || state_generation == reset_generation)
                    return;
                committed_generation = state_generation;
                reset_generation = state_generation;
                Volatile.Write(ref account_preferences, null);
                if (!room_active)
                    return;
                room_active = false;
                ClearState();
                generation++;
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(new WiredStateUpdate(WiredStateChangeKind.Reset, updated, null));
        }
    }

    internal void EnterRoom(Id _)
    {
        ChangeRoom(true);
    }

    internal void LeaveRoom()
    {
        ChangeRoom(false);
    }

    private void ChangeRoom(bool active)
    {
        lock (publication_sync)
        {
            WiredSnapshot updated;
            lock (state_sync)
            {
                room_active = active;
                ClearState();
                generation++;
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(new WiredStateUpdate(WiredStateChangeKind.Reset, updated, null));
        }
    }

    private void ClearState()
    {
        permissions = null;
        environment = null;
        click_settings = null;
        room_settings = null;
        open_furni_id = null;
        configuration = null;
        last_save_succeeded = null;
        last_validation_error = null;
        last_menu_error = null;
        last_reward_result = null;
        last_opened_chest_id = null;
        chests.Clear();
        contract = new(null, null, null);
        trade = new(WiredTradeStatus.None, null, null, null, null, null, null);
    }

    private void OnConfiguration<T>(
        MessageContract<T> message_contract,
        Func<T, WiredConfig> select)
        where T : Qx.Messages.IParserComposer<T> =>
        OnIncoming(
            message_contract,
            (message, state_generation) =>
            {
                WiredConfigurationSnapshot value = SnapshotOf(select(message));
                Store(
                    state_generation,
                    WiredStateChangeKind.ConfigurationReceived,
                    value,
                    () =>
                    {
                        configuration = value;
                        open_furni_id = value.Id;
                    });
            });

    private void Observe<T>(
        MessageContract<T> message_contract,
        WiredStateChangeKind kind,
        Func<T, T> snapshot_value)
        where T : Qx.Messages.IParserComposer<T> =>
        OnIncoming(
            message_contract,
            (message, state_generation) =>
            {
                T value = snapshot_value(message);
                Store(state_generation, kind, value, static () => { });
            });

    private void OnChestCoins(CoinsChestContents message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ChestCoins,
            message,
            () => Chest(message.ChestId).Coins = message.Coins);
    }

    private void OnChestItemsChunk(ItemsChestContentsChunk message, long state_generation)
    {
        WiredChestItemsChunkSnapshot value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.ChestItemsChunk,
            value,
            () => Chest(message.ChestId).Apply(value));
    }

    private void OnChestItemsUpdated(ItemsChestContentsUpdated message, long state_generation)
    {
        WiredChestItemsUpdatedSnapshot value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.ChestItemsUpdated,
            value,
            () => Chest(message.ChestId).Apply(value));
    }

    private void OnChestUpgradeResult(UpgradeChestResult message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ChestUpgradeResult,
            message,
            () => Chest(message.ChestId).LastUpgradeResult = message);
    }

    private void OnChestPreferencesUpdated(
        ChestPreferencesUpdateSuccess message,
        long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ChestPreferencesUpdated,
            message,
            () => Chest(message.ChestId).LastPreferencesResult = message);
    }

    private void OnTransactionSucceeded(WiredTransactionSuccess message, long state_generation)
    {
        WiredTransactionSuccess value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.TransactionSucceeded,
            value,
            () => trade = trade with { LastTransactionSuccess = value, LastTransactionFailure = null });
    }

    private void OnTransactionFailed(WiredTransactionFail message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.TransactionFailed,
            message,
            () => trade = trade with { LastTransactionFailure = message });
    }

    private void OnContractContents(WiredContractContents message, long state_generation)
    {
        WiredContractContents value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.ContractContents,
            value,
            () => contract = contract with
            {
                OpenContractId = value.ContractId,
                Contents = value
            });
    }

    private void OnContractOpened(WiredOpenContract message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ContractOpened,
            message,
            () => contract = contract with
            {
                OpenContractId = message.ContractId,
                Contents = null,
                LastUpdateResult = null
            });
    }

    private void OnContractUpdateResult(WiredContractUpdateResult message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ContractUpdateResult,
            message,
            () => contract = contract with { LastUpdateResult = message });
    }

    private void OnTradeInitiated(WiredTradeInitiate message, long state_generation)
    {
        WiredTradeInitiate value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.TradeInitiated,
            value,
            () => trade = new(WiredTradeStatus.Initiated, value, null, null, null, null, null));
    }

    private void OnTradeItemsUpdated(WiredTradeItemsUpdate message, long state_generation)
    {
        WiredTradingItemsSnapshot value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.TradeItemsUpdated,
            value,
            () => trade = trade with
            {
                Status = WiredTradeStatus.Active,
                Items = value,
                Cancellation = null
            });
    }

    private void OnTradeCancelled(WiredTradeCancelled message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.TradeCancelled,
            message,
            () => trade = trade with
            {
                Status = WiredTradeStatus.Cancelled,
                Cancellation = message
            });
    }

    private void OnTradeCompleted(WiredTradeCompleted message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.TradeCompleted,
            message,
            () => trade = trade with
            {
                Status = WiredTradeStatus.Completed,
                Cancellation = null
            });
    }

    private void OnTradeNotification(
        WiredTradeTransactionNotification message,
        long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.TradeNotification,
            message,
            () => trade = trade with { LastNotification = message });
    }

    private void Store(
        long state_generation,
        WiredStateChangeKind kind,
        object? value,
        Action mutation)
    {
        lock (publication_sync)
        {
            WiredSnapshot updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation || !room_active)
                    return;
                committed_generation = state_generation;
                reset_generation = -1;
                mutation();
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(new WiredStateUpdate(kind, updated, value));
        }
    }

    private WiredSnapshot PublishState()
    {
        IReadOnlyList<WiredChestContentsSnapshot> chest_snapshot = ReadOnly(
            chests
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value.Snapshot(pair.Key)));
        var updated = new WiredSnapshot(
            generation,
            revision,
            permissions,
            environment,
            click_settings,
            room_settings,
            open_furni_id,
            configuration,
            last_save_succeeded,
            last_validation_error,
            last_menu_error,
            last_reward_result,
            last_opened_chest_id,
            chest_snapshot,
            contract,
            trade);
        Volatile.Write(ref snapshot, updated);
        return updated;
    }

    private ChestState Chest(Id chest_id)
    {
        if (!chests.TryGetValue(chest_id, out ChestState? state))
        {
            state = new ChestState();
            chests.Add(chest_id, state);
        }
        return state;
    }

    internal static WiredConfigurationSnapshot SnapshotOf(WiredConfig value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredConfigurationKind kind = value switch
        {
            WiredTriggerConfig => WiredConfigurationKind.Trigger,
            WiredActionConfig => WiredConfigurationKind.Action,
            WiredConditionConfig => WiredConfigurationKind.Condition,
            WiredSelectorConfig => WiredConfigurationKind.Selector,
            WiredAddonConfig => WiredConfigurationKind.Addon,
            WiredVariableConfig => WiredConfigurationKind.Variable,
            _ => throw new InvalidDataException($"Unsupported wired configuration type '{value.GetType().FullName}'.")
        };
        return new WiredConfigurationSnapshot(
            kind,
            value.FurniLimit,
            ReadOnly(value.StuffIds),
            ReadOnly(value.StuffIds2),
            value.StuffTypeId,
            value.Id,
            value.StringParam,
            ReadOnly(value.IntParams),
            ReadOnly(value.VariableIds),
            ReadOnly(value.FurniSourceTypes),
            ReadOnly(value.UserSourceTypes),
            value.Code,
            value.AdvancedMode,
            SnapshotOf(value.InputSources),
            value.AllowWallFurni,
            SnapshotOf(value.Context),
            ReadOnly(value.DefaultIntParams),
            (value as WiredActionConfig)?.DelayInPulses,
            (value as WiredConditionConfig)?.QuantifierCode,
            (value as WiredConditionConfig)?.QuantifierType,
            (value as WiredConditionConfig)?.DefinitionIsInvert,
            (value as WiredSelectorConfig)?.IsFilter,
            value switch
            {
                WiredConditionConfig condition => condition.IsInvert,
                WiredSelectorConfig selector => selector.IsInvert,
                _ => null
            });
    }

    internal static WiredAllVariablesDiffs SnapshotOf(WiredAllVariablesDiffs value) => new(
        value.AllVariablesHash,
        value.IsLastChunk,
        ReadOnly(value.RemovedVariables),
        ReadOnly(value.AddedOrUpdated.Select(entry =>
            new WiredVariableWithHash(entry.PerVariableHash, CloneOf(entry.Variable)))));

    internal static WiredVariablesForObject SnapshotOf(WiredVariablesForObject value) => new(
        new WiredObjectInspectionData(
            value.Data.Type,
            value.Data.ObjectId,
            value.Data.UserIndex,
            ReadOnly(value.Data.VariableValues),
            value.Data.ConfiguredInWireds is null ? null : ReadOnly(value.Data.ConfiguredInWireds)));

    internal static WiredAllVariableHolders SnapshotOf(WiredAllVariableHolders value) => new(
        value.LeadingValue,
        CloneOf(value.VariableInfoAndHolders));

    internal static WiredUserPermanentVariables SnapshotOf(WiredUserPermanentVariables value)
    {
        WiredUserPermanentVariablesList list = value.List;
        return new WiredUserPermanentVariables(new WiredUserPermanentVariablesList(
            list.EntityType,
            list.EntityId,
            list.EntityName,
            list.EntityFigure,
            list.OwnerId,
            list.OwnerName,
            list.OwnerFigure,
            ReadOnly(list.VariableStorage)));
    }

    internal static WiredUserVariablesList SnapshotOf(WiredUserVariablesList value)
    {
        WiredUserVariablesPage page = value.Page;
        return new WiredUserVariablesList(new WiredUserVariablesPage(
            page.VariableId,
            page.TotalEntries,
            page.CurrentPage,
            page.Amount,
            ReadOnly(page.Elements),
            page.UserTypeFilter,
            page.SortTypeFilter));
    }

    internal static WiredRoomStats SnapshotOf(WiredRoomStats value) => value;

    internal static WiredRoomLogs SnapshotOf(WiredRoomLogs value) => new(
        new WiredLogPage(
            value.Page.TotalEntries,
            value.Page.CurrentPage,
            value.Page.Amount,
            ReadOnly(value.Page.Elements),
            value.Page.LogLevelFilter,
            value.Page.LogSourceFilter,
            value.Page.Query));

    internal static WiredErrorLogs SnapshotOf(WiredErrorLogs value) =>
        new(ReadOnly(value.Errors));

    internal static WiredTransactionLogList SnapshotOf(WiredTransactionLogList value) => new(
        new WiredTransactionLogPage(
            value.Logs.LogListType,
            value.Logs.LogListId,
            value.Logs.TotalLogs,
            value.Logs.CurrentPage,
            value.Logs.Amount,
            ReadOnly(value.Logs.Logs)));

    internal static WiredTransactionLogDetails SnapshotOf(WiredTransactionLogDetails value) => new(
        new WiredTransactionDetails(
            value.Details.TransactionInfo,
            ReadOnly(value.Details.ChestIds),
            ReadOnly(value.Details.DepositedFurnis),
            ReadOnly(value.Details.WithdrawnFurnis),
            value.Details.IsIncompleteData));

    internal static WiredContractContents SnapshotOf(WiredContractContents value) => new(
        value.ContractId,
        value.ContractType,
        SnapshotOf(value.Definition),
        value.PaymentMode,
        value.ReceiveText,
        value.LayoutType,
        value.RewardCategory,
        value.ShowDialog,
        value.RewardText);

    internal static WiredTransactionSuccess SnapshotOf(WiredTransactionSuccess value) => new(
        value.TransactionSuccessTypeId,
        value.RewardContents is null ? null : SnapshotOf(value.RewardContents),
        value.RewardText,
        value.OpenByDefault);

    internal static WiredChestItemsChunkSnapshot SnapshotOf(ItemsChestContentsChunk value) => new(
        value.ChestId,
        value.TotalFragments,
        value.FragmentNo,
        ReadOnly(value.StorageChunk.Select(SnapshotOf)));

    internal static WiredChestItemsUpdatedSnapshot SnapshotOf(ItemsChestContentsUpdated value) => new(
        value.ChestId,
        ReadOnly(value.RemovedIds),
        ReadOnly(value.AddedStorage.Select(SnapshotOf)));

    internal static WiredTradingItemsSnapshot SnapshotOf(WiredTradeItemsUpdate value) => new(
        value.TradingItems.FirstUserId,
        ReadOnly(value.TradingItems.FirstUserItems.Select(SnapshotOf)),
        value.TradingItems.FirstUserNumItems,
        value.TradingItems.FirstUserNumCredits,
        value.TradingItems.SecondUserId,
        ReadOnly(value.TradingItems.SecondUserItems.Select(SnapshotOf)),
        value.TradingItems.SecondUserNumItems,
        value.TradingItems.SecondUserNumCredits,
        value.CanAccept,
        value.RequirementsMetCount);

    internal static WiredTradeInitiate SnapshotOf(WiredTradeInitiate value) => new(
        SnapshotOf(value.Requirement),
        value.ShowRequirementsImmediate,
        value.OverridePreviousTrade,
        value.TimeoutSeconds);

    internal static WiredVariableSnapshot SnapshotOf(WiredVariable value) => new(
        value.VariableId,
        value.VariableType,
        value.VariableName,
        value.AvailabilityType,
        value.VariableTarget,
        value.AlwaysAvailable,
        value.CanCreateAndDelete,
        value.HasValue,
        value.CanWriteValue,
        value.CanInterceptChanges,
        value.IsInvisible,
        value.CanReadCreationTime,
        value.CanReadLastUpdateTime,
        value.TextConnector is null
            ? null
            : ReadOnly(value.TextConnector.Select(pair =>
                new WiredTextConnectorSnapshot(pair.Key, pair.Value))));

    private static WiredEnvironment SnapshotOf(WiredEnvironment value) => new(
        value.HasClickUserWired,
        value.EnabledAchievements is null ? null : ReadOnly(value.EnabledAchievements));

    private static WiredValidationError SnapshotOf(WiredValidationError value) => new(
        value.LocalizationKey,
        ReadOnly(value.Parameters));

    private static WiredChestStorageSnapshot SnapshotOf(ChestStorage value) => new(
        value.InventoryId,
        value.LockState,
        value.TransactionId,
        value.Type,
        value.Groupable,
        value.SpecialType,
        SnapshotOf(value.StuffData),
        value.Extra);

    private static WiredTradeItemSnapshot SnapshotOf(TradeItem value) => new(
        value.ItemId,
        value.Type,
        value.Id,
        value.Kind,
        value.Category,
        value.IsGroupable,
        SnapshotOf(value.Data),
        value.CreationDay,
        value.CreationMonth,
        value.CreationYear,
        value.Extra);

    private static ItemDataSnapshot SnapshotOf(ItemData value)
    {
        ItemDataSnapshot data = SnapshotFactory.From(value);
        return data with
        {
            MapEntries = data.MapEntries is null
                ? null
                : new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(data.MapEntries, StringComparer.Ordinal)),
            StringValues = data.StringValues is null ? null : ReadOnly(data.StringValues),
            IntValues = data.IntValues is null ? null : ReadOnly(data.IntValues),
            HighScores = data.HighScores is null
                ? null
                : ReadOnly(data.HighScores.Select(score => score with
                {
                    Names = ReadOnly(score.Names)
                }))
        };
    }

    private static WiredInputSourcesSnapshot SnapshotOf(InputSourcesConf value) => new(
        ReadOnly(value.AllowedFurniSources.Select(ReadOnly)),
        ReadOnly(value.AllowedUserSources.Select(ReadOnly)),
        ReadOnly(value.DefaultFurniSources),
        ReadOnly(value.DefaultUserSources));

    private static IReadOnlyList<WiredContextEntrySnapshot> SnapshotOf(WiredContext value) =>
        ReadOnly(value.Entries.Select(entry => SnapshotOf(entry.Tag, entry.Value)));

    private static WiredContextEntrySnapshot SnapshotOf(
        int tag,
        IWiredContextEntry value) => (tag, value) switch
        {
            (WiredContext.TagRoomVariables, AllVariablesInRoom room) => new(
                tag,
                WiredContextValueKind.RoomVariables,
                room.Hash,
                null,
                null,
                null,
                null,
                null,
                null),
            (WiredContext.TagFurniVariableInfo, VariableInfoAndHolders holders) => new(
                tag,
                WiredContextValueKind.FurniVariable,
                null,
                ContextSnapshotOf(holders.Variable),
                ReadOnly(holders.Holders.Select(item =>
                    new WiredObjectValueSnapshot(item.ObjectId, item.Value))),
                null,
                null,
                null,
                null),
            (WiredContext.TagUserVariableInfo, VariableInfoAndHolders holders) => new(
                tag,
                WiredContextValueKind.UserVariable,
                null,
                ContextSnapshotOf(holders.Variable),
                ReadOnly(holders.Holders.Select(item =>
                    new WiredObjectValueSnapshot(item.ObjectId, item.Value))),
                null,
                null,
                null,
                null),
            (WiredContext.TagGlobalVariableInfo, VariableInfoAndValue current) => new(
                tag,
                WiredContextValueKind.GlobalVariable,
                null,
                ContextSnapshotOf(current.Variable),
                null,
                current.Value,
                null,
                null,
                null),
            (WiredContext.TagReferenceVariables, SharedVariableList shared) => new(
                tag,
                WiredContextValueKind.ReferenceVariables,
                null,
                null,
                null,
                null,
                ReadOnly(shared.SharedVariables.Select(item =>
                    SnapshotOf(item))),
                null,
                null),
            (WiredContext.TagRulesetVariables, VariableList variables) => new(
                tag,
                WiredContextValueKind.RulesetVariables,
                null,
                null,
                null,
                null,
                null,
                ReadOnly(variables.Variables.Select(ContextSnapshotOf)),
                null),
            (WiredContext.TagReferencePlaceholders, SharedGlobalPlaceholderList placeholders) => new(
                tag,
                WiredContextValueKind.ReferencePlaceholders,
                null,
                null,
                null,
                null,
                null,
                null,
                ReadOnly(placeholders.SharedPlaceholders.Select(item =>
                    new WiredSharedPlaceholderSnapshot(
                        item.RoomId,
                        item.RoomName,
                        item.PlaceholderName)))),
            _ => throw new InvalidDataException(
                $"Unsupported Wired context tag {tag} with value '{value.GetType().FullName}'.")
        };

    private static WiredVariable CloneOf(WiredVariable value) => new()
    {
        VariableId = value.VariableId,
        VariableType = value.VariableType,
        VariableName = value.VariableName,
        AvailabilityType = value.AvailabilityType,
        VariableTarget = value.VariableTarget,
        AlwaysAvailable = value.AlwaysAvailable,
        CanCreateAndDelete = value.CanCreateAndDelete,
        HasValue = value.HasValue,
        CanWriteValue = value.CanWriteValue,
        CanInterceptChanges = value.CanInterceptChanges,
        IsInvisible = value.IsInvisible,
        CanReadCreationTime = value.CanReadCreationTime,
        CanReadLastUpdateTime = value.CanReadLastUpdateTime,
        TextConnector = value.TextConnector is null ? null : ReadOnly(value.TextConnector)
    };

    private static WiredSharedVariableSnapshot SnapshotOf(SharedVariable value)
    {
        WiredVariable variable = value.WiredVariable;
        return new WiredSharedVariableSnapshot(
            value.RoomId,
            value.RoomName,
            variable.VariableId,
            variable.VariableType,
            variable.VariableName,
            variable.AvailabilityType,
            variable.VariableTarget,
            variable.AlwaysAvailable,
            variable.CanCreateAndDelete,
            variable.HasValue,
            variable.CanWriteValue,
            variable.CanInterceptChanges,
            variable.IsInvisible,
            variable.CanReadCreationTime,
            variable.CanReadLastUpdateTime,
            variable.TextConnector is null
                ? null
                : ReadOnly(variable.TextConnector.Select(pair => pair.Key)),
            variable.TextConnector is null
                ? null
                : ReadOnly(variable.TextConnector.Select(pair => pair.Value)));
    }

    private static WiredContextVariableSnapshot ContextSnapshotOf(WiredVariable value) => new(
        value.VariableId,
        value.VariableType,
        value.VariableName,
        value.AvailabilityType,
        value.VariableTarget,
        value.AlwaysAvailable,
        value.CanCreateAndDelete,
        value.HasValue,
        value.CanWriteValue,
        value.CanInterceptChanges,
        value.IsInvisible,
        value.CanReadCreationTime,
        value.CanReadLastUpdateTime,
        value.TextConnector is null
            ? null
            : ReadOnly(value.TextConnector.Select(pair => pair.Key)),
        value.TextConnector is null
            ? null
            : ReadOnly(value.TextConnector.Select(pair => pair.Value)));

    private static VariableInfoAndHolders CloneOf(VariableInfoAndHolders value) => new(
        CloneOf(value.Variable),
        ReadOnly(value.Holders));

    private static TradeRequirement SnapshotOf(TradeRequirement value) => new(
        value.Type,
        value.YouGetText,
        value.LayoutType,
        value.Rules is null ? null : new TradeRequirementRules(
            SnapshotOf(value.Rules.Definition),
            value.Rules.Type,
            value.Rules.Multiplier,
            value.Rules.AutoMultiplierMax));

    private static TradeRequirementRulesDefinition SnapshotOf(
        TradeRequirementRulesDefinition value) => new(
        value.YouGiveRule is null
            ? null
            : ReadOnly(value.YouGiveRule.Select(SnapshotOf)),
        value.YouGetRule is null ? null : SnapshotOf(value.YouGetRule));

    private static TradeRequirementRule SnapshotOf(TradeRequirementRule value) => new(
        ReadOnly(value.Nodes));

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private sealed class ChestState
    {
        private readonly Dictionary<int, IReadOnlyList<WiredChestStorageSnapshot>> fragments = [];
        private readonly Dictionary<int, WiredChestStorageSnapshot?> changes = [];
        private IReadOnlyList<WiredChestStorageSnapshot> items = [];

        public int? Coins { get; set; }
        public int ExpectedFragments { get; private set; }
        public bool ItemsComplete { get; private set; }
        public UpgradeChestResult? LastUpgradeResult { get; set; }
        public ChestPreferencesUpdateSuccess? LastPreferencesResult { get; set; }

        public void Apply(WiredChestItemsChunkSnapshot value)
        {
            if (value.TotalFragments <= 0 || value.FragmentNo < 0 || value.FragmentNo >= value.TotalFragments)
                return;
            if (value.FragmentNo == 0)
            {
                fragments.Clear();
                changes.Clear();
                ExpectedFragments = value.TotalFragments;
                ItemsComplete = false;
            }
            else if (!fragments.ContainsKey(0) || ExpectedFragments != value.TotalFragments ||
                fragments.ContainsKey(value.FragmentNo))
                return;
            fragments[value.FragmentNo] = value.StorageChunk;
            Dictionary<int, WiredChestStorageSnapshot> current = fragments.OrderBy(pair => pair.Key)
                .SelectMany(pair => pair.Value)
                .GroupBy(item => item.InventoryId)
                .ToDictionary(group => group.Key, group => group.First());
            foreach ((int id, WiredChestStorageSnapshot? item) in changes)
            {
                if (item is null)
                    current.Remove(id);
                else
                    current[id] = item;
            }
            items = ReadOnly(current.Values);
            ItemsComplete = fragments.Count == ExpectedFragments;
        }

        public void Apply(WiredChestItemsUpdatedSnapshot value)
        {
            Dictionary<int, WiredChestStorageSnapshot> current = items.ToDictionary(item => item.InventoryId);
            foreach (int id in value.RemovedIds)
            {
                current.Remove(id);
                changes[id] = null;
            }
            foreach (WiredChestStorageSnapshot item in value.AddedStorage)
            {
                if (current.TryAdd(item.InventoryId, item))
                    changes[item.InventoryId] = item;
            }
            items = ReadOnly(current.Values);
        }

        public WiredChestContentsSnapshot Snapshot(Id chest_id) => new(
            chest_id,
            Coins,
            items,
            ItemsComplete,
            ExpectedFragments,
            ReadOnly(fragments.Keys.Order()),
            LastUpgradeResult,
            LastPreferencesResult);
    }
}

/// <summary>Represents one fragment of a wired chest's item list.</summary>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="TotalFragments">The number of fragments the item list is sent in.</param>
/// <param name="FragmentNo">The zero-based number of this fragment.</param>
/// <param name="StorageChunk">The items in this fragment.</param>
public sealed record WiredChestItemsChunkSnapshot(
    int ChestId,
    int TotalFragments,
    int FragmentNo,
    IReadOnlyList<WiredChestStorageSnapshot> StorageChunk);

/// <summary>Represents a change to a wired chest's item list.</summary>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="RemovedIds">The inventory ids of the items removed from the chest.</param>
/// <param name="AddedStorage">New items to add; an existing ID is ignored unless removed in the same update.</param>
public sealed record WiredChestItemsUpdatedSnapshot(
    int ChestId,
    IReadOnlyList<int> RemovedIds,
    IReadOnlyList<WiredChestStorageSnapshot> AddedStorage);
