using Qx.Game.Protocol;
using Qx.Messages;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class WiredApplication
{
    private ValueTask<WiredOverlaysView> read_overlays(WiredOverlaysRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.LabelOffset < 0 || request.LabelLimit is < 0 or > 250)
            throw new ArgumentOutOfRangeException(nameof(request));
        var scope = CaptureOperation(cancellation_token);
        return ValueTask.FromResult(overlay_view(scope, request.LabelOffset, request.LabelLimit, cancellation_token));
    }

    private WiredOverlaysView overlay_view(WiredOperationScope scope, int offset, int limit, CancellationToken cancellation_token)
    {
        var state = CaptureCurrentState(scope, cancellation_token);
        return new(game.Room.RoomId, scope.RoomGeneration, state.LabelsLoaded, state.Labels.Count, offset,
            Array.AsReadOnly(state.Labels.Skip(offset).Take(limit).ToArray()), state.LeaderboardsLoaded,
            state.Leaderboards, state.LeaderboardChunks);
    }

    private ValueTask<WiredFurnitureLabel> set_label_text(WiredLabelTextRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Update);
        return update_label(MessageContracts.Wired.Labels.TextUpdate, request.Update, request.Update.FurnitureId,
            request.ExpectedRoomGeneration, request.TimeoutMilliseconds,
            label => label.Text == request.Update.Text, cancellation_token);
    }

    private ValueTask<WiredFurnitureLabel> set_label_settings(WiredLabelSettingsRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Update);
        return update_label(MessageContracts.Wired.Labels.SettingsUpdate, request.Update, request.Update.FurnitureId,
            request.ExpectedRoomGeneration, request.TimeoutMilliseconds,
            label => label.Color == request.Update.Color && label.Visibility == request.Update.Visibility &&
                label.AlwaysOpen == request.Update.AlwaysOpen, cancellation_token);
    }

    private async ValueTask<WiredFurnitureLabel> update_label<T>(MessageContract<T> contract, T message, int id,
        long expected_room_generation, int timeout, Func<WiredFurnitureLabel, bool> matches,
        CancellationToken cancellation_token) where T : IParserComposer<T>
    {
        ValidateId((Id)id, nameof(id));
        ValidateTimeout(timeout);
        var scope = CaptureOperation(cancellation_token);
        using var operation = LinkCancellation(cancellation_token);
        long started = time_provider.GetTimestamp();
        await EnterExclusive(configuration_lock, started, timeout, "wired label update", "wired label confirmation", operation.Token).ConfigureAwait(false);
        try
        {
            void validate()
            {
                var state = CaptureCurrentState(scope, operation.Token);
                if (scope.RoomGeneration != expected_room_generation) throw new InvalidOperationException("The reviewed room is no longer current.");
                if (!state.CanModify) throw new InvalidOperationException("Wired editing permission is required.");
                if (!state.Labels.Any(label => label.FurnitureId == id && label.Region is null))
                    throw new InvalidOperationException("The Wired label is not present in the current room snapshot.");
            }
            validate();
            var existing = CaptureCurrentState(scope, operation.Token).Labels.Single(label => label.FurnitureId == id);
            if (matches(existing)) return existing;
            using var updates = new WiredUpdateQueue(wired, scope.WiredGeneration);
            DispatchInRoom(contract, message, scope, operation.Token, validate);
            var update = await updates.WaitAsync(candidate => candidate.Kind is WiredStateChangeKind.LabelsReceived or WiredStateChangeKind.LabelsUpdated &&
                candidate.State.Labels.Any(label => label.FurnitureId == id && matches(label)),
                interceptor, scope.Session, time_provider, started, timeout, updates.Revision,
                "wired label update", "wired label confirmation", operation.Token).ConfigureAwait(false);
            CaptureCurrentState(scope, operation.Token);
            return update.State.Labels.Single(label => label.FurnitureId == id);
        }
        finally { configuration_lock.Release(); }
    }

    private async ValueTask<WiredOverlaysView> refresh_leaderboards(WiredLeaderboardsRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        var scope = CaptureOperation(cancellation_token);
        using var operation = LinkCancellation(cancellation_token);
        long started = time_provider.GetTimestamp();
        await EnterExclusive(configuration_lock, started, request.TimeoutMilliseconds,
            "leaderboard subscription", "leaderboard snapshot", operation.Token).ConfigureAwait(false);
        try
        {
            var state = CaptureCurrentState(scope, operation.Token);
            if (state.LeaderboardsLoaded && WiredManager.same_subscription(state.LeaderboardSubscription, VariableLeaderboardReplaceSubscription.RoomOpen))
                return overlay_view(scope, 0, 100, operation.Token);
            using var updates = new WiredUpdateQueue(wired, scope.WiredGeneration);
            send_leaderboard_subscription(VariableLeaderboardReplaceSubscription.RoomOpen, scope, operation.Token);
            await updates.WaitAsync(candidate => candidate.Kind == WiredStateChangeKind.LeaderboardsReceived,
                interceptor, scope.Session, time_provider, started, request.TimeoutMilliseconds, updates.Revision,
                MessageKeys.Wired.Leaderboards.SubscriptionReplace.Value, MessageKeys.Wired.Leaderboards.Snapshot.Value,
                operation.Token).ConfigureAwait(false);
            return overlay_view(scope, 0, 100, operation.Token);
        }
        finally { configuration_lock.Release(); }
    }

    private async ValueTask<WiredLeaderboardPage> read_leaderboard_page(WiredLeaderboardPageRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        if (request.Page < 1) throw new ArgumentOutOfRangeException(nameof(request.Page));
        var scope = CaptureOperation(cancellation_token);
        using var operation = LinkCancellation(cancellation_token);
        long started = time_provider.GetTimestamp();
        await EnterExclusive(configuration_lock, started, request.TimeoutMilliseconds,
            "leaderboard subscription", "leaderboard rows", operation.Token).ConfigureAwait(false);
        try
        {
            WiredLeaderboardDescriptor validate()
            {
                var state = CaptureCurrentState(scope, operation.Token);
                if (scope.RoomGeneration != request.ExpectedRoomGeneration) throw new InvalidOperationException("The reviewed room is no longer current.");
                var board = state.Leaderboards.FirstOrDefault(board => board.OverlayInstanceId == request.OverlayInstanceId && board.QueryRevision == request.QueryRevision)
                    ?? throw new InvalidOperationException("Refresh the leaderboard descriptors before reading this page.");
                if (!board.Choices.Any(choice => choice.VariableId == request.VariableId))
                    throw new ArgumentException("The variable is not offered by this leaderboard.", nameof(request.VariableId));
                return board;
            }
            var board = validate();
            int offset = checked((request.Page - 1) * board.EntriesPerPage);
            int first = offset / 50, last = checked(offset + board.EntriesPerPage - 1) / 50;
            int[] indices = first == last ? [first] : [first, last];
            var subscription = new VariableLeaderboardReplaceSubscription(request.OverlayInstanceId, request.QueryRevision, request.VariableId, indices);
            using var updates = new WiredUpdateQueue(wired, scope.WiredGeneration);
            var state = CaptureCurrentState(scope, operation.Token);
            bool subscribed = WiredManager.same_subscription(state.LeaderboardSubscription, subscription);
            var chunks = subscribed || state.LeaderboardSubscription is null ? state.LeaderboardChunks.Where(chunk => chunk.OverlayInstanceId == request.OverlayInstanceId &&
                chunk.QueryRevision == request.QueryRevision && chunk.SelectedVariableId == request.VariableId && indices.Contains(chunk.ChunkIndex))
                .ToDictionary(chunk => chunk.ChunkIndex) : new Dictionary<int, VariableLeaderboardChunkUpdate>();
            if (!subscribed)
                send_leaderboard_subscription(subscription, scope, operation.Token, () => validate());
            long revision = updates.Revision;
            while (chunks.Count < indices.Length)
            {
                var update = await updates.WaitAsync(candidate => candidate.Kind == WiredStateChangeKind.LeaderboardChunkUpdated &&
                    candidate.Value is VariableLeaderboardChunkUpdate chunk && chunk.OverlayInstanceId == request.OverlayInstanceId &&
                    chunk.QueryRevision == request.QueryRevision && chunk.SelectedVariableId == request.VariableId && indices.Contains(chunk.ChunkIndex),
                    interceptor, scope.Session, time_provider, started, request.TimeoutMilliseconds, revision,
                    MessageKeys.Wired.Leaderboards.SubscriptionReplace.Value, MessageKeys.Wired.Leaderboards.ChunkUpdate.Value,
                    operation.Token).ConfigureAwait(false);
                validate();
                var chunk = (VariableLeaderboardChunkUpdate)update.Value!;
                chunks[chunk.ChunkIndex] = chunk;
                revision = update.State.Revision;
            }
            var rows = indices.SelectMany(index => chunks[index].Rows).Skip(offset - first * 50).Take(board.EntriesPerPage).ToArray();
            return new(board.OverlayInstanceId, board.QueryRevision, request.VariableId, request.Page, board.EntriesPerPage,
                chunks[last].TotalCount, Array.AsReadOnly(rows));
        }
        finally { configuration_lock.Release(); }
    }

    private void send_leaderboard_subscription(VariableLeaderboardReplaceSubscription subscription,
        WiredOperationScope scope, CancellationToken cancellation_token, Action? validation = null)
    {
        CaptureCurrentState(scope, cancellation_token);
        validation?.Invoke();
        wired.RecordLeaderboardSubscription(subscription, scope.WiredGeneration);
        try
        {
            DispatchInRoom(MessageContracts.Wired.Leaderboards.SubscriptionReplace, subscription, scope, cancellation_token, validation);
        }
        catch
        {
            wired.ForgetLeaderboardSubscription(subscription, scope.WiredGeneration);
            throw;
        }
    }
}
