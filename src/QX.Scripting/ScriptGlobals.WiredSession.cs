using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Model.Wired;

namespace Qx.Scripting;

/// <content>
/// Opening wired configurations and the retained wired state, on top of the request helpers that
/// read variables, logs and settings.
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Gets a snapshot of the retained wired state, such as menu rights, environment, room settings
    /// and the configuration of the wired furni that was last opened.
    /// </summary>
    /// <remarks>
    /// Every read builds a new view with the first page of up to 5 opened chests and up to 20 items
    /// per chest. Use <see cref="GetWiredState(int, int, int, int)"/> to page through more.
    /// </remarks>
    public WiredStateView Wired => GetWiredState();

    /// <summary>
    /// Gets a snapshot of the retained wired state with one page of the opened chests.
    /// </summary>
    /// <remarks>
    /// Nothing is sent to the server. Chests are ordered by id, and only chests the server has
    /// reported contents or results for in this room are included.
    /// </remarks>
    /// <param name="chestOffset">The number of chests to skip.</param>
    /// <param name="chestLimit">The maximum number of chests to include, from 0 to 10.</param>
    /// <param name="itemOffset">The number of items to skip in each chest.</param>
    /// <param name="itemLimit">The maximum number of items to include per chest, from 0 to 50.</param>
    /// <returns>The current wired state.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an offset is negative or a limit is out of range.</exception>
    public WiredStateView GetWiredState(
        int chestOffset = 0,
        int chestLimit = 5,
        int itemOffset = 0,
        int itemLimit = 20) =>
        _application.Invoke<WiredStateRequest, WiredStateView>(
            ApplicationMemberIds.WiredState,
            new WiredStateRequest(chestOffset, chestLimit, itemOffset, itemLimit),
            Ct);

    /// <summary>
    /// Gets whether the local user may change wired in this room.
    /// </summary>
    /// <remarks>
    /// It is <see langword="false"/> until the hotel has sent the wired permissions, which it does
    /// on entering a room it considers the user able to configure.
    /// </remarks>
    public bool CanModifyWired => Wired.CanModify;

    /// <summary>Gets whether the local user may read wired in this room.</summary>
    /// <remarks>It is <see langword="false"/> until the hotel has sent the wired permissions.</remarks>
    public bool CanReadWired => Wired.CanRead;

    /// <summary>
    /// Asks the hotel for a wired furni's configuration without waiting for it.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="GetWiredConfig(Id, int)"/>, which waits for the answer. Using the furni
    /// instead only makes the game client perform this same request.
    /// </remarks>
    /// <param name="furniId">The wired furni to open.</param>
    /// <exception cref="InvalidOperationException">Thrown when no hotel session is connected or no room is active.</exception>
    public void OpenWired(Id furniId) =>
        wired_send(
            ApplicationMemberIds.WiredConfigurationOpen,
            new WiredConfigurationOpenRequest(furniId));

    /// <summary>
    /// Commits a wired furni's current state as its restore snapshot.
    /// </summary>
    /// <remarks>The request is sent without waiting for an answer.</remarks>
    /// <param name="furniId">The wired furni whose snapshot to write.</param>
    /// <exception cref="InvalidOperationException">Thrown when no hotel session is connected or no room is active.</exception>
    public void ApplyWiredSnapshot(Id furniId) =>
        wired_send(
            ApplicationMemberIds.WiredConfigurationSnapshotApply,
            new WiredConfigurationApplySnapshotRequest(furniId));

    /// <summary>
    /// Requests a wired furni's configuration and waits for it.
    /// </summary>
    /// <remarks>
    /// Concurrent calls are served one at a time, and the time spent waiting for an earlier call
    /// counts against <paramref name="timeoutMs"/>.
    /// </remarks>
    /// <param name="furniId">The wired furni to open.</param>
    /// <param name="timeoutMs">The total timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The configuration of the furni.</returns>
    /// <exception cref="RequestTimeoutException">Thrown when no configuration arrived in time.</exception>
    /// <exception cref="RequestDisconnectedException">Thrown when the session or room changed while waiting.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped.</exception>
    public Task<WiredConfigurationSnapshot> GetWiredConfig(
        Id furniId,
        int timeoutMs = 10000) =>
        wired_call<WiredConfigurationGetRequest, WiredConfigurationSnapshot>(
            ApplicationMemberIds.WiredConfigurationGet,
            new WiredConfigurationGetRequest(furniId, timeoutMs));

    /// <summary>
    /// Registers a handler that runs when the hotel asks for a wired configuration to be opened,
    /// which is what it sends when the wired furni is used.
    /// </summary>
    /// <param name="handler">The handler to call with the furni id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredOpenRequested(Action<Id> handler) =>
        wired_event(ApplicationMemberIds.WiredConfigurationOpened, handler);

    /// <summary>
    /// Registers a handler that runs when any wired configuration arrives, whatever its kind.
    /// </summary>
    /// <param name="handler">The handler to call with the configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredConfig(Action<WiredConfigurationSnapshot> handler) =>
        wired_event(ApplicationMemberIds.WiredConfigurationReceived, handler);

    /// <summary>Registers a handler that runs when a wired transaction fails.</summary>
    /// <param name="handler">The handler to call with the failure reason.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTransactionFailed(Action<WiredTransactionFail> handler) =>
        wired_event(ApplicationMemberIds.WiredTransactionFailed, handler);

    /// <summary>Registers a handler that runs when a wired trade transaction raises a notification.</summary>
    /// <param name="handler">The handler to call with the notification.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTradeNotification(Action<WiredTradeTransactionNotification> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeNotification, handler);
}
