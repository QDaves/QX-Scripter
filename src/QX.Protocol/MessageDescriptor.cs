using System.Collections.ObjectModel;
using Qx;

namespace Qx.Protocol;

public readonly record struct MessageAlias(ClientType Client, string Name);

public sealed class MessageDescriptor
{
    private readonly IReadOnlyDictionary<ClientType, IReadOnlyList<string>> _names;

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

        var names = new Dictionary<ClientType, List<string>>();
        foreach (MessageAlias alias in aliases)
        {
            if (!ProtocolClients.Supported.Contains(alias.Client))
                throw new ArgumentOutOfRangeException(nameof(aliases), alias.Client, "A message alias requires one client dialect.");
            if (string.IsNullOrWhiteSpace(alias.Name) || alias.Name != alias.Name.Trim())
                throw new ArgumentException("A message alias requires a trimmed non-empty name.", nameof(aliases));
            if (!names.TryGetValue(alias.Client, out List<string>? client_names))
            {
                client_names = [];
                names.Add(alias.Client, client_names);
            }
            if (client_names.Contains(alias.Name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"Message '{key}' declares duplicate alias '{alias.Name}' for {alias.Client}.");
            client_names.Add(alias.Name);
        }

        if (names.Count == 0)
            throw new ArgumentException("A message descriptor requires at least one alias.", nameof(aliases));

        Key = key;
        Direction = direction;
        HasExplicitKey = has_explicit_key;
        Aliases = Array.AsReadOnly(
        [
            .. ProtocolClients.Supported.SelectMany(client =>
                names.TryGetValue(client, out List<string>? client_names)
                    ? client_names.Select(name => new MessageAlias(client, name))
                    : [])
        ]);
        _names = new ReadOnlyDictionary<ClientType, IReadOnlyList<string>>(
            names.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.AsReadOnly()));
    }

    public MessageKey Key { get; }

    public Direction Direction { get; }

    public bool HasExplicitKey { get; }

    public IReadOnlyList<MessageAlias> Aliases { get; }

    public string? NameFor(ClientType client) =>
        _names.TryGetValue(client, out IReadOnlyList<string>? names) && names.Count > 0
            ? names[0]
            : null;

    public IReadOnlyList<string> NamesFor(ClientType client) =>
        _names.TryGetValue(client, out IReadOnlyList<string>? names) ? names : [];
}
