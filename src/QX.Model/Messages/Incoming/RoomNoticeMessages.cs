using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// A room avatar's favourite-group badge changed.
/// </summary>
/// <remarks>
/// The avatar is named by its room index rather than its user id, so it resolves through the room's
/// avatar list rather than the friend list.
/// </remarks>
/// <param name="RoomIndex">Which avatar in the room.</param>
/// <param name="GroupId">
/// The group now shown, or zero when the badge was cleared. Flash transmits this as a fixed signed
/// 32 bit value.
/// </param>
/// <param name="Status">The hotel's membership status value for that group.</param>
/// <param name="GroupName">The group's name.</param>
public sealed record FavouriteMembershipUpdate(
    int RoomIndex,
    Id GroupId,
    int Status,
    string GroupName) : IParserComposer<FavouriteMembershipUpdate>
{
    public static FavouriteMembershipUpdate Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static FavouriteMembershipUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(FavouriteMembershipUpdate value, in PacketWriter p)
    {
        int group_id = checked((int)value.GroupId);
        ArgumentNullException.ThrowIfNull(value.GroupName, nameof(GroupName));
        if (p.Encoding.GetByteCount(value.GroupName) > ushort.MaxValue)
            throw new ArgumentException("String exceeds the protocol limit.", nameof(GroupName));

        p.WriteInt(value.RoomIndex);
        p.WriteInt(group_id);
        p.WriteInt(value.Status);
        p.WriteString(value.GroupName);
    }
}

/// <summary>
/// A special Flash room-chat signal associated with an avatar.
/// </summary>
/// <param name="UserIndex">Which avatar in the room.</param>
/// <param name="SpecialSystemType">Which special chat signal the hotel sent.</param>
public sealed record SpecialSystemChat(int UserIndex, int SpecialSystemType)
    : IParserComposer<SpecialSystemChat>
{
    public static SpecialSystemChat Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static SpecialSystemChat ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(SpecialSystemChat value, in PacketWriter p)
    {
        p.WriteInt(value.UserIndex);
        p.WriteInt(value.SpecialSystemType);
    }
}

/// <summary>
/// The Flash hotel's messages of the day, delivered on connect.
/// </summary>
/// <param name="Messages">The notices, each already localised by the hotel.</param>
public sealed record MOTDNotification(IReadOnlyList<string> Messages)
    : IParserComposer<MOTDNotification>
{
    public static MOTDNotification Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static MOTDNotification ParseFlash(in PacketReader p)
    {
        int count = p.ReadInt();
        if (count < 0)
            throw new InvalidDataException("Message-of-the-day count cannot be negative.");
        if ((long)count * sizeof(ushort) > p.Available)
            throw new InvalidDataException("Message-of-the-day count exceeds the available payload.");

        var messages = new string[count];
        for (int i = 0; i < count; i++)
            messages[i] = p.ReadString();
        return new MOTDNotification(messages);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(MOTDNotification value, in PacketWriter p)
    {
        string[] messages = Validate(value, in p);
        p.WriteInt(messages.Length);
        foreach (string message in messages)
            p.WriteString(message);
    }

    private static string[] Validate(MOTDNotification value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Messages, nameof(Messages));
        string[] messages = value.Messages.ToArray();
        foreach (string message in messages)
        {
            ArgumentNullException.ThrowIfNull(message, nameof(Messages));
            if (p.Encoding.GetByteCount(message) > ushort.MaxValue)
                throw new ArgumentException("String exceeds the protocol limit.", nameof(Messages));
        }
        return messages;
    }
}
