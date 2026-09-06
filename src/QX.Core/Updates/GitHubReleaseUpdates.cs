using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Qx.Updates;

public sealed record GitHubRelease(string Tag, string Version, string Name, Uri Uri);

public static class GitHubReleaseUpdates
{
    private const int MaxResponseBytes = 1024 * 1024;

    public static async Task<GitHubRelease?> GetLatestAsync(
        HttpClient http,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(http);

        using var request = new HttpRequestMessage(HttpMethod.Get, ProjectLinks.ReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("QXScripter", ProductVersion.Current));
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

        try
        {
            using HttpResponseMessage response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellation_token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxResponseBytes)
                return null;

            byte[]? json = await ReadBoundedAsync(response.Content, cancellation_token).ConfigureAwait(false);
            if (json is null)
                return null;

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
                return null;
            GitHubRelease? latest = null;
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
                latest = new GitHubRelease(tag, version.Text, name, ProjectLinks.Release(tag));
                latest_version = version;
            }
            return latest;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool ShouldNotify(
        string installed_version,
        string? last_notified_release,
        GitHubRelease release)
    {
        ArgumentNullException.ThrowIfNull(release);
        return !string.Equals(last_notified_release, release.Tag, StringComparison.OrdinalIgnoreCase)
            && TryInstalledVersion(installed_version, out ReleaseNumber installed)
            && TryReleaseVersion(release.Tag, out ReleaseNumber available)
            && available.CompareTo(installed) > 0;
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
            destination.Write(buffer, 0, read);
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
