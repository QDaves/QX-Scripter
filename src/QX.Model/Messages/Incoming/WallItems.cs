using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record WallItems(IReadOnlyList<WallItem> Items) : IParserComposer<WallItems>
{
    public static WallItems Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WallItems ParseFlash(in PacketReader p) => ParseItems(in p);

    private static WallItems ParseUnity(in PacketReader p) => ParseItems(in p);

    private static WallItems ParseItems(in PacketReader p)
    {
        var owners = new Dictionary<long, string>();
        int count = p.ReadLength();
        for (int i = 0; i < count; i++)
            owners[p.ReadId()] = p.ReadString();

        count = p.ReadLength();
        var items = new List<WallItem>(count);
        for (int i = 0; i < count; i++)
        {
            var item = p.Parse<WallItem>();
            if (owners.TryGetValue(item.OwnerId, out string? name))
                item.OwnerName = name;
            items.Add(item);
        }

        return new WallItems(items);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WallItems value, in PacketWriter p) => value.ComposeItems(in p);

    private static void ComposeUnity(WallItems value, in PacketWriter p) => value.ComposeItems(in p);

    private void ComposeItems(in PacketWriter p)
    {
        var owners = new Dictionary<long, string>();
        foreach (WallItem item in Items)
            owners.TryAdd(item.OwnerId, item.OwnerName);

        p.WriteLength((Length)owners.Count);
        foreach ((long id, string name) in owners)
        {
            p.WriteId(id);
            p.WriteString(name);
        }

        p.WriteLength((Length)Items.Count);
        foreach (WallItem item in Items)
            p.Compose(item);
    }
}
