using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents the details of a group.</summary>
/// <remarks>Received as the Flash <c>HabboGroupDetails</c> message.</remarks>
/// <param name="Id">The group identifier.</param>
/// <param name="IsGuild">The guild flag the hotel sends with the group.</param>
/// <param name="Type">The group type as the hotel numbers it.</param>
/// <param name="Name">The group name.</param>
/// <param name="Description">The group description.</param>
/// <param name="BadgeCode">The group's badge code.</param>
/// <param name="RoomId">The identifier of the group's home room.</param>
/// <param name="RoomName">The name of the group's home room.</param>
/// <param name="MemberStatus">The local user's membership status as the hotel numbers it.</param>
/// <param name="MemberCount">The number of members.</param>
/// <param name="IsFavourite">Whether this is the local user's favorite group.</param>
/// <param name="Created">The creation date as the hotel formats it.</param>
/// <param name="IsOwner">Whether the local user owns the group.</param>
/// <param name="IsAdmin">Whether the local user is an administrator of the group.</param>
/// <param name="OwnerName">The name of the group's owner.</param>
/// <param name="OpenDetails">Whether the client should open the group details window for this response.</param>
/// <param name="MembersCanDecorate">Whether members may decorate the group's home room.</param>
/// <param name="PendingMemberCount">The number of pending membership requests.</param>
/// <param name="HasBoard">Whether the group has a forum board.</param>
/// <param name="MemberLimit">The member limit, or <see langword="null"/> when the packet does not carry it.</param>
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
    /// <summary>Reads group details from a packet.</summary>
    /// <remarks><see cref="MemberLimit"/> is read only when at least 4 bytes remain after the fixed fields.</remarks>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when bytes remain after the last field.</exception>
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

    /// <summary>Writes the group details to a packet, including <see cref="MemberLimit"/> when it has a value.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="InvalidDataException">Thrown when an identifier does not fit in 32 bits or a string is too long.</exception>
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
