using System.Collections.ObjectModel;
using Qx.Messages;

using Qx.Protocol;

namespace Qx.Game.Protocol;

/// <summary>Represents a validated set of message contracts indexed by message key.</summary>
public sealed class MessageContractCatalog
{
    private readonly IReadOnlyDictionary<MessageKey, IMessageContract> _by_key;

    /// <summary>Initializes a new instance of the <see cref="MessageContractCatalog"/> class and validates every contract against the registry.</summary>
    /// <remarks>
    /// Every contract needs a key that the registry declares as an explicit key and a model type. Each key may
    /// appear only once.
    /// </remarks>
    /// <param name="registry">The message registry to validate the contracts against.</param>
    /// <param name="contracts">The contracts to index.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/>, <paramref name="contracts"/> or one of the contracts is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when a contract fails validation or a key is declared more than once.</exception>
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

    /// <summary>Gets the message registry the contracts were validated against.</summary>
    public MessageRegistry Registry { get; }

    /// <summary>Gets the contracts ordered by message key.</summary>
    public IReadOnlyList<IMessageContract> Contracts { get; }

    /// <summary>Gets the number of contracts.</summary>
    public int Count => Contracts.Count;

    /// <summary>Gets the contract with the specified key.</summary>
    /// <param name="key">The message key.</param>
    /// <param name="contract">The contract with the key, or <see langword="null"/> when there is none.</param>
    /// <returns><see langword="true"/> when a contract with the key exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(MessageKey key, out IMessageContract contract) =>
        _by_key.TryGetValue(key, out contract!);

    /// <summary>Gets the contract for the message with the specified direction and name.</summary>
    /// <remarks>
    /// The name is matched, ignoring case, against the names the registry declares for that direction. A lookup
    /// with an empty name returns <see langword="false"/>.
    /// </remarks>
    /// <param name="direction">The direction of the message.</param>
    /// <param name="name">The message name, such as the Flash name.</param>
    /// <param name="contract">The matching contract, or <see langword="null"/> when there is none.</param>
    /// <returns><see langword="true"/> when a contract matches; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(
        MessageDirection direction,
        string name,
        out IMessageContract contract)
    {
        if (Registry.TryGet(direction, name, out MessageDescriptor descriptor) &&
            _by_key.TryGetValue(descriptor.Key, out IMessageContract? resolved))
        {
            contract = resolved;
            return true;
        }

        contract = null!;
        return false;
    }

    /// <summary>Gets the typed contract with the specified key.</summary>
    /// <typeparam name="T">The message model type.</typeparam>
    /// <param name="key">The message key.</param>
    /// <param name="contract">The typed contract with the key, or <see langword="null"/> when there is none.</param>
    /// <returns><see langword="true"/> when a contract with the key exists and its model type is <typeparamref name="T"/>; otherwise, <see langword="false"/>.</returns>
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
    }
}
