using Qx;

namespace Qx.Protocol;

public readonly record struct MessageAlias(ClientType Client, string Name);

public sealed class MessageDescriptor
{
    private readonly IReadOnlyList<string> _names;

    public MessageDescriptor(
        MessageKey key,
        Direction direction,
        IEnumerable<MessageAlias> aliases,
        bool has_explicit_key)
    {
        if (key.IsEmpty)
            throw new ArgumentException("A message descriptor requires a key.", nameof(key));
        if (direction is not (Direction.In or Direction.Out))
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "A message descriptor requires one direction.");
        ArgumentNullException.ThrowIfNull(aliases);

        var names = new List<string>();
        foreach (MessageAlias alias in aliases)
        {
            if (alias.Client is not ClientType.Flash)
                throw new ArgumentOutOfRangeException(nameof(aliases), alias.Client, "A message alias requires Flash.");
            if (string.IsNullOrWhiteSpace(alias.Name) || alias.Name != alias.Name.Trim())
                throw new ArgumentException("A message alias requires a trimmed non-empty name.", nameof(aliases));
            if (names.Contains(alias.Name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"Message '{key}' declares duplicate alias '{alias.Name}' for {alias.Client}.");
            names.Add(alias.Name);
        }

        if (names.Count == 0)
            throw new ArgumentException("A message descriptor requires at least one alias.", nameof(aliases));

        Key = key;
        Direction = direction;
        HasExplicitKey = has_explicit_key;
        Aliases = Array.AsReadOnly(names.Select(name => new MessageAlias(ClientType.Flash, name)).ToArray());
        _names = names.AsReadOnly();
    }

    public MessageKey Key { get; }

    public Direction Direction { get; }

    public bool HasExplicitKey { get; }

    public IReadOnlyList<MessageAlias> Aliases { get; }

    public string? NameFor(ClientType client) =>
        client is ClientType.Flash
            ? _names[0]
            : null;

    public IReadOnlyList<string> NamesFor(ClientType client) =>
        client is ClientType.Flash ? _names : [];
}
