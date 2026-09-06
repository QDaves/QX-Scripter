using System.Collections.ObjectModel;
using Qx.Messages;

using Qx.Protocol;

namespace Qx.Game.Protocol;

public sealed class MessageContractCatalog
{
    private readonly IReadOnlyDictionary<MessageKey, IMessageContract> _by_key;

    public MessageContractCatalog(
        MessageRegistry registry,
        IEnumerable<IMessageContract> contracts)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(contracts);

        var by_key = new Dictionary<MessageKey, IMessageContract>();
        foreach (IMessageContract contract in contracts)
        {
            ArgumentNullException.ThrowIfNull(contract);
            Validate(registry, contract);
            if (!by_key.TryAdd(contract.Key, contract))
                throw new InvalidDataException($"Message contract '{contract.Key}' is declared more than once.");
        }

        Registry = registry;
        Contracts = Array.AsReadOnly(by_key.Values.OrderBy(contract => contract.Key).ToArray());
        _by_key = new ReadOnlyDictionary<MessageKey, IMessageContract>(by_key);
    }

    public MessageRegistry Registry { get; }

    public IReadOnlyList<IMessageContract> Contracts { get; }

    public int Count => Contracts.Count;

    public bool TryGet(MessageKey key, out IMessageContract contract) =>
        _by_key.TryGetValue(key, out contract!);

    public bool TryGet(
        ClientType client,
        Direction direction,
        string name,
        out IMessageContract contract)
    {
        if (Registry.TryGet(client, direction, name, out MessageDescriptor descriptor) &&
            _by_key.TryGetValue(descriptor.Key, out IMessageContract? resolved) &&
            resolved.Supports(client))
        {
            contract = resolved;
            return true;
        }

        contract = null!;
        return false;
    }

    public bool TryGet<T>(MessageKey key, out MessageContract<T> contract)
        where T : IParserComposer<T>
    {
        if (_by_key.TryGetValue(key, out IMessageContract? untyped) &&
            untyped is MessageContract<T> typed)
        {
            contract = typed;
            return true;
        }

        contract = null!;
        return false;
    }

    private static void Validate(MessageRegistry registry, IMessageContract contract)
    {
        if (contract.Key.IsEmpty)
            throw new InvalidDataException("A message contract requires a key.");
        if (contract.MessageType is null)
            throw new InvalidDataException($"Message contract '{contract.Key}' requires a model type.");
        if (!registry.TryGet(contract.Key, out MessageDescriptor? descriptor))
            throw new InvalidDataException($"Message contract '{contract.Key}' is not declared in the message registry.");
        if (!descriptor.HasExplicitKey)
            throw new InvalidDataException($"Message contract '{contract.Key}' cannot use a generated legacy key.");
        if (contract.Clients is null || contract.Clients.Count == 0)
            throw new InvalidDataException($"Message contract '{contract.Key}' requires at least one client dialect.");

        var clients = new HashSet<ClientType>();
        foreach (ClientType client in contract.Clients)
        {
            if (client is not (ClientType.Flash or ClientType.Unity))
                throw new InvalidDataException($"Message contract '{contract.Key}' declares unsupported client dialect '{client}'.");
            if (!clients.Add(client))
                throw new InvalidDataException($"Message contract '{contract.Key}' declares {client} more than once.");
            if (descriptor.NameFor(client) is null)
            {
                throw new InvalidDataException(
                    $"Message contract '{contract.Key}' declares {client}, but the message registry has no matching alias.");
            }
        }
    }
}
