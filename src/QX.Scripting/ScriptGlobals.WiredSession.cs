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
    /// The retained wired state: menu rights, environment, room settings and the configuration of
    /// the wired furni that was last opened.
    /// </summary>
    public WiredStateView Wired => GetWiredState();

    public WiredStateView GetWiredState(
        int chestOffset = 0,
        int chestLimit = 5,
        int itemOffset = 0,
        int itemLimit = 20) =>
        Application.Invoke<WiredStateRequest, WiredStateView>(
            ApplicationMemberIds.WiredState,
            new WiredStateRequest(chestOffset, chestLimit, itemOffset, itemLimit),
            Ct);

    /// <summary>
    /// Whether the local user may change wired in this room. False until the hotel has said,
    /// which it does on entering a room it considers the user able to configure.
    /// </summary>
    public bool CanModifyWired => Wired.CanModify;

    /// <summary>Whether the local user may read wired in this room.</summary>
    public bool CanReadWired => Wired.CanRead;

    /// <summary>
    /// Asks the hotel for a wired furni's configuration without waiting for it.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="GetWiredConfig(Id, int)"/>, which waits for the answer. Using the furni
    /// instead only makes the game client perform this same request.
    /// </remarks>
    /// <param name="furniId">The wired furni to open.</param>
    public void OpenWired(Id furniId) =>
        wired_send(
            ApplicationMemberIds.WiredConfigurationOpen,
            new WiredConfigurationOpenRequest(furniId));

    /// <summary>
    /// Commits a wired furni's current state as its restore snapshot.
    /// </summary>
    /// <param name="furniId">The wired furni whose snapshot to write.</param>
    public void ApplyWiredSnapshot(Id furniId) =>
        wired_send(
            ApplicationMemberIds.WiredConfigurationSnapshotApply,
            new WiredConfigurationApplySnapshotRequest(furniId));

    /// <summary>
    /// Requests a wired furni's configuration and waits for it.
    /// </summary>
    /// <param name="furniId">The wired furni to open.</param>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    /// <exception cref="RequestTimeoutException">No definition arrived in time.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped.</exception>
    public Task<WiredConfigurationSnapshot> GetWiredConfig(
        Id furniId,
        int timeoutMs = 10000) =>
        wired_call<WiredConfigurationGetRequest, WiredConfigurationSnapshot>(
            ApplicationMemberIds.WiredConfigurationGet,
            new WiredConfigurationGetRequest(furniId, timeoutMs));

    /// <summary>
    /// Subscribes to the hotel asking for a wired configuration to be opened, which is what it
    /// sends when the wired furni is used.
    /// </summary>
    /// <param name="handler">Receives the furni identifier.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWiredOpenRequested(Action<Id> handler) =>
        wired_event(ApplicationMemberIds.WiredConfigurationOpened, handler);

    /// <summary>
    /// Subscribes to every wired configuration that arrives, whatever its kind.
    /// </summary>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWiredConfig(Action<WiredConfigurationSnapshot> handler) =>
        wired_event(ApplicationMemberIds.WiredConfigurationReceived, handler);

    /// <summary>Subscribes to a wired transaction failing.</summary>
    /// <param name="handler">Receives the failure reason.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWiredTransactionFailed(Action<WiredTransactionFail> handler) =>
        wired_event(ApplicationMemberIds.WiredTransactionFailed, handler);

    /// <summary>Subscribes to notifications raised by a wired trade transaction.</summary>
    /// <param name="handler">Receives the notification identifier.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnWiredTradeNotification(Action<WiredTradeTransactionNotification> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeNotification, handler);
}
