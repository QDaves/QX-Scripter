using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Qx.Model;

namespace Qx.Ui;

/// <summary>
/// What the hotel's own marketplace says a kind of furniture is going for.
/// </summary>
/// <param name="Type">Whether the furniture is a floor or wall item.</param>
/// <param name="Identifier">The furniture identifier used by the marketplace endpoint.</param>
/// <param name="CurrentPrice">The cheapest offer standing right now, where one is standing.</param>
/// <param name="AveragePrice">What it has gone for lately.</param>
/// <param name="OpenOffers">How many offers are currently standing.</param>
/// <param name="SoldLately">How many items were sold during the reported period.</param>
public sealed record MarketplacePrice(
    ItemType Type,
    string Identifier,
    int? CurrentPrice,
    int? AveragePrice,
    int OpenOffers,
    int SoldLately)
{
    /// <summary>
    /// The number to put in front of someone about to sell.
    /// </summary>
    /// <remarks>
    /// The standing price when the market has one, because that is what a buyer is looking at.
    /// Otherwise what it has been going for, which is the only other thing that is true. A kind
    /// nobody has offered or sold has neither, and that is reported as nothing rather than as zero —
    /// a zero here would be read as free.
    /// </remarks>
    public int? Suggested => CurrentPrice ?? AveragePrice;

    /// <summary>Whether the market said anything at all about this kind.</summary>
    public bool IsKnown => Suggested is not null;

    /// <summary>Whether the figure is a standing offer rather than a recollection.</summary>
    public bool IsCurrent => CurrentPrice is not null;
}

/// <summary>
/// Reads marketplace prices from the hotel's public API.
/// </summary>
/// <remarks>
/// <para>
/// The batch endpoint answers for many kinds at once, which is the whole reason to use it: asking
/// the game for one kind at a time turns a screen of inventory into a screen of round trips. Twenty
/// five is the most it will take, so requests are cut into that size and the pieces run together.
/// </para>
/// <para>
/// Answers are held for a while and asked for once. Scrolling a list back and forth would otherwise
/// re-ask the same question every time a row came into view, and a price does not change between one
/// scroll and the next.
/// </para>
/// </remarks>
public static class MarketplacePrices
{
    /// <summary>The most kinds the hotel will answer for in one request.</summary>
    public const int BatchSize = 25;

    /// <summary>How many times one batch is put before it is given up on.</summary>
    private const int Attempts = 3;

    private static readonly TimeSpan Freshness = TimeSpan.FromMinutes(10);

    /// <summary>Keeps the batches in single file, so the hotel is never asked twice at once.</summary>
    private static readonly SemaphoreSlim Turnstile = new(1, 1);

    private static readonly HttpClient Http = CreateClient();

    private static readonly ConcurrentDictionary<(ItemType Type, string Identifier), Held> Cache =
        new();

    private sealed record Held(MarketplacePrice? Price, DateTimeOffset Read);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Add("User-Agent", "QX");
        return client;
    }

    /// <summary>What is already known, without asking.</summary>
    public static MarketplacePrice? Known(ItemType type, string? identifier) =>
        identifier is { Length: > 0 } &&
        Cache.TryGetValue((type, identifier), out Held? held) &&
        DateTimeOffset.UtcNow - held.Read < Freshness
            ? held.Price
            : null;

    public static bool WasRead(ItemType type, string? identifier) =>
        identifier is { Length: > 0 } &&
        Cache.TryGetValue((type, identifier), out Held? held) &&
        DateTimeOffset.UtcNow - held.Read < Freshness;

    /// <summary>Forgets everything read so far, so the next look asks again.</summary>
    public static void Forget()
    {
        Cache.Clear();
    }

    /// <summary>
    /// Prices for the kinds named, read from the hotel and remembered.
    /// </summary>
    /// <remarks>
    /// Kinds already held and still fresh are answered from what is held. What is left is cut into
    /// batches the endpoint will accept. A kind the market has never heard of is remembered as
    /// unknown for the same while as a known one, so a list full of untraded furniture asks once
    /// rather than on every redraw.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<(ItemType Type, string Identifier), MarketplacePrice>>
        FetchAsync(
            IEnumerable<(ItemType Type, string Identifier)> kinds,
            CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        var answer = new Dictionary<(ItemType, string), MarketplacePrice>();
        var wanted = new List<(ItemType Type, string Identifier)>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((ItemType type, string identifier) in kinds.Distinct())
        {
            if (identifier is not { Length: > 0 })
                continue;
            if (Cache.TryGetValue((type, identifier), out Held? held) &&
                now - held.Read < Freshness)
            {
                if (held.Price is { } price)
                    answer[(type, identifier)] = price;
                continue;
            }
            wanted.Add((type, identifier));
        }
        if (wanted.Count == 0)
            return answer;

        foreach ((ItemType Type, string Identifier)[] batch in Batches(wanted))
        {
            cancellation_token.ThrowIfCancellationRequested();
            foreach (KeyValuePair<(ItemType, string), MarketplacePrice> found in
                await ReadBatchAsync(batch, cancellation_token).ConfigureAwait(false))
            {
                answer[found.Key] = found.Value;
            }
        }
        return answer;
    }

    private static IEnumerable<(ItemType Type, string Identifier)[]> Batches(
        List<(ItemType Type, string Identifier)> wanted)
    {
        for (int index = 0; index < wanted.Count; index += BatchSize)
            yield return [.. wanted.Skip(index).Take(BatchSize)];
    }

    private static async Task<Dictionary<(ItemType, string), MarketplacePrice>> ReadBatchAsync(
        (ItemType Type, string Identifier)[] batch,
        CancellationToken cancellation_token)
    {
        var found = new Dictionary<(ItemType, string), MarketplacePrice>();
        string host = HabboImages.WebHost;
        if (string.IsNullOrWhiteSpace(host))
            return found;

        var request = new BatchRequest(
            [.. batch.Where(v => v.Type != ItemType.Wall).Select(v => v.Identifier)],
            [.. batch.Where(v => v.Type == ItemType.Wall).Select(v => v.Identifier)]);

        BatchResponse? response = await AskAsync(host, request, cancellation_token)
            .ConfigureAwait(false);
        if (response is null)
        {
            // A hotel that will not answer is not an inventory that has no prices; nothing is
            // remembered, so the next look asks again rather than showing a blank as though it were
            // the answer.
            return found;
        }

        Record(response?.RoomItemData, ItemType.Floor, found);
        Record(response?.WallItemData, ItemType.Wall, found);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach ((ItemType type, string identifier) in batch)
        {
            Cache[(type, identifier)] = new Held(
                found.GetValueOrDefault((type, identifier)),
                now);
        }
        return found;
    }

    /// <summary>
    /// Puts one batch to the hotel, waiting its turn and giving way when told to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One request at a time. A page of furniture can ask about hundreds of kinds, and firing every
    /// batch at once is what turns a public endpoint against you — the hotel starts refusing and the
    /// answers get worse the harder it is asked.
    /// </para>
    /// <para>
    /// A refusal that says to come back later is waited out rather than treated as an answer: a
    /// <c>Retry-After</c> is honoured as given, and without one the wait doubles each time. A refusal
    /// that will not change however long anyone waits — a bad request, an unknown path — is taken at
    /// face value and not retried at all.
    /// </para>
    /// </remarks>
    private static async Task<BatchResponse?> AskAsync(
        string host,
        BatchRequest request,
        CancellationToken cancellation_token)
    {
        await Turnstile.WaitAsync(cancellation_token).ConfigureAwait(false);
        try
        {
            TimeSpan wait = TimeSpan.FromSeconds(1);
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    using HttpResponseMessage message = await Http
                        .PostAsJsonAsync(
                            $"https://{host}/api/public/marketplace/stats/batch",
                            request,
                            cancellation_token)
                        .ConfigureAwait(false);

                    if (message.IsSuccessStatusCode)
                    {
                        return await message.Content
                            .ReadFromJsonAsync<BatchResponse>(cancellation_token)
                            .ConfigureAwait(false);
                    }
                    if (attempt >= Attempts || !WorthRetrying(message.StatusCode))
                        return null;

                    wait = message.Headers.RetryAfter?.Delta ?? wait;
                }
                catch (Exception error) when (
                    error is HttpRequestException ||
                    (error is TaskCanceledException && !cancellation_token.IsCancellationRequested))
                {
                    if (attempt >= Attempts)
                        return null;
                }

                await Task.Delay(wait, cancellation_token).ConfigureAwait(false);
                wait += wait;
            }
        }
        finally
        {
            Turnstile.Release();
        }
    }

    private static bool WorthRetrying(System.Net.HttpStatusCode status) =>
        status is System.Net.HttpStatusCode.TooManyRequests or
            System.Net.HttpStatusCode.RequestTimeout or
            System.Net.HttpStatusCode.InternalServerError or
            System.Net.HttpStatusCode.BadGateway or
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.GatewayTimeout;

    private static void Record(
        IReadOnlyList<ItemStats>? stats,
        ItemType type,
        Dictionary<(ItemType, string), MarketplacePrice> into)
    {
        foreach (ItemStats entry in stats ?? [])
        {
            if (entry.Item is not { Length: > 0 } identifier)
                continue;
            into[(type, identifier)] = new MarketplacePrice(
                type,
                identifier,
                Positive(entry.CurrentPrice),
                Positive(entry.AveragePrice) ?? FromHistory(entry.History),
                Math.Max(entry.CurrentOpenOffers, entry.TotalOpenOffers),
                entry.SoldItemCount);
        }
    }

    private static int? Positive(int value) => value > 0 ? value : null;

    /// <summary>
    /// What the kind has gone for over the days the hotel still remembers.
    /// </summary>
    /// <remarks>
    /// Weighted by how many actually changed hands rather than averaging the daily averages: a day
    /// on which one sold should not count as much as a day on which forty did.
    /// </remarks>
    private static int? FromHistory(IReadOnlyList<HistoryPoint>? history)
    {
        long credits = 0;
        long items = 0;
        foreach (HistoryPoint point in history ?? [])
        {
            if (!long.TryParse(point.TotalCreditSum, out long day_credits) ||
                !long.TryParse(point.TotalSoldItems, out long day_items) ||
                day_items <= 0)
            {
                continue;
            }
            credits += day_credits;
            items += day_items;
        }
        return items > 0 && credits > 0 ? (int)Math.Round((double)credits / items) : null;
    }

    private sealed record BatchRequest(
        [property: JsonPropertyName("roomItems")] IReadOnlyList<string> RoomItems,
        [property: JsonPropertyName("wallItems")] IReadOnlyList<string> WallItems);

    private sealed record BatchResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("roomItemData")] IReadOnlyList<ItemStats>? RoomItemData,
        [property: JsonPropertyName("wallItemData")] IReadOnlyList<ItemStats>? WallItemData);

    private sealed record ItemStats(
        [property: JsonPropertyName("item")] string? Item,
        [property: JsonPropertyName("currentPrice")] int CurrentPrice,
        [property: JsonPropertyName("averagePrice")] int AveragePrice,
        [property: JsonPropertyName("currentOpenOffers")] int CurrentOpenOffers,
        [property: JsonPropertyName("totalOpenOffers")] int TotalOpenOffers,
        [property: JsonPropertyName("soldItemCount")] int SoldItemCount,
        [property: JsonPropertyName("history")] IReadOnlyList<HistoryPoint>? History);

    private sealed record HistoryPoint(
        [property: JsonPropertyName("dayOffset")] string? DayOffset,
        [property: JsonPropertyName("averagePrice")] string? AveragePrice,
        [property: JsonPropertyName("totalSoldItems")] string? TotalSoldItems,
        [property: JsonPropertyName("totalCreditSum")] string? TotalCreditSum);
}
