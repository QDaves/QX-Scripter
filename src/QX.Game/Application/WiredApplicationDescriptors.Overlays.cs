using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static partial class WiredApplicationDescriptors
{
    public static ApplicationDescriptor Overlays { get; } = Call<WiredOverlaysRequest, WiredOverlaysView>(
        ApplicationMemberIds.WiredOverlays, "Read Wired labels and leaderboards",
        "Reads cached labels, regions, leaderboard definitions and the most recently received chunks. Loaded flags distinguish an empty result from missing data. Sends nothing.",
        [new("label_offset", typeof(int), false, 0, "First label index.", new(Minimum: 0)),
         new("label_limit", typeof(int), false, 100, "Maximum labels returned.", new(Minimum: 0, Maximum: 250))],
        [], new(true, false, true, false));

    public static ApplicationDescriptor LabelText { get; } = Call<WiredLabelTextRequest, WiredFurnitureLabel>(
        ApplicationMemberIds.WiredLabelText, "Set Wired label text",
        "Updates a known label in the reviewed room with editing permission and waits for the matching server update. Does not open an editor.",
        [new("update", typeof(SetFurnitureLabelData), true, null, "Label furniture ID and replacement text."),
         new("expected_room_generation", typeof(long), true, null, "Room generation from the overlay read."), TimeoutParameter(5000)],
        [Send(MessageKeys.Wired.Labels.TextUpdate), Observe(MessageKeys.Wired.Labels.Updated)], WriteHints(false, true));

    public static ApplicationDescriptor LabelSettings { get; } = Call<WiredLabelSettingsRequest, WiredFurnitureLabel>(
        ApplicationMemberIds.WiredLabelSettings, "Set Wired label settings",
        "Updates a known label's color, visibility and always-open setting, then waits for server confirmation. Does not open an editor.",
        [new("update", typeof(SetFurnitureLabelSettings), true, null, "Label ID and complete display settings."),
         new("expected_room_generation", typeof(long), true, null, "Room generation from the overlay read."), TimeoutParameter(5000)],
        [Send(MessageKeys.Wired.Labels.SettingsUpdate), Observe(MessageKeys.Wired.Labels.Updated)], WriteHints(false, true));

    public static ApplicationDescriptor LeaderboardsRefresh { get; } = Call<WiredLeaderboardsRequest, WiredOverlaysView>(
        ApplicationMemberIds.WiredLeaderboardsRefresh, "Refresh Wired leaderboards",
        "Requests the room's current leaderboard definitions without displaying a window. Replaces the client leaderboard subscription.",
        [TimeoutParameter(5000)],
        [Send(MessageKeys.Wired.Leaderboards.SubscriptionReplace), Observe(MessageKeys.Wired.Leaderboards.Snapshot)], ReadHints());

    public static ApplicationDescriptor LeaderboardPage { get; } = Call<WiredLeaderboardPageRequest, WiredLeaderboardPage>(
        ApplicationMemberIds.WiredLeaderboardPage, "Read a Wired leaderboard page",
        "Reads one one-based page from a current leaderboard descriptor. Subscribes to the one or two 50-row chunks needed; repeated reads use the server-pushed cache because identical subscriptions need not receive another response. Preserves exact values and timestamps. Replaces the client subscription without opening a window.",
        [new("overlay_instance_id", typeof(int), true, null, "Overlay identity from the current descriptor."),
         new("query_revision", typeof(int), true, null, "Query revision from that descriptor."),
         new("variable_id", typeof(string), true, null, "A variable offered by the descriptor."),
         new("page", typeof(int), true, null, "One-based page number.", new(Minimum: 1)),
         new("expected_room_generation", typeof(long), true, null, "Room generation from the overlay read."), TimeoutParameter(5000)],
        [Send(MessageKeys.Wired.Leaderboards.SubscriptionReplace), Observe(MessageKeys.Wired.Leaderboards.ChunkUpdate)], ReadHints());
}
