using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;

namespace Qx.Game;

internal sealed record LocalProfileSnapshot(
    Id Id,
    string Name,
    string Figure,
    Gender Gender,
    string Motto,
    string RealName,
    bool DirectMail,
    int RespectTotal,
    int RespectLeft,
    int PetRespectLeft,
    bool StreamPublishingAllowed,
    string LastAccessDate,
    bool IsNameChangeable,
    bool IsSafetyLocked,
    bool IsTradeLocked,
    string NameColor,
    int RespectReplenishesLeft,
    int MaxRespectPerDay,
    int TrailingFields)
{
    public static LocalProfileSnapshot From(UserData value) => new(
        value.Id,
        value.Name,
        value.Figure,
        value.Gender,
        value.Motto,
        value.RealName,
        value.DirectMail,
        value.RespectTotal,
        value.RespectLeft,
        value.PetRespectLeft,
        value.StreamPublishingAllowed,
        value.LastAccessDate,
        value.IsNameChangeable,
        value.IsSafetyLocked,
        value.IsTradeLocked,
        value.NameColor,
        value.RespectReplenishesLeft,
        value.MaxRespectPerDay,
        value.TrailingFields);

    public UserData ToUserData() => new()
    {
        Id = Id,
        Name = Name,
        Figure = Figure,
        Gender = Gender,
        Motto = Motto,
        RealName = RealName,
        DirectMail = DirectMail,
        RespectTotal = RespectTotal,
        RespectLeft = RespectLeft,
        PetRespectLeft = PetRespectLeft,
        StreamPublishingAllowed = StreamPublishingAllowed,
        LastAccessDate = LastAccessDate,
        IsNameChangeable = IsNameChangeable,
        IsSafetyLocked = IsSafetyLocked,
        IsTradeLocked = IsTradeLocked,
        NameColor = NameColor,
        RespectReplenishesLeft = RespectReplenishesLeft,
        MaxRespectPerDay = MaxRespectPerDay,
        TrailingFields = TrailingFields
    };
}

internal sealed record ProfileState(
    long Generation,
    long Revision,
    Session? Session,
    LocalProfileSnapshot? Identity,
    bool BlockListLoaded,
    IReadOnlyList<Id> BlockedUserIds,
    bool IgnoreListLoaded,
    IReadOnlyList<Id> IgnoredUserIds,
    bool FigureSetsLoaded,
    IReadOnlyList<FigureSetEntry> FigureSets,
    IReadOnlyList<string> BoundFurnitureNames,
    bool SanctionsLoaded,
    AccountSanctionStatus? Sanctions)
{
    public static ProfileState Empty(
        long generation,
        long revision,
        Session? session = null) => new(
        generation,
        revision,
        session,
        null,
        false,
        ReadOnly<Id>([]),
        false,
        ReadOnly<Id>([]),
        false,
        ReadOnly<FigureSetEntry>([]),
        ReadOnly<string>([]),
        false,
        null);

    public bool Loaded => Identity is not null;

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}

internal enum ProfileStateChangeKind
{
    Identity,
    BlockList,
    BlockResult,
    IgnoreList,
    IgnoreResult,
    FigureSets,
    Sanctions,
    Reset
}

internal sealed record ProfileStateUpdate(
    ProfileStateChangeKind Kind,
    ProfileState State,
    object? Value);

internal sealed class ProfileManager : GameStateManager
{
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private ProfileState state = ProfileState.Empty(0, 0);
    private long committed_generation;
    private long reset_generation = -1;

    internal ProfileState State => Volatile.Read(ref state);
    internal UserData? UserData => State.Identity?.ToUserData();
    internal Id Id => State.Identity?.Id ?? -1;
    internal string Name => State.Identity?.Name ?? string.Empty;
    internal bool IsLoaded => State.Loaded;
    internal Func<int, User?>? RoomUserByIndex { get; set; }
    internal event Action<ProfileStateUpdate>? StateChanged;

    protected override void OnAttach()
    {
        Reset();
        OnConnected(BindSession);
        OnIncoming(
            MessageContracts.Users.ProfileSnapshot,
            (message, generation) => Store(
                generation,
                ProfileStateChangeKind.Identity,
                message,
                current => current with { Identity = LocalProfileSnapshot.From(message) }));
        OnIncoming(
            MessageContracts.Users.Block.ListSnapshot,
            (message, generation) => Store(
                generation,
                ProfileStateChangeKind.BlockList,
                message,
                current => current with
                {
                    BlockListLoaded = true,
                    BlockedUserIds = Ids(message.UserIds)
                }));
        OnIncoming(
            MessageContracts.Users.Block.Updated,
            (message, generation) => ApplyBlockResult(message, generation));
        OnIncoming(
            MessageContracts.Users.Ignore.ListSnapshot,
            (message, generation) => Store(
                generation,
                ProfileStateChangeKind.IgnoreList,
                message,
                current => current with
                {
                    IgnoreListLoaded = true,
                    IgnoredUserIds = Ids(message.UserIds)
                }));
        OnIncoming(
            MessageContracts.Users.Ignore.Updated,
            (message, generation) => Store(
                generation,
                ProfileStateChangeKind.IgnoreResult,
                message,
                current => current));
        OnIncoming(
            MessageContracts.Users.FigureSets.Added,
            (message, generation) => ApplyFigureSetAdded(message, generation));
        OnIncoming(
            MessageContracts.Users.FigureSets.Removed,
            (message, generation) => ApplyFigureSetRemoved(message, generation));
        OnIncoming(
            MessageContracts.Users.FigureSets.Snapshot,
            (message, generation) => Store(
                generation,
                ProfileStateChangeKind.FigureSets,
                message,
                current => current with
                {
                    FigureSetsLoaded = true,
                    FigureSets = FigureEntries(message.Entries),
                    BoundFurnitureNames = Strings(message.BoundFurnitureNames)
                }));
        OnIncoming(
            MessageContracts.Users.Sanctions.Snapshot,
            (message, generation) => Store(
                generation,
                ProfileStateChangeKind.Sanctions,
                message,
                current => current with
                {
                    SanctionsLoaded = true,
                    Sanctions = message
                }));
        OnIncoming(
            MessageContracts.Users.FigureUpdated,
            (message, generation) => UpdateIdentity(
                generation,
                current => current with
                {
                    Figure = message.Figure,
                    Gender = Genders.Parse(message.Gender)
                }));
        OnIncoming(
            MessageContracts.Room.Occupants.Identity.Appearance,
            (message, generation) => ApplyRoomAppearance(message, generation));
        OnIncoming(
            MessageContracts.Room.Occupants.Identity.Name,
            (message, generation) => UpdateIdentity(
                generation,
                current => current.Id == message.WebId
                    ? current with
                    {
                        Name = message.NewName,
                        IsNameChangeable = false
                    }
                    : null));
        OnIncoming(
            MessageContracts.Users.NameChangeResult,
            (message, generation) => UpdateIdentity(
                generation,
                current => message.Success && current.IsNameChangeable
                    ? current with { IsNameChangeable = false }
                    : null));
        OnIncoming(
            MessageContracts.Users.SafetyLockChanged,
            (message, generation) => UpdateIdentity(
                generation,
                current => current.IsSafetyLocked != message.IsLocked
                    ? current with { IsSafetyLocked = message.IsLocked }
                    : null));
    }

    protected override void Reset()
    {
        long generation = CurrentStateGeneration;
        Session? session = CurrentSession;
        lock (publication_sync)
        {
            ProfileState updated;
            lock (state_sync)
            {
                if (generation < committed_generation || generation == reset_generation)
                    return;
                committed_generation = generation;
                reset_generation = generation;
                updated = ProfileState.Empty(
                    generation,
                    checked(state.Revision + 1),
                    session);
                Volatile.Write(ref state, updated);
            }
            StateChanged?.Invoke(new ProfileStateUpdate(
                ProfileStateChangeKind.Reset,
                updated,
                null));
        }
    }

    private void ApplyBlockResult(BlockUserUpdate message, long generation)
    {
        ClientType client = CurrentClient;
        Store(
            generation,
            ProfileStateChangeKind.BlockResult,
            message,
            current => client is ClientType.Unity
                ? current with
                {
                    BlockedUserIds = message.Result switch
                    {
                        0 => Ids(current.BlockedUserIds.Where(id => id != message.UserId)),
                        1 => Ids(current.BlockedUserIds.Append(message.UserId)),
                        _ => current.BlockedUserIds
                    }
                }
                : current);
    }

    private void ApplyFigureSetAdded(FigureSetIdAdded message, long generation) => Store(
        generation,
        ProfileStateChangeKind.FigureSets,
        message,
        current => current with
        {
            FigureSets = FigureEntries(
                current.FigureSets
                    .Where(entry => entry.FigureSetId != message.FigureSetId)
                    .Append(new FigureSetEntry(message.FigureSetId, 0)))
        });

    private void ApplyFigureSetRemoved(FigureSetIdRemoved message, long generation) => Store(
        generation,
        ProfileStateChangeKind.FigureSets,
        message,
        current => current with
        {
            FigureSets = FigureEntries(
                current.FigureSets.Where(entry => entry.FigureSetId != message.FigureSetId))
        });

    private void ApplyRoomAppearance(
        UserChanged message,
        long generation)
    {
        if (message.Index < 0)
        {
            UpdateIdentity(
                generation,
                current => current with
                {
                    Figure = message.Figure,
                    Gender = Genders.Parse(message.Gender)
                });
            return;
        }

        User? room_user = RoomUserByIndex?.Invoke(message.Index);
        if (room_user is null)
            return;
        UpdateIdentity(
            generation,
            current => current.Id == room_user.Id
                ? current with
                {
                    Figure = message.Figure,
                    Gender = Genders.Parse(message.Gender),
                    Motto = message.Motto
                }
                : null);
    }

    private void UpdateIdentity(
        long generation,
        Func<LocalProfileSnapshot, LocalProfileSnapshot?> update) => Store(
            generation,
            ProfileStateChangeKind.Identity,
            null,
            current => current.Identity is not { } identity
                ? null
                : update(identity) is { } replacement
                    ? current with { Identity = replacement }
                    : null);

    private void Store(
        long generation,
        ProfileStateChangeKind kind,
        object? value,
        Func<ProfileState, ProfileState?> mutation)
    {
        Session? session = Interceptor.Session;
        if (session is null)
            return;
        lock (publication_sync)
        {
            ProfileState? updated;
            lock (state_sync)
            {
                if (generation < committed_generation)
                    return;
                ProfileState current = generation == state.Generation &&
                    ReferenceEquals(state.Session, session)
                    ? state
                    : ProfileState.Empty(generation, state.Revision, session);
                ProfileState? replacement = mutation(current);
                if (replacement is null)
                    return;
                committed_generation = generation;
                reset_generation = -1;
                updated = replacement with
                {
                    Generation = generation,
                    Revision = checked(state.Revision + 1),
                    Session = session
                };
                Volatile.Write(ref state, updated);
            }
            StateChanged?.Invoke(new ProfileStateUpdate(kind, updated, value));
        }
    }

    private void BindSession(Session session)
    {
        long generation = CurrentStateGeneration;
        lock (publication_sync)
        {
            ProfileState updated;
            lock (state_sync)
            {
                if (generation < committed_generation)
                    return;
                committed_generation = generation;
                reset_generation = -1;
                updated = ProfileState.Empty(
                    generation,
                    checked(state.Revision + 1),
                    session);
                Volatile.Write(ref state, updated);
            }
            StateChanged?.Invoke(new ProfileStateUpdate(
                ProfileStateChangeKind.Reset,
                updated,
                null));
        }
    }

    private static IReadOnlyList<Id> Ids(IEnumerable<Id> values) =>
        Array.AsReadOnly(values
            .Distinct()
            .OrderBy(value => (long)value)
            .ToArray());

    private static IReadOnlyList<FigureSetEntry> FigureEntries(
        IEnumerable<FigureSetEntry> values) => Array.AsReadOnly(values
            .GroupBy(value => value.FigureSetId)
            .Select(group => group.Last())
            .OrderBy(value => value.FigureSetId)
            .ToArray());

    private static IReadOnlyList<string> Strings(IEnumerable<string> values) =>
        Array.AsReadOnly(values.ToArray());
}
