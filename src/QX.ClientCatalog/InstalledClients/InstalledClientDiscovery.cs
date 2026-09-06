using System.Security.Cryptography;
using System.Text.Json;
using Qx.Headers.Flash;
using Qx.Unity;

namespace Qx.ClientCatalog.InstalledClients;

internal sealed class InstalledClientDiscovery
{
    const string AirClient = "air";
    const string UnityClient = "unity";

    readonly HabboAirClientResolver _air;
    readonly HabboUnityClientResolver _unity;
    readonly string _launcher_data;
    readonly string _cache_root;
    readonly Dictionary<string, bool> _verified = new(StringComparer.Ordinal);

    public InstalledClientDiscovery(HttpClient http, string? launcher_data, string? cache_root)
    {
        ArgumentNullException.ThrowIfNull(http);
        _launcher_data = Path.GetFullPath(launcher_data ?? HabboAirClientResolver.DefaultLauncherDataPath());
        _cache_root = Path.GetFullPath(cache_root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QX"));
        _air = new HabboAirClientResolver(http, _launcher_data, Path.Combine(_cache_root, "swf"));
        _unity = new HabboUnityClientResolver(http, _launcher_data, Path.Combine(_cache_root, "unity"));
    }

    public IReadOnlyList<string> WatchRoots
    {
        get
        {
            string air_cache = Path.Combine(_cache_root, "swf");
            string unity_cache = Path.Combine(_cache_root, "unity");
            return new[]
            {
                _launcher_data,
                ExistingRoot(air_cache, _cache_root),
                ExistingRoot(unity_cache, _cache_root)
            }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        }
    }

    public IReadOnlyList<InstalledClientCandidate> Find()
    {
        var candidates = new List<InstalledClientCandidate>();
        FindAir(candidates);
        FindUnity(candidates);
        return candidates
            .GroupBy(candidate => CandidateKey(candidate), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => candidate.LastModified)
                .ThenByDescending(candidate => ParseVersion(candidate.Version))
                .First())
            .ToArray();
    }

    void FindAir(List<InstalledClientCandidate> candidates)
    {
        foreach (string? version in VersionHints(AirClient, Path.Combine(_cache_root, "swf")))
        {
            HabboAirRelease? release;
            try
            {
                release = _air.FindInstalled(version);
            }
            catch (Exception error) when (IsCandidateError(error))
            {
                continue;
            }

            if (release is null)
                continue;

            string path = Path.GetFullPath(release.SwfPath);
            if (!TryModified(path, out DateTimeOffset modified))
                continue;
            candidates.Add(new InstalledClientCandidate(
                InstalledClientFamily.Flash,
                release.Version,
                path,
                release.Source.ToString(),
                modified,
                Array.AsReadOnly(new[] { path })));
        }
    }

    void FindUnity(List<InstalledClientCandidate> candidates)
    {
        foreach (string? version in VersionHints(UnityClient, Path.Combine(_cache_root, "unity")))
        {
            HabboUnityRelease? release;
            try
            {
                release = _unity.FindInstalled(version);
            }
            catch (Exception error) when (IsCandidateError(error))
            {
                continue;
            }

            if (release is null)
                continue;

            string assembly = Path.GetFullPath(release.Client.GameAssemblyPath);
            string metadata = Path.GetFullPath(release.Client.MetadataPath);
            string[] files = [assembly, metadata];
            if (!TryModified(assembly, out DateTimeOffset modified))
                continue;
            candidates.Add(new InstalledClientCandidate(
                InstalledClientFamily.Unity,
                release.Version,
                Path.GetFullPath(release.Client.RootPath),
                release.Source.ToString(),
                modified,
                Array.AsReadOnly(files)));
        }
    }

    IEnumerable<string?> VersionHints(string client, string cache_path)
    {
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ReadInstallationVersions(client, versions);
        ReadDirectoryVersions(Path.Combine(_launcher_data, "downloads", client), versions);
        ReadDirectoryVersions(cache_path, versions);
        foreach (string version in versions)
            yield return version;
        yield return null;
    }

    void ReadInstallationVersions(string client, HashSet<string> versions)
    {
        string path = Path.Combine(_launcher_data, "versions.json");
        try
        {
            using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(path));
            if (!json.RootElement.TryGetProperty("installations", out JsonElement installations) ||
                installations.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement installation in installations.EnumerateArray())
            {
                if (!installation.TryGetProperty("client", out JsonElement client_element) ||
                    !string.Equals(client_element.GetString(), client, StringComparison.OrdinalIgnoreCase) ||
                    !installation.TryGetProperty("version", out JsonElement version_element))
                    continue;

                string? version = version_element.ValueKind switch
                {
                    JsonValueKind.String => version_element.GetString(),
                    JsonValueKind.Number => version_element.GetRawText(),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(version))
                    versions.Add(version);
            }
        }
        catch (Exception error) when (IsCandidateError(error) || error is JsonException)
        {
        }
    }

    static void ReadDirectoryVersions(string path, HashSet<string> versions)
    {
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(path))
                versions.Add(Path.GetFileName(directory));
        }
        catch (Exception error) when (IsCandidateError(error))
        {
        }
    }

    public bool TryVerify(InstalledClientCandidate candidate, out string revision)
    {
        revision = string.Empty;
        if (!TryHash(candidate, out string content_revision, out FileState[] before))
            return false;

        bool valid;
        if (_verified.TryGetValue(content_revision, out bool cached))
        {
            valid = cached;
        }
        else
        {
            valid = candidate.Family switch
            {
                InstalledClientFamily.Flash => VerifySwf(candidate),
                InstalledClientFamily.Unity => VerifyUnity(candidate),
                _ => false
            };
            _verified[content_revision] = valid;
        }

        if (!valid || !Unchanged(before))
            return false;
        revision = $"{CandidateKey(candidate)}:{content_revision}";
        return true;
    }

    static bool VerifySwf(InstalledClientCandidate candidate)
    {
        try
        {
            using SwfInfo swf = SwfLoader.Load(candidate.Files.Single());
            return swf.Flash.Tags.Count > 0;
        }
        catch (Exception error) when (IsCandidateError(error))
        {
            return false;
        }
    }

    static bool VerifyUnity(InstalledClientCandidate candidate)
    {
        try
        {
            UnityExecutableValidator.Validate(candidate.Files[0]);
            _ = new UnityHeaderExtractor().ExtractMetadata(candidate.Files[1]);
            return true;
        }
        catch (Exception error) when (IsCandidateError(error))
        {
            return false;
        }
    }

    static bool TryHash(
        InstalledClientCandidate candidate,
        out string revision,
        out FileState[] states)
    {
        revision = string.Empty;
        states = [];
        try
        {
            var hashes = new List<string>(candidate.Files.Count);
            var observed = new List<FileState>(candidate.Files.Count);
            foreach (string path in candidate.Files.Order(StringComparer.OrdinalIgnoreCase))
            {
                FileState state = State(path);
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete,
                    131072,
                    FileOptions.SequentialScan);
                hashes.Add(Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
                observed.Add(state);
            }
            states = observed.ToArray();
            revision = $"{candidate.Family.ToString().ToLowerInvariant()}:{string.Join(':', hashes)}";
            return true;
        }
        catch (Exception error) when (IsCandidateError(error))
        {
            return false;
        }
    }

    static bool Unchanged(IEnumerable<FileState> before)
    {
        try
        {
            return before.All(state => state == State(state.Path));
        }
        catch (Exception error) when (IsCandidateError(error))
        {
            return false;
        }
    }

    static FileState State(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new FileNotFoundException("Installed client file was not found.", path);
        return new FileState(Path.GetFullPath(path), file.Length, file.LastWriteTimeUtc.Ticks);
    }

    static bool TryModified(string path, out DateTimeOffset modified)
    {
        modified = DateTimeOffset.MinValue;
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
                return false;
            modified = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
            return true;
        }
        catch (Exception error) when (IsCandidateError(error))
        {
            return false;
        }
    }

    static bool IsCandidateError(Exception error) => error is
        IOException or
        UnauthorizedAccessException or
        InvalidDataException or
        ArgumentException or
        NotSupportedException;

    static string CandidateKey(InstalledClientCandidate candidate) =>
        $"{candidate.Family}:{candidate.Version}:{candidate.Path}";

    static long ParseVersion(string version) => long.TryParse(version, out long parsed) ? parsed : -1;

    static string ExistingRoot(string preferred, string fallback) =>
        Directory.Exists(preferred) ? preferred : fallback;

    readonly record struct FileState(string Path, long Length, long LastWriteTicks);
}
