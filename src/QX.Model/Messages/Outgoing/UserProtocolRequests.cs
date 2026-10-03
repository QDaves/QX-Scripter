using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Requests the list of users the user has blocked.</summary>
/// <remarks>Sent as the Flash <c>BlockListInit</c> message, which carries no fields.</remarks>
public sealed record BlockListRequest : IParserComposer<BlockListRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BlockListRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BlockListRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BlockListRequest value, in PacketWriter p) { }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(BlockListRequest)} contains {p.Available} unexpected bytes.");
    }
}

/// <summary>Sent when the user blocks another user.</summary>
/// <remarks>Sent as the Flash <c>BlockUser</c> message.</remarks>
/// <param name="UserId">The id of the user to block, written as a 32 bit integer.</param>
public sealed record BlockUserRequest(Id UserId) : IParserComposer<BlockUserRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BlockUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BlockUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BlockUserRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));
}

/// <summary>Sent when the user unblocks a blocked user.</summary>
/// <remarks>Sent as the Flash <c>UnblockUser</c> message.</remarks>
/// <param name="UserId">The id of the user to unblock, written as a 32 bit integer.</param>
public sealed record UnblockUserRequest(Id UserId) : IParserComposer<UnblockUserRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UnblockUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UnblockUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UnblockUserRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));
}

/// <summary>Requests the list of users the user ignores.</summary>
/// <remarks>Sent as the Flash <c>GetIgnoredUsers</c> message, which carries no fields.</remarks>
public sealed record IgnoreListRequest : IParserComposer<IgnoreListRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static IgnoreListRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static IgnoreListRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(IgnoreListRequest value, in PacketWriter p) { }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(IgnoreListRequest)} contains {p.Available} unexpected bytes.");
    }
}

/// <summary>Specifies how a user is identified in a request.</summary>
public enum UserIdentityKind
{
    /// <summary>The user is identified by id.</summary>
    Id,
    /// <summary>The user is identified by name.</summary>
    Name
}

/// <summary>Sent when the user stops ignoring another user.</summary>
/// <remarks>Sent as the Flash <c>UnignoreUser</c> message, which carries the user id as a 32 bit integer. Composing throws <see cref="InvalidDataException"/> when the request identifies the user by name.</remarks>
public sealed record UnignoreUserRequest : IParserComposer<UnignoreUserRequest>
{
    /// <summary>Initializes a new instance of the <see cref="UnignoreUserRequest"/> record that identifies the user by id.</summary>
    /// <param name="userId">The id of the user to stop ignoring.</param>
    public UnignoreUserRequest(Id userId)
    {
        Kind = UserIdentityKind.Id;
        UserId = userId;
    }

    /// <summary>Initializes a new instance of the <see cref="UnignoreUserRequest"/> record that identifies the user by name.</summary>
    /// <param name="userName">The name of the user to stop ignoring.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userName"/> is <see langword="null"/> or empty.</exception>
    public UnignoreUserRequest(string userName)
    {
        ArgumentException.ThrowIfNullOrEmpty(userName);
        Kind = UserIdentityKind.Name;
        UserName = userName;
    }

    /// <summary>Gets how the request identifies the user.</summary>
    public UserIdentityKind Kind { get; }
    /// <summary>Gets the id of the user, or <see langword="null"/> when the request identifies the user by name.</summary>
    public Id? UserId { get; }
    /// <summary>Gets the name of the user, or <see langword="null"/> when the request identifies the user by id.</summary>
    public string? UserName { get; }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UnignoreUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UnignoreUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UnignoreUserRequest value, in PacketWriter p)
    {
        if (value.Kind is not UserIdentityKind.Id || value.UserId is not Id user_id)
            throw new InvalidDataException("Flash unignore requests require a user id.");
        p.WriteInt(checked((int)user_id));
    }

    private static void RequireString(string value, in PacketWriter p)
    {
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException("UserName exceeds the wire string limit.", nameof(UserName));
    }
}

/// <summary>Requests the user's own account data.</summary>
/// <remarks>Sent as the Flash <c>InfoRetrieve</c> message, which carries no fields.</remarks>
public sealed record ProfileRequest : IParserComposer<ProfileRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ProfileRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ProfileRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ProfileRequest value, in PacketWriter p) { }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(ProfileRequest)} contains {p.Available} unexpected bytes.");
    }
}

/// <summary>Requests the user's sanction status.</summary>
/// <remarks>Sent as the Flash <c>GetMySanctionStatus</c> message, which carries no fields.</remarks>
public sealed record SanctionStatusRequest : IParserComposer<SanctionStatusRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SanctionStatusRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SanctionStatusRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SanctionStatusRequest value, in PacketWriter p) { }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(SanctionStatusRequest)} contains {p.Available} unexpected bytes.");
    }
}

/// <summary>Sent when the user changes the motto.</summary>
/// <remarks>Sent as the Flash <c>ChangeMotto</c> message. Composing throws when <paramref name="Motto"/> is <see langword="null"/> or exceeds 65535 bytes.</remarks>
/// <param name="Motto">The new motto.</param>
public sealed record MottoUpdateRequest(string Motto) : IParserComposer<MottoUpdateRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static MottoUpdateRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MottoUpdateRequest ParseFlash(in PacketReader p) => new(p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MottoUpdateRequest value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteString(value.Motto);
    }

    private static void Validate(MottoUpdateRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Motto, nameof(Motto));
        if (p.Encoding.GetByteCount(value.Motto) > ushort.MaxValue)
            throw new ArgumentException("String exceeds the protocol limit.", nameof(Motto));
    }
}

/// <summary>Sent when the user selects a group as the favorite group.</summary>
/// <remarks>Sent as the Flash <c>SelectFavouriteHabboGroup</c> message.</remarks>
/// <param name="GroupId">The id of the group, written as a 32 bit integer.</param>
public sealed record SelectFavoriteGroupRequest(Id GroupId)
    : IParserComposer<SelectFavoriteGroupRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SelectFavoriteGroupRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SelectFavoriteGroupRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SelectFavoriteGroupRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.GroupId));
}

/// <summary>Sent when the user clears a group as the favorite group.</summary>
/// <remarks>Sent as the Flash <c>DeselectFavouriteHabboGroup</c> message.</remarks>
/// <param name="GroupId">The id of the group, written as a 32 bit integer.</param>
public sealed record DeselectFavoriteGroupRequest(Id GroupId)
    : IParserComposer<DeselectFavoriteGroupRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static DeselectFavoriteGroupRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DeselectFavoriteGroupRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DeselectFavoriteGroupRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.GroupId));
}

/// <summary>Sent when the user ignores another user.</summary>
/// <remarks>Sent as the Flash <c>IgnoreUser</c> message.</remarks>
/// <param name="UserId">The id of the user to ignore, written as a 32 bit integer.</param>
public sealed record IgnoreUserByIdRequest(Id UserId)
    : IParserComposer<IgnoreUserByIdRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static IgnoreUserByIdRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static IgnoreUserByIdRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(IgnoreUserByIdRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));
}

/// <summary>Requests the extended profile of a user.</summary>
/// <remarks>Sent as the Flash <c>GetExtendedProfile</c> message.</remarks>
/// <param name="UserId">The id of the user, written as a 32 bit integer.</param>
/// <param name="OpenInClient">Whether the client opens the profile window for the reply.</param>
public sealed record ExtendedProfileRequest(Id UserId, bool OpenInClient)
    : IParserComposer<ExtendedProfileRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ExtendedProfileRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ExtendedProfileRequest ParseFlash(in PacketReader p)
    {
        var value = new ExtendedProfileRequest(p.ReadInt(), p.ReadBool());
        RequireEmpty(in p);
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ExtendedProfileRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int user_id = checked((int)(long)value.UserId);
        p.WriteInt(user_id);
        p.WriteBool(value.OpenInClient);
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(ExtendedProfileRequest)} contains {p.Available} unexpected bytes.");
    }
}

/// <summary>Requests the relationships a user shows on the profile.</summary>
/// <remarks>Sent as the Flash <c>GetRelationshipStatusInfo</c> message.</remarks>
/// <param name="UserId">The id of the user, written as a 32 bit integer.</param>
public sealed record RelationshipStatusRequest(Id UserId)
    : IParserComposer<RelationshipStatusRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RelationshipStatusRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RelationshipStatusRequest ParseFlash(in PacketReader p)
    {
        var value = new RelationshipStatusRequest(p.ReadInt());
        RequireEmpty(in p);
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RelationshipStatusRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int user_id = checked((int)(long)value.UserId);
        p.WriteInt(user_id);
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(RelationshipStatusRequest)} contains {p.Available} unexpected bytes.");
    }
}

/// <summary>Requests the badges a user is wearing.</summary>
/// <remarks>Sent as the Flash <c>GetSelectedBadges</c> message.</remarks>
/// <param name="UserId">The id of the user, written as a 32 bit integer.</param>
public sealed record SelectedBadgesRequest(Id UserId)
    : IParserComposer<SelectedBadgesRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SelectedBadgesRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SelectedBadgesRequest ParseFlash(in PacketReader p)
    {
        var value = new SelectedBadgesRequest(p.ReadInt());
        RequireEmpty(in p);
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SelectedBadgesRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int user_id = checked((int)(long)value.UserId);
        p.WriteInt(user_id);
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(SelectedBadgesRequest)} contains {p.Available} unexpected bytes.");
    }
}
