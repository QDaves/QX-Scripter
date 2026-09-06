using System.IO.Compression;
using System.Text.Json;

namespace Qx.Unity;

public enum HabboUnitySource
{
    Launcher,
    Cache,
    Download
}

public sealed record HabboUnityManifest(string Version, Uri DownloadUrl);

public sealed record HabboUnityRelease(
    string Version,
    UnityClientLayout Client,
    HabboUnitySource Source,
    bool IsCurrent,
    Uri? DownloadUrl = null,
    string? FallbackReason = null);

public sealed class HabboUnityClientResolver
{
    const string ClientUrlsPath = "gamedata/clienturls";
    const string WindowsVersionProperty = "unity-windows-version";
    const string WindowsPathProperty = "unity-windows";
    const long MaximumArchiveBytes = 2_147_483_648;
    const long MaximumAssemblyBytes = 536_870_912;
    const long MaximumMetadataBytes = 536_870_912;

    readonly HttpClient _http;
    readonly string _launcher_data;
    readonly string _cache_root;

    public HabboUnityClientResolver(HttpClient http, string? launcher_data = null, string? cache_root = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _launcher_data = launcher_data ?? DefaultLauncherDataPath();
        _cache_root = cache_root ?? DefaultCachePath();
    }

    public static string DefaultLauncherDataPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Habbo Launcher");

    public static string DefaultCachePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QX", "unity");

    public async Task<HabboUnityManifest> GetLatestManifestAsync(
        Uri? hotel = null,
        CancellationToken cancellation_token = default)
    {
        hotel ??= new Uri("https://www.habbo.com/", UriKind.Absolute);
        if (!hotel.IsAbsoluteUri)
            throw new ArgumentException("Hotel URL must be absolute.", nameof(hotel));

        using HttpResponseMessage response = await SendWithRetryAsync(
            new Uri(hotel, ClientUrlsPath),
            cancellation_token).ConfigureAwait(false);
        await using Stream content = await response.Content.ReadAsStreamAsync(cancellation_token).ConfigureAwait(false);
        using JsonDocument json = await JsonDocument.ParseAsync(
            content,
            cancellationToken: cancellation_token).ConfigureAwait(false);
        return ParseManifest(json.RootElement);
    }

    public HabboUnityRelease? FindInstalled(string? version = null)
    {
        List<InstalledRelease> candidates = ReadLauncherInstallations();
        candidates.AddRange(ReadVersionFolders(
            Path.Combine(_launcher_data, "downloads", "unity"),
            HabboUnitySource.Launcher));
        candidates.AddRange(ReadVersionFolders(_cache_root, HabboUnitySource.Cache));

        foreach (InstalledRelease candidate in candidates
            .Where(candidate => version is null || candidate.Version == version)
            .OrderByDescending(candidate => candidate.LastModified)
            .ThenByDescending(candidate => ParseVersion(candidate.Version)))
        {
            try
            {
                UnityClientLayout client = UnityClientLayout.Locate(candidate.RootPath);
                return new HabboUnityRelease(candidate.Version, client, candidate.Source, false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            {
            }
        }
        return null;
    }

    public async Task<HabboUnityRelease> ResolveLatestAsync(
        Uri? hotel = null,
        CancellationToken cancellation_token = default)
    {
        HabboUnityRelease? fallback = FindInstalled();
        HabboUnityManifest manifest;
        try
        {
            manifest = await GetLatestManifestAsync(hotel, cancellation_token).ConfigureAwait(false);
        }
        catch (Exception error) when (fallback is not null && error is not OperationCanceledException)
        {
            return fallback with { FallbackReason = error.Message };
        }

        HabboUnityRelease? installed = FindInstalled(manifest.Version);
        if (installed is not null)
            return installed with { IsCurrent = true, DownloadUrl = manifest.DownloadUrl };

        UnityClientLayout client = await DownloadAsync(manifest, cancellation_token).ConfigureAwait(false);
        return new HabboUnityRelease(
            manifest.Version,
            client,
            HabboUnitySource.Download,
            true,
            manifest.DownloadUrl);
    }

    internal static HabboUnityManifest ParseManifest(JsonElement json)
    {
        string version = ReadRequiredString(json, WindowsVersionProperty);
        string path = ReadRequiredString(json, WindowsPathProperty);
        if (!Uri.TryCreate(path, UriKind.Absolute, out Uri? download_uri) || download_uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException($"Manifest property '{WindowsPathProperty}' is not a valid HTTPS URL.");
        return new HabboUnityManifest(version, download_uri);
    }

    async Task<UnityClientLayout> DownloadAsync(
        HabboUnityManifest manifest,
        CancellationToken cancellation_token)
    {
        string release_root = Path.Combine(_cache_root, SafeVersion(manifest.Version));
        if (Directory.Exists(release_root))
        {
            try
            {
                return UnityClientLayout.Locate(release_root);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            {
            }
        }

        Directory.CreateDirectory(release_root);
        string archive_path = Path.Combine(release_root, $"download-{Guid.NewGuid():N}.zip");
        string staging_root = Path.Combine(release_root, $"extract-{Guid.NewGuid():N}");
        string standalone_root = Path.Combine(release_root, "StandaloneWindows");

        try
        {
            using HttpResponseMessage response = await SendWithRetryAsync(
                manifest.DownloadUrl,
                cancellation_token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength > MaximumArchiveBytes)
                throw new InvalidDataException($"Unity archive is too large ({response.Content.Headers.ContentLength} bytes).");

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

            Directory.CreateDirectory(staging_root);
            using (ZipArchive archive = ZipFile.OpenRead(archive_path))
            {
                ZipArchiveEntry assembly = FindEntry(archive, "GameAssembly.dll");
                ZipArchiveEntry metadata = FindEntry(archive, "global-metadata.dat");
                await ExtractAsync(
                    assembly,
                    Path.Combine(staging_root, "GameAssembly.dll"),
                    MaximumAssemblyBytes,
                    cancellation_token).ConfigureAwait(false);
                await ExtractAsync(
                    metadata,
                    Path.Combine(
                        staging_root,
                        "habbo2020-global-prod_Data",
                        "il2cpp_data",
                        "Metadata",
                        "global-metadata.dat"),
                    MaximumMetadataBytes,
                    cancellation_token).ConfigureAwait(false);
            }

            UnityClientLayout.Locate(staging_root);
            if (Directory.Exists(standalone_root))
                Directory.Delete(standalone_root, true);
            Directory.Move(staging_root, standalone_root);
            return UnityClientLayout.Locate(release_root);
        }
        finally
        {
            TryDelete(archive_path);
            TryDeleteDirectory(staging_root);
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
                if (!installation.TryGetProperty("client", out JsonElement client) || client.GetString() != "unity")
                    continue;
                string? version = OptionalString(installation, "version");
                string? root = OptionalString(installation, "path");
                if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(root))
                    continue;
                long modified = installation.TryGetProperty("lastModified", out JsonElement last_modified) &&
                    last_modified.TryGetInt64(out long parsed_modified)
                    ? parsed_modified
                    : 0;
                releases.Add(new InstalledRelease(version, root, HabboUnitySource.Launcher, modified));
            }
            return releases;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    static IEnumerable<InstalledRelease> ReadVersionFolders(string root, HabboUnitySource source)
    {
        if (!Directory.Exists(root))
            yield break;

        string[] directories;
        try
        {
            directories = Directory.GetDirectories(root);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (string directory in directories)
        {
            long modified;
            try
            {
                modified = new DateTimeOffset(Directory.GetLastWriteTimeUtc(directory)).ToUnixTimeMilliseconds();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                modified = 0;
            }
            yield return new InstalledRelease(Path.GetFileName(directory), directory, source, modified);
        }
    }

    static ZipArchiveEntry FindEntry(ZipArchive archive, string name) =>
        archive.Entries
            .Where(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName.Count(character => character is '/' or '\\'))
            .FirstOrDefault()
        ?? throw new InvalidDataException($"Unity archive does not contain {name}.");

    static async Task ExtractAsync(
        ZipArchiveEntry entry,
        string destination_path,
        long maximum_bytes,
        CancellationToken cancellation_token)
    {
        if (entry.Length <= 0 || entry.Length > maximum_bytes)
            throw new InvalidDataException($"Invalid archive entry size for {entry.Name}: {entry.Length} bytes.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination_path)!);
        await using Stream source = entry.Open();
        await using var destination = new FileStream(
            destination_path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            131072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await CopyWithLimitAsync(source, destination, maximum_bytes, cancellation_token).ConfigureAwait(false);
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
            throw new InvalidDataException($"Invalid Unity version '{version}'.");
        return version;
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

    static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
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
        string RootPath,
        HabboUnitySource Source,
        long LastModified);
}
