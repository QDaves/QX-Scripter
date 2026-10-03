using Qx.Game.Protocol;
using Qx.Model;
using Qx.Model.Messages.Outgoing;
using Qx.Model.Wired;

namespace Qx.Game.Application;

internal sealed partial class WiredApplication
{
    private ValueTask<WiredAreaHideView> get_area_hide(WiredAreaHideGetRequest request, CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.FurniId, nameof(request.FurniId));
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        WiredAreaHideView view = read_area_hide(request.FurniId, scope.RoomGeneration);
        CaptureCurrentState(scope, cancellation_token);
        return ValueTask.FromResult(view);
    }

    private WiredAreaHideView read_area_hide(Id furni_id, long room_generation) => game.Room.Capture(room =>
    {
        if (!room.IsInRoom || room.Generation != room_generation)
            throw new InvalidOperationException("The reviewed area-hide room is no longer current.");
        FloorItem item = room.FloorItems.FirstOrDefault(item => item.Id == furni_id)
            ?? throw new InvalidOperationException("The area-hide furniture is not in the current room.");
        if (item.Identifier is { Length: > 0 } identifier && identifier != "conf_area_hide")
            throw new ArgumentException("The furniture is not an area-hide controller.", nameof(furni_id));
        if (item.Data is not IntArrayData data)
            throw new InvalidOperationException("The area-hide furniture has no integer settings data.");
        WiredAreaHideSettings settings = WiredAreaHideSettings.Read(furni_id, data.Values);
        AreaHideData? region = room.FloorPlan?.HiddenAreas.Where(area => area.FurniId == furni_id)
            .Select(area => (AreaHideData?)area).FirstOrDefault();
        return new WiredAreaHideView(room.RoomId, room.Generation, settings, region);
    });

    private ValueTask<WiredDispatchResult> set_area_hide(WiredAreaHideSetRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Update);
        ValidateId(request.Update.FurniId, nameof(request.Update.FurniId));
        return dispatch_area_hide(MessageContracts.Room.Environment.AreaHideSet, request.Update,
            request.Update.FurniId, request.ExpectedRoomGeneration, true, cancellation_token);
    }

    private ValueTask<WiredDispatchResult> toggle_area_hide(WiredAreaHideToggleRequest request, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.FurniId, nameof(request.FurniId));
        return dispatch_area_hide(MessageContracts.Room.FloorItem.Use, new UseFloorItemRequest(request.FurniId, 0),
            request.FurniId, request.ExpectedRoomGeneration, false, cancellation_token);
    }

    private ValueTask<WiredDispatchResult> dispatch_area_hide<T>(MessageContract<T> contract, T message,
        Id furni_id, long room_generation, bool require_off, CancellationToken cancellation_token) where T : Qx.Messages.IParserComposer<T>
    {
        ThrowIfDisposed();
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        DispatchInRoom(contract, message, scope, cancellation_token, () =>
        {
            WiredAreaHideView current = read_area_hide(furni_id, room_generation);
            if (require_off && (current.Region?.On ?? current.Settings.On))
                throw new InvalidOperationException("Switch area hiding off before editing its settings.");
        });
        WiredSnapshot state = CaptureCurrentState(scope, cancellation_token);
        return ValueTask.FromResult(new WiredDispatchResult(time_provider.GetUtcNow(), state.Generation, state.Revision));
    }
}
