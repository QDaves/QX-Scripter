using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// The hotel republished its catalog, which invalidates any page already loaded.
/// </summary>
/// <remarks>
/// The Flash furni-data hash is optional. Unity always carries it.
/// </remarks>
/// <param name="InstantlyRefreshCatalogue">Whether the client should reload the catalog at once.</param>
/// <param name="NewFurniDataHash">
/// The new furni-data hash, or <see langword="null"/> when the republish did not change it.
/// </param>
public sealed record CatalogPublished(bool InstantlyRefreshCatalogue, string? NewFurniDataHash)
    : IParserComposer<CatalogPublished>
{
    public static CatalogPublished Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static CatalogPublished ParseUnity(in PacketReader p)
    {
        bool instantly_refresh_catalogue = p.ReadBool();
        var strings = new CatalogStringBudget(1, ushort.MaxValue);
        string new_furni_data_hash = strings.Read(in p, nameof(NewFurniDataHash));
        CatalogWire.RequireEmpty(in p, nameof(CatalogPublished));
        return new CatalogPublished(instantly_refresh_catalogue, new_furni_data_hash);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogPublished value, in PacketWriter p)
    {
        if (value.NewFurniDataHash is not null)
            CatalogWire.RequireString(value.NewFurniDataHash, nameof(NewFurniDataHash), in p);
        p.WriteBool(value.InstantlyRefreshCatalogue);
        if (value.NewFurniDataHash is not null)
            p.WriteString(value.NewFurniDataHash);
    }

    private static void ComposeUnity(CatalogPublished value, in PacketWriter p)
    {
        if (value.NewFurniDataHash is null)
            throw new InvalidDataException("Unity catalog expiry requires a furni-data hash.");
        CatalogWire.RequireString(value.NewFurniDataHash, nameof(NewFurniDataHash), in p);
        p.WriteBool(value.InstantlyRefreshCatalogue);
        p.WriteString(value.NewFurniDataHash);
    }
}
