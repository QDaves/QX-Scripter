using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record FloorItems(IReadOnlyList<FloorItem> Items) : IParserComposer<FloorItems>
{
    public static FloorItems Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FloorItems ParseFlash(in PacketReader p) => ParseItems(in p);

    private static FloorItems ParseUnity(in PacketReader p) => ParseItems(in p);

    private static FloorItems ParseItems(in PacketReader p)
    {
        var owners = new Dictionary<long, string>();
        int count = p.ReadLength();
        for (int i = 0; i < count; i++)
            owners[p.ReadId()] = p.ReadString();

        count = p.ReadLength();
        var items = new List<FloorItem>(count);
        for (int i = 0; i < count; i++)
        {
            var item = p.Parse<FloorItem>();
            if (owners.TryGetValue(item.OwnerId, out string? name))
                item.OwnerName = name;
            items.Add(item);
        }

        return new FloorItems(items);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FloorItems value, in PacketWriter p) => value.ComposeItems(in p);

    private static void ComposeUnity(FloorItems value, in PacketWriter p) => value.ComposeItems(in p);

    private void ComposeItems(in PacketWriter p)
    {
        var owners = new Dictionary<long, string>();
        foreach (FloorItem item in Items)
            owners.TryAdd(item.OwnerId, item.OwnerName);

        p.WriteLength((Length)owners.Count);
        foreach ((long id, string name) in owners)
        {
            p.WriteId(id);
            p.WriteString(name);
        }

        p.WriteLength((Length)Items.Count);
        foreach (FloorItem item in Items)
            p.Compose(item);
    }
}
