using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record CatalogIndexRequest(string CatalogType)
    : IParserComposer<CatalogIndexRequest>
{
    public static CatalogIndexRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogIndexRequest ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static CatalogIndexRequest ParseUnity(in PacketReader p) => ParseRequest(in p);

    private static CatalogIndexRequest ParseRequest(in PacketReader p)
    {
        var value = new CatalogIndexRequest(p.ReadString());
        CatalogRequestWire.RequireEmpty(in p, nameof(CatalogIndexRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogIndexRequest value, in PacketWriter p) =>
        ComposeRequest(value, in p);

    private static void ComposeUnity(CatalogIndexRequest value, in PacketWriter p) =>
        ComposeRequest(value, in p);

    private static void ComposeRequest(CatalogIndexRequest value, in PacketWriter p)
    {
        CatalogRequestWire.RequireString(value.CatalogType, nameof(CatalogType), in p);
        p.WriteString(value.CatalogType);
    }
}

public sealed record CatalogPageRequest(
    int PageId,
    int OfferId,
    string CatalogType) : IParserComposer<CatalogPageRequest>
{
    public static CatalogPageRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogPageRequest ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static CatalogPageRequest ParseUnity(in PacketReader p) => ParseRequest(in p);

    private static CatalogPageRequest ParseRequest(in PacketReader p)
    {
        var value = new CatalogPageRequest(p.ReadInt(), p.ReadInt(), p.ReadString());
        CatalogRequestWire.RequireEmpty(in p, nameof(CatalogPageRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogPageRequest value, in PacketWriter p) =>
        ComposeRequest(value, in p);

    private static void ComposeUnity(CatalogPageRequest value, in PacketWriter p) =>
        ComposeRequest(value, in p);

    private static void ComposeRequest(CatalogPageRequest value, in PacketWriter p)
    {
        CatalogRequestWire.RequireString(value.CatalogType, nameof(CatalogType), in p);
        p.WriteInt(value.PageId);
        p.WriteInt(value.OfferId);
        p.WriteString(value.CatalogType);
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
