using Qx.Game.Protocol;
using Qx.Model.Wired;

namespace Qx.Game;

public sealed partial class WiredManager
{
    private readonly Dictionary<int, WiredFurnitureLabel> labels = [];
    private readonly Dictionary<int, WiredLeaderboardDescriptor> leaderboards = [];
    private readonly Dictionary<(int Overlay, int Chunk), VariableLeaderboardChunkUpdate> leaderboard_chunks = [];
    private bool labels_loaded;
    private bool leaderboards_loaded;
    private VariableLeaderboardReplaceSubscription? leaderboard_subscription;

    private void observe_overlays()
    {
        OnOutgoing(MessageContracts.Wired.Leaderboards.SubscriptionReplace, (message, state_generation) =>
        {
            var value = message with { ChunkIndices = Array.AsReadOnly(message.ChunkIndices.ToArray()) };
            Store(state_generation, WiredStateChangeKind.LeaderboardSubscriptionChanged, value, () =>
            {
                remember_subscription(value);
            });
        });
        OnIncoming(MessageContracts.Wired.Labels.Snapshot, (message, state_generation) =>
        {
            var entries = Array.AsReadOnly(message.Entries.Select(copy_label).ToArray());
            Store(state_generation, WiredStateChangeKind.LabelsReceived, new FurnitureLabelsAndRegions(entries), () =>
            {
                labels.Clear();
                foreach (var entry in entries) labels[entry.FurnitureId] = entry;
                labels_loaded = true;
            });
        });
        OnIncoming(MessageContracts.Wired.Labels.Updated, (message, state_generation) =>
        {
            var value = new FurnitureLabelsAndRegionsUpdate(
                Array.AsReadOnly(message.AddedOrUpdated.Select(copy_label).ToArray()),
                Array.AsReadOnly(message.RemovedIds.ToArray()));
            Store(state_generation, WiredStateChangeKind.LabelsUpdated, value, () =>
            {
                foreach (var entry in value.AddedOrUpdated) labels[entry.FurnitureId] = entry;
                foreach (int id in value.RemovedIds) labels.Remove(id);
            });
        });
        OnIncoming(MessageContracts.Wired.Leaderboards.Snapshot, (message, state_generation) =>
        {
            var descriptors = Array.AsReadOnly(message.Descriptors.Select(copy_leaderboard).ToArray());
            Store(state_generation, WiredStateChangeKind.LeaderboardsReceived, new VariableLeaderboardSnapshot(descriptors), () =>
            {
                leaderboards.Clear();
                foreach (var descriptor in descriptors) leaderboards[descriptor.OverlayInstanceId] = descriptor;
                foreach (var key in leaderboard_chunks.Keys.ToArray())
                {
                    var chunk = leaderboard_chunks[key];
                    if (!leaderboards.TryGetValue(key.Overlay, out var board) || board.QueryRevision != chunk.QueryRevision ||
                        !board.Choices.Any(choice => choice.VariableId == chunk.SelectedVariableId))
                        leaderboard_chunks.Remove(key);
                }
                leaderboards_loaded = true;
            });
        });
        OnIncoming(MessageContracts.Wired.Leaderboards.ChunkUpdate, (message, state_generation) =>
        {
            var value = message with { Rows = Array.AsReadOnly(message.Rows.ToArray()) };
            Store(state_generation, WiredStateChangeKind.LeaderboardChunkUpdated, value, () =>
            {
                if (!leaderboards.TryGetValue(value.OverlayInstanceId, out var board) ||
                    board.QueryRevision != value.QueryRevision || value.ChunkIndex < 0 ||
                    !board.Choices.Any(choice => choice.VariableId == value.SelectedVariableId)) return;
                foreach (var key in leaderboard_chunks.Keys.Where(key => key.Overlay == value.OverlayInstanceId).ToArray())
                    if (leaderboard_chunks[key].SelectedVariableId != value.SelectedVariableId)
                        leaderboard_chunks.Remove(key);
                var current_key = (value.OverlayInstanceId, value.ChunkIndex);
                if (!leaderboard_chunks.ContainsKey(current_key))
                {
                    var previous = leaderboard_chunks.Keys.Where(key => key.Overlay == value.OverlayInstanceId).ToArray();
                    if (previous.Length >= 2) leaderboard_chunks.Remove(previous[0]);
                }
                leaderboard_chunks[current_key] = value;
            });
        });
    }

    private void clear_overlays()
    {
        labels.Clear();
        leaderboards.Clear();
        leaderboard_chunks.Clear();
        labels_loaded = false;
        leaderboards_loaded = false;
        leaderboard_subscription = null;
    }

    internal void RecordLeaderboardSubscription(VariableLeaderboardReplaceSubscription value, long expected_generation)
    {
        value.Validate();
        lock (publication_sync)
        {
            if (Snapshot.Generation != expected_generation) throw new InvalidOperationException("The leaderboard room is no longer current.");
            var copy = value with { ChunkIndices = Array.AsReadOnly(value.ChunkIndices.ToArray()) };
            Store(CurrentStateGeneration, WiredStateChangeKind.LeaderboardSubscriptionChanged, copy, () => remember_subscription(copy));
        }
    }

    internal void ForgetLeaderboardSubscription(VariableLeaderboardReplaceSubscription value, long expected_generation)
    {
        lock (publication_sync)
        {
            if (Snapshot.Generation != expected_generation || !same_subscription(leaderboard_subscription, value)) return;
            Store(CurrentStateGeneration, WiredStateChangeKind.LeaderboardSubscriptionChanged, null, () =>
            {
                leaderboard_subscription = null;
                leaderboard_chunks.Clear();
            });
        }
    }

    private void remember_subscription(VariableLeaderboardReplaceSubscription value)
    {
        if (leaderboard_subscription is null)
        {
            foreach (var key in leaderboard_chunks.Keys.ToArray())
            {
                var chunk = leaderboard_chunks[key];
                if (chunk.OverlayInstanceId != value.OverlayInstanceId || chunk.QueryRevision != value.QueryRevision ||
                    chunk.SelectedVariableId != value.SelectedVariableId || !value.ChunkIndices.Contains(chunk.ChunkIndex))
                    leaderboard_chunks.Remove(key);
            }
        }
        else if (!same_subscription(leaderboard_subscription, value)) leaderboard_chunks.Clear();
        leaderboard_subscription = value;
    }

    internal static bool same_subscription(VariableLeaderboardReplaceSubscription? left, VariableLeaderboardReplaceSubscription right) =>
        left is not null && left.OverlayInstanceId == right.OverlayInstanceId && left.QueryRevision == right.QueryRevision &&
        left.SelectedVariableId == right.SelectedVariableId && left.ChunkIndices.Order().SequenceEqual(right.ChunkIndices.Order());

    private static WiredFurnitureLabel copy_label(WiredFurnitureLabel value) => value with
    {
        Region = value.Region is { } region ? region with { Areas = Array.AsReadOnly(region.Areas.ToArray()) } : null
    };

    private static WiredLeaderboardDescriptor copy_leaderboard(WiredLeaderboardDescriptor value) => value with
    {
        Columns = Array.AsReadOnly(value.Columns.ToArray()),
        Choices = Array.AsReadOnly(value.Choices.ToArray())
    };
}
