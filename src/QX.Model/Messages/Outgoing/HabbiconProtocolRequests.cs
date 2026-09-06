using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record HabbiconShopRequest : IParserComposer<HabbiconShopRequest>
{
    public static HabbiconShopRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseMessage, ParseMessage);

    private static HabbiconShopRequest ParseMessage(in PacketReader p)
    {
        HabbiconWire.RequireEmpty(in p, nameof(HabbiconShopRequest));
        return new HabbiconShopRequest();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeMessage, ComposeMessage);

    private static void ComposeMessage(HabbiconShopRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);
}

public sealed record HabbiconInfoRequest(int HabbiconId) : IParserComposer<HabbiconInfoRequest>
{
    public static HabbiconInfoRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseMessage, ParseMessage);

    private static HabbiconInfoRequest ParseMessage(in PacketReader p) =>
        new(ReadInt(in p, nameof(HabbiconInfoRequest)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeMessage, ComposeMessage);

    private static void ComposeMessage(HabbiconInfoRequest value, in PacketWriter p) =>
        WriteInt(value, value.HabbiconId, in p);

    internal static int ReadInt(in PacketReader p, string name)
    {
        HabbiconWire.RequireRemaining(in p, sizeof(int), 0, name);
        int value = p.ReadInt();
        HabbiconWire.RequireEmpty(in p, name);
        return value;
    }

    internal static void WriteInt<T>(T value, int id, in PacketWriter p)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteInt(id);
    }
}

public sealed record HabbiconBuyRequest(int HabbiconId) : IParserComposer<HabbiconBuyRequest>
{
    public static HabbiconBuyRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static HabbiconBuyRequest ParseFlash(in PacketReader p) =>
        new(HabbiconInfoRequest.ReadInt(in p, nameof(HabbiconBuyRequest)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(HabbiconBuyRequest value, in PacketWriter p) =>
        HabbiconInfoRequest.WriteInt(value, value.HabbiconId, in p);
}

public sealed record HabbiconCollectionBuyRequest(int CollectionId)
    : IParserComposer<HabbiconCollectionBuyRequest>
{
    public static HabbiconCollectionBuyRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static HabbiconCollectionBuyRequest ParseFlash(in PacketReader p) =>
        new(HabbiconInfoRequest.ReadInt(in p, nameof(HabbiconCollectionBuyRequest)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(HabbiconCollectionBuyRequest value, in PacketWriter p) =>
        HabbiconInfoRequest.WriteInt(value, value.CollectionId, in p);
}

public sealed record HabbiconClaimRequest(int HabbiconId) : IParserComposer<HabbiconClaimRequest>
{
    public static HabbiconClaimRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static HabbiconClaimRequest ParseFlash(in PacketReader p) =>
        new(HabbiconInfoRequest.ReadInt(in p, nameof(HabbiconClaimRequest)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(HabbiconClaimRequest value, in PacketWriter p) =>
        HabbiconInfoRequest.WriteInt(value, value.HabbiconId, in p);
}

public sealed record HabbiconFavoriteRequest(int HabbiconId)
    : IParserComposer<HabbiconFavoriteRequest>
{
    public static HabbiconFavoriteRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static HabbiconFavoriteRequest ParseFlash(in PacketReader p) =>
        new(HabbiconInfoRequest.ReadInt(in p, nameof(HabbiconFavoriteRequest)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(HabbiconFavoriteRequest value, in PacketWriter p) =>
        HabbiconInfoRequest.WriteInt(value, value.HabbiconId, in p);
}

public sealed record HabbiconUnfavoriteRequest(int HabbiconId)
    : IParserComposer<HabbiconUnfavoriteRequest>
{
    public static HabbiconUnfavoriteRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static HabbiconUnfavoriteRequest ParseFlash(in PacketReader p) =>
        new(HabbiconInfoRequest.ReadInt(in p, nameof(HabbiconUnfavoriteRequest)));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(HabbiconUnfavoriteRequest value, in PacketWriter p) =>
        HabbiconInfoRequest.WriteInt(value, value.HabbiconId, in p);
}
