using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record ItemStateUpdate(Id Id, string ItemData) : IParserComposer<ItemStateUpdate>
{
    public static ItemStateUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ItemStateUpdate ParseFlash(in PacketReader p) => ParseItem(in p);

    private static ItemStateUpdate ParseItem(in PacketReader p) => new(p.ReadId(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ItemStateUpdate value, in PacketWriter p) => value.ComposeItem(in p);

    private void ComposeItem(in PacketWriter p)
    {
        p.WriteId(Id);
        p.WriteString(ItemData);
    }
}

public sealed record WallItemsStateUpdate(IReadOnlyList<ItemStateUpdate> Items)
    : IParserComposer<WallItemsStateUpdate>
{
    public static WallItemsStateUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WallItemsStateUpdate ParseFlash(in PacketReader p)
    {
        int count = p.ReadLength();
        var items = new ItemStateUpdate[count];
        for (int i = 0; i < count; i++)
            items[i] = p.Parse<ItemStateUpdate>();
        return new WallItemsStateUpdate(items);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WallItemsStateUpdate value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Items.Count);
        foreach (ItemStateUpdate item in value.Items)
            p.Compose(item);
    }
}
