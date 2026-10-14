using Qx.Game.Protocol;
using Qx.Model.Wired;

namespace Qx.Game.Application;

public sealed record WiredSharedAddonsSynchronizeRequest(Id FurniId, long ExpectedGeneration, long ExpectedRevision);

internal sealed partial class WiredApplication
{
    private ValueTask<WiredDispatchResult> synchronize_shared_addons(
        WiredSharedAddonsSynchronizeRequest request, CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.FurniId, nameof(request.FurniId));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((long)request.FurniId, int.MaxValue, nameof(request.FurniId));
        var scope = CaptureOperation(cancellation_token);
        using var operation = LinkCancellation(cancellation_token);
        void validate()
        {
            var state = CaptureCurrentState(scope, operation.Token);
            if (state.Generation != request.ExpectedGeneration || state.Revision != request.ExpectedRevision ||
                state.Configuration?.Id != request.FurniId)
                throw new WiredFormReviewException();
            if (state.Permissions?.CanModify != true)
                throw new UnauthorizedAccessException("Current Wired write permission is required to synchronize shared add-ons.");
            if (state.Configuration.Kind != WiredConfigurationKind.Addon || state.Configuration.Code != 1003)
                throw new InvalidOperationException("Shared add-on synchronization requires a reviewed Variable Add-on Share configuration.");
            if (game.Room.Capture(room => room.FloorItem(request.FurniId)) is null)
                throw new InvalidOperationException("The reviewed Variable Add-on Share furniture is no longer in the room.");
        }
        DispatchInRoom(MessageContracts.Wired.SharedAddons.Synchronize,
            new SynchronizeSharedAddons(request.FurniId), scope, operation.Token, validate);
        var current = CaptureCurrentState(scope, operation.Token);
        return ValueTask.FromResult(new WiredDispatchResult(time_provider.GetUtcNow(), current.Generation, current.Revision));
    }
}
