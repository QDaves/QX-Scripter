using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a group listed on a user's profile.</summary>
/// <param name="Id">The group identifier.</param>
/// <param name="Name">The group name.</param>
/// <param name="BadgeCode">The group's badge code.</param>
/// <param name="PrimaryColor">The group's primary color as sent by the hotel.</param>
/// <param name="SecondaryColor">The group's secondary color as sent by the hotel.</param>
/// <param name="IsFavourite">Whether this is the user's favorite group.</param>
/// <param name="OwnerId">The identifier of the group's owner.</param>
/// <param name="HasForum">Whether the group has a forum.</param>
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
    /// <summary>Reads a profile group from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static ProfileGroup Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Writes the profile group to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="InvalidDataException">Thrown when an identifier does not fit in 32 bits or a string is too long.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ProfileGroup value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(PeopleWire.RequireFlashId(value.Id, nameof(Id)));
        p.WriteString(value.Name);
        p.WriteString(value.BadgeCode);
        p.WriteString(value.PrimaryColor);
        p.WriteString(value.SecondaryColor);
        p.WriteBool(value.IsFavourite);
        p.WriteInt(PeopleWire.RequireFlashId(value.OwnerId, nameof(OwnerId)));
        p.WriteBool(value.HasForum);
    }

    internal static void Validate(ProfileGroup value, in PacketWriter p)
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

/// <summary>Represents a badge rarity tier and its count on a user's profile.</summary>
/// <param name="RarityId">The rarity tier identifier.</param>
/// <param name="Count">The count the hotel reports for the tier.</param>
public readonly record struct BadgeRarity(byte RarityId, int Count);

/// <summary>Represents a user's extended profile.</summary>
/// <remarks>Received as the Flash <c>ExtendedProfile</c> message.</remarks>
public sealed class UserProfile : IParserComposer<UserProfile>
{
    private IReadOnlyList<ProfileGroup> _groups = Array.AsReadOnly(Array.Empty<ProfileGroup>());
    private IReadOnlyList<BadgeRarity> _badge_rarities = Array.AsReadOnly(Array.Empty<BadgeRarity>());

    /// <summary>Gets or sets the user identifier.</summary>
    public Id Id { get; set; }
    /// <summary>Gets or sets the user's name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the user's figure string.</summary>
    public string Figure { get; set; } = string.Empty;
    /// <summary>Gets or sets the user's motto.</summary>
    public string Motto { get; set; } = string.Empty;
    /// <summary>Gets or sets the account creation date as the hotel formats it.</summary>
    public string Created { get; set; } = string.Empty;
    /// <summary>Gets or sets the user's achievement score.</summary>
    public int AchievementScore { get; set; }
    /// <summary>Gets or sets the number of friends the user has.</summary>
    public int FriendCount { get; set; }
    /// <summary>Gets or sets whether the user is a friend of the local user.</summary>
    public bool IsFriend { get; set; }
    /// <summary>Gets or sets whether the local user has sent the user a friend request.</summary>
    public bool IsFriendRequestSent { get; set; }
    /// <summary>Gets or sets the online status byte, where 0 means offline.</summary>
    /// <remarks>It is sent as one byte, so <see cref="Compose"/> requires a value from 0 to 255.</remarks>
    public int OnlineStatus { get; set; }

    /// <summary>Gets or sets the groups on the profile; the list is copied on set.</summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/> or a list with a <see langword="null"/> entry.</exception>
    public IReadOnlyList<ProfileGroup> Groups
    {
        get => _groups;
        set => _groups = PeopleWire.FreezeReferences(value, nameof(Groups));
    }

    /// <summary>Gets or sets the seconds since the user was last online.</summary>
    public int LastAccessSeconds { get; set; }
    /// <summary>Gets or sets whether the client should open the profile window for this response.</summary>
    /// <remarks>This echoes the flag of the profile request.</remarks>
    public bool OpenProfileWindow { get; set; }
    /// <summary>Gets or sets whether the profile is hidden.</summary>
    public bool IsHidden { get; set; }
    /// <summary>Gets or sets the user's level as sent by the hotel.</summary>
    public int Level { get; set; }
    /// <summary>Gets or sets the user's subscription level as sent by the hotel.</summary>
    public int SubscriptionLevel { get; set; }
    /// <summary>Gets or sets the user's star gem count as sent by the hotel.</summary>
    public int StarGems { get; set; }
    /// <summary>Gets or sets whether the user accepts friend requests.</summary>
    public bool AllowFriendRequests { get; set; }
    /// <summary>Gets or sets whether the hotel reports pending friend requests for the user.</summary>
    public bool HasFriendRequestsPending { get; set; }
    /// <summary>Gets or sets the total number of badges the user owns.</summary>
    public int TotalBadges { get; set; }
    /// <summary>Gets or sets the user's achievement level as sent by the hotel.</summary>
    public int AchievementLevel { get; set; }

    /// <summary>Gets or sets the badge rarity tiers on the profile; the list is copied on set.</summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
    public IReadOnlyList<BadgeRarity> BadgeRarities
    {
        get => _badge_rarities;
        set => _badge_rarities = PeopleWire.FreezeValues(value, nameof(BadgeRarities));
    }

    /// <summary>Gets or sets the user's rank by total badges as sent by the hotel.</summary>
    public int TotalBadgesRank { get; set; }

    /// <summary>Gets whether <see cref="OnlineStatus"/> is above 0.</summary>
    public bool IsOnline => OnlineStatus > 0;
    /// <summary>Gets the time since the user was last online, from <see cref="LastAccessSeconds"/>.</summary>
    public TimeSpan LastAccess => TimeSpan.FromSeconds(LastAccessSeconds);

    /// <summary>Reads a user profile from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when a count is invalid or bytes remain after the last field.</exception>
    public static UserProfile Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UserProfile ParseFlash(in PacketReader p)
    {
        UserProfile value = ParseCommon(in p);
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

    private static UserProfile ParseCommon(in PacketReader p) =>
        new()
        {
            Id = p.ReadInt(),
            Name = p.ReadString(),
            Figure = p.ReadString(),
            Motto = p.ReadString(),
            Created = p.ReadString(),
            AchievementScore = p.ReadInt()
        };

    /// <summary>Writes the user profile to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when an identifier does not fit in 32 bits, a string is too long or
    /// <see cref="OnlineStatus"/> is outside 0 to 255.
    /// </exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UserProfile value, in PacketWriter p)
    {
        UserProfile prepared = Prepare(value, in p);
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

    private static void ComposeCommon(UserProfile value, in PacketWriter p)
    {
        p.WriteString(value.Name);
        p.WriteString(value.Figure);
        p.WriteString(value.Motto);
        p.WriteString(value.Created);
        p.WriteInt(value.AchievementScore);
    }

    private static UserProfile Prepare(UserProfile value, in PacketWriter p)
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
            TotalBadgesRank = value.TotalBadgesRank
        };

        PeopleWire.RequireString(prepared.Name, nameof(Name), in p);
        PeopleWire.RequireString(prepared.Figure, nameof(Figure), in p);
        PeopleWire.RequireString(prepared.Motto, nameof(Motto), in p);
        PeopleWire.RequireString(prepared.Created, nameof(Created), in p);

        {
            _ = PeopleWire.RequireFlashId(prepared.Id, nameof(Id));
            if ((uint)prepared.OnlineStatus > byte.MaxValue)
                throw new InvalidDataException("OnlineStatus exceeds the Flash wire byte range.");
        }

        foreach (ProfileGroup group in prepared.Groups)
            ProfileGroup.Validate(group, in p);
        return prepared;
    }
}

internal static class PeopleWire
{
    internal const int FlashRelationshipEntryMinimumBytes =
        sizeof(int) + sizeof(int) + sizeof(int) + sizeof(short) + sizeof(short);
    internal const int FlashGroupMinimumBytes =
        sizeof(int) + 4 * sizeof(short) + sizeof(byte) + sizeof(int) + sizeof(byte);
    internal const int BadgeRarityMinimumBytes = sizeof(byte) + sizeof(int);
    internal const int SelectedBadgeMinimumBytes = sizeof(int) + sizeof(short);

    internal static int ReadFlashCount(
        in PacketReader p,
        int minimum_bytes,
        string name,
        int trailing_bytes = 0) =>
        RequireCount(p.ReadInt(), p.Available - trailing_bytes, minimum_bytes, name);

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
