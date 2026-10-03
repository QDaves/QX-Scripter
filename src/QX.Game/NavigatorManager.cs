using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

/// <summary>
/// Specifies which room field a navigator text search matches.
/// </summary>
public enum RoomSearchField
{
    /// <summary>Any field, sent without a prefix.</summary>
    Anything,
    /// <summary>The room owner's name, sent with the <c>owner:</c> prefix.</summary>
    Owner,
    /// <summary>The room name, sent with the <c>roomname:</c> prefix.</summary>
    RoomName,
    /// <summary>A room tag, sent with the <c>tag:</c> prefix.</summary>
    Tag,
    /// <summary>The room's group, sent with the <c>group:</c> prefix.</summary>
    Group
}

/// <summary>
/// Represents an immutable copy of a navigator search entry, either a quick link or a saved search.
/// </summary>
/// <param name="Id">The id of a saved search, or 0 for a quick link.</param>
/// <param name="SearchCode">The code of the view the search belongs to, such as <c>hotel_view</c>.</param>
/// <param name="Filter">The filter text in the navigator's prefix syntax.</param>
/// <param name="Localization">The text key of the label the client shows.</param>
public sealed record NavigatorSearchEntrySnapshot(
    int Id,
    string SearchCode,
    string Filter,
    string Localization);

/// <summary>
/// Represents an immutable copy of a navigator category and the quick links offered under it.
/// </summary>
/// <param name="SearchCode">The code of the category.</param>
/// <param name="QuickLinks">The searches the hotel offers under the category.</param>
public sealed record NavigatorCategorySnapshot(
    string SearchCode,
    IReadOnlyList<NavigatorSearchEntrySnapshot> QuickLinks);

/// <summary>
/// Represents an immutable copy of a room the hotel promotes in the navigator.
/// </summary>
/// <param name="RoomId">The id of the room.</param>
/// <param name="AreaId">The id of the promoted area the room belongs to.</param>
/// <param name="Image">The image reference of the room's tile.</param>
/// <param name="Caption">The caption of the room's tile.</param>
public sealed record NavigatorLiftedRoomSnapshot(
    int RoomId,
    int AreaId,
    string Image,
    string Caption);

/// <summary>
/// Represents an immutable copy of a room category that rooms can be filed under.
/// </summary>
/// <param name="NodeId">The id of the category, which is the value of a room's category field.</param>
/// <param name="Name">The name of the category.</param>
/// <param name="Visible">Whether the category is shown.</param>
/// <param name="Automatic">Whether the hotel assigns the category itself.</param>
/// <param name="AutomaticCategoryKey">The key of an automatic category.</param>
/// <param name="GlobalCategoryKey">The hotel-wide key the category maps to.</param>
/// <param name="StaffOnly">Whether only staff may file a room under the category.</param>
/// <param name="Selectable">Whether a room owner can file a room under the category, which requires it to be visible, not automatic and not staff only.</param>
public sealed record NavigatorFlatCategorySnapshot(
    int NodeId,
    string Name,
    bool Visible,
    bool Automatic,
    string AutomaticCategoryKey,
    string GlobalCategoryKey,
    bool StaffOnly,
    bool Selectable);

/// <summary>
/// Represents an immutable copy of the local user's navigator settings.
/// </summary>
/// <param name="HomeRoomId">The id of the local user's home room.</param>
/// <param name="RoomIdToEnter">The id of the room the hotel tells the client to enter.</param>
public sealed record NavigatorSettingsSnapshot(Id HomeRoomId, Id RoomIdToEnter);

/// <summary>
/// Represents an immutable copy of how the local user has arranged the navigator window.
/// </summary>
/// <param name="WindowX">The x position of the window.</param>
/// <param name="WindowY">The y position of the window.</param>
/// <param name="WindowWidth">The width of the window.</param>
/// <param name="WindowHeight">The height of the window.</param>
/// <param name="LeftPaneHidden">Whether the category pane is collapsed.</param>
/// <param name="ResultsMode">The mode the results are drawn in, such as a list or thumbnails.</param>
public sealed record NavigatorPreferencesSnapshot(
    int WindowX,
    int WindowY,
    int WindowWidth,
    int WindowHeight,
    bool LeftPaneHidden,
    int ResultsMode);

/// <summary>
/// Represents an immutable copy of the navigator metadata sent for a room.
/// </summary>
/// <param name="RoomId">The id of the room.</param>
/// <param name="FirstValue">The first metadata value.</param>
/// <param name="SecondValue">The second metadata value.</param>
public sealed record NavigatorRoomMetadataSnapshot(
    Id RoomId,
    string FirstValue,
    string SecondValue);

/// <summary>
/// Represents an immutable copy of one block of rooms in a navigator search result.
/// </summary>
/// <param name="SearchCode">The search code of the block.</param>
/// <param name="Text">The title text of the block.</param>
/// <param name="ActionAllowed">The action code the hotel allows for the block.</param>
/// <param name="ForceClosed">Whether the block is shown collapsed.</param>
/// <param name="ViewMode">The view mode the block is shown in.</param>
/// <param name="Rooms">The rooms in the block.</param>
public sealed record NavigatorSearchBlockSnapshot(
    string SearchCode,
    string Text,
    int ActionAllowed,
    bool ForceClosed,
    int ViewMode,
    IReadOnlyList<RoomDataSnapshot> Rooms);

/// <summary>
/// Represents an immutable copy of a navigator search result.
/// </summary>
/// <param name="SearchCode">The search code the result answers.</param>
/// <param name="Filter">The filter text the result answers.</param>
/// <param name="Blocks">The blocks of rooms in the result.</param>
public sealed record NavigatorSearchSnapshot(
    string SearchCode,
    string Filter,
    IReadOnlyList<NavigatorSearchBlockSnapshot> Blocks)
{
    /// <summary>
    /// Gets the rooms of every block, in block order.
    /// </summary>
    public IEnumerable<RoomDataSnapshot> Rooms => Blocks.SelectMany(block => block.Rooms);
}

/// <summary>
/// Represents an immutable view of the navigator state at one point in time.
/// </summary>
/// <param name="MetadataLoaded">Whether the navigator categories have been received.</param>
/// <param name="FlatCategoriesLoaded">Whether the room categories have been received.</param>
/// <param name="Generation">The session generation the state belongs to.</param>
/// <param name="Revision">The revision of the state, which increases with every change.</param>
/// <param name="Categories">The navigator categories and their quick links.</param>
/// <param name="SavedSearches">The local user's saved searches.</param>
/// <param name="LiftedRooms">The rooms the hotel promotes.</param>
/// <param name="FlatCategories">The room categories that rooms can be filed under.</param>
/// <param name="CollapsedCategories">The codes of the categories the local user has collapsed.</param>
/// <param name="Settings">The local user's navigator settings, or <see langword="null"/> when not received.</param>
/// <param name="Preferences">The local user's navigator window preferences, or <see langword="null"/> when not received.</param>
public sealed record NavigatorState(
    bool MetadataLoaded,
    bool FlatCategoriesLoaded,
    long Generation,
    long Revision,
    IReadOnlyList<NavigatorCategorySnapshot> Categories,
    IReadOnlyList<NavigatorSearchEntrySnapshot> SavedSearches,
    IReadOnlyList<NavigatorLiftedRoomSnapshot> LiftedRooms,
    IReadOnlyList<NavigatorFlatCategorySnapshot> FlatCategories,
    IReadOnlyList<string> CollapsedCategories,
    NavigatorSettingsSnapshot? Settings,
    NavigatorPreferencesSnapshot? Preferences)
{
    /// <summary>
    /// Gets an empty navigator state.
    /// </summary>
    public static NavigatorState Empty { get; } = new(
        false,
        false,
        0,
        0,
        Array.AsReadOnly(Array.Empty<NavigatorCategorySnapshot>()),
        Array.AsReadOnly(Array.Empty<NavigatorSearchEntrySnapshot>()),
        Array.AsReadOnly(Array.Empty<NavigatorLiftedRoomSnapshot>()),
        Array.AsReadOnly(Array.Empty<NavigatorFlatCategorySnapshot>()),
        Array.AsReadOnly(Array.Empty<string>()),
        null,
        null);
}

internal enum NavigatorStateChangeKind
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

/// <summary>
/// Manages the navigator state received from the hotel and sends navigator room requests.
/// </summary>
/// <remarks>
/// <para>
/// All members are safe to call from any thread. Each change publishes a new immutable
/// <see cref="NavigatorState"/>. Search results are not stored in the state.
/// </para>
/// <para>
/// The state is cleared when the hotel connection closes.
/// </para>
/// </remarks>
public sealed class NavigatorManager : GameStateManager
{
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private IReadOnlyList<NavigatorCategorySnapshot> categories =
        Array.AsReadOnly(Array.Empty<NavigatorCategorySnapshot>());
    private IReadOnlyList<NavigatorSearchEntrySnapshot> saved_searches =
        Array.AsReadOnly(Array.Empty<NavigatorSearchEntrySnapshot>());
    private IReadOnlyList<NavigatorLiftedRoomSnapshot> lifted_rooms =
        Array.AsReadOnly(Array.Empty<NavigatorLiftedRoomSnapshot>());
    private IReadOnlyList<NavigatorFlatCategorySnapshot> flat_categories =
        Array.AsReadOnly(Array.Empty<NavigatorFlatCategorySnapshot>());
    private IReadOnlyList<string> collapsed_categories =
        Array.AsReadOnly(Array.Empty<string>());
    private NavigatorSettingsSnapshot? settings;
    private NavigatorPreferencesSnapshot? preferences;
    private NavigatorState state = NavigatorState.Empty;
    private bool metadata_loaded;
    private bool flat_categories_loaded;
    private long generation;
    private long revision;
    private long committed_generation;
    private long reset_generation = -1;

    /// <summary>
    /// Gets the current navigator state.
    /// </summary>
    public NavigatorState State => Volatile.Read(ref state);

    internal event Action<NavigatorStateChangeKind, NavigatorState>? StateChanged;
    internal event Action<NavigatorSearchSnapshot, long, long>? SearchReceived;

    /// <inheritdoc/>
    protected override void OnAttach()
    {
        OnIncoming(
            MessageContracts.Navigator.State.Metadata,
            (message, state_generation) => Store(
                state_generation,
                NavigatorStateChangeKind.Metadata,
                () =>
                {
                    categories = ReadOnly(message.Categories.Select(Snapshot));
                    metadata_loaded = true;
                }));
        OnIncoming(
            MessageContracts.Navigator.State.FlatCategories,
            (message, state_generation) => Store(
                state_generation,
                NavigatorStateChangeKind.FlatCategories,
                () =>
                {
                    flat_categories = ReadOnly(message.Categories.Select(Snapshot));
                    flat_categories_loaded = true;
                }));
        OnIncoming(
            MessageContracts.Navigator.Personalization.SavedSearches,
            (message, state_generation) => Store(
                state_generation,
                NavigatorStateChangeKind.SavedSearches,
                () => saved_searches = ReadOnly(message.Searches.Select(Snapshot))));
        OnIncoming(
            MessageContracts.Navigator.State.LiftedRooms,
            (message, state_generation) => Store(
                state_generation,
                NavigatorStateChangeKind.LiftedRooms,
                () => lifted_rooms = ReadOnly(message.Rooms.Select(Snapshot))));
        OnIncoming(
            MessageContracts.Navigator.Personalization.CollapsedCategories,
            (message, state_generation) => Store(
                state_generation,
                NavigatorStateChangeKind.CollapsedCategories,
                () => collapsed_categories = ReadOnly(message.Categories)));
        OnIncoming(
            MessageContracts.Navigator.State.Settings,
            (message, state_generation) => Store(
                state_generation,
                NavigatorStateChangeKind.Settings,
                () => settings = Snapshot(message)));
        OnIncoming(
            MessageContracts.Navigator.State.Preferences,
            (message, state_generation) => Store(
                state_generation,
                NavigatorStateChangeKind.Preferences,
                () => preferences = Snapshot(message)));
        OnIncoming(
            MessageContracts.Navigator.Search.Result,
            (message, state_generation) => StoreSearch(
                state_generation,
                Snapshot(message)));
        OnIncoming(
            MessageContracts.Navigator.Search.LegacyResult,
            (message, state_generation) => StoreSearch(
                state_generation,
                Snapshot(message)));
    }

    /// <summary>
    /// Builds the navigator filter text that searches a room field for a text.
    /// </summary>
    /// <param name="field">The room field to search.</param>
    /// <param name="text">The text to search for.</param>
    /// <returns>The text with the prefix of <paramref name="field"/>, or the text unchanged for <see cref="RoomSearchField.Anything"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="field"/> is not a defined value.</exception>
    public static string FilterText(RoomSearchField field, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!Enum.IsDefined(field))
            throw new ArgumentOutOfRangeException(nameof(field));
        return field switch
        {
            RoomSearchField.Owner => "owner:" + text,
            RoomSearchField.RoomName => "roomname:" + text,
            RoomSearchField.Tag => "tag:" + text,
            RoomSearchField.Group => "group:" + text,
            _ => text
        };
    }

    /// <summary>
    /// Sends a request to set the local user's home room.
    /// </summary>
    /// <param name="roomId">The id of the room.</param>
    public void SetHomeRoom(Id roomId) =>
        SendMessage(
            MessageContracts.Navigator.HomeRoomUpdate,
            new SetHomeRoomRequest(roomId));

    /// <summary>
    /// Sends a request to create a room.
    /// </summary>
    /// <param name="name">The name of the room.</param>
    /// <param name="description">The description of the room.</param>
    /// <param name="model">The name of the floor plan model.</param>
    /// <param name="category">The id of the room category.</param>
    /// <param name="maxVisitors">The maximum number of visitors.</param>
    /// <param name="tradeMode">The trading mode of the room.</param>
    public void CreateRoom(
        string name,
        string description,
        string model,
        int category,
        int maxVisitors,
        int tradeMode) =>
        SendMessage(
            MessageContracts.Navigator.RoomCreate,
            new CreateRoomRequest(
                name,
                description,
                model,
                category,
                maxVisitors,
                tradeMode));

    /// <summary>
    /// Sends a request to delete a room.
    /// </summary>
    /// <param name="roomId">The id of the room.</param>
    public void DeleteRoom(Id roomId) =>
        SendMessage(
            MessageContracts.Navigator.RoomDelete,
            new DeleteRoomRequest(roomId));

    internal static NavigatorSearchSnapshot Snapshot(NavigatorSearchResult value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new NavigatorSearchSnapshot(
            value.SearchCode,
            value.Filter,
            ReadOnly(value.Blocks.Select(block => new NavigatorSearchBlockSnapshot(
                block.SearchCode,
                block.Text,
                block.ActionAllowed,
                block.ForceClosed,
                block.ViewMode,
                ReadOnly(block.Rooms.Select(SnapshotRoom))))));
    }

    /// <inheritdoc/>
    protected override void Reset()
    {
        long state_generation = CurrentStateGeneration;
        lock (publication_sync)
        {
            NavigatorState updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation ||
                    state_generation == reset_generation)
                {
                    return;
                }
                committed_generation = state_generation;
                reset_generation = state_generation;
                categories = ReadOnly<NavigatorCategorySnapshot>([]);
                saved_searches = ReadOnly<NavigatorSearchEntrySnapshot>([]);
                lifted_rooms = ReadOnly<NavigatorLiftedRoomSnapshot>([]);
                flat_categories = ReadOnly<NavigatorFlatCategorySnapshot>([]);
                collapsed_categories = ReadOnly<string>([]);
                settings = null;
                preferences = null;
                metadata_loaded = false;
                flat_categories_loaded = false;
                generation = state_generation;
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(NavigatorStateChangeKind.Reset, updated);
        }
    }

    private void Store(
        long state_generation,
        NavigatorStateChangeKind kind,
        Action mutation)
    {
        lock (publication_sync)
        {
            NavigatorState updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation)
                    return;
                committed_generation = state_generation;
                reset_generation = -1;
                mutation();
                generation = state_generation;
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(kind, updated);
        }
    }

    private void StoreSearch(long state_generation, NavigatorSearchSnapshot result)
    {
        lock (publication_sync)
        {
            long state_revision;
            lock (state_sync)
            {
                if (state_generation < committed_generation ||
                    state_generation == reset_generation)
                {
                    return;
                }
                committed_generation = state_generation;
                state_revision = state.Revision;
            }
            SearchReceived?.Invoke(result, state_generation, state_revision);
        }
    }

    private NavigatorState PublishState()
    {
        var updated = new NavigatorState(
            metadata_loaded,
            flat_categories_loaded,
            generation,
            revision,
            categories,
            saved_searches,
            lifted_rooms,
            flat_categories,
            collapsed_categories,
            settings,
            preferences);
        Volatile.Write(ref state, updated);
        return updated;
    }

    private static NavigatorCategorySnapshot Snapshot(NavigatorCategory value) => new(
        value.SearchCode,
        ReadOnly(value.QuickLinks.Select(Snapshot)));

    private static NavigatorSearchEntrySnapshot Snapshot(NavigatorSearch value) => new(
        value.Id,
        value.SearchCode,
        value.Filter,
        value.Localization);

    private static NavigatorLiftedRoomSnapshot Snapshot(NavigatorLiftedRoom value) => new(
        value.RoomId,
        value.AreaId,
        value.Image,
        value.Caption);

    private static NavigatorFlatCategorySnapshot Snapshot(FlatCategory value) => new(
        value.NodeId,
        value.Name,
        value.Visible,
        value.Automatic,
        value.AutomaticCategoryKey,
        value.GlobalCategoryKey,
        value.StaffOnly,
        value.IsSelectable);

    private static NavigatorSettingsSnapshot Snapshot(NavigatorSettings value) => new(
        value.HomeRoomId,
        value.RoomIdToEnter);

    private static NavigatorPreferencesSnapshot Snapshot(NewNavigatorPreferences value) => new(
        value.WindowX,
        value.WindowY,
        value.WindowWidth,
        value.WindowHeight,
        value.LeftPaneHidden,
        value.ResultsMode);

    private static RoomDataSnapshot SnapshotRoom(RoomData value)
    {
        RoomDataSnapshot snapshot = SnapshotFactory.From(value);
        return snapshot with { Tags = ReadOnly(snapshot.Tags) };
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}
