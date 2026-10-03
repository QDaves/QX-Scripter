using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Requests the page tree of a catalog.</summary>
/// <remarks>Sent as the Flash <c>GetCatalogIndex</c> message.</remarks>
/// <param name="CatalogType">The catalog to load, <c>NORMAL</c> or <c>BUILDERS_CLUB</c>.</param>
public sealed record CatalogIndexRequest(string CatalogType)
    : IParserComposer<CatalogIndexRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CatalogIndexRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CatalogIndexRequest ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static CatalogIndexRequest ParseRequest(in PacketReader p)
    {
        var value = new CatalogIndexRequest(p.ReadString());
        CatalogRequestWire.RequireEmpty(in p, nameof(CatalogIndexRequest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CatalogIndexRequest value, in PacketWriter p) =>
        ComposeRequest(value, in p);

    private static void ComposeRequest(CatalogIndexRequest value, in PacketWriter p)
    {
        CatalogRequestWire.RequireString(value.CatalogType, nameof(CatalogType), in p);
        p.WriteString(value.CatalogType);
    }
}

/// <summary>Requests the contents of a catalog page.</summary>
/// <remarks>Sent as the Flash <c>GetCatalogPage</c> message.</remarks>
/// <param name="PageId">The id of the catalog page.</param>
/// <param name="OfferId">The offer to select on the page, or -1 for none.</param>
/// <param name="CatalogType">The catalog the page belongs to, <c>NORMAL</c> or <c>BUILDERS_CLUB</c>.</param>
public sealed record CatalogPageRequest(
    int PageId,
    int OfferId,
    string CatalogType) : IParserComposer<CatalogPageRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CatalogPageRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CatalogPageRequest ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static CatalogPageRequest ParseRequest(in PacketReader p)
    {
        var value = new CatalogPageRequest(p.ReadInt(), p.ReadInt(), p.ReadString());
        CatalogRequestWire.RequireEmpty(in p, nameof(CatalogPageRequest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CatalogPageRequest value, in PacketWriter p) =>
        ComposeRequest(value, in p);

    private static void ComposeRequest(CatalogPageRequest value, in PacketWriter p)
    {
        CatalogRequestWire.RequireString(value.CatalogType, nameof(CatalogType), in p);
        p.WriteInt(value.PageId);
        p.WriteInt(value.OfferId);
        p.WriteString(value.CatalogType);
    }
}

/// <summary>Represents the outgoing <c>GetRoomAdPurchaseInfo</c> message, sent to ask which rooms may be advertised.</summary>
/// <remarks>The message carries no data. The hotel answers with <see cref="RoomAdPurchaseInfo"/>.</remarks>
public sealed record GetRoomAdPurchaseInfo : IParserComposer<GetRoomAdPurchaseInfo>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetRoomAdPurchaseInfo Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetRoomAdPurchaseInfo ParseFlash(in PacketReader p)
    {
        RoomObjectReadWire.RequireEmpty(in p, nameof(GetRoomAdPurchaseInfo));
        return new GetRoomAdPurchaseInfo();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetRoomAdPurchaseInfo value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);
}

/// <summary>Represents the outgoing <c>PurchaseRoomAd</c> message, sent to buy a room event that advertises a room in the navigator for a while.</summary>
/// <remarks>
/// Argument order taken from the client's own call, which passes the page and offer it is
/// buying from followed by the event's own details. The hotel answers with the ordinary catalog
/// purchase messages, so the outcome arrives the same way any other purchase does.
/// </remarks>
/// <param name="PageId">The catalog page the offer sits on.</param>
/// <param name="OfferId">The offer to buy.</param>
/// <param name="RoomId">Which room to advertise; must be one the hotel listed as eligible.</param>
/// <param name="Name">The event's title as shown in the navigator.</param>
/// <param name="Extended">
/// Whether to run the longer form. The client clears this on its own when the account's membership
/// has already expired, so a script should read the purchase info first rather than assume it.
/// </param>
/// <param name="Description">The event's description.</param>
/// <param name="CategoryId">Which navigator category it is listed under.</param>
public sealed record PurchaseRoomAd(
    int PageId,
    int OfferId,
    Id RoomId,
    string Name,
    bool Extended,
    string Description,
    int CategoryId) : IParserComposer<PurchaseRoomAd>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PurchaseRoomAd Parse(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadId(), p.ReadString(), p.ReadBool(), p.ReadString(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(PageId);
        p.WriteInt(OfferId);
        p.WriteId(RoomId);
        p.WriteString(Name);
        p.WriteBool(Extended);
        p.WriteString(Description);
        p.WriteInt(CategoryId);
    }
}

internal static class CatalogRequestWire
{
    public static void RequireEmpty(in PacketReader p, string message_name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{message_name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }
}
