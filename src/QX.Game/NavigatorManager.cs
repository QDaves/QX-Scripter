using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

public enum RoomSearchField
{
    Anything,
    Owner,
    RoomName,
    Tag,
    Group
}

public sealed record NavigatorSearchEntrySnapshot(
    int Id,
    string SearchCode,
    string Filter,
    string Localization);

public sealed record NavigatorCategorySnapshot(
    string SearchCode,
    IReadOnlyList<NavigatorSearchEntrySnapshot> QuickLinks);

public sealed record NavigatorLiftedRoomSnapshot(
    int RoomId,
    int AreaId,
    string Image,
    string Caption);

public sealed record NavigatorFlatCategorySnapshot(
    int NodeId,
    string Name,
    bool Visible,
    bool Automatic,
    string AutomaticCategoryKey,
    string GlobalCategoryKey,
    bool StaffOnly,
    bool Selectable);

public sealed record NavigatorSettingsSnapshot(Id HomeRoomId, Id RoomIdToEnter);

public sealed record NavigatorPreferencesSnapshot(
    int WindowX,
    int WindowY,
    int WindowWidth,
    int WindowHeight,
    bool LeftPaneHidden,
    int ResultsMode);

public sealed record NavigatorRoomMetadataSnapshot(
    Id RoomId,
    string FirstValue,
    string SecondValue);

public sealed record NavigatorSearchBlockSnapshot(
    string SearchCode,
    string Text,
    int ActionAllowed,
    bool ForceClosed,
    int ViewMode,
    IReadOnlyList<RoomDataSnapshot> Rooms,
    IReadOnlyList<NavigatorRoomMetadataSnapshot> UnityMetadata);

public sealed record NavigatorSearchSnapshot(
    string SearchCode,
    string Filter,
    IReadOnlyList<NavigatorSearchBlockSnapshot> Blocks)
{
    public IEnumerable<RoomDataSnapshot> Rooms => Blocks.SelectMany(block => block.Rooms);
}

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

    public NavigatorState State => Volatile.Read(ref state);

    internal event Action<NavigatorStateChangeKind, NavigatorState>? StateChanged;
    internal event Action<NavigatorSearchSnapshot, long, long>? SearchReceived;

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

    public void SetHomeRoom(Id room_id) =>
        SendMessage(
            MessageContracts.Navigator.HomeRoomUpdate,
            new SetHomeRoomRequest(room_id));

    public void CreateRoom(
        string name,
        string description,
        string model,
        int category,
        int max_visitors,
        int trade_mode) =>
        SendMessage(
            MessageContracts.Navigator.RoomCreate,
            new CreateRoomRequest(
                name,
                description,
                model,
                category,
                max_visitors,
                trade_mode));

    public void DeleteRoom(Id room_id) =>
        SendMessage(
            MessageContracts.Navigator.RoomDelete,
            new DeleteRoomRequest(room_id));

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
                ReadOnly(block.Rooms.Select(SnapshotRoom)),
                ReadOnly(block.UnityMetadata.Select(metadata => new NavigatorRoomMetadataSnapshot(
                    metadata.RoomId,
                    metadata.FirstValue,
                    metadata.SecondValue)))))));
    }

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
