using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class RoomReadsApplicationDescriptors
{
    public static ApplicationDescriptor DataGet { get; } = Read<RoomDataReadRequest, RoomDataReadResult>(
        ApplicationMemberIds.RoomDataGet,
        "Room data",
        "Loads an immutable navigator snapshot for one room in the active hotel session.",
        MessageKeys.Room.SnapshotRequest,
        MessageKeys.Room.Snapshot);

    public static ApplicationDescriptor RightsList { get; } = Read<RoomRightsReadRequest, RoomRightsReadResult>(
        ApplicationMemberIds.RoomRightsList,
        "Room rights",
        "Loads the immutable rights-holder list for one owned room in the active hotel session.",
        MessageKeys.Room.Authority.ControllersRequest,
        MessageKeys.Room.Authority.ControllersSnapshot);

    public static ApplicationDescriptor PetInfoGet { get; } = Read<PetInfoReadRequest, PetInfoReadResult>(
        ApplicationMemberIds.PetsInfoGet,
        "Pet info",
        "Loads an immutable statistics snapshot for one pet in the active hotel session.",
        MessageKeys.Room.Occupants.Pet.InfoRequest,
        MessageKeys.Room.Occupants.Pet.Info,
        "pet_id");

    public static ApplicationDescriptor StickyGet { get; } = Read<StickyReadRequest, StickyReadResult>(
        ApplicationMemberIds.RoomStickyGet,
        "Sticky data",
        "Loads the immutable color and text of one sticky note in the active hotel session.",
        MessageKeys.Room.WallItem.StickyDataRequest,
        MessageKeys.Room.WallItem.StickyData,
        "item_id");

    public static ApplicationDescriptor RoomAdInfoGet { get; } = new(
        ApplicationMemberIds.CatalogRoomAdInfoGet,
        "Room advertisement purchase info",
        "Loads the latest observed eligible-room list and membership flag from the active hotel session.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomAdInfoReadRequest),
        typeof(RoomAdInfoReadResult),
        [
            new(
                "timeout_milliseconds",
                typeof(int),
                false,
                10000,
                "Maximum time for dispatch and the first fresh route response.",
                new(Minimum: 1, Maximum: 120000)),
            new(
                "expected_session_generation",
                typeof(long?),
                false,
                null,
                "Optional active hotel-session generation guard.",
                new(Minimum: 0))
        ],
        [ApplicationStateKey.HotelConnected],
        messages:
        [
            new(MessageKeys.Catalog.RoomAdInfoRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Catalog.RoomAdInfo, Direction.In, ApplicationMessageRole.Observe)
        ],
        tool_hints: new(true, false, true, true));

    private static ApplicationDescriptor Read<TRequest, TResult>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey snapshot_key,
        string id_name = "room_id") => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(TResult),
            [
                new(
                    id_name,
                    typeof(Id),
                    true,
                    null,
                    "Positive room identifier.",
                    new(Pattern: "^[1-9][0-9]*$")),
                new(
                    "timeout_milliseconds",
                    typeof(int),
                    false,
                    10000,
                    "Maximum time for dispatch and the matching hotel response.",
                    new(Minimum: 1, Maximum: 120000)),
                new(
                    "expected_session_generation",
                    typeof(long?),
                    false,
                    null,
                    "Optional active hotel-session generation guard.",
                    new(Minimum: 0))
            ],
            [ApplicationStateKey.HotelConnected],
            messages:
            [
                new(request_key, Direction.Out, ApplicationMessageRole.Send),
                new(snapshot_key, Direction.In, ApplicationMessageRole.Observe)
            ],
            tool_hints: new(true, false, true, true));
}
