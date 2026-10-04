using Qx.Model.Wired;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read the wired state of the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredState"/>. The state is read from the current wired
/// snapshot without sending anything to the hotel. Chests are ordered by chest id.
/// </remarks>
/// <param name="ChestOffset">The zero-based index of the first chest to return.</param>
/// <param name="ChestLimit">The maximum number of chests to return, from 0 to 10.</param>
/// <param name="ItemOffset">The zero-based index of the first item to return within each returned chest.</param>
/// <param name="ItemLimit">The maximum number of items to return per chest, from 0 to 50.</param>
public sealed record WiredStateRequest(
    int ChestOffset = 0,
    int ChestLimit = 5,
    int ItemOffset = 0,
    int ItemLimit = 20);

/// <summary>
/// Represents the known contents of one wired chest with a page of its items.
/// </summary>
/// <param name="ChestId">The id of the chest.</param>
/// <param name="Coins">The coins in the chest, or <see langword="null"/> when the hotel has not sent them.</param>
/// <param name="TotalItems">The number of items known in the chest.</param>
/// <param name="ItemOffset">The zero-based index of the first item in <paramref name="Items"/>.</param>
/// <param name="ItemLimit">The maximum number of items that were requested.</param>
/// <param name="Items">The page of items, merged from the fragments and updates received so far.</param>
/// <param name="ItemsComplete">Whether every fragment of the item list has been received.</param>
/// <param name="ExpectedFragments">The number of fragments the item list is sent in.</param>
/// <param name="ReceivedFragments">The fragment numbers received, in ascending order.</param>
/// <param name="LastUpgradeResult">The result of the last capacity upgrade of the chest, or <see langword="null"/>.</param>
/// <param name="LastPreferencesResult">The result of the last preferences update of the chest, or <see langword="null"/>.</param>
public sealed record WiredChestStateEntry(
    Id ChestId,
    int? Coins,
    int TotalItems,
    int ItemOffset,
    int ItemLimit,
    IReadOnlyList<WiredChestStorageSnapshot> Items,
    bool ItemsComplete,
    int ExpectedFragments,
    IReadOnlyList<int> ReceivedFragments,
    UpgradeChestResult? LastUpgradeResult,
    ChestPreferencesUpdateSuccess? LastPreferencesResult);

/// <summary>
/// Represents a page of the wired chests seen in the current room.
/// </summary>
/// <param name="TotalChests">The number of chests seen in the room.</param>
/// <param name="ChestOffset">The zero-based index of the first chest in <paramref name="Entries"/>.</param>
/// <param name="ChestLimit">The maximum number of chests that were requested.</param>
/// <param name="ItemOffset">The zero-based index of the first item returned within each chest.</param>
/// <param name="ItemLimit">The maximum number of items that were requested per chest.</param>
/// <param name="Entries">The chests in the page, ordered by chest id.</param>
public sealed record WiredChestStatePage(
    int TotalChests,
    int ChestOffset,
    int ChestLimit,
    int ItemOffset,
    int ItemLimit,
    IReadOnlyList<WiredChestStateEntry> Entries);

/// <summary>
/// Represents the wired state of the current room.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredState"/>. Wired messages are only recorded while
/// the user is in a room, and the state is cleared when a room is entered or left.
/// </remarks>
/// <param name="Generation">The wired state generation, increased each time the state is cleared.</param>
/// <param name="Revision">The wired state revision, increased on every change.</param>
/// <param name="Permissions">The user's wired permissions in the room, or <see langword="null"/> when not received.</param>
/// <param name="Environment">The wired environment of the room, or <see langword="null"/> when not received.</param>
/// <param name="ClickSettings">The wired click settings of the room, or <see langword="null"/> when not received.</param>
/// <param name="RoomSettings">The wired settings of the room, or <see langword="null"/> when not received.</param>
/// <param name="OpenFurniId">
/// The id of the wired furni item whose configuration was opened last, or <see langword="null"/> when none was opened.
/// </param>
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
/// <param name="LastMenuError">The last wired menu error, or <see langword="null"/> when none was received.</param>
/// <param name="LastRewardResult">The last wired reward result, or <see langword="null"/> when none was received.</param>
/// <param name="LastOpenedChestId">The id of the chest the hotel opened last, or <see langword="null"/> when none was opened.</param>
/// <param name="Chests">The requested page of the chests seen in the room.</param>
/// <param name="Contract">The state of the wired contract.</param>
/// <param name="Trade">The state of the wired trade.</param>
public sealed record WiredStateView(
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
    WiredChestStatePage Chests,
    WiredContractSnapshot Contract,
    WiredTradeSnapshot Trade)
{
    /// <summary>
    /// Gets whether the user can modify wired in the room.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> when the permissions have not been received.
    /// </remarks>
    public bool CanModify => Permissions?.CanModify is true;
    /// <summary>
    /// Gets whether the user can read wired in the room.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> when the permissions have not been received.
    /// </remarks>
    public bool CanRead => Permissions?.CanRead is true;
}

/// <summary>
/// Represents a wired request that takes only a response timeout.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesHashGet"/>, <see cref="ApplicationMemberIds.WiredRoomSettingsGet"/>,
/// <see cref="ApplicationMemberIds.WiredRoomStatsGet"/> and <see cref="ApplicationMemberIds.WiredRoomErrorLogsGet"/>.
/// The request needs an active hotel session and room, and fails when the session, the room or the
/// wired state generation changes before the response arrives.
/// </remarks>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredTimeoutRequest(int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to fetch the configuration of one wired furni item.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredConfigurationGet"/>. The configuration open request is
/// sent and the call completes with the first configuration received for the furni item. Only one
/// configuration fetch runs at a time, and the wait for earlier fetches counts toward the timeout.
/// </remarks>
/// <param name="FurniId">The id of the wired furni item, which must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the configuration in milliseconds, from 1 to 120000.</param>
public sealed record WiredConfigurationGetRequest(
    Id FurniId,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to open the configuration of one wired furni item without waiting for it.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredConfigurationOpen"/>. The configuration arrives through
/// <see cref="ApplicationMemberIds.WiredConfigurationReceived"/> and the wired state.
/// </remarks>
/// <param name="FurniId">The id of the wired furni item, which must be positive.</param>
public sealed record WiredConfigurationOpenRequest(Id FurniId);

/// <summary>
/// Represents a request to store the current state of one wired furni item as its restore snapshot.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredConfigurationSnapshotApply"/>. The message is sent without
/// waiting for a response.
/// </remarks>
/// <param name="FurniId">The id of the wired furni item, which must be positive.</param>
public sealed record WiredConfigurationApplySnapshotRequest(Id FurniId);

/// <summary>
/// Represents a request to replace the configuration of a wired trigger.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredConfigurationTriggerSave"/>. The call completes with the
/// next save success or validation failure the hotel sends. Only one configuration save runs at a time.
/// </remarks>
/// <param name="Update">The complete replacement configuration, whose furni id must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the save result in milliseconds, from 1 to 120000.</param>
public sealed record WiredTriggerSaveRequest(
    UpdateTrigger Update,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to replace the configuration of a wired action.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredConfigurationActionSave"/>. The call completes with the
/// next save success or validation failure the hotel sends. Only one configuration save runs at a time.
/// </remarks>
/// <param name="Update">The complete replacement configuration, whose furni id must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the save result in milliseconds, from 1 to 120000.</param>
public sealed record WiredActionSaveRequest(
    UpdateAction Update,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to replace the configuration of a wired condition.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredConfigurationConditionSave"/>. The call completes with the
/// next save success or validation failure the hotel sends. Only one configuration save runs at a time.
/// </remarks>
/// <param name="Update">The complete replacement configuration, whose furni id must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the save result in milliseconds, from 1 to 120000.</param>
public sealed record WiredConditionSaveRequest(
    UpdateCondition Update,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to replace the configuration of a wired selector.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredConfigurationSelectorSave"/>. The call completes with the
/// next save success or validation failure the hotel sends. Only one configuration save runs at a time.
/// </remarks>
/// <param name="Update">The complete replacement configuration, whose furni id must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the save result in milliseconds, from 1 to 120000.</param>
public sealed record WiredSelectorSaveRequest(
    UpdateSelector Update,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to replace the configuration of a wired add-on.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredConfigurationAddonSave"/>. The call completes with the
/// next save success or validation failure the hotel sends. Only one configuration save runs at a time.
/// </remarks>
/// <param name="Update">The complete replacement configuration, whose furni id must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the save result in milliseconds, from 1 to 120000.</param>
public sealed record WiredAddonSaveRequest(
    UpdateAddon Update,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to replace the configuration of a wired variable box.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredConfigurationVariableSave"/>. The call completes with the
/// next save success or validation failure the hotel sends. Only one configuration save runs at a time.
/// </remarks>
/// <param name="Update">The complete replacement configuration, whose furni id must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the save result in milliseconds, from 1 to 120000.</param>
public sealed record WiredVariableSaveRequest(
    UpdateVariable Update,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents the hotel's answer to a wired configuration save.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredConfigurationTriggerSave"/>,
/// <see cref="ApplicationMemberIds.WiredConfigurationActionSave"/>, <see cref="ApplicationMemberIds.WiredConfigurationConditionSave"/>,
/// <see cref="ApplicationMemberIds.WiredConfigurationSelectorSave"/>, <see cref="ApplicationMemberIds.WiredConfigurationAddonSave"/>
/// and <see cref="ApplicationMemberIds.WiredConfigurationVariableSave"/>, and published by
/// <see cref="ApplicationMemberIds.WiredConfigurationSaveResult"/>.
/// </remarks>
/// <param name="Success">Whether the save succeeded.</param>
/// <param name="ValidationError">
/// The validation error of a failed save, or <see langword="null"/> when <paramref name="Success"/> is
/// <see langword="true"/>.
/// </param>
/// <param name="Generation">The wired state generation the answer was recorded in.</param>
/// <param name="Revision">The wired state revision after the answer was recorded.</param>
public sealed record WiredConfigurationSaveResult(
    bool Success,
    WiredValidationError? ValidationError,
    long Generation,
    long Revision);

/// <summary>
/// Represents a request to fetch the wired variable definitions that differ from a cached set.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesDifferencesGet"/>. The hotel can answer in
/// several chunks and only the first chunk is returned. Use
/// <see cref="ApplicationMemberIds.WiredVariablesList"/> to read every chunk.
/// </remarks>
/// <param name="Cache">
/// The variable ids and per-variable hashes already known, with unique non-empty ids, or
/// <see langword="null"/> to receive every variable.
/// </param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredVariableDifferencesRequest(
    IReadOnlyList<VariableHashEntry>? Cache = null,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to read every wired variable definition in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesList"/>. A difference request with an empty
/// cache is sent and chunks are read until the hotel marks the last one. The call fails when more than
/// <paramref name="MaximumChunks"/> chunks arrive. Only one variable listing runs at a time.
/// </remarks>
/// <param name="MaximumChunks">The maximum number of difference chunks to read, from 1 to 256.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for every chunk in milliseconds, from 1 to 120000.</param>
public sealed record WiredVariableListRequest(
    int MaximumChunks = 64,
    int TimeoutMilliseconds = 30000);

/// <summary>
/// Represents every wired variable definition in the current room.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredVariablesList"/>.
/// </remarks>
/// <param name="Generation">The wired state generation the variables were read in.</param>
/// <param name="AllVariablesHash">The hash of all variables sent with the last chunk.</param>
/// <param name="Chunks">The number of difference chunks that were read.</param>
/// <param name="Variables">The variable definitions with their per-variable hashes, in the order they first arrived.</param>
/// <param name="Cache">
/// The variable ids and per-variable hashes of <paramref name="Variables"/>, to pass back to
/// <see cref="ApplicationMemberIds.WiredVariablesDifferencesGet"/>.
/// </param>
public sealed record WiredVariableCollectionSnapshot(
    long Generation,
    int AllVariablesHash,
    int Chunks,
    IReadOnlyList<WiredVariableWithHashSnapshot> Variables,
    IReadOnlyList<VariableHashEntry> Cache);

/// <summary>
/// Represents a wired variable definition with its per-variable hash.
/// </summary>
/// <param name="PerVariableHash">The hash of the variable definition.</param>
/// <param name="Variable">The variable definition.</param>
public sealed record WiredVariableWithHashSnapshot(
    int PerVariableHash,
    WiredVariableSnapshot Variable);

/// <summary>
/// Represents one chunk of the wired variable definitions that differ from a cached set.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredVariablesDifferencesGet"/>.
/// </remarks>
/// <param name="Generation">The wired state generation the chunk was read in.</param>
/// <param name="AllVariablesHash">The hash of all variables in the room.</param>
/// <param name="IsLastChunk">Whether the chunk is the last one of the answer.</param>
/// <param name="RemovedVariables">The ids of the cached variables that no longer exist.</param>
/// <param name="AddedOrUpdated">The variables that are new or changed, with their per-variable hashes.</param>
public sealed record WiredVariableDifferencesSnapshot(
    long Generation,
    int AllVariablesHash,
    bool IsLastChunk,
    IReadOnlyList<string> RemovedVariables,
    IReadOnlyList<WiredVariableWithHashSnapshot> AddedOrUpdated);

/// <summary>
/// Represents the value of one wired variable held by an object.
/// </summary>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="Value">The variable value.</param>
public sealed record WiredVariableValueSnapshot(string VariableId, int Value);

/// <summary>
/// Represents the wired variable values held by one furni item, room user or the global scope.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredVariablesObjectGet"/>.
/// </remarks>
/// <param name="Generation">The wired state generation the values were read in.</param>
/// <param name="Target">The kind of object that holds the values.</param>
/// <param name="ObjectId">The furni id for a furni target, the room index for a user target and 0 for the global scope.</param>
/// <param name="Values">The variable values the object holds.</param>
/// <param name="ConfiguredInWireds">
/// The ids the hotel lists as the wireds the furni item is configured in, empty for user and global targets.
/// </param>
public sealed record WiredVariablesObjectSnapshot(
    long Generation,
    WiredTarget Target,
    int ObjectId,
    IReadOnlyList<WiredVariableValueSnapshot> Values,
    IReadOnlyList<int> ConfiguredInWireds);

/// <summary>
/// Represents a wired variable definition with every object that holds its value.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredVariablesHoldersGet"/>.
/// </remarks>
/// <param name="Generation">The wired state generation the holders were read in.</param>
/// <param name="LeadingValue">The leading value the hotel sends before the variable, which the game client ignores.</param>
/// <param name="Variable">The variable definition.</param>
/// <param name="Holders">The furni items or users that hold the variable, with their values.</param>
public sealed record WiredVariableHoldersSnapshot(
    long Generation,
    int LeadingValue,
    WiredVariableSnapshot Variable,
    IReadOnlyList<WiredObjectValueSnapshot> Holders);

/// <summary>
/// Represents one stored value of a permanent wired variable.
/// </summary>
/// <param name="VariableId">
/// The id of the variable, or <see langword="null"/> in an owner page, where the hotel does not send it.
/// </param>
/// <param name="Value">The stored value.</param>
/// <param name="CreationTime">The creation time as the hotel sends it.</param>
/// <param name="CreationTimeText">The creation time as text formatted by the hotel.</param>
/// <param name="LastUpdateTime">The last update time as the hotel sends it.</param>
/// <param name="LastUpdateTimeText">The last update time as text formatted by the hotel.</param>
public sealed record WiredVariableStorageSnapshot(
    string? VariableId,
    int Value,
    long CreationTime,
    string CreationTimeText,
    long LastUpdateTime,
    string LastUpdateTimeText);

/// <summary>
/// Represents the permanent wired variables stored for one entity.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredVariablesPermanentGet"/>.
/// </remarks>
/// <param name="EntityType">The entity type code, where 1 is a user.</param>
/// <param name="EntityId">The id of the entity.</param>
/// <param name="EntityName">The name of the entity.</param>
/// <param name="EntityFigure">The figure string of the entity.</param>
/// <param name="OwnerId">The id of the entity's owner, or <see langword="null"/> when the entity is a user.</param>
/// <param name="OwnerName">The name of the entity's owner, or <see langword="null"/> when the entity is a user.</param>
/// <param name="OwnerFigure">The figure string of the entity's owner, or <see langword="null"/> when the entity is a user.</param>
/// <param name="Variables">The stored variable values, each with its variable id.</param>
public sealed record WiredPermanentVariablesSnapshot(
    int EntityType,
    int EntityId,
    string EntityName,
    string EntityFigure,
    int? OwnerId,
    string? OwnerName,
    string? OwnerFigure,
    IReadOnlyList<WiredVariableStorageSnapshot> Variables);

/// <summary>
/// Represents one entity that owns a value of a permanent wired variable.
/// </summary>
/// <param name="EntityType">The entity type code, where 1 is a user.</param>
/// <param name="EntityId">The id of the entity.</param>
/// <param name="EntityName">The name of the entity.</param>
/// <param name="Storage">The value the entity stores, without its variable id.</param>
public sealed record WiredVariableOwnerSnapshot(
    int EntityType,
    int EntityId,
    string EntityName,
    WiredVariableStorageSnapshot Storage);

/// <summary>
/// Represents one page of the entities that own a permanent wired variable.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredVariablesOwnersGet"/>.
/// </remarks>
/// <param name="VariableId">The id of the variable.</param>
/// <param name="TotalEntries">The number of owners across every page.</param>
/// <param name="CurrentPage">The one-based number of the page.</param>
/// <param name="Amount">The amount value the hotel sends with the page.</param>
/// <param name="Owners">The owners in the page.</param>
/// <param name="UserTypeFilter">The entity type filter the hotel applied, where -1 shows every entity type.</param>
/// <param name="SortTypeFilter">The sort order the hotel applied.</param>
public sealed record WiredVariableOwnersSnapshot(
    string VariableId,
    int TotalEntries,
    int CurrentPage,
    int Amount,
    IReadOnlyList<WiredVariableOwnerSnapshot> Owners,
    int UserTypeFilter,
    int SortTypeFilter);

/// <summary>
/// Represents a request to read the wired variable values held by one object.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesObjectGet"/>. The call completes with the first
/// response for the same target and object.
/// </remarks>
/// <param name="Target">
/// The kind of object to inspect, which must be <see cref="WiredTarget.Furni"/>, <see cref="WiredTarget.User"/>
/// or <see cref="WiredTarget.Global"/>.
/// </param>
/// <param name="ObjectId">
/// The furni id for a furni target, the non-negative room index for a user target, or 0 for the global scope.
/// </param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredVariablesObjectRequest(
    WiredTarget Target,
    int ObjectId,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to read a wired variable definition with every object that holds it.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesHoldersGet"/>. The call completes with the first
/// response for the same variable.
/// </remarks>
/// <param name="VariableId">The id of the variable, which must not be empty.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredVariableHoldersRequest(
    string VariableId,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to remove one wired variable from every furni item and user that holds it.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesHoldersDelete"/>. The message is sent without
/// waiting for a response. The game client only offers this with wired modify rights, for a persisted
/// furni or user variable that can be created and deleted.
/// </remarks>
/// <param name="VariableId">The id of the variable, which must not be empty.</param>
public sealed record WiredVariableHoldersDeleteRequest(string VariableId);

/// <summary>
/// Represents a request to read the permanent wired variables stored for one entity.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesPermanentGet"/>. The call completes with the first
/// response for the same entity type and id.
/// </remarks>
/// <param name="EntityType">The entity type code, which must not be 0. 1 is a user.</param>
/// <param name="EntityId">The id of the entity, which must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredPermanentVariablesRequest(
    int EntityType,
    int EntityId,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to read one page of the entities that own a permanent wired variable.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesOwnersGet"/>. The call completes with the first
/// page that echoes the same variable, page number and filters.
/// </remarks>
/// <param name="VariableId">The id of the variable, which must not be empty.</param>
/// <param name="Page">The one-based page number.</param>
/// <param name="PageSize">The number of rows to request, from 1 to 250.</param>
/// <param name="SortTypeFilter">The sort order of the owner table. The game client starts with 0.</param>
/// <param name="UserTypeFilter">The entity type filter of the owner table, or -1 for every entity type.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredVariableOwnersRequest(
    string VariableId,
    int Page = 1,
    int PageSize = 50,
    int SortTypeFilter = 0,
    int UserTypeFilter = -1,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to write, create or delete one wired variable value on an object.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesObjectSet"/>. The message is sent without waiting
/// for a response.
/// </remarks>
/// <param name="Target">
/// The kind of object, which must be <see cref="WiredTarget.Furni"/>, <see cref="WiredTarget.User"/> or
/// <see cref="WiredTarget.Global"/>.
/// </param>
/// <param name="ObjectId">
/// The furni id for a furni target, the non-negative room index for a user target, or 0 for the global scope.
/// </param>
/// <param name="VariableId">The id of the variable, which must not be empty.</param>
/// <param name="Value">The value to write, ignored when the variable is deleted.</param>
/// <param name="Operation">
/// The operation, <see cref="WiredVariableOperation.Write"/> (0), <see cref="WiredVariableOperation.Create"/> (1)
/// or <see cref="WiredVariableOperation.Delete"/> (2).
/// </param>
public sealed record WiredObjectVariableSetRequest(
    WiredTarget Target,
    int ObjectId,
    string VariableId,
    int Value,
    int Operation = WiredVariableOperation.Write);

/// <summary>
/// Represents a request to write, create or delete one permanent wired variable and wait for the result.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesPermanentSet"/>. The call completes with the next
/// permanent variable result the hotel sends. <see cref="WiredPermanentVariableSendRequest"/> sends the
/// same message without waiting.
/// </remarks>
/// <param name="EntityType">The entity type code, which must not be 0. 1 is a user.</param>
/// <param name="EntityId">The id of the entity, which must be positive.</param>
/// <param name="VariableId">The id of the variable, which must not be empty.</param>
/// <param name="Value">The value to write, ignored when the variable is deleted.</param>
/// <param name="Operation">
/// The operation, <see cref="WiredVariableOperation.Write"/> (0), <see cref="WiredVariableOperation.Create"/> (1)
/// or <see cref="WiredVariableOperation.Delete"/> (2).
/// </param>
/// <param name="TimeoutMilliseconds">The total time to wait for the result in milliseconds, from 1 to 120000.</param>
public sealed record WiredPermanentVariableSetRequest(
    int EntityType,
    int EntityId,
    string VariableId,
    int Value,
    int Operation = WiredVariableOperation.Write,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to write, create or delete one permanent wired variable without waiting for the result.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredVariablesPermanentSetSend"/>. The result arrives through
/// <see cref="ApplicationMemberIds.WiredChanged"/> as a <see cref="WiredChangeKind.PermanentVariableSetResult"/> change.
/// </remarks>
/// <param name="EntityType">The entity type code, which must not be 0. 1 is a user.</param>
/// <param name="EntityId">The id of the entity, which must be positive.</param>
/// <param name="VariableId">The id of the variable, which must not be empty.</param>
/// <param name="Value">The value to write, ignored when the variable is deleted.</param>
/// <param name="Operation">
/// The operation, <see cref="WiredVariableOperation.Write"/> (0), <see cref="WiredVariableOperation.Create"/> (1)
/// or <see cref="WiredVariableOperation.Delete"/> (2).
/// </param>
public sealed record WiredPermanentVariableSendRequest(
    int EntityType,
    int EntityId,
    string VariableId,
    int Value,
    int Operation = WiredVariableOperation.Write);

/// <summary>
/// Represents a request to replace the wired permission masks and timezone of the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredRoomSettingsSet"/>. The call completes when the hotel sends
/// room settings that match all three values.
/// </remarks>
/// <param name="ModifyPermissionMask">The mask of who may modify wired in the room.</param>
/// <param name="ReadPermissionMask">The mask of who may read wired in the room.</param>
/// <param name="Timezone">The wired timezone of the room, which must not be empty.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the settings in milliseconds, from 1 to 120000.</param>
public sealed record WiredRoomSettingsSetRequest(
    int ModifyPermissionMask,
    int ReadPermissionMask,
    string Timezone,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to read one page of the wired log of the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredRoomLogsGet"/>. The call completes with the first page that
/// echoes the same page number, filters and query.
/// </remarks>
/// <param name="Page">The one-based page number.</param>
/// <param name="PageSize">The number of log rows to request, from 1 to 250.</param>
/// <param name="LogLevelFilter">The log level to keep, or -1 for no level filter.</param>
/// <param name="LogSourceFilter">The log source to keep, or -1 for no source filter.</param>
/// <param name="Query">The text to search for, or an empty string for no text filter.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredRoomLogsRequest(
    int Page = 1,
    int PageSize = 50,
    int LogLevelFilter = -1,
    int LogSourceFilter = -1,
    string Query = "",
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to send a wired user click for one room user.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredRoomUserClick"/>. The call completes with the first answer
/// for the same room index, which says whether the wired user menu should open.
/// </remarks>
/// <param name="Index">The room index of the user, which must not be negative.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the answer in milliseconds, from 1 to 120000.</param>
public sealed record WiredUserClickRequest(
    int Index,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to update the user's wired preferences.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredPreferencesSet"/>. The message is sent without waiting for a
/// response.
/// </remarks>
/// <param name="Preferences">The complete preference set.</param>
public sealed record WiredPreferencesSetRequest(WiredSetPreferences Preferences);

/// <summary>
/// Represents a request that targets one wired chest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredChestOpen"/>, <see cref="ApplicationMemberIds.WiredChestClose"/>,
/// <see cref="ApplicationMemberIds.WiredChestWithdrawAll"/> and <see cref="ApplicationMemberIds.WiredChestAddStart"/>.
/// The message is sent without waiting for a response.
/// </remarks>
/// <param name="ChestId">The id of the chest, which must be positive.</param>
public sealed record WiredChestRequest(Id ChestId);

/// <summary>
/// Represents a request to lock or unlock wired chests in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredChestsLock"/>. The message is sent without waiting for a
/// response.
/// </remarks>
/// <param name="Locked">Whether the chests are locked.</param>
/// <param name="ApplyToAllInRoom">
/// Whether to apply the change to every chest in the room instead of only the user's own chests in the room.
/// </param>
public sealed record WiredChestsLockRequest(
    bool Locked,
    bool ApplyToAllInRoom = false);

/// <summary>
/// Represents a request to buy capacity upgrades for a wired chest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredChestUpgrade"/>. The message is sent without waiting, and
/// the outcome arrives through <see cref="ApplicationMemberIds.WiredChestUpgradeResult"/>.
/// </remarks>
/// <param name="ChestId">The id of the chest, which must be positive.</param>
/// <param name="UpgradeAmount">The number of upgrades to buy, which must be positive.</param>
public sealed record WiredChestUpgradeRequest(
    int ChestId,
    int UpgradeAmount);

/// <summary>
/// Represents a request to withdraw coins from a wired chest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredChestWithdrawCoins"/>. The message is sent without waiting
/// for a response.
/// </remarks>
/// <param name="ChestId">The id of the chest, which must be positive.</param>
/// <param name="CoinAmount">The number of coins to withdraw, which must be positive.</param>
public sealed record WiredChestCoinsWithdrawRequest(
    Id ChestId,
    int CoinAmount);

/// <summary>
/// Represents a request to withdraw items of one furni type from a wired chest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredChestWithdrawItems"/>. The message is sent without waiting,
/// and the change arrives as a chest contents update.
/// </remarks>
/// <param name="ChestId">The id of the chest, which must be positive.</param>
/// <param name="ItemType">The furni type of the items to withdraw.</param>
/// <param name="Count">The number of items to withdraw, which must be positive.</param>
public sealed record WiredChestItemsWithdrawRequest(
    Id ChestId,
    ChestItemType ItemType,
    int Count);

/// <summary>
/// Represents a request to update the lock, auto-lock and capacity options of a wired chest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredChestOptionsSet"/>. The message is sent without waiting for
/// a response.
/// </remarks>
/// <param name="Options">The complete option set, with a positive chest id and a capacity that is not negative.</param>
public sealed record WiredChestOptionsSetRequest(SetChestOptions Options);

/// <summary>
/// Represents a request to update the general preferences of a wired chest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredChestPreferencesSet"/>. The message is sent without waiting,
/// and the hotel confirms through <see cref="ApplicationMemberIds.WiredChestPreferencesUpdated"/>.
/// </remarks>
/// <param name="Preferences">The complete preference set, with a positive chest id.</param>
public sealed record WiredChestPreferencesSetRequest(SetChestPreferences Preferences);

/// <summary>
/// Represents a request to update the notification preferences of a wired chest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredChestNotificationPreferencesSet"/>. The message is sent
/// without waiting, and the hotel confirms through <see cref="ApplicationMemberIds.WiredChestPreferencesUpdated"/>.
/// </remarks>
/// <param name="Preferences">The complete notification preference set, with a positive chest id.</param>
public sealed record WiredChestNotificationPreferencesSetRequest(
    SetChestNotificationPreferences Preferences);

/// <summary>
/// Represents a request to deposit inventory items into a wired chest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredChestDeposit"/>. The chest is opened and its item list
/// read, a chest trade is started, the items are offered, the trade is confirmed after a delay of three
/// seconds, and the call completes when the chest contents show every accepted item. The trade is
/// canceled when the call fails before it settles. Only one deposit runs at a time.
/// </remarks>
/// <param name="ChestId">The id of the chest, from 1 to <see cref="int.MaxValue"/>.</param>
/// <param name="InventoryIds">The inventory ids of the items, from 1 to 1000 distinct non-zero 32-bit ids.</param>
/// <param name="TimeoutMilliseconds">The total time for the whole deposit in milliseconds, from 1 to 120000.</param>
public sealed record WiredChestDepositRequest(
    Id ChestId,
    IReadOnlyList<Id> InventoryIds,
    int TimeoutMilliseconds = 30000);

/// <summary>
/// Represents the result of a wired chest deposit.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredChestDeposit"/>.
/// </remarks>
/// <param name="Success">Whether the accepted items were stored in the chest.</param>
/// <param name="Failure">The reason the deposit failed, or an empty string when it succeeded.</param>
/// <param name="Requested">The number of inventory items that were requested.</param>
/// <param name="Accepted">The number of requested items the chest trade accepted, or 0 when the deposit failed.</param>
/// <param name="Stored">
/// The chest storage entries of the accepted items, in the order they were accepted, or an empty list when
/// the deposit failed.
/// </param>
/// <param name="Generation">The wired state generation the deposit ended in.</param>
/// <param name="Revision">The wired state revision the deposit ended at.</param>
public sealed record WiredChestDepositResult(
    bool Success,
    string Failure,
    int Requested,
    int Accepted,
    IReadOnlyList<WiredChestStorageSnapshot> Stored,
    long Generation,
    long Revision);

/// <summary>
/// Represents a request to read one page of the transaction log of a wired chest.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredTransactionChestLogsGet"/>. The call completes with the first
/// chest log page that echoes the same log list id and page number.
/// </remarks>
/// <param name="LogListId">The id of the chest transaction log list, which must be positive.</param>
/// <param name="Page">The one-based page number.</param>
/// <param name="PageSize">The number of transaction rows to request, from 1 to 250.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredTransactionChestLogsRequest(
    int LogListId,
    int Page = 1,
    int PageSize = 50,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to read one page of the wired transaction log of the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredTransactionRoomLogsGet"/>. The call completes with the first
/// room log page that echoes the same page number.
/// </remarks>
/// <param name="Page">The one-based page number.</param>
/// <param name="PageSize">The number of transaction rows to request, from 1 to 250.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredTransactionRoomLogsRequest(
    int Page = 1,
    int PageSize = 50,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to read the details of one wired transaction.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredTransactionDetailsGet"/>. The call completes with the first
/// details for the same transaction id.
/// </remarks>
/// <param name="TransactionId">The 64-bit id of the transaction, which must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
public sealed record WiredTransactionDetailsRequest(
    long TransactionId,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to open a wired contract and wait for its contents.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredContractOpen"/>. The call completes with the first contents
/// for the same contract id. <see cref="WiredContractOpenSendRequest"/> sends the same message without waiting.
/// </remarks>
/// <param name="ContractId">The id of the contract, which must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the contents in milliseconds, from 1 to 120000.</param>
public sealed record WiredContractOpenRequest(
    int ContractId,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to open a wired contract without waiting for its contents.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredContractOpenSend"/>. The contents arrive through
/// <see cref="ApplicationMemberIds.WiredContractContentsReceived"/>.
/// </remarks>
/// <param name="ContractId">The id of the contract, which must be positive.</param>
public sealed record WiredContractOpenSendRequest(int ContractId);

/// <summary>
/// Represents a request to replace a wired contract and wait for the result.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredContractUpdate"/>. The call completes with the first update
/// result for the same contract id. <see cref="WiredContractSendRequest"/> sends the same message without waiting.
/// </remarks>
/// <param name="Contract">The complete contract definition, whose contract id must be positive.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the result in milliseconds, from 1 to 120000.</param>
public sealed record WiredContractUpdateRequest(
    WiredContractContents Contract,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to replace a wired contract without waiting for the result.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredContractUpdateSend"/>. The result arrives through
/// <see cref="ApplicationMemberIds.WiredContractUpdateResult"/>.
/// </remarks>
/// <param name="Contract">The complete contract definition, whose contract id must be positive.</param>
public sealed record WiredContractSendRequest(WiredContractContents Contract);

/// <summary>
/// Represents a request to add inventory items to or remove them from the active wired chest trade.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredTradeItemsAdd"/> and <see cref="ApplicationMemberIds.WiredTradeItemsRemove"/>.
/// The message is sent without waiting for a response.
/// </remarks>
/// <param name="InventoryIds">The inventory ids of the items, from 1 to 1000 distinct non-zero 32-bit ids.</param>
public sealed record WiredTradeItemsRequest(IReadOnlyList<Id> InventoryIds);

/// <summary>
/// Represents one explicit stage of confirming the active wired trade.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredTradeConfirm"/>. The message is sent without waiting, and
/// the outcome arrives as a trade completion or cancellation.
/// </remarks>
/// <param name="Confirm">False for initial acceptance; true only for final confirmation after the countdown.</param>
public sealed record WiredTradeConfirmRequest(bool Confirm = true);

/// <summary>
/// Represents a wired request that takes no arguments.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WiredRoomErrorLogsClear"/>, <see cref="ApplicationMemberIds.WiredRoomReload"/>,
/// <see cref="ApplicationMemberIds.WiredRoomRollback"/> and <see cref="ApplicationMemberIds.WiredTradeCancel"/>.
/// The message is sent without waiting for a response.
/// </remarks>
public sealed record WiredCommandRequest;

/// <summary>
/// Represents the result of sending a wired message without waiting for a response.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WiredConfigurationOpen"/>, <see cref="ApplicationMemberIds.WiredConfigurationSnapshotApply"/>,
/// <see cref="ApplicationMemberIds.WiredVariablesHoldersDelete"/>, <see cref="ApplicationMemberIds.WiredVariablesObjectSet"/>,
/// <see cref="ApplicationMemberIds.WiredVariablesPermanentSetSend"/>, <see cref="ApplicationMemberIds.WiredRoomErrorLogsClear"/>,
/// <see cref="ApplicationMemberIds.WiredRoomReload"/>, <see cref="ApplicationMemberIds.WiredRoomRollback"/>,
/// <see cref="ApplicationMemberIds.WiredPreferencesSet"/>, <see cref="ApplicationMemberIds.WiredChestOpen"/>,
/// <see cref="ApplicationMemberIds.WiredChestClose"/>, <see cref="ApplicationMemberIds.WiredChestsLock"/>,
/// <see cref="ApplicationMemberIds.WiredChestUpgrade"/>, <see cref="ApplicationMemberIds.WiredChestWithdrawAll"/>,
/// <see cref="ApplicationMemberIds.WiredChestWithdrawCoins"/>, <see cref="ApplicationMemberIds.WiredChestWithdrawItems"/>,
/// <see cref="ApplicationMemberIds.WiredChestAddStart"/>, <see cref="ApplicationMemberIds.WiredChestOptionsSet"/>,
/// <see cref="ApplicationMemberIds.WiredChestPreferencesSet"/>, <see cref="ApplicationMemberIds.WiredChestNotificationPreferencesSet"/>,
/// <see cref="ApplicationMemberIds.WiredContractOpenSend"/>, <see cref="ApplicationMemberIds.WiredContractUpdateSend"/>,
/// <see cref="ApplicationMemberIds.WiredTradeItemsAdd"/>, <see cref="ApplicationMemberIds.WiredTradeItemsRemove"/>,
/// <see cref="ApplicationMemberIds.WiredTradeConfirm"/> and <see cref="ApplicationMemberIds.WiredTradeCancel"/>.
/// The message is only sent while the hotel session, the room and the wired state generation are unchanged.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
/// <param name="Generation">The wired state generation the message was sent in.</param>
/// <param name="Revision">The wired state revision right after the message was sent.</param>
public sealed record WiredDispatchResult(
    DateTimeOffset DispatchedAtUtc,
    long Generation,
    long Revision);

/// <summary>
/// Specifies the kind of change reported by <see cref="WiredChanged"/>.
/// </summary>
public enum WiredChangeKind
{
    /// <summary>The user's wired permissions in the room were received.</summary>
    Permissions,
    /// <summary>The wired environment of the room was received.</summary>
    Environment,
    /// <summary>The wired click settings of the room were received.</summary>
    ClickSettings,
    /// <summary>The wired settings of the room were received.</summary>
    RoomSettings,
    /// <summary>The hotel opened the configuration of a wired furni item.</summary>
    ConfigurationOpened,
    /// <summary>The configuration of a wired furni item was received.</summary>
    ConfigurationReceived,
    /// <summary>A configuration save succeeded.</summary>
    SaveSucceeded,
    /// <summary>A configuration save failed validation.</summary>
    ValidationFailed,
    /// <summary>A wired menu error was received.</summary>
    MenuError,
    /// <summary>A wired reward result was received.</summary>
    RewardResult,
    /// <summary>The wired statistics of the room were received.</summary>
    RoomStats,
    /// <summary>A page of the wired log of the room was received.</summary>
    RoomLogs,
    /// <summary>The wired error log of the room was received.</summary>
    ErrorLogs,
    /// <summary>The answer to a wired user click was received.</summary>
    UserClickResult,
    /// <summary>The hash of all wired variables in the room was received.</summary>
    VariablesHash,
    /// <summary>A chunk of wired variable differences was received.</summary>
    VariablesDifferences,
    /// <summary>The wired variable values of one object were received.</summary>
    VariablesObject,
    /// <summary>The holders of a wired variable were received.</summary>
    VariableHolders,
    /// <summary>The permanent wired variables of an entity were received.</summary>
    PermanentVariables,
    /// <summary>A page of the owners of a permanent wired variable was received.</summary>
    VariableOwners,
    /// <summary>The result of setting a permanent wired variable was received.</summary>
    PermanentVariableSetResult,
    /// <summary>The hotel opened a wired chest.</summary>
    ChestOpened,
    /// <summary>The coins of a wired chest were received.</summary>
    ChestCoins,
    /// <summary>A fragment of the item list of a wired chest was received.</summary>
    ChestItemsChunk,
    /// <summary>A change to the item list of a wired chest was received.</summary>
    ChestItemsUpdated,
    /// <summary>The result of a chest capacity upgrade was received.</summary>
    ChestUpgradeResult,
    /// <summary>The hotel confirmed a chest preferences update.</summary>
    ChestPreferencesUpdated,
    /// <summary>A wired transaction succeeded.</summary>
    TransactionSucceeded,
    /// <summary>A wired transaction failed.</summary>
    TransactionFailed,
    /// <summary>A page of a wired transaction log was received.</summary>
    TransactionLogs,
    /// <summary>The details of a wired transaction were received.</summary>
    TransactionLogDetails,
    /// <summary>The contents of a wired contract were received.</summary>
    ContractContents,
    /// <summary>The hotel asked to open a wired contract editor.</summary>
    ContractOpened,
    /// <summary>The result of a wired contract update was received.</summary>
    ContractUpdateResult,
    /// <summary>A wired chest trade was started.</summary>
    TradeInitiated,
    /// <summary>The offers of the wired chest trade were updated.</summary>
    TradeItemsUpdated,
    /// <summary>The wired chest trade was canceled.</summary>
    TradeCancelled,
    /// <summary>The wired chest trade was completed.</summary>
    TradeCompleted,
    /// <summary>A wired trade transaction notification was received.</summary>
    TradeNotification,
    /// <summary>The wired state was cleared because a room was entered or left or the game state was reset.</summary>
    Reset
}

/// <summary>
/// Represents a change of the wired state of the current room.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.WiredChanged"/> for every committed change, including
/// <see cref="WiredChangeKind.Reset"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="Generation">The wired state generation after the change.</param>
/// <param name="Revision">The wired state revision after the change.</param>
public sealed record WiredChanged(
    WiredChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    long Generation,
    long Revision);

/// <summary>
/// Represents a wired value received from the hotel together with the state it was recorded in.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.WiredPermissionsChanged"/>, <see cref="ApplicationMemberIds.WiredEnvironmentChanged"/>,
/// <see cref="ApplicationMemberIds.WiredClickSettingsChanged"/>, <see cref="ApplicationMemberIds.WiredRoomSettingsChanged"/>,
/// <see cref="ApplicationMemberIds.WiredConfigurationOpened"/>, <see cref="ApplicationMemberIds.WiredConfigurationReceived"/>,
/// <see cref="ApplicationMemberIds.WiredConfigurationSaveResult"/>, <see cref="ApplicationMemberIds.WiredMenuError"/>,
/// <see cref="ApplicationMemberIds.WiredRewardResult"/>, <see cref="ApplicationMemberIds.WiredChestOpened"/>,
/// <see cref="ApplicationMemberIds.WiredChestCoinsReceived"/>, <see cref="ApplicationMemberIds.WiredChestItemsChunkReceived"/>,
/// <see cref="ApplicationMemberIds.WiredChestItemsUpdated"/>, <see cref="ApplicationMemberIds.WiredChestUpgradeResult"/>,
/// <see cref="ApplicationMemberIds.WiredChestPreferencesUpdated"/>, <see cref="ApplicationMemberIds.WiredTransactionSucceeded"/>,
/// <see cref="ApplicationMemberIds.WiredTransactionFailed"/>, <see cref="ApplicationMemberIds.WiredContractContentsReceived"/>,
/// <see cref="ApplicationMemberIds.WiredContractOpened"/>, <see cref="ApplicationMemberIds.WiredContractUpdateResult"/>,
/// <see cref="ApplicationMemberIds.WiredTradeInitiated"/>, <see cref="ApplicationMemberIds.WiredTradeItemsUpdated"/>,
/// <see cref="ApplicationMemberIds.WiredTradeCancelled"/>, <see cref="ApplicationMemberIds.WiredTradeCompleted"/>
/// and <see cref="ApplicationMemberIds.WiredTradeNotification"/>.
/// </remarks>
/// <typeparam name="T">The type of the received value.</typeparam>
/// <param name="Generation">The wired state generation the value was recorded in.</param>
/// <param name="Revision">The wired state revision after the value was recorded.</param>
/// <param name="ReceivedAtUtc">The time the value was published.</param>
/// <param name="Value">The received value.</param>
public sealed record WiredEvent<T>(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    T Value);
