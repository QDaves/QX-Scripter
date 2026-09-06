using System.Collections.ObjectModel;
using Qx.Game.Protocol;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class GiftApplication
{
    private const int heavy_commit_history_limit = 2;
    private const int scalar_commit_history_limit = 32;
    private static readonly IReadOnlyDictionary<int, GiftOfferGiftabilityState>
        empty_giftability = new ReadOnlyDictionary<int, GiftOfferGiftabilityState>(
            new Dictionary<int, GiftOfferGiftabilityState>());
    private readonly object refresh_sync = new();
    private readonly List<ObservedGiftCommit> wrapping_commits = [];
    private readonly List<ObservedGiftCommit> club_info_commits = [];
    private readonly List<ObservedGiftCommit> giftability_commits = [];

    public ValueTask<GiftRefreshResult> Refresh(
        GiftRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => RefreshCore(request, token));

    private async ValueTask<GiftRefreshResult> RefreshCore(
        GiftRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateRefreshTimeout(request.TimeoutMilliseconds);
        ValidateExpectedRevision(
            request.ExpectedSessionGeneration,
            nameof(request.ExpectedSessionGeneration));
        using var deadline = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(request.TimeoutMilliseconds),
            time_provider);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation_token,
            deadline.Token);
        bool wrapping_locked = false;
        bool club_locked = false;
        try
        {
            await wrapping_refresh_lane.WaitAsync(linked.Token).ConfigureAwait(false);
            wrapping_locked = true;
            await club_info_refresh_lane.WaitAsync(linked.Token).ConfigureAwait(false);
            club_locked = true;
            GiftOperationScope scope = CaptureScope(
                request.ExpectedSessionGeneration,
                linked.Token);
            Task<ObservedGiftCommit> wrapping = RequestWrapping(
                scope,
                request.TimeoutMilliseconds,
                linked.Token);
            Task<ObservedGiftCommit> club_info = RequestClubInfo(
                scope,
                request.TimeoutMilliseconds,
                linked.Token);
            await AwaitRefreshPair(wrapping, club_info, linked).ConfigureAwait(false);
            RequireScope(scope);
            ObservedGiftCommit wrapping_commit = await wrapping.ConfigureAwait(false);
            ObservedGiftCommit club_info_commit = await club_info.ConfigureAwait(false);
            GiftSnapshotLease lease = StoreRefreshLease(
                wrapping_commit.Update,
                club_info_commit.Update);
            GiftWrappingConfiguration wrapping_value =
                (GiftWrappingConfiguration)wrapping_commit.Update.Value!;
            ClubGiftInfo club_info_value = (ClubGiftInfo)club_info_commit.Update.Value!;
            GiftClubInfoPage first_page;
            try
            {
                first_page = ClubInfoPage(
                    lease,
                    GiftClubInfoCollection.Offers,
                    0,
                    request.Limit);
            }
            catch
            {
                RemoveLease(lease.Revision);
                throw;
            }
            return new GiftRefreshResult(
                scope.Session.Client,
                scope.SessionGeneration,
                time_provider.GetUtcNow(),
                wrapping_commit.ObservedAtUtc,
                club_info_commit.ObservedAtUtc,
                lease.Revision,
                wrapping_commit.Update.State.WrappingRevision,
                club_info_commit.Update.State.ClubInfoRevision,
                WrappingSummary(wrapping_value),
                ClubInfoSummary(club_info_value),
                first_page);
        }
        catch (OperationCanceledException) when (
            deadline.IsCancellationRequested &&
            !cancellation_token.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Gift refresh timed out after {request.TimeoutMilliseconds} ms.");
        }
        finally
        {
            if (club_locked)
                club_info_refresh_lane.Release();
            if (wrapping_locked)
                wrapping_refresh_lane.Release();
        }
    }

    private static async Task AwaitRefreshPair(
        Task wrapping,
        Task club_info,
        CancellationTokenSource cancellation)
    {
        Task completed = await Task.WhenAny(wrapping, club_info).ConfigureAwait(false);
        try
        {
            await completed.ConfigureAwait(false);
        }
        catch
        {
            CancelRefreshPair(cancellation);
            await DrainRefreshPair(wrapping, club_info).ConfigureAwait(false);
            throw;
        }
        Task remaining = ReferenceEquals(completed, wrapping) ? club_info : wrapping;
        try
        {
            await remaining.ConfigureAwait(false);
        }
        catch
        {
            CancelRefreshPair(cancellation);
            await DrainRefreshPair(wrapping, club_info).ConfigureAwait(false);
            throw;
        }
    }

    private static void CancelRefreshPair(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
        }
        catch
        {
        }
    }

    private static async Task DrainRefreshPair(Task wrapping, Task club_info)
    {
        try
        {
            await Task.WhenAll(wrapping, club_info).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    public ValueTask<GiftOfferGiftabilityRefreshResult> RefreshOfferGiftability(
        GiftOfferGiftabilityRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => RefreshOfferGiftabilityCore(request, token));

    private async ValueTask<GiftOfferGiftabilityRefreshResult>
        RefreshOfferGiftabilityCore(
            GiftOfferGiftabilityRefreshRequest request,
            CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRefreshTimeout(request.TimeoutMilliseconds);
        ValidateExpectedRevision(
            request.ExpectedSessionGeneration,
            nameof(request.ExpectedSessionGeneration));
        GiftOperationScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        if (UsesUnityGiftWire(scope.Session.Client))
        {
            throw new NotSupportedException(
                "Typed offer-giftability refresh is available only for Flash sessions.");
        }
        var await_state = new RouteAwaitState();
        await requests.RequestAsync(
            MessageContracts.Gifts.OfferGiftabilityRequest,
            new GetIsOfferGiftable(request.OfferId),
            MessageContracts.Gifts.OfferGiftability,
            scope.Session,
            match: response => MatchGiftability(
                await_state,
                scope,
                request.OfferId,
                response),
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () => ArmGiftability(await_state, scope),
            attempt_start: () => Disarm(await_state)).ConfigureAwait(false);
        RequireScope(scope);
        ObservedGiftCommit observed = Accepted(
            await_state,
            "offer-giftability");
        IsOfferGiftable value = (IsOfferGiftable)observed.Update.Value!;
        return new GiftOfferGiftabilityRefreshResult(
            scope.Session.Client,
            scope.SessionGeneration,
            observed.Update.State.Revision,
            observed.Update.State.OfferGiftabilityRevision,
            observed.ObservedAtUtc,
            value.OfferId,
            value.IsGiftable);
    }

    private async Task<ObservedGiftCommit> RequestWrapping(
        GiftOperationScope scope,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        var await_state = new RouteAwaitState();
        await requests.RequestAsync(
            MessageContracts.Gifts.WrappingConfigurationRequest,
            new GetGiftWrappingConfiguration(),
            MessageContracts.Gifts.WrappingConfiguration,
            scope.Session,
            match: response => MatchWrapping(await_state, scope, response),
            timeout_ms: timeout_milliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => ArmWrapping(await_state, scope),
            attempt_start: () => Disarm(await_state)).ConfigureAwait(false);
        RequireScope(scope);
        return Accepted(await_state, "wrapping-configuration");
    }

    private async Task<ObservedGiftCommit> RequestClubInfo(
        GiftOperationScope scope,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        var await_state = new RouteAwaitState();
        await requests.RequestAsync(
            MessageContracts.Gifts.ClubInfoRequest,
            new GetClubGift(),
            MessageContracts.Gifts.ClubInfo,
            scope.Session,
            match: response => MatchClubInfo(await_state, scope, response),
            timeout_ms: timeout_milliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () => ArmClubInfo(await_state, scope),
            attempt_start: () => Disarm(await_state)).ConfigureAwait(false);
        RequireScope(scope);
        return Accepted(await_state, "club-gift-info");
    }

    private void ArmWrapping(RouteAwaitState await_state, GiftOperationScope scope)
    {
        RequireScope(scope);
        lock (refresh_sync)
        {
            await_state.Baseline = gifts.State.WrappingRevision;
            await_state.Accepted = null;
            await_state.Armed = true;
        }
    }

    private void ArmClubInfo(RouteAwaitState await_state, GiftOperationScope scope)
    {
        RequireScope(scope);
        lock (refresh_sync)
        {
            await_state.Baseline = gifts.State.ClubInfoRevision;
            await_state.Accepted = null;
            await_state.Armed = true;
        }
    }

    private void ArmGiftability(RouteAwaitState await_state, GiftOperationScope scope)
    {
        RequireScope(scope);
        lock (refresh_sync)
        {
            await_state.Baseline = gifts.State.OfferGiftabilityRevision;
            await_state.Accepted = null;
            await_state.Armed = true;
        }
    }

    private void Disarm(RouteAwaitState await_state)
    {
        lock (refresh_sync)
        {
            await_state.Baseline = -1;
            await_state.Accepted = null;
            await_state.Armed = false;
        }
    }

    private bool MatchWrapping(
        RouteAwaitState await_state,
        GiftOperationScope scope,
        GiftWrappingConfiguration response)
    {
        lock (refresh_sync)
        {
            if (!await_state.Armed || await_state.Accepted is not null)
                return false;
            ObservedGiftCommit? accepted = FindWrappingCommit(
                scope,
                await_state.Baseline,
                response);
            if (accepted is null)
                return false;
            await_state.Accepted = accepted;
            await_state.Armed = false;
            return true;
        }
    }

    private bool MatchClubInfo(
        RouteAwaitState await_state,
        GiftOperationScope scope,
        ClubGiftInfo response)
    {
        lock (refresh_sync)
        {
            if (!await_state.Armed || await_state.Accepted is not null)
                return false;
            ObservedGiftCommit? accepted = FindClubInfoCommit(
                scope,
                await_state.Baseline,
                response);
            if (accepted is null)
                return false;
            await_state.Accepted = accepted;
            await_state.Armed = false;
            return true;
        }
    }

    private bool MatchGiftability(
        RouteAwaitState await_state,
        GiftOperationScope scope,
        int offer_id,
        IsOfferGiftable response)
    {
        lock (refresh_sync)
        {
            if (!await_state.Armed ||
                await_state.Accepted is not null ||
                response.OfferId != offer_id)
            {
                return false;
            }
            ObservedGiftCommit? accepted = FindGiftabilityCommit(
                scope,
                await_state.Baseline,
                response);
            if (accepted is null)
                return false;
            await_state.Accepted = accepted;
            await_state.Armed = false;
            return true;
        }
    }

    private ObservedGiftCommit? FindWrappingCommit(
        GiftOperationScope scope,
        long baseline,
        GiftWrappingConfiguration response)
    {
        for (int index = 0; index < wrapping_commits.Count; index++)
        {
            ObservedGiftCommit commit = wrapping_commits[index];
            if (CommitMatches(
                    commit,
                    scope,
                    baseline,
                    commit.Update.State.WrappingRevision) &&
                commit.Update.Value is GiftWrappingConfiguration value &&
                WrappingEqual(value, response))
            {
                return commit;
            }
        }
        return null;
    }

    private ObservedGiftCommit? FindClubInfoCommit(
        GiftOperationScope scope,
        long baseline,
        ClubGiftInfo response)
    {
        for (int index = 0; index < club_info_commits.Count; index++)
        {
            ObservedGiftCommit commit = club_info_commits[index];
            if (CommitMatches(
                    commit,
                    scope,
                    baseline,
                    commit.Update.State.ClubInfoRevision) &&
                commit.Update.Value is ClubGiftInfo value &&
                ClubInfoEqual(value, response))
            {
                return commit;
            }
        }
        return null;
    }

    private ObservedGiftCommit? FindGiftabilityCommit(
        GiftOperationScope scope,
        long baseline,
        IsOfferGiftable response)
    {
        for (int index = 0; index < giftability_commits.Count; index++)
        {
            ObservedGiftCommit commit = giftability_commits[index];
            if (CommitMatches(
                    commit,
                    scope,
                    baseline,
                    commit.Update.State.OfferGiftabilityRevision) &&
                commit.Update.Value is IsOfferGiftable value &&
                value.OfferId == response.OfferId &&
                value.IsGiftable == response.IsGiftable)
            {
                return commit;
            }
        }
        return null;
    }

    private static bool CommitMatches(
        ObservedGiftCommit commit,
        GiftOperationScope scope,
        long baseline,
        long revision) =>
        baseline >= 0 &&
        revision > baseline &&
        ReferenceEquals(commit.Update.State.Session, scope.Session) &&
        commit.Update.State.SessionGeneration == scope.SessionGeneration;

    private ObservedGiftCommit Accepted(
        RouteAwaitState await_state,
        string route_name)
    {
        lock (refresh_sync)
        {
            return await_state.Accepted ??
                throw new InvalidOperationException(
                    $"The accepted {route_name} response was not committed by the gift state owner.");
        }
    }

    private void ObserveCommit(GiftStateUpdate update)
    {
        Invocation invocation;
        try
        {
            invocation = EnterInvocation();
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        using (invocation)
        {
            DateTimeOffset observed_at = time_provider.GetUtcNow();
            lock (refresh_sync)
            {
                if (DisposalStarted())
                    return;
                switch (update.Kind)
                {
                    case GiftStateChangeKind.Wrapping:
                        AddCommit(
                            wrapping_commits,
                            new ObservedGiftCommit(
                                Sanitize(update, GiftStateChangeKind.Wrapping),
                                observed_at),
                            heavy_commit_history_limit);
                        break;
                    case GiftStateChangeKind.ClubInfo:
                        AddCommit(
                            club_info_commits,
                            new ObservedGiftCommit(
                                Sanitize(update, GiftStateChangeKind.ClubInfo),
                                observed_at),
                            heavy_commit_history_limit);
                        break;
                    case GiftStateChangeKind.OfferGiftability:
                        AddCommit(
                            giftability_commits,
                            new ObservedGiftCommit(Sanitize(update, null), observed_at),
                            scalar_commit_history_limit);
                        break;
                    case GiftStateChangeKind.Reset:
                        wrapping_commits.Clear();
                        club_info_commits.Clear();
                        giftability_commits.Clear();
                        break;
                }
            }
            if (update.Kind is GiftStateChangeKind.Reset)
                ClearLeases();
        }
    }

    private void PublishChanged(GiftStateUpdate update)
    {
        Invocation invocation;
        try
        {
            invocation = EnterInvocation();
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        using (invocation)
        {
            if (!PublicationCurrent(update))
                return;
            long? snapshot_revision = update.Kind is
                GiftStateChangeKind.Wrapping or
                GiftStateChangeKind.ClubInfo or
                GiftStateChangeKind.ClubSelected or
                GiftStateChangeKind.NewUserOffer
                    ? StoreStateLease(update.State).Revision
                    : null;
            changed.Publish(
                new GiftChanged(
                    ChangeKind(update.Kind),
                    time_provider.GetUtcNow(),
                    update.State.Session?.Client,
                    update.State.SessionGeneration,
                    update.State.Revision,
                    SourceRevision(update),
                    snapshot_revision,
                    update.Value is GiftWrappingConfiguration wrapping
                        ? WrappingSummary(wrapping)
                        : null,
                    update.Value is ClubGiftInfo club_info
                        ? ClubInfoSummary(club_info)
                        : null,
                    update.Value is ClubGiftSelected selected
                        ? ClubSelectedSummary(selected)
                        : null,
                    update.Value as PresentOpened,
                    update.Kind is GiftStateChangeKind.ReceiverNotFound,
                    update.Value as ClubGiftNotification,
                    update.Value as IsOfferGiftable,
                    update.Value is NuxGiftOffer new_user_offer
                        ? NewUserOfferSummary(new_user_offer)
                        : null,
                    update.Kind is GiftStateChangeKind.NewUserIncomplete),
                () => PublicationCurrent(update));
        }
    }

    private void ClearRefreshState()
    {
        lock (refresh_sync)
        {
            wrapping_commits.Clear();
            club_info_commits.Clear();
            giftability_commits.Clear();
        }
    }

    private static GiftStateUpdate Sanitize(
        GiftStateUpdate update,
        GiftStateChangeKind? preserve) => update with
    {
        State = update.State with
        {
            Wrapping = preserve is GiftStateChangeKind.Wrapping
                ? update.State.Wrapping
                : null,
            ClubInfo = preserve is GiftStateChangeKind.ClubInfo
                ? update.State.ClubInfo
                : null,
            ClubSelected = null,
            OfferGiftability = empty_giftability,
            NewUserOffer = null
        }
    };

    private static void AddCommit(
        List<ObservedGiftCommit> commits,
        ObservedGiftCommit commit,
        int limit)
    {
        commits.Add(commit);
        if (commits.Count > limit)
            commits.RemoveAt(0);
    }

    private static GiftChangeKind ChangeKind(GiftStateChangeKind kind) => kind switch
    {
        GiftStateChangeKind.Wrapping => GiftChangeKind.Wrapping,
        GiftStateChangeKind.ClubInfo => GiftChangeKind.ClubInfo,
        GiftStateChangeKind.ClubSelected => GiftChangeKind.ClubSelected,
        GiftStateChangeKind.PresentOpened => GiftChangeKind.PresentOpened,
        GiftStateChangeKind.ReceiverNotFound => GiftChangeKind.ReceiverNotFound,
        GiftStateChangeKind.ClubNotification => GiftChangeKind.ClubNotification,
        GiftStateChangeKind.OfferGiftability => GiftChangeKind.OfferGiftability,
        GiftStateChangeKind.NewUserOffer => GiftChangeKind.NewUserOffer,
        GiftStateChangeKind.NewUserIncomplete => GiftChangeKind.NewUserIncomplete,
        GiftStateChangeKind.Reset => GiftChangeKind.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static long SourceRevision(GiftStateUpdate update) => update.Kind switch
    {
        GiftStateChangeKind.Wrapping => update.State.WrappingRevision,
        GiftStateChangeKind.ClubInfo => update.State.ClubInfoRevision,
        GiftStateChangeKind.ClubSelected => update.State.ClubSelectedRevision,
        GiftStateChangeKind.PresentOpened => update.State.PresentOpenedRevision,
        GiftStateChangeKind.ReceiverNotFound => update.State.ReceiverNotFoundRevision,
        GiftStateChangeKind.ClubNotification => update.State.ClubNotificationRevision,
        GiftStateChangeKind.OfferGiftability => update.State.OfferGiftabilityRevision,
        GiftStateChangeKind.NewUserOffer => update.State.NewUserOfferRevision,
        GiftStateChangeKind.NewUserIncomplete => update.State.NewUserIncompleteRevision,
        GiftStateChangeKind.Reset => update.State.Revision,
        _ => throw new ArgumentOutOfRangeException(nameof(update))
    };

    private static bool WrappingEqual(
        GiftWrappingConfiguration left,
        GiftWrappingConfiguration right) =>
        left.IsWrappingEnabled == right.IsWrappingEnabled &&
        left.WrappingPrice == right.WrappingPrice &&
        left.StuffTypes.SequenceEqual(right.StuffTypes) &&
        left.BoxTypes.SequenceEqual(right.BoxTypes) &&
        left.RibbonTypes.SequenceEqual(right.RibbonTypes) &&
        left.DefaultStuffTypes.SequenceEqual(right.DefaultStuffTypes);

    private static bool ClubInfoEqual(ClubGiftInfo left, ClubGiftInfo right)
    {
        if (left.DaysUntilNextGift != right.DaysUntilNextGift ||
            left.GiftsAvailable != right.GiftsAvailable ||
            left.Offers.Count != right.Offers.Count ||
            left.GiftEligibility.Count != right.GiftEligibility.Count)
        {
            return false;
        }
        for (int index = 0; index < left.Offers.Count; index++)
        {
            if (!ClubOfferEqual(left.Offers[index], right.Offers[index]))
                return false;
        }
        return left.GiftEligibility.SequenceEqual(right.GiftEligibility);
    }

    private static bool ClubOfferEqual(
        CatalogPageOffer left,
        CatalogPageOffer right) =>
        left.OfferId == right.OfferId &&
        string.Equals(left.LocalizationId, right.LocalizationId, StringComparison.Ordinal) &&
        left.IsRent == right.IsRent &&
        left.PriceInCredits == right.PriceInCredits &&
        left.PriceInActivityPoints == right.PriceInActivityPoints &&
        left.ActivityPointType == right.ActivityPointType &&
        left.PriceInSilver == right.PriceInSilver &&
        left.Giftable == right.Giftable &&
        left.ClubLevel == right.ClubLevel &&
        left.BundlePurchaseAllowed == right.BundlePurchaseAllowed &&
        left.IsPet == right.IsPet &&
        string.Equals(left.PreviewImage, right.PreviewImage, StringComparison.Ordinal) &&
        left.Products.SequenceEqual(right.Products) &&
        OptionalSequenceEqual(
            left.UnityProductReferences,
            right.UnityProductReferences) &&
        OptionalSequenceEqual(left.UnityProducts, right.UnityProducts);

    private static bool OptionalSequenceEqual<T>(
        IReadOnlyList<T>? left,
        IReadOnlyList<T>? right) =>
        left is null
            ? right is null
            : right is not null && left.SequenceEqual(right);

    private static void ValidateRefreshTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidatePageLimit(int limit)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private sealed class RouteAwaitState
    {
        public long Baseline = -1;
        public ObservedGiftCommit? Accepted;
        public bool Armed;
    }

    private sealed record ObservedGiftCommit(
        GiftStateUpdate Update,
        DateTimeOffset ObservedAtUtc);
}
