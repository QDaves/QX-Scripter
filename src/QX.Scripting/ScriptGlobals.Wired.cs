using Qx.Game.Protocol;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Model.Wired;

namespace Qx.Scripting;

/// <content>
/// The wired subsystem: configuration boxes, wired variables, the wired menu's room settings,
/// permissions, statistics and logs.
/// <para>
/// <b>Rights.</b> Every read and write in the wired menu is gated server-side on the viewer's
/// wired permissions; a call from a user without rights is ignored or answered with a menu error.
/// The wire layouts carry no extra owner-only fields.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Registers a handler that runs when the server pushes a wired trigger box's configuration,
    /// which happens when its configuration dialog is opened.
    /// </summary>
    /// <param name="handler">The handler to call with the trigger configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTrigger(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Trigger, handler);

    /// <summary>
    /// Registers a handler that runs when the server pushes a wired effect box's configuration.
    /// </summary>
    /// <remarks>
    /// Effects are actions internally: the message is the action configuration plus the action's
    /// delay in pulses.
    /// </remarks>
    /// <param name="handler">The handler to call with the action configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredEffect(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Action, handler);

    /// <summary>
    /// Registers a handler that runs when the server pushes a wired condition box's configuration,
    /// including its quantifier and inversion flag.
    /// </summary>
    /// <param name="handler">The handler to call with the condition configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredCondition(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Condition, handler);

    /// <summary>
    /// Registers a handler that runs when the server pushes a wired selector box's configuration,
    /// including its filter and inversion flags.
    /// </summary>
    /// <param name="handler">The handler to call with the selector configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredSelector(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Selector, handler);

    /// <summary>Registers a handler that runs when the server pushes a wired add-on box's configuration.</summary>
    /// <param name="handler">The handler to call with the add-on configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredAddon(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Addon, handler);

    /// <summary>Registers a handler that runs when the server pushes a wired variable box's configuration.</summary>
    /// <param name="handler">The handler to call with the variable box configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredVariableConfig(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Variable, handler);

    /// <summary>
    /// Saves a wired trigger box, the equivalent of pressing Save in its dialog.
    /// </summary>
    /// <remarks>
    /// The method returns immediately and the save runs in the background; the server answers with
    /// a save success or a validation error, see <see cref="OnWiredSaveSuccess(Action{WiredSaveSuccess})"/>
    /// and <see cref="OnWiredValidationError(Action{WiredValidationError})"/>. Saves are sent one at
    /// a time. When no answer arrives within 10000 milliseconds, or the save cannot be sent, the
    /// error is reported as a background error.
    /// </remarks>
    /// <param name="update">
    /// The complete new configuration. Saves replace the whole box, so start from the
    /// configuration the server pushed rather than sending a partial update.
    /// </param>
    public void SaveWiredTrigger(UpdateTrigger update) =>
        wired_background<WiredTriggerSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationTriggerSave,
            new WiredTriggerSaveRequest(update));

    /// <summary>
    /// Saves a wired effect box.
    /// </summary>
    /// <remarks>
    /// The wire message is <c>UpdateAction</c>: there is no separate effect message, and an effect
    /// is an action carrying a delay. The method returns immediately and the save runs in the
    /// background like <see cref="SaveWiredTrigger(UpdateTrigger)"/>.
    /// </remarks>
    /// <param name="update">The complete new configuration, including the delay in pulses.</param>
    public void SaveWiredEffect(UpdateAction update) =>
        wired_background<WiredActionSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationActionSave,
            new WiredActionSaveRequest(update));

    /// <summary>Saves a wired condition box.</summary>
    /// <remarks>
    /// The method returns immediately and the save runs in the background like
    /// <see cref="SaveWiredTrigger(UpdateTrigger)"/>.
    /// </remarks>
    /// <param name="update">The complete new configuration, including quantifier and inversion.</param>
    public void SaveWiredCondition(UpdateCondition update) =>
        wired_background<WiredConditionSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationConditionSave,
            new WiredConditionSaveRequest(update));

    /// <summary>Saves a wired selector box.</summary>
    /// <remarks>
    /// The method returns immediately and the save runs in the background like
    /// <see cref="SaveWiredTrigger(UpdateTrigger)"/>.
    /// </remarks>
    /// <param name="update">The complete new configuration, including the filter and inversion flags.</param>
    public void SaveWiredSelector(UpdateSelector update) =>
        wired_background<WiredSelectorSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationSelectorSave,
            new WiredSelectorSaveRequest(update));

    /// <summary>Saves a wired add-on box.</summary>
    /// <remarks>
    /// The method returns immediately and the save runs in the background like
    /// <see cref="SaveWiredTrigger(UpdateTrigger)"/>.
    /// </remarks>
    /// <param name="update">The complete new configuration.</param>
    public void SaveWiredAddon(UpdateAddon update) =>
        wired_background<WiredAddonSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationAddonSave,
            new WiredAddonSaveRequest(update));

    /// <summary>Saves a wired variable box.</summary>
    /// <remarks>
    /// The method returns immediately and the save runs in the background like
    /// <see cref="SaveWiredTrigger(UpdateTrigger)"/>.
    /// </remarks>
    /// <param name="update">The complete new configuration.</param>
    public void SaveWiredVariable(UpdateVariable update) =>
        wired_background<WiredVariableSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationVariableSave,
            new WiredVariableSaveRequest(update));

    /// <summary>
    /// Requests the single hash that covers every wired variable in the room.
    /// </summary>
    /// <remarks>
    /// This is the cheap half of the variable sync protocol: poll the hash, and only fetch diffs
    /// when it moved.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The current all-variables hash.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredAllVariablesHash> GetRoomVariablesHash(int timeoutMs = 10000) =>
        wired_call<WiredTimeoutRequest, WiredAllVariablesHash>(
            ApplicationMemberIds.WiredVariablesHashGet,
            new WiredTimeoutRequest(timeoutMs));

    /// <summary>
    /// Requests the difference between a cached set of per-variable hashes and the room's current
    /// wired variables.
    /// </summary>
    /// <param name="cache">
    /// The variable ids and per-variable hashes already held, with unique ids. Pass
    /// <see langword="null"/> or an empty list to receive everything.
    /// </param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>
    /// One chunk of the diff: the new global hash, the removed variable ids, the added or updated
    /// variables with their per-variable hashes, and a last-chunk flag. A large room answers in
    /// several chunks, and only the first one is awaited here; use
    /// <see cref="GetRoomVariables(int)"/> for the complete set.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredVariableDifferencesSnapshot> GetRoomVariableDiffs(
        IReadOnlyList<VariableHashEntry>? cache = null,
        int timeoutMs = 10000) =>
        wired_call<WiredVariableDifferencesRequest, WiredVariableDifferencesSnapshot>(
            ApplicationMemberIds.WiredVariablesDifferencesGet,
            new WiredVariableDifferencesRequest(cache, timeoutMs));

    /// <summary>
    /// Inspects the wired variable values held by one object, taking the object id as a 32-bit
    /// value.
    /// </summary>
    /// <param name="target">
    /// The kind of holder to inspect: furni, user or global. Only those three are used by this
    /// request.
    /// </param>
    /// <param name="objectId">
    /// The furni id for a furni, the user's room index for a user, and 0 for global.
    /// </param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The inspection snapshot: variable id to value, plus which wireds reference them.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="target"/> is not furni, user or global, or <paramref name="objectId"/> does
    /// not fit the target.
    /// </exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetVariablesForObject(
        WiredTarget target,
        int objectId,
        int timeoutMs = 10000) =>
        wired_call<WiredVariablesObjectRequest, WiredVariablesObjectSnapshot>(
            ApplicationMemberIds.WiredVariablesObjectGet,
            new WiredVariablesObjectRequest(target, objectId, timeoutMs));

    /// <summary>
    /// Inspects the wired variable values held by one object, taking the object id as a native id.
    /// </summary>
    /// <param name="target">The kind of holder to inspect: furni, user or global.</param>
    /// <param name="objectId">
    /// The furni id for a furni, the user's room index for a user, and 0 for global.
    /// </param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>
    /// The inspection snapshot. The list of wireds that reference the variables is only present
    /// for a furni.
    /// </returns>
    /// <exception cref="OverflowException">Thrown when <paramref name="objectId"/> does not fit in 32 bits.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetVariablesForObject(
        WiredTarget target,
        Id objectId,
        int timeoutMs = 10000) =>
        GetVariablesForObject(target, checked((int)(long)objectId), timeoutMs);

    /// <summary>Inspects the wired variable values held by one furni.</summary>
    /// <param name="furniId">The floor item id of the furni.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The inspection snapshot for that furni.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetFurniVariables(Id furniId, int timeoutMs = 10000) =>
        GetVariablesForObject(WiredTarget.Furni, furniId, timeoutMs);

    /// <summary>Inspects the wired variable values held by one user in the room.</summary>
    /// <param name="userIndex">
    /// The user's room index, which is the per-room entity index and not their account id.
    /// </param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The inspection snapshot for that user.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetUserWiredVariables(int userIndex, int timeoutMs = 10000) =>
        GetVariablesForObject(WiredTarget.User, userIndex, timeoutMs);

    /// <summary>Inspects the room's global wired variable values.</summary>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The inspection snapshot for the global scope.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetGlobalVariables(int timeoutMs = 10000) =>
        GetVariablesForObject(WiredTarget.Global, 0, timeoutMs);

    /// <summary>
    /// Requests which objects currently hold a value for one variable, and what those values are.
    /// </summary>
    /// <param name="variableId">The variable's id string, not its display name.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The variable's definition together with its holders and their values.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredVariableHoldersSnapshot> GetVariableHolders(
        string variableId,
        int timeoutMs = 10000) =>
        wired_call<WiredVariableHoldersRequest, WiredVariableHoldersSnapshot>(
            ApplicationMemberIds.WiredVariablesHoldersGet,
            new WiredVariableHoldersRequest(variableId, timeoutMs));

    /// <summary>
    /// Requests the full permanent variable storage of one entity, including creation and update
    /// timestamps per slot.
    /// </summary>
    /// <param name="entityType">
    /// The entity kind, which must not be 0. 1 is a user, which has no owner; the reply for any
    /// other kind also carries the owner's id, name and figure.
    /// </param>
    /// <param name="entityId">The entity's id, which must be positive.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The entity's permanent variable storage.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredPermanentVariablesSnapshot> GetUserPermanentVariables(
        int entityType,
        int entityId,
        int timeoutMs = 10000) =>
        wired_call<WiredPermanentVariablesRequest, WiredPermanentVariablesSnapshot>(
            ApplicationMemberIds.WiredVariablesPermanentGet,
            new WiredPermanentVariablesRequest(entityType, entityId, timeoutMs));

    /// <summary>
    /// Requests one page of the entities that own a permanent variable, as the wired variable
    /// management table shows them.
    /// </summary>
    /// <param name="variableId">The variable's id string.</param>
    /// <param name="page">The one-based page number; the game client starts at 1.</param>
    /// <param name="pageSize">The number of rows per page, from 1 to 250; the game client uses 50.</param>
    /// <param name="sortFilter">
    /// The sort order the table applies; the game client opens the table with 0.
    /// </param>
    /// <param name="userTypeFilter">
    /// The entity type filter the table applies; the game client opens the table with -1, which
    /// shows every entity type.
    /// </param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>
    /// One page of owners, echoing the total entry count, the current page and both filters.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredVariableOwnersSnapshot> GetVariableOwnersPage(
        string variableId, int page = 1, int pageSize = 50, int sortFilter = 0, int userTypeFilter = -1, int timeoutMs = 10000) =>
        wired_call<WiredVariableOwnersRequest, WiredVariableOwnersSnapshot>(
            ApplicationMemberIds.WiredVariablesOwnersGet,
            new WiredVariableOwnersRequest(
                variableId,
                page,
                pageSize,
                sortFilter,
                userTypeFilter,
                timeoutMs));

    /// <summary>
    /// Writes, creates or deletes a wired variable value on one object, taking the object id as a
    /// 32-bit value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The request is sent without waiting: the server sends no acknowledgement, so read the
    /// object's values again to see the effect.
    /// </para>
    /// <para>
    /// The server enforces both the room's wired write permission and the variable's own write,
    /// create and delete capability flags, so an unauthorized call is dropped silently.
    /// </para>
    /// </remarks>
    /// <param name="target">The holder kind: furni, user or global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The variable's id string, not its display name.</param>
    /// <param name="value">The integer value to store; ignored for a delete.</param>
    /// <param name="operation">The operation: 0 write, 1 create, 2 delete.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="target"/>, <paramref name="objectId"/> or <paramref name="operation"/> is out
    /// of range.
    /// </exception>
    public void SetObjectVariable(WiredTarget target, int objectId, string variableId, int value, int operation = WiredVariableOperation.Write) =>
        wired_send(
            ApplicationMemberIds.WiredVariablesObjectSet,
            new WiredObjectVariableSetRequest(
                target,
                objectId,
                variableId,
                value,
                operation));

    /// <summary>
    /// Writes, creates or deletes a wired variable value on one object, taking the object id as a
    /// native id.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; the server sends no acknowledgement.
    /// </remarks>
    /// <param name="target">The holder kind: furni, user or global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The variable's id string, not its display name.</param>
    /// <param name="value">The integer value to store; ignored for a delete.</param>
    /// <param name="operation">The operation: 0 write, 1 create, 2 delete.</param>
    /// <exception cref="OverflowException">Thrown when <paramref name="objectId"/> does not fit in 32 bits.</exception>
    public void SetObjectVariable(WiredTarget target, Id objectId, string variableId, int value, int operation = WiredVariableOperation.Write) =>
        SetObjectVariable(
            target,
            checked((int)(long)objectId),
            variableId,
            value,
            operation);

    /// <summary>
    /// Writes a wired variable value on one furni.
    /// </summary>
    /// <remarks>The request is sent without waiting; no acknowledgement is sent.</remarks>
    /// <param name="furniId">The floor item id of the furni.</param>
    /// <param name="variableId">The variable's id string.</param>
    /// <param name="value">The integer value to store.</param>
    public void SetFurniVariable(Id furniId, string variableId, int value) =>
        SetObjectVariable(WiredTarget.Furni, furniId, variableId, value);

    /// <summary>
    /// Writes a global wired variable value in the room.
    /// </summary>
    /// <remarks>The request is sent without waiting; no acknowledgement is sent.</remarks>
    /// <param name="variableId">The variable's id string.</param>
    /// <param name="value">The integer value to store.</param>
    public void SetGlobalVariable(string variableId, int value) =>
        SetObjectVariable(WiredTarget.Global, 0, variableId, value);

    /// <summary>
    /// Creates a wired variable on one object, taking the object id as a 32-bit value.
    /// </summary>
    /// <remarks>The request is sent without waiting; no acknowledgement is sent.</remarks>
    /// <param name="target">The holder kind: furni, user or global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The id string for the new variable.</param>
    /// <param name="value">The initial value; the game client sends 0 when none is given.</param>
    public void CreateObjectVariable(WiredTarget target, int objectId, string variableId, int value = 0) =>
        CreateObjectVariable(target, (Id)(long)objectId, variableId, value);

    /// <summary>
    /// Creates a wired variable on one object.
    /// </summary>
    /// <remarks>The request is sent without waiting; no acknowledgement is sent.</remarks>
    /// <param name="target">The holder kind: furni, user or global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The id string for the new variable.</param>
    /// <param name="value">The initial value.</param>
    public void CreateObjectVariable(WiredTarget target, Id objectId, string variableId, int value = 0) =>
        SetObjectVariable(target, objectId, variableId, value, WiredVariableOperation.Create);

    /// <summary>
    /// Deletes a wired variable from one object, taking the object id as a 32-bit value.
    /// </summary>
    /// <remarks>The request is sent without waiting; no acknowledgement is sent.</remarks>
    /// <param name="target">The holder kind: furni, user or global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The variable's id string.</param>
    public void DeleteObjectVariable(WiredTarget target, int objectId, string variableId) =>
        DeleteObjectVariable(target, (Id)(long)objectId, variableId);

    /// <summary>
    /// Deletes a wired variable from one object.
    /// </summary>
    /// <remarks>The request is sent without waiting; no acknowledgement is sent.</remarks>
    /// <param name="target">The holder kind: furni, user or global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The variable's id string.</param>
    public void DeleteObjectVariable(WiredTarget target, Id objectId, string variableId) =>
        SetObjectVariable(target, objectId, variableId, 0, WiredVariableOperation.Delete);

    /// <summary>
    /// Deletes a wired variable from every furni and user holding it, as the delete button of the
    /// wired menu's variable overview does.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; no acknowledgement is sent. The game client only
    /// offers this with wired modify rights, for a persisted furni or user variable that can be
    /// created and deleted.
    /// </remarks>
    /// <param name="variableId">The variable's id string.</param>
    public void DeleteAllVariableHolders(string variableId) =>
        wired_send(
            ApplicationMemberIds.WiredVariablesHoldersDelete,
            new WiredVariableHoldersDeleteRequest(variableId));

    /// <summary>
    /// Writes, creates or deletes a permanent variable on one entity and waits for the server's
    /// answer.
    /// </summary>
    /// <remarks>
    /// Unlike the object variable writes, this one is acknowledged.
    /// </remarks>
    /// <param name="entityType">The entity kind, which must not be 0; 1 is a user.</param>
    /// <param name="entityId">The entity's id, which must be positive.</param>
    /// <param name="variableId">The variable's id string.</param>
    /// <param name="value">The integer value to store; ignored for a delete.</param>
    /// <param name="operation">The operation: 0 write, 1 create, 2 delete.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The result, which carries only a success flag.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredSetUserPermanentVariableResult> SetUserVariable(
        int entityType, int entityId, string variableId, int value, int operation = WiredVariableOperation.Write, int timeoutMs = 10000) =>
        wired_call<WiredPermanentVariableSetRequest, WiredSetUserPermanentVariableResult>(
            ApplicationMemberIds.WiredVariablesPermanentSet,
            new WiredPermanentVariableSetRequest(
                entityType,
                entityId,
                variableId,
                value,
                operation,
                timeoutMs));

    /// <summary>
    /// Polls the room's wired variable definitions in the background and calls a handler whenever
    /// they change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every <paramref name="intervalMs"/> milliseconds, while the room is ready, the cheap
    /// all-variables hash is requested; the full definitions are fetched only when the hash or the
    /// room changed since the last poll. The handler runs on the polling task, first with the
    /// initial definitions and then after each change.
    /// </para>
    /// <para>
    /// Timeouts, disconnects and leaving the room are skipped over and polling continues. An
    /// exception thrown by <paramref name="onChange"/>, or any other request error, ends the watch
    /// and is reported as a background error.
    /// </para>
    /// </remarks>
    /// <param name="onChange">The handler to call with the complete definitions after each change.</param>
    /// <param name="intervalMs">The delay between polls in milliseconds.</param>
    /// <returns>A handle that stops the polling when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onChange"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="intervalMs"/> is zero or negative.</exception>
    public IDisposable WatchRoomVariables(
        Action<WiredVariableCollectionSnapshot> onChange,
        int intervalMs = 1000) =>
        watch_variable_collections(onChange, intervalMs);

    /// <summary>
    /// Requests the room's wired settings, which are the modify and read permission masks and the
    /// room's wired timezone.
    /// </summary>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The current wired room settings.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredRoomSettings> GetWiredRoomSettings(int timeoutMs = 10000) =>
        wired_call<WiredTimeoutRequest, WiredRoomSettings>(
            ApplicationMemberIds.WiredRoomSettingsGet,
            new WiredTimeoutRequest(timeoutMs));

    /// <summary>
    /// Rewrites the room's wired settings.
    /// </summary>
    /// <remarks>
    /// All three values travel together, so read the current settings first when only one of them
    /// should change. The method returns immediately and the request runs in the background until
    /// the server sends the settings back with the new values; when that does not happen within
    /// 10000 milliseconds, the timeout is reported as a background error.
    /// </remarks>
    /// <param name="modifyPermissionMask">The mask of who may edit wired in this room, paired with the viewer's "can modify" flag.</param>
    /// <param name="readPermissionMask">The mask of who may see the wired menu, paired with the viewer's "can read" flag.</param>
    /// <param name="timezone">The room's wired timezone string, which must not be empty.</param>
    public void SetWiredRoomSettings(int modifyPermissionMask, int readPermissionMask, string timezone) =>
        wired_background<WiredRoomSettingsSetRequest, WiredRoomSettings>(
            ApplicationMemberIds.WiredRoomSettingsSet,
            new WiredRoomSettingsSetRequest(
                modifyPermissionMask,
                readPermissionMask,
                timezone));

    /// <summary>
    /// Requests the room's wired budget statistics.
    /// </summary>
    /// <remarks>
    /// The statistics are the execution cost against its cap, the heavy room flag, floor and wall
    /// item counts against their caps, and how many permanent furni, user and global variables are
    /// used out of the allowance.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The statistics.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredRoomStats> GetWiredRoomStats(int timeoutMs = 10000) =>
        wired_call<WiredTimeoutRequest, WiredRoomStats>(
            ApplicationMemberIds.WiredRoomStatsGet,
            new WiredTimeoutRequest(timeoutMs));

    /// <summary>
    /// Registers a handler that runs when the server states what the local user may do with the
    /// wired menu in this room.
    /// </summary>
    /// <remarks>
    /// It is pushed on entering a room and when the menu is opened, so it is the reliable way to
    /// learn whether wired reads and writes will be accepted.
    /// </remarks>
    /// <param name="handler">The handler to call with the can modify and can read flags.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredPermissions(Action<WiredPermissions> handler) =>
        wired_event(ApplicationMemberIds.WiredPermissionsChanged, handler);

    /// <summary>
    /// Registers a handler that runs when the server describes the room's wired environment.
    /// </summary>
    /// <remarks>
    /// The environment says whether a click user wired exists, and which achievements wired may
    /// award. The achievement list is optional on the wire, so a <see langword="null"/> list means
    /// the server omitted the section rather than sent an empty one.
    /// </remarks>
    /// <param name="handler">The handler to call with the environment description.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredEnvironment(Action<WiredEnvironment> handler) =>
        wired_event(ApplicationMemberIds.WiredEnvironmentChanged, handler);

    /// <summary>
    /// Registers a handler that runs when a wired save was accepted.
    /// </summary>
    /// <remarks>
    /// The message has no payload, so it does not say which box it acknowledges. Pair it with the
    /// save that was just sent.
    /// </remarks>
    /// <param name="handler">The handler to call with the empty success message.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredSaveSuccess(Action<WiredSaveSuccess> handler) =>
        wired_save_event(
            true,
            _ => handler(new WiredSaveSuccess()));

    /// <summary>
    /// Registers a handler that runs when a wired save was rejected.
    /// </summary>
    /// <remarks>
    /// The error carries a localization key and its substitution parameters rather than a
    /// ready-made message.
    /// </remarks>
    /// <param name="handler">The handler to call with the validation error.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredValidationError(Action<WiredValidationError> handler) =>
        wired_save_event(
            false,
            result => handler(result.ValidationError!));

    /// <summary>
    /// Registers a handler that runs when a wired menu operation fails.
    /// </summary>
    /// <remarks>
    /// The message carries a numeric error code and nothing else. This is the usual answer to a
    /// wired request made without sufficient rights.
    /// </remarks>
    /// <param name="handler">The handler to call with the error code.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredMenuError(Action<WiredMenuError> handler) =>
        wired_event(ApplicationMemberIds.WiredMenuError, handler);

    /// <summary>
    /// Registers a handler that runs when the server reports the outcome of a wired reward.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the result, which carries only the reason code that explains why
    /// the reward was or was not given.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredRewardResult(Action<WiredRewardResult> handler) =>
        wired_event(ApplicationMemberIds.WiredRewardResult, handler);

    /// <summary>
    /// Registers a handler that runs when the server sends the room's wired click options.
    /// </summary>
    /// <remarks>
    /// The options say what clicking a user and what clicking a furni should do while wired is
    /// active.
    /// </remarks>
    /// <param name="handler">The handler to call with the two option codes.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredClickSettings(Action<WiredClickSettings> handler) =>
        wired_event(ApplicationMemberIds.WiredClickSettingsChanged, handler);

    /// <summary>Requests a page of the room's wired execution log.</summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of entries per page, from 1 to 250; the game client uses 50.</param>
    /// <param name="logLevelFilter">
    /// The log level to keep, or -1 for no level filter.
    /// </param>
    /// <param name="logSourceFilter">
    /// The log source to keep, or -1 for no source filter.
    /// </param>
    /// <param name="query">A free text filter, or an empty string for no text filter.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>
    /// The page, echoing the total entry count, the current page and the filters that were applied.
    /// A filter the server did not apply comes back as -1 or null.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredRoomLogs> GetWiredRoomLogs(
        int page = 1, int pageSize = 50, int logLevelFilter = -1, int logSourceFilter = -1, string query = "", int timeoutMs = 10000) =>
        wired_call<WiredRoomLogsRequest, WiredRoomLogs>(
            ApplicationMemberIds.WiredRoomLogsGet,
            new WiredRoomLogsRequest(
                page,
                pageSize,
                logLevelFilter,
                logSourceFilter,
                query,
                timeoutMs));

    /// <summary>
    /// Requests the room's wired error statistics.
    /// </summary>
    /// <remarks>
    /// There is one row per error kind with its name, category, how often it was thrown and how
    /// long ago it last happened.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The error rows. The whole list is returned at once, not paged.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredErrorLogs> GetWiredErrorLogs(int timeoutMs = 10000) =>
        wired_call<WiredTimeoutRequest, WiredErrorLogs>(
            ApplicationMemberIds.WiredRoomErrorLogsGet,
            new WiredTimeoutRequest(timeoutMs));

    /// <summary>
    /// Clears the room's wired error statistics.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; the server sends no acknowledgement, so read the error
    /// log again to confirm.
    /// </remarks>
    public void ClearWiredErrorLogs() =>
        wired_send(
            ApplicationMemberIds.WiredRoomErrorLogsClear,
            new WiredCommandRequest());

    /// <summary>
    /// Sends the wired "user was clicked" message for one user in the room and waits for the
    /// server's answer, which is what a click user wired reacts to.
    /// </summary>
    /// <param name="index">The user's room index, not their account id.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The response, which echoes the index and says whether a menu should open.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredClickUserResponse> ClickWiredUser(int index, int timeoutMs = 10000) =>
        wired_call<WiredUserClickRequest, WiredClickUserResponse>(
            ApplicationMemberIds.WiredRoomUserClick,
            new WiredUserClickRequest(index, timeoutMs));

    /// <summary>
    /// Reloads the room's state.
    /// </summary>
    /// <remarks>
    /// This is the wired menu's reload button. Nothing is discarded and nothing is asked; the hotel
    /// sends no acknowledgement, so this returns as soon as the request is away.
    /// </remarks>
    public void ReloadRoomState() =>
        wired_send(
            ApplicationMemberIds.WiredRoomReload,
            new WiredCommandRequest());

    /// <summary>
    /// Rolls the room back to its last saved state.
    /// </summary>
    /// <remarks>
    /// This is the wired menu's roll back button, which the client only sends after the user
    /// confirms a warning: everything done since the last save is thrown away, furni included.
    /// There is no acknowledgement and no undo, so this returns as soon as the request is away.
    /// </remarks>
    public void RollBackRoomState() =>
        wired_send(
            ApplicationMemberIds.WiredRoomRollback,
            new WiredCommandRequest());

    /// <summary>
    /// Stores the local user's wired menu preferences.
    /// </summary>
    /// <remarks>
    /// The preferences are which buttons are shown, play test mode, whether wired whispers are
    /// suppressed, whether all notifications are shown, and the UI style. The request is sent
    /// without waiting; the server sends no acknowledgement.
    /// </remarks>
    /// <param name="preferences">The complete preference set, since all fields are sent together.</param>
    public void SetWiredPreferences(WiredSetPreferences preferences) =>
        wired_send(
            ApplicationMemberIds.WiredPreferencesSet,
            new WiredPreferencesSetRequest(preferences));

    private Task<TResult> wired_call<TRequest, TResult>(
        string member_id,
        TRequest request) =>
        _application.InvokeAsync<TRequest, TResult>(member_id, request, Ct).AsTask();

    private void wired_send<TRequest>(string member_id, TRequest request) =>
        _application.Invoke<TRequest, WiredDispatchResult>(member_id, request, Ct);

    private void wired_background<TRequest, TResult>(
        string member_id,
        TRequest request) =>
        StartObservedTask(
            () => _application.InvokeAsync<TRequest, TResult>(member_id, request, Ct).AsTask(),
            Ct);

    private IDisposable wired_event<T>(string member_id, Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<WiredEvent<T>>(
            member_id,
            Guarded<WiredEvent<T>>(value => handler(value.Value))));
    }

    private IDisposable wired_configuration_event(
        WiredConfigurationKind kind,
        Action<WiredConfigurationSnapshot> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<WiredEvent<WiredConfigurationSnapshot>>(
            ApplicationMemberIds.WiredConfigurationReceived,
            Guarded<WiredEvent<WiredConfigurationSnapshot>>(value =>
            {
                if (value.Value.Kind == kind)
                    handler(value.Value);
            })));
    }

    private IDisposable wired_save_event(
        bool success,
        Action<WiredConfigurationSaveResult> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<WiredEvent<WiredConfigurationSaveResult>>(
            ApplicationMemberIds.WiredConfigurationSaveResult,
            Guarded<WiredEvent<WiredConfigurationSaveResult>>(value =>
            {
                if (value.Value.Success == success)
                    handler(value.Value);
            })));
    }
}
