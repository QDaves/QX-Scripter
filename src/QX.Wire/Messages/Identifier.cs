namespace Qx.Messages;

public readonly record struct Identifier(ClientType Client, Direction Direction, string Name)
{
    public static readonly Identifier Unknown = new();

    public Identifier()
        : this(ClientType.None, Direction.None, "")
    { }

    public override int GetHashCode() => (Client, Direction, Name.ToUpperInvariant()).GetHashCode();

    public bool Equals(Identifier other) =>
        Client == other.Client &&
        Direction == other.Direction &&
        string.Equals(Name, other.Name, StringComparison.InvariantCultureIgnoreCase);

    public string ToString(bool includeDirection)
    {
        string result = "";
        if (includeDirection)
            result += Direction switch
            {
                Direction.None => "",
                Direction.In => "in:",
                Direction.Out => "out:",
                _ => throw new ArgumentOutOfRangeException(nameof(Direction))
            };
        result += Client switch
        {
            ClientType.None => "",
            ClientType.Unity => "unity:",
            ClientType.Flash => "flash:",
            _ => throw new UnsupportedClientException(Client)
        };
        return result + Name;
    }

    public override string ToString() => ToString(false);

    public static implicit operator Identifier((Direction direction, string name) x) => new(ClientType.None, x.direction, x.name);
    public static implicit operator Identifier((ClientType client, Direction direction, string name) x) => new(x.client, x.direction, x.name);

    public static implicit operator ReadOnlySpan<Identifier>(in Identifier identifier) => new(in identifier);
}
