using Qx.Game.Application;
using Qx.Model.Wired;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>Reads a WIRED Link's connected network, own-room receiving and every network of the account.</summary>
    /// <param name="linkId">The WIRED Link furniture ID in the current room.</param>
    /// <param name="hideWindow">True keeps the client's Link window from opening.</param>
    /// <param name="timeoutMs">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
    /// <returns>The configuration; the selected network is <see cref="WiredLinkNetworks.Disconnected"/> while disconnected.</returns>
    public Task<WiredLinkConfiguration> GetWiredLinkConfiguration(Id linkId, bool hideWindow = true, int timeoutMs = 10000) =>
        wired_call<WiredLinkRequest, WiredLinkConfiguration>(
            ApplicationMemberIds.WiredLinkConfigurationGet, new WiredLinkRequest(linkId, hideWindow, timeoutMs));

    /// <summary>Connects a WIRED Link to a network, or disconnects it, and sets own-room receiving.</summary>
    /// <param name="linkId">The WIRED Link furniture ID in the current room.</param>
    /// <param name="networkId">The network identifier, or <see cref="WiredLinkNetworks.Disconnected"/>.</param>
    /// <param name="receiveLocal">True to receive messages and dataframes sent from this room.</param>
    /// <param name="hideWindow">True keeps the client's Link window from updating.</param>
    /// <param name="timeoutMs">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
    /// <returns>The hotel result; check <see cref="WiredLinkOperationResult.Succeeded"/>.</returns>
    public Task<WiredLinkOperationResult> SaveWiredLinkConfiguration(Id linkId, string networkId, bool receiveLocal,
        bool hideWindow = true, int timeoutMs = 10000) =>
        wired_call<WiredLinkSaveRequest, WiredLinkOperationResult>(
            ApplicationMemberIds.WiredLinkConfigurationSave,
            new WiredLinkSaveRequest(linkId, networkId, receiveLocal, hideWindow, timeoutMs));

    /// <summary>Creates a network owned by the account. It does not connect the Link.</summary>
    /// <param name="linkId">The WIRED Link furniture ID in the current room.</param>
    /// <param name="name">The exact, case-sensitive network name, 1 to 255 characters with visible text.</param>
    /// <param name="hideWindow">True keeps the client's Link window from updating.</param>
    /// <param name="timeoutMs">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
    /// <returns>The hotel result; on success, <see cref="WiredLinkOperationResult.Networks"/> holds the new network.</returns>
    public Task<WiredLinkOperationResult> CreateWiredLinkNetwork(Id linkId, string name, bool hideWindow = true, int timeoutMs = 10000) =>
        wired_call<WiredLinkNetworkCreateRequest, WiredLinkOperationResult>(
            ApplicationMemberIds.WiredLinkNetworkCreate, new WiredLinkNetworkCreateRequest(linkId, name, hideWindow, timeoutMs));

    /// <summary>Renames a network owned by the account. Existing connections are unchanged.</summary>
    /// <param name="linkId">The WIRED Link furniture ID in the current room.</param>
    /// <param name="networkId">The network identifier.</param>
    /// <param name="name">The new exact network name, 1 to 255 characters with visible text.</param>
    /// <param name="hideWindow">True keeps the client's Link window from updating.</param>
    /// <param name="timeoutMs">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
    /// <returns>The hotel result.</returns>
    public Task<WiredLinkOperationResult> RenameWiredLinkNetwork(Id linkId, string networkId, string name, bool hideWindow = true,
        int timeoutMs = 10000) =>
        wired_call<WiredLinkNetworkRenameRequest, WiredLinkOperationResult>(
            ApplicationMemberIds.WiredLinkNetworkRename,
            new WiredLinkNetworkRenameRequest(linkId, networkId, name, hideWindow, timeoutMs));

    /// <summary>Deletes a network owned by the account and disconnects its Links in every room.</summary>
    /// <param name="linkId">The WIRED Link furniture ID in the current room.</param>
    /// <param name="networkId">The network identifier.</param>
    /// <param name="hideWindow">True keeps the client's Link window from updating.</param>
    /// <param name="timeoutMs">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
    /// <returns>The hotel result; <see cref="WiredLinkOperationResult.RemovedNetworkId"/> names the deleted network.</returns>
    public Task<WiredLinkOperationResult> DeleteWiredLinkNetwork(Id linkId, string networkId, bool hideWindow = true, int timeoutMs = 10000) =>
        wired_call<WiredLinkNetworkRequest, WiredLinkOperationResult>(
            ApplicationMemberIds.WiredLinkNetworkDelete, new WiredLinkNetworkRequest(linkId, networkId, hideWindow, timeoutMs));

    /// <summary>Lists every Link connected to a network, including saved connections in unloaded rooms.</summary>
    /// <param name="linkId">The WIRED Link furniture ID in the current room.</param>
    /// <param name="networkId">The network identifier.</param>
    /// <param name="hideWindow">True keeps the client's Link window from updating.</param>
    /// <param name="timeoutMs">The total timeout including queue time, from 1 to 120000 milliseconds.</param>
    /// <returns>The connected Links.</returns>
    public Task<WiredLinkConnections> GetWiredLinkConnections(Id linkId, string networkId, bool hideWindow = true, int timeoutMs = 10000) =>
        wired_call<WiredLinkNetworkRequest, WiredLinkConnections>(
            ApplicationMemberIds.WiredLinkConnectionsGet, new WiredLinkNetworkRequest(linkId, networkId, hideWindow, timeoutMs));
}
