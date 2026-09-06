using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record PurchaseNotAllowed(int ErrorCode) : IParserComposer<PurchaseNotAllowed>
{
    public static PurchaseNotAllowed Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PurchaseNotAllowed ParseFlash(in PacketReader p) => ParseResult(in p);

    private static PurchaseNotAllowed ParseUnity(in PacketReader p) => ParseResult(in p);

    private static PurchaseNotAllowed ParseResult(in PacketReader p)
    {
        var value = new PurchaseNotAllowed(p.ReadInt());
        CatalogWire.RequireEmpty(in p, nameof(PurchaseNotAllowed));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PurchaseNotAllowed value, in PacketWriter p) =>
        p.WriteInt(value.ErrorCode);

    private static void ComposeUnity(PurchaseNotAllowed value, in PacketWriter p) =>
        p.WriteInt(value.ErrorCode);
}
