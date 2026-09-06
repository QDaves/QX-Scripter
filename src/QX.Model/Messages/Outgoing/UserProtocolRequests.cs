using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record BlockListRequest : IParserComposer<BlockListRequest>
{
    public static BlockListRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BlockListRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    private static BlockListRequest ParseUnity(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BlockListRequest value, in PacketWriter p) { }

    private static void ComposeUnity(BlockListRequest value, in PacketWriter p) { }

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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BlockUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static BlockUserRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BlockUserRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));

    private static void ComposeUnity(BlockUserRequest value, in PacketWriter p) =>
        p.WriteLong(value.UserId);
}

public sealed record UnblockUserRequest(Id UserId) : IParserComposer<UnblockUserRequest>
{
    public static UnblockUserRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UnblockUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static UnblockUserRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UnblockUserRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));

    private static void ComposeUnity(UnblockUserRequest value, in PacketWriter p) =>
        p.WriteLong(value.UserId);
}

public sealed record IgnoreListRequest : IParserComposer<IgnoreListRequest>
{
    public static IgnoreListRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static IgnoreListRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    private static IgnoreListRequest ParseUnity(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(IgnoreListRequest value, in PacketWriter p) { }

    private static void ComposeUnity(IgnoreListRequest value, in PacketWriter p) { }

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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnityUnresolved);

    private static UnignoreUserRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static UnignoreUserRequest ParseUnityUnresolved(in PacketReader p) =>
        throw new NotSupportedException(
            "Unity unignore requests require a verified header schema projection.");

    public static UnignoreUserRequest ParseUnity(
        in PacketReader p,
        UserIdentityKind wire_kind) => wire_kind switch
    {
        UserIdentityKind.Id => new UnignoreUserRequest(p.ReadLong()),
        UserIdentityKind.Name => new UnignoreUserRequest(p.ReadString()),
        _ => throw new ArgumentOutOfRangeException(nameof(wire_kind), wire_kind, null)
    };

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnityUnresolved);

    private static void ComposeFlash(UnignoreUserRequest value, in PacketWriter p)
    {
        if (value.Kind is not UserIdentityKind.Id || value.UserId is not Id user_id)
            throw new InvalidDataException("Flash unignore requests require a user id.");
        p.WriteInt(checked((int)user_id));
    }

    private static void ComposeUnityUnresolved(UnignoreUserRequest value, in PacketWriter p) =>
        throw new NotSupportedException(
            "Unity unignore requests require a verified header schema projection.");

    public void ComposeUnity(in PacketWriter p, UserIdentityKind wire_kind)
    {
        if (wire_kind != Kind)
            throw new InvalidDataException(
                $"Unity unignore header expects {wire_kind}, but the request contains {Kind}.");

        switch (wire_kind)
        {
            case UserIdentityKind.Id when UserId is Id user_id:
                p.WriteLong(user_id);
                return;
            case UserIdentityKind.Name when UserName is string user_name:
                RequireString(user_name, in p);
                p.WriteString(user_name);
                return;
            default:
                throw new InvalidDataException("The unignore request has no matching identity value.");
        }
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ProfileRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    private static ProfileRequest ParseUnity(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ProfileRequest value, in PacketWriter p) { }

    private static void ComposeUnity(ProfileRequest value, in PacketWriter p) { }

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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SanctionStatusRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    private static SanctionStatusRequest ParseUnity(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SanctionStatusRequest value, in PacketWriter p) { }

    private static void ComposeUnity(SanctionStatusRequest value, in PacketWriter p) { }

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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MottoUpdateRequest ParseFlash(in PacketReader p) => new(p.ReadString());

    private static MottoUpdateRequest ParseUnity(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MottoUpdateRequest value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteString(value.Motto);
    }

    private static void ComposeUnity(MottoUpdateRequest value, in PacketWriter p)
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SelectFavoriteGroupRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static SelectFavoriteGroupRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SelectFavoriteGroupRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.GroupId));

    private static void ComposeUnity(SelectFavoriteGroupRequest value, in PacketWriter p) =>
        p.WriteLong(value.GroupId);
}

public sealed record DeselectFavoriteGroupRequest(Id GroupId)
    : IParserComposer<DeselectFavoriteGroupRequest>
{
    public static DeselectFavoriteGroupRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static DeselectFavoriteGroupRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static DeselectFavoriteGroupRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(DeselectFavoriteGroupRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.GroupId));

    private static void ComposeUnity(DeselectFavoriteGroupRequest value, in PacketWriter p) =>
        p.WriteLong(value.GroupId);
}

public sealed record IgnoreUserByIdRequest(Id UserId)
    : IParserComposer<IgnoreUserByIdRequest>
{
    public static IgnoreUserByIdRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static IgnoreUserByIdRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static IgnoreUserByIdRequest ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(IgnoreUserByIdRequest value, in PacketWriter p) =>
        p.WriteInt(checked((int)value.UserId));

    private static void ComposeUnity(IgnoreUserByIdRequest value, in PacketWriter p) =>
        p.WriteLong(value.UserId);
}

public sealed record IgnoreUserByNameRequest(string UserName)
    : IParserComposer<IgnoreUserByNameRequest>
{
    public static IgnoreUserByNameRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseUnity(in p, ParseUnity);

    private static IgnoreUserByNameRequest ParseUnity(in PacketReader p) =>
        new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeUnity(this, in p, ComposeUnity);

    private static void ComposeUnity(IgnoreUserByNameRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.UserName, nameof(UserName));
        if (p.Encoding.GetByteCount(value.UserName) > ushort.MaxValue)
            throw new ArgumentException("String exceeds the protocol limit.", nameof(UserName));
        p.WriteString(value.UserName);
    }
}

public sealed record ExtendedProfileRequest(Id UserId, bool OpenInClient)
    : IParserComposer<ExtendedProfileRequest>
{
    public static ExtendedProfileRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ExtendedProfileRequest ParseFlash(in PacketReader p)
    {
        var value = new ExtendedProfileRequest(p.ReadInt(), p.ReadBool());
        RequireEmpty(in p);
        return value;
    }

    private static ExtendedProfileRequest ParseUnity(in PacketReader p)
    {
        var value = new ExtendedProfileRequest(p.ReadLong(), p.ReadBool());
        RequireEmpty(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ExtendedProfileRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int user_id = checked((int)(long)value.UserId);
        p.WriteInt(user_id);
        p.WriteBool(value.OpenInClient);
    }

    private static void ComposeUnity(ExtendedProfileRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteLong(value.UserId);
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RelationshipStatusRequest ParseFlash(in PacketReader p)
    {
        var value = new RelationshipStatusRequest(p.ReadInt());
        RequireEmpty(in p);
        return value;
    }

    private static RelationshipStatusRequest ParseUnity(in PacketReader p)
    {
        var value = new RelationshipStatusRequest(p.ReadLong());
        if (p.ReadString().Length != 0)
            throw new InvalidDataException("Unity relationship requests require an empty trailing string.");
        RequireEmpty(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RelationshipStatusRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int user_id = checked((int)(long)value.UserId);
        p.WriteInt(user_id);
    }

    private static void ComposeUnity(RelationshipStatusRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteLong(value.UserId);
        p.WriteString(string.Empty);
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SelectedBadgesRequest ParseFlash(in PacketReader p)
    {
        var value = new SelectedBadgesRequest(p.ReadInt());
        RequireEmpty(in p);
        return value;
    }

    private static SelectedBadgesRequest ParseUnity(in PacketReader p)
    {
        var value = new SelectedBadgesRequest(p.ReadLong());
        RequireEmpty(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SelectedBadgesRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int user_id = checked((int)(long)value.UserId);
        p.WriteInt(user_id);
    }

    private static void ComposeUnity(SelectedBadgesRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteLong(value.UserId);
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(SelectedBadgesRequest)} contains {p.Available} unexpected bytes.");
    }
}
