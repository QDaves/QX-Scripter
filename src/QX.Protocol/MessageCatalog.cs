using Qx;
using Qx.Messages;

namespace Qx.Protocol;

public sealed record MessageCatalogHeader(Direction Direction, int Id, string Name);

public sealed class MessageCatalog
{
    private readonly object _sync = new();
    private readonly Dictionary<(Direction, string), List<short>> _forward = new();
    private readonly Dictionary<(Direction, string), IReadOnlyList<short>> _forward_views = new();
    private readonly Dictionary<(Direction, short), string> _reverse = new();
    private readonly Dictionary<short, List<OutgoingMessageSchema>> _outgoing_schemas = new();
    private readonly Dictionary<short, IReadOnlyList<OutgoingMessageSchema>> _outgoing_schema_views = new();
    private bool _read_only;

    public int Count
    {
        get
        {
            lock (_sync)
                return _forward.Count;
        }
    }

    public int HeaderCount
    {
        get
        {
            lock (_sync)
                return _reverse.Count;
        }
    }

    public IReadOnlyList<MessageCatalogHeader> Headers
    {
        get
        {
            lock (_sync)
            {
                return Array.AsReadOnly(_reverse
                    .Select(entry => new MessageCatalogHeader(
                        entry.Key.Item1,
                        unchecked((ushort)entry.Key.Item2),
                        entry.Value))
                    .OrderBy(entry => entry.Direction)
                    .ThenBy(entry => entry.Id)
                    .ToArray());
            }
        }
    }

    public string? BuildFingerprint
    {
        get
        {
            lock (_sync)
                return _build_fingerprint;
        }
        private set => _build_fingerprint = value;
    }

    public string? SchemaFingerprint
    {
        get
        {
            lock (_sync)
                return _schema_fingerprint;
        }
        private set => _schema_fingerprint = value;
    }

    public MessageWireProfile WireProfile
    {
        get
        {
            lock (_sync)
                return _wire_profile;
        }
        private set => _wire_profile = value;
    }

    public bool IsReadOnly
    {
        get
        {
            lock (_sync)
                return _read_only;
        }
    }

    private string? _build_fingerprint;
    private string? _schema_fingerprint;
    private MessageWireProfile _wire_profile;

    public static MessageCatalog FromJson(MessagesJson json)
    {
        var catalog = new MessageCatalog();
        foreach (MessageEntry entry in json.Incoming)
            catalog.Add(Direction.In, entry.Id, entry.Name);
        foreach (MessageEntry entry in json.Outgoing)
            catalog.Add(Direction.Out, entry.Id, entry.Name);
        return catalog;
    }

    public void SetBuildFingerprint(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        lock (_sync)
        {
            RequireMutable();
            BuildFingerprint = fingerprint.Trim().ToUpperInvariant();
        }
    }

    public bool MatchesBuildFingerprint(string fingerprint)
    {
        lock (_sync)
        {
            return BuildFingerprint is not null &&
                BuildFingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetSchemaFingerprint(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        lock (_sync)
        {
            RequireMutable();
            SchemaFingerprint = fingerprint.Trim().ToUpperInvariant();
        }
    }

    public void SetWireProfile(MessageWireProfile profile)
    {
        if (!profile.IsAnalyzed)
            throw new ArgumentException("The message wire profile was not analyzed.", nameof(profile));
        lock (_sync)
        {
            RequireMutable();
            WireProfile = profile;
        }
    }

    public bool MatchesSchemaFingerprint(string? fingerprint)
    {
        lock (_sync)
        {
            return SchemaFingerprint is not null &&
                fingerprint is not null &&
                SchemaFingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Add(Direction direction, int id, string name)
    {
        lock (_sync)
        {
            RequireMutable();
            short value = (short)id;
            AddForward(direction, name, value);
            _reverse[(direction, value)] = name;
        }
    }

    public void AddAlias(Direction direction, int id, string name)
    {
        lock (_sync)
        {
            RequireMutable();
            short value = (short)id;
            AddForward(direction, name, value);
            _reverse.TryAdd((direction, value), name);
        }
    }

    public bool TryGetId(Direction direction, string name, out short id)
    {
        if (TryGetIds(direction, name, out IReadOnlyList<short> ids))
        {
            id = ids[^1];
            return true;
        }
        id = default;
        return false;
    }

    public bool TryGetIds(Direction direction, string name, out IReadOnlyList<short> ids)
    {
        lock (_sync)
        {
            if (_forward_views.TryGetValue((direction, name.ToUpperInvariant()), out IReadOnlyList<short>? values) && values.Count > 0)
            {
                ids = values;
                return true;
            }
            ids = [];
            return false;
        }
    }

    public bool TryGetName(Direction direction, short id, out string name)
    {
        lock (_sync)
            return _reverse.TryGetValue((direction, id), out name!);
    }

    public bool CoversHeaders(MessageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        (Direction, short)[] keys;
        lock (catalog._sync)
            keys = catalog._reverse.Keys.ToArray();
        lock (_sync)
            return keys.Length > 0 && keys.All(_reverse.ContainsKey);
    }

    public int MatchingHeaders(MessageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        KeyValuePair<(Direction, short), string>[] entries;
        lock (catalog._sync)
            entries = catalog._reverse.ToArray();
        return entries.Count(entry =>
            TryGetIds(entry.Key.Item1, entry.Value, out IReadOnlyList<short> ids) &&
            ids.Contains(entry.Key.Item2));
    }

    public bool HasExactHeaders(MessageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return HeaderCount > 0 &&
            HeaderCount == catalog.HeaderCount &&
            CoversHeaders(catalog) &&
            catalog.CoversHeaders(this) &&
            MatchingHeaders(catalog) == catalog.HeaderCount &&
            catalog.MatchingHeaders(this) == HeaderCount;
    }

    public void AddOutgoingSchema(int id, OutgoingMessageSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        lock (_sync)
        {
            RequireMutable();
            short value = unchecked((short)id);
            if (!_outgoing_schemas.TryGetValue(value, out List<OutgoingMessageSchema>? schemas))
            {
                schemas = [];
                _outgoing_schemas.Add(value, schemas);
                _outgoing_schema_views.Add(value, schemas.AsReadOnly());
            }
            schemas.Add(schema);
        }
    }

    public bool TryGetOutgoingSchemas(short id, out IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        lock (_sync)
        {
            if (_outgoing_schema_views.TryGetValue(id, out IReadOnlyList<OutgoingMessageSchema>? values) && values.Count > 0)
            {
                schemas = values;
                return true;
            }
            schemas = [];
            return false;
        }
    }

    private void AddForward(Direction direction, string name, short value)
    {
        var key = (direction, name.ToUpperInvariant());
        if (!_forward.TryGetValue(key, out List<short>? values))
        {
            values = [];
            _forward.Add(key, values);
            _forward_views.Add(key, values.AsReadOnly());
        }
        if (!values.Contains(value))
            values.Add(value);
    }

    public MessageCatalog Snapshot()
    {
        lock (_sync)
        {
            var snapshot = new MessageCatalog();
            foreach (((Direction direction, string name), List<short> values) in _forward)
            {
                var copied = new List<short>(values);
                var key = (direction, name);
                snapshot._forward.Add(key, copied);
                snapshot._forward_views.Add(key, copied.AsReadOnly());
            }
            foreach (((Direction direction, short id), string name) in _reverse)
                snapshot._reverse.Add((direction, id), name);
            foreach ((short id, List<OutgoingMessageSchema> schemas) in _outgoing_schemas)
            {
                List<OutgoingMessageSchema> copied = schemas.Select(Copy).ToList();
                snapshot._outgoing_schemas.Add(id, copied);
                snapshot._outgoing_schema_views.Add(id, copied.AsReadOnly());
            }
            snapshot.BuildFingerprint = BuildFingerprint;
            snapshot.SchemaFingerprint = SchemaFingerprint;
            snapshot.WireProfile = WireProfile;
            snapshot._read_only = true;
            return snapshot;
        }
    }

    static OutgoingMessageSchema Copy(OutgoingMessageSchema schema) => new(
        schema.SourceName,
        Array.AsReadOnly(schema.Parameters.Select(parameter => parameter with
        {
            ElementWireTypes = parameter.ElementWireTypes is null
                    ? null
                    : Array.AsReadOnly(parameter.ElementWireTypes.ToArray())
        })
            .ToArray()));

    void RequireMutable()
    {
        if (_read_only)
            throw new InvalidOperationException("The message catalog is read-only.");
    }
}
