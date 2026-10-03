using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a badge a user wears in one of their badge slots.</summary>
/// <param name="Slot">The one-based badge slot.</param>
/// <param name="Code">The badge code.</param>
/// <param name="OwnerCount">The owner count the hotel reports for the badge, or 0 when <paramref name="HasRarityData"/> is <see langword="false"/>.</param>
/// <param name="RarityId">The rarity identifier the hotel reports for the badge, or 0 when <paramref name="HasRarityData"/> is <see langword="false"/>.</param>
/// <param name="HasRarityData">Whether the packet carried <paramref name="OwnerCount"/> and <paramref name="RarityId"/>.</param>
public readonly record struct SelectedBadge(
    int Slot,
    string Code,
    int OwnerCount,
    int RarityId,
    bool HasRarityData = true)
{
    /// <summary>Initializes a new instance of the <see cref="SelectedBadge"/> struct with rarity data.</summary>
    /// <param name="Slot">The one-based badge slot.</param>
    /// <param name="Code">The badge code.</param>
    /// <param name="first">The owner count, stored in <see cref="OwnerCount"/>.</param>
    /// <param name="second">The rarity identifier, stored in <see cref="RarityId"/>.</param>
    public SelectedBadge(int Slot, string Code, int first, int second)
        : this(Slot, Code, first, second, true)
    {
    }

    /// <summary>Gets the zero-based badge slot, which is <see cref="Slot"/> minus 1.</summary>
    public int SlotIndex => Slot - 1;

    /// <summary>Gets the owner count, the same value as <see cref="OwnerCount"/>.</summary>
    public int First
    {
        get => OwnerCount;
        init => OwnerCount = value;
    }

    /// <summary>Gets the rarity identifier, the same value as <see cref="RarityId"/>.</summary>
    public int Second
    {
        get => RarityId;
        init => RarityId = value;
    }

    /// <summary>Deconstructs the badge into its slot, code and rarity values.</summary>
    /// <param name="Slot">The one-based badge slot.</param>
    /// <param name="Code">The badge code.</param>
    /// <param name="first">The owner count.</param>
    /// <param name="second">The rarity identifier.</param>
    public void Deconstruct(out int Slot, out string Code, out int first, out int second)
    {
        Slot = this.Slot;
        Code = this.Code;
        first = OwnerCount;
        second = RarityId;
    }
}

/// <summary>Represents the badges a user wears.</summary>
/// <remarks>Received as the Flash <c>HabboUserBadges</c> message.</remarks>
public sealed record UserBadges : IParserComposer<UserBadges>
{
    private IReadOnlyList<SelectedBadge> _badges =
        Array.AsReadOnly(Array.Empty<SelectedBadge>());

    /// <summary>Initializes a new instance of the <see cref="UserBadges"/> class.</summary>
    /// <param name="userId">The user who wears the badges.</param>
    /// <param name="badges">The worn badges; the list is copied.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="badges"/> is <see langword="null"/>.</exception>
    public UserBadges(Id userId, IReadOnlyList<SelectedBadge> badges)
    {
        UserId = userId;
        Badges = badges;
    }

    /// <summary>Gets the user who wears the badges.</summary>
    public Id UserId { get; init; }

    /// <summary>Gets the worn badges as a read-only copy.</summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
    public IReadOnlyList<SelectedBadge> Badges
    {
        get => _badges;
        init => _badges = AchievementBadgeWire.FreezeValues(value, nameof(Badges));
    }

    /// <summary>Deconstructs the value into its user and badges.</summary>
    /// <param name="userId">The user who wears the badges.</param>
    /// <param name="badges">The worn badges.</param>
    public void Deconstruct(out Id userId, out IReadOnlyList<SelectedBadge> badges)
    {
        userId = UserId;
        badges = Badges;
    }

    /// <summary>Reads the worn badges from a packet.</summary>
    /// <remarks>
    /// The packet is checked against both entry layouts, with and without rarity data, and exactly
    /// one of them has to fit.
    /// </remarks>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when neither layout fits or both do.</exception>
    public static UserBadges Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UserBadges ParseFlash(in PacketReader p)
    {
        ReadHeader(in p, out Id user_id, out int count);
        if (count == 0)
        {
            AchievementBadgeWire.RequireEmpty(in p, nameof(UserBadges));
            return new UserBadges(user_id, Array.Empty<SelectedBadge>());
        }

        int start = p.Pos;
        _ = TryParseEntries(in p, count, false, out bool compact_valid);
        p.Pos = start;
        _ = TryParseEntries(in p, count, true, out bool expanded_valid);
        p.Pos = start;
        if (compact_valid == expanded_valid)
        {
            throw new InvalidDataException(compact_valid
                ? "Selected badge entry layout is ambiguous."
                : "Selected badge entry layout is unsupported.");
        }
        SelectedBadge[] badges = ParseEntries(in p, count, expanded_valid);
        AchievementBadgeWire.RequireEmpty(in p, nameof(UserBadges));
        return new UserBadges(user_id, badges);
    }

    /// <summary>Writes the worn badges to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="InvalidDataException">Thrown when the badges mix entries with and without rarity data.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UserBadges value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ReadHeader(
        in PacketReader p,
        out Id user_id,
        out int count)
    {
        int count_width = AchievementBadgeWire.CountWidth;
        AchievementBadgeWire.RequireRemaining(
            in p,
            checked(AchievementBadgeWire.UserIdWidth + count_width),
            0,
            nameof(UserBadges));
        user_id = AchievementBadgeWire.ReadUserId(in p, count_width, nameof(UserId));
        count = AchievementBadgeWire.ReadCount(
            in p,
            AchievementBadgeWire.SelectedBadgeMinimumBytes,
            0,
            nameof(Badges));
    }

    private static SelectedBadge[] ParseEntries(
        in PacketReader p,
        int count,
        bool has_rarity_data)
    {
        int minimum_bytes = checked(
            AchievementBadgeWire.SelectedBadgeMinimumBytes +
            (has_rarity_data ? sizeof(int) * 2 : 0));
        AchievementBadgeWire.RequireRemaining(
            in p,
            checked(count * minimum_bytes),
            0,
            nameof(Badges));
        var strings = AchievementBadgeWire.NewStringBudget();
        var badges = new SelectedBadge[count];
        for (int index = 0; index < badges.Length; index++)
        {
            int sibling_bytes = checked((badges.Length - index - 1) * minimum_bytes);
            AchievementBadgeWire.RequireRemaining(
                in p,
                minimum_bytes,
                sibling_bytes,
                nameof(SelectedBadge));
            int slot = p.ReadInt();
            string code = strings.Read(
                in p,
                nameof(SelectedBadge.Code),
                checked(sibling_bytes + (has_rarity_data ? sizeof(int) * 2 : 0)));
            badges[index] = has_rarity_data
                ? new SelectedBadge(slot, code, p.ReadInt(), p.ReadInt())
                : new SelectedBadge(slot, code, 0, 0, false);
        }
        return badges;
    }

    private static SelectedBadge[]? TryParseEntries(
        in PacketReader p,
        int count,
        bool has_rarity_data,
        out bool valid)
    {
        try
        {
            SelectedBadge[] badges = ParseEntries(in p, count, has_rarity_data);
            AchievementBadgeWire.RequireEmpty(in p, nameof(UserBadges));
            valid = true;
            return badges;
        }
        catch (Exception error) when (
            error is InvalidDataException or
                IndexOutOfRangeException or
                ArgumentOutOfRangeException or
                OverflowException)
        {
            valid = false;
            return null;
        }
    }

    private static void ComposeMessage(
        UserBadges value,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        AchievementBadgeWire.RequireUserId(value.UserId);
        int count = AchievementBadgeWire.RequireListCount(value.Badges, nameof(value.Badges));
        var strings = AchievementBadgeWire.NewStringBudget();
        var badges = new SelectedBadgeWireValue[count];
        bool? has_rarity_data = null;
        for (int index = 0; index < badges.Length; index++)
        {
            SelectedBadge badge = value.Badges[index];
            if (has_rarity_data is bool expected && expected != badge.HasRarityData)
                throw new InvalidDataException("Selected badge entries cannot mix wire layouts.");
            has_rarity_data = badge.HasRarityData;
            strings.Require(badge.Code, nameof(SelectedBadge.Code), in p);
            badges[index] = new SelectedBadgeWireValue(
                badge.Slot,
                badge.Code,
                badge.OwnerCount,
                badge.RarityId,
                badge.HasRarityData);
        }

        AchievementBadgeWire.WriteUserId(value.UserId, in p);
        AchievementBadgeWire.WriteCount(badges.Length, in p);
        foreach (SelectedBadgeWireValue badge in badges)
        {
            p.WriteInt(badge.Slot);
            p.WriteString(badge.Code);
            if (badge.HasRarityData)
            {
                p.WriteInt(badge.OwnerCount);
                p.WriteInt(badge.RarityId);
            }
        }
    }
}

internal readonly record struct SelectedBadgeWireValue(
    int Slot,
    string Code,
    int OwnerCount,
    int RarityId,
    bool HasRarityData);
