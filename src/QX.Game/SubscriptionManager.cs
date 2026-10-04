using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

internal sealed record SubscriptionUserInfoState(
    ScrSendUserInfo Value,
    long Revision);

internal sealed record SubscriptionState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long UserInfoRevision,
    IReadOnlyDictionary<string, SubscriptionUserInfoState> UserInfo,
    long KickbackRevision,
    ScrSendKickbackInfo? KickbackInfo,
    long BuildersClubFurniCountRevision,
    BuildersClubFurniCount? BuildersClubFurniCount,
    long BuildersClubMembershipRevision,
    BuildersClubMembershipStatus? BuildersClubStatus,
    long BuildersClubPlacementWarningRevision,
    BuildersClubPlacementWarning? LastPlacementWarning,
    long ClubOffersRevision,
    HabboClubOffers? ClubOffers);

internal enum SubscriptionStateChangeKind
{
    UserInfo,
    KickbackInfo,
    BuildersClubFurniCount,
    BuildersClubMembershipStatus,
    BuildersClubPlacementWarning,
    Reset,
    ClubOffers
}

internal sealed record SubscriptionStateUpdate(
    SubscriptionStateChangeKind Kind,
    SubscriptionState State,
    object? Value,
    long PublicationEpoch,
    bool PublishLegacyReset);

/// <summary>
/// Manages the subscription state of the current session.
/// </summary>
/// <remarks>
/// <para>
/// Tracks the subscription info per product, the club kickback info and the Builders Club furni
/// count, membership status and last placement warning.
/// </para>
/// <para>
/// The state is cleared when the hotel connection closes and when a new hotel session connects.
/// Events are raised after the state is updated and are skipped once the session they belong to has
/// ended. All members are safe to call from any thread.
/// </para>
/// </remarks>
public sealed class SubscriptionManager : GameStateManager
{
    private const int user_info_limit = 500;
    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<SubscriptionStateUpdate> publications = [];
    private SubscriptionState state = InitialState();
    private ISubscriptionOperations? operations;
    private long committed_generation;
    private long reset_generation = -1;
    private long publication_epoch;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    /// <summary>Gets the last subscription info received for each product, keyed by product name.</summary>
    /// <remarks>
    /// The keys are compared without regard to case. At most 500 products are kept; when a new product
    /// arrives at the limit, the least recently updated one is dropped. The dictionary is a copy.
    /// </remarks>
    public IReadOnlyDictionary<string, ScrSendUserInfo> UserInfo
    {
        get
        {
            SubscriptionState current = State;
            var values = current.UserInfo.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Value,
                StringComparer.OrdinalIgnoreCase);
            return new ReadOnlyDictionary<string, ScrSendUserInfo>(values);
        }
    }

    /// <summary>Gets the last club kickback info the server sent, or <see langword="null"/> if none was received.</summary>
    public ScrSendKickbackInfo? KickbackInfo => State.KickbackInfo;
    /// <summary>Gets the last Builders Club furni count the server sent, or <see langword="null"/> if none was received.</summary>
    public BuildersClubFurniCount? BuildersClubFurniCount =>
        State.BuildersClubFurniCount;
    /// <summary>Gets the last Builders Club membership status the server sent, or <see langword="null"/> if none was received.</summary>
    public BuildersClubMembershipStatus? BuildersClubStatus =>
        State.BuildersClubStatus;
    /// <summary>Gets the last Builders Club placement warning the server sent, or <see langword="null"/> if none was received.</summary>
    public BuildersClubPlacementWarning? LastPlacementWarning =>
        State.LastPlacementWarning;

    internal SubscriptionState State => Volatile.Read(ref state);

    /// <summary>Occurs when the server sends subscription info for a product, with the received info.</summary>
    public event Action<ScrSendUserInfo>? UserInfoChanged;
    /// <summary>Occurs when the server sends the club kickback info, with the received info.</summary>
    public event Action<ScrSendKickbackInfo>? KickbackInfoChanged;
    /// <summary>Occurs when the server sends the Builders Club furni count, with the received count.</summary>
    public event Action<BuildersClubFurniCount>? BuildersClubFurniCountChanged;
    /// <summary>Occurs when the server sends the Builders Club membership status, with the received status.</summary>
    public event Action<BuildersClubMembershipStatus>? BuildersClubStatusChanged;
    /// <summary>Occurs when the server sends a Builders Club placement warning, with the received warning.</summary>
    public event Action<BuildersClubPlacementWarning>? PlacementWarningReceived;
    /// <summary>Occurs after the subscription state is cleared when the hotel connection closes.</summary>
    /// <remarks>Not raised when the state is cleared for a newly connected session.</remarks>
    public event Action? ResetCompleted;
    internal event Action<SubscriptionStateUpdate>? StateCommitted;
    internal event Action<SubscriptionStateUpdate>? StateChanged;

    /// <summary>Gets the last subscription info received for a product.</summary>
    /// <param name="productName">The product name, compared without regard to case.</param>
    /// <returns>The subscription info, or <see langword="null"/> if none was received for the product.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="productName"/> is <see langword="null"/>.</exception>
    public ScrSendUserInfo? FindUserInfo(string productName)
    {
        ArgumentNullException.ThrowIfNull(productName);
        return State.UserInfo.TryGetValue(productName, out SubscriptionUserInfoState? entry)
            ? entry.Value
            : null;
    }

    /// <inheritdoc/>
    protected override void OnAttach()
    {
        CommitReset(CurrentSession, false);
        OnConnected(BindSession);
        OnIncoming(MessageContracts.Subscriptions.UserInfo, ApplyUserInfo);
        OnIncoming(MessageContracts.Subscriptions.KickbackInfo, ApplyKickbackInfo);
        OnIncoming(MessageContracts.Subscriptions.ClubOffersSnapshot, ApplyClubOffers);
        OnIncoming(
            MessageContracts.Subscriptions.BuildersClubFurniCount,
            ApplyBuildersClubFurniCount);
        OnIncoming(
            MessageContracts.Subscriptions.BuildersClubMembershipStatus,
            ApplyBuildersClubMembershipStatus);
        OnIncoming(
            MessageContracts.Subscriptions.BuildersClubPlacementWarning,
            ApplyBuildersClubPlacementWarning);
    }

    /// <summary>Requests the subscription info for a product from the server.</summary>
    /// <param name="productName">The product name.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="productName"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the subscription operations are not bound yet.</exception>
    public void RequestUserInfo(string productName)
    {
        ArgumentNullException.ThrowIfNull(productName);
        Operations().RequestUserInfo(productName);
    }

    /// <summary>Requests the club kickback info from the server.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the subscription operations are not bound yet.</exception>
    public void RequestKickbackInfo() => Operations().RequestKickbackInfo();

    /// <summary>Requests the Builders Club furni count from the server.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the subscription operations are not bound yet.</exception>
    public void RequestBuildersClubFurniCount() =>
        Operations().RequestBuildersClubFurniCount();

    internal void BindOperations(ISubscriptionOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
                throw new InvalidOperationException("Subscription operations are already bound.");
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(ISubscriptionOperations value)
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

    private void ApplyUserInfo(ScrSendUserInfo message, long state_generation) =>
        Store(
            state_generation,
            SubscriptionStateChangeKind.UserInfo,
            message,
            current =>
            {
                long next_revision = checked(current.UserInfoRevision + 1);
                var values = new Dictionary<string, SubscriptionUserInfoState>(
                    current.UserInfo,
                    StringComparer.OrdinalIgnoreCase);
                if (!values.ContainsKey(message.ProductName) &&
                    values.Count >= user_info_limit)
                {
                    string oldest = values
                        .OrderBy(entry => entry.Value.Revision)
                        .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                        .First()
                        .Key;
                    values.Remove(oldest);
                }
                values[message.ProductName] = new SubscriptionUserInfoState(
                    message,
                    next_revision);
                return current with
                {
                    Revision = checked(current.Revision + 1),
                    UserInfoRevision = next_revision,
                    UserInfo = Freeze(values)
                };
            });

    private void ApplyKickbackInfo(
        ScrSendKickbackInfo message,
        long state_generation) =>
        Store(
            state_generation,
            SubscriptionStateChangeKind.KickbackInfo,
            message,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                KickbackRevision = checked(current.KickbackRevision + 1),
                KickbackInfo = message
            });

    private void ApplyClubOffers(
        HabboClubOffers message,
        long state_generation)
    {
        HabboClubOffers snapshot = FreezeClubOffers(message);
        Store(
            state_generation,
            SubscriptionStateChangeKind.ClubOffers,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                ClubOffersRevision = checked(current.ClubOffersRevision + 1),
                ClubOffers = snapshot
            });
    }

    private void ApplyBuildersClubFurniCount(
        BuildersClubFurniCount message,
        long state_generation) =>
        Store(
            state_generation,
            SubscriptionStateChangeKind.BuildersClubFurniCount,
            message,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                BuildersClubFurniCountRevision = checked(
                    current.BuildersClubFurniCountRevision + 1),
                BuildersClubFurniCount = message
            });

    private void ApplyBuildersClubMembershipStatus(
        BuildersClubMembershipStatus message,
        long state_generation) =>
        Store(
            state_generation,
            SubscriptionStateChangeKind.BuildersClubMembershipStatus,
            message,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                BuildersClubMembershipRevision = checked(
                    current.BuildersClubMembershipRevision + 1),
                BuildersClubStatus = message
            });

    private void ApplyBuildersClubPlacementWarning(
        BuildersClubPlacementWarning message,
        long state_generation) =>
        Store(
            state_generation,
            SubscriptionStateChangeKind.BuildersClubPlacementWarning,
            message,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                BuildersClubPlacementWarningRevision = checked(
                    current.BuildersClubPlacementWarningRevision + 1),
                LastPlacementWarning = message
            });

    private void Store(
        long state_generation,
        SubscriptionStateChangeKind kind,
        object value,
        Func<SubscriptionState, SubscriptionState> mutation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure = null;
        lock (publication_sync)
        {
            SubscriptionStateUpdate update;
            lock (state_sync)
            {
                SubscriptionState current = state;
                if (state_generation != committed_generation ||
                    current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                SubscriptionState updated = mutation(current);
                update = null!;
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new SubscriptionStateUpdate(
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
            try
            {
                StateCommitted?.Invoke(update);
            }
            catch (Exception error)
            {
                committed_failure = error;
            }
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
        Exception? committed_failure = null;
        lock (publication_sync)
        {
            while (delivering && delivery_thread_id != thread_id)
                Monitor.Wait(publication_sync);
            SubscriptionStateUpdate update;
            lock (state_sync)
            {
                SubscriptionState current = state;
                if (state_generation < committed_generation ||
                    state_generation == reset_generation &&
                    ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                var updated = new SubscriptionState(
                    active_session,
                    state_generation,
                    checked(current.Revision + 1),
                    checked(current.UserInfoRevision + 1),
                    EmptyUserInfo(),
                    checked(current.KickbackRevision + 1),
                    null,
                    checked(current.BuildersClubFurniCountRevision + 1),
                    null,
                    checked(current.BuildersClubMembershipRevision + 1),
                    null,
                    checked(current.BuildersClubPlacementWarningRevision + 1),
                    null,
                    checked(current.ClubOffersRevision + 1),
                    null);
                Volatile.Write(ref state, updated);
                committed_generation = state_generation;
                reset_generation = state_generation;
                publication_epoch = checked(publication_epoch + 1);
                update = new SubscriptionStateUpdate(
                    SubscriptionStateChangeKind.Reset,
                    updated,
                    null,
                    publication_epoch,
                    publish_legacy_reset);
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            try
            {
                StateCommitted?.Invoke(update);
            }
            catch (Exception error)
            {
                committed_failure = error;
            }
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
            SubscriptionStateUpdate update;
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

    internal bool IsCurrentPublication(SubscriptionStateUpdate update) =>
        UpdateCurrent(update);

    private bool UpdateCurrent(SubscriptionStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            SubscriptionState current = State;
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

    private Exception? NotifyLegacy(
        SubscriptionStateUpdate update,
        Exception? failure) => update.Kind switch
        {
            SubscriptionStateChangeKind.UserInfo =>
                Notify(UserInfoChanged, (ScrSendUserInfo)update.Value!, update, failure),
            SubscriptionStateChangeKind.KickbackInfo =>
                Notify(KickbackInfoChanged, (ScrSendKickbackInfo)update.Value!, update, failure),
            SubscriptionStateChangeKind.BuildersClubFurniCount =>
                Notify(
                    BuildersClubFurniCountChanged,
                    (BuildersClubFurniCount)update.Value!,
                    update,
                    failure),
            SubscriptionStateChangeKind.BuildersClubMembershipStatus =>
                Notify(
                    BuildersClubStatusChanged,
                    (BuildersClubMembershipStatus)update.Value!,
                    update,
                    failure),
            SubscriptionStateChangeKind.BuildersClubPlacementWarning =>
                Notify(
                    PlacementWarningReceived,
                    (BuildersClubPlacementWarning)update.Value!,
                    update,
                    failure),
            SubscriptionStateChangeKind.ClubOffers => failure,
            SubscriptionStateChangeKind.Reset when update.PublishLegacyReset =>
                Notify(ResetCompleted, update, failure),
            SubscriptionStateChangeKind.Reset => failure,
            _ => throw new ArgumentOutOfRangeException(nameof(update))
        };

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        SubscriptionStateUpdate update,
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
        SubscriptionStateUpdate update,
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

    private ISubscriptionOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Subscription operations are unavailable until the application runtime is active.");

    private static SubscriptionState InitialState() => new(
        null,
        0,
        0,
        0,
        EmptyUserInfo(),
        0,
        null,
        0,
        null,
        0,
        null,
        0,
        null,
        0,
        null);

    private static HabboClubOffers FreezeClubOffers(HabboClubOffers value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.Offers);
        if (value.Offers.Count > ushort.MaxValue)
            throw new InvalidDataException("Club offers exceed the wire count limit.");
        var offers = new HabboClubOffer[value.Offers.Count];
        for (int index = 0; index < offers.Length; index++)
        {
            HabboClubOffer offer = value.Offers[index] ??
                throw new InvalidDataException("Club offers contain a null entry.");
            offers[index] = offer with { };
        }
        return new HabboClubOffers(Array.AsReadOnly(offers), value.DaysLeft);
    }

    private static IReadOnlyDictionary<string, SubscriptionUserInfoState> Freeze(
        Dictionary<string, SubscriptionUserInfoState> values) =>
        new ReadOnlyDictionary<string, SubscriptionUserInfoState>(values);

    private static IReadOnlyDictionary<string, SubscriptionUserInfoState> EmptyUserInfo() =>
        Freeze(new Dictionary<string, SubscriptionUserInfoState>(
            StringComparer.OrdinalIgnoreCase));
}
