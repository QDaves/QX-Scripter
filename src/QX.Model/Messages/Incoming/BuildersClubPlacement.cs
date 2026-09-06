using Qx.Messages;
using Qx.Model;

namespace Qx.Model.Messages.Incoming;

public sealed record BuildersClubPlaceRoomItem(
    int PageId,
    int OfferId,
    string ExtraData,
    int X,
    int Y,
    int Direction,
    bool IsRetry = false) : IParserComposer<BuildersClubPlaceRoomItem>
{
    public static BuildersClubPlaceRoomItem Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BuildersClubPlaceRoomItem ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static BuildersClubPlaceRoomItem ParseUnity(in PacketReader p) => ParseRequest(in p);

    private static BuildersClubPlaceRoomItem ParseRequest(in PacketReader p)
    {
        SubscriptionAdjunctWire.RequireMinimum(in p, 23, nameof(BuildersClubPlaceRoomItem));
        var value = new BuildersClubPlaceRoomItem(
            p.ReadInt(),
            p.ReadInt(),
            p.ReadString(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadBool());
        SubscriptionAdjunctWire.RequireEmpty(in p, nameof(BuildersClubPlaceRoomItem));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BuildersClubPlaceRoomItem value, in PacketWriter p) =>
        ComposeRequest(value, in p);

    private static void ComposeUnity(BuildersClubPlaceRoomItem value, in PacketWriter p) =>
        ComposeRequest(value, in p);

    private static void ComposeRequest(BuildersClubPlaceRoomItem value, in PacketWriter p)
    {
        SubscriptionAdjunctWire.RequireString(value.ExtraData, nameof(ExtraData), in p);
        p.WriteInt(value.PageId);
        p.WriteInt(value.OfferId);
        p.WriteString(value.ExtraData);
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
        p.WriteInt(value.Direction);
        p.WriteBool(value.IsRetry);
    }
}

public sealed record BuildersClubPlaceWallItem(
    int PageId,
    int OfferId,
    string ExtraData,
    string WallLocation,
    bool IsRetry = false) : IParserComposer<BuildersClubPlaceWallItem>
{
    public static BuildersClubPlaceWallItem Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BuildersClubPlaceWallItem ParseFlash(in PacketReader p)
    {
        SubscriptionAdjunctWire.RequireMinimum(in p, 13, nameof(BuildersClubPlaceWallItem));
        var value = new BuildersClubPlaceWallItem(
            p.ReadInt(),
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadBool());
        SubscriptionAdjunctWire.RequireEmpty(in p, nameof(BuildersClubPlaceWallItem));
        return value;
    }

    private static BuildersClubPlaceWallItem ParseUnity(in PacketReader p)
    {
        SubscriptionAdjunctWire.RequireMinimum(in p, 30, nameof(BuildersClubPlaceWallItem));
        int page_id = p.ReadInt();
        int offer_id = p.ReadInt();
        string extra_data = p.ReadString();
        int wall_x = p.ReadInt();
        int wall_y = p.ReadInt();
        int offset_x = p.ReadInt();
        int offset_y = p.ReadInt();
        string orientation = p.ReadString();
        if (orientation.Length != 1 || orientation[0] is not ('l' or 'r'))
            throw new InvalidDataException("Unity Builders Club wall placement contains an invalid orientation.");
        var value = new BuildersClubPlaceWallItem(
            page_id,
            offer_id,
            extra_data,
            new WallLocation(
                wall_x,
                wall_y,
                offset_x,
                offset_y,
                orientation[0]).ToString(),
            p.ReadBool());
        SubscriptionAdjunctWire.RequireEmpty(in p, nameof(BuildersClubPlaceWallItem));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BuildersClubPlaceWallItem value, in PacketWriter p)
    {
        SubscriptionAdjunctWire.RequireString(value.ExtraData, nameof(ExtraData), in p);
        SubscriptionAdjunctWire.RequireString(value.WallLocation, nameof(WallLocation), in p);
        p.WriteInt(value.PageId);
        p.WriteInt(value.OfferId);
        p.WriteString(value.ExtraData);
        p.WriteString(value.WallLocation);
        p.WriteBool(value.IsRetry);
    }

    private static void ComposeUnity(BuildersClubPlaceWallItem value, in PacketWriter p)
    {
        SubscriptionAdjunctWire.RequireString(value.ExtraData, nameof(ExtraData), in p);
        SubscriptionAdjunctWire.RequireString(value.WallLocation, nameof(WallLocation), in p);
        Qx.Model.WallLocation wall_location =
            Qx.Model.WallLocation.ParseString(value.WallLocation);
        SubscriptionAdjunctWire.RequireString(
            wall_location.Orientation.ToString(),
            nameof(WallLocation),
            in p);
        p.WriteInt(value.PageId);
        p.WriteInt(value.OfferId);
        p.WriteString(value.ExtraData);
        wall_location.Compose(in p);
        p.WriteBool(value.IsRetry);
    }
}
