using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qx;
using Qx.Messages;

namespace Qx.ClientCatalog;

public sealed class HeaderCatalogStore
{
    public const int FormatVersion = 3;
    const long MaximumCacheBytes = 64L * 1024 * 1024;

    readonly ConcurrentDictionary<string, Lazy<Task<HeaderCatalogCacheResult>>> _operations =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    readonly string _root;
    readonly long _maximum_cache_bytes;
    readonly CancellationToken _lifetime_token;

    public HeaderCatalogStore(string root, CancellationToken lifetime_token = default)
        : this(root, MaximumCacheBytes, lifetime_token)
    {
    }

    internal HeaderCatalogStore(
        string root,
        long maximum_cache_bytes,
        CancellationToken lifetime_token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (maximum_cache_bytes <= 0 || maximum_cache_bytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximum_cache_bytes));
        _root = Path.GetFullPath(root);
        _maximum_cache_bytes = maximum_cache_bytes;
        _lifetime_token = lifetime_token;
        RejectReparseAncestors(_root);
        Directory.CreateDirectory(_root);
        RejectReparseAncestors(_root);
    }

    public string RootPath => _root;

    internal async Task<HeaderCatalogCacheResult?> TryGetAsync(
        HeaderCatalogKey key,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellation_token.ThrowIfCancellationRequested();
        CacheRead read = await ReadAsync(CachePath(key), key, cancellation_token).ConfigureAwait(false);
        return read.Result;
    }

    public async Task<HeaderCatalogCacheResult> GetOrCreateAsync(
        HeaderCatalogKey key,
        Func<CancellationToken, Task<HeaderCatalogSnapshot>> create_catalog,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(create_catalog);
        cancellation_token.ThrowIfCancellationRequested();
        string path = CachePath(key);
        Lazy<Task<HeaderCatalogCacheResult>> operation = _operations.GetOrAdd(
            path,
            _ => new Lazy<Task<HeaderCatalogCacheResult>>(
                () => LoadOrCreateAsync(path, key, create_catalog, _lifetime_token),
                LazyThreadSafetyMode.ExecutionAndPublication));
        Task<HeaderCatalogCacheResult> task = operation.Value;
        _ = task.ContinueWith(
            (_, state) =>
            {
                var values = ((
                    ConcurrentDictionary<string, Lazy<Task<HeaderCatalogCacheResult>>> Operations,
                    string Path,
                    Lazy<Task<HeaderCatalogCacheResult>> Operation))state!;
                ((ICollection<KeyValuePair<string, Lazy<Task<HeaderCatalogCacheResult>>>>)values.Operations)
                    .Remove(new KeyValuePair<string, Lazy<Task<HeaderCatalogCacheResult>>>(values.Path, values.Operation));
            },
            (_operations, path, operation),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return await task.WaitAsync(cancellation_token).ConfigureAwait(false);
    }

    async Task<HeaderCatalogCacheResult> LoadOrCreateAsync(
        string path,
        HeaderCatalogKey key,
        Func<CancellationToken, Task<HeaderCatalogSnapshot>> create_catalog,
        CancellationToken cancellation_token)
    {
        CacheRead read = await ReadAsync(path, key, cancellation_token).ConfigureAwait(false);
        if (read.Result is not null)
            return read.Result;

        cancellation_token.ThrowIfCancellationRequested();
        HeaderCatalogSnapshot created = await create_catalog(cancellation_token).ConfigureAwait(false)
            ?? throw new InvalidDataException("The header catalog factory returned no catalog.");
        HeaderCatalogSnapshot catalog = Normalize(key, created);
        byte[] body = SerializeCatalog(key, catalog, _maximum_cache_bytes);
        string content_sha256 = Convert.ToHexStringLower(SHA256.HashData(body));
        byte[] envelope = SerializeEnvelope(body, content_sha256, _maximum_cache_bytes);
        await PublishAsync(path, envelope, cancellation_token).ConfigureAwait(false);
        return new HeaderCatalogCacheResult(
            catalog,
            read.Corrupt ? HeaderCatalogCacheState.Rebuilt : HeaderCatalogCacheState.Created,
            content_sha256);
    }

    async Task<CacheRead> ReadAsync(
        string path,
        HeaderCatalogKey expected_key,
        CancellationToken cancellation_token)
    {
        byte[] content;
        try
        {
            using var input = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                65536,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (input.Length <= 0 || input.Length > _maximum_cache_bytes)
                return CacheRead.Invalid;
            content = GC.AllocateUninitializedArray<byte>(checked((int)input.Length));
            await input.ReadExactlyAsync(content, cancellation_token).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return CacheRead.Missing;
        }
        catch (DirectoryNotFoundException)
        {
            return CacheRead.Missing;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;
            RequireProperties(root, "formatVersion", "contentSha256", "catalog");
            if (!root.GetProperty("formatVersion").TryGetInt32(out int format_version) ||
                format_version != FormatVersion)
            {
                return CacheRead.Invalid;
            }
            string content_sha256 = HeaderCatalogKey.NormalizeHash(
                ReadString(root, "contentSha256"),
                "contentSha256");
            JsonElement catalog_element = root.GetProperty("catalog");
            byte[] body = Encoding.UTF8.GetBytes(catalog_element.GetRawText());
            string actual_sha256 = Convert.ToHexStringLower(SHA256.HashData(body));
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(content_sha256),
                    Encoding.ASCII.GetBytes(actual_sha256)))
            {
                return CacheRead.Invalid;
            }

            (HeaderCatalogKey key, HeaderCatalogSnapshot catalog) = ReadCatalog(catalog_element);
            if (key != expected_key)
                return CacheRead.Invalid;
            byte[] canonical_body = SerializeCatalog(key, catalog, _maximum_cache_bytes);
            byte[] canonical_envelope = SerializeEnvelope(
                canonical_body,
                content_sha256,
                _maximum_cache_bytes);
            if (!content.AsSpan().SequenceEqual(canonical_envelope))
                return CacheRead.Invalid;
            return new CacheRead(
                new HeaderCatalogCacheResult(catalog, HeaderCatalogCacheState.Hit, content_sha256),
                false);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException or OverflowException)
        {
            return CacheRead.Invalid;
        }
    }

    static (HeaderCatalogKey Key, HeaderCatalogSnapshot Catalog) ReadCatalog(JsonElement value)
    {
        RequireProperties(
            value,
            "client",
            "sourceSha256",
            "nameDatabaseSha256",
            "extractorRevision",
            "provenance",
            "clientBuildIds",
            "flashMarketplaceLayout",
            "entries");
        if (!Enum.TryParse(
                ReadString(value, "client"),
                true,
                out ClientType client))
        {
            throw new InvalidDataException("The cached client type is invalid.");
        }
        JsonElement provenance_value = value.GetProperty("provenance");
        RequireProperties(provenance_value, "clientVersion", "source", "sourceRevision");
        var provenance = new HeaderCatalogProvenance(
            ReadString(provenance_value, "clientVersion"),
            ReadString(provenance_value, "source"),
            ReadNullableString(provenance_value, "sourceRevision"));
        var key = new HeaderCatalogKey(
            client,
            ReadString(value, "sourceSha256"),
            ReadString(value, "nameDatabaseSha256"),
            ReadString(value, "extractorRevision"),
            provenance);

        JsonElement build_ids_value = value.GetProperty("clientBuildIds");
        if (build_ids_value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The cached client build IDs are invalid.");
        var client_build_ids = new List<string>();
        foreach (JsonElement build_id in build_ids_value.EnumerateArray())
        {
            if (client_build_ids.Count >= 32 || build_id.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("A cached client build ID is invalid.");
            client_build_ids.Add(build_id.GetString()
                ?? throw new InvalidDataException("A cached client build ID is invalid."));
        }

        JsonElement entries_value = value.GetProperty("entries");
        if (entries_value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The cached header entries are invalid.");
        var entries = new List<HeaderCatalogEntry>();
        foreach (JsonElement entry_value in entries_value.EnumerateArray())
        {
            if (entries.Count >= 131072)
                throw new InvalidDataException("The cached header entry limit was exceeded.");
            RequireProperties(entry_value, "direction", "headerId", "name", "aliases");
            Direction direction = ReadString(entry_value, "direction") switch
            {
                "in" => Direction.In,
                "out" => Direction.Out,
                _ => throw new InvalidDataException("The cached header direction is invalid.")
            };
            if (!entry_value.GetProperty("headerId").TryGetUInt16(out ushort header_id))
                throw new InvalidDataException("The cached header ID is invalid.");
            JsonElement aliases_value = entry_value.GetProperty("aliases");
            if (aliases_value.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("The cached header aliases are invalid.");
            var aliases = new List<string>();
            foreach (JsonElement alias in aliases_value.EnumerateArray())
            {
                if (aliases.Count >= 64 || alias.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("A cached header alias is invalid.");
                aliases.Add(alias.GetString()
                    ?? throw new InvalidDataException("A cached header alias is invalid."));
            }
            entries.Add(new HeaderCatalogEntry(
                direction,
                header_id,
                ReadNullableString(entry_value, "name"),
                aliases));
        }
        FlashMarketplaceWireLayout flash_marketplace_layout =
            ReadString(value, "flashMarketplaceLayout") switch
            {
                "unknown" => FlashMarketplaceWireLayout.Unknown,
                "legacy" => FlashMarketplaceWireLayout.Legacy,
                "modern" => FlashMarketplaceWireLayout.Modern,
                _ => throw new InvalidDataException("The cached Flash marketplace layout is invalid.")
            };
        return (key, new HeaderCatalogSnapshot(
            provenance,
            entries,
            client_build_ids,
            flash_marketplace_layout));
    }

    static HeaderCatalogSnapshot Normalize(HeaderCatalogKey key, HeaderCatalogSnapshot value)
    {
        if (value.Provenance != key.Provenance)
            throw new InvalidDataException("The extracted header catalog provenance does not match its cache key.");
        return new HeaderCatalogSnapshot(
            new HeaderCatalogProvenance(
                value.Provenance.ClientVersion,
                value.Provenance.Source,
                value.Provenance.SourceRevision),
            value.Entries.Select(entry =>
                new HeaderCatalogEntry(
                    entry.Direction,
                    entry.HeaderId,
                    entry.Name,
                    entry.Aliases)),
            value.ClientBuildIds,
            value.FlashMarketplaceLayout);
    }

    static byte[] SerializeCatalog(
        HeaderCatalogKey key,
        HeaderCatalogSnapshot catalog,
        long maximum_bytes)
    {
        using var output = new LimitedMemoryStream(maximum_bytes);
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("client", key.Client.ToString().ToLowerInvariant());
            writer.WriteString("sourceSha256", key.SourceSha256);
            writer.WriteString("nameDatabaseSha256", key.NameDatabaseSha256);
            writer.WriteString("extractorRevision", key.ExtractorRevision);
            writer.WritePropertyName("provenance");
            writer.WriteStartObject();
            writer.WriteString("clientVersion", catalog.Provenance.ClientVersion);
            writer.WriteString("source", catalog.Provenance.Source);
            if (catalog.Provenance.SourceRevision is null)
                writer.WriteNull("sourceRevision");
            else
                writer.WriteString("sourceRevision", catalog.Provenance.SourceRevision);
            writer.WriteEndObject();
            writer.WritePropertyName("clientBuildIds");
            writer.WriteStartArray();
            foreach (string build_id in catalog.ClientBuildIds)
                writer.WriteStringValue(build_id);
            writer.WriteEndArray();
            writer.WriteString(
                "flashMarketplaceLayout",
                catalog.FlashMarketplaceLayout switch
                {
                    FlashMarketplaceWireLayout.Unknown => "unknown",
                    FlashMarketplaceWireLayout.Legacy => "legacy",
                    FlashMarketplaceWireLayout.Modern => "modern",
                    _ => throw new InvalidDataException("The Flash marketplace layout is invalid.")
                });
            writer.WritePropertyName("entries");
            writer.WriteStartArray();
            foreach (HeaderCatalogEntry entry in catalog.Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("direction", entry.Direction == Direction.In ? "in" : "out");
                writer.WriteNumber("headerId", entry.HeaderId);
                if (entry.Name is null)
                    writer.WriteNull("name");
                else
                    writer.WriteString("name", entry.Name);
                writer.WritePropertyName("aliases");
                writer.WriteStartArray();
                foreach (string alias in entry.Aliases)
                    writer.WriteStringValue(alias);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    static byte[] SerializeEnvelope(
        ReadOnlySpan<byte> catalog,
        string content_sha256,
        long maximum_bytes)
    {
        using var output = new LimitedMemoryStream(maximum_bytes);
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", FormatVersion);
            writer.WriteString("contentSha256", content_sha256);
            writer.WritePropertyName("catalog");
            writer.WriteRawValue(catalog, true);
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    async Task PublishAsync(string path, byte[] content, CancellationToken cancellation_token)
    {
        string directory = Path.GetDirectoryName(path)!;
        EnsureStorageDirectory(directory);
        string temporary = Path.Combine(
            directory,
            $".{Path.GetFileNameWithoutExtension(path)}.{Guid.NewGuid():N}.tmp");
        EnsureContained(temporary);
        try
        {
            using (var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                65536,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await output.WriteAsync(content, cancellation_token).ConfigureAwait(false);
                await output.FlushAsync(cancellation_token).ConfigureAwait(false);
                output.Flush(true);
            }
            cancellation_token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    string CachePath(HeaderCatalogKey key)
    {
        string client = key.Client.ToString().ToLowerInvariant();
        string path = Path.GetFullPath(Path.Combine(
            _root,
            $"v{FormatVersion}",
            client,
            key.Fingerprint[..2],
            $"{key.Fingerprint}.json"));
        EnsureContained(path);
        RejectExistingReparsePoints(path);
        return path;
    }

    void EnsureContained(string path)
    {
        string relative = Path.GetRelativePath(_root, path);
        if (relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("The header catalog path leaves its configured root.");
        }
    }

    void EnsureStorageDirectory(string directory)
    {
        EnsureContained(directory);
        string relative = Path.GetRelativePath(_root, directory);
        string current = _root;
        foreach (string component in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            Directory.CreateDirectory(current);
            RejectReparsePoint(current);
        }
    }

    void RejectExistingReparsePoints(string path)
    {
        string relative = Path.GetRelativePath(_root, path);
        string current = _root;
        string[] components = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        foreach (string component in components)
        {
            current = Path.Combine(current, component);
            if (!File.Exists(current) && !Directory.Exists(current))
                break;
            RejectReparsePoint(current);
        }
    }

    static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The header catalog cache path cannot traverse reparse points.");
    }

    static void RejectReparseAncestors(string path)
    {
        DirectoryInfo? current = new(path);
        while (current is not null)
        {
            if (current.Exists)
                RejectReparsePoint(current.FullName);
            current = current.Parent;
        }
    }

    static string ReadString(JsonElement value, string property)
    {
        JsonElement item = value.GetProperty(property);
        if (item.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(item.GetString()))
            throw new InvalidDataException($"The cached property '{property}' is invalid.");
        return item.GetString()!;
    }

    static string? ReadNullableString(JsonElement value, string property)
    {
        JsonElement item = value.GetProperty(property);
        return item.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String when !string.IsNullOrEmpty(item.GetString()) => item.GetString(),
            _ => throw new InvalidDataException($"The cached property '{property}' is invalid.")
        };
    }

    static void RequireProperties(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("A cached header catalog object is invalid.");
        var properties = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!properties.Add(property.Name) || !expected.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidDataException($"The cached property '{property.Name}' is invalid.");
        }
        if (properties.Count != expected.Length || expected.Any(property => !properties.Contains(property)))
            throw new InvalidDataException("A cached header catalog object is incomplete.");
    }

    readonly record struct CacheRead(HeaderCatalogCacheResult? Result, bool Corrupt)
    {
        public static CacheRead Missing => new(null, false);
        public static CacheRead Invalid => new(null, true);
    }

    sealed class LimitedMemoryStream(long maximum_bytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacity(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacity(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureCapacity(1);
            base.WriteByte(value);
        }

        void EnsureCapacity(int additional_bytes)
        {
            if (additional_bytes < 0 || Position > maximum_bytes - additional_bytes)
                throw new InvalidDataException("The canonical header catalog exceeds its cache size limit.");
        }
    }
}
