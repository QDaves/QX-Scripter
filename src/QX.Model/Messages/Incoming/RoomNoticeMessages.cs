using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>FavoriteMembershipUpdate</c> message, also named <c>FavouriteMembershipUpdate</c>, received when a room avatar's favorite group badge changes.</summary>
/// <remarks>
/// The avatar is named by its room index rather than its user id, so it resolves through the room's
/// avatar list rather than the friend list.
/// </remarks>
/// <param name="RoomIndex">The room index of the avatar.</param>
/// <param name="GroupId">
/// The group now shown, or zero when the badge was cleared. Flash transmits this as a fixed signed
/// 32 bit value.
/// </param>
/// <param name="Status">
/// The hotel's membership status value for that group.
/// <c>RoomUsersHandler.onFavoriteMembershipUpdate</c> forwards it on the dispatched event only and
/// never stores it on the avatar, so it is not mirrored onto <see cref="User.GroupStatus"/>.
/// </param>
/// <param name="GroupName">The group's name.</param>
public sealed record FavoriteMembershipUpdate(
    int RoomIndex,
    Id GroupId,
    int Status,
    string GroupName) : IParserComposer<FavoriteMembershipUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FavoriteMembershipUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FavoriteMembershipUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FavoriteMembershipUpdate value, in PacketWriter p)
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

/// <summary>Represents the <c>SpecialSystemChat</c> message, received with a special room chat signal for an avatar.</summary>
/// <param name="UserIndex">The room index of the avatar.</param>
/// <param name="SpecialSystemType">The type of special chat signal the hotel sent.</param>
public sealed record SpecialSystemChat(int UserIndex, int SpecialSystemType)
    : IParserComposer<SpecialSystemChat>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SpecialSystemChat Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SpecialSystemChat ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SpecialSystemChat value, in PacketWriter p)
    {
        p.WriteInt(value.UserIndex);
        p.WriteInt(value.SpecialSystemType);
    }
}

/// <summary>Represents the <c>MOTDNotification</c> message, received with the hotel's messages of the day when the user connects.</summary>
/// <param name="Messages">The notices, each already localized by the hotel.</param>
public sealed record MOTDNotification(IReadOnlyList<string> Messages)
    : IParserComposer<MOTDNotification>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MOTDNotification Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
