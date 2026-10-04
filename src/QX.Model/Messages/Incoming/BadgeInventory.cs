using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents a badge in the user's badge inventory.</summary>
public readonly record struct OwnedBadge : IParserComposer<OwnedBadge>
{
    /// <summary>Gets the identifier of the badge.</summary>
    public Id NativeBadgeId { get; init; }

    /// <summary>Gets the identifier of the badge as a 32 bit integer.</summary>
    /// <exception cref="OverflowException">
    /// Thrown when <see cref="NativeBadgeId"/> does not fit in an <see cref="int"/>.
    /// </exception>
    public int BadgeId
    {
        get => checked((int)(long)NativeBadgeId);
        init => NativeBadgeId = value;
    }

    /// <summary>Gets the badge code.</summary>
    public string Code { get; init; }
    /// <summary>Gets the number of users who own the badge, or 0 when there is no rarity data.</summary>
    public int OwnerCount { get; init; }
    /// <summary>Gets the rarity of the badge, or 0 when there is no rarity data.</summary>
    public int RarityId { get; init; }
    /// <summary>Gets whether the hotel sent the owner count and rarity for the badge.</summary>
    public bool HasRarityData { get; init; }

    /// <summary>Initializes a new instance of the <see cref="OwnedBadge"/> struct with rarity data.</summary>
    /// <param name="badgeId">The identifier of the badge.</param>
    /// <param name="code">The badge code.</param>
    /// <param name="ownerCount">The number of users who own the badge.</param>
    /// <param name="rarityId">The rarity of the badge.</param>
    public OwnedBadge(int badgeId, string code, int ownerCount, int rarityId)
        : this((Id)badgeId, code, ownerCount, rarityId, true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OwnedBadge"/> struct.</summary>
    /// <param name="badgeId">The identifier of the badge.</param>
    /// <param name="code">The badge code.</param>
    /// <param name="ownerCount">The number of users who own the badge.</param>
    /// <param name="rarityId">The rarity of the badge.</param>
    /// <param name="hasRarityData">Whether the owner count and rarity are present.</param>
    public OwnedBadge(
        int badgeId,
        string code,
        int ownerCount,
        int rarityId,
        bool hasRarityData)
        : this((Id)badgeId, code, ownerCount, rarityId, hasRarityData)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OwnedBadge"/> struct.</summary>
    /// <param name="badgeId">The identifier of the badge.</param>
    /// <param name="code">The badge code.</param>
    /// <param name="ownerCount">The number of users who own the badge.</param>
    /// <param name="rarityId">The rarity of the badge.</param>
    /// <param name="hasRarityData">Whether the owner count and rarity are present.</param>
    public OwnedBadge(
        Id badgeId,
        string code,
        int ownerCount,
        int rarityId,
        bool hasRarityData = true)
    {
        NativeBadgeId = badgeId;
        Code = code;
        OwnerCount = ownerCount;
        RarityId = rarityId;
        HasRarityData = hasRarityData;
    }

    /// <summary>Deconstructs the badge into its parts, with a 32 bit identifier.</summary>
    /// <param name="badgeId">The identifier of the badge.</param>
    /// <param name="code">The badge code.</param>
    /// <param name="ownerCount">The number of users who own the badge.</param>
    /// <param name="rarityId">The rarity of the badge.</param>
    public void Deconstruct(
        out int badgeId,
        out string code,
        out int ownerCount,
        out int rarityId)
    {
        badgeId = BadgeId;
        code = Code;
        ownerCount = OwnerCount;
        rarityId = RarityId;
    }

    /// <summary>Deconstructs the badge into its parts, including whether rarity data is present.</summary>
    /// <param name="badgeId">The identifier of the badge.</param>
    /// <param name="code">The badge code.</param>
    /// <param name="ownerCount">The number of users who own the badge.</param>
    /// <param name="rarityId">The rarity of the badge.</param>
    /// <param name="hasRarityData">Whether the owner count and rarity are present.</param>
    public void Deconstruct(
        out Id badgeId,
        out string code,
        out int ownerCount,
        out int rarityId,
        out bool hasRarityData)
    {
        badgeId = NativeBadgeId;
        code = Code;
        ownerCount = OwnerCount;
        rarityId = RarityId;
        hasRarityData = HasRarityData;
    }

    /// <summary>Parses a badge from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <remarks>The owner count and rarity are read only when exactly 8 bytes follow the badge code.</remarks>
    public static OwnedBadge Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the badge into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(OwnedBadge value, in PacketWriter p)
    {
        OwnedBadgeWireValue prepared = Prepare(value, in p);
        Write(prepared, in p);
    }

    internal static OwnedBadgeWireValue Prepare(
        OwnedBadge value,
        in PacketWriter p,
        ref AchievementBadgeStringBudget strings)
    {
        int badge_id = AchievementBadgeWire.RequireBadgeId(value.NativeBadgeId);
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
        in PacketWriter p)
    {
        var strings = AchievementBadgeWire.NewStringBudget();
        return Prepare(value, in p, ref strings);
    }

    /// <inheritdoc/>
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

/// <summary>Represents the <c>Badges</c> message, received with one page of the user's badge inventory.</summary>
public sealed record BadgeInventory : IParserComposer<BadgeInventory>
{
    private IReadOnlyList<OwnedBadge> _badges =
        Array.AsReadOnly(Array.Empty<OwnedBadge>());

    /// <summary>Initializes a new instance of the <see cref="BadgeInventory"/> record.</summary>
    /// <param name="totalPages">The total number of pages in the badge inventory.</param>
    /// <param name="currentPage">The zero based index of this page.</param>
    /// <param name="badges">The badges on this page, copied into a read only list.</param>
    public BadgeInventory(
        int totalPages,
        int currentPage,
        IReadOnlyList<OwnedBadge> badges)
    {
        TotalPages = totalPages;
        CurrentPage = currentPage;
        Badges = badges;
    }

    /// <summary>Gets the total number of pages in the badge inventory.</summary>
    public int TotalPages { get; init; }
    /// <summary>Gets the zero based index of this page.</summary>
    public int CurrentPage { get; init; }

    /// <summary>Gets the badges on this page, as a read only copy.</summary>
    public IReadOnlyList<OwnedBadge> Badges
    {
        get => _badges;
        init => _badges = AchievementBadgeWire.FreezeValues(value, nameof(Badges));
    }

    /// <summary>Deconstructs the message into its parts.</summary>
    /// <param name="totalPages">The total number of pages in the badge inventory.</param>
    /// <param name="currentPage">The zero based index of this page.</param>
    /// <param name="badges">The badges on this page.</param>
    public void Deconstruct(
        out int totalPages,
        out int currentPage,
        out IReadOnlyList<OwnedBadge> badges)
    {
        totalPages = TotalPages;
        currentPage = CurrentPage;
        badges = Badges;
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <remarks>
    /// The parser detects whether the entries carry rarity data and throws <see cref="InvalidDataException"/> when
    /// neither layout, or both, fit the payload.
    /// </remarks>
    public static BadgeInventory Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when some badges have rarity data and others do not.
    /// </exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BadgeInventory value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ReadHeader(
        in PacketReader p,
        out int total_pages,
        out int current_page,
        out int count)
    {
        AchievementBadgeWire.RequireRemaining(
            in p,
            checked(sizeof(int) * 2 + AchievementBadgeWire.CountWidth),
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
