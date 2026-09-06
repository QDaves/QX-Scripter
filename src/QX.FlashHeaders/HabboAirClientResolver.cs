using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Qx.Headers.Flash;

public enum HabboAirSource
{
    Launcher,
    Cache,
    Download
}

public sealed record HabboAirManifest(string Version, Uri DownloadUrl);

public sealed record HabboAirRelease(
    string Version,
    string SwfPath,
    HabboAirSource Source,
    bool IsCurrent,
    Uri? DownloadUrl = null,
    string? FallbackReason = null);

public sealed class HabboAirClientResolver
{
    const string ClientUrlsPath = "gamedata/clienturls";
    const string WindowsVersionProperty = "flash-windows-version";
    const string WindowsPathProperty = "flash-windows";
    const string SwfFileName = "HabboAir.swf";
    const long MaximumArchiveBytes = 1_073_741_824;
    const long MaximumSwfBytes = 536_870_912;

    readonly HttpClient _http;
    readonly string _launcher_data;
    readonly string _cache_root;

    public HabboAirClientResolver(HttpClient http, string? launcher_data = null, string? cache_root = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _launcher_data = launcher_data ?? DefaultLauncherDataPath();
        _cache_root = cache_root ?? DefaultCachePath();
    }

    public static string DefaultLauncherDataPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Habbo Launcher");

    public static string DefaultCachePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QX", "swf");

    public async Task<HabboAirManifest> GetLatestManifestAsync(
        Uri? hotel = null,
        CancellationToken cancellation_token = default)
    {
        hotel ??= new Uri("https://www.habbo.com/", UriKind.Absolute);
        if (!hotel.IsAbsoluteUri)
            throw new ArgumentException("Hotel URL must be absolute.", nameof(hotel));

        Uri manifest_uri = new(hotel, ClientUrlsPath);
        using HttpResponseMessage response = await SendWithRetryAsync(manifest_uri, cancellation_token).ConfigureAwait(false);
        await using Stream content = await response.Content.ReadAsStreamAsync(cancellation_token).ConfigureAwait(false);
        using JsonDocument json = await JsonDocument.ParseAsync(content, cancellationToken: cancellation_token).ConfigureAwait(false);
        return ParseManifest(json.RootElement);
    }

    public HabboAirRelease? FindInstalled(string? version = null)
    {
        List<InstalledRelease> candidates = ReadLauncherInstallations();
        candidates.AddRange(ReadLauncherDownloadFolders());
        candidates.AddRange(ReadCacheFolders());

        return candidates
            .Where(candidate => version is null || candidate.Version == version)
            .Where(candidate => File.Exists(candidate.SwfPath))
            .OrderByDescending(candidate => candidate.LastModified)
            .ThenByDescending(candidate => ParseVersion(candidate.Version))
            .Select(candidate => new HabboAirRelease(
                candidate.Version,
                candidate.SwfPath,
                candidate.Source,
                false))
            .FirstOrDefault();
    }

    public async Task<HabboAirRelease> ResolveLatestAsync(
        Uri? hotel = null,
        CancellationToken cancellation_token = default)
    {
        HabboAirRelease? fallback = FindInstalled();
        HabboAirManifest manifest;
        try
        {
            manifest = await GetLatestManifestAsync(hotel, cancellation_token).ConfigureAwait(false);
        }
        catch (Exception error) when (fallback is not null && error is not OperationCanceledException)
        {
            return fallback with { FallbackReason = error.Message };
        }

        HabboAirRelease? installed = FindInstalled(manifest.Version);
        if (installed is not null)
            return installed with { IsCurrent = true, DownloadUrl = manifest.DownloadUrl };

        string swf_path = await DownloadAsync(manifest, cancellation_token).ConfigureAwait(false);
        return new HabboAirRelease(
            manifest.Version,
            swf_path,
            HabboAirSource.Download,
            true,
            manifest.DownloadUrl);
    }

    internal static HabboAirManifest ParseManifest(JsonElement json)
    {
        string? version = ReadRequiredString(json, WindowsVersionProperty);
        string? path = ReadRequiredString(json, WindowsPathProperty);
        if (!Uri.TryCreate(path, UriKind.Absolute, out Uri? download_uri) || download_uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException($"Manifest property '{WindowsPathProperty}' is not a valid HTTPS URL.");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            RuntimeInformation.OSArchitecture == Architecture.X64 &&
            ParseVersion(version) >= 143 &&
            download_uri.AbsolutePath.EndsWith("/HabboWin.zip", StringComparison.OrdinalIgnoreCase))
        {
            download_uri = new Uri(download_uri.AbsoluteUri[..^"HabboWin.zip".Length] + "HabboWin_x64.zip");
        }

        return new HabboAirManifest(version, download_uri);
    }

    async Task<string> DownloadAsync(HabboAirManifest manifest, CancellationToken cancellation_token)
    {
        string version_root = Path.Combine(_cache_root, SafeVersion(manifest.Version));
        string swf_path = Path.Combine(version_root, SwfFileName);
        if (File.Exists(swf_path))
            return swf_path;

        Directory.CreateDirectory(version_root);
        string archive_path = Path.Combine(version_root, $"download-{Guid.NewGuid():N}.zip");
        string temporary_swf = Path.Combine(version_root, $"{SwfFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            using HttpResponseMessage response = await SendWithRetryAsync(manifest.DownloadUrl, cancellation_token).ConfigureAwait(false);
            long? content_length = response.Content.Headers.ContentLength;
            if (content_length > MaximumArchiveBytes)
                throw new InvalidDataException($"AIR archive is too large ({content_length} bytes).");

            await using (Stream source = await response.Content.ReadAsStreamAsync(cancellation_token).ConfigureAwait(false))
            await using (var destination = new FileStream(
                archive_path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                131072,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await CopyWithLimitAsync(source, destination, MaximumArchiveBytes, cancellation_token).ConfigureAwait(false);
            }

            using (ZipArchive archive = ZipFile.OpenRead(archive_path))
            {
                ZipArchiveEntry entry = archive.Entries
                    .Where(candidate => string.Equals(candidate.Name, SwfFileName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(candidate => candidate.FullName.Count(character => character is '/' or '\\'))
                    .FirstOrDefault()
                    ?? throw new InvalidDataException($"AIR archive does not contain {SwfFileName}.");

                if (entry.Length <= 8 || entry.Length > MaximumSwfBytes)
                    throw new InvalidDataException($"Invalid SWF size in AIR archive: {entry.Length} bytes.");

                await using Stream source = entry.Open();
                await using var destination = new FileStream(
                    temporary_swf,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    131072,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await CopyWithLimitAsync(source, destination, MaximumSwfBytes, cancellation_token).ConfigureAwait(false);
            }

            ValidateSwfHeader(temporary_swf);
            File.Move(temporary_swf, swf_path, true);
            return swf_path;
        }
        finally
        {
            TryDelete(archive_path);
            TryDelete(temporary_swf);
        }
    }

    async Task<HttpResponseMessage> SendWithRetryAsync(Uri uri, CancellationToken cancellation_token)
    {
        Exception? last_error = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("Habbo-Launcher/1.0.80");
                HttpResponseMessage response = await _http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellation_token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return response;

                last_error = new HttpRequestException(
                    $"HTTP {(int)response.StatusCode} while downloading '{uri}'.",
                    null,
                    response.StatusCode);
                response.Dispose();
            }
            catch (Exception error) when (error is HttpRequestException or IOException)
            {
                last_error = error;
            }

            if (attempt < 3)
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellation_token).ConfigureAwait(false);
        }

        throw last_error ?? new HttpRequestException($"Unable to download '{uri}'.");
    }

    List<InstalledRelease> ReadLauncherInstallations()
    {
        string versions_path = Path.Combine(_launcher_data, "versions.json");
        if (!File.Exists(versions_path))
            return [];

        try
        {
            using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(versions_path));
            if (!json.RootElement.TryGetProperty("installations", out JsonElement installations) ||
                installations.ValueKind != JsonValueKind.Array)
                return [];

            var releases = new List<InstalledRelease>();
            foreach (JsonElement installation in installations.EnumerateArray())
            {
                if (!installation.TryGetProperty("client", out JsonElement client) || client.GetString() != "air")
                    continue;

                string? version = OptionalString(installation, "version");
                string? root = OptionalString(installation, "path");
                if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(root))
                    continue;

                long modified = installation.TryGetProperty("lastModified", out JsonElement last_modified) &&
                    last_modified.TryGetInt64(out long parsed_modified)
                    ? parsed_modified
                    : 0;
                releases.Add(new InstalledRelease(
                    version,
                    Path.Combine(root, SwfFileName),
                    HabboAirSource.Launcher,
                    modified));
            }
            return releases;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    IEnumerable<InstalledRelease> ReadLauncherDownloadFolders() =>
        ReadVersionFolders(Path.Combine(_launcher_data, "downloads", "air"), HabboAirSource.Launcher);

    IEnumerable<InstalledRelease> ReadCacheFolders() =>
        ReadVersionFolders(_cache_root, HabboAirSource.Cache);

    static IEnumerable<InstalledRelease> ReadVersionFolders(string root, HabboAirSource source)
    {
        if (!Directory.Exists(root))
            yield break;

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(root).ToArray();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (string directory in directories)
        {
            string swf_path = Path.Combine(directory, SwfFileName);
            if (!File.Exists(swf_path))
                continue;

            long modified;
            try
            {
                modified = new DateTimeOffset(File.GetLastWriteTimeUtc(swf_path)).ToUnixTimeMilliseconds();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                modified = 0;
            }
            yield return new InstalledRelease(Path.GetFileName(directory), swf_path, source, modified);
        }
    }

    static string ReadRequiredString(JsonElement json, string property)
    {
        string? value = OptionalString(json, property);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Manifest property '{property}' is missing.");
        return value;
    }

    static string? OptionalString(JsonElement json, string property)
    {
        if (!json.TryGetProperty(property, out JsonElement value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    static int ParseVersion(string version) => int.TryParse(version, out int parsed) ? parsed : -1;

    static string SafeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version) ||
            version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            version is "." or "..")
            throw new InvalidDataException($"Invalid AIR version '{version}'.");
        return version;
    }

    static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long maximum_bytes,
        CancellationToken cancellation_token)
    {
        byte[] buffer = new byte[131072];
        long written = 0;
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellation_token).ConfigureAwait(false);
            if (read == 0)
                break;
            written += read;
            if (written > maximum_bytes)
                throw new InvalidDataException($"Downloaded content exceeds {maximum_bytes} bytes.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellation_token).ConfigureAwait(false);
        }
    }

    static void ValidateSwfHeader(string path)
    {
        Span<byte> header = stackalloc byte[8];
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        file.ReadExactly(header);
        byte marker = (byte)(header[0] & ~0x20);
        if (header[1] != (byte)'W' || header[2] != (byte)'S' || marker is not ((byte)'F') and not ((byte)'C') and not ((byte)'Z'))
            throw new InvalidDataException($"Extracted {SwfFileName} has an invalid SWF header.");
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    sealed record InstalledRelease(
        string Version,
        string SwfPath,
        HabboAirSource Source,
        long LastModified);
}
