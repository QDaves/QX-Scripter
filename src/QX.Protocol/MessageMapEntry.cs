using Qx;

namespace Qx.Protocol;

public sealed class MessageMapEntry
{
    private readonly List<string> _names = [];

    public string? FlashName
    {
        get => NameFor(ProtocolClients.Flash);
        set => SetPrimary(value);
    }

    public string? NameFor(ClientType client) =>
        client is ClientType.Flash && _names.Count > 0
            ? _names[0]
            : null;

    public IReadOnlyList<string> NamesFor(ClientType client) =>
        client is ClientType.Flash ? _names : [];

    public void Set(ClientType client, string name)
    {
        if (client is not ClientType.Flash)
            throw new ArgumentOutOfRangeException(nameof(client), client, "A message alias requires Flash.");
        if (string.IsNullOrWhiteSpace(name))
            return;
        if (!_names.Contains(name, StringComparer.OrdinalIgnoreCase))
            _names.Add(name);
    }

    private void SetPrimary(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _names.Clear();
            return;
        }
        int duplicate = _names.FindIndex(value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (duplicate > 0)
            _names.RemoveAt(duplicate);
        if (_names.Count == 0)
            _names.Add(name);
        else
            _names[0] = name;
    }
}
