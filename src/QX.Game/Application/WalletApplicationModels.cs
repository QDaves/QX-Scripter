using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Provides the activity point types of the common wallet currencies.
/// </summary>
public static class WalletPointTypes
{
    /// <summary>
    /// The activity point type of duckets.
    /// </summary>
    public const int Duckets = 0;
    /// <summary>
    /// The activity point type of diamonds.
    /// </summary>
    public const int Diamonds = 5;
}

/// <summary>
/// Represents a request to read the wallet state with a page of activity point balances.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WalletState"/>. Balances are ordered by type. No older
/// snapshots are retained, so a continuation page fails once the activity points change.
/// </remarks>
/// <param name="PointOffset">The zero-based offset of the first balance to return.</param>
/// <param name="PointLimit">The maximum number of balances to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">
/// The activity point snapshot revision that must still be current, or <see langword="null"/> to read the
/// current balances. Required when <paramref name="PointOffset"/> is greater than 0.
/// </param>
/// <param name="PointType">The activity point type to return, or <see langword="null"/> to return every type.</param>
public sealed record WalletStateRequest(
    int PointOffset = 0,
    int PointLimit = 100,
    long? SnapshotRevision = null,
    int? PointType = null);

/// <summary>
/// Represents a request to fetch the credit balance from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.WalletRefresh"/>. The refresh sends a credits request, makes up
/// to two attempts within the timeout and completes when a new credit balance is received. Activity points
/// are not requested and are returned as last observed. Concurrent refreshes share one request.
/// </remarks>
/// <param name="PointLimit">The maximum number of activity point balances in the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the credit balance in milliseconds, from 1 to 120000.</param>
public sealed record WalletRefreshRequest(
    int PointLimit = 100,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents the balance of one activity point type.
/// </summary>
/// <param name="Type">The activity point type, such as <see cref="WalletPointTypes.Duckets"/>.</param>
/// <param name="Amount">The balance.</param>
public sealed record WalletPointBalance(int Type, int Amount);

/// <summary>
/// Represents a page of activity point balances.
/// </summary>
/// <param name="SnapshotRevision">
/// The activity point snapshot revision, passed back to read the next page.
/// </param>
/// <param name="TotalPoints">The number of balances that match the requested type.</param>
/// <param name="Offset">The zero-based offset of the first balance in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Points">The balances in the page, ordered by type.</param>
public sealed record WalletPointPage(
    long SnapshotRevision,
    int TotalPoints,
    int Offset,
    int? NextOffset,
    IReadOnlyList<WalletPointBalance> Points);

/// <summary>
/// Represents the wallet state of the local user.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.WalletState"/> and <see cref="ApplicationMemberIds.WalletRefresh"/>.
/// </remarks>
/// <param name="Connected">Whether the state belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the state belongs to.</param>
/// <param name="Revision">The wallet state revision, increased by every committed wallet change and reset.</param>
/// <param name="CreditsSnapshotRevision">
/// The credits revision, increased when a credit balance is received and when the balances are cleared.
/// </param>
/// <param name="CreditsLoaded">Whether a credit balance was received in the hotel session.</param>
/// <param name="Credits">The credit balance, or <see langword="null"/> when <paramref name="CreditsLoaded"/> is <see langword="false"/>.</param>
/// <param name="PointsLoaded">Whether the full activity point balances were received in the hotel session.</param>
/// <param name="ActivityPoints">The requested page of activity point balances.</param>
public sealed record WalletStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long CreditsSnapshotRevision,
    bool CreditsLoaded,
    int? Credits,
    bool PointsLoaded,
    WalletPointPage ActivityPoints);

/// <summary>
/// Specifies the kind of change reported by <see cref="WalletChanged"/>.
/// </summary>
public enum WalletChangeKind
{
    /// <summary>A credit balance was received.</summary>
    CreditsRefreshed,
    /// <summary>The full activity point balances were received.</summary>
    ActivityPointsRefreshed,
    /// <summary>The balance of one activity point type changed.</summary>
    ActivityPointUpdated,
    /// <summary>The balances were cleared because the manager was reset or a new hotel session connected.</summary>
    Reset
}

/// <summary>
/// Represents a change of the wallet state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.WalletChanged"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The state generation of the hotel session.</param>
/// <param name="Revision">The wallet state revision after the change.</param>
/// <param name="CreditsSnapshotRevision">The credits revision after the change.</param>
/// <param name="ActivityPointsSnapshotRevision">
/// The activity point snapshot revision after the change, increased when balances are received, when one
/// balance changes and when the balances are cleared.
/// </param>
/// <param name="CreditsLoaded">Whether a credit balance was received in the hotel session.</param>
/// <param name="Credits">The credit balance, or <see langword="null"/> when <paramref name="CreditsLoaded"/> is <see langword="false"/>.</param>
/// <param name="PointsLoaded">Whether the full activity point balances were received in the hotel session.</param>
/// <param name="TotalPoints">The number of activity point types with a known balance.</param>
/// <param name="PointType">
/// The changed activity point type for <see cref="WalletChangeKind.ActivityPointUpdated"/>; otherwise, <see langword="null"/>.
/// </param>
/// <param name="PointAmount">
/// The new balance for <see cref="WalletChangeKind.ActivityPointUpdated"/>; otherwise, <see langword="null"/>.
/// </param>
/// <param name="PointChange">
/// The change reported by the hotel for <see cref="WalletChangeKind.ActivityPointUpdated"/>; otherwise, <see langword="null"/>.
/// </param>
public sealed record WalletChanged(
    WalletChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
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

/// <summary>
/// Provides helpers that read the wallet state with every activity point balance.
/// </summary>
/// <remarks>
/// The helpers call <see cref="ApplicationMemberIds.WalletState"/> in pages of 500 balances and join the
/// pages of one snapshot.
/// </remarks>
public static class WalletApplicationPages
{
    private const int page_limit = 500;

    /// <summary>
    /// Reads the wallet state with every activity point balance.
    /// </summary>
    /// <param name="application">The application runtime to call.</param>
    /// <param name="pointType">The activity point type to return, or <see langword="null"/> to return every type.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The wallet state whose activity point page holds every matching balance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the wallet returns an invalid page or the snapshot changes while the pages are read.
    /// </exception>
    /// <remarks>
    /// Blocks the calling thread until every page is read.
    /// </remarks>
    public static WalletStateView Read(
        IApplicationRuntime application,
        int? pointType = null,
        CancellationToken cancellationToken = default) =>
        ReadAsync(application, pointType, cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    /// <summary>
    /// Reads the wallet state with every activity point balance.
    /// </summary>
    /// <param name="application">The application runtime to call.</param>
    /// <param name="pointType">The activity point type to return, or <see langword="null"/> to return every type.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The wallet state whose activity point page holds every matching balance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the wallet returns an invalid page or the snapshot changes while the pages are read.
    /// </exception>
    public static async ValueTask<WalletStateView> ReadAsync(
        IApplicationRuntime application,
        int? pointType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        WalletStateView first = await application
            .InvokeAsync<WalletStateRequest, WalletStateView>(
                ApplicationMemberIds.WalletState,
                new WalletStateRequest(PointLimit: page_limit, PointType: pointType),
                cancellationToken)
            .ConfigureAwait(false);
        return await CompleteAsync(
            application,
            first,
            pointType,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the remaining activity point pages of a wallet state read from its first page.
    /// </summary>
    /// <param name="application">The application runtime to call.</param>
    /// <param name="first">The wallet state read with a point offset of 0.</param>
    /// <param name="pointType">The activity point type <paramref name="first"/> was read with, or <see langword="null"/> for every type.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The wallet state whose activity point page holds every matching balance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="application"/> or <paramref name="first"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a page is invalid or the snapshot changes while the pages are read.
    /// </exception>
    /// <remarks>
    /// Blocks the calling thread until every page is read.
    /// </remarks>
    public static WalletStateView Complete(
        IApplicationRuntime application,
        WalletStateView first,
        int? pointType = null,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(application, first, pointType, cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    /// <summary>
    /// Reads the remaining activity point pages of a wallet state read from its first page.
    /// </summary>
    /// <param name="application">The application runtime to call.</param>
    /// <param name="first">The wallet state read with a point offset of 0.</param>
    /// <param name="pointType">The activity point type <paramref name="first"/> was read with, or <see langword="null"/> for every type.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The wallet state whose activity point page holds every matching balance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="application"/> or <paramref name="first"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a page is invalid or the snapshot changes while the pages are read.
    /// </exception>
    public static async ValueTask<WalletStateView> CompleteAsync(
        IApplicationRuntime application,
        WalletStateView first,
        int? pointType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(first);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateFirst(first, pointType);
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
                        pointType),
                    cancellationToken)
                .ConfigureAwait(false);
            ValidatePage(first, page, offset, pointType);
            points.AddRange(page.ActivityPoints.Points);
            next_offset = page.ActivityPoints.NextOffset;
        }
        if (points.Count != first.ActivityPoints.TotalPoints)
            throw new InvalidOperationException("The wallet returned an incomplete activity-point snapshot.");
        ValidateBalances(points, pointType);
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
        if (view.CreditsLoaded != (view.Credits is not null))
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
