using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record PurchaseError(int ErrorCode) : IParserComposer<PurchaseError>
{
    public static PurchaseError Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PurchaseError ParseFlash(in PacketReader p) => ParseResult(in p);

    private static PurchaseError ParseUnity(in PacketReader p) => ParseResult(in p);

    private static PurchaseError ParseResult(in PacketReader p)
    {
        var value = new PurchaseError(p.ReadInt());
        CatalogWire.RequireEmpty(in p, nameof(PurchaseError));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PurchaseError value, in PacketWriter p) =>
        p.WriteInt(value.ErrorCode);

    private static void ComposeUnity(PurchaseError value, in PacketWriter p) =>
        p.WriteInt(value.ErrorCode);
}
