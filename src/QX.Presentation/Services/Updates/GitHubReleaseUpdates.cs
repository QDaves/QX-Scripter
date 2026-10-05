using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Qx.Presentation.Services.Updates;

/// <summary>Represents a published release of the application.</summary>
/// <param name="Tag">The release tag, for example <c>v1.2.3</c>.</param>
/// <param name="Version">The normalized <c>major.minor.patch</c> version parsed from the tag.</param>
/// <param name="Name">The release title with control characters and extra whitespace removed, or the tag when the title is empty or longer than 120 characters.</param>
/// <param name="Uri">The URL of the release page, or <see langword="null"/> for a release installed through the G-ExtensionStore.</param>
public sealed record Release(string Tag, string Version, string Name, Uri? Uri);

/// <summary>Provides update checks against the project's GitHub releases and its G-ExtensionStore entry.</summary>
public static class GitHubReleaseUpdates
{
    private const int MaxResponseBytes = 1024 * 1024;

    /// <summary>Requests the release list from the GitHub API and gets the highest released version.</summary>
    /// <remarks>
    /// Only the first 100 releases are read. Drafts and tags that are not a plain <c>major.minor.patch</c>
    /// version, with an optional <c>v</c> prefix, are ignored. Responses larger than 1 MiB are rejected.
    /// </remarks>
    /// <param name="http">The HTTP client used to send the request.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// The highest release, or <see langword="null"/> when the request fails, the response is invalid or
    /// no release matched.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="http"/> is <see langword="null"/>.</exception>
    public static async Task<Release?> GetLatestAsync(
        HttpClient http,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);

        using var request = new HttpRequestMessage(HttpMethod.Get, ProjectLinks.ReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        using JsonDocument? document = await ReadJsonAsync(http, request, cancellationToken).ConfigureAwait(false);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Array } root)
            return null;

        Release? latest = null;
        ReleaseNumber latest_version = default;
        foreach (JsonElement entry in root.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                IsTrue(entry, "draft") ||
                !entry.TryGetProperty("tag_name", out JsonElement tag_element) ||
                tag_element.ValueKind != JsonValueKind.String ||
                !TryReleaseVersion(tag_element.GetString(), out ReleaseNumber version) ||
                latest is not null && version.CompareTo(latest_version) <= 0)
            {
                continue;
            }
            string tag = tag_element.GetString()!.Trim();
            string name = entry.TryGetProperty("name", out JsonElement name_element) &&
                name_element.ValueKind == JsonValueKind.String
                ? CleanName(name_element.GetString(), tag)
                : tag;
            latest = new Release(tag, version.Text, name, ProjectLinks.Release(tag));
            latest_version = version;
        }
        return latest;
    }

    /// <summary>Requests the extension list of the G-ExtensionStore and gets the version it offers of the application.</summary>
    /// <remarks>
    /// This is the list G-Earth shows, read from the store's current branch. An entry marked as outdated, or one
    /// whose version is not a plain <c>major.minor.patch</c>, gives no release. Responses larger than 1 MiB are
    /// rejected.
    /// </remarks>
    /// <param name="http">The HTTP client used to send the request.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    /// The release the store offers, or <see langword="null"/> when the request fails, the response is invalid or
    /// the store has no usable entry.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="http"/> is <see langword="null"/>.</exception>
    public static async Task<Release?> GetStoreReleaseAsync(
        HttpClient http,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);

        using var request = new HttpRequestMessage(HttpMethod.Get, ProjectLinks.StoreExtensions);
        using JsonDocument? document = await ReadJsonAsync(http, request, cancellationToken).ConfigureAwait(false);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Array } root)
            return null;

        foreach (JsonElement entry in root.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("title", out JsonElement title) ||
                title.ValueKind != JsonValueKind.String ||
                title.GetString() != ProjectLinks.StoreTitle)
            {
                continue;
            }
            if (IsTrue(entry, "isOutdated") ||
                !entry.TryGetProperty("version", out JsonElement version_element) ||
                version_element.ValueKind != JsonValueKind.String ||
                !TryReleaseVersion(version_element.GetString(), out ReleaseNumber version))
            {
                return null;
            }
            string tag = "v" + version.Text;
            return new Release(tag, version.Text, tag, null);
        }
        return null;
    }

    /// <summary>Gets whether the user should be told about a release.</summary>
    /// <remarks>
    /// <see langword="true"/> when the release was not the last one notified and its version is higher than
    /// the installed version. An installed version that is not <c>major.minor.patch</c> or
    /// <c>major.minor.patch.0</c> never triggers a notification.
    /// </remarks>
    /// <param name="installedVersion">The version of the running application.</param>
    /// <param name="lastNotifiedRelease">The tag of the release the user was last told about, or <see langword="null"/> for none.</param>
    /// <param name="release">The available release.</param>
    /// <returns><see langword="true"/> when a notification should be shown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="release"/> is <see langword="null"/>.</exception>
    public static bool ShouldNotify(
        string installedVersion,
        string? lastNotifiedRelease,
        Release release)
    {
        ArgumentNullException.ThrowIfNull(release);
        return !string.Equals(lastNotifiedRelease, release.Tag, StringComparison.OrdinalIgnoreCase)
            && TryInstalledVersion(installedVersion, out ReleaseNumber installed)
            && TryReleaseVersion(release.Tag, out ReleaseNumber available)
            && available.CompareTo(installed) > 0;
    }

    private static async Task<JsonDocument?> ReadJsonAsync(
        HttpClient http,
        HttpRequestMessage request,
        CancellationToken cancellation_token)
    {
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("QXScripter", ProductVersion.Current));
        try
        {
            using HttpResponseMessage response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellation_token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxResponseBytes)
                return null;
            byte[]? json = await ReadBoundedAsync(response.Content, cancellation_token).ConfigureAwait(false);
            return json is null ? null : JsonDocument.Parse(json);
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException)
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        HttpContent content,
        CancellationToken cancellation_token)
    {
        await using Stream source = await content.ReadAsStreamAsync(cancellation_token).ConfigureAwait(false);
        using var destination = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellation_token).ConfigureAwait(false);
            if (read == 0)
                return destination.ToArray();
            if (destination.Length + read > MaxResponseBytes)
                return null;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellation_token).ConfigureAwait(false);
        }
    }

    private static bool IsTrue(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    private static string CleanName(string? value, string fallback)
    {
        string printable = new((value ?? "")
            .Where(character => !char.IsControl(character) || char.IsWhiteSpace(character))
            .ToArray());
        string name = string.Join(' ', printable.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));
        return name.Length is > 0 and <= 120 ? name : fallback;
    }

    private static bool TryReleaseVersion(string? value, out ReleaseNumber version) =>
        TryVersion(value, true, out version);

    private static bool TryInstalledVersion(string? value, out ReleaseNumber version) =>
        TryVersion(value, false, out version);

    private static bool TryVersion(string? value, bool release_tag, out ReleaseNumber version)
    {
        version = default;
        string text = value?.Trim() ?? "";
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];

        string[] parts = text.Split('.');
        if (parts.Length != 3 && (release_tag || parts.Length != 4))
            return false;
        if (!TryPart(parts[0], out int major) ||
            !TryPart(parts[1], out int minor) ||
            !TryPart(parts[2], out int patch) ||
            parts.Length == 4 && (!TryPart(parts[3], out int revision) || revision != 0))
        {
            return false;
        }

        version = new ReleaseNumber(major, minor, patch);
        return true;
    }

    private static bool TryPart(string value, out int part)
    {
        part = 0;
        return value.Length > 0
            && (value.Length == 1 || value[0] != '0')
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out part);
    }

    private readonly record struct ReleaseNumber(int Major, int Minor, int Patch) : IComparable<ReleaseNumber>
    {
        public string Text => $"{Major}.{Minor}.{Patch}";

        public int CompareTo(ReleaseNumber other)
        {
            int major = Major.CompareTo(other.Major);
            if (major != 0)
                return major;
            int minor = Minor.CompareTo(other.Minor);
            return minor != 0 ? minor : Patch.CompareTo(other.Patch);
        }
    }
}
