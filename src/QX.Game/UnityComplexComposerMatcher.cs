using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game;

internal static class UnityComplexComposerMatcher
{
    public static bool RequiresExactMatch(
        string name,
        IComposer composer) =>
        IsWithdrawItems(name, composer) ||
        IsForumReadMarkers(name, composer) ||
        IsBuildersClubPlaceWallItem(name, composer) ||
        IsPlaceWallItem(name, composer) ||
        IsMoveWallItem(name, composer);

    public static bool TryMatch(
        string name,
        IComposer composer,
        IPacket packet,
        IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        if (IsWithdrawItems(name, composer))
            return TryMatchWithdrawItems(packet, schemas);
        if (IsForumReadMarkers(name, composer))
            return TryMatchForumReadMarkers(packet, schemas);
        if (IsBuildersClubPlaceWallItem(name, composer))
        {
            return composer is BuildersClubPlaceWallItem value &&
                TryMatchBuildersClubWallLocation(value, packet, schemas);
        }
        if (IsPlaceWallItem(name, composer))
        {
            return composer is PlaceRoomItemRequest
            {
                Kind: RoomItemPlacementKind.Wall
            } && TryMatchWallLocation(packet, schemas);
        }
        if (IsMoveWallItem(name, composer))
            return TryMatchWallLocation(packet, schemas);
        return false;
    }

    private static bool TryMatchWithdrawItems(
        IPacket packet,
        IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        if (!schemas.Any(IsWithdrawItemsSchema))
        {
            return false;
        }

        try
        {
            int position = 0;
            var reader = new PacketReader(packet, ref position);
            reader.ReadLong();
            if (reader.ReadByte() > 1)
                return false;
            reader.ReadInt();
            reader.ReadString();
            reader.ReadInt();
            return reader.Available == 0;
        }
        catch (Exception error) when (error is IndexOutOfRangeException or InvalidDataException)
        {
            return false;
        }
    }

    private static bool TryMatchForumReadMarkers(
        IPacket packet,
        IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        if (!schemas.Any(IsForumReadMarkersSchema))
            return false;

        try
        {
            int position = 0;
            var reader = new PacketReader(packet, ref position);
            int count = reader.ReadLength();
            for (int index = 0; index < count; index++)
            {
                reader.ReadLong();
                reader.ReadInt();
                if (reader.ReadByte() > 1)
                    return false;
            }
            return reader.Available == 0;
        }
        catch (Exception error) when (error is IndexOutOfRangeException or InvalidDataException)
        {
            return false;
        }
    }

    private static bool TryMatchWallLocation(
        IPacket packet,
        IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        if (schemas.Count == 0 || !schemas.All(IsWallLocationSchema))
            return false;

        try
        {
            int position = 0;
            var reader = new PacketReader(packet, ref position);
            reader.ReadLong();
            reader.ReadInt();
            reader.ReadInt();
            reader.ReadInt();
            reader.ReadInt();
            string orientation = reader.ReadString();
            return orientation.Length == 1 &&
                orientation[0] is 'l' or 'r' &&
                reader.Available == 0;
        }
        catch (Exception error) when (error is IndexOutOfRangeException or InvalidDataException)
        {
            return false;
        }
    }

    private static bool TryMatchBuildersClubWallLocation(
        BuildersClubPlaceWallItem value,
        IPacket packet,
        IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        string? source_type = null;
        foreach (OutgoingMessageSchema schema in schemas)
        {
            if (!IsBuildersClubWallLocationSchema(schema))
                return false;
            string current = schema.Parameters[3].SourceType;
            if (string.IsNullOrWhiteSpace(current))
                return false;
            source_type ??= current;
            if (!string.Equals(source_type, current, StringComparison.Ordinal))
                return false;
        }
        if (source_type is null)
            return false;

        try
        {
            WallLocation location = WallLocation.ParseString(value.WallLocation);
            int position = 0;
            var reader = new PacketReader(packet, ref position);
            return reader.ReadInt() == value.PageId &&
                reader.ReadInt() == value.OfferId &&
                reader.ReadString() == value.ExtraData &&
                reader.ReadInt() == location.Wall.X &&
                reader.ReadInt() == location.Wall.Y &&
                reader.ReadInt() == location.Offset.X &&
                reader.ReadInt() == location.Offset.Y &&
                reader.ReadString() == location.Orientation.ToString() &&
                reader.ReadByte() == (value.IsRetry ? 1 : 0) &&
                reader.Available == 0;
        }
        catch (Exception error) when (
            error is IndexOutOfRangeException or InvalidDataException or FormatException)
        {
            return false;
        }
    }

    private static bool IsWithdrawItems(
        string name,
        IComposer composer) =>
        name.Equals(
            nameof(WithdrawItemsFromChest),
            StringComparison.OrdinalIgnoreCase) &&
        composer is WithdrawItemsFromChest;

    private static bool IsForumReadMarkers(
        string name,
        IComposer composer) =>
        name.Equals(
            nameof(UpdateForumReadMarkers),
            StringComparison.OrdinalIgnoreCase) &&
        composer is UpdateForumReadMarkers ||
        name.Equals(
            nameof(UpdateForumReadMarker),
            StringComparison.OrdinalIgnoreCase) &&
        composer is UpdateForumReadMarker;

    private static bool IsPlaceWallItem(
        string name,
        IComposer composer) =>
        name.Equals(
            nameof(Msg.Out.PlaceWallItem),
            StringComparison.OrdinalIgnoreCase) &&
        composer is PlaceRoomItemRequest;

    private static bool IsBuildersClubPlaceWallItem(
        string name,
        IComposer composer) =>
        name.Equals(
            nameof(Msg.Out.BuildersClubPlaceWallItem),
            StringComparison.OrdinalIgnoreCase) &&
        composer is BuildersClubPlaceWallItem;

    private static bool IsMoveWallItem(
        string name,
        IComposer composer) =>
        name.Equals(
            nameof(Msg.Out.MoveWallItem),
            StringComparison.OrdinalIgnoreCase) &&
        composer is MoveWallItemRequest;

    private static bool IsWithdrawItemsSchema(OutgoingMessageSchema schema)
    {
        if (schema.Parameters.Count != 3)
            return false;

        OutgoingParameterSchema chest_id = schema.Parameters[0];
        OutgoingParameterSchema item_type = schema.Parameters[1];
        OutgoingParameterSchema count = schema.Parameters[2];
        return chest_id.Position == 0 &&
            chest_id.WireType is OutgoingWireType.Int64 &&
            chest_id.Collection is OutgoingCollectionKind.None &&
            item_type.Position == 1 &&
            item_type.WireType is OutgoingWireType.Unknown &&
            item_type.Collection is OutgoingCollectionKind.None &&
            count.Position == 2 &&
            count.WireType is OutgoingWireType.Int32 &&
            count.Collection is OutgoingCollectionKind.None;
    }

    private static bool IsForumReadMarkersSchema(
        OutgoingMessageSchema schema) =>
        schema.Parameters.Count == 1 &&
        schema.Parameters[0].Collection is not OutgoingCollectionKind.None &&
        schema.Parameters[0].WireType is OutgoingWireType.Unknown &&
        schema.Parameters[0].ElementWireTypes is { } element_types &&
        element_types.SequenceEqual(
            [
                OutgoingWireType.Int64,
                OutgoingWireType.Int32,
                OutgoingWireType.Boolean
            ]);

    private static bool IsWallLocationSchema(OutgoingMessageSchema schema)
    {
        if (schema.Parameters.Count != 2)
            return false;

        OutgoingParameterSchema item_id = schema.Parameters[0];
        OutgoingParameterSchema location = schema.Parameters[1];
        return item_id.Position == 0 &&
            item_id.WireType is OutgoingWireType.Int64 &&
            item_id.Collection is OutgoingCollectionKind.None &&
            location.Position == 1 &&
            location.WireType is OutgoingWireType.Unknown &&
            location.Collection is OutgoingCollectionKind.None;
    }

    private static bool IsBuildersClubWallLocationSchema(
        OutgoingMessageSchema schema) =>
        schema.Parameters.Count == 5 &&
        IsScalar(schema.Parameters[0], 0, OutgoingWireType.Int32) &&
        IsScalar(schema.Parameters[1], 1, OutgoingWireType.Int32) &&
        IsScalar(schema.Parameters[2], 2, OutgoingWireType.String) &&
        IsScalar(schema.Parameters[3], 3, OutgoingWireType.Unknown) &&
        IsScalar(schema.Parameters[4], 4, OutgoingWireType.Boolean);

    private static bool IsScalar(
        OutgoingParameterSchema parameter,
        int position,
        OutgoingWireType wire_type) =>
        parameter.Position == position &&
        parameter.WireType == wire_type &&
        parameter.Collection is OutgoingCollectionKind.None &&
        parameter.ElementWireTypes is null;
}
