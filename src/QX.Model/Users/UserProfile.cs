using Qx.Messages;

namespace Qx.Model;

public sealed record ProfileGroup(
    Id Id,
    string Name,
    string BadgeCode,
    string PrimaryColor,
    string SecondaryColor,
    bool IsFavourite,
    Id OwnerId,
    bool HasForum) : IParserComposer<ProfileGroup>
{
    public static ProfileGroup Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ProfileGroup ParseFlash(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadBool(),
            p.ReadInt(),
            p.ReadBool());

    private static ProfileGroup ParseUnity(in PacketReader p) =>
        new(
            p.ReadLong(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadBool(),
            p.ReadLong(),
            p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ProfileGroup value, in PacketWriter p)
    {
        Validate(value, true, in p);
        p.WriteInt(PeopleWire.RequireFlashId(value.Id, nameof(Id)));
        p.WriteString(value.Name);
        p.WriteString(value.BadgeCode);
        p.WriteString(value.PrimaryColor);
        p.WriteString(value.SecondaryColor);
        p.WriteBool(value.IsFavourite);
        p.WriteInt(PeopleWire.RequireFlashId(value.OwnerId, nameof(OwnerId)));
        p.WriteBool(value.HasForum);
    }

    private static void ComposeUnity(ProfileGroup value, in PacketWriter p)
    {
        Validate(value, false, in p);
        p.WriteLong(value.Id);
        p.WriteString(value.Name);
        p.WriteString(value.BadgeCode);
        p.WriteString(value.PrimaryColor);
        p.WriteString(value.SecondaryColor);
        p.WriteBool(value.IsFavourite);
        p.WriteLong(value.OwnerId);
        p.WriteBool(value.HasForum);
    }

    internal static void Validate(ProfileGroup value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (flash)
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

public readonly record struct BadgeRarity(byte RarityId, int Count);

public readonly record struct ProfileOldName(Id Id, string Name) : IParserComposer<ProfileOldName>
{
    public static ProfileOldName Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ProfileOldName ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString());

    private static ProfileOldName ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ProfileOldName value, in PacketWriter p)
    {
        Validate(value, true, in p);
        p.WriteInt(PeopleWire.RequireFlashId(value.Id, nameof(Id)));
        p.WriteString(value.Name);
    }

    private static void ComposeUnity(ProfileOldName value, in PacketWriter p)
    {
        Validate(value, false, in p);
        p.WriteLong(value.Id);
        p.WriteString(value.Name);
    }

    internal static void Validate(ProfileOldName value, bool flash, in PacketWriter p)
    {
        if (flash)
            _ = PeopleWire.RequireFlashId(value.Id, nameof(Id));
        PeopleWire.RequireString(value.Name, nameof(Name), in p);
    }
}

public sealed class UserProfile : IParserComposer<UserProfile>
{
    private IReadOnlyList<ProfileGroup> _groups = Array.AsReadOnly(Array.Empty<ProfileGroup>());
    private IReadOnlyList<BadgeRarity> _badge_rarities = Array.AsReadOnly(Array.Empty<BadgeRarity>());
    private IReadOnlyList<ProfileOldName> _old_names = Array.AsReadOnly(Array.Empty<ProfileOldName>());

    public Id Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Figure { get; set; } = string.Empty;
    public string Motto { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
    public int AchievementScore { get; set; }
    public int FriendCount { get; set; }
    public bool IsFriend { get; set; }
    public bool IsFriendRequestSent { get; set; }
    public int OnlineStatus { get; set; }

    public IReadOnlyList<ProfileGroup> Groups
    {
        get => _groups;
        set => _groups = PeopleWire.FreezeReferences(value, nameof(Groups));
    }

    public int LastAccessSeconds { get; set; }
    public bool OpenProfileWindow { get; set; }
    public bool IsHidden { get; set; }
    public int Level { get; set; }
    public int SubscriptionLevel { get; set; }
    public int StarGems { get; set; }
    public bool AllowFriendRequests { get; set; }
    public bool HasFriendRequestsPending { get; set; }
    public int TotalBadges { get; set; }
    public int AchievementLevel { get; set; }

    public IReadOnlyList<BadgeRarity> BadgeRarities
    {
        get => _badge_rarities;
        set => _badge_rarities = PeopleWire.FreezeValues(value, nameof(BadgeRarities));
    }

    public int TotalBadgesRank { get; set; }
    public string NameColor { get; set; } = string.Empty;

    public IReadOnlyList<ProfileOldName> OldNames
    {
        get => _old_names;
        set => _old_names = PeopleWire.FreezeValues(value, nameof(OldNames));
    }

    public bool IsOnline => OnlineStatus > 0;
    public TimeSpan LastAccess => TimeSpan.FromSeconds(LastAccessSeconds);

    public static UserProfile Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UserProfile ParseFlash(in PacketReader p)
    {
        UserProfile value = ParseCommon(in p, false);
        value.FriendCount = p.ReadInt();
        value.IsFriend = p.ReadBool();
        value.IsFriendRequestSent = p.ReadBool();
        value.OnlineStatus = p.ReadByte();

        int group_count = PeopleWire.ReadFlashCount(
            in p,
            PeopleWire.FlashGroupMinimumBytes,
            nameof(Groups));
        var groups = new ProfileGroup[group_count];
        for (int index = 0; index < groups.Length; index++)
            groups[index] = p.Parse<ProfileGroup>();
        value.Groups = groups;

        value.LastAccessSeconds = p.ReadInt();
        value.OpenProfileWindow = p.ReadBool();
        value.IsHidden = p.ReadBool();
        value.Level = p.ReadInt();
        value.SubscriptionLevel = p.ReadInt();
        value.StarGems = p.ReadInt();
        value.AllowFriendRequests = p.ReadBool();
        value.HasFriendRequestsPending = p.ReadBool();
        value.TotalBadges = p.ReadInt();
        value.AchievementLevel = p.ReadInt();

        int rarity_count = PeopleWire.ReadFlashCount(
            in p,
            PeopleWire.BadgeRarityMinimumBytes,
            nameof(BadgeRarities),
            sizeof(int));
        var rarities = new BadgeRarity[rarity_count];
        for (int index = 0; index < rarities.Length; index++)
            rarities[index] = new BadgeRarity(p.ReadByte(), p.ReadInt());
        value.BadgeRarities = rarities;
        value.TotalBadgesRank = p.ReadInt();
        PeopleWire.RequireEmpty(in p, nameof(UserProfile));
        return value;
    }

    private static UserProfile ParseUnity(in PacketReader p)
    {
        UserProfile value = ParseCommon(in p, true);
        value.FriendCount = -1;
        value.IsFriend = p.ReadBool();
        value.IsFriendRequestSent = p.ReadBool();
        value.OnlineStatus = p.ReadBool() ? 1 : 0;

        int group_count = PeopleWire.ReadUnityCount(
            in p,
            PeopleWire.UnityGroupMinimumBytes,
            nameof(Groups));
        var groups = new ProfileGroup[group_count];
        for (int index = 0; index < groups.Length; index++)
            groups[index] = p.Parse<ProfileGroup>();
        value.Groups = groups;

        value.LastAccessSeconds = p.ReadInt();
        value.OpenProfileWindow = p.ReadBool();
        value.IsHidden = p.ReadBool();
        value.Level = p.ReadInt();
        value.SubscriptionLevel = p.ReadInt();
        value.StarGems = p.ReadInt();
        value.AllowFriendRequests = p.ReadBool();
        value.HasFriendRequestsPending = p.ReadBool();
        value.NameColor = p.ReadString();

        int old_name_count = PeopleWire.ReadUnityCount(
            in p,
            PeopleWire.UnityOldNameMinimumBytes,
            nameof(OldNames));
        var old_names = new ProfileOldName[old_name_count];
        for (int index = 0; index < old_names.Length; index++)
            old_names[index] = p.Parse<ProfileOldName>();
        value.OldNames = old_names;
        PeopleWire.RequireEmpty(in p, nameof(UserProfile));
        return value;
    }

    private static UserProfile ParseCommon(in PacketReader p, bool unity) =>
        new()
        {
            Id = unity ? p.ReadLong() : p.ReadInt(),
            Name = p.ReadString(),
            Figure = p.ReadString(),
            Motto = p.ReadString(),
            Created = p.ReadString(),
            AchievementScore = p.ReadInt()
        };

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UserProfile value, in PacketWriter p)
    {
        UserProfile prepared = Prepare(value, true, in p);
        p.WriteInt(PeopleWire.RequireFlashId(prepared.Id, nameof(Id)));
        ComposeCommon(prepared, in p);
        p.WriteInt(prepared.FriendCount);
        p.WriteBool(prepared.IsFriend);
        p.WriteBool(prepared.IsFriendRequestSent);
        p.WriteByte((byte)prepared.OnlineStatus);
        p.WriteInt(prepared.Groups.Count);
        foreach (ProfileGroup group in prepared.Groups)
            p.Compose(group);
        p.WriteInt(prepared.LastAccessSeconds);
        p.WriteBool(prepared.OpenProfileWindow);
        p.WriteBool(prepared.IsHidden);
        p.WriteInt(prepared.Level);
        p.WriteInt(prepared.SubscriptionLevel);
        p.WriteInt(prepared.StarGems);
        p.WriteBool(prepared.AllowFriendRequests);
        p.WriteBool(prepared.HasFriendRequestsPending);
        p.WriteInt(prepared.TotalBadges);
        p.WriteInt(prepared.AchievementLevel);
        p.WriteInt(prepared.BadgeRarities.Count);
        foreach (BadgeRarity rarity in prepared.BadgeRarities)
        {
            p.WriteByte(rarity.RarityId);
            p.WriteInt(rarity.Count);
        }
        p.WriteInt(prepared.TotalBadgesRank);
    }

    private static void ComposeUnity(UserProfile value, in PacketWriter p)
    {
        UserProfile prepared = Prepare(value, false, in p);
        p.WriteLong(prepared.Id);
        ComposeCommon(prepared, in p);
        p.WriteBool(prepared.IsFriend);
        p.WriteBool(prepared.IsFriendRequestSent);
        p.WriteBool(prepared.IsOnline);
        PeopleWire.WriteUnityCount(prepared.Groups.Count, in p);
        foreach (ProfileGroup group in prepared.Groups)
            p.Compose(group);
        p.WriteInt(prepared.LastAccessSeconds);
        p.WriteBool(prepared.OpenProfileWindow);
        p.WriteBool(prepared.IsHidden);
        p.WriteInt(prepared.Level);
        p.WriteInt(prepared.SubscriptionLevel);
        p.WriteInt(prepared.StarGems);
        p.WriteBool(prepared.AllowFriendRequests);
        p.WriteBool(prepared.HasFriendRequestsPending);
        p.WriteString(prepared.NameColor);
        PeopleWire.WriteUnityCount(prepared.OldNames.Count, in p);
        foreach (ProfileOldName old_name in prepared.OldNames)
            p.Compose(old_name);
    }

    private static void ComposeCommon(UserProfile value, in PacketWriter p)
    {
        p.WriteString(value.Name);
        p.WriteString(value.Figure);
        p.WriteString(value.Motto);
        p.WriteString(value.Created);
        p.WriteInt(value.AchievementScore);
    }

    private static UserProfile Prepare(UserProfile value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var prepared = new UserProfile
        {
            Id = value.Id,
            Name = value.Name,
            Figure = value.Figure,
            Motto = value.Motto,
            Created = value.Created,
            AchievementScore = value.AchievementScore,
            FriendCount = value.FriendCount,
            IsFriend = value.IsFriend,
            IsFriendRequestSent = value.IsFriendRequestSent,
            OnlineStatus = value.OnlineStatus,
            Groups = value.Groups,
            LastAccessSeconds = value.LastAccessSeconds,
            OpenProfileWindow = value.OpenProfileWindow,
            IsHidden = value.IsHidden,
            Level = value.Level,
            SubscriptionLevel = value.SubscriptionLevel,
            StarGems = value.StarGems,
            AllowFriendRequests = value.AllowFriendRequests,
            HasFriendRequestsPending = value.HasFriendRequestsPending,
            TotalBadges = value.TotalBadges,
            AchievementLevel = value.AchievementLevel,
            BadgeRarities = value.BadgeRarities,
            TotalBadgesRank = value.TotalBadgesRank,
            NameColor = value.NameColor,
            OldNames = value.OldNames
        };

        PeopleWire.RequireString(prepared.Name, nameof(Name), in p);
        PeopleWire.RequireString(prepared.Figure, nameof(Figure), in p);
        PeopleWire.RequireString(prepared.Motto, nameof(Motto), in p);
        PeopleWire.RequireString(prepared.Created, nameof(Created), in p);

        if (flash)
        {
            _ = PeopleWire.RequireFlashId(prepared.Id, nameof(Id));
            if ((uint)prepared.OnlineStatus > byte.MaxValue)
                throw new InvalidDataException("OnlineStatus exceeds the Flash wire byte range.");
            if (prepared.NameColor is not "" || prepared.OldNames.Count != 0)
                throw new InvalidDataException("Flash UserProfile cannot contain Unity-only fields.");
        }
        else
        {
            if (prepared.FriendCount != -1 ||
                prepared.OnlineStatus is not (0 or 1) ||
                prepared.TotalBadges != 0 ||
                prepared.AchievementLevel != 0 ||
                prepared.BadgeRarities.Count != 0 ||
                prepared.TotalBadgesRank != 0)
            {
                throw new InvalidDataException("Unity UserProfile cannot contain Flash-only fields.");
            }
            PeopleWire.RequireUnityCount(prepared.Groups.Count, nameof(Groups));
            PeopleWire.RequireUnityCount(prepared.OldNames.Count, nameof(OldNames));
            PeopleWire.RequireString(prepared.NameColor, nameof(NameColor), in p);
        }

        foreach (ProfileGroup group in prepared.Groups)
            ProfileGroup.Validate(group, flash, in p);
        if (!flash)
        {
            foreach (ProfileOldName old_name in prepared.OldNames)
                ProfileOldName.Validate(old_name, false, in p);
        }
        return prepared;
    }
}

internal static class PeopleWire
{
    internal const int FlashRelationshipEntryMinimumBytes =
        sizeof(int) + sizeof(int) + sizeof(int) + sizeof(short) + sizeof(short);
    internal const int UnityRelationshipEntryMinimumBytes =
        sizeof(int) + sizeof(int) + sizeof(long) + sizeof(short) + sizeof(short);
    internal const int FlashGroupMinimumBytes =
        sizeof(int) + 4 * sizeof(short) + sizeof(byte) + sizeof(int) + sizeof(byte);
    internal const int UnityGroupMinimumBytes =
        sizeof(long) + 4 * sizeof(short) + sizeof(byte) + sizeof(long) + sizeof(byte);
    internal const int UnityOldNameMinimumBytes = sizeof(long) + sizeof(short);
    internal const int BadgeRarityMinimumBytes = sizeof(byte) + sizeof(int);
    internal const int SelectedBadgeMinimumBytes = sizeof(int) + sizeof(short);

    internal static int ReadFlashCount(
        in PacketReader p,
        int minimum_bytes,
        string name,
        int trailing_bytes = 0) =>
        RequireCount(p.ReadInt(), p.Available - trailing_bytes, minimum_bytes, name);

    internal static int ReadUnityCount(
        in PacketReader p,
        int minimum_bytes,
        string name,
        int trailing_bytes = 0) =>
        RequireCount(
            unchecked((ushort)p.ReadShort()),
            p.Available - trailing_bytes,
            minimum_bytes,
            name);

    internal static void RequireUnityCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the Unity wire limit.");
    }

    internal static int RequireFlashId(Id value, string name)
    {
        try
        {
            return checked((int)(long)value);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException($"{name} does not fit the 32-bit wire format.", exception);
        }
    }

    internal static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }

    internal static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    internal static void WriteUnityCount(int count, in PacketWriter p)
    {
        RequireUnityCount(count, nameof(count));
        p.WriteShort(unchecked((short)(ushort)count));
    }

    internal static IReadOnlyList<T> FreezeReferences<T>(IReadOnlyList<T> values, string name)
        where T : class
    {
        T[] copy = SnapshotReferences(values, name);
        return Array.AsReadOnly(copy);
    }

    internal static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string name)
    {
        T[] copy = SnapshotValues(values, name);
        return Array.AsReadOnly(copy);
    }

    internal static T[] SnapshotReferences<T>(IReadOnlyList<T> values, string name)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, name);
        T[] copy = values.ToArray();
        foreach (T value in copy)
            ArgumentNullException.ThrowIfNull(value, name);
        return copy;
    }

    internal static T[] SnapshotValues<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return values.ToArray();
    }

    private static int RequireCount(
        int count,
        int available,
        int minimum_bytes,
        string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum_bytes);
        if (available < 0 || count > available / minimum_bytes)
            throw new InvalidDataException($"{name} count {count} exceeds the remaining payload capacity.");
        return count;
    }
}
