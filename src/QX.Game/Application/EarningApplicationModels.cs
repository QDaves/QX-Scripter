using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>Represents a request for the earnings vault state view.</summary>
/// <remarks>
/// Used by the <c>earnings.state</c> query. The application keeps the four most recent earning
/// snapshots of the active hotel session, and a retained snapshot can no longer be read once it is
/// dropped or the session changes.
/// </remarks>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.</param>
public sealed record EarningStateRequest(
    long? SnapshotRevision = null);

/// <summary>Represents a summary of the earnings vault.</summary>
/// <param name="Loaded">Whether the earnings status has been received in the current session.</param>
/// <param name="EntryCount">The number of lines in the vault.</param>
/// <param name="CategoryCount">The number of distinct categories that hold at least one line.</param>
/// <param name="Credits">The sum of the amounts of all credit lines.</param>
/// <param name="Duckets">The sum of the amounts of all ducket lines.</param>
/// <param name="Products">The number of lines that hand over an item rather than currency.</param>
/// <param name="HasClaimable">Whether any line other than duckets carries an amount, which is when the client shows the claim indicator.</param>
public sealed record EarningVaultSummary(
    bool Loaded,
    int EntryCount,
    int CategoryCount,
    int Credits,
    int Duckets,
    int Products,
    bool HasClaimable);

/// <summary>Represents one line of the earnings vault.</summary>
/// <param name="Ordinal">The zero-based position of the line in the snapshot.</param>
/// <param name="Category">The category the earning came from, as a signed byte value such as 2 for achievements.</param>
/// <param name="Kind">The reward kind as a signed byte value, 0 for duckets and 1 for credits.</param>
/// <param name="Amount">The amount waiting.</param>
/// <param name="ProductCode">The product the line hands over, or an empty string when the line is plain currency.</param>
/// <param name="IsProduct">Whether the line hands over an item rather than currency.</param>
public sealed record EarningEntryView(
    int Ordinal,
    int Category,
    int Kind,
    int Amount,
    string ProductCode,
    bool IsProduct);

/// <summary>Represents the earnings vault state read from one snapshot.</summary>
/// <remarks>Returned by the <c>earnings.state</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="Revision">The earning state revision, which increases with every change.</param>
/// <param name="StatusRevision">The revision of the vault contents, which increases when the status is received, a successful claim removes lines or the state is cleared.</param>
/// <param name="BaselineRevision">The revision of the full status, which increases each time the server sends the complete vault.</param>
/// <param name="ClaimRevision">The revision of claim results, which increases each time a claim result is received or the state is cleared.</param>
/// <param name="NotificationRevision">The revision of reward notifications, which increases each time the server announces a new reward or the state is cleared.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the view was read from.</param>
/// <param name="Vault">The summary of the earnings vault.</param>
public sealed record EarningStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long StatusRevision,
    long BaselineRevision,
    long ClaimRevision,
    long NotificationRevision,
    long SnapshotRevision,
    EarningVaultSummary Vault);

/// <summary>Represents a request for a page of earnings vault lines.</summary>
/// <remarks>Used by the <c>earnings.entries.list</c> query.</remarks>
/// <param name="Offset">The zero-based index of the first line to return.</param>
/// <param name="Limit">The maximum number of lines to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record EarningEntryPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of earnings vault lines read from one snapshot.</summary>
/// <remarks>Returned by the <c>earnings.entries.list</c> query. The lines keep the order in which the server sent them.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The earning state revision, which increases with every change.</param>
/// <param name="StatusRevision">The revision of the vault contents, which increases when the status is received, a successful claim removes lines or the state is cleared.</param>
/// <param name="BaselineRevision">The revision of the full status, which increases each time the server sends the complete vault.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Vault">The summary of the earnings vault.</param>
/// <param name="Total">The number of lines in the snapshot.</param>
/// <param name="Offset">The zero-based index of the first line in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more lines.</param>
/// <param name="Entries">The vault lines in the page.</param>
public sealed record EarningEntryPage(
    bool Connected,
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

/// <summary>Represents a request to reload the earnings vault from the server.</summary>
/// <remarks>
/// Used by the <c>earnings.refresh</c> operation. Concurrent callers in the same hotel session share
/// one status request, which is sent after earlier status requests have been answered, and wait for
/// the full status that answers it. Timing out or canceling ends only the caller's own wait.
/// </remarks>
/// <param name="Limit">The maximum number of lines in the first page of the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record EarningRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of an earnings vault refresh.</summary>
/// <remarks>Returned by the <c>earnings.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the status was observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The earning state revision after the status was received.</param>
/// <param name="StatusRevision">The revision of the vault contents after the status was received.</param>
/// <param name="BaselineRevision">The revision of the full status that was received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading further pages.</param>
/// <param name="MessagesDispatched">1 when the shared request is counted for this call, 0 when the call joined a request counted for another caller.</param>
/// <param name="FirstPage">The first page of vault lines from the refreshed snapshot.</param>
public sealed record EarningRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long StatusRevision,
    long BaselineRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    EarningEntryPage FirstPage);

/// <summary>Represents a request to claim the earnings of one category or of every category.</summary>
/// <remarks>
/// Used by the <c>earnings.claim</c> operation. The claim is sent after earlier claims for the same
/// category have been answered, and calls for the same category are sent in order and never merged.
/// The call waits for the claim result. Timing out or canceling ends only the caller's own wait and
/// does not cancel a claim that was already sent.
/// </remarks>
/// <param name="Category">The category to claim, a signed byte value such as 2 for achievements, or -1 to claim every category.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the claim result, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record EarningClaimActionRequest(
    int Category,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of an earnings claim.</summary>
/// <remarks>Returned by the <c>earnings.claim</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the claim was sent.</param>
/// <param name="ObservedAtUtc">The UTC time the claim result was observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the claim ran in.</param>
/// <param name="StateRevision">The earning state revision after the claim result was received.</param>
/// <param name="StatusRevision">The revision of the vault contents after the claim result was received.</param>
/// <param name="BaselineRevision">The revision of the last full status received.</param>
/// <param name="ClaimRevision">The revision of the claim result that was received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot that holds the claim result.</param>
/// <param name="Category">The category that was claimed, or -1 when every category was claimed.</param>
/// <param name="Success">Whether the server reported the claim as successful.</param>
/// <param name="MessagesDispatched">The number of claim messages the call sent.</param>
/// <param name="Vault">The summary of the earnings vault after the claim result was applied.</param>
public sealed record EarningClaimActionResult(
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

/// <summary>Specifies the kind of an earnings change.</summary>
public enum EarningChangeKind
{
    /// <summary>The full earnings status was received.</summary>
    Snapshot,
    /// <summary>A claim result was received.</summary>
    Claimed,
    /// <summary>The server announced a new reward in the earnings vault.</summary>
    Notification,
    /// <summary>The earnings state was cleared for a new or closed hotel session.</summary>
    Reset
}

/// <summary>Represents a change to the earnings vault state.</summary>
/// <remarks>Published by the <c>earnings.changed</c> event.</remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session the change belongs to.</param>
/// <param name="Revision">The earning state revision after the change.</param>
/// <param name="SourceRevision">The status revision for <see cref="EarningChangeKind.Snapshot"/>, the claim revision for <see cref="EarningChangeKind.Claimed"/>, the notification revision for <see cref="EarningChangeKind.Notification"/>, and <paramref name="Revision"/> for <see cref="EarningChangeKind.Reset"/>.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot that holds the change, or <see langword="null"/> when no snapshot could be stored.</param>
/// <param name="Vault">The summary of the earnings vault for <see cref="EarningChangeKind.Snapshot"/> and <see cref="EarningChangeKind.Claimed"/>; otherwise, <see langword="null"/>.</param>
/// <param name="Category">The claimed or announced category for <see cref="EarningChangeKind.Claimed"/> and <see cref="EarningChangeKind.Notification"/>; otherwise, <see langword="null"/>.</param>
/// <param name="ClaimSucceeded">Whether the claim succeeded for <see cref="EarningChangeKind.Claimed"/>; otherwise, <see langword="null"/>.</param>
public sealed record EarningChanged(
    EarningChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
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
