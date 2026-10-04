using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Represents the request that buys an offer from a catalog page.</summary>
/// <remarks>Sent as the Flash <c>PurchaseFromCatalog</c> message.</remarks>
/// <param name="PageId">The catalog page the offer sits on.</param>
/// <param name="OfferId">The offer to buy.</param>
/// <param name="ExtraData">The offer's selection data, empty when it takes none.</param>
/// <param name="Quantity">How many to buy.</param>
public sealed record PurchaseFromCatalogRequest(
    int PageId,
    int OfferId,
    string ExtraData,
    int Quantity) : IParserComposer<PurchaseFromCatalogRequest>
{
    /// <summary>Reads the request from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static PurchaseFromCatalogRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PurchaseFromCatalogRequest ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static PurchaseFromCatalogRequest ParseRequest(in PacketReader p)
    {
        int page_id = p.ReadInt();
        int offer_id = p.ReadInt();
        var strings = new CatalogStringBudget(1, ushort.MaxValue);
        var value = new PurchaseFromCatalogRequest(
            page_id,
            offer_id,
            strings.Read(in p, nameof(ExtraData), sizeof(int)),
            p.ReadInt());
        CatalogRequestWire.RequireEmpty(in p, nameof(PurchaseFromCatalogRequest));
        return value;
    }

    /// <summary>Writes the request to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PurchaseFromCatalogRequest value, in PacketWriter p) =>
        value.ComposeRequest(in p);

    private void ComposeRequest(in PacketWriter p)
    {
        CatalogRequestWire.RequireString(ExtraData, nameof(ExtraData), in p);
        p.WriteInt(PageId);
        p.WriteInt(OfferId);
        p.WriteString(ExtraData);
        p.WriteInt(Quantity);
    }
}
