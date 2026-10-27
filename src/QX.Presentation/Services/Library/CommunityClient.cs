using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.Services.Library;

public sealed record CommunityAuthor(string Id, string Name, string? HabboName, string? Hotel);

public sealed record CommunityVersion(int Version, string Note, long CreatedAt);

public sealed record CommunityScript
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public string ScopeType { get; init; } = "";
    public string? ScopeRoom { get; init; }
    public string? ScopeHotel { get; init; }
    public int Version { get; init; }
    public long CreatedAt { get; init; }
    public long VersionCreatedAt { get; init; }
    public long AvailableAt { get; init; }
    public int Score { get; init; }
    public int CopyCount { get; init; }
    public string Note { get; init; } = "";
    public CommunityAuthor Author { get; init; } = new("", "", null, null);
    public string Code { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public IReadOnlyList<CommunityVersion> Versions { get; init; } = [];
    public string AuthorText => Author.Name;
    public string HeadName => string.IsNullOrWhiteSpace(Author.HabboName) ? Author.Name : Author.HabboName;
    public string UpdatedText => $"Updated {DateTimeOffset.FromUnixTimeMilliseconds(VersionCreatedAt):d}";
    public string ScopeText => ScopeType == "room" ? $"Room {ScopeRoom} · {ScopeHotel}" : "Any room";
    [JsonIgnore]
    public ImageRequest? Head => Author.Hotel is "habbo.com" or "habbo.de" or "habbo.es" or "habbo.fr" or "habbo.it" or "habbo.nl" or "habbo.com.br" or "habbo.com.tr" &&
        HabboUrls.HeadForName(Author.HabboName, "www." + Author.Hotel) is { } url ? new(url, true, IconKind.User) : null;
}

public sealed record CommunityPage(IReadOnlyList<CommunityScript> Items, int Total, int Page, int Pages, long ServerTime);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(CommunityPage))]
[JsonSerializable(typeof(CommunityScript))]
internal sealed partial class CommunityJson : JsonSerializerContext;

public sealed partial class CommunityClient : IDisposable
{
    public const string SiteUrl = "https://qxscripter.xyz";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    const int MaxResponseBytes = 2 * 1024 * 1024;
    readonly HttpClient _http;

    public CommunityClient(HttpMessageHandler? handler = null)
    {
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("QX-Scripter");
    }

    public async Task<CommunityPage> BrowseAsync(string search, string category, bool newest, int page, CancellationToken cancellation_token)
    {
        string query = $"?q={Uri.EscapeDataString(search)}&category={Uri.EscapeDataString(category == "All" ? "" : category)}&sort={(newest ? "new" : "top")}&page={page}";
        CommunityPage result = await ReadAsync("/api/community/scripts" + query, CommunityJson.Default.CommunityPage, cancellation_token);
        if (result.Items is null || result.Items.Count > 40 || result.Total < 0 || result.Page < 1 || result.Pages < result.Page)
            throw new InvalidDataException("The community catalogue is invalid.");
        foreach (CommunityScript script in result.Items)
            Validate(script, result.ServerTime);
        return result;
    }

    public async Task<CommunityScript> GetAsync(string id, int? version, CancellationToken cancellation_token)
    {
        if (!ValidId().IsMatch(id))
            throw new ArgumentException("Invalid script id.", nameof(id));
        string suffix = version is { } number ? $"?version={number}" : "";
        CommunityScript script = await ReadAsync("/api/community/scripts/" + id + suffix, CommunityJson.Default.CommunityScript, cancellation_token);
        Validate(script, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (script.Id != id || version is { } requested && script.Version != requested || script.Code is not { Length: >= 10 and <= 100000 } ||
            !string.Equals(Hash(script.Code), script.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded script does not match its version.");
        return script;
    }

    public static string Hash(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    static void Validate(CommunityScript? script, long now)
    {
        if (script is null || script.Id is null || !ValidId().IsMatch(script.Id) || script.Version < 1 ||
            script.Title is not { Length: >= 3 and <= 80 } || script.Author is null ||
            script.CreatedAt <= 0 || script.VersionCreatedAt <= 0 || script.Author.Name is not { Length: >= 1 and <= 80 } ||
            script.Description is not { Length: <= 200 } || script.Author.Id is null ||
            !ValidId().IsMatch(script.Author.Id) || script.CreatedAt > now - (long)TimeSpan.FromHours(73).TotalMilliseconds ||
            script.VersionCreatedAt > now - (long)TimeSpan.FromHours(24).TotalMilliseconds || script.AvailableAt > now)
            throw new InvalidDataException("The script has not completed the community waiting period.");
    }

    async Task<T> ReadAsync<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, CancellationToken cancellation_token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation_token);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            return await ReadResponseAsync(path, type, timeout.Token);
        }
        catch (OperationCanceledException error) when (!cancellation_token.IsCancellationRequested)
        {
            throw new TimeoutException("The community service did not respond in time.", error);
        }
    }

    async Task<T> ReadResponseAsync<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, CancellationToken cancellation_token)
    {
        using HttpResponseMessage response = await _http.GetAsync(SiteUrl + path, HttpCompletionOption.ResponseHeadersRead, cancellation_token);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("This script version is no longer available.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
            throw new InvalidDataException("The community response is too large.");
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellation_token);
        using var content = new MemoryStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellation_token)) != 0)
        {
            if (content.Length + read > MaxResponseBytes)
                throw new InvalidDataException("The community response is too large.");
            await content.WriteAsync(buffer.AsMemory(0, read), cancellation_token);
        }
        return JsonSerializer.Deserialize(content.ToArray(), type) ?? throw new InvalidDataException("The community response is empty.");
    }

    public void Dispose() => _http.Dispose();

    [GeneratedRegex("\\A[a-z0-9]{12}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ValidId();
}
