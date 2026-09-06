using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>
/// Buys an offer from a catalog page.
/// </summary>
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
    public static PurchaseFromCatalogRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PurchaseFromCatalogRequest ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static PurchaseFromCatalogRequest ParseUnity(in PacketReader p) => ParseRequest(in p);

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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PurchaseFromCatalogRequest value, in PacketWriter p) =>
        value.ComposeRequest(in p);

    private static void ComposeUnity(PurchaseFromCatalogRequest value, in PacketWriter p) =>
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
