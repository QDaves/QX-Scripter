namespace Qx.Hosting;

using Qx;

public enum RuntimeServicePhase
{
    Disabled,
    Pending,
    Running,
    Completed,
    Canceled,
    Faulted
}

public sealed record RuntimeServiceStatus(
    bool Enabled,
    RuntimeServicePhase Phase,
    Exception? Error = null);

public sealed record RuntimeHostStatus(
    bool Started,
    bool Disposed,
    RuntimeServiceStatus Transport,
    RuntimeServiceStatus FallbackCatalogs,
    RuntimeServiceStatus HeaderPreparation,
    RuntimeServiceStatus Mcp);

public sealed record RuntimeHeaderCatalogFailure(
    ClientType Client,
    string Path,
    Exception Error);
