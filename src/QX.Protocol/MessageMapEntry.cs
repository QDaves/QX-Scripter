using Qx;

namespace Qx.Protocol;

public sealed class MessageMapEntry
{
    private readonly Dictionary<ClientType, List<string>> _names = [];

    public string? UnityName
    {
        get => NameFor(ProtocolClients.Unity);
        set => SetPrimary(ProtocolClients.Unity, value);
    }

    public string? FlashName
    {
        get => NameFor(ProtocolClients.Flash);
        set => SetPrimary(ProtocolClients.Flash, value);
    }

    public string? NameFor(ClientType client) =>
        _names.TryGetValue(client, out List<string>? names) && names.Count > 0
            ? names[0]
            : null;

    public IReadOnlyList<string> NamesFor(ClientType client) =>
        _names.TryGetValue(client, out List<string>? names) ? names : [];

    public void Set(ClientType client, string name)
    {
        if (!ProtocolClients.Supported.Contains(client))
            throw new ArgumentOutOfRangeException(nameof(client), client, "A message alias requires Flash or Unity.");
        if (string.IsNullOrWhiteSpace(name))
            return;
        if (!_names.TryGetValue(client, out List<string>? names))
        {
            names = [];
            _names[client] = names;
        }
        if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            names.Add(name);
    }

    private void SetPrimary(ClientType client, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _names.Remove(client);
            return;
        }
        if (!_names.TryGetValue(client, out List<string>? names))
        {
            _names[client] = [name];
            return;
        }
        int duplicate = names.FindIndex(value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (duplicate > 0)
            names.RemoveAt(duplicate);
        if (names.Count == 0)
            names.Add(name);
        else
            names[0] = name;
    }
}
