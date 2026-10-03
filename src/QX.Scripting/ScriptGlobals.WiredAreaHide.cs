using Qx.Game.Application;
using Qx.Model.Wired;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>Reads area-hide settings from retained room furniture without sending.</summary>
    /// <param name="furniId">The area-hide furniture identity.</param>
    /// <returns>The editor settings and current room generation.</returns>
    public WiredAreaHideView GetAreaHide(Id furniId) =>
        _application.Invoke<WiredAreaHideGetRequest, WiredAreaHideView>(ApplicationMemberIds.WiredAreaHideGet, new(furniId), Ct);

    /// <summary>Sends complete area-hide settings while the effect is off.</summary>
    /// <param name="update">The rectangle and options.</param>
    /// <param name="expectedRoomGeneration">The room generation from GetAreaHide.</param>
    /// <returns>A dispatch receipt, not a server acknowledgement.</returns>
    public Task<WiredDispatchResult> SetAreaHide(SetAreaHideData update, long expectedRoomGeneration) =>
        wired_call<WiredAreaHideSetRequest, WiredDispatchResult>(ApplicationMemberIds.WiredAreaHideSet, new(update, expectedRoomGeneration));

    /// <summary>Toggles area hiding with the client's furniture-use parameter zero.</summary>
    /// <param name="furniId">The area-hide furniture identity.</param>
    /// <param name="expectedRoomGeneration">The room generation from GetAreaHide.</param>
    /// <returns>A dispatch receipt, not a server acknowledgement.</returns>
    public Task<WiredDispatchResult> ToggleAreaHide(Id furniId, long expectedRoomGeneration) =>
        wired_call<WiredAreaHideToggleRequest, WiredDispatchResult>(ApplicationMemberIds.WiredAreaHideToggle, new(furniId, expectedRoomGeneration));
}
