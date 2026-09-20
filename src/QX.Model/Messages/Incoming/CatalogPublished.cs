using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record CatalogPublished(bool InstantlyRefreshCatalogue, string? NewFurniDataHash)
    : IParserComposer<CatalogPublished>
{
    public static CatalogPublished Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CatalogPublished ParseFlash(in PacketReader p)
    {
        bool instantly_refresh_catalogue = p.ReadBool();
        string? new_furni_data_hash = null;
        if (p.Available > 0)
        {
            var strings = new CatalogStringBudget(1, ushort.MaxValue);
            new_furni_data_hash = strings.Read(in p, nameof(NewFurniDataHash));
        }
        CatalogWire.RequireEmpty(in p, nameof(CatalogPublished));
        return new CatalogPublished(instantly_refresh_catalogue, new_furni_data_hash);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CatalogPublished value, in PacketWriter p)
    {
        if (value.NewFurniDataHash is not null)
            CatalogWire.RequireString(value.NewFurniDataHash, nameof(NewFurniDataHash), in p);
        p.WriteBool(value.InstantlyRefreshCatalogue);
        if (value.NewFurniDataHash is not null)
            p.WriteString(value.NewFurniDataHash);
    }
}
