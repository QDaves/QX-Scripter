namespace Qx.Protocol;

public sealed record ClientBuildIdentity(
    string CatalogFingerprint,
    string? SchemaFingerprint = null);

public sealed record ClientBuildBinding(
    ClientBuildIdentity Identity,
    MessageCatalog? Catalog = null,
    Task<ClientBuildBinding>? SchemaUpgrade = null,
    Exception? SchemaError = null);
