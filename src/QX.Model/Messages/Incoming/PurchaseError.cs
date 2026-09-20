using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record PurchaseError(int ErrorCode) : IParserComposer<PurchaseError>
{
    public static PurchaseError Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PurchaseError ParseFlash(in PacketReader p) => ParseResult(in p);

    private static PurchaseError ParseResult(in PacketReader p)
    {
        var value = new PurchaseError(p.ReadInt());
        CatalogWire.RequireEmpty(in p, nameof(PurchaseError));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PurchaseError value, in PacketWriter p) =>
        p.WriteInt(value.ErrorCode);
}
