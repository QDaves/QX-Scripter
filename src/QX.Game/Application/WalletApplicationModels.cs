using Qx.Model;

namespace Qx.Game.Application;

public static class WalletPointTypes
{
    public const int Duckets = 0;
    public const int Diamonds = 5;
}

public sealed record WalletStateRequest(
    int PointOffset = 0,
    int PointLimit = 100,
    long? SnapshotRevision = null,
    int? PointType = null);

public sealed record WalletRefreshRequest(
    int PointLimit = 100,
    int TimeoutMilliseconds = 10000);

public sealed record WalletPointBalance(int Type, int Amount);

public sealed record WalletPointPage(
    long SnapshotRevision,
    int TotalPoints,
    int Offset,
    int? NextOffset,
    IReadOnlyList<WalletPointBalance> Points);

public sealed record WalletStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long CreditsSnapshotRevision,
    bool CreditsLoaded,
    int? Credits,
    bool PointsLoaded,
    WalletPointPage ActivityPoints);

public enum WalletChangeKind
{
    CreditsRefreshed,
    ActivityPointsRefreshed,
    ActivityPointUpdated,
    Reset
}

public sealed record WalletChanged(
    WalletChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long CreditsSnapshotRevision,
    long ActivityPointsSnapshotRevision,
    bool CreditsLoaded,
    int? Credits,
    bool PointsLoaded,
    int TotalPoints,
    int? PointType,
    int? PointAmount,
    int? PointChange);

internal interface IWalletOperations
{
    Task EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token = default);
}

public static class WalletApplicationPages
{
    private const int page_limit = 500;

    public static WalletStateView Read(
        IApplicationRuntime application,
        int? point_type = null,
        CancellationToken cancellation_token = default) =>
        ReadAsync(application, point_type, cancellation_token)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public static async ValueTask<WalletStateView> ReadAsync(
        IApplicationRuntime application,
        int? point_type = null,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        WalletStateView first = await application
            .InvokeAsync<WalletStateRequest, WalletStateView>(
                ApplicationMemberIds.WalletState,
                new WalletStateRequest(PointLimit: page_limit, PointType: point_type),
                cancellation_token)
            .ConfigureAwait(false);
        return await CompleteAsync(
            application,
            first,
            point_type,
            cancellation_token).ConfigureAwait(false);
    }

    public static WalletStateView Complete(
        IApplicationRuntime application,
        WalletStateView first,
        int? point_type = null,
        CancellationToken cancellation_token = default) =>
        CompleteAsync(application, first, point_type, cancellation_token)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    public static async ValueTask<WalletStateView> CompleteAsync(
        IApplicationRuntime application,
        WalletStateView first,
        int? point_type = null,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(first);
        cancellation_token.ThrowIfCancellationRequested();
        ValidateFirst(first, point_type);
        var points = new List<WalletPointBalance>(first.ActivityPoints.TotalPoints);
        points.AddRange(first.ActivityPoints.Points);
        int? next_offset = first.ActivityPoints.NextOffset;
        while (next_offset is int offset)
        {
            WalletStateView page = await application
                .InvokeAsync<WalletStateRequest, WalletStateView>(
                    ApplicationMemberIds.WalletState,
                    new WalletStateRequest(
                        offset,
                        page_limit,
                        first.ActivityPoints.SnapshotRevision,
                        point_type),
                    cancellation_token)
                .ConfigureAwait(false);
            ValidatePage(first, page, offset, point_type);
            points.AddRange(page.ActivityPoints.Points);
            next_offset = page.ActivityPoints.NextOffset;
        }
        if (points.Count != first.ActivityPoints.TotalPoints)
            throw new InvalidOperationException("The wallet returned an incomplete activity-point snapshot.");
        ValidateBalances(points, point_type);
        return first with
        {
            ActivityPoints = first.ActivityPoints with
            {
                Offset = 0,
                NextOffset = null,
                Points = Array.AsReadOnly(points.ToArray())
            }
        };
    }

    private static void ValidateFirst(WalletStateView first, int? point_type)
    {
        ValidateEnvelope(first);
        WalletPointPage page = first.ActivityPoints;
        int consumed = page.Points.Count;
        int? expected_next = consumed < page.TotalPoints ? consumed : null;
        bool initial_empty = page.SnapshotRevision == 0 &&
            !first.Connected &&
            page.TotalPoints == 0 &&
            page.Points.Count == 0 &&
            page.NextOffset is null;
        if (page.SnapshotRevision < 0 ||
            page.SnapshotRevision == 0 && !initial_empty ||
            page.TotalPoints < 0 ||
            page.Offset != 0 ||
            page.Points.Count > page_limit ||
            page.Points.Count > page.TotalPoints ||
            page.NextOffset != expected_next ||
            expected_next is int next && next <= page.Offset)
        {
            throw new InvalidOperationException("The wallet returned an invalid first activity-point page.");
        }
        ValidateBalances(page.Points, point_type);
    }

    private static void ValidatePage(
        WalletStateView first,
        WalletStateView page,
        int offset,
        int? point_type)
    {
        ValidateEnvelope(page);
        WalletPointPage current = page.ActivityPoints;
        int consumed = checked(offset + current.Points.Count);
        int? expected_next = consumed < current.TotalPoints ? consumed : null;
        if (page.Connected != first.Connected ||
            page.Client != first.Client ||
            page.SessionGeneration != first.SessionGeneration ||
            page.Revision != first.Revision ||
            page.CreditsSnapshotRevision != first.CreditsSnapshotRevision ||
            page.CreditsLoaded != first.CreditsLoaded ||
            page.Credits != first.Credits ||
            page.PointsLoaded != first.PointsLoaded ||
            current.SnapshotRevision != first.ActivityPoints.SnapshotRevision ||
            current.TotalPoints != first.ActivityPoints.TotalPoints ||
            current.Offset != offset ||
            current.Points.Count > page_limit ||
            consumed > current.TotalPoints ||
            current.NextOffset != expected_next ||
            expected_next is int next && next <= offset)
        {
            throw new InvalidOperationException("The wallet snapshot changed while it was being read.");
        }
        ValidateBalances(current.Points, point_type);
    }

    private static void ValidateEnvelope(WalletStateView view)
    {
        ArgumentNullException.ThrowIfNull(view.ActivityPoints);
        ArgumentNullException.ThrowIfNull(view.ActivityPoints.Points);
        if (view.Connected != (view.Client is not null) ||
            view.CreditsLoaded != (view.Credits is not null))
        {
            throw new InvalidOperationException("The wallet returned an inconsistent state envelope.");
        }
    }

    private static void ValidateBalances(
        IReadOnlyList<WalletPointBalance> points,
        int? point_type)
    {
        int? previous = null;
        foreach (WalletPointBalance point in points)
        {
            ArgumentNullException.ThrowIfNull(point);
            if (point_type is int expected && point.Type != expected)
                throw new InvalidOperationException("The wallet returned an activity-point type outside the requested filter.");
            if (previous is int preceding && point.Type <= preceding)
                throw new InvalidOperationException("The wallet returned activity-point balances out of order.");
            previous = point.Type;
        }
    }
}
