using Qx.Messages;

namespace Qx.Model;

public sealed record GroupData(
    Id Id,
    bool IsGuild,
    int Type,
    string Name,
    string Description,
    string BadgeCode,
    Id RoomId,
    string RoomName,
    int MemberStatus,
    int MemberCount,
    bool IsFavourite,
    string Created,
    bool IsOwner,
    bool IsAdmin,
    string OwnerName,
    bool OpenDetails,
    bool MembersCanDecorate,
    int PendingMemberCount,
    bool HasBoard,
    int? MemberLimit = null) : IParserComposer<GroupData>
{
    public static GroupData Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GroupData ParseFlash(in PacketReader p)
    {
        var value = new GroupData(
            p.ReadInt(),
            p.ReadBool(),
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadInt(),
            p.ReadString(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadBool(),
            p.ReadString(),
            p.ReadBool(),
            p.ReadBool(),
            p.ReadString(),
            p.ReadBool(),
            p.ReadBool(),
            p.ReadInt(),
            p.ReadBool());
        if (p.Available >= 4)
            value = value with { MemberLimit = p.ReadInt() };
        PeopleWire.RequireEmpty(in p, nameof(GroupData));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GroupData value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(PeopleWire.RequireFlashId(value.Id, nameof(Id)));
        p.WriteBool(value.IsGuild);
        p.WriteInt(value.Type);
        p.WriteString(value.Name);
        p.WriteString(value.Description);
        p.WriteString(value.BadgeCode);
        p.WriteInt(PeopleWire.RequireFlashId(value.RoomId, nameof(RoomId)));
        ComposeTail(value, in p);
        if (value.MemberLimit is { } limit)
            p.WriteInt(limit);
    }

    private static void ComposeTail(GroupData value, in PacketWriter p)
    {
        p.WriteString(value.RoomName);
        p.WriteInt(value.MemberStatus);
        p.WriteInt(value.MemberCount);
        p.WriteBool(value.IsFavourite);
        p.WriteString(value.Created);
        p.WriteBool(value.IsOwner);
        p.WriteBool(value.IsAdmin);
        p.WriteString(value.OwnerName);
        p.WriteBool(value.OpenDetails);
        p.WriteBool(value.MembersCanDecorate);
        p.WriteInt(value.PendingMemberCount);
        p.WriteBool(value.HasBoard);
    }

    private static void Validate(GroupData value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        {
            _ = PeopleWire.RequireFlashId(value.Id, nameof(Id));
            _ = PeopleWire.RequireFlashId(value.RoomId, nameof(RoomId));
        }
        PeopleWire.RequireString(value.Name, nameof(Name), in p);
        PeopleWire.RequireString(value.Description, nameof(Description), in p);
        PeopleWire.RequireString(value.BadgeCode, nameof(BadgeCode), in p);
        PeopleWire.RequireString(value.RoomName, nameof(RoomName), in p);
        PeopleWire.RequireString(value.Created, nameof(Created), in p);
        PeopleWire.RequireString(value.OwnerName, nameof(OwnerName), in p);
    }
}
