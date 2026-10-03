using System.Collections.ObjectModel;

namespace Qx.Protocol;

/// <summary>Represents the registry of declared messages, looked up by key or by Flash name.</summary>
public sealed class MessageRegistry
{
    private readonly IReadOnlyDictionary<MessageKey, MessageDescriptor> _by_key;
    private readonly IReadOnlyDictionary<(MessageDirection Direction, string Name), MessageDescriptor> _by_name;

    /// <summary>Initializes a new instance of the <see cref="MessageRegistry"/> class.</summary>
    /// <param name="descriptors">The message descriptors, kept in the given order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptors"/> or one of its items is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when a key is declared twice or a name in one direction belongs to two descriptors, ignoring case.</exception>
    public MessageRegistry(IEnumerable<MessageDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var ordered = descriptors.ToArray();
        var by_key = new Dictionary<MessageKey, MessageDescriptor>();
        var by_name = new Dictionary<(MessageDirection, string), MessageDescriptor>();

        foreach (MessageDescriptor descriptor in ordered)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (!by_key.TryAdd(descriptor.Key, descriptor))
                throw new InvalidDataException($"Message key '{descriptor.Key}' is declared more than once.");

            foreach (string name in descriptor.Names)
            {
                var lookup = (descriptor.Direction, Normalize(name));
                if (by_name.TryGetValue(lookup, out MessageDescriptor? existing))
                {
                    throw new InvalidDataException(
                        $"Name '{name}' for {descriptor.Direction} belongs to both '{existing.Key}' and '{descriptor.Key}'.");
                }
                by_name.Add(lookup, descriptor);
            }
        }

        Descriptors = Array.AsReadOnly(ordered);
        _by_key = new ReadOnlyDictionary<MessageKey, MessageDescriptor>(by_key);
        _by_name = new ReadOnlyDictionary<(MessageDirection, string), MessageDescriptor>(by_name);
    }

    /// <summary>Gets the message descriptors in declaration order.</summary>
    public IReadOnlyList<MessageDescriptor> Descriptors { get; }

    /// <summary>Gets the number of message descriptors.</summary>
    public int Count => Descriptors.Count;

    /// <summary>Gets the number of Flash names across all descriptors.</summary>
    public int NameCount => _by_name.Count;

    /// <summary>Tries to get the descriptor of a message key.</summary>
    /// <param name="key">The message key.</param>
    /// <param name="descriptor">The descriptor, or <see langword="null"/> when the key is not declared.</param>
    /// <returns><see langword="true"/> if the key is declared; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(MessageKey key, out MessageDescriptor descriptor) =>
        _by_key.TryGetValue(key, out descriptor!);

    /// <summary>Tries to get the descriptor that declares a Flash message name.</summary>
    /// <param name="direction">The direction of the message.</param>
    /// <param name="name">The message name, matched without regard to case.</param>
    /// <param name="descriptor">The descriptor, or <see langword="null"/> when none is found.</param>
    /// <returns><see langword="true"/> if a descriptor was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(
        MessageDirection direction,
        string name,
        out MessageDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            descriptor = null!;
            return false;
        }

        return _by_name.TryGetValue((direction, Normalize(name)), out descriptor!);
    }

    private static string Normalize(string name) => name.ToUpperInvariant();
}
