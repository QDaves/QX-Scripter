using Qx;

namespace Qx.Protocol;

public sealed class MessageMap
{
    private readonly Dictionary<(Direction, string), MessageMapEntry> _by_name = [];

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
        foreach (string name in entry.NamesFor(ClientType.Flash))
        {
            var key = (direction, name.ToUpperInvariant());
            if (!_by_name.TryAdd(key, entry))
            {
                throw new InvalidDataException(
                    $"Alias '{name}' for Flash {direction} is assigned to multiple message entries.");
            }
        }
    }

    public bool TryGetEntry(ClientType client, Direction direction, string name, out MessageMapEntry entry)
    {
        entry = null!;
        return client is ClientType.Flash &&
            _by_name.TryGetValue((direction, name.ToUpperInvariant()), out entry!);
    }

    public IReadOnlyList<string> EquivalentNames(ClientType client, Direction direction, string name) =>
        TryGetEntry(client, direction, name, out MessageMapEntry entry)
            ? entry.NamesFor(client)
            : [];

    public bool AreEquivalent(ClientType client, Direction direction, string first, string second) =>
        TryGetEntry(client, direction, first, out MessageMapEntry first_entry) &&
        TryGetEntry(client, direction, second, out MessageMapEntry second_entry) &&
        ReferenceEquals(first_entry, second_entry);

    public int Count => _by_name.Count;
}
