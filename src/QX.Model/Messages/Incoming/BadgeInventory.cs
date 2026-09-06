using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public readonly record struct OwnedBadge : IParserComposer<OwnedBadge>
{
    public Id NativeBadgeId { get; init; }

    public int BadgeId
    {
        get => checked((int)(long)NativeBadgeId);
        init => NativeBadgeId = value;
    }

    public string Code { get; init; }
    public int OwnerCount { get; init; }
    public int RarityId { get; init; }
    public bool HasRarityData { get; init; }

    public OwnedBadge(int BadgeId, string Code, int OwnerCount, int RarityId)
        : this((Id)BadgeId, Code, OwnerCount, RarityId, true)
    {
    }

    public OwnedBadge(
        int BadgeId,
        string Code,
        int OwnerCount,
        int RarityId,
        bool HasRarityData)
        : this((Id)BadgeId, Code, OwnerCount, RarityId, HasRarityData)
    {
    }

    public OwnedBadge(
        Id BadgeId,
        string Code,
        int OwnerCount,
        int RarityId,
        bool HasRarityData = true)
    {
        NativeBadgeId = BadgeId;
        this.Code = Code;
        this.OwnerCount = OwnerCount;
        this.RarityId = RarityId;
        this.HasRarityData = HasRarityData;
    }

    public void Deconstruct(
        out int BadgeId,
        out string Code,
        out int OwnerCount,
        out int RarityId)
    {
        BadgeId = this.BadgeId;
        Code = this.Code;
        OwnerCount = this.OwnerCount;
        RarityId = this.RarityId;
    }

    public void Deconstruct(
        out Id BadgeId,
        out string Code,
        out int OwnerCount,
        out int RarityId,
        out bool HasRarityData)
    {
        BadgeId = NativeBadgeId;
        Code = this.Code;
        OwnerCount = this.OwnerCount;
        RarityId = this.RarityId;
        HasRarityData = this.HasRarityData;
    }

    public static OwnedBadge Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static OwnedBadge ParseFlash(in PacketReader p)
    {
        AchievementBadgeWire.RequireRemaining(
            in p,
            AchievementBadgeWire.BadgeMinimumBytes,
            0,
            nameof(OwnedBadge));
        var strings = AchievementBadgeWire.NewStringBudget();
        int badge_id = p.ReadInt();
        string code = strings.Read(in p, nameof(Code), 0);
        OwnedBadge value = p.Available switch
        {
            0 => new OwnedBadge(badge_id, code, 0, 0, false),
            sizeof(int) * 2 => new OwnedBadge(
                badge_id,
                code,
                p.ReadInt(),
                p.ReadInt()),
            _ => throw new InvalidDataException(
                $"{nameof(OwnedBadge)} contains an unsupported {p.Available}-byte suffix.")
        };
        AchievementBadgeWire.RequireEmpty(in p, nameof(OwnedBadge));
        return value;
    }

    private static OwnedBadge ParseUnity(in PacketReader p)
    {
        AchievementBadgeWire.RequireRemaining(
            in p,
            AchievementBadgeWire.BadgeMinimumBytes,
            0,
            nameof(OwnedBadge));
        var strings = AchievementBadgeWire.NewStringBudget();
        var value = new OwnedBadge(
            p.ReadInt(),
            strings.Read(in p, nameof(Code), 0),
            0,
            0,
            false);
        AchievementBadgeWire.RequireEmpty(in p, nameof(OwnedBadge));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(OwnedBadge value, in PacketWriter p)
    {
        OwnedBadgeWireValue prepared = Prepare(value, true, in p);
        Write(prepared, in p);
    }

    private static void ComposeUnity(OwnedBadge value, in PacketWriter p)
    {
        OwnedBadgeWireValue prepared = Prepare(value, false, in p);
        Write(prepared, in p);
    }

    internal static OwnedBadgeWireValue Prepare(
        OwnedBadge value,
        bool allows_rarity_data,
        in PacketWriter p,
        ref AchievementBadgeStringBudget strings)
    {
        int badge_id = AchievementBadgeWire.RequireBadgeId(value.NativeBadgeId);
        if (!allows_rarity_data && value.HasRarityData)
            throw new InvalidDataException("Unity owned badges cannot contain rarity data.");
        strings.Require(value.Code, nameof(Code), in p);
        return new OwnedBadgeWireValue(
            badge_id,
            value.Code,
            value.OwnerCount,
            value.RarityId,
            value.HasRarityData);
    }

    internal static void Write(OwnedBadgeWireValue value, in PacketWriter p)
    {
        p.WriteInt(value.BadgeId);
        p.WriteString(value.Code);
        if (value.HasRarityData)
        {
            p.WriteInt(value.OwnerCount);
            p.WriteInt(value.RarityId);
        }
    }

    private static OwnedBadgeWireValue Prepare(
        OwnedBadge value,
        bool allows_rarity_data,
        in PacketWriter p)
    {
        var strings = AchievementBadgeWire.NewStringBudget();
        return Prepare(value, allows_rarity_data, in p, ref strings);
    }

    public override string ToString() =>
        $"{nameof(OwnedBadge)} {{ {nameof(NativeBadgeId)} = {NativeBadgeId}, {nameof(Code)} = {Code}, " +
        $"{nameof(OwnerCount)} = {OwnerCount}, {nameof(RarityId)} = {RarityId}, " +
        $"{nameof(HasRarityData)} = {HasRarityData} }}";
}

internal readonly record struct OwnedBadgeWireValue(
    int BadgeId,
    string Code,
    int OwnerCount,
    int RarityId,
    bool HasRarityData);

public sealed record BadgeInventory : IParserComposer<BadgeInventory>
{
    private IReadOnlyList<OwnedBadge> _badges =
        Array.AsReadOnly(Array.Empty<OwnedBadge>());

    public BadgeInventory(
        int TotalPages,
        int CurrentPage,
        IReadOnlyList<OwnedBadge> Badges)
    {
        this.TotalPages = TotalPages;
        this.CurrentPage = CurrentPage;
        this.Badges = Badges;
    }

    public int TotalPages { get; init; }
    public int CurrentPage { get; init; }

    public IReadOnlyList<OwnedBadge> Badges
    {
        get => _badges;
        init => _badges = AchievementBadgeWire.FreezeValues(value, nameof(Badges));
    }

    public void Deconstruct(
        out int TotalPages,
        out int CurrentPage,
        out IReadOnlyList<OwnedBadge> Badges)
    {
        TotalPages = this.TotalPages;
        CurrentPage = this.CurrentPage;
        Badges = this.Badges;
    }

    public static BadgeInventory Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BadgeInventory ParseFlash(in PacketReader p)
    {
        ReadHeader(in p, out int total_pages, out int current_page, out int count);
        if (count == 0)
        {
            AchievementBadgeWire.RequireEmpty(in p, nameof(BadgeInventory));
            return new BadgeInventory(total_pages, current_page, Array.Empty<OwnedBadge>());
        }

        int start = p.Pos;
        OwnedBadge[]? compact = TryParseEntries(in p, count, false, out bool compact_valid);
        p.Pos = start;
        OwnedBadge[]? expanded = TryParseEntries(in p, count, true, out bool expanded_valid);
        p.Pos = start;
        if (compact_valid == expanded_valid)
        {
            throw new InvalidDataException(compact_valid
                ? "Badge inventory entry layout is ambiguous."
                : "Badge inventory entry layout is unsupported.");
        }
        OwnedBadge[] badges = ParseEntries(in p, count, expanded_valid);
        AchievementBadgeWire.RequireEmpty(in p, nameof(BadgeInventory));
        return new BadgeInventory(total_pages, current_page, badges);
    }

    private static BadgeInventory ParseUnity(in PacketReader p)
    {
        ReadHeader(in p, out int total_pages, out int current_page, out int count);
        OwnedBadge[] badges = ParseEntries(in p, count, false);
        AchievementBadgeWire.RequireEmpty(in p, nameof(BadgeInventory));
        return new BadgeInventory(total_pages, current_page, badges);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BadgeInventory value, in PacketWriter p) =>
        ComposeMessage(value, true, in p);

    private static void ComposeUnity(BadgeInventory value, in PacketWriter p) =>
        ComposeMessage(value, false, in p);

    private static void ReadHeader(
        in PacketReader p,
        out int total_pages,
        out int current_page,
        out int count)
    {
        AchievementBadgeWire.RequireRemaining(
            in p,
            checked(sizeof(int) * 2 + AchievementBadgeWire.CountWidth(p.Client)),
            0,
            nameof(BadgeInventory));
        total_pages = p.ReadInt();
        current_page = p.ReadInt();
        count = AchievementBadgeWire.ReadCount(
            in p,
            AchievementBadgeWire.BadgeMinimumBytes,
            0,
            nameof(Badges));
    }

    private static OwnedBadge[] ParseEntries(
        in PacketReader p,
        int count,
        bool has_rarity_data)
    {
        int minimum_bytes = checked(
            AchievementBadgeWire.BadgeMinimumBytes +
            (has_rarity_data ? sizeof(int) * 2 : 0));
        AchievementBadgeWire.RequireRemaining(
            in p,
            checked(count * minimum_bytes),
            0,
            nameof(Badges));
        var strings = AchievementBadgeWire.NewStringBudget();
        var badges = new OwnedBadge[count];
        for (int index = 0; index < badges.Length; index++)
        {
            int sibling_bytes = checked((badges.Length - index - 1) * minimum_bytes);
            AchievementBadgeWire.RequireRemaining(
                in p,
                minimum_bytes,
                sibling_bytes,
                nameof(OwnedBadge));
            int badge_id = p.ReadInt();
            string code = strings.Read(
                in p,
                nameof(OwnedBadge.Code),
                checked(sibling_bytes + (has_rarity_data ? sizeof(int) * 2 : 0)));
            badges[index] = has_rarity_data
                ? new OwnedBadge(badge_id, code, p.ReadInt(), p.ReadInt())
                : new OwnedBadge(badge_id, code, 0, 0, false);
        }
        return badges;
    }

    private static OwnedBadge[]? TryParseEntries(
        in PacketReader p,
        int count,
        bool has_rarity_data,
        out bool valid)
    {
        try
        {
            OwnedBadge[] badges = ParseEntries(in p, count, has_rarity_data);
            AchievementBadgeWire.RequireEmpty(in p, nameof(BadgeInventory));
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
        BadgeInventory value,
        bool allows_rarity_data,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int count = AchievementBadgeWire.RequireListCount(value.Badges, nameof(value.Badges));
        var strings = AchievementBadgeWire.NewStringBudget();
        var badges = new OwnedBadgeWireValue[count];
        bool? has_rarity_data = null;
        for (int index = 0; index < badges.Length; index++)
        {
            OwnedBadge badge = value.Badges[index];
            if (has_rarity_data is bool expected && expected != badge.HasRarityData)
                throw new InvalidOperationException(
                    "Badge inventory entries cannot mix wire layouts.");
            has_rarity_data = badge.HasRarityData;
            badges[index] = OwnedBadge.Prepare(
                badge,
                allows_rarity_data,
                in p,
                ref strings);
        }

        p.WriteInt(value.TotalPages);
        p.WriteInt(value.CurrentPage);
        AchievementBadgeWire.WriteCount(badges.Length, in p);
        foreach (OwnedBadgeWireValue badge in badges)
            OwnedBadge.Write(badge, in p);
    }
}
