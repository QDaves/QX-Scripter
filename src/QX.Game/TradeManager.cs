using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

internal sealed record TradeItemState(
    Id ItemId,
    ItemType Type,
    Id Id,
    int Kind,
    int Category,
    bool IsGroupable,
    ItemDataSnapshot Data,
    int CreationDay,
    int CreationMonth,
    int CreationYear,
    long Extra);

internal sealed record TradeOfferState(
    Id UserId,
    int FurniCount,
    int CreditCount,
    IReadOnlyList<TradeItemState> Items);

internal sealed record TradeNftAssetState(
    long AssetId,
    short ProductTypeId,
    string ItemTypeId,
    int Score,
    string PetFigureString,
    IReadOnlyList<int> FigureSetIds,
    string ProductCode,
    string Rarity);

internal sealed record TradeNftOffersState(
    IReadOnlyList<TradeNftAssetState> OwnAssets,
    IReadOnlyList<TradeNftAssetState> OtherAssets);

internal sealed record TradeParticipantState(
    Id UserId,
    bool CanTrade,
    bool Accepted);

internal sealed record TradeEpochState(
    long Epoch,
    TradePhase Phase,
    TradeParticipantState FirstParticipant,
    TradeParticipantState SecondParticipant,
    TradeOfferState? FirstOffer,
    TradeOfferState? SecondOffer,
    TradeNftOffersState? NftOffers,
    int OwnSilver,
    int OtherSilver,
    int SilverFee);

internal sealed record TradeNftInventoryState(
    long Revision,
    bool Loaded,
    IReadOnlyList<TradeNftAssetState> Assets);

internal sealed record TradeState(
    long Generation,
    long Revision,
    Session? Session,
    long Epoch,
    TradeEpochState? Active,
    TradeNftInventoryState NftInventory);

internal enum TradeStateChangeKind
{
    Opened,
    OffersUpdated,
    AcceptanceUpdated,
    Confirmation,
    Completed,
    Closed,
    OpenFailed,
    NftOffersUpdated,
    SilverUpdated,
    SilverFeeUpdated,
    NftInventoryUpdated,
    RoomChanged,
    Reset
}

internal sealed record TradeAcceptanceCommit(Id UserId, bool Accepted);

internal sealed record TradeCloseCommit(Id UserId, int Reason);

internal sealed record TradeOpenFailureCommit(int Reason, string OtherUserName);

internal sealed record TradeStateUpdate(
    TradeStateChangeKind Kind,
    TradeState State,
    object? Value,
    TradeEpochState? PreviousEpoch);

internal sealed class TradeManager : GameStateManager
{
    private readonly object publication_sync = new();
    private readonly Queue<TradeStateUpdate> publications = [];
    private TradeState state = new(
        0,
        0,
        null,
        0,
        null,
        new TradeNftInventoryState(0, false, ReadOnly<TradeNftAssetState>([])));
    private long reset_generation = -1;
    private long room_generation = -1;
    private bool publishing;

    internal TradeState State => Volatile.Read(ref state);
    internal event Action<TradeStateUpdate>? StateCommitted;
    internal event Action<TradeStateUpdate>? StateChanged;
    internal Func<long>? RoomGeneration { get; set; }

    protected override void OnAttach()
    {
        Reset();
        OnConnected(session => CommitReset(CurrentStateGeneration, session));
        OnIncoming(MessageContracts.Trade.Opened, ApplyOpened);
        OnIncoming(MessageContracts.Trade.Offers, ApplyOffers);
        OnIncoming(MessageContracts.Trade.AcceptanceUpdated, ApplyAcceptance);
        OnIncoming(MessageContracts.Trade.Confirmation, ApplyConfirmation);
        OnIncoming(MessageContracts.Trade.Completed, ApplyCompleted);
        OnIncoming(MessageContracts.Trade.Closed, ApplyClosed);
        OnIncoming(MessageContracts.Trade.OpenFailed, ApplyOpenFailure);
        OnIncoming(ClientType.Flash, MessageContracts.Trade.NftOffers, ApplyNftOffers);
        OnIncoming(ClientType.Flash, MessageContracts.Trade.NftInventory, ApplyNftInventory);
        OnIncoming(ClientType.Flash, MessageContracts.Trade.SilverUpdated, ApplySilver);
        OnIncoming(ClientType.Flash, MessageContracts.Trade.SilverFee, ApplySilverFee);
    }

    protected override void Reset() =>
        CommitReset(CurrentStateGeneration, CurrentSession);

    internal void EnterRoom(Id _) => InvalidateRoom();

    internal void LeaveRoom() => InvalidateRoom();

    internal static bool EquivalentNftInventory(
        TradeNftInventoryState state,
        TradeNftAssetInventory message)
    {
        if (state.Assets.Count != message.Assets.Count)
            return false;
        for (int index = 0; index < state.Assets.Count; index++)
        {
            if (!Equivalent(state.Assets[index], message.Assets[index]))
                return false;
        }
        return true;
    }

    private void ApplyOpened(TradeOpened message, long generation) => Store(
        generation,
        TradeStateChangeKind.Opened,
        current =>
        {
            long epoch = checked(current.Epoch + 1);
            var active = new TradeEpochState(
                epoch,
                TradePhase.Trading,
                new TradeParticipantState(message.UserId, message.UserCanTrade, false),
                new TradeParticipantState(message.OtherUserId, message.OtherUserCanTrade, false),
                null,
                null,
                null,
                0,
                0,
                0);
            return new TradeMutation(
                current with { Epoch = epoch, Active = active },
                null,
                current.Active);
        });

    private void ApplyOffers(TradeOffers message, long generation)
    {
        TradeOfferState first = SnapshotOf(message.First);
        TradeOfferState second = SnapshotOf(message.Second);
        Store(
            generation,
            TradeStateChangeKind.OffersUpdated,
            current =>
            {
                if (current.Active is not { } active || !ParticipantsMatch(active, first.UserId, second.UserId))
                    return null;
                TradeOfferState first_offer = first.UserId == active.FirstParticipant.UserId
                    ? first
                    : second;
                TradeOfferState second_offer = second.UserId == active.SecondParticipant.UserId
                    ? second
                    : first;
                TradeEpochState updated = ResetAcceptance(active) with
                {
                    Phase = TradePhase.Trading,
                    FirstOffer = first_offer,
                    SecondOffer = second_offer
                };
                return new TradeMutation(current with { Active = updated });
            });
    }

    private void ApplyAcceptance(TradeAccepted message, long generation) => Store(
        generation,
        TradeStateChangeKind.AcceptanceUpdated,
        current =>
        {
            if (current.Active is not { } active || !Participant(active, message.UserId))
                return null;
            TradeParticipantState first = active.FirstParticipant.UserId == message.UserId
                ? active.FirstParticipant with { Accepted = message.Accepted }
                : active.FirstParticipant;
            TradeParticipantState second = active.SecondParticipant.UserId == message.UserId
                ? active.SecondParticipant with { Accepted = message.Accepted }
                : active.SecondParticipant;
            TradePhase phase = !message.Accepted && active.Phase is TradePhase.AwaitingConfirmation
                ? TradePhase.Trading
                : active.Phase;
            return new TradeMutation(
                current with
                {
                    Active = active with
                    {
                        Phase = phase,
                        FirstParticipant = first,
                        SecondParticipant = second
                    }
                },
                new TradeAcceptanceCommit(message.UserId, message.Accepted));
        });

    private void ApplyConfirmation(TradeConfirmation _, long generation) => Store(
        generation,
        TradeStateChangeKind.Confirmation,
        current => current.Active is not { } active
            ? null
            : new TradeMutation(
                current with
                {
                    Active = active with { Phase = TradePhase.AwaitingConfirmation }
                }));

    private void ApplyCompleted(TradeCompleted _, long generation) => Store(
        generation,
        TradeStateChangeKind.Completed,
        current => current.Active is not { } active
            ? null
            : new TradeMutation(
                current with { Active = null },
                null,
                active));

    private void ApplyClosed(TradeClosed message, long generation) => Store(
        generation,
        TradeStateChangeKind.Closed,
        current => current.Active is not { } active || !Participant(active, message.UserId)
            ? null
            : new TradeMutation(
                current with { Active = null },
                new TradeCloseCommit(message.UserId, message.Reason),
                active));

    private void ApplyOpenFailure(TradeOpenFailed message, long generation) => Store(
        generation,
        TradeStateChangeKind.OpenFailed,
        current => new TradeMutation(
            current,
            new TradeOpenFailureCommit(message.Reason, message.OtherUserName)));

    private void ApplyNftOffers(TradeNftAssets message, long generation)
    {
        TradeNftOffersState offers = new(
            ReadOnly(message.OwnAssets.Select(SnapshotOf)),
            ReadOnly(message.OtherAssets.Select(SnapshotOf)));
        Store(
            generation,
            TradeStateChangeKind.NftOffersUpdated,
            current =>
            {
                if (current.Active is not { } active)
                    return null;
                TradeEpochState updated = ResetAcceptance(active) with
                {
                    Phase = TradePhase.Trading,
                    NftOffers = offers
                };
                return new TradeMutation(current with { Active = updated });
            });
    }

    private void ApplyNftInventory(TradeNftAssetInventory message, long generation)
    {
        IReadOnlyList<TradeNftAssetState> assets = ReadOnly(message.Assets.Select(SnapshotOf));
        Store(
            generation,
            TradeStateChangeKind.NftInventoryUpdated,
            current => new TradeMutation(
                current with
                {
                    NftInventory = new TradeNftInventoryState(
                        checked(current.NftInventory.Revision + 1),
                        true,
                        assets)
                }));
    }

    private void ApplySilver(TradeSilverSet message, long generation) => Store(
        generation,
        TradeStateChangeKind.SilverUpdated,
        current => current.Active is not { } active
            ? null
            : new TradeMutation(
                current with
                {
                    Active = active with
                    {
                        OwnSilver = message.OwnSilver,
                        OtherSilver = message.OtherSilver
                    }
                }));

    private void ApplySilverFee(TradeSilverFee message, long generation) => Store(
        generation,
        TradeStateChangeKind.SilverFeeUpdated,
        current => current.Active is not { } active
            ? null
            : new TradeMutation(
                current with
                {
                    Active = active with { SilverFee = message.SilverFee }
                }));

    private void InvalidateRoom()
    {
        long current_room_generation = RoomGeneration?.Invoke() ?? -1;
        Store(
            CurrentStateGeneration,
            TradeStateChangeKind.RoomChanged,
            current =>
            {
                if (current_room_generation <= room_generation)
                    return null;
                room_generation = current_room_generation;
                if (current.Active is null)
                    return null;
                return new TradeMutation(
                    current with
                    {
                        Epoch = checked(current.Epoch + 1),
                        Active = null
                    },
                    null,
                    current.Active);
            });
    }

    private void Store(
        long generation,
        TradeStateChangeKind kind,
        Func<TradeState, TradeMutation?> mutation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        lock (publication_sync)
        {
            TradeState current = state;
            if (generation < current.Generation)
                return;
            if (generation != current.Generation || !ReferenceEquals(current.Session, active_session))
            {
                current = current with
                {
                    Generation = generation,
                    Session = active_session,
                    Active = null,
                    NftInventory = new TradeNftInventoryState(
                        checked(current.NftInventory.Revision + 1),
                        false,
                        ReadOnly<TradeNftAssetState>([]))
                };
            }
            TradeMutation? mutation_result = mutation(current);
            if (mutation_result is null)
                return;
            TradeState updated = mutation_result.State with
            {
                Generation = generation,
                Revision = checked(current.Revision + 1),
                Session = active_session
            };
            Volatile.Write(ref state, updated);
            reset_generation = -1;
            var update = new TradeStateUpdate(
                kind,
                updated,
                mutation_result.Value,
                mutation_result.PreviousEpoch);
            StateCommitted?.Invoke(update);
            drain = Enqueue(update);
        }
        if (drain)
            DrainPublications();
    }

    private void CommitReset(long generation, Session? active_session)
    {
        bool drain;
        lock (publication_sync)
        {
            TradeState current = state;
            if (generation < current.Generation ||
                generation == reset_generation && ReferenceEquals(current.Session, active_session))
            {
                return;
            }
            TradeState updated = current with
            {
                Generation = generation,
                Revision = checked(current.Revision + 1),
                Session = active_session,
                Active = null,
                NftInventory = new TradeNftInventoryState(
                    checked(current.NftInventory.Revision + 1),
                    false,
                    ReadOnly<TradeNftAssetState>([]))
            };
            Volatile.Write(ref state, updated);
            reset_generation = generation;
            var update = new TradeStateUpdate(
                TradeStateChangeKind.Reset,
                updated,
                null,
                current.Active);
            StateCommitted?.Invoke(update);
            drain = Enqueue(update);
        }
        if (drain)
            DrainPublications();
    }

    private bool Enqueue(TradeStateUpdate update)
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
            TradeStateUpdate update;
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

    private static TradeEpochState ResetAcceptance(TradeEpochState value) => value with
    {
        FirstParticipant = value.FirstParticipant with { Accepted = false },
        SecondParticipant = value.SecondParticipant with { Accepted = false }
    };

    private static bool Participant(TradeEpochState active, Id user_id) =>
        active.FirstParticipant.UserId == user_id || active.SecondParticipant.UserId == user_id;

    private static bool ParticipantsMatch(TradeEpochState active, Id first, Id second) =>
        first != second &&
        Participant(active, first) &&
        Participant(active, second);

    private static TradeOfferState SnapshotOf(TradeOffer value) => new(
        value.UserId,
        value.FurniCount,
        value.CreditCount,
        ReadOnly(value.Items.Select(SnapshotOf)));

    private static TradeItemState SnapshotOf(TradeItem value) => new(
        value.ItemId,
        value.Type,
        value.Id,
        value.Kind,
        value.Category,
        value.IsGroupable,
        SnapshotOf(value.Data),
        value.CreationDay,
        value.CreationMonth,
        value.CreationYear,
        value.Extra);

    private static ItemDataSnapshot SnapshotOf(ItemData value)
    {
        ItemDataSnapshot data = SnapshotFactory.From(value);
        return data with
        {
            MapEntries = data.MapEntries is null
                ? null
                : new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(data.MapEntries, StringComparer.Ordinal)),
            StringValues = data.StringValues is null ? null : ReadOnly(data.StringValues),
            IntValues = data.IntValues is null ? null : ReadOnly(data.IntValues),
            HighScores = data.HighScores is null
                ? null
                : ReadOnly(data.HighScores.Select(score => score with
                {
                    Names = ReadOnly(score.Names)
                }))
        };
    }

    private static TradeNftAssetState SnapshotOf(TradeNftAsset value) => new(
        value.AssetId,
        value.ProductTypeId,
        value.ItemTypeId,
        value.Score,
        value.PetFigureString,
        ReadOnly(value.FigureSetIds),
        value.ProductCode,
        value.Rarity);

    private static bool Equivalent(TradeNftAssetState left, TradeNftAsset right)
    {
        if (left.AssetId != right.AssetId ||
            left.ProductTypeId != right.ProductTypeId ||
            left.ItemTypeId != right.ItemTypeId ||
            left.Score != right.Score ||
            left.PetFigureString != right.PetFigureString ||
            left.ProductCode != right.ProductCode ||
            left.Rarity != right.Rarity ||
            left.FigureSetIds.Count != right.FigureSetIds.Count)
        {
            return false;
        }
        for (int index = 0; index < left.FigureSetIds.Count; index++)
        {
            if (left.FigureSetIds[index] != right.FigureSetIds[index])
                return false;
        }
        return true;
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private sealed record TradeMutation(
        TradeState State,
        object? Value = null,
        TradeEpochState? PreviousEpoch = null);
}
