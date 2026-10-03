using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the earnings vault manager, which tracks what each source has paid out and is waiting
    /// to be claimed.
    /// </summary>
    public EarningsManager Earnings => Game.Earnings;

    /// <summary>
    /// Gets the whole vault, requesting it from the hotel when it has not been loaded yet.
    /// </summary>
    /// <remarks>
    /// Once the vault is loaded, the cached copy is read without a request.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the vault request.</param>
    /// <returns>The vault with every entry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the vault changed while it was read.</exception>
    public async Task<EarningStatus> GetEarnings(int timeoutMs = 10000) =>
        (await ReadEarningsSnapshot(timeoutMs).ConfigureAwait(false)).Status;

    /// <summary>
    /// Gets the categories that are holding something, with what each one is worth.
    /// </summary>
    /// <remarks>
    /// One row per category, in the order the hotel listed them, so a script can print the vault
    /// without adding anything up itself. The vault is requested from the hotel only when it has
    /// not been loaded yet.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the vault request.</param>
    /// <returns>One line per category.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the vault changed while it was read.</exception>
    public async Task<IReadOnlyList<EarningsLine>> GetEarningsByCategory(int timeoutMs = 10000)
    {
        EarningStatus status = await GetEarnings(timeoutMs);
        return
        [
            .. status.Categories.Select(category => new EarningsLine(
                category,
                status.Credits(category),
                status.Duckets(category),
                status.Products(category),
                status.HasClaimable(category)))
        ];
    }

    /// <summary>
    /// Gets what the whole vault is worth, added up across every category.
    /// </summary>
    /// <remarks>
    /// The vault is requested from the hotel only when it has not been loaded yet.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the vault request.</param>
    /// <returns>A line with <see cref="EarningCategory.All"/> as its category and the vault totals.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the vault changed while it was read.</exception>
    public async Task<EarningsLine> GetEarningsTotal(int timeoutMs = 10000)
    {
        EarningStatus status = await GetEarnings(timeoutMs);
        return new EarningsLine(
            EarningCategory.All,
            status.Credits(),
            status.Duckets(),
            status.Products(),
            status.HasClaimable());
    }

    /// <summary>
    /// Sends a claim for one category of the vault.
    /// </summary>
    /// <remarks>
    /// It returns without waiting. The hotel answers with a result rather than a new vault;
    /// subscribe with <see cref="OnEarningsClaimed"/> to see whether it went through. A refused
    /// claim leaves the cached vault unchanged.
    /// </remarks>
    /// <param name="category">
    /// The category to claim; <see cref="EarningCategory.All"/> claims every category.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the application runtime is not active.</exception>
    public void ClaimEarnings(EarningCategory category) => Game.Earnings.Claim(category);

    /// <summary>
    /// Sends a claim for every category in one request.
    /// </summary>
    /// <remarks>
    /// This is one request, not one per category: the hotel takes the claim-all value
    /// <see cref="EarningCategory.All"/> and empties the whole vault, exactly as the client's
    /// claim-all button does. It returns without waiting for the answer.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the application runtime is not active.</exception>
    public void ClaimAllEarnings() => Game.Earnings.ClaimAll();

    /// <summary>
    /// Claims every category that has something worth claiming and waits for each answer.
    /// </summary>
    /// <remarks>
    /// Sent one category at a time so a category the hotel refuses can be told apart from one it
    /// accepted, which a single claim-all cannot report. Categories holding nothing but duckets are
    /// left alone, matching the client, whose claim button lights up for the rest. A category
    /// whose answer times out is skipped and not reported as accepted. The vault is requested
    /// from the hotel first when it has not been loaded yet.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the vault request and for each claim answer.</param>
    /// <returns>The categories the hotel accepted.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, the session changed, or the vault changed while it was read.</exception>
    public async Task<IReadOnlyList<EarningCategory>> ClaimEarningsPerCategory(int timeoutMs = 10000)
    {
        EarningReadSnapshot snapshot = await ReadEarningsSnapshot(timeoutMs).ConfigureAwait(false);
        EarningStatus status = snapshot.Status;
        var claimed = new List<EarningCategory>();

        foreach (EarningCategory category in status.Categories)
        {
            if (!status.HasClaimable(category))
                continue;

            EarningClaimActionResult result;
            try
            {
                result = await _application
                    .InvokeAsync<EarningClaimActionRequest, EarningClaimActionResult>(
                        ApplicationMemberIds.EarningsClaim,
                        new EarningClaimActionRequest(
                            (int)category,
                            timeoutMs,
                            snapshot.SessionGeneration),
                        Ct)
                    .ConfigureAwait(false);
            }
            catch (RequestTimeoutException)
            {
                continue;
            }

            if (result.SessionGeneration != snapshot.SessionGeneration ||
                result.Category != (int)category ||
                result.MessagesDispatched != 1)
            {
                throw new InvalidOperationException(
                    "The earning application returned an invalid claim result.");
            }

            if (result.Success)
                claimed.Add(category);
        }

        return claimed;
    }

    /// <summary>Sends a request for the hotel to resend the vault.</summary>
    /// <remarks>
    /// It returns without waiting; the answer updates <see cref="Earnings"/> and raises
    /// <see cref="OnEarningsChanged"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the application runtime is not active.</exception>
    public void RefreshEarnings() => Game.Earnings.Request();

    /// <summary>Registers a handler that runs whenever the vault arrives or is changed by a successful claim.</summary>
    /// <param name="handler">The handler to call with the vault as it now stands.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnEarningsChanged(Action<EarningStatus> handler)
        => Subscribe(
            handler,
            value => Game.Earnings.StatusChanged += value,
            value => Game.Earnings.StatusChanged -= value);

    /// <summary>Registers a handler that runs whenever the hotel answers a claim, whether it succeeded or not.</summary>
    /// <param name="handler">The handler to call with the category and whether the claim went through.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnEarningsClaimed(Action<EarningClaimResult> handler)
        => Subscribe(
            handler,
            value => Game.Earnings.Claimed += value,
            value => Game.Earnings.Claimed -= value);

    /// <summary>Registers a handler that runs whenever the hotel reports that a category has a new reward.</summary>
    /// <param name="handler">The handler to call with the category.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnEarningAvailable(Action<EarningCategory> handler)
        => Subscribe(
            handler,
            value => Game.Earnings.RewardAvailable += value,
            value => Game.Earnings.RewardAvailable -= value);

    private async Task<EarningReadSnapshot> ReadEarningsSnapshot(int timeout_ms)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout_ms, "timeoutMs");
        EarningStateView state = await _application
            .InvokeAsync<EarningStateRequest, EarningStateView>(
                ApplicationMemberIds.EarningsState,
                new EarningStateRequest(),
                Ct)
            .ConfigureAwait(false);
        ValidateEarningState(state);

        EarningEntryPage first_page;
        if (state.Vault.Loaded)
        {
            first_page = await _application
                .InvokeAsync<EarningEntryPageRequest, EarningEntryPage>(
                    ApplicationMemberIds.EarningsEntriesList,
                    new EarningEntryPageRequest(
                        Limit: 500,
                        SnapshotRevision: state.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateEarningStatePage(state, first_page);
        }
        else
        {
            EarningRefreshResult refreshed = await _application
                .InvokeAsync<EarningRefreshRequest, EarningRefreshResult>(
                    ApplicationMemberIds.EarningsRefresh,
                    new EarningRefreshRequest(
                        Limit: 500,
                        TimeoutMilliseconds: timeout_ms,
                        ExpectedSessionGeneration: state.SessionGeneration),
                    Ct)
                .ConfigureAwait(false);
            ValidateEarningRefresh(refreshed, state.SessionGeneration);
            first_page = refreshed.FirstPage;
        }

        EarningEntryPage page = first_page;
        ValidateEarningPage(first_page, page, 0);
        var entries = new List<EarningEntry>(page.Total);
        AddEarningEntries(page, entries);
        while (page.NextOffset is int offset)
        {
            page = await _application
                .InvokeAsync<EarningEntryPageRequest, EarningEntryPage>(
                    ApplicationMemberIds.EarningsEntriesList,
                    new EarningEntryPageRequest(offset, 500, first_page.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateEarningPage(first_page, page, offset);
            AddEarningEntries(page, entries);
        }

        if (entries.Count != first_page.Total)
            throw new InvalidOperationException("The earning application returned an incomplete vault.");
        var status = new EarningStatus(Array.AsReadOnly(entries.ToArray()));
        ValidateEarningSummary(first_page.Vault, status);
        return new EarningReadSnapshot(first_page.SessionGeneration, status);
    }

    private static void AddEarningEntries(EarningEntryPage page, List<EarningEntry> entries)
    {
        for (int index = 0; index < page.Entries.Count; index++)
        {
            EarningEntryView entry = page.Entries[index];
            if (entry.Ordinal != checked(page.Offset + index) ||
                entry.Category is < sbyte.MinValue or > sbyte.MaxValue ||
                entry.Kind is < sbyte.MinValue or > sbyte.MaxValue ||
                entry.ProductCode is null ||
                entry.IsProduct != (entry.ProductCode.Length != 0))
            {
                throw new InvalidOperationException(
                    "The earning application returned an invalid vault entry.");
            }
            entries.Add(new EarningEntry(
                (EarningCategory)entry.Category,
                (EarningRewardKind)entry.Kind,
                entry.Amount,
                entry.ProductCode));
        }
    }

    private static void ValidateEarningState(EarningStateView state)
    {
        if (!state.Connected ||
            state.SessionGeneration <= 0 ||
            state.SnapshotRevision <= 0 ||
            state.Vault.EntryCount < 0 ||
            state.Vault.CategoryCount < 0)
        {
            throw new InvalidOperationException(
                "The earning application returned an invalid state snapshot.");
        }
    }

    private static void ValidateEarningStatePage(
        EarningStateView state,
        EarningEntryPage page)
    {
        if (page.Connected != state.Connected ||
            page.SessionGeneration != state.SessionGeneration ||
            page.StateRevision != state.Revision ||
            page.StatusRevision != state.StatusRevision ||
            page.BaselineRevision != state.BaselineRevision ||
            page.SnapshotRevision != state.SnapshotRevision ||
            page.Vault != state.Vault)
        {
            throw new InvalidOperationException(
                "The earning application returned a page from another state snapshot.");
        }
    }

    private static void ValidateEarningRefresh(
        EarningRefreshResult refreshed,
        long expected_session_generation)
    {
        EarningEntryPage page = refreshed.FirstPage;
        if (refreshed.SnapshotRevision <= 0 ||
            refreshed.MessagesDispatched is < 0 or > 1 ||
            refreshed.SessionGeneration != expected_session_generation ||
            !page.Connected ||
            page.SessionGeneration != refreshed.SessionGeneration ||
            page.StateRevision != refreshed.StateRevision ||
            page.StatusRevision != refreshed.StatusRevision ||
            page.BaselineRevision != refreshed.BaselineRevision ||
            page.SnapshotRevision != refreshed.SnapshotRevision)
        {
            throw new InvalidOperationException(
                "The earning application returned an invalid refresh result.");
        }
    }

    private static void ValidateEarningPage(
        EarningEntryPage first_page,
        EarningEntryPage page,
        int offset)
    {
        int consumed = checked(offset + page.Entries.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (first_page.SnapshotRevision <= 0 ||
            !first_page.Connected ||
            !first_page.Vault.Loaded ||
            page.Connected != first_page.Connected ||
            page.SessionGeneration != first_page.SessionGeneration ||
            page.StateRevision != first_page.StateRevision ||
            page.StatusRevision != first_page.StatusRevision ||
            page.BaselineRevision != first_page.BaselineRevision ||
            page.SnapshotRevision != first_page.SnapshotRevision ||
            page.Vault != first_page.Vault ||
            page.Total < 0 ||
            page.Total != first_page.Total ||
            page.Vault.EntryCount != page.Total ||
            page.Offset != offset ||
            page.Entries.Count > 500 ||
            consumed > page.Total ||
            consumed < page.Total && page.Entries.Count == 0 ||
            page.NextOffset != expected_next)
        {
            throw new InvalidOperationException(
                "The earning application returned an invalid snapshot page.");
        }
    }

    private static void ValidateEarningSummary(
        EarningVaultSummary summary,
        EarningStatus status)
    {
        if (!summary.Loaded ||
            summary.EntryCount != status.Entries.Count ||
            summary.CategoryCount != status.Categories.Count ||
            summary.Credits != status.Credits() ||
            summary.Duckets != status.Duckets() ||
            summary.Products != status.Products() ||
            summary.HasClaimable != status.HasClaimable())
        {
            throw new InvalidOperationException(
                "The earning application returned inconsistent vault totals.");
        }
    }

    private sealed record EarningReadSnapshot(
        long SessionGeneration,
        EarningStatus Status);
}

/// <summary>
/// Represents what one category of the earnings vault is worth.
/// </summary>
/// <param name="Category">
/// The category, or <see cref="EarningCategory.All"/> when the row is the whole vault added up.
/// </param>
/// <param name="Credits">The credits waiting.</param>
/// <param name="Duckets">The duckets waiting.</param>
/// <param name="Products">The number of items waiting.</param>
/// <param name="HasClaimable">
/// Whether there is anything the client would light its claim button for. Duckets on their own do
/// not count, which is the client's rule rather than this one.
/// </param>
public sealed record EarningsLine(
    EarningCategory Category,
    int Credits,
    int Duckets,
    int Products,
    bool HasClaimable);
