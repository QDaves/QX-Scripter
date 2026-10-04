using System.Collections.Frozen;
using Qx.Game.Protocol;
using Qx.Messages;
using Qx.Protocol;
using Qx.Scripting.Hosting;

namespace Qx.Scripting;

internal sealed class MessageNames(IMessageResolver messages)
{
    private static readonly MessageDirection[] single_directions = [MessageDirection.In, MessageDirection.Out];

    private static readonly MessageContractCatalog contracts =
        new(MessageManager.CreateWithEmbeddedMap().Registry, MessageContracts.All);

    private static readonly FrozenSet<string> model_assemblies =
        ScriptEngine.ReferenceAssemblies.Select(assembly => assembly.GetName().Name!).ToFrozenSet(StringComparer.Ordinal);

    public static MessageNames Embedded { get; } = new(new MessageManager(contracts.Registry));

    public static MessageNames For(IMessageResolver? messages) => messages is null ? Embedded : new(messages);

    public static bool IsModelAssembly(string? assembly_name) =>
        assembly_name is not null && model_assemblies.Contains(assembly_name);

    public bool IsKnown(MessageDirection direction, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (direction == MessageDirection.Both)
            return IsKnown(MessageDirection.In, name) || IsKnown(MessageDirection.Out, name);
        return messages.Registry.TryGet(direction, name, out _) ||
            messages.TryGetHeaders(new Identifier(direction, name), out _);
    }

    public MessageDirection ModelDirections(MessageDirection direction, string name, Func<Type, bool> is_model)
    {
        bool known = false;
        MessageDirection bound = MessageDirection.None;
        MessageDirection open = MessageDirection.None;
        foreach (MessageDirection single in single_directions)
        {
            if (!Overlaps(direction, single) || !IsKnown(single, name))
                continue;
            known = true;
            if (ModelOf(single, name) is not { } model)
                open = Union(open, single);
            else if (is_model(model))
                bound = Union(bound, single);
        }
        if (!known)
            return direction;
        return bound != MessageDirection.None ? bound : open;
    }

    public string Unknown(MessageDirection direction, string name)
    {
        string unknown = $"'{name}' is not a known {Kind(direction)}message";
        MessageDirection other = Opposite(direction);
        if (other != MessageDirection.None && IsKnown(other, name))
            return $"{unknown}; it is an {Kind(other)}message.";
        return Closest(direction, name) is { } closest
            ? $"{unknown}. Did you mean '{closest}'?"
            : $"{unknown}.";
    }

    public string Mismatch(MessageDirection direction, string name, string model_name, Func<Type, bool> is_model)
    {
        var parsed = new List<string>();
        foreach (MessageDirection single in single_directions)
        {
            if (Overlaps(direction, single) && IsKnown(single, name) && ModelOf(single, name) is { } contract_model)
                parsed.Add($"{Kind(single)}'{name}' is parsed as {contract_model.Name}");
        }
        string text = string.Join(" and ", parsed);
        text = $"{char.ToUpperInvariant(text[0])}{text[1..]}, not {model_name}.";
        MessageDirection other = Opposite(direction);
        if (other != MessageDirection.None && IsKnown(other, name) && ModelOf(other, name) is { } model && is_model(model))
            text += $" {model_name} is the {Kind(other)}'{name}' model.";
        return text;
    }

    private static string Kind(MessageDirection direction) => direction switch
    {
        MessageDirection.In => "incoming ",
        MessageDirection.Out => "outgoing ",
        _ => ""
    };

    private static MessageDirection Opposite(MessageDirection direction) => direction switch
    {
        MessageDirection.In => MessageDirection.Out,
        MessageDirection.Out => MessageDirection.In,
        _ => MessageDirection.None
    };

    private static bool Overlaps(MessageDirection first, MessageDirection second) =>
        first != MessageDirection.None && second != MessageDirection.None &&
        (first == second || first == MessageDirection.Both || second == MessageDirection.Both);

    private static MessageDirection Union(MessageDirection first, MessageDirection second) =>
        first == MessageDirection.None || first == second ? second :
        second == MessageDirection.None ? first :
        MessageDirection.Both;

    private static Type? ModelOf(MessageDirection direction, string name) =>
        contracts.TryGet(direction, name, out IMessageContract contract) ? contract.MessageType : null;

    private string? Closest(MessageDirection direction, string name)
    {
        int best = Math.Max(1, name.Length / 4);
        string? closest = null;
        foreach (string candidate in Candidates(direction))
        {
            if (Math.Abs(candidate.Length - name.Length) > best)
                continue;
            int distance = Distance(name, candidate);
            if (distance > best || (distance == best && closest is not null))
                continue;
            best = distance;
            closest = candidate;
        }
        return closest;
    }

    private IEnumerable<string> Candidates(MessageDirection direction)
    {
        foreach (MessageDescriptor descriptor in messages.Registry.Descriptors)
        {
            if (!Overlaps(direction, descriptor.Direction))
                continue;
            foreach (string alias in descriptor.Names)
                yield return alias;
        }
        if (messages.ActiveCatalogBinding?.Catalog is not { } catalog)
            yield break;
        foreach (MessageCatalogHeader header in catalog.Headers)
        {
            if (Overlaps(direction, header.Direction))
                yield return header.Name;
        }
    }

    private static int Distance(string first, string second)
    {
        var previous = new int[second.Length + 1];
        var current = new int[second.Length + 1];
        for (int column = 0; column <= second.Length; column++)
            previous[column] = column;
        for (int row = 1; row <= first.Length; row++)
        {
            current[0] = row;
            char left = char.ToUpperInvariant(first[row - 1]);
            for (int column = 1; column <= second.Length; column++)
            {
                int cost = left == char.ToUpperInvariant(second[column - 1]) ? 0 : 1;
                current[column] = Math.Min(Math.Min(previous[column] + 1, current[column - 1] + 1), previous[column - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[second.Length];
    }
}
