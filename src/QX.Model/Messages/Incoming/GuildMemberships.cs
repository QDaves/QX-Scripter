using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GuildMembership(
    Id Id,
    string Name,
    string BadgeCode,
    string PrimaryColor,
    string SecondaryColor,
    bool IsFavorite,
    Id OwnerId,
    bool HasForum) : IParserComposer<GuildMembership>
{
    public static GuildMembership Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GuildMembership ParseFlash(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadBool(),
            p.ReadInt(),
            p.ReadBool());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GuildMembership value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(PeopleWire.RequireFlashId(value.Id, nameof(Id)));
        ComposeStrings(value, in p);
        p.WriteBool(value.IsFavorite);
        p.WriteInt(PeopleWire.RequireFlashId(value.OwnerId, nameof(OwnerId)));
        p.WriteBool(value.HasForum);
    }

    private static void ComposeStrings(GuildMembership value, in PacketWriter p)
    {
        p.WriteString(value.Name);
        p.WriteString(value.BadgeCode);
        p.WriteString(value.PrimaryColor);
        p.WriteString(value.SecondaryColor);
    }

    internal static void Validate(GuildMembership value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        {
            _ = PeopleWire.RequireFlashId(value.Id, nameof(Id));
            _ = PeopleWire.RequireFlashId(value.OwnerId, nameof(OwnerId));
        }
        PeopleWire.RequireString(value.Name, nameof(Name), in p);
        PeopleWire.RequireString(value.BadgeCode, nameof(BadgeCode), in p);
        PeopleWire.RequireString(value.PrimaryColor, nameof(PrimaryColor), in p);
        PeopleWire.RequireString(value.SecondaryColor, nameof(SecondaryColor), in p);
    }
}

public sealed record GuildMemberships : IParserComposer<GuildMemberships>
{
    private IReadOnlyList<GuildMembership> _items =
        Array.AsReadOnly(Array.Empty<GuildMembership>());

    public GuildMemberships(IReadOnlyList<GuildMembership> Items) => this.Items = Items;

    public IReadOnlyList<GuildMembership> Items
    {
        get => _items;
        init => _items = PeopleWire.FreezeReferences(value, nameof(Items));
    }

    public void Deconstruct(out IReadOnlyList<GuildMembership> Items) => Items = this.Items;

    public static GuildMemberships Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GuildMemberships ParseFlash(in PacketReader p)
    {
        int count = PeopleWire.ReadFlashCount(
            in p,
            PeopleWire.FlashGroupMinimumBytes,
            nameof(Items));
        var items = new GuildMembership[count];
        for (int index = 0; index < items.Length; index++)
            items[index] = p.Parse<GuildMembership>();
        PeopleWire.RequireEmpty(in p, nameof(GuildMemberships));
        return new GuildMemberships(items);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GuildMemberships value, in PacketWriter p)
    {
        GuildMemberships prepared = Prepare(value, in p);
        p.WriteInt(prepared.Items.Count);
        foreach (GuildMembership item in prepared.Items)
            p.Compose(item);
    }

    private static GuildMemberships Prepare(
        GuildMemberships value,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        GuildMembership[] items = PeopleWire.SnapshotReferences(value.Items, nameof(Items));
        foreach (GuildMembership item in items)
            GuildMembership.Validate(item, in p);
        return new GuildMemberships(items);
    }
}
