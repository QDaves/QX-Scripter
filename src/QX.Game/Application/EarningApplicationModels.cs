using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record EarningStateRequest(
    long? SnapshotRevision = null);

public sealed record EarningVaultSummary(
    bool Loaded,
    int EntryCount,
    int CategoryCount,
    int Credits,
    int Duckets,
    int Products,
    bool HasClaimable);

public sealed record EarningEntryView(
    int Ordinal,
    int Category,
    int Kind,
    int Amount,
    string ProductCode,
    bool IsProduct);

public sealed record EarningStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long StatusRevision,
    long BaselineRevision,
    long ClaimRevision,
    long NotificationRevision,
    long SnapshotRevision,
    EarningVaultSummary Vault);

public sealed record EarningEntryPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record EarningEntryPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long StatusRevision,
    long BaselineRevision,
    long SnapshotRevision,
    EarningVaultSummary Vault,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<EarningEntryView> Entries);

public sealed record EarningRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record EarningRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long StatusRevision,
    long BaselineRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    EarningEntryPage FirstPage);

public sealed record EarningClaimActionRequest(
    int Category,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record EarningClaimActionResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long StatusRevision,
    long BaselineRevision,
    long ClaimRevision,
    long SnapshotRevision,
    int Category,
    bool Success,
    int MessagesDispatched,
    EarningVaultSummary Vault);

public enum EarningChangeKind
{
    Snapshot,
    Claimed,
    Notification,
    Reset
}

public sealed record EarningChanged(
    EarningChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    EarningVaultSummary? Vault,
    int? Category,
    bool? ClaimSucceeded);

internal interface IEarningOperations
{
    void RequestStatus();
    void RequestStatusAfterNotification(
        Session expected_session,
        long expected_session_generation);
    void Claim(EarningCategory category);
    Task<EarningStatus> EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token = default);
}
