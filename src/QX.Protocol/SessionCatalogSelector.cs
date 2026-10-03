namespace Qx.Protocol;

/// <summary>Specifies why a session catalog is being selected.</summary>
public enum SessionCatalogSelectionIntent
{
    /// <summary>A hotel session is starting.</summary>
    SessionStart,
    /// <summary>A prepared catalog became ready during a session.</summary>
    CatalogReady
}

/// <summary>Represents a request to select the message catalog for a hotel session.</summary>
/// <param name="HotelVersion">The client build version reported for the session.</param>
/// <param name="ClientIdentifier">The client identifier reported for the session.</param>
/// <param name="Fallback">The binding to use when no better catalog is selected, such as the G-Earth catalog.</param>
/// <param name="Intent">Why the catalog is being selected.</param>
public sealed record SessionCatalogRequest(
    string HotelVersion,
    string ClientIdentifier,
    SessionCatalogBinding Fallback,
    SessionCatalogSelectionIntent Intent = SessionCatalogSelectionIntent.SessionStart);

/// <summary>Defines a selector that chooses the message catalog for a hotel session.</summary>
public interface ISessionCatalogSelector
{
    /// <summary>Selects the message catalog for a hotel session.</summary>
    /// <param name="request">The session details and the fallback binding.</param>
    /// <returns>The selected binding, or <see langword="null"/> to use <see cref="SessionCatalogRequest.Fallback"/>.</returns>
    SessionCatalogBinding? Select(SessionCatalogRequest request);
}
