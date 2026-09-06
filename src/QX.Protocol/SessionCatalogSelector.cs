using Qx;

namespace Qx.Protocol;

public enum SessionCatalogSelectionIntent
{
    SessionStart,
    CatalogReady
}

public sealed record SessionCatalogRequest(
    ClientType Client,
    string HotelVersion,
    string ClientIdentifier,
    SessionCatalogBinding Fallback,
    SessionCatalogSelectionIntent Intent = SessionCatalogSelectionIntent.SessionStart);

public interface ISessionCatalogSelector
{
    SessionCatalogBinding? Select(SessionCatalogRequest request);
}
