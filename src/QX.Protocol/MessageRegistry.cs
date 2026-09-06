using System.Collections.ObjectModel;
using Qx;

namespace Qx.Protocol;

public sealed class MessageRegistry
{
    private readonly IReadOnlyDictionary<MessageKey, MessageDescriptor> _by_key;
    private readonly IReadOnlyDictionary<(ClientType Client, Direction Direction, string Name), MessageDescriptor> _by_alias;

    public MessageRegistry(IEnumerable<MessageDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var ordered = descriptors.ToArray();
        var by_key = new Dictionary<MessageKey, MessageDescriptor>();
        var by_alias = new Dictionary<(ClientType, Direction, string), MessageDescriptor>();

        foreach (MessageDescriptor descriptor in ordered)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (!by_key.TryAdd(descriptor.Key, descriptor))
                throw new InvalidDataException($"Message key '{descriptor.Key}' is declared more than once.");

            foreach (MessageAlias alias in descriptor.Aliases)
            {
                var lookup = (alias.Client, descriptor.Direction, Normalize(alias.Name));
                if (by_alias.TryGetValue(lookup, out MessageDescriptor? existing))
                {
                    throw new InvalidDataException(
                        $"Alias '{alias.Name}' for {alias.Client} {descriptor.Direction} belongs to both '{existing.Key}' and '{descriptor.Key}'.");
                }
                by_alias.Add(lookup, descriptor);
            }
        }

        Descriptors = Array.AsReadOnly(ordered);
        _by_key = new ReadOnlyDictionary<MessageKey, MessageDescriptor>(by_key);
        _by_alias = new ReadOnlyDictionary<(ClientType, Direction, string), MessageDescriptor>(by_alias);
    }

    public IReadOnlyList<MessageDescriptor> Descriptors { get; }

    public int Count => Descriptors.Count;

    public int AliasCount => _by_alias.Count;

    public bool TryGet(MessageKey key, out MessageDescriptor descriptor) =>
        _by_key.TryGetValue(key, out descriptor!);

    public bool TryGet(
        ClientType client,
        Direction direction,
        string name,
        out MessageDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            descriptor = null!;
            return false;
        }

        return _by_alias.TryGetValue((client, direction, Normalize(name)), out descriptor!);
    }

    private static string Normalize(string name) => name.ToUpperInvariant();
}
