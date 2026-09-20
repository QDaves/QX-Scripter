using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Qx.Model;
using Qx.Presentation.Services.Images;

namespace Qx.Presentation.Services.Marketplace;

public sealed class MarketplacePriceService : IMarketplacePrices, IDisposable
{
    public const int BatchSize = 25;
    public const int Attempts = 3;
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    readonly HotelContext _hotel;
    readonly TimeProvider _time;
    readonly HttpClient _http;
    readonly SemaphoreSlim _turnstile = new(1, 1);
    readonly ConcurrentDictionary<CacheKey, Held> _cache = new();

    public MarketplacePriceService(HotelContext hotel, TimeProvider time, HttpMessageHandler? handler = null)
    {
        _hotel = hotel ?? throw new ArgumentNullException(nameof(hotel));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = RequestTimeout;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("QX");
    }

    public MarketplacePrice? Known(MarketplaceKind kind) =>
        Fresh(kind, out Held? held) ? held.Price : null;

    public bool WasRead(MarketplaceKind kind) => Fresh(kind, out _);

    public async Task<IReadOnlyDictionary<MarketplaceKind, MarketplacePrice>> FetchAsync(IEnumerable<MarketplaceKind> kinds, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        var answer = new Dictionary<MarketplaceKind, MarketplacePrice>();
        var wanted = new List<MarketplaceKind>();
        foreach (MarketplaceKind kind in kinds.Distinct())
        {
            if (kind.Identifier is not { Length: > 0 })
                continue;
            if (Fresh(kind, out Held? held))
            {
                if (held.Price is { } price)
                    answer[kind] = price;
                continue;
            }
            wanted.Add(kind);
        }
        for (int index = 0; index < wanted.Count; index += BatchSize)
        {
            cancellation_token.ThrowIfCancellationRequested();
            MarketplaceKind[] batch = [.. wanted.Skip(index).Take(BatchSize)];
            foreach ((MarketplaceKind kind, MarketplacePrice price) in await ReadBatchAsync(batch, cancellation_token).ConfigureAwait(false))
                answer[kind] = price;
        }
        return answer;
    }

    public void Dispose()
    {
        _http.Dispose();
        _turnstile.Dispose();
    }

    bool Fresh(MarketplaceKind kind, [NotNullWhen(true)] out Held? held)
    {
        held = null;
        if (kind.Identifier is not { Length: > 0 })
            return false;
        if (!_cache.TryGetValue(new CacheKey(_hotel.WebHost, kind.Type, kind.Identifier), out Held? found))
            return false;
        if (_time.GetUtcNow() - found.Read >= Freshness)
            return false;
        held = found;
        return true;
    }

    async Task<Dictionary<MarketplaceKind, MarketplacePrice>> ReadBatchAsync(MarketplaceKind[] batch, CancellationToken cancellation_token)
    {
        var found = new Dictionary<MarketplaceKind, MarketplacePrice>();
        string host = _hotel.WebHost;
        if (string.IsNullOrWhiteSpace(host))
            return found;
        var request = new MarketplaceBatchRequest(
            [.. batch.Where(kind => kind.Type != ItemType.Wall).Select(kind => kind.Identifier)],
            [.. batch.Where(kind => kind.Type == ItemType.Wall).Select(kind => kind.Identifier)]);
        MarketplaceBatchResponse? response = await AskAsync(host, request, cancellation_token).ConfigureAwait(false);
        if (response is null)
            return found;
        Collect(response.RoomItemData, ItemType.Floor, found);
        Collect(response.WallItemData, ItemType.Wall, found);
        DateTimeOffset now = _time.GetUtcNow();
        foreach (MarketplaceKind kind in batch)
            _cache[new CacheKey(host, kind.Type, kind.Identifier)] = new Held(found.GetValueOrDefault(kind), now);
        return found;
    }

    async Task<MarketplaceBatchResponse?> AskAsync(string host, MarketplaceBatchRequest request, CancellationToken cancellation_token)
    {
        await _turnstile.WaitAsync(cancellation_token).ConfigureAwait(false);
        try
        {
            TimeSpan wait = TimeSpan.FromSeconds(1);
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    using HttpResponseMessage message = await _http
                        .PostAsJsonAsync($"https://{host}/api/public/marketplace/stats/batch", request, MarketplaceJson.Default.MarketplaceBatchRequest, cancellation_token)
                        .ConfigureAwait(false);
                    if (message.IsSuccessStatusCode)
                        return await message.Content.ReadFromJsonAsync(MarketplaceJson.Default.MarketplaceBatchResponse, cancellation_token).ConfigureAwait(false);
                    if (attempt >= Attempts || !WorthRetrying(message.StatusCode))
                        return null;
                    wait = message.Headers.RetryAfter?.Delta ?? wait;
                }
                catch (Exception error) when (error is HttpRequestException || (error is TaskCanceledException && !cancellation_token.IsCancellationRequested))
                {
                    if (attempt >= Attempts)
                        return null;
                }
                await Task.Delay(wait, _time, cancellation_token).ConfigureAwait(false);
                wait += wait;
            }
        }
        finally
        {
            _turnstile.Release();
        }
    }

    static bool WorthRetrying(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    static void Collect(IReadOnlyList<MarketplaceItemStats>? stats, ItemType type, Dictionary<MarketplaceKind, MarketplacePrice> into)
    {
        foreach (MarketplaceItemStats entry in stats ?? [])
        {
            if (entry.Item is not { Length: > 0 } identifier)
                continue;
            into[new MarketplaceKind(type, identifier)] = new MarketplacePrice(
                type,
                identifier,
                Positive(entry.CurrentPrice),
                Positive(entry.AveragePrice) ?? FromHistory(entry.History),
                Math.Max(entry.CurrentOpenOffers, entry.TotalOpenOffers),
                entry.SoldItemCount);
        }
    }

    static int? Positive(int value) => value > 0 ? value : null;

    static int? FromHistory(IReadOnlyList<MarketplaceHistoryPoint>? history)
    {
        long credits = 0;
        long items = 0;
        foreach (MarketplaceHistoryPoint point in history ?? [])
        {
            if (!long.TryParse(point.TotalCreditSum, out long day_credits) || !long.TryParse(point.TotalSoldItems, out long day_items) || day_items <= 0)
                continue;
            credits += day_credits;
            items += day_items;
        }
        return items > 0 && credits > 0 ? (int)Math.Round((double)credits / items) : null;
    }

    readonly record struct CacheKey(string WebHost, ItemType Type, string Identifier);

    sealed record Held(MarketplacePrice? Price, DateTimeOffset Read);
}
