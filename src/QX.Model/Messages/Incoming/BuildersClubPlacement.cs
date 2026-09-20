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
        FlashWire.Parse(in p, ParseFlash);

    private static BuildersClubPlaceRoomItem ParseFlash(in PacketReader p) => ParseRequest(in p);

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
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BuildersClubPlaceRoomItem value, in PacketWriter p) =>
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
        FlashWire.Parse(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}
