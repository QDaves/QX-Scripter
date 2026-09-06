using Qx.Messages;

namespace Qx.Model;

public readonly record struct SelectedBadge(
    int Slot,
    string Code,
    int OwnerCount,
    int RarityId,
    bool HasRarityData = true)
{
    public SelectedBadge(int Slot, string Code, int First, int Second)
        : this(Slot, Code, First, Second, true)
    {
    }

    public int SlotIndex => Slot - 1;

    public int First
    {
        get => OwnerCount;
        init => OwnerCount = value;
    }

    public int Second
    {
        get => RarityId;
        init => RarityId = value;
    }

    public void Deconstruct(out int Slot, out string Code, out int First, out int Second)
    {
        Slot = this.Slot;
        Code = this.Code;
        First = OwnerCount;
        Second = RarityId;
    }
}

public sealed record UserBadges : IParserComposer<UserBadges>
{
    private IReadOnlyList<SelectedBadge> _badges =
        Array.AsReadOnly(Array.Empty<SelectedBadge>());

    public UserBadges(Id UserId, IReadOnlyList<SelectedBadge> Badges)
    {
        this.UserId = UserId;
        this.Badges = Badges;
    }

    public Id UserId { get; init; }

    public IReadOnlyList<SelectedBadge> Badges
    {
        get => _badges;
        init => _badges = AchievementBadgeWire.FreezeValues(value, nameof(Badges));
    }

    public void Deconstruct(out Id UserId, out IReadOnlyList<SelectedBadge> Badges)
    {
        UserId = this.UserId;
        Badges = this.Badges;
    }

    public static UserBadges Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static UserBadges ParseUnity(in PacketReader p)
    {
        ReadHeader(in p, out Id user_id, out int count);
        SelectedBadge[] badges = ParseEntries(in p, count, false);
        AchievementBadgeWire.RequireEmpty(in p, nameof(UserBadges));
        return new UserBadges(user_id, badges);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UserBadges value, in PacketWriter p) =>
        ComposeMessage(value, true, in p);

    private static void ComposeUnity(UserBadges value, in PacketWriter p) =>
        ComposeMessage(value, false, in p);

    private static void ReadHeader(
        in PacketReader p,
        out Id user_id,
        out int count)
    {
        int count_width = AchievementBadgeWire.CountWidth(p.Client);
        AchievementBadgeWire.RequireRemaining(
            in p,
            checked(AchievementBadgeWire.UserIdWidth(p.Client) + count_width),
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
        bool allows_rarity_data,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        AchievementBadgeWire.RequireUserId(value.UserId, p.Client);
        int count = AchievementBadgeWire.RequireListCount(value.Badges, nameof(value.Badges));
        var strings = AchievementBadgeWire.NewStringBudget();
        var badges = new SelectedBadgeWireValue[count];
        bool? has_rarity_data = null;
        for (int index = 0; index < badges.Length; index++)
        {
            SelectedBadge badge = value.Badges[index];
            if (has_rarity_data is bool expected && expected != badge.HasRarityData)
                throw new InvalidDataException("Selected badge entries cannot mix wire layouts.");
            if (!allows_rarity_data && badge.HasRarityData)
                throw new InvalidDataException("Unity selected badges cannot contain rarity data.");
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
