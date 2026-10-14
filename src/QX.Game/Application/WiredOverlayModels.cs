using Qx.Model.Wired;

namespace Qx.Game.Application;

public sealed record WiredOverlaysRequest(int LabelOffset = 0, int LabelLimit = 100);

public sealed record WiredOverlaysView(Id RoomId, long RoomGeneration, bool LabelsLoaded, int LabelCount,
    int LabelOffset, IReadOnlyList<WiredFurnitureLabel> Labels, bool LeaderboardsLoaded,
    IReadOnlyList<WiredLeaderboardDescriptor> Leaderboards, IReadOnlyList<VariableLeaderboardChunkUpdate> Chunks);

public sealed record WiredLabelTextRequest(SetFurnitureLabelData Update, long ExpectedRoomGeneration,
    int TimeoutMilliseconds = 5000);

public sealed record WiredLabelSettingsRequest(SetFurnitureLabelSettings Update, long ExpectedRoomGeneration,
    int TimeoutMilliseconds = 5000);

public sealed record WiredLeaderboardsRequest(int TimeoutMilliseconds = 5000);

public sealed record WiredLeaderboardPageRequest(int OverlayInstanceId, int QueryRevision, string VariableId,
    int Page, long ExpectedRoomGeneration, int TimeoutMilliseconds = 5000);

public sealed record WiredLeaderboardPage(int OverlayInstanceId, int QueryRevision, string VariableId,
    int Page, int PageSize, int TotalCount, IReadOnlyList<WiredLeaderboardRow> Rows);
