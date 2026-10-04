using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents a group the local user belongs to.</summary>
/// <param name="Id">The identifier of the group.</param>
/// <param name="Name">The name of the group.</param>
/// <param name="BadgeCode">The group's badge code.</param>
/// <param name="PrimaryColor">The primary color of the group.</param>
/// <param name="SecondaryColor">The secondary color of the group.</param>
/// <param name="IsFavorite">Whether the group is the user's favorite group.</param>
/// <param name="OwnerId">The identifier of the group's owner.</param>
/// <param name="HasForum">Whether the group has a forum.</param>
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
    /// <summary>Parses a group membership from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the group membership into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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

/// <summary>
/// Represents the <c>GuildMemberships</c> message, received with the groups the local user belongs to.
/// </summary>
public sealed record GuildMemberships : IParserComposer<GuildMemberships>
{
    private IReadOnlyList<GuildMembership> _items =
        Array.AsReadOnly(Array.Empty<GuildMembership>());

    /// <summary>Initializes a new instance of the <see cref="GuildMemberships"/> record.</summary>
    /// <param name="items">The groups, copied into a read only list.</param>
    public GuildMemberships(IReadOnlyList<GuildMembership> items) => Items = items;

    /// <summary>Gets the groups, as a read only copy.</summary>
    public IReadOnlyList<GuildMembership> Items
    {
        get => _items;
        init => _items = PeopleWire.FreezeReferences(value, nameof(Items));
    }

    /// <summary>Deconstructs the message into its groups.</summary>
    /// <param name="items">The groups.</param>
    public void Deconstruct(out IReadOnlyList<GuildMembership> items) => items = Items;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
