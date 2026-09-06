using System.Reflection;
using Qx;

namespace Qx.Protocol;

public static class MessagesIniParser
{
    private const string ResourceName = "Qx.Protocol.messages.ini";

    public static MessageMap ParseEmbedded()
        => new(ParseEmbeddedRegistry());

    public static MessageRegistry ParseEmbeddedRegistry()
    {
        Assembly assembly = typeof(MessagesIniParser).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return ParseRegistry(reader.ReadToEnd());
    }

    public static MessageMap Parse(string text) => new(ParseRegistry(text));

    public static MessageRegistry ParseRegistry(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var descriptors = new List<MessageDescriptor>();
        var legacy_occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        Direction direction = Direction.None;

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';')
                continue;

            if (line[0] == '[')
            {
                direction = line switch
                {
                    "[Incoming]" => Direction.In,
                    "[Outgoing]" => Direction.Out,
                    _ => throw new InvalidDataException($"Unknown message section '{line}'.")
                };
                continue;
            }

            if (direction == Direction.None)
                throw new InvalidDataException($"Message row '{line}' appears before a direction section.");

            ParseLine(descriptors, legacy_occurrences, direction, line);
        }

        return new MessageRegistry(descriptors);
    }

    private static void ParseLine(
        List<MessageDescriptor> descriptors,
        Dictionary<string, int> legacy_occurrences,
        Direction direction,
        string line)
    {
        int comment = line.IndexOf(';');
        if (comment >= 0)
            line = line[..comment].Trim();
        if (line.Length == 0)
            return;

        var merged = new List<MessageAlias>();
        string? explicit_key = null;

        foreach (string field in line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (field.StartsWith('!'))
                throw new InvalidDataException($"Separate message alias '{field}' is not supported.");

            int colon = field.IndexOf(':');
            if (colon <= 0)
                throw new InvalidDataException($"Message field '{field}' is malformed.");

            string runes = field[..colon];
            string name = field[(colon + 1)..];

            if (runes == "k")
            {
                if (explicit_key is not null)
                    throw new InvalidDataException("A message row declares more than one stable key.");
                if (!MessageKey.TryParse(name, out MessageKey parsed_key))
                    throw new InvalidDataException($"'{name}' is not a valid stable message key.");
                explicit_key = parsed_key.Value;
                continue;
            }

            if (runes is not ("u" or "f" or "uf"))
                throw new InvalidDataException($"Message field '{field}' uses unsupported client runes '{runes}'.");
            if (name.Length == 0)
                throw new InvalidDataException($"Message field '{field}' has no alias name.");
            if (name == "-")
                continue;

            merged.AddRange(AliasesFor(runes, name));
        }

        if (merged.Count > 0)
            descriptors.Add(CreateDescriptor(direction, merged, explicit_key, legacy_occurrences));
        else if (explicit_key is not null)
            throw new InvalidDataException($"Message key '{explicit_key}' has no aliases.");
    }

    private static IReadOnlyList<MessageAlias> AliasesFor(string runes, string name) => runes switch
    {
        "u" => [new(ProtocolClients.Unity, name)],
        "f" => [new(ProtocolClients.Flash, name)],
        "uf" => [new(ProtocolClients.Unity, name), new(ProtocolClients.Flash, name)],
        _ => throw new InvalidDataException($"Unsupported client runes '{runes}'.")
    };

    private static MessageDescriptor CreateDescriptor(
        Direction direction,
        IReadOnlyList<MessageAlias> aliases,
        string? explicit_key,
        Dictionary<string, int> legacy_occurrences)
    {
        if (explicit_key is not null)
            return new MessageDescriptor(new MessageKey(explicit_key), direction, aliases, true);

        string identity = string.Join(
            "|",
            aliases
                .Select(alias => $"{(int)alias.Client}:{alias.Name.ToUpperInvariant()}")
                .Order(StringComparer.Ordinal));
        string scoped_identity = $"{(int)direction}|{identity}";
        int occurrence = legacy_occurrences.GetValueOrDefault(scoped_identity) + 1;
        legacy_occurrences[scoped_identity] = occurrence;
        MessageKey key = MessageKey.Legacy(direction, scoped_identity, occurrence);
        return new MessageDescriptor(key, direction, aliases, false);
    }
}
