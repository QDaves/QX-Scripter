using Qx.Model;

namespace Qx.Game.Application;

public sealed record NavigatorStateRequest;

public sealed record NavigatorRefreshRequest(int TimeoutMilliseconds = 10000);

public sealed record NavigatorSearchRequest(int TimeoutMilliseconds = 10000);

public sealed record NavigatorViewSearchInput(
    string SearchCode,
    string Filter = "",
    int TimeoutMilliseconds = 10000);

public sealed record NavigatorTextSearchInput(
    RoomSearchField Field,
    string Text = "",
    int TimeoutMilliseconds = 10000);

public sealed record NavigatorPopularSearchInput(
    string Tag = "",
    int AdIndex = -1,
    int TimeoutMilliseconds = 10000);

public sealed record NavigatorAdSearchInput(
    int AdIndex = -1,
    int TimeoutMilliseconds = 10000);

public sealed record NavigatorSavedSearchAddInput(string SearchCode, string Filter = "");

public sealed record NavigatorSavedSearchDeleteInput(int SavedSearchId);

public sealed record NavigatorCategoryInput(string SearchCode);

public sealed record NavigatorRoomCreateInput(
    string Name,
    string Description,
    string Model,
    int Category,
    int MaxVisitors,
    int TradeMode = 0);

public sealed record NavigatorRoomDeleteInput(Id RoomId);

public sealed record NavigatorHomeRoomSetInput(Id RoomId);

public sealed record NavigatorRoomOperationResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    Id? RoomId = null);

public sealed record NavigatorOperationResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    string? SearchCode = null,
    string? Filter = null,
    int? SavedSearchId = null);

public enum NavigatorChangeKind
{
    Metadata,
    FlatCategories,
    SavedSearches,
    LiftedRooms,
    CollapsedCategories,
    Settings,
    Preferences,
    Reset
}

public sealed record NavigatorChanged(
    NavigatorChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    NavigatorState State);

public sealed record NavigatorSearchReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    NavigatorSearchSnapshot Result);
