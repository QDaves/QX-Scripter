namespace Qx.Game.Application;

internal sealed partial class AchievementApplication
{
    private const int maximum_page_size = 500;

    public AchievementStateView ReadAchievementState(AchievementStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshotRevision(request.SnapshotRevision);
        AchievementSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadAchievementLease(revision)
            : StoreCurrentAchievementLease();
        AchievementStateView result = AchievementStateViewFor(lease);
        RequireAchievementLeaseActive(lease);
        return result;
    }

    public AchievementPage ReadAchievements(AchievementPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        AchievementSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadAchievementLease(revision)
            : StoreCurrentAchievementLease();
        AchievementPage result = AchievementPageFor(
            lease,
            request.Offset,
            request.Limit);
        RequireAchievementLeaseActive(lease);
        return result;
    }

    public AchievementPointLimitPage ReadAchievementPointLimits(
        AchievementPointLimitPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        AchievementSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadAchievementLease(revision)
            : StoreCurrentAchievementLease();
        AchievementPointLimitPage result = AchievementPointLimitPageFor(
            lease,
            request.Offset,
            request.Limit);
        RequireAchievementLeaseActive(lease);
        return result;
    }

    public BadgeStateView ReadBadgeState(BadgeStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshotRevision(request.SnapshotRevision);
        BadgeSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadBadgeLease(revision)
            : StoreCurrentBadgeLease();
        BadgeStateView result = BadgeStateViewFor(lease);
        RequireBadgeLeaseActive(lease);
        return result;
    }

    public OwnedBadgePage ReadOwnedBadges(OwnedBadgePageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        BadgeSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadBadgeLease(revision)
            : StoreCurrentBadgeLease();
        OwnedBadgePage result = OwnedBadgePageFor(
            lease,
            request.Offset,
            request.Limit);
        RequireBadgeLeaseActive(lease);
        return result;
    }

    public BadgeSelectedSetPage ReadBadgeSelectedSets(
        BadgeSelectedSetPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        BadgeSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadBadgeLease(revision)
            : StoreCurrentBadgeLease();
        BadgeSelectedSetPage result = BadgeSelectedSetPageFor(
            lease,
            request.Offset,
            request.Limit);
        RequireBadgeLeaseActive(lease);
        return result;
    }

    public BadgeSelectedPage ReadSelectedBadges(BadgeSelectedPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        BadgeSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadBadgeLease(revision)
            : StoreCurrentBadgeLease();
        BadgeSelectedPage result = BadgeSelectedPageFor(
            lease,
            request.UserId,
            request.Offset,
            request.Limit);
        RequireBadgeLeaseActive(lease);
        return result;
    }

    private AchievementStateView AchievementStateViewFor(
        AchievementSnapshotLease lease)
    {
        AchievementState state = lease.State;
        bool connected = AchievementConnected(state);
        return new AchievementStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.ListRevision,
            state.BaselineRevision,
            state.ScoreRevision,
            state.PointLimitsRevision,
            state.NewCodesRevision,
            lease.Revision,
            AchievementSummary(state),
            state.ScoreLoaded,
            state.ScoreLoaded ? state.Score : null,
            state.PointLimitsLoaded,
            lease.PointLimits.Count,
            state.NewCodes.Count);
    }

    private AchievementPage AchievementPageFor(
        AchievementSnapshotLease lease,
        int offset,
        int limit)
    {
        AchievementState state = lease.State;
        IReadOnlyList<AchievementApplicationItem> page = Slice(
            lease.Achievements,
            offset,
            limit);
        bool connected = AchievementConnected(state);
        return new AchievementPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.ListRevision,
            state.BaselineRevision,
            state.NewCodesRevision,
            lease.Revision,
            state.Loaded,
            state.DefaultCategory,
            lease.Achievements.Count,
            lease.Achievements.Count(value => value.IsComplete),
            offset,
            NextOffset(offset, page.Count, lease.Achievements.Count),
            page);
    }

    private AchievementPointLimitPage AchievementPointLimitPageFor(
        AchievementSnapshotLease lease,
        int offset,
        int limit)
    {
        AchievementState state = lease.State;
        IReadOnlyList<AchievementPointLimitItem> page = Slice(
            lease.PointLimits,
            offset,
            limit);
        bool connected = AchievementConnected(state);
        return new AchievementPointLimitPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.PointLimitsRevision,
            lease.Revision,
            state.PointLimitsLoaded,
            lease.PointLimits.Count,
            offset,
            NextOffset(offset, page.Count, lease.PointLimits.Count),
            page);
    }

    private BadgeStateView BadgeStateViewFor(BadgeSnapshotLease lease)
    {
        BadgeInventoryState state = lease.State;
        bool connected = BadgeConnected(state);
        return new BadgeStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.InventoryRevision,
            state.BaselineRevision,
            state.SelectedRevision,
            lease.Revision,
            BadgeSummary(lease));
    }

    private OwnedBadgePage OwnedBadgePageFor(
        BadgeSnapshotLease lease,
        int offset,
        int limit)
    {
        BadgeInventoryState state = lease.State;
        IReadOnlyList<Qx.Game.Snapshots.OwnedBadgeSnapshot> page = Slice(
            lease.OwnedBadges,
            offset,
            limit);
        bool connected = BadgeConnected(state);
        return new OwnedBadgePage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.InventoryRevision,
            state.BaselineRevision,
            lease.Revision,
            BadgeSummary(lease),
            lease.OwnedBadges.Count,
            offset,
            NextOffset(offset, page.Count, lease.OwnedBadges.Count),
            page);
    }

    private BadgeSelectedSetPage BadgeSelectedSetPageFor(
        BadgeSnapshotLease lease,
        int offset,
        int limit)
    {
        BadgeInventoryState state = lease.State;
        IReadOnlyList<BadgeSelectedSetSummary> sets = Slice(
            lease.SelectedSets.Select(value => new BadgeSelectedSetSummary(
                value.UserId,
                value.Badges.Count,
                value.Revision)).ToArray(),
            offset,
            limit);
        bool connected = BadgeConnected(state);
        return new BadgeSelectedSetPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.SelectedRevision,
            lease.Revision,
            lease.SelectedSets.Count,
            offset,
            NextOffset(offset, sets.Count, lease.SelectedSets.Count),
            sets);
    }

    private BadgeSelectedPage BadgeSelectedPageFor(
        BadgeSnapshotLease lease,
        Id user_id,
        int offset,
        int limit)
    {
        BadgeInventoryState state = lease.State;
        BadgeSelectedLeaseSet? selected = lease.SelectedSets.FirstOrDefault(
            value => value.UserId == user_id);
        int total = selected?.Badges.Count ?? 0;
        IReadOnlyList<SelectedBadgeSnapshot> page = selected is null
            ? Array.AsReadOnly(Array.Empty<SelectedBadgeSnapshot>())
            : Slice(selected.Badges, offset, limit);
        bool connected = BadgeConnected(state);
        return new BadgeSelectedPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.SelectedRevision,
            lease.Revision,
            user_id,
            selected is not null,
            total,
            offset,
            NextOffset(offset, page.Count, total),
            page);
    }

    private AchievementListSummary AchievementSummary(AchievementState state)
    {
        int progress = state.Achievements
            .Where(value => value.IsListed)
            .Sum(value => value.LevelsAchieved);
        int maximum = state.Achievements
            .Where(value => value.IsListed)
            .Sum(value => value.LevelCount);
        return new AchievementListSummary(
            state.Loaded,
            state.DefaultCategory,
            state.Achievements.Count,
            state.Achievements.Count(value => value.IsComplete),
            progress,
            maximum,
            maximum <= 0 ? 0 : (double)progress / maximum);
    }

    private static BadgeInventorySummary BadgeSummary(BadgeSnapshotLease lease)
    {
        BadgeInventoryState state = lease.State;
        return new BadgeInventorySummary(
            state.Loaded,
            state.Loading,
            state.Stale,
            state.RecoveryPending,
            state.LoadGeneration,
            state.ExpectedFragments,
            state.ReceivedFragments,
            lease.OwnedBadges.Count,
            lease.SelectedSets.Count,
            lease.SelectedSets.Sum(value => value.Badges.Count),
            state.RecoveryRetiredRequestEpoch > 0
                ? state.RecoveryRetiredRequestEpoch
                : null,
            state.RecoveryActiveRequestEpoch > 0
                ? state.RecoveryActiveRequestEpoch
                : null);
    }

    private bool AchievementConnected(AchievementState state) =>
        state.Session is not null &&
        ReferenceEquals(connection.Session, state.Session);

    private bool BadgeConnected(BadgeInventoryState state) =>
        state.Session is not null &&
        ReferenceEquals(connection.Session, state.Session);

    private void RequireAchievementLeaseActive(AchievementSnapshotLease lease)
    {
        if (!AchievementLeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the achievement snapshot was being read.");
        }
    }

    private void RequireBadgeLeaseActive(BadgeSnapshotLease lease)
    {
        if (!BadgeLeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the badge snapshot was being read.");
        }
    }

    private static void ValidatePage(
        int offset,
        int limit,
        long? snapshot_revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (limit is < 1 or > maximum_page_size)
            throw new ArgumentOutOfRangeException(nameof(limit));
        ValidateSnapshotRevision(snapshot_revision);
        if (offset > 0 && snapshot_revision is null)
        {
            throw new ArgumentException(
                "A snapshot revision is required after the first page.",
                nameof(snapshot_revision));
        }
    }

    private static void ValidateSnapshotRevision(long? snapshot_revision)
    {
        if (snapshot_revision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot_revision));
    }

    private static AchievementApplicationItem AchievementItem(
        Qx.Model.Messages.Incoming.Achievement value,
        bool is_new) => new(
        value.Id,
        value.Level,
        value.BadgeCode,
        value.BaseProgress,
        value.MaxProgress,
        value.LevelRewardPoints,
        value.LevelRewardPointType,
        value.CurrentProgress,
        value.IsComplete,
        value.Category,
        value.Subcategory,
        value.MaxLevel,
        value.DisplayMethod,
        value.State,
        is_new);

    private static IReadOnlyList<T> Slice<T>(
        IReadOnlyList<T> values,
        int offset,
        int limit)
    {
        if (offset >= values.Count)
            return Array.AsReadOnly(Array.Empty<T>());
        int count = Math.Min(limit, values.Count - offset);
        var page = new T[count];
        for (int index = 0; index < count; index++)
            page[index] = values[offset + index];
        return Array.AsReadOnly(page);
    }

    private static int? NextOffset(int offset, int count, int total)
    {
        int next = checked(offset + count);
        return next < total ? next : null;
    }
}
