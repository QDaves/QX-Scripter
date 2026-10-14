using Qx.Game.Protocol;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class WiredApplication
{
    private async ValueTask<WiredStateUpdate> open_deposit_chest(WiredChestDepositRequest request,
        WiredUpdateQueue updates, WiredOperationScope scope, long started, CancellationToken cancellation_token)
    {
        for (int attempt = 0; ; attempt++)
        {
            CaptureCurrentState(scope, cancellation_token);
            long attempted = time_provider.GetTimestamp();
            int remaining = RemainingMilliseconds(started, request.TimeoutMilliseconds);
            if (remaining < 1)
                throw new RequestTimeoutException(MessageKeys.Wired.Chests.OpenRequest.Value,
                    MessageKeys.Wired.Chests.ItemsChunk.Value, request.TimeoutMilliseconds);
            if (attempt > 0)
                DispatchInRoom(MessageContracts.Wired.Chests.Close, new CloseChest(request.ChestId), scope, cancellation_token);
            DispatchInRoom(MessageContracts.Wired.Chests.OpenRequest, new OpenChestAndGetContents(request.ChestId), scope, cancellation_token);
            int window = attempt < 3 ? Math.Min(remaining, 500 << attempt) : remaining;
            try
            {
                return await updates.WaitAsync(candidate =>
                        candidate.Kind is WiredStateChangeKind.ChestItemsChunk &&
                        candidate.Value is WiredChestItemsChunkSnapshot chunk &&
                        (Id)chunk.ChestId == request.ChestId &&
                        candidate.State.Chests.Any(chest => chest.ChestId == request.ChestId && chest.ItemsComplete),
                    interceptor, scope.Session, time_provider, attempted, window, updates.Revision,
                    MessageKeys.Wired.Chests.OpenRequest.Value, MessageKeys.Wired.Chests.ItemsChunk.Value,
                    cancellation_token).ConfigureAwait(false);
            }
            catch (RequestTimeoutException) when (attempt < 3 && RemainingMilliseconds(started, request.TimeoutMilliseconds) > 0)
            {
            }
        }
    }
}
