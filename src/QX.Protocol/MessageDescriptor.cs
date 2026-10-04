namespace Qx.Protocol;

/// <summary>Represents a message declared in the message registry, with its key, direction and Flash names.</summary>
public sealed class MessageDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="MessageDescriptor"/> class.</summary>
    /// <param name="key">The message key.</param>
    /// <param name="direction">The direction, which must be <see cref="MessageDirection.In"/> or <see cref="MessageDirection.Out"/>.</param>
    /// <param name="names">The Flash names of the message, the first being the primary name.</param>
    /// <param name="hasExplicitKey">Whether the key is declared in the registry rather than generated.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty, <paramref name="names"/> is empty, or a name is blank or not trimmed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="direction"/> is not a single direction.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="names"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when two names are the same, ignoring case.</exception>
    public MessageDescriptor(
        MessageKey key,
        MessageDirection direction,
        IEnumerable<string> names,
        bool hasExplicitKey)
    {
        if (key.IsEmpty)
            throw new ArgumentException("A message descriptor requires a key.", nameof(key));
        if (direction is not (MessageDirection.In or MessageDirection.Out))
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "A message descriptor requires one direction.");
        ArgumentNullException.ThrowIfNull(names);

        var declared = new List<string>();
        foreach (string name in names)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim())
                throw new ArgumentException("A message name must be trimmed and not empty.", nameof(names));
            if (declared.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"Message '{key}' declares duplicate name '{name}'.");
            declared.Add(name);
        }

        if (declared.Count == 0)
            throw new ArgumentException("A message descriptor requires at least one name.", nameof(names));

        Key = key;
        Direction = direction;
        HasExplicitKey = hasExplicitKey;
        Names = declared.AsReadOnly();
    }

    /// <summary>Gets the message key.</summary>
    public MessageKey Key { get; }

    /// <summary>Gets the direction of the message.</summary>
    public MessageDirection Direction { get; }

    /// <summary>Gets whether the key is declared with <c>k:</c> in the registry rather than generated as a <c>legacy.</c> key.</summary>
    public bool HasExplicitKey { get; }

    /// <summary>Gets the primary Flash name of the message.</summary>
    public string Name => Names[0];

    /// <summary>Gets every Flash name of the message, the first being the primary name.</summary>
    public IReadOnlyList<string> Names { get; }
}
