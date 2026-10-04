using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

internal sealed record GiftOfferGiftabilityState(
    bool Value,
    long Revision);

internal sealed record GiftState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long WrappingRevision,
    GiftWrappingConfiguration? Wrapping,
    long ClubInfoRevision,
    ClubGiftInfo? ClubInfo,
    long ClubSelectedRevision,
    ClubGiftSelected? ClubSelected,
    long PresentOpenedRevision,
    PresentOpened? PresentOpened,
    long ReceiverNotFoundRevision,
    long ClubNotificationRevision,
    ClubGiftNotification? ClubNotification,
    long OfferGiftabilityRevision,
    IReadOnlyDictionary<int, GiftOfferGiftabilityState> OfferGiftability,
    long NewUserOfferRevision,
    NuxGiftOffer? NewUserOffer,
    long NewUserIncompleteRevision,
    bool NewUserFlowIncomplete);

internal enum GiftStateChangeKind
{
    Wrapping,
    ClubInfo,
    ClubSelected,
    PresentOpened,
    ReceiverNotFound,
    ClubNotification,
    OfferGiftability,
    NewUserOffer,
    NewUserIncomplete,
    Reset
}

internal sealed record GiftStateUpdate(
    GiftStateChangeKind Kind,
    GiftState State,
    object? Value,
    long PublicationEpoch,
    bool PublishLegacyReset);

/// <summary>
/// Manages gift wrapping, opened presents, club gifts and new user gifts.
/// </summary>
/// <remarks>
/// <para>
/// The state is kept as immutable snapshots. Events are raised after the state is updated and are
/// skipped when the hotel session changes before they are delivered. Requests need the application
/// runtime and throw <see cref="InvalidOperationException"/> until it is active.
/// </para>
/// <para>
/// All members are safe to call from any thread. The state is cleared when the hotel connection
/// closes and when a new connection starts.
/// </para>
/// </remarks>
public sealed class GiftManager : GameStateManager
{
    private const int offer_giftability_limit = 500;
    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<GiftStateUpdate> publications = [];
    private GiftState state = InitialState();
    private IGiftOperations? operations;
    private long committed_generation;
    private long reset_generation = -1;
    private long publication_epoch;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    /// <summary>Gets the gift wrapping configuration, or <see langword="null"/> if it has not been received.</summary>
    public GiftWrappingConfiguration? WrappingConfiguration => State.Wrapping;
    /// <summary>Gets the club gift information, or <see langword="null"/> if it has not been received.</summary>
    public ClubGiftInfo? ClubGifts => State.ClubInfo;
    /// <summary>Gets the last club gift selection the hotel confirmed, or <see langword="null"/> if there is none.</summary>
    public ClubGiftSelected? LastClubGift => State.ClubSelected;
    /// <summary>Gets the contents of the last present the user opened, or <see langword="null"/> if there is none.</summary>
    public PresentOpened? LastOpenedPresent => State.PresentOpened;
    /// <summary>Gets the last club gift notification, or <see langword="null"/> if there is none.</summary>
    public ClubGiftNotification? LatestNotification => State.ClubNotification;
    /// <summary>Gets the new user gift offer, or <see langword="null"/> if it has not been received.</summary>
    public NuxGiftOffer? NewUserOffer => State.NewUserOffer;
    /// <summary>Gets whether the hotel reported that the new user flow is incomplete.</summary>
    public bool NewUserFlowIsIncomplete => State.NewUserFlowIncomplete;

    /// <summary>Gets whether each catalog offer can be sent as a gift, keyed by offer id.</summary>
    /// <remarks>
    /// Holds the answers to <see cref="RequestOfferGiftability(int)"/> for up to 500 offers. The
    /// oldest answer is dropped when a new offer would exceed the limit. Each read returns a new copy.
    /// </remarks>
    public IReadOnlyDictionary<int, bool> OfferGiftability
    {
        get
        {
            Dictionary<int, bool> values = State.OfferGiftability.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Value);
            return new ReadOnlyDictionary<int, bool>(values);
        }
    }

    internal GiftState State => Volatile.Read(ref state);

    /// <summary>Occurs when the gift wrapping configuration arrives; the argument is the configuration.</summary>
    public event Action<GiftWrappingConfiguration>? WrappingConfigurationChanged;
    /// <summary>Occurs when club gift information arrives; the argument is the information.</summary>
    public event Action<ClubGiftInfo>? ClubGiftsChanged;
    /// <summary>Occurs when the hotel confirms a club gift selection; the argument is the selection.</summary>
    public event Action<ClubGiftSelected>? ClubGiftSelectedReceived;
    /// <summary>Occurs when the hotel reports the contents of an opened present; the argument is the contents.</summary>
    public event Action<PresentOpened>? PresentOpenedReceived;
    /// <summary>Occurs when the hotel reports that the receiver of a gift does not exist.</summary>
    public event Action? GiftReceiverNotFound;
    /// <summary>Occurs when a club gift notification arrives; the argument is the notification.</summary>
    public event Action<ClubGiftNotification>? ClubGiftNotificationReceived;
    /// <summary>Occurs when the hotel answers whether an offer can be gifted; the argument is the answer.</summary>
    public event Action<IsOfferGiftable>? OfferGiftabilityChanged;
    /// <summary>Occurs when the new user gift offer arrives; the argument is the offer.</summary>
    public event Action<NuxGiftOffer>? NewUserOfferChanged;
    /// <summary>Occurs when the hotel reports that the new user flow is incomplete.</summary>
    public event Action? NewUserFlowIncomplete;
    /// <summary>Occurs after the gift state has been cleared because the hotel connection closed.</summary>
    public event Action? ResetCompleted;
    internal event Action<GiftStateUpdate>? StateCommitted;
    internal event Action<GiftStateUpdate>? StateChanged;

    /// <inheritdoc/>
    protected override void OnAttach()
    {
        CommitReset(CurrentSession, false);
        OnConnected(BindSession);
        OnIncoming(MessageContracts.Gifts.WrappingConfiguration, ApplyWrapping);
        OnIncoming(MessageContracts.Gifts.PresentOpened, ApplyPresentOpened);
        OnIncoming(MessageContracts.Gifts.ClubInfo, ApplyClubInfo);
        OnIncoming(MessageContracts.Gifts.ClubSelected, ApplyClubSelected);
        OnIncoming(MessageContracts.Gifts.ReceiverNotFound, ApplyReceiverNotFound);
        OnIncoming(MessageContracts.Gifts.ClubNotification, ApplyClubNotification);
        OnIncoming(MessageContracts.Gifts.OfferGiftability, ApplyOfferGiftability);
        OnIncoming(MessageContracts.Gifts.NewUserOffer, ApplyNewUserOffer);
        OnIncoming(MessageContracts.Gifts.NewUserIncomplete, ApplyNewUserIncomplete);
    }

    /// <summary>Requests the gift wrapping configuration from the server.</summary>
    /// <remarks>
    /// The answer updates <see cref="WrappingConfiguration"/> and raises <see cref="WrappingConfigurationChanged"/>.
    /// </remarks>
    public void RequestWrappingConfiguration() =>
        Operations().RequestWrappingConfiguration();

    /// <summary>Opens a present placed in the current room.</summary>
    /// <param name="furniId">The room id of the present.</param>
    /// <remarks>
    /// The user must be in a room that has finished loading. The contents are reported through
    /// <see cref="PresentOpenedReceived"/>.
    /// </remarks>
    public void OpenPresent(Id furniId) => Operations().OpenPresent(furniId);

    /// <summary>Purchases a catalog offer as a gift for another user.</summary>
    /// <param name="request">The purchase, with the receiver, the gift message and the wrapping.</param>
    /// <remarks>
    /// The catalog state must belong to the same hotel session as the gift state.
    /// </remarks>
    public void Purchase(PurchaseFromCatalogAsGift request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Operations().Purchase(request);
    }

    /// <summary>Requests the club gift information from the server.</summary>
    /// <remarks>
    /// The answer updates <see cref="ClubGifts"/> and raises <see cref="ClubGiftsChanged"/>.
    /// </remarks>
    public void RequestClubGifts() => Operations().RequestClubGifts();

    /// <summary>Selects a club gift.</summary>
    /// <param name="productCode">The product code of the club gift.</param>
    public void SelectClubGift(string productCode)
    {
        ArgumentNullException.ThrowIfNull(productCode);
        Operations().SelectClubGift(productCode);
    }

    /// <summary>Requests whether a catalog offer can be sent as a gift.</summary>
    /// <param name="offerId">The id of the catalog offer.</param>
    /// <remarks>
    /// The answer updates <see cref="OfferGiftability"/> and raises <see cref="OfferGiftabilityChanged"/>.
    /// </remarks>
    public void RequestOfferGiftability(int offerId) =>
        Operations().RequestOfferGiftability(offerId);

    /// <summary>Selects gifts from the new user gift offer.</summary>
    /// <param name="selections">The chosen gift for each day and step.</param>
    public void SelectNewUserGifts(IReadOnlyList<NuxGiftSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        Operations().SelectNewUserGifts(selections);
    }

    /// <summary>Advances the new user flow to its next step.</summary>
    public void AdvanceNewUserFlow() => Operations().AdvanceNewUserFlow();

    internal void BindOperations(IGiftOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
                throw new InvalidOperationException("Gift operations are already bound.");
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(IGiftOperations value)
    {
        lock (operations_sync)
        {
            if (ReferenceEquals(operations, value))
                Volatile.Write(ref operations, null);
        }
    }

    /// <inheritdoc/>
    protected override void Reset() => CommitReset(CurrentSession, true);

    private void BindSession(Session session) => CommitReset(session, false);

    private void ApplyWrapping(
        GiftWrappingConfiguration message,
        long state_generation)
    {
        GiftWrappingConfiguration snapshot = FreezeWrapping(message);
        Store(
            state_generation,
            GiftStateChangeKind.Wrapping,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                WrappingRevision = checked(current.WrappingRevision + 1),
                Wrapping = snapshot
            });
    }

    private void ApplyPresentOpened(PresentOpened message, long state_generation)
    {
        PresentOpened snapshot = message with { };
        Store(
            state_generation,
            GiftStateChangeKind.PresentOpened,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                PresentOpenedRevision = checked(current.PresentOpenedRevision + 1),
                PresentOpened = snapshot
            });
    }

    private void ApplyClubInfo(ClubGiftInfo message, long state_generation)
    {
        ClubGiftInfo snapshot = FreezeClubInfo(message);
        Store(
            state_generation,
            GiftStateChangeKind.ClubInfo,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                ClubInfoRevision = checked(current.ClubInfoRevision + 1),
                ClubInfo = snapshot
            });
    }

    private void ApplyClubSelected(ClubGiftSelected message, long state_generation)
    {
        ClubGiftSelected snapshot = FreezeClubSelected(message);
        Store(
            state_generation,
            GiftStateChangeKind.ClubSelected,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                ClubSelectedRevision = checked(current.ClubSelectedRevision + 1),
                ClubSelected = snapshot
            });
    }

    private void ApplyReceiverNotFound(
        GiftReceiverNotFound message,
        long state_generation) =>
        Store(
            state_generation,
            GiftStateChangeKind.ReceiverNotFound,
            message,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                ReceiverNotFoundRevision = checked(current.ReceiverNotFoundRevision + 1)
            });

    private void ApplyClubNotification(
        ClubGiftNotification message,
        long state_generation)
    {
        ClubGiftNotification snapshot = message with { };
        Store(
            state_generation,
            GiftStateChangeKind.ClubNotification,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                ClubNotificationRevision = checked(current.ClubNotificationRevision + 1),
                ClubNotification = snapshot
            });
    }

    private void ApplyOfferGiftability(
        IsOfferGiftable message,
        long state_generation)
    {
        var snapshot = new IsOfferGiftable(message.OfferId, message.IsGiftable);
        Store(
            state_generation,
            GiftStateChangeKind.OfferGiftability,
            snapshot,
            current =>
            {
                long next_revision = checked(current.OfferGiftabilityRevision + 1);
                var values = new Dictionary<int, GiftOfferGiftabilityState>(
                    current.OfferGiftability);
                if (!values.ContainsKey(snapshot.OfferId) &&
                    values.Count >= offer_giftability_limit)
                {
                    int oldest = values
                        .OrderBy(entry => entry.Value.Revision)
                        .ThenBy(entry => entry.Key)
                        .First()
                        .Key;
                    values.Remove(oldest);
                }
                values[snapshot.OfferId] = new GiftOfferGiftabilityState(
                    snapshot.IsGiftable,
                    next_revision);
                return current with
                {
                    Revision = checked(current.Revision + 1),
                    OfferGiftabilityRevision = next_revision,
                    OfferGiftability = FreezeGiftability(values)
                };
            });
    }

    private void ApplyNewUserOffer(NuxGiftOffer message, long state_generation)
    {
        NuxGiftOffer snapshot = FreezeNewUserOffer(message);
        Store(
            state_generation,
            GiftStateChangeKind.NewUserOffer,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                NewUserOfferRevision = checked(current.NewUserOfferRevision + 1),
                NewUserOffer = snapshot
            });
    }

    private void ApplyNewUserIncomplete(
        NuxNotComplete message,
        long state_generation) =>
        Store(
            state_generation,
            GiftStateChangeKind.NewUserIncomplete,
            message,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                NewUserIncompleteRevision = checked(current.NewUserIncompleteRevision + 1),
                NewUserFlowIncomplete = true
            });

    private void Store(
        long state_generation,
        GiftStateChangeKind kind,
        object? value,
        Func<GiftState, GiftState> mutation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            GiftStateUpdate update;
            lock (state_sync)
            {
                GiftState current = state;
                if (state_generation != committed_generation ||
                    current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                GiftState updated = mutation(current);
                update = null!;
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new GiftStateUpdate(
                            kind,
                            updated,
                            value,
                            publication_epoch,
                            false);
                    }))
                {
                    return;
                }
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = null;
        if (drain)
        {
            try
            {
                DrainPublications();
            }
            catch (Exception error)
            {
                publication_failure = error;
            }
        }
        ThrowFailures(committed_failure, publication_failure);
    }

    private void CommitReset(Session? active_session, bool publish_legacy_reset)
    {
        long state_generation = CurrentStateGeneration;
        int thread_id = Environment.CurrentManagedThreadId;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            while (delivering && delivery_thread_id != thread_id)
                Monitor.Wait(publication_sync);
            GiftStateUpdate update;
            lock (state_sync)
            {
                GiftState current = state;
                if (state_generation < committed_generation ||
                    state_generation == reset_generation &&
                    ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                var updated = new GiftState(
                    active_session,
                    state_generation,
                    checked(current.Revision + 1),
                    checked(current.WrappingRevision + 1),
                    null,
                    checked(current.ClubInfoRevision + 1),
                    null,
                    checked(current.ClubSelectedRevision + 1),
                    null,
                    checked(current.PresentOpenedRevision + 1),
                    null,
                    checked(current.ReceiverNotFoundRevision + 1),
                    checked(current.ClubNotificationRevision + 1),
                    null,
                    checked(current.OfferGiftabilityRevision + 1),
                    EmptyGiftability(),
                    checked(current.NewUserOfferRevision + 1),
                    null,
                    checked(current.NewUserIncompleteRevision + 1),
                    false);
                Volatile.Write(ref state, updated);
                committed_generation = state_generation;
                reset_generation = state_generation;
                publication_epoch = checked(publication_epoch + 1);
                update = new GiftStateUpdate(
                    GiftStateChangeKind.Reset,
                    updated,
                    null,
                    publication_epoch,
                    publish_legacy_reset);
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = null;
        if (drain)
        {
            try
            {
                DrainPublications();
            }
            catch (Exception error)
            {
                publication_failure = error;
            }
        }
        ThrowFailures(committed_failure, publication_failure);
    }

    private void DrainPublications()
    {
        Exception? failure = null;
        while (true)
        {
            GiftStateUpdate update;
            lock (publication_sync)
            {
                if (!publications.TryDequeue(out update!))
                {
                    publishing = false;
                    break;
                }
                delivering = true;
                delivery_thread_id = Environment.CurrentManagedThreadId;
            }
            try
            {
                if (!UpdateCurrent(update))
                    continue;
                failure = Notify(StateChanged, update, update, failure);
                if (!UpdateCurrent(update))
                    continue;
                failure = NotifyLegacy(update, failure);
            }
            finally
            {
                lock (publication_sync)
                {
                    delivering = false;
                    delivery_thread_id = 0;
                    Monitor.PulseAll(publication_sync);
                }
            }
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal bool IsCurrentPublication(GiftStateUpdate update) => UpdateCurrent(update);

    private bool UpdateCurrent(GiftStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            GiftState current = State;
            if (current.SessionGeneration != update.State.SessionGeneration ||
                !ReferenceEquals(current.Session, update.State.Session))
            {
                return false;
            }
        }
        long before = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        long after = CurrentStateGeneration;
        return before == update.State.SessionGeneration &&
            after == update.State.SessionGeneration &&
            ReferenceEquals(active_session, update.State.Session);
    }

    private Exception? NotifyCommitted(GiftStateUpdate update)
    {
        Exception? failure = null;
        Action<GiftStateUpdate>? listeners = StateCommitted;
        if (listeners is null)
            return null;
        foreach (Action<GiftStateUpdate> listener in listeners
            .GetInvocationList()
            .Cast<Action<GiftStateUpdate>>())
        {
            try
            {
                listener(update);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private Exception? NotifyLegacy(GiftStateUpdate update, Exception? failure) =>
        update.Kind switch
        {
            GiftStateChangeKind.Wrapping => Notify(
                WrappingConfigurationChanged,
                (GiftWrappingConfiguration)update.Value!,
                update,
                failure),
            GiftStateChangeKind.ClubInfo => Notify(
                ClubGiftsChanged,
                (ClubGiftInfo)update.Value!,
                update,
                failure),
            GiftStateChangeKind.ClubSelected => Notify(
                ClubGiftSelectedReceived,
                (ClubGiftSelected)update.Value!,
                update,
                failure),
            GiftStateChangeKind.PresentOpened => Notify(
                PresentOpenedReceived,
                (PresentOpened)update.Value!,
                update,
                failure),
            GiftStateChangeKind.ReceiverNotFound => Notify(
                GiftReceiverNotFound,
                update,
                failure),
            GiftStateChangeKind.ClubNotification => Notify(
                ClubGiftNotificationReceived,
                (ClubGiftNotification)update.Value!,
                update,
                failure),
            GiftStateChangeKind.OfferGiftability => Notify(
                OfferGiftabilityChanged,
                (IsOfferGiftable)update.Value!,
                update,
                failure),
            GiftStateChangeKind.NewUserOffer => Notify(
                NewUserOfferChanged,
                (NuxGiftOffer)update.Value!,
                update,
                failure),
            GiftStateChangeKind.NewUserIncomplete => Notify(
                NewUserFlowIncomplete,
                update,
                failure),
            GiftStateChangeKind.Reset when update.PublishLegacyReset => Notify(
                ResetCompleted,
                update,
                failure),
            GiftStateChangeKind.Reset => failure,
            _ => throw new ArgumentOutOfRangeException(nameof(update))
        };

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        GiftStateUpdate update,
        Exception? failure)
    {
        if (listeners is null)
            return failure;
        foreach (Action<T> listener in listeners.GetInvocationList().Cast<Action<T>>())
        {
            if (!UpdateCurrent(update))
                break;
            try
            {
                listener(value);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private Exception? Notify(
        Action? listeners,
        GiftStateUpdate update,
        Exception? failure)
    {
        if (listeners is null)
            return failure;
        foreach (Action listener in listeners.GetInvocationList().Cast<Action>())
        {
            if (!UpdateCurrent(update))
                break;
            try
            {
                listener();
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private static void ThrowFailures(Exception? first, Exception? second)
    {
        if (first is not null && second is not null)
            throw new AggregateException(first, second);
        if (first is not null)
            ExceptionDispatchInfo.Capture(first).Throw();
        if (second is not null)
            ExceptionDispatchInfo.Capture(second).Throw();
    }

    private IGiftOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Gift operations are unavailable until the application runtime is active.");

    private static GiftState InitialState() => new(
        null,
        0,
        0,
        0,
        null,
        0,
        null,
        0,
        null,
        0,
        null,
        0,
        0,
        null,
        0,
        EmptyGiftability(),
        0,
        null,
        0,
        false);

    private static GiftWrappingConfiguration FreezeWrapping(
        GiftWrappingConfiguration value) => new(
        value.IsWrappingEnabled,
        value.WrappingPrice,
        value.StuffTypes,
        value.BoxTypes,
        value.RibbonTypes,
        value.DefaultStuffTypes);

    private static ClubGiftInfo FreezeClubInfo(ClubGiftInfo value) => new(
        value.DaysUntilNextGift,
        value.GiftsAvailable,
        value.Offers.Select(FreezeOffer).ToArray(),
        value.GiftEligibility.Select(entry => entry with { }).ToArray());

    private static CatalogPageOffer FreezeOffer(CatalogPageOffer value) => new(
        value.OfferId,
        value.LocalizationId,
        value.IsRent,
        value.PriceInCredits,
        value.PriceInActivityPoints,
        value.ActivityPointType,
        value.PriceInSilver,
        value.Giftable,
        value.Products.Select(product => product with { }).ToArray(),
        value.ClubLevel,
        value.BundlePurchaseAllowed,
        value.IsPet,
        value.PreviewImage);

    private static ClubGiftSelected FreezeClubSelected(ClubGiftSelected value) => new(
        value.ProductCode,
        value.Products.Select(product => product with { }).ToArray());

    private static NuxGiftOffer FreezeNewUserOffer(NuxGiftOffer value) => new(
        value.Steps.Select(step => new NuxGiftStep(
            step.DayIndex,
            step.StepIndex,
            step.Options.Select(option => new NuxGiftOption(
                option.ThumbnailUrl,
                option.Products.Select(product => product with { }).ToArray())).ToArray())).ToArray());

    private static IReadOnlyDictionary<int, GiftOfferGiftabilityState>
        FreezeGiftability(Dictionary<int, GiftOfferGiftabilityState> values) =>
        new ReadOnlyDictionary<int, GiftOfferGiftabilityState>(values);

    private static IReadOnlyDictionary<int, GiftOfferGiftabilityState>
        EmptyGiftability() =>
        FreezeGiftability(new Dictionary<int, GiftOfferGiftabilityState>());
}
