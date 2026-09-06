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
/// wired permissions; a call from a user without rights is simply ignored or answered with a menu
/// error. The wire layouts carry no extra owner-only fields.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Raised when the server pushes a wired trigger box's configuration, which happens when its
    /// configuration dialog is opened.
    /// </summary>
    /// <param name="handler">Receives the trigger configuration.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    public IDisposable OnWiredTrigger(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Trigger, handler);

    /// <summary>
    /// Raised when the server pushes a wired effect box's configuration. Effects are actions
    /// internally: the message is the action configuration plus the action's delay in pulses.
    /// </summary>
    /// <param name="handler">Receives the action configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredEffect(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Action, handler);

    /// <summary>
    /// Raised when the server pushes a wired condition box's configuration, including its
    /// quantifier and inversion flag.
    /// </summary>
    /// <param name="handler">Receives the condition configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredCondition(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Condition, handler);

    /// <summary>
    /// Raised when the server pushes a wired selector box's configuration, including its filter
    /// and inversion flags.
    /// </summary>
    /// <param name="handler">Receives the selector configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredSelector(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Selector, handler);

    /// <summary>Raised when the server pushes a wired add-on box's configuration.</summary>
    /// <param name="handler">Receives the add-on configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredAddon(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Addon, handler);

    /// <summary>Raised when the server pushes a wired variable box's configuration.</summary>
    /// <param name="handler">Receives the variable-box configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredVariableConfig(Action<WiredConfigurationSnapshot> handler) =>
        wired_configuration_event(WiredConfigurationKind.Variable, handler);

    /// <summary>
    /// Saves a wired trigger box, the equivalent of pressing Save in its dialog. Returns
    /// immediately; the server answers with a save-success or a validation error.
    /// </summary>
    /// <param name="update">
    /// The complete new configuration. Saves replace the whole box, so start from the
    /// configuration the server pushed rather than sending a partial update.
    /// </param>
    public void SaveWiredTrigger(UpdateTrigger update) =>
        wired_background<WiredTriggerSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationTriggerSave,
            new WiredTriggerSaveRequest(update));

    /// <summary>
    /// Saves a wired effect box. The wire message is <c>UpdateAction</c> — there is no separate
    /// effect message; an effect is an action carrying a delay. Returns immediately.
    /// </summary>
    /// <param name="update">The complete new configuration, including the delay in pulses.</param>
    public void SaveWiredEffect(UpdateAction update) =>
        wired_background<WiredActionSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationActionSave,
            new WiredActionSaveRequest(update));

    /// <summary>Saves a wired condition box. Returns immediately.</summary>
    /// <param name="update">The complete new configuration, including quantifier and inversion.</param>
    public void SaveWiredCondition(UpdateCondition update) =>
        wired_background<WiredConditionSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationConditionSave,
            new WiredConditionSaveRequest(update));

    /// <summary>Saves a wired selector box. Returns immediately.</summary>
    /// <param name="update">The complete new configuration, including the filter and inversion flags.</param>
    public void SaveWiredSelector(UpdateSelector update) =>
        wired_background<WiredSelectorSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationSelectorSave,
            new WiredSelectorSaveRequest(update));

    /// <summary>Saves a wired add-on box. Returns immediately.</summary>
    /// <param name="update">The complete new configuration.</param>
    public void SaveWiredAddon(UpdateAddon update) =>
        wired_background<WiredAddonSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationAddonSave,
            new WiredAddonSaveRequest(update));

    /// <summary>Saves a wired variable box. Returns immediately.</summary>
    /// <param name="update">The complete new configuration.</param>
    public void SaveWiredVariable(UpdateVariable update) =>
        wired_background<WiredVariableSaveRequest, WiredConfigurationSaveResult>(
            ApplicationMemberIds.WiredConfigurationVariableSave,
            new WiredVariableSaveRequest(update));

    /// <summary>
    /// Asks for the single hash that covers every wired variable in the room. This is the cheap
    /// half of the variable sync protocol: poll the hash, and only fetch diffs when it moved.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The current all-variables hash.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredAllVariablesHash> GetRoomVariablesHash(int timeoutMs = 10000) =>
        wired_call<WiredTimeoutRequest, WiredAllVariablesHash>(
            ApplicationMemberIds.WiredVariablesHashGet,
            new WiredTimeoutRequest(timeoutMs));

    /// <summary>
    /// Asks the server for the difference between a cached set of per-variable hashes and the
    /// room's current wired variables.
    /// </summary>
    /// <param name="cache">
    /// The variable ids and per-variable hashes already held. Pass <see langword="null"/> or an
    /// empty list to receive everything.
    /// </param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>
    /// One chunk of the diff: the new global hash, the removed variable ids, the added or updated
    /// variables with their per-variable hashes, and a last-chunk flag. A large room answers in
    /// several chunks, and only the first one is awaited here.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
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
    /// What kind of holder to inspect: 0 furni, 1 user, -10 global. Only those three are used by
    /// this request.
    /// </param>
    /// <param name="objectId">
    /// The furni id for target 0, the user's room index for target 1, and 0 for global.
    /// </param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The inspection snapshot: variable id to value, plus which wireds reference them.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetVariablesForObject(
        int target,
        int objectId,
        int timeoutMs = 10000) =>
        wired_call<WiredVariablesObjectRequest, WiredVariablesObjectSnapshot>(
            ApplicationMemberIds.WiredVariablesObjectGet,
            new WiredVariablesObjectRequest((WiredTarget)target, objectId, timeoutMs));

    /// <summary>
    /// Inspects the wired variable values held by one object, taking the object id as a native id.
    /// </summary>
    /// <param name="target">What kind of holder to inspect: 0 furni, 1 user, -10 global.</param>
    /// <param name="objectId">
    /// The furni id for target 0, the user's room index for target 1, and 0 for global.
    /// </param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>
    /// The inspection snapshot. The list of wireds that reference the variables is only present
    /// for target 0.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetVariablesForObject(
        int target,
        Id objectId,
        int timeoutMs = 10000) =>
        GetVariablesForObject(target, checked((int)(long)objectId), timeoutMs);

    /// <summary>Inspects the wired variable values held by one furni.</summary>
    /// <param name="furniId">The floor item id of the furni.</param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The inspection snapshot for that furni.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetFurniVariables(Id furniId, int timeoutMs = 10000) =>
        GetVariablesForObject(WiredVariableTarget.Furni, furniId, timeoutMs);

    /// <summary>Inspects the wired variable values held by one user in the room.</summary>
    /// <param name="userIndex">
    /// The user's room index — the per-room entity index, not their account id.
    /// </param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The inspection snapshot for that user.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetUserWiredVariables(int userIndex, int timeoutMs = 10000) =>
        GetVariablesForObject(WiredVariableTarget.User, userIndex, timeoutMs);

    /// <summary>Inspects the room's global wired variable values.</summary>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The inspection snapshot for the global scope.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredVariablesObjectSnapshot> GetGlobalVariables(int timeoutMs = 10000) =>
        GetVariablesForObject(WiredVariableTarget.Global, 0, timeoutMs);

    /// <summary>
    /// Asks which objects currently hold a value for one variable, and what those values are.
    /// </summary>
    /// <param name="variableId">The variable's id string, not its display name.</param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The variable's definition together with its holders and their values.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredVariableHoldersSnapshot> GetVariableHolders(
        string variableId,
        int timeoutMs = 10000) =>
        wired_call<WiredVariableHoldersRequest, WiredVariableHoldersSnapshot>(
            ApplicationMemberIds.WiredVariablesHoldersGet,
            new WiredVariableHoldersRequest(variableId, timeoutMs));

    /// <summary>
    /// Asks for the full permanent-variable storage of one entity, including creation and update
    /// timestamps per slot.
    /// </summary>
    /// <param name="entityType">
    /// The entity kind. 1 means the user's own/default entity, and is the value for which the
    /// reply omits the owner block; any other value carries owner id, name and figure.
    /// </param>
    /// <param name="entityId">The entity's id.</param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The entity's permanent variable storage.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredPermanentVariablesSnapshot> GetUserPermanentVariables(
        int entityType,
        int entityId,
        int timeoutMs = 10000) =>
        wired_call<WiredPermanentVariablesRequest, WiredPermanentVariablesSnapshot>(
            ApplicationMemberIds.WiredVariablesPermanentGet,
            new WiredPermanentVariablesRequest(entityType, entityId, timeoutMs));

    /// <summary>
    /// Asks for one page of the entities that own a permanent variable, as the wired variable
    /// management table shows them.
    /// </summary>
    /// <param name="variableId">The variable's id string.</param>
    /// <param name="page">The one-based page number; the game client starts at 1.</param>
    /// <param name="pageSize">How many rows per page; the game client uses 50.</param>
    /// <param name="userTypeFilter">
    /// The entity-type filter the table applies; the game client sends 0 for "no filter".
    /// </param>
    /// <param name="sortFilter">
    /// The sort order the table applies; the game client sends -1 for "default order".
    /// </param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>
    /// One page of owners, echoing the total entry count, the current page and both filters.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredVariableOwnersSnapshot> GetVariableOwnersPage(
        string variableId, int page = 1, int pageSize = 50, int userTypeFilter = 0, int sortFilter = -1, int timeoutMs = 10000) =>
        wired_call<WiredVariableOwnersRequest, WiredVariableOwnersSnapshot>(
            ApplicationMemberIds.WiredVariablesOwnersGet,
            new WiredVariableOwnersRequest(
                variableId,
                page,
                pageSize,
                userTypeFilter,
                sortFilter,
                timeoutMs));

    /// <summary>
    /// Writes, creates or deletes a wired variable value on one object, taking the object id as a
    /// 32-bit value. Returns immediately: the server sends no acknowledgement, so the effect is
    /// only visible through the hash/diff poll.
    /// </summary>
    /// <param name="target">The holder kind: 0 furni, 1 user, 2 merged, -10 global, -20 context.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The variable's id string, not its display name.</param>
    /// <param name="value">The integer value to store; ignored for a delete.</param>
    /// <param name="operation">0 write, 1 create, 2 delete.</param>
    /// <remarks>
    /// The server enforces both the room's wired write permission and the variable's own
    /// write / create-and-delete capability flags, so an unauthorised call is dropped silently.
    /// </remarks>
    public void SetObjectVariable(int target, int objectId, string variableId, int value, int operation = WiredVariableOperation.Write) =>
        wired_send(
            ApplicationMemberIds.WiredVariablesObjectSet,
            new WiredObjectVariableSetRequest(
                (WiredTarget)target,
                objectId,
                variableId,
                value,
                operation));

    /// <summary>
    /// Writes, creates or deletes a wired variable value on one object, taking the object id as a
    /// native id. Returns immediately; the server sends no acknowledgement.
    /// </summary>
    /// <param name="target">The holder kind: 0 furni, 1 user, 2 merged, -10 global, -20 context.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The variable's id string, not its display name.</param>
    /// <param name="value">The integer value to store; ignored for a delete.</param>
    /// <param name="operation">0 write, 1 create, 2 delete.</param>
    public void SetObjectVariable(int target, Id objectId, string variableId, int value, int operation = WiredVariableOperation.Write) =>
        SetObjectVariable(
            target,
            checked((int)(long)objectId),
            variableId,
            value,
            operation);

    /// <summary>
    /// Writes a wired variable value on one furni. Returns immediately; no acknowledgement is
    /// sent.
    /// </summary>
    /// <param name="furniId">The floor item id of the furni.</param>
    /// <param name="variableId">The variable's id string.</param>
    /// <param name="value">The integer value to store.</param>
    public void SetFurniVariable(Id furniId, string variableId, int value) =>
        SetObjectVariable(WiredVariableTarget.Furni, furniId, variableId, value);

    /// <summary>
    /// Writes a global wired variable value in the room. Returns immediately; no acknowledgement
    /// is sent.
    /// </summary>
    /// <param name="variableId">The variable's id string.</param>
    /// <param name="value">The integer value to store.</param>
    public void SetGlobalVariable(string variableId, int value) =>
        SetObjectVariable(WiredVariableTarget.Global, 0, variableId, value);

    /// <summary>
    /// Creates a wired variable on one object, taking the object id as a 32-bit value. Returns
    /// immediately; no acknowledgement is sent.
    /// </summary>
    /// <param name="target">The holder kind: 0 furni, 1 user, -10 global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The id string for the new variable.</param>
    /// <param name="value">The initial value; the game client sends 0 when none is given.</param>
    public void CreateObjectVariable(int target, int objectId, string variableId, int value = 0) =>
        CreateObjectVariable(target, (Id)(long)objectId, variableId, value);

    /// <summary>
    /// Creates a wired variable on one object. Returns immediately; no acknowledgement is sent.
    /// </summary>
    /// <param name="target">The holder kind: 0 furni, 1 user, -10 global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The id string for the new variable.</param>
    /// <param name="value">The initial value.</param>
    public void CreateObjectVariable(int target, Id objectId, string variableId, int value = 0) =>
        SetObjectVariable(target, objectId, variableId, value, WiredVariableOperation.Create);

    /// <summary>
    /// Deletes a wired variable from one object, taking the object id as a 32-bit value. Returns
    /// immediately; no acknowledgement is sent.
    /// </summary>
    /// <param name="target">The holder kind: 0 furni, 1 user, -10 global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The variable's id string.</param>
    public void DeleteObjectVariable(int target, int objectId, string variableId) =>
        DeleteObjectVariable(target, (Id)(long)objectId, variableId);

    /// <summary>
    /// Deletes a wired variable from one object. Returns immediately; no acknowledgement is sent.
    /// </summary>
    /// <param name="target">The holder kind: 0 furni, 1 user, -10 global.</param>
    /// <param name="objectId">The furni id, the user's room index, or 0 for global.</param>
    /// <param name="variableId">The variable's id string.</param>
    public void DeleteObjectVariable(int target, Id objectId, string variableId) =>
        SetObjectVariable(target, objectId, variableId, 0, WiredVariableOperation.Delete);

    /// <summary>
    /// Writes, creates or deletes a permanent variable on one entity and waits for the server's
    /// answer. Unlike the object-variable writes, this one is acknowledged.
    /// </summary>
    /// <param name="entityType">The entity kind; 1 is the user's own/default entity.</param>
    /// <param name="entityId">The entity's id.</param>
    /// <param name="variableId">The variable's id string.</param>
    /// <param name="value">The integer value to store; ignored for a delete.</param>
    /// <param name="operation">0 write, 1 create, 2 delete.</param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The result, which carries only a success flag.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
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

    public IDisposable WatchRoomVariables(
        Action<WiredVariableCollectionSnapshot> onChange,
        int intervalMs = 1000) =>
        watch_variable_collections(onChange, intervalMs);

    /// <summary>
    /// Asks for the room's wired settings: the modify and read permission masks and the room's
    /// wired timezone.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The current wired room settings.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredRoomSettings> GetWiredRoomSettings(int timeoutMs = 10000) =>
        wired_call<WiredTimeoutRequest, WiredRoomSettings>(
            ApplicationMemberIds.WiredRoomSettingsGet,
            new WiredTimeoutRequest(timeoutMs));

    /// <summary>
    /// Rewrites the room's wired settings. All three values travel together, so read the current
    /// settings first when only one of them should change. Returns immediately; the server answers
    /// by re-sending the settings.
    /// </summary>
    /// <param name="modifyPermissionMask">Who may edit wired in this room. Pairs with the viewer-facing "can modify" flag.</param>
    /// <param name="readPermissionMask">Who may see the wired menu. Pairs with the viewer-facing "can read" flag.</param>
    /// <param name="timezone">The room's wired timezone string.</param>
    public void SetWiredRoomSettings(int modifyPermissionMask, int readPermissionMask, string timezone) =>
        wired_background<WiredRoomSettingsSetRequest, WiredRoomSettings>(
            ApplicationMemberIds.WiredRoomSettingsSet,
            new WiredRoomSettingsSetRequest(
                modifyPermissionMask,
                readPermissionMask,
                timezone));

    /// <summary>
    /// Asks for the room's wired budget statistics: execution cost against its cap, the heavy-room
    /// flag, floor and wall item counts against their caps, and how many permanent furni, user and
    /// global variables are used out of the allowance.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The statistics.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredRoomStats> GetWiredRoomStats(int timeoutMs = 10000) =>
        wired_call<WiredTimeoutRequest, WiredRoomStats>(
            ApplicationMemberIds.WiredRoomStatsGet,
            new WiredTimeoutRequest(timeoutMs));

    /// <summary>
    /// Raised when the server states what the local user may do with the wired menu in this room.
    /// It is pushed on entering a room and when the menu is opened, so it is the reliable way to
    /// learn whether wired reads and writes will be accepted.
    /// </summary>
    /// <param name="handler">Receives the can-modify and can-read flags.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredPermissions(Action<WiredPermissions> handler) =>
        wired_event(ApplicationMemberIds.WiredPermissionsChanged, handler);

    /// <summary>
    /// Raised when the server describes the room's wired environment: whether a click-user wired
    /// exists, and which achievements wired may award. The achievement list is optional on the
    /// wire, so a null list means the server omitted the section rather than sent an empty one.
    /// </summary>
    /// <param name="handler">Receives the environment description.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredEnvironment(Action<WiredEnvironment> handler) =>
        wired_event(ApplicationMemberIds.WiredEnvironmentChanged, handler);

    /// <summary>
    /// Raised when a wired save was accepted. The message has no payload, so it does not say which
    /// box it acknowledges — pair it with the save that was just sent.
    /// </summary>
    /// <param name="handler">Receives the empty success message.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredSaveSuccess(Action<WiredSaveSuccess> handler) =>
        wired_save_event(
            true,
            _ => handler(new WiredSaveSuccess()));

    /// <summary>
    /// Raised when a wired save was rejected, carrying a localization key and its substitution
    /// parameters rather than a ready-made message.
    /// </summary>
    /// <param name="handler">Receives the validation error.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredValidationError(Action<WiredValidationError> handler) =>
        wired_save_event(
            false,
            result => handler(result.ValidationError!));

    /// <summary>
    /// Raised when a wired menu operation fails, carrying a numeric error code and nothing else.
    /// This is the usual answer to a wired request made without sufficient rights.
    /// </summary>
    /// <param name="handler">Receives the error code.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredMenuError(Action<WiredMenuError> handler) =>
        wired_event(ApplicationMemberIds.WiredMenuError, handler);

    /// <summary>
    /// Raised when the server reports the outcome of a wired reward, carrying only the reason
    /// code that explains why the reward was or was not given.
    /// </summary>
    /// <param name="handler">Receives the reason code.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredRewardResult(Action<WiredRewardResult> handler) =>
        wired_event(ApplicationMemberIds.WiredRewardResult, handler);

    /// <summary>
    /// Raised when the server sends the room's wired click options — what clicking a user and what
    /// clicking a furni should do while wired is active.
    /// </summary>
    /// <param name="handler">Receives the two option codes.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredClickSettings(Action<WiredClickSettings> handler) =>
        wired_event(ApplicationMemberIds.WiredClickSettingsChanged, handler);

    /// <summary>Asks for a page of the room's wired execution log.</summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">How many entries per page; the game client uses 50.</param>
    /// <param name="logLevelFilter">
    /// Keep only entries of this log level, or -1 for no level filter.
    /// </param>
    /// <param name="logSourceFilter">
    /// Keep only entries from this log source, or -1 for no source filter.
    /// </param>
    /// <param name="query">A free-text filter; empty means no text filter.</param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>
    /// The page, echoing the total entry count, the current page and the filters that were applied.
    /// A filter the server did not apply comes back as -1 or null.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
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
    /// Asks for the room's wired error statistics: one row per error kind with its name, category,
    /// how often it was thrown and how long ago it last happened.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The error rows. The whole list is returned at once, not paged.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredErrorLogs> GetWiredErrorLogs(int timeoutMs = 10000) =>
        wired_call<WiredTimeoutRequest, WiredErrorLogs>(
            ApplicationMemberIds.WiredRoomErrorLogsGet,
            new WiredTimeoutRequest(timeoutMs));

    /// <summary>
    /// Clears the room's wired error statistics. Returns immediately; the server sends no
    /// acknowledgement, so re-read the error log to confirm.
    /// </summary>
    public void ClearWiredErrorLogs() =>
        wired_send(
            ApplicationMemberIds.WiredRoomErrorLogsClear,
            new WiredCommandRequest());

    /// <summary>
    /// Sends the wired "user was clicked" message for one user in the room and waits for the
    /// server's answer, which is what a click-user wired reacts to.
    /// </summary>
    /// <param name="index">The user's room index, not their account id.</param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The response, which echoes the index and says whether a menu should open.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredClickUserResponse> ClickWiredUser(int index, int timeoutMs = 10000) =>
        wired_call<WiredUserClickRequest, WiredClickUserResponse>(
            ApplicationMemberIds.WiredRoomUserClick,
            new WiredUserClickRequest(index, timeoutMs));

    /// <summary>
    /// Reloads the room's state.
    /// </summary>
    /// <remarks>
    /// The wired menu's reload button. Nothing is discarded and nothing is asked; the hotel sends
    /// no acknowledgement, so this returns as soon as the request is away.
    /// </remarks>
    public void ReloadRoomState() =>
        wired_send(
            ApplicationMemberIds.WiredRoomReload,
            new WiredCommandRequest());

    /// <summary>
    /// Rolls the room back to its last saved state.
    /// </summary>
    /// <remarks>
    /// The wired menu's roll-back button, which the client only sends after the user confirms a
    /// warning: everything done since the last save is thrown away, furni included. There is no
    /// acknowledgement and no undo, so this returns as soon as the request is away.
    /// </remarks>
    public void RollBackRoomState() =>
        wired_send(
            ApplicationMemberIds.WiredRoomRollback,
            new WiredCommandRequest());

    /// <summary>
    /// Stores the local user's wired menu preferences: which buttons are shown, play-test mode,
    /// whether wired whispers are suppressed, whether all notifications are shown, and the UI
    /// style. Returns immediately; the server sends no acknowledgement.
    /// </summary>
    /// <param name="preferences">The complete preference set — all fields are sent together.</param>
    public void SetWiredPreferences(WiredSetPreferences preferences) =>
        wired_send(
            ApplicationMemberIds.WiredPreferencesSet,
            new WiredPreferencesSetRequest(preferences));

    private Task<TResult> wired_call<TRequest, TResult>(
        string member_id,
        TRequest request) =>
        Application.InvokeAsync<TRequest, TResult>(member_id, request, Ct).AsTask();

    private void wired_send<TRequest>(string member_id, TRequest request) =>
        Application.Invoke<TRequest, WiredDispatchResult>(member_id, request, Ct);

    private void wired_background<TRequest, TResult>(
        string member_id,
        TRequest request) =>
        StartObservedTask(
            () => Application.InvokeAsync<TRequest, TResult>(member_id, request, Ct).AsTask(),
            Ct);

    private IDisposable wired_event<T>(string member_id, Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<WiredEvent<T>>(
            member_id,
            Guarded<WiredEvent<T>>(value => handler(value.Value))));
    }

    private IDisposable wired_configuration_event(
        WiredConfigurationKind kind,
        Action<WiredConfigurationSnapshot> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<WiredEvent<WiredConfigurationSnapshot>>(
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
        return Track(Application.Subscribe<WiredEvent<WiredConfigurationSaveResult>>(
            ApplicationMemberIds.WiredConfigurationSaveResult,
            Guarded<WiredEvent<WiredConfigurationSaveResult>>(value =>
            {
                if (value.Value.Success == success)
                    handler(value.Value);
            })));
    }
}
