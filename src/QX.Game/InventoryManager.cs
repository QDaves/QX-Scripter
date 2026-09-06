using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

internal sealed record FurniInventoryState(
    long SnapshotRevision,
    long LoadGeneration,
    bool Loaded,
    bool Loading,
    bool Stale,
    bool RecoveryPending,
    long RecoveryRetiredRequestEpoch,
    long RecoveryActiveRequestEpoch,
    int ExpectedFragments,
    int ReceivedFragments,
    IReadOnlyDictionary<long, InventoryItemSnapshot> Items);

internal sealed record PetInventoryState(
    long SnapshotRevision,
    long LoadGeneration,
    bool Loaded,
    bool Loading,
    bool Stale,
    bool RecoveryPending,
    long RecoveryRetiredRequestEpoch,
    long RecoveryActiveRequestEpoch,
    int ExpectedFragments,
    int ReceivedFragments,
    IReadOnlyDictionary<long, InventoryPetSnapshot> Pets);

internal sealed record InventoryState(
    long Generation,
    long Revision,
    Session? Session,
    FurniInventoryState Furni,
    PetInventoryState Pets);

internal enum InventoryStateChangeKind
{
    FurniRequest,
    FurniFragment,
    FurniAddedOrUpdated,
    FurniRemoved,
    FurniInvalidated,
    PetRequest,
    PetFragment,
    PetAddedOrUpdated,
    PetRemoved,
    Reset
}

internal sealed record InventoryStateUpdate(
    InventoryStateChangeKind Kind,
    InventoryState State,
    object? Value);

internal sealed record FurniFragmentCommit(
    int Total,
    int Index,
    IReadOnlyList<InventoryItemSnapshot> Items,
    long RequestEpoch,
    long LoadGeneration);

internal sealed record PetFragmentCommit(
    int Total,
    int Index,
    IReadOnlyList<InventoryPetSnapshot> Pets,
    long RequestEpoch,
    long LoadGeneration);

internal sealed record FurniItemsCommit(
    IReadOnlyList<InventoryItemSnapshot> Added,
    IReadOnlyList<InventoryItemSnapshot> Updated);

internal sealed record FurniRemovedCommit(
    IReadOnlyList<InventoryItemSnapshot> Items);

internal sealed record PetItemCommit(
    InventoryPetSnapshot Pet,
    bool Added,
    bool OpenInventory);

internal sealed record PetRemovedCommit(InventoryPetSnapshot? Pet);

internal sealed class InventoryManager : GameStateManager
{
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<InventoryStateUpdate> publications = [];
    private readonly FragmentedInventory<InventoryItemSnapshot> furni = new(item => item.ItemId);
    private readonly FragmentedInventory<InventoryPetSnapshot> pets = new(pet => pet.Id);
    private InventoryState state;
    private long generation;
    private long revision;
    private long committed_generation;
    private long reset_generation = -1;
    private Session? session;
    private bool publishing;

    public InventoryManager()
    {
        state = Snapshot();
    }

    internal InventoryState State => Volatile.Read(ref state);
    internal event Action<InventoryStateUpdate>? StateCommitted;
    internal event Action<InventoryStateUpdate>? StateChanged;

    protected override void OnAttach()
    {
        Reset();
        OnConnected(BindSession);
        OnOutgoing(MessageContracts.Inventory.Furni.Request, (_, state_generation) =>
            Store(
                state_generation,
                InventoryStateChangeKind.FurniRequest,
                null,
                () => furni.BeginRequest()));
        OnIncoming(MessageContracts.Inventory.Furni.Snapshot, ApplyFurniFragment);
        OnIncoming(MessageContracts.Inventory.Furni.AddedOrUpdated, ApplyFurniItems);
        OnIncoming(
            MessageContracts.Inventory.Furni.Removed,
            (message, state_generation) => RemoveFurni([message.ItemId], state_generation));
        OnIncoming(
            MessageContracts.Inventory.Furni.RemovedMultiple,
            (message, state_generation) => RemoveFurni(message.ItemIds, state_generation));
        OnIncoming(
            MessageContracts.Inventory.Furni.Invalidated,
            (_, state_generation) => Store(
                state_generation,
                InventoryStateChangeKind.FurniInvalidated,
                null,
                furni.Invalidate));
        OnIncoming(MessageContracts.Inventory.Furni.PostItPlaced, ApplyPostItCount);
        OnOutgoing(MessageContracts.Inventory.Pets.Request, (_, state_generation) =>
            Store(
                state_generation,
                InventoryStateChangeKind.PetRequest,
                null,
                () => pets.BeginRequest()));
        OnIncoming(MessageContracts.Inventory.Pets.Snapshot, ApplyPetFragment);
        OnIncoming(MessageContracts.Inventory.Pets.Added, ApplyPet);
        OnIncoming(
            MessageContracts.Inventory.Pets.Removed,
            (message, state_generation) => RemovePet(message.PetId, state_generation));
    }

    protected override void Reset()
    {
        long state_generation = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        bool drain;
        lock (publication_sync)
        {
            InventoryState updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation || state_generation == reset_generation)
                    return;
                committed_generation = state_generation;
                reset_generation = state_generation;
                generation = state_generation;
                session = active_session;
                furni.Reset();
                pets.Reset();
                revision = checked(revision + 1);
                updated = Publish();
            }
            var update = new InventoryStateUpdate(
                InventoryStateChangeKind.Reset,
                updated,
                null);
            StateCommitted?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private void BindSession(Session active_session)
    {
        long state_generation = CurrentStateGeneration;
        bool drain;
        lock (publication_sync)
        {
            InventoryState updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation)
                    return;
                committed_generation = state_generation;
                reset_generation = -1;
                generation = state_generation;
                session = active_session;
                furni.Reset();
                pets.Reset();
                revision = checked(revision + 1);
                updated = Publish();
            }
            var update = new InventoryStateUpdate(
                InventoryStateChangeKind.Reset,
                updated,
                null);
            StateCommitted?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private void ApplyFurniFragment(FurniList message, long state_generation)
    {
        IReadOnlyList<InventoryItemSnapshot> items = ReadOnly(message.Items.Select(SnapshotOf));
        Store(
            state_generation,
            InventoryStateChangeKind.FurniFragment,
            () =>
            {
                FragmentCommit result = furni.ApplyFragment(message.Total, message.Index, items);
                return result.Accepted
                    ? new FurniFragmentCommit(
                        message.Total,
                        message.Index,
                        items,
                        result.RequestEpoch,
                        result.LoadGeneration)
                    : null;
            });
    }

    private void ApplyPetFragment(PetInventory message, long state_generation)
    {
        IReadOnlyList<InventoryPetSnapshot> values = ReadOnly(message.Pets.Select(SnapshotOf));
        Store(
            state_generation,
            InventoryStateChangeKind.PetFragment,
            () =>
            {
                FragmentCommit result = pets.ApplyFragment(message.Total, message.Index, values);
                return result.Accepted
                    ? new PetFragmentCommit(
                        message.Total,
                        message.Index,
                        values,
                        result.RequestEpoch,
                        result.LoadGeneration)
                    : null;
            });
    }

    private void ApplyFurniItems(FurniListAddOrUpdate message, long state_generation)
    {
        IReadOnlyList<InventoryItemSnapshot> values = ReadOnly(message.Items.Select(SnapshotOf));
        Store(
            state_generation,
            InventoryStateChangeKind.FurniAddedOrUpdated,
            () =>
            {
                UpsertCommit<InventoryItemSnapshot> result = furni.Upsert(values);
                return result.Changed
                    ? new FurniItemsCommit(result.Added, result.Updated)
                    : null;
            });
    }

    private void ApplyPostItCount(PostItPlaced message, long state_generation)
    {
        Store(
            state_generation,
            InventoryStateChangeKind.FurniAddedOrUpdated,
            () =>
            {
                if (!furni.TryGet(message.ItemId, out InventoryItemSnapshot? item) || item is null ||
                    item.Data.Type != ItemDataType.Legacy.ToString())
                {
                    return null;
                }
                string value = message.ItemsLeft.ToString(CultureInfo.InvariantCulture);
                InventoryItemSnapshot updated = item with
                {
                    Data = item.Data with
                    {
                        Value = value,
                        State = message.ItemsLeft
                    }
                };
                return furni.Replace(updated)
                    ? new FurniItemsCommit(
                        ReadOnly<InventoryItemSnapshot>([]),
                        ReadOnly([updated]))
                    : null;
            });
    }

    private void RemoveFurni(IReadOnlyList<Id> item_ids, long state_generation)
    {
        long[] identifiers = item_ids.Select(value => (long)value).Distinct().ToArray();
        Store(
            state_generation,
            InventoryStateChangeKind.FurniRemoved,
            () =>
            {
                RemovalCommit<InventoryItemSnapshot> removed = furni.Remove(identifiers);
                return removed.Changed ? new FurniRemovedCommit(removed.Items) : null;
            });
    }

    private void ApplyPet(PetAddedToInventory message, long state_generation)
    {
        InventoryPetSnapshot value = SnapshotOf(message.Pet);
        Store(
            state_generation,
            InventoryStateChangeKind.PetAddedOrUpdated,
            () =>
            {
                UpsertCommit<InventoryPetSnapshot> result = pets.Upsert([value]);
                if (!result.Changed)
                    return null;
                return new PetItemCommit(
                    value,
                    result.Added.Count != 0,
                    message.OpenInventory);
            });
    }

    private void RemovePet(Id pet_id, long state_generation)
    {
        Store(
            state_generation,
            InventoryStateChangeKind.PetRemoved,
            () =>
            {
                RemovalCommit<InventoryPetSnapshot> removed = pets.Remove([(long)pet_id]);
                return removed.Changed
                    ? new PetRemovedCommit(removed.Items.FirstOrDefault())
                    : null;
            });
    }

    private void Store(
        long state_generation,
        InventoryStateChangeKind kind,
        object? value,
        Func<bool> mutation) => Store(
            state_generation,
            kind,
            () => mutation() ? value ?? RequestCommit.Instance : null,
            value is null);

    private void Store(
        long state_generation,
        InventoryStateChangeKind kind,
        Func<object?> mutation,
        bool unwrap_request_commit = false)
    {
        Session? active_session = Interceptor.Session;
        if (active_session is null)
            return;
        bool drain;
        lock (publication_sync)
        {
            InventoryState? updated = null;
            object? value = null;
            lock (state_sync)
            {
                if (state_generation < committed_generation)
                    return;
                if (generation != state_generation || !ReferenceEquals(session, active_session))
                {
                    generation = state_generation;
                    session = active_session;
                    furni.Reset();
                    pets.Reset();
                }
                object? candidate = mutation();
                if (candidate is null)
                    return;
                committed_generation = state_generation;
                reset_generation = -1;
                revision = checked(revision + 1);
                value = unwrap_request_commit && ReferenceEquals(candidate, RequestCommit.Instance)
                    ? null
                    : candidate;
                updated = Publish();
            }
            var update = new InventoryStateUpdate(kind, updated, value);
            StateCommitted?.Invoke(update);
            drain = EnqueuePublication(update);
        }
        if (drain)
            DrainPublications();
    }

    private bool EnqueuePublication(InventoryStateUpdate update)
    {
        publications.Enqueue(update);
        if (publishing)
            return false;
        publishing = true;
        return true;
    }

    private void DrainPublications()
    {
        Exception? failure = null;
        while (true)
        {
            InventoryStateUpdate update;
            lock (publication_sync)
            {
                if (!publications.TryDequeue(out update!))
                {
                    publishing = false;
                    break;
                }
            }
            try
            {
                StateChanged?.Invoke(update);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private InventoryState Publish()
    {
        var updated = new InventoryState(
            generation,
            revision,
            session,
            new FurniInventoryState(
                furni.SnapshotRevision,
                furni.LoadGeneration,
                furni.Loaded,
                furni.Loading,
                furni.Stale,
                furni.RecoveryPending,
                furni.RecoveryRetiredRequestEpoch,
                furni.RecoveryActiveRequestEpoch,
                furni.ExpectedFragments,
                furni.ReceivedFragments,
                furni.Snapshot),
            new PetInventoryState(
                pets.SnapshotRevision,
                pets.LoadGeneration,
                pets.Loaded,
                pets.Loading,
                pets.Stale,
                pets.RecoveryPending,
                pets.RecoveryRetiredRequestEpoch,
                pets.RecoveryActiveRequestEpoch,
                pets.ExpectedFragments,
                pets.ReceivedFragments,
                pets.Snapshot));
        Volatile.Write(ref state, updated);
        return updated;
    }

    private InventoryState Snapshot() => new(
        generation,
        revision,
        session,
        new FurniInventoryState(
            furni.SnapshotRevision,
            furni.LoadGeneration,
            furni.Loaded,
            furni.Loading,
            furni.Stale,
            furni.RecoveryPending,
            furni.RecoveryRetiredRequestEpoch,
            furni.RecoveryActiveRequestEpoch,
            furni.ExpectedFragments,
            furni.ReceivedFragments,
            furni.Snapshot),
        new PetInventoryState(
            pets.SnapshotRevision,
            pets.LoadGeneration,
            pets.Loaded,
            pets.Loading,
            pets.Stale,
            pets.RecoveryPending,
            pets.RecoveryRetiredRequestEpoch,
            pets.RecoveryActiveRequestEpoch,
            pets.ExpectedFragments,
            pets.ReceivedFragments,
            pets.Snapshot));

    internal static InventoryItemSnapshot SnapshotOf(InventoryItem value)
    {
        ArgumentNullException.ThrowIfNull(value);
        InventoryItemSnapshot item = SnapshotFactory.From(value);
        return item with { Data = Freeze(item.Data), Definition = null };
    }

    internal static InventoryPetSnapshot SnapshotOf(InventoryPet value)
    {
        ArgumentNullException.ThrowIfNull(value);
        InventoryPetSnapshot pet = SnapshotFactory.From(value);
        return pet with { CustomParts = ReadOnly(pet.CustomParts) };
    }

    internal static bool Equivalent(FurniFragmentCommit value, FurniList message) =>
        value.Total == message.Total &&
        value.Index == message.Index &&
        EquivalentItems(value.Items, message.Items.Select(SnapshotOf));

    internal static bool Equivalent(PetFragmentCommit value, PetInventory message) =>
        value.Total == message.Total &&
        value.Index == message.Index &&
        EquivalentPets(value.Pets, message.Pets.Select(SnapshotOf));

    private static ItemDataSnapshot Freeze(ItemDataSnapshot value) => value with
    {
        MapEntries = value.MapEntries is null
            ? null
            : new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(value.MapEntries, StringComparer.Ordinal)),
        StringValues = value.StringValues is null ? null : ReadOnly(value.StringValues),
        IntValues = value.IntValues is null ? null : ReadOnly(value.IntValues),
        HighScores = value.HighScores is null
            ? null
            : ReadOnly(value.HighScores.Select(score => score with
            {
                Names = ReadOnly(score.Names)
            }))
    };

    private static bool EquivalentItems(
        IReadOnlyList<InventoryItemSnapshot> left,
        IEnumerable<InventoryItemSnapshot> right)
    {
        InventoryItemSnapshot[] values = right.ToArray();
        if (left.Count != values.Length)
            return false;
        for (int index = 0; index < left.Count; index++)
        {
            InventoryItemSnapshot first = left[index];
            InventoryItemSnapshot second = values[index];
            if (first.ItemId != second.ItemId ||
                first.Type != second.Type ||
                first.Id != second.Id ||
                first.Kind != second.Kind ||
                first.Category != second.Category ||
                first.IsRecyclable != second.IsRecyclable ||
                first.IsTradeable != second.IsTradeable ||
                first.IsGroupable != second.IsGroupable ||
                first.IsSellable != second.IsSellable ||
                first.SecondsToExpiration != second.SecondsToExpiration ||
                first.HasRentPeriodStarted != second.HasRentPeriodStarted ||
                first.RoomId != second.RoomId ||
                first.IsUnseen != second.IsUnseen ||
                first.Timestamp != second.Timestamp ||
                first.IsNft != second.IsNft ||
                first.NftName != second.NftName ||
                first.IsExternalImage != second.IsExternalImage ||
                first.SlotId != second.SlotId ||
                first.Extra != second.Extra ||
                !Equivalent(first.Data, second.Data))
            {
                return false;
            }
        }
        return true;
    }

    private static bool EquivalentPets(
        IReadOnlyList<InventoryPetSnapshot> left,
        IEnumerable<InventoryPetSnapshot> right)
    {
        InventoryPetSnapshot[] values = right.ToArray();
        if (left.Count != values.Length)
            return false;
        for (int index = 0; index < left.Count; index++)
        {
            InventoryPetSnapshot first = left[index];
            InventoryPetSnapshot second = values[index];
            if (first.Id != second.Id ||
                first.Name != second.Name ||
                first.TypeId != second.TypeId ||
                first.PaletteId != second.PaletteId ||
                first.Color != second.Color ||
                first.BreedId != second.BreedId ||
                first.Level != second.Level ||
                first.RarityLevel != second.RarityLevel ||
                first.RoomId != second.RoomId ||
                first.RoomName != second.RoomName ||
                first.RoomContext != second.RoomContext ||
                first.CustomParts.Count != second.CustomParts.Count)
            {
                return false;
            }
            for (int part_index = 0; part_index < first.CustomParts.Count; part_index++)
            {
                if (first.CustomParts[part_index] != second.CustomParts[part_index])
                    return false;
            }
        }
        return true;
    }

    private static bool Equivalent(ItemDataSnapshot left, ItemDataSnapshot right)
    {
        if (left.Type != right.Type ||
            left.Flags != right.Flags ||
            left.Value != right.Value ||
            left.State != right.State ||
            left.IsLimitedRare != right.IsLimitedRare ||
            left.UniqueSerialNumber != right.UniqueSerialNumber ||
            left.UniqueSeriesSize != right.UniqueSeriesSize ||
            left.UniqueLimitedData != right.UniqueLimitedData ||
            left.VoteResult != right.VoteResult ||
            left.ScoreType != right.ScoreType ||
            left.ClearType != right.ClearType ||
            left.Hits != right.Hits ||
            left.Target != right.Target ||
            !Equivalent(left.MapEntries, right.MapEntries) ||
            !Equivalent(left.StringValues, right.StringValues) ||
            !Equivalent(left.IntValues, right.IntValues) ||
            !EquivalentScores(left.HighScores, right.HighScores))
        {
            return false;
        }
        return true;
    }

    private static bool Equivalent(
        IReadOnlyDictionary<string, string>? left,
        IReadOnlyDictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;
        return left.All(pair =>
            right.TryGetValue(pair.Key, out string? value) && value == pair.Value);
    }

    private static bool Equivalent<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        return left is not null && right is not null && left.SequenceEqual(right);
    }

    private static bool EquivalentScores(
        IReadOnlyList<HighScoreSnapshot>? left,
        IReadOnlyList<HighScoreSnapshot>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;
        for (int index = 0; index < left.Count; index++)
        {
            if (left[index].Score != right[index].Score ||
                !left[index].Names.SequenceEqual(right[index].Names))
            {
                return false;
            }
        }
        return true;
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private sealed class RequestCommit
    {
        public static RequestCommit Instance { get; } = new();
    }

    private sealed class FragmentedInventory<T>(Func<T, long> identity)
    {
        private readonly Dictionary<long, T> entries = [];
        private readonly Dictionary<int, IReadOnlyList<T>> fragments = [];
        private readonly Dictionary<long, InventoryDelta<T>> deltas = [];
        private IReadOnlyDictionary<long, T> snapshot = EmptyMap();
        private long? retired_request_epoch;
        private long active_request_epoch;
        private long fragment_request_epoch;
        private long request_epoch;
        private bool restart_on_index_zero;
        private bool allow_nonzero_restart;

        public long SnapshotRevision { get; private set; }
        public long LoadGeneration { get; private set; }
        public bool Loaded { get; private set; }
        public bool Loading { get; private set; }
        public bool Stale { get; private set; }
        public bool RecoveryPending { get; private set; }
        public long RecoveryRetiredRequestEpoch { get; private set; }
        public long RecoveryActiveRequestEpoch { get; private set; }
        public int ExpectedFragments { get; private set; } = -1;
        public int ReceivedFragments => Loaded && ExpectedFragments > 0
            ? ExpectedFragments
            : fragments.Count;
        public IReadOnlyDictionary<long, T> Snapshot => snapshot;

        public bool BeginRequest()
        {
            long next_epoch = checked(++request_epoch);
            if (RecoveryPending)
            {
                fragments.Clear();
                deltas.Clear();
                ExpectedFragments = -1;
                fragment_request_epoch = 0;
                retired_request_epoch = null;
                restart_on_index_zero = true;
                allow_nonzero_restart = true;
                RecoveryPending = false;
                RecoveryRetiredRequestEpoch = 0;
                RecoveryActiveRequestEpoch = 0;
                LoadGeneration = checked(LoadGeneration + 1);
            }
            else if (Loading && active_request_epoch != 0)
            {
                retired_request_epoch = fragment_request_epoch == 0
                    ? active_request_epoch
                    : fragment_request_epoch;
                fragments.Clear();
                deltas.Clear();
                ExpectedFragments = -1;
                fragment_request_epoch = 0;
                restart_on_index_zero = true;
                allow_nonzero_restart = true;
                LoadGeneration = checked(LoadGeneration + 1);
            }
            else if (!Loaded && ExpectedFragments >= 0)
            {
                restart_on_index_zero = true;
            }
            active_request_epoch = next_epoch;
            Loading = true;
            Stale = entries.Count != 0;
            Touch();
            return true;
        }

        public FragmentCommit ApplyFragment(int total, int index, IReadOnlyList<T> values)
        {
            if (total <= 0)
                throw new InvalidDataException($"Fragment count must be positive, received {total}.");
            if ((uint)index >= (uint)total)
                throw new InvalidDataException($"Fragment index {index} is outside 0..{total - 1}.");
            if (restart_on_index_zero)
            {
                if (index != 0 && !allow_nonzero_restart)
                    return default;
                BeginGeneration(total, TakeRequestEpoch());
                restart_on_index_zero = false;
                allow_nonzero_restart = false;
            }
            else if (Loaded)
            {
                if (index != 0)
                    return default;
                BeginGeneration(total, active_request_epoch);
            }
            else if (ExpectedFragments < 0)
            {
                BeginGeneration(total, active_request_epoch);
            }
            else if (ExpectedFragments != total)
            {
                if (index != 0)
                    return default;
                BeginGeneration(total, fragment_request_epoch);
            }
            else if (index == 0 && fragments.ContainsKey(0))
            {
                BeginGeneration(total, fragment_request_epoch);
            }

            fragments[index] = ReadOnly(values);
            Loading = true;
            Touch();
            long load_generation = LoadGeneration;
            long response_epoch = fragment_request_epoch;
            if (fragments.Count != ExpectedFragments)
                return new FragmentCommit(true, false, response_epoch, load_generation);

            var replacement = new Dictionary<long, T>();
            for (int fragment_index = 0; fragment_index < ExpectedFragments; fragment_index++)
            {
                if (!fragments.TryGetValue(fragment_index, out IReadOnlyList<T>? fragment))
                    return new FragmentCommit(true, false, response_epoch, load_generation);
                foreach (T value in fragment)
                    replacement[identity(value)] = value;
            }
            foreach (InventoryDelta<T> delta in deltas.Values)
                delta.Apply(replacement);

            if (fragment_request_epoch != 0 &&
                active_request_epoch != 0 &&
                fragment_request_epoch != active_request_epoch)
            {
                RecoveryRetiredRequestEpoch = fragment_request_epoch;
                RecoveryActiveRequestEpoch = active_request_epoch;
                fragments.Clear();
                deltas.Clear();
                ExpectedFragments = -1;
                fragment_request_epoch = 0;
                restart_on_index_zero = true;
                allow_nonzero_restart = true;
                Loaded = false;
                Loading = false;
                Stale = entries.Count != 0;
                RecoveryPending = true;
                LoadGeneration = checked(LoadGeneration + 1);
                Touch();
                return new FragmentCommit(true, false, response_epoch, load_generation);
            }

            entries.Clear();
            foreach ((long key, T value) in replacement)
                entries[key] = value;
            PublishEntries();
            fragments.Clear();
            deltas.Clear();
            restart_on_index_zero = false;
            retired_request_epoch = null;
            active_request_epoch = 0;
            Loaded = true;
            Loading = false;
            Stale = false;
            RecoveryPending = false;
            return new FragmentCommit(true, true, response_epoch, load_generation);
        }

        public UpsertCommit<T> Upsert(IReadOnlyList<T> values)
        {
            var added = new List<T>();
            var updated = new List<T>();
            foreach (T value in values)
            {
                long key = identity(value);
                bool exists = entries.ContainsKey(key);
                entries[key] = value;
                if (!Loaded || Loading)
                    deltas[key] = InventoryDelta<T>.Upsert(key, value);
                (exists ? updated : added).Add(value);
            }
            if (added.Count == 0 && updated.Count == 0)
                return new UpsertCommit<T>(false, ReadOnly<T>([]), ReadOnly<T>([]));
            PublishEntries();
            Touch();
            return new UpsertCommit<T>(true, ReadOnly(added), ReadOnly(updated));
        }

        public RemovalCommit<T> Remove(IEnumerable<long> identifiers)
        {
            var removed = new List<T>();
            bool changed = false;
            foreach (long key in identifiers)
            {
                changed = true;
                if (!Loaded || Loading)
                {
                    deltas[key] = InventoryDelta<T>.Remove(key);
                }
                if (entries.Remove(key, out T? value))
                {
                    removed.Add(value);
                    changed = true;
                }
            }
            if (!changed)
                return new RemovalCommit<T>(false, ReadOnly<T>([]));
            if (removed.Count != 0)
                PublishEntries();
            Touch();
            return new RemovalCommit<T>(true, ReadOnly(removed));
        }

        public bool TryGet(long identifier, out T? value) =>
            entries.TryGetValue(identifier, out value);

        public bool Replace(T value)
        {
            long key = identity(value);
            if (!entries.ContainsKey(key))
                return false;
            entries[key] = value;
            PublishEntries();
            Touch();
            return true;
        }

        public bool Invalidate()
        {
            fragments.Clear();
            deltas.Clear();
            ExpectedFragments = -1;
            fragment_request_epoch = 0;
            active_request_epoch = 0;
            retired_request_epoch = null;
            restart_on_index_zero = true;
            allow_nonzero_restart = false;
            Loaded = false;
            Loading = false;
            Stale = entries.Count != 0;
            RecoveryPending = false;
            RecoveryRetiredRequestEpoch = 0;
            RecoveryActiveRequestEpoch = 0;
            LoadGeneration = checked(LoadGeneration + 1);
            Touch();
            return true;
        }

        public void Reset()
        {
            entries.Clear();
            fragments.Clear();
            deltas.Clear();
            snapshot = EmptyMap();
            ExpectedFragments = -1;
            fragment_request_epoch = 0;
            active_request_epoch = 0;
            retired_request_epoch = null;
            restart_on_index_zero = false;
            allow_nonzero_restart = false;
            Loaded = false;
            Loading = false;
            Stale = false;
            RecoveryPending = false;
            RecoveryRetiredRequestEpoch = 0;
            RecoveryActiveRequestEpoch = 0;
            LoadGeneration = checked(LoadGeneration + 1);
            Touch();
        }

        private void BeginGeneration(int expected_fragments, long request)
        {
            LoadGeneration = checked(LoadGeneration + 1);
            ExpectedFragments = expected_fragments;
            fragments.Clear();
            fragment_request_epoch = request;
            Loaded = false;
            Loading = true;
            Stale = entries.Count != 0;
        }

        private long TakeRequestEpoch()
        {
            if (retired_request_epoch is not { } retired)
                return active_request_epoch;
            retired_request_epoch = null;
            return retired;
        }

        private void Touch() => SnapshotRevision = checked(SnapshotRevision + 1);

        private void PublishEntries() => snapshot = new ReadOnlyDictionary<long, T>(
            new Dictionary<long, T>(entries));

        private static IReadOnlyDictionary<long, T> EmptyMap() =>
            new ReadOnlyDictionary<long, T>(new Dictionary<long, T>());
    }

    private readonly record struct FragmentCommit(
        bool Accepted,
        bool Loaded,
        long RequestEpoch,
        long LoadGeneration);

    private sealed record UpsertCommit<T>(
        bool Changed,
        IReadOnlyList<T> Added,
        IReadOnlyList<T> Updated);

    private sealed record RemovalCommit<T>(
        bool Changed,
        IReadOnlyList<T> Items);

    private readonly record struct InventoryDelta<T>(long Identifier, T? Value, bool Removed)
    {
        public static InventoryDelta<T> Upsert(long identifier, T value) =>
            new(identifier, value, false);

        public static InventoryDelta<T> Remove(long identifier) =>
            new(identifier, default, true);

        public void Apply(Dictionary<long, T> values)
        {
            if (Removed)
                values.Remove(Identifier);
            else
                values[Identifier] = Value!;
        }
    }
}
