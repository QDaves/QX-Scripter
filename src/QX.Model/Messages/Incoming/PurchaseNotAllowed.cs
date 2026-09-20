using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record PurchaseNotAllowed(int ErrorCode) : IParserComposer<PurchaseNotAllowed>
{
    public static PurchaseNotAllowed Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PurchaseNotAllowed ParseFlash(in PacketReader p) => ParseResult(in p);

    private static PurchaseNotAllowed ParseResult(in PacketReader p)
    {
        var value = new PurchaseNotAllowed(p.ReadInt());
        CatalogWire.RequireEmpty(in p, nameof(PurchaseNotAllowed));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PurchaseNotAllowed value, in PacketWriter p) =>
        p.WriteInt(value.ErrorCode);
}
