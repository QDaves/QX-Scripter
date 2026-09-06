using Qx;

namespace Qx.Protocol;

public sealed class MessageMap
{
    private readonly Dictionary<(ClientType, Direction, string), MessageMapEntry> _byName = new();

    internal MessageMap(MessageRegistry registry)
    {
        Registry = registry;
        foreach (MessageDescriptor descriptor in registry.Descriptors)
        {
            var entry = new MessageMapEntry();
            foreach (MessageAlias alias in descriptor.Aliases)
                entry.Set(alias.Client, alias.Name);
            AddEntry(descriptor.Direction, entry);
        }
    }

    public MessageRegistry Registry { get; }

    private void AddEntry(Direction direction, MessageMapEntry entry)
    {
        var additions = new List<((ClientType Client, Direction Direction, string Name) Key, string Name)>();
        foreach (ClientType client in ProtocolClients.Supported)
        {
            foreach (string name in entry.NamesFor(client))
            {
                var key = (client, direction, name.ToUpperInvariant());
                if (_byName.TryGetValue(key, out MessageMapEntry? existing) && !ReferenceEquals(existing, entry))
                {
                    throw new InvalidDataException(
                        $"Alias '{name}' for {client} {direction} is assigned to multiple message entries.");
                }
                additions.Add((key, name));
            }
        }

        foreach (var addition in additions)
            _byName[addition.Key] = entry;
    }

    public bool TryGetEntry(ClientType client, Direction direction, string name, out MessageMapEntry entry) =>
        _byName.TryGetValue((client, direction, name.ToUpperInvariant()), out entry!);

    public bool TryTranslate(ClientType from, ClientType to, Direction direction, string name, out string translated)
    {
        if (from == to)
        {
            translated = name;
            return true;
        }

        if (TryGetEntry(from, direction, name, out MessageMapEntry entry))
        {
            string? target = entry.NameFor(to);
            if (target is not null)
            {
                translated = target;
                return true;
            }
        }

        translated = name;
        return false;
    }

    public IReadOnlyList<string> EquivalentNames(
        ClientType from,
        ClientType to,
        Direction direction,
        string name) =>
        TryGetEntry(from, direction, name, out MessageMapEntry entry)
            ? entry.NamesFor(to)
            : [];

    public IReadOnlyList<string> EquivalentNames(ClientType to, Direction direction, string name)
    {
        if (TryGetEntry(to, direction, name, out MessageMapEntry target_entry))
            return target_entry.NamesFor(to);
        MessageMapEntry? source_entry = null;
        foreach (ClientType from in ProtocolClients.Supported)
        {
            if (from == to)
                continue;
            if (!TryGetEntry(from, direction, name, out MessageMapEntry entry))
                continue;
            if (entry.NamesFor(to).Count == 0)
                continue;
            if (source_entry is not null && !ReferenceEquals(source_entry, entry))
                return [];
            source_entry = entry;
        }
        return source_entry?.NamesFor(to) ?? [];
    }

    public bool AreEquivalent(ClientType client, Direction direction, string first, string second) =>
        TryGetEntry(client, direction, first, out MessageMapEntry first_entry) &&
        TryGetEntry(client, direction, second, out MessageMapEntry second_entry) &&
        ReferenceEquals(first_entry, second_entry);

    public int Count => _byName.Count;
}
