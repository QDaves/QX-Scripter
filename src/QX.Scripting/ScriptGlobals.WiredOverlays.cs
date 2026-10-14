using Qx.Game.Application;
using Qx.Model.Wired;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    public WiredOverlaysView GetWiredOverlays(int labelOffset = 0, int labelLimit = 100) =>
        _application.Invoke<WiredOverlaysRequest, WiredOverlaysView>(ApplicationMemberIds.WiredOverlays, new(labelOffset, labelLimit), Ct);

    public Task<WiredFurnitureLabel> SetWiredLabelText(SetFurnitureLabelData update, long expectedRoomGeneration, int timeoutMilliseconds = 5000) =>
        wired_call<WiredLabelTextRequest, WiredFurnitureLabel>(ApplicationMemberIds.WiredLabelText, new(update, expectedRoomGeneration, timeoutMilliseconds));

    public Task<WiredFurnitureLabel> SetWiredLabelSettings(SetFurnitureLabelSettings update, long expectedRoomGeneration, int timeoutMilliseconds = 5000) =>
        wired_call<WiredLabelSettingsRequest, WiredFurnitureLabel>(ApplicationMemberIds.WiredLabelSettings, new(update, expectedRoomGeneration, timeoutMilliseconds));

    public Task<WiredOverlaysView> RefreshWiredLeaderboards(int timeoutMilliseconds = 5000) =>
        wired_call<WiredLeaderboardsRequest, WiredOverlaysView>(ApplicationMemberIds.WiredLeaderboardsRefresh, new(timeoutMilliseconds));

    public Task<WiredLeaderboardPage> GetWiredLeaderboardPage(int overlayInstanceId, int queryRevision, string variableId,
        int page, long expectedRoomGeneration, int timeoutMilliseconds = 5000) =>
        wired_call<WiredLeaderboardPageRequest, WiredLeaderboardPage>(ApplicationMemberIds.WiredLeaderboardPage,
            new(overlayInstanceId, queryRevision, variableId, page, expectedRoomGeneration, timeoutMilliseconds));
}
