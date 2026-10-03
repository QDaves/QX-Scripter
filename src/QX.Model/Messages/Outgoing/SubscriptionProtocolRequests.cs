using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Represents the outgoing <c>ScrGetUserInfo</c> message, sent to request the user's subscription info for one product.</summary>
/// <remarks>The hotel answers with <see cref="ScrSendUserInfo"/>.</remarks>
/// <param name="ProductName">The subscription product name, such as <c>habbo_club</c> or <c>builders_club</c>.</param>
public sealed record SubscriptionGetUserInfo(string ProductName)
    : IParserComposer<SubscriptionGetUserInfo>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SubscriptionGetUserInfo Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SubscriptionGetUserInfo ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static SubscriptionGetUserInfo ParseRequest(in PacketReader p)
    {
        var value = new SubscriptionGetUserInfo(p.ReadString());
        SubscriptionWire.RequireEmpty(in p, nameof(SubscriptionGetUserInfo));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SubscriptionGetUserInfo value, in PacketWriter p)
    {
        SubscriptionWire.RequireString(value.ProductName, nameof(ProductName), in p);
        p.WriteString(value.ProductName);
    }
}

/// <summary>Represents the outgoing <c>ScrGetKickbackInfo</c> message, sent to request the user's Habbo Club kickback summary.</summary>
/// <remarks>The message carries no data. The hotel answers with <see cref="ScrSendKickbackInfo"/>.</remarks>
public sealed record SubscriptionGetKickbackInfo
    : IParserComposer<SubscriptionGetKickbackInfo>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SubscriptionGetKickbackInfo Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SubscriptionGetKickbackInfo ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static SubscriptionGetKickbackInfo ParseRequest(in PacketReader p)
    {
        SubscriptionWire.RequireEmpty(in p, nameof(SubscriptionGetKickbackInfo));
        return new SubscriptionGetKickbackInfo();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SubscriptionGetKickbackInfo value, in PacketWriter p) { }
}

/// <summary>Represents the <c>GetClubOffers</c> message, sent to request the club membership offers.</summary>
/// <param name="OfferType">The offer set selector sent to the hotel.</param>
public sealed record GetClubOffers(int OfferType) : IParserComposer<GetClubOffers>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetClubOffers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetClubOffers ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static GetClubOffers ParseRequest(in PacketReader p)
    {
        SubscriptionAdjunctWire.RequireSize(in p, sizeof(int), nameof(GetClubOffers));
        var value = new GetClubOffers(p.ReadInt());
        SubscriptionAdjunctWire.RequireEmpty(in p, nameof(GetClubOffers));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetClubOffers value, in PacketWriter p) =>
        p.WriteInt(value.OfferType);
}

/// <summary>Represents the outgoing <c>BuildersClubQueryFurniCount</c> message, sent to request the number of Builders Club furni the user has placed.</summary>
/// <remarks>The message carries no data. The hotel answers with <see cref="BuildersClubFurniCount"/>.</remarks>
public sealed record BuildersClubQueryFurniCount
    : IParserComposer<BuildersClubQueryFurniCount>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BuildersClubQueryFurniCount Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BuildersClubQueryFurniCount ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static BuildersClubQueryFurniCount ParseRequest(in PacketReader p)
    {
        SubscriptionWire.RequireEmpty(in p, nameof(BuildersClubQueryFurniCount));
        return new BuildersClubQueryFurniCount();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BuildersClubQueryFurniCount value, in PacketWriter p) { }
}

/// <summary>
/// Represents the <c>BuildersClubPlaceRoomItem</c> message, sent to place a Builders Club floor item in the room.
/// </summary>
/// <param name="PageId">The identifier of the catalog page with the offer.</param>
/// <param name="OfferId">The identifier of the Builders Club offer.</param>
/// <param name="ExtraData">The offer selection data.</param>
/// <param name="X">The tile x coordinate.</param>
/// <param name="Y">The tile y coordinate.</param>
/// <param name="Direction">The item direction.</param>
/// <param name="IsRetry">Whether the placement is flagged to the hotel as a retry.</param>
public sealed record BuildersClubPlaceRoomItem(
    int PageId,
    int OfferId,
    string ExtraData,
    int X,
    int Y,
    int Direction,
    bool IsRetry = false) : IParserComposer<BuildersClubPlaceRoomItem>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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

/// <summary>
/// Represents the <c>BuildersClubPlaceWallItem</c> message, sent to place a Builders Club wall item in the room.
/// </summary>
/// <param name="PageId">The identifier of the catalog page with the offer.</param>
/// <param name="OfferId">The identifier of the Builders Club offer.</param>
/// <param name="ExtraData">The offer selection data.</param>
/// <param name="WallLocation">The wall location as a string in the Flash client's format.</param>
/// <param name="IsRetry">Whether the placement is flagged to the hotel as a retry.</param>
public sealed record BuildersClubPlaceWallItem(
    int PageId,
    int OfferId,
    string ExtraData,
    string WallLocation,
    bool IsRetry = false) : IParserComposer<BuildersClubPlaceWallItem>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
