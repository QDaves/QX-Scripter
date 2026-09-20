using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record BlockListRequest : IParserComposer<BlockListRequest>
{
    public static BlockListRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BlockListRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

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

public sealed record BlockUserRequest(Id UserId) : IParserComposer<BlockUserRequest>
{
    public static BlockUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BlockUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BlockUserRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));
}

public sealed record UnblockUserRequest(Id UserId) : IParserComposer<UnblockUserRequest>
{
    public static UnblockUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UnblockUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UnblockUserRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));
}

public sealed record IgnoreListRequest : IParserComposer<IgnoreListRequest>
{
    public static IgnoreListRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static IgnoreListRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

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

public enum UserIdentityKind
{
    Id,
    Name
}

public sealed record UnignoreUserRequest : IParserComposer<UnignoreUserRequest>
{
    public UnignoreUserRequest(Id user_id)
    {
        Kind = UserIdentityKind.Id;
        UserId = user_id;
    }

    public UnignoreUserRequest(string user_name)
    {
        ArgumentException.ThrowIfNullOrEmpty(user_name);
        Kind = UserIdentityKind.Name;
        UserName = user_name;
    }

    public UserIdentityKind Kind { get; }
    public Id? UserId { get; }
    public string? UserName { get; }

    public static UnignoreUserRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UnignoreUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

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

public sealed record ProfileRequest : IParserComposer<ProfileRequest>
{
    public static ProfileRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ProfileRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

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

public sealed record SanctionStatusRequest : IParserComposer<SanctionStatusRequest>
{
    public static SanctionStatusRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SanctionStatusRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

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

public sealed record MottoUpdateRequest(string Motto) : IParserComposer<MottoUpdateRequest>
{
    public static MottoUpdateRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MottoUpdateRequest ParseFlash(in PacketReader p) => new(p.ReadString());

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

public sealed record SelectFavoriteGroupRequest(Id GroupId)
    : IParserComposer<SelectFavoriteGroupRequest>
{
    public static SelectFavoriteGroupRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SelectFavoriteGroupRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SelectFavoriteGroupRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.GroupId));
}

public sealed record DeselectFavoriteGroupRequest(Id GroupId)
    : IParserComposer<DeselectFavoriteGroupRequest>
{
    public static DeselectFavoriteGroupRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DeselectFavoriteGroupRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DeselectFavoriteGroupRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.GroupId));
}

public sealed record IgnoreUserByIdRequest(Id UserId)
    : IParserComposer<IgnoreUserByIdRequest>
{
    public static IgnoreUserByIdRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static IgnoreUserByIdRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(IgnoreUserByIdRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));
}

public sealed record ExtendedProfileRequest(Id UserId, bool OpenInClient)
    : IParserComposer<ExtendedProfileRequest>
{
    public static ExtendedProfileRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ExtendedProfileRequest ParseFlash(in PacketReader p)
    {
        var value = new ExtendedProfileRequest(p.ReadInt(), p.ReadBool());
        RequireEmpty(in p);
        return value;
    }

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

public sealed record RelationshipStatusRequest(Id UserId)
    : IParserComposer<RelationshipStatusRequest>
{
    public static RelationshipStatusRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RelationshipStatusRequest ParseFlash(in PacketReader p)
    {
        var value = new RelationshipStatusRequest(p.ReadInt());
        RequireEmpty(in p);
        return value;
    }

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

public sealed record SelectedBadgesRequest(Id UserId)
    : IParserComposer<SelectedBadgesRequest>
{
    public static SelectedBadgesRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SelectedBadgesRequest ParseFlash(in PacketReader p)
    {
        var value = new SelectedBadgesRequest(p.ReadInt());
        RequireEmpty(in p);
        return value;
    }

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
