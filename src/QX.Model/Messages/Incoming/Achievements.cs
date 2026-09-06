using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// What the hotel says an achievement is for, which decides where the client files it.
/// </summary>
public static class AchievementState
{
    /// <summary>Ordinary, listed under its own category.</summary>
    public const short Normal = 0;

    /// <summary>Retired: the client moves these into a single "archive" category.</summary>
    public const short Archived = 2;

    /// <summary>
    /// Hidden. The client drops these from the list entirely, unless they belong to
    /// <c>wired_games</c>, which is shown from the room's own wired state instead.
    /// </summary>
    public const short Hidden = 4;
}

/// <summary>How the client draws an achievement's progress.</summary>
public static class AchievementDisplay
{
    /// <summary>Draw the progress bar while the achievement is not at its final level.</summary>
    public const int Progress = 0;

    /// <summary>Draw no progress bar: the achievement is one-shot rather than counted.</summary>
    public const int NoProgress = 1;
}

public sealed class Achievement : IParserComposer<Achievement>
{
    /// <summary>The prefix every achievement badge code carries.</summary>
    public const string BadgePrefix = "ACH_";

    public int Id { get; set; }
    public int Level { get; set; }
    public string BadgeCode { get; set; } = "";
    public int BaseProgress { get; set; }
    public int MaxProgress { get; set; }
    public int LevelRewardPoints { get; set; }
    public int LevelRewardPointType { get; set; }
    public int CurrentProgress { get; set; }
    public bool IsComplete { get; set; }
    public string Category { get; set; } = "";
    public string Subcategory { get; set; } = "";
    public int MaxLevel { get; set; }
    public int DisplayMethod { get; set; }
    public short State { get; set; }

    /// <summary>
    /// The achievement's stable code, with the badge prefix and the level suffix taken off.
    /// </summary>
    /// <remarks>
    /// Derived exactly as the client's own <c>AchievementData</c> constructor does it: drop a
    /// leading <c>ACH_</c>, then drop trailing digits for as long as there are any. So
    /// <c>ACH_RoomEntry5</c> is <c>RoomEntry</c>. This is the key the hotel uses for badge point
    /// limits and for the hotel's own list of new achievements.
    /// </remarks>
    public string Code => CodeOf(BadgeCode);

    /// <summary>
    /// Whether this is the last level, so there is nothing further to reach.
    /// </summary>
    /// <remarks>
    /// The same flag as <see cref="IsComplete"/>, under the name the client gives it. It does not
    /// mean the level is finished — it means no further level exists.
    /// </remarks>
    public bool IsFinalLevel => IsComplete;

    /// <summary>
    /// Whether at least one level has ever been reached, so a badge is owned.
    /// </summary>
    /// <remarks>The client's <c>firstLevelAchieved</c>: past level one, or already at the last.</remarks>
    public bool HasBadge => Level > 1 || IsComplete;

    /// <summary>
    /// How many points this level needs, counted from where the level started.
    /// </summary>
    /// <remarks>
    /// The client clamps the raw limit to at least one as it parses, then subtracts the level's
    /// starting score. The clamp lives here rather than in the parser so composing gives the bytes
    /// back unchanged.
    /// </remarks>
    public int ScoreLimit => Math.Max(1, MaxProgress) - BaseProgress;

    /// <summary>How many points are in, counted from where the level started.</summary>
    public int CurrentPoints => CurrentProgress - BaseProgress;

    /// <summary>The points still to earn before the level is done, never below zero.</summary>
    public int PointsToNextLevel => Math.Max(0, ScoreLimit - CurrentPoints);

    /// <summary>
    /// How far through the level this is, from 0 to 1.
    /// </summary>
    /// <remarks>
    /// Reports 1 at the final level, where there is nothing left to fill, and clamps otherwise, so
    /// a hotel that reports more points than the level asks for does not read as over full.
    /// </remarks>
    public double Progress
    {
        get
        {
            if (IsComplete && CurrentPoints >= ScoreLimit)
                return 1;
            if (ScoreLimit <= 0)
                return 0;
            return Math.Clamp((double)CurrentPoints / ScoreLimit, 0, 1);
        }
    }

    /// <summary>Whether the client draws a progress bar for this one.</summary>
    public bool ShowsProgress => DisplayMethod != AchievementDisplay.NoProgress && !IsComplete;

    /// <summary>
    /// How many levels are done: every level below the current one, and the current one too once
    /// there is nothing above it.
    /// </summary>
    /// <remarks>The client's per-category progress is the sum of this over its achievements.</remarks>
    public int LevelsAchieved => IsComplete ? Level : Level - 1;

    /// <summary>How many levels this achievement has in total.</summary>
    public int LevelCount => MaxLevel;

    /// <summary>Whether the client would leave this out of its list.</summary>
    /// <remarks>
    /// Hidden achievements outside <c>wired_games</c> are dropped, and so is anything the hotel
    /// sent with no category at all.
    /// </remarks>
    public bool IsListed =>
        Category.Length > 0 &&
        (State != AchievementState.Hidden || Category == "wired_games");

    /// <summary>Whether the client files this under its archive category rather than its own.</summary>
    public bool IsArchived => State == AchievementState.Archived;

    /// <summary>
    /// The badge code for a given level of this achievement.
    /// </summary>
    /// <remarks>
    /// Built the way the hotel builds it for badge point limits: the prefix, the code, then the
    /// level. Returns <see langword="null"/> when this achievement's own badge code does not come
    /// back out of that rule, because then the pattern does not hold for it and a guessed code
    /// would be worse than none.
    /// </remarks>
    /// <param name="level">The level to name, from one upwards.</param>
    public string? BadgeCodeForLevel(int level)
    {
        string code = Code;
        if (level < 1 || code.Length == 0)
            return null;
        return (BadgePrefix + code + Level) == BadgeCode
            ? BadgePrefix + code + level
            : null;
    }

    /// <summary>
    /// The badge the next level would grant, or <see langword="null"/> at the last level.
    /// </summary>
    public string? NextBadgeCode => IsComplete ? null : BadgeCodeForLevel(Level + 1);

    /// <summary>
    /// Takes the achievement code out of a badge code.
    /// </summary>
    /// <param name="badgeCode">A badge code, with or without the achievement prefix.</param>
    public static string CodeOf(string badgeCode)
    {
        ArgumentNullException.ThrowIfNull(badgeCode);
        ReadOnlySpan<char> code = badgeCode;
        if (code.StartsWith(BadgePrefix, StringComparison.Ordinal))
            code = code[BadgePrefix.Length..];
        while (code.Length > 0 && char.IsAsciiDigit(code[^1]))
            code = code[..^1];
        return new string(code);
    }

    public Achievement() { }

    public static Achievement Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static Achievement ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static Achievement ParseUnity(in PacketReader p) => ParseRoot(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(Achievement value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(Achievement value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    internal static Achievement ParseWire(
        in PacketReader p,
        int trailing_bytes,
        ref AchievementBadgeStringBudget strings)
    {
        AchievementBadgeWire.RequireRemaining(
            in p,
            AchievementBadgeWire.AchievementMinimumBytes,
            trailing_bytes,
            nameof(Achievement));
        int id = p.ReadInt();
        int level = p.ReadInt();
        string badge_code = strings.Read(
            in p,
            nameof(BadgeCode),
            checked(trailing_bytes + 35));
        int base_progress = p.ReadInt();
        int max_progress = p.ReadInt();
        int level_reward_points = p.ReadInt();
        int level_reward_point_type = p.ReadInt();
        int current_progress = p.ReadInt();
        bool is_complete = p.ReadBool();
        string category = strings.Read(
            in p,
            nameof(Category),
            checked(trailing_bytes + 12));
        string subcategory = strings.Read(
            in p,
            nameof(Subcategory),
            checked(trailing_bytes + 10));
        int max_level = p.ReadInt();
        int display_method = p.ReadInt();
        short state = p.ReadShort();
        return new Achievement
        {
            Id = id,
            Level = level,
            BadgeCode = badge_code,
            BaseProgress = base_progress,
            MaxProgress = max_progress,
            LevelRewardPoints = level_reward_points,
            LevelRewardPointType = level_reward_point_type,
            CurrentProgress = current_progress,
            IsComplete = is_complete,
            Category = category,
            Subcategory = subcategory,
            MaxLevel = max_level,
            DisplayMethod = display_method,
            State = state
        };
    }

    internal static AchievementWireSnapshot PrepareWire(
        Achievement value,
        ref AchievementBadgeStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var snapshot = new AchievementWireSnapshot(
            value.Id,
            value.Level,
            value.BadgeCode,
            value.BaseProgress,
            value.MaxProgress,
            value.LevelRewardPoints,
            value.LevelRewardPointType,
            value.CurrentProgress,
            value.IsComplete,
            value.Category,
            value.Subcategory,
            value.MaxLevel,
            value.DisplayMethod,
            value.State);
        strings.Require(snapshot.BadgeCode, nameof(BadgeCode), in p);
        strings.Require(snapshot.Category, nameof(Category), in p);
        strings.Require(snapshot.Subcategory, nameof(Subcategory), in p);
        return snapshot;
    }

    internal static void WriteWire(AchievementWireSnapshot value, in PacketWriter p)
    {
        p.WriteInt(value.Id);
        p.WriteInt(value.Level);
        p.WriteString(value.BadgeCode);
        p.WriteInt(value.BaseProgress);
        p.WriteInt(value.MaxProgress);
        p.WriteInt(value.LevelRewardPoints);
        p.WriteInt(value.LevelRewardPointType);
        p.WriteInt(value.CurrentProgress);
        p.WriteBool(value.IsComplete);
        p.WriteString(value.Category);
        p.WriteString(value.Subcategory);
        p.WriteInt(value.MaxLevel);
        p.WriteInt(value.DisplayMethod);
        p.WriteShort(value.State);
    }

    private static Achievement ParseRoot(in PacketReader p)
    {
        var strings = AchievementBadgeWire.NewStringBudget();
        Achievement value = ParseWire(in p, 0, ref strings);
        AchievementBadgeWire.RequireEmpty(in p, nameof(Achievement));
        return value;
    }

    private static void ComposeRoot(Achievement value, in PacketWriter p)
    {
        var strings = AchievementBadgeWire.NewStringBudget();
        AchievementWireSnapshot snapshot = PrepareWire(value, ref strings, in p);
        WriteWire(snapshot, in p);
    }
}

internal readonly record struct AchievementWireSnapshot(
    int Id,
    int Level,
    string BadgeCode,
    int BaseProgress,
    int MaxProgress,
    int LevelRewardPoints,
    int LevelRewardPointType,
    int CurrentProgress,
    bool IsComplete,
    string Category,
    string Subcategory,
    int MaxLevel,
    int DisplayMethod,
    short State);

public sealed record AchievementUpdate(Achievement Achievement) : IParserComposer<AchievementUpdate>
{
    public static AchievementUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AchievementUpdate ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static AchievementUpdate ParseUnity(in PacketReader p) => ParseMessage(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AchievementUpdate value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeUnity(AchievementUpdate value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static AchievementUpdate ParseMessage(in PacketReader p)
    {
        var strings = AchievementBadgeWire.NewStringBudget();
        var value = new AchievementUpdate(Achievement.ParseWire(in p, 0, ref strings));
        AchievementBadgeWire.RequireEmpty(in p, nameof(AchievementUpdate));
        return value;
    }

    private static void ComposeMessage(AchievementUpdate value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = AchievementBadgeWire.NewStringBudget();
        AchievementWireSnapshot snapshot = Achievement.PrepareWire(
            value.Achievement,
            ref strings,
            in p);
        Achievement.WriteWire(snapshot, in p);
    }
}

/// <summary>
/// Announces that an achievement reached a new level, granting a badge and - from the second level
/// onwards - retiring the badge of the level it replaced.
/// </summary>
/// <remarks>
/// Flash reads all fourteen fields. Unity stops after the dialog flag: its parser performs twelve
/// reads and never touches the owner count or the rarity, so both are left at zero there and are
/// not written back.
/// </remarks>
/// <param name="Type">Achievement type identifier.</param>
/// <param name="Level">Level that was just reached.</param>
/// <param name="BadgeId">Numeric identifier of the granted badge.</param>
/// <param name="BadgeCode">Badge code that is now owned.</param>
/// <param name="Points">Achievement score awarded in total.</param>
/// <param name="LevelRewardPoints">Currency amount awarded for this level.</param>
/// <param name="LevelRewardPointType">Activity point type the level reward was paid in.</param>
/// <param name="BonusPoints">Bonus achievement score awarded on top of <paramref name="Points"/>.</param>
/// <param name="AchievementId">Identifier of the achievement within its category.</param>
/// <param name="RemovedBadgeCode">
/// Badge code the granted badge replaces. Empty when the achievement had no previous level.
/// </param>
/// <param name="Category">Achievement category name.</param>
/// <param name="ShowDialogToUser">Whether the client is expected to present the level-up dialog.</param>
/// <param name="OwnerCount">Number of accounts owning the granted badge.</param>
/// <param name="BadgeRarityId">Rarity bucket of the granted badge.</param>
public sealed record AchievementNotification(
    int Type,
    int Level,
    int BadgeId,
    string BadgeCode,
    int Points,
    int LevelRewardPoints,
    int LevelRewardPointType,
    int BonusPoints,
    int AchievementId,
    string RemovedBadgeCode,
    string Category,
    bool ShowDialogToUser,
    int OwnerCount,
    int BadgeRarityId) : IParserComposer<AchievementNotification>
{
    public static AchievementNotification Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AchievementNotification ParseFlash(in PacketReader p) =>
        ParseMessage(in p, true);

    private static AchievementNotification ParseUnity(in PacketReader p) =>
        ParseMessage(in p, false);

    private static AchievementNotification ParseMessage(in PacketReader p, bool has_rarity_data)
    {
        int rarity_bytes = has_rarity_data ? sizeof(int) * 2 : 0;
        AchievementBadgeWire.RequireRemaining(
            in p,
            checked(39 + rarity_bytes),
            0,
            nameof(AchievementNotification));
        var strings = AchievementBadgeWire.NewStringBudget();
        int type = p.ReadInt();
        int level = p.ReadInt();
        int badge_id = p.ReadInt();
        string badge_code = strings.Read(
            in p,
            nameof(BadgeCode),
            checked(25 + rarity_bytes));
        int points = p.ReadInt();
        int level_reward_points = p.ReadInt();
        int level_reward_point_type = p.ReadInt();
        int bonus_points = p.ReadInt();
        int achievement_id = p.ReadInt();
        string removed_badge_code = strings.Read(
            in p,
            nameof(RemovedBadgeCode),
            checked(3 + rarity_bytes));
        string category = strings.Read(
            in p,
            nameof(Category),
            checked(sizeof(byte) + rarity_bytes));
        bool show_dialog_to_user = p.ReadBool();
        int owner_count = has_rarity_data ? p.ReadInt() : 0;
        int badge_rarity_id = has_rarity_data ? p.ReadInt() : 0;

        var value = new AchievementNotification(
            type,
            level,
            badge_id,
            badge_code,
            points,
            level_reward_points,
            level_reward_point_type,
            bonus_points,
            achievement_id,
            removed_badge_code,
            category,
            show_dialog_to_user,
            owner_count,
            badge_rarity_id);
        AchievementBadgeWire.RequireEmpty(in p, nameof(AchievementNotification));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AchievementNotification value, in PacketWriter p) =>
        ComposeMessage(value, true, in p);

    private static void ComposeUnity(AchievementNotification value, in PacketWriter p) =>
        ComposeMessage(value, false, in p);

    private static void ComposeMessage(
        AchievementNotification value,
        bool has_rarity_data,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!has_rarity_data && (value.OwnerCount != 0 || value.BadgeRarityId != 0))
        {
            throw new InvalidDataException(
                "Unity achievement notifications cannot contain rarity data.");
        }
        var strings = AchievementBadgeWire.NewStringBudget();
        strings.Require(value.BadgeCode, nameof(BadgeCode), in p);
        strings.Require(value.RemovedBadgeCode, nameof(RemovedBadgeCode), in p);
        strings.Require(value.Category, nameof(Category), in p);

        p.WriteInt(value.Type);
        p.WriteInt(value.Level);
        p.WriteInt(value.BadgeId);
        p.WriteString(value.BadgeCode);
        p.WriteInt(value.Points);
        p.WriteInt(value.LevelRewardPoints);
        p.WriteInt(value.LevelRewardPointType);
        p.WriteInt(value.BonusPoints);
        p.WriteInt(value.AchievementId);
        p.WriteString(value.RemovedBadgeCode);
        p.WriteString(value.Category);
        p.WriteBool(value.ShowDialogToUser);

        if (!has_rarity_data)
            return;

        p.WriteInt(value.OwnerCount);
        p.WriteInt(value.BadgeRarityId);
    }
}

/// <summary>The account's total achievement score.</summary>
/// <remarks>
/// Flash layout. The Unity build declares <c>AchievementScore</c> but the extract carries no
/// schema for it, so nothing parses this on Unity sessions.
/// </remarks>
/// <param name="Score">The score.</param>
public sealed record AchievementScore(int Score) : IParserComposer<AchievementScore>
{
    public static AchievementScore Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static AchievementScore ParseFlash(in PacketReader p)
    {
        AchievementBadgeWire.RequireRemaining(
            in p,
            sizeof(int),
            0,
            nameof(AchievementScore));
        var value = new AchievementScore(p.ReadInt());
        AchievementBadgeWire.RequireEmpty(in p, nameof(AchievementScore));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(AchievementScore value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteInt(value.Score);
    }
}

/// <summary>
/// How many points one level of one achievement asks for.
/// </summary>
/// <param name="AchievementCode">The achievement's code, without prefix or level.</param>
/// <param name="Level">The level.</param>
/// <param name="Limit">The point total that level needs.</param>
public sealed record BadgePointLimit(string AchievementCode, int Level, int Limit)
{
    /// <summary>
    /// The badge code this limit belongs to.
    /// </summary>
    /// <remarks>
    /// Composed the way the hotel's own parser composes it, prefix then code then level, which is
    /// what proves the same rule the other way round in <see cref="Achievement.Code"/>.
    /// </remarks>
    public string BadgeCode => Achievement.BadgePrefix + AchievementCode + Level;
}

/// <summary>
/// Every badge's point limit, grouped by achievement.
/// </summary>
/// <remarks>
/// Flash and Unity both read a nested pair of loops: an outer one per achievement code and an inner
/// one per level. Their counter widths differ.
/// </remarks>
public sealed record BadgePointLimits : IParserComposer<BadgePointLimits>
{
    private IReadOnlyList<BadgePointLimit> _limits =
        Array.AsReadOnly(Array.Empty<BadgePointLimit>());

    public BadgePointLimits(IReadOnlyList<BadgePointLimit> Limits)
    {
        this.Limits = Limits;
    }

    public IReadOnlyList<BadgePointLimit> Limits
    {
        get => _limits;
        init => _limits = AchievementBadgeWire.FreezeReferences(value, nameof(Limits));
    }

    public void Deconstruct(out IReadOnlyList<BadgePointLimit> Limits)
    {
        Limits = this.Limits;
    }

    /// <summary>The point limit for one badge, or <see langword="null"/> when none was sent.</summary>
    /// <param name="achievementCode">The achievement's code, without prefix or level.</param>
    /// <param name="level">The level.</param>
    public int? Limit(string achievementCode, int level)
    {
        ArgumentNullException.ThrowIfNull(achievementCode);
        foreach (BadgePointLimit limit in Limits)
        {
            if (limit.Level == level &&
                string.Equals(limit.AchievementCode, achievementCode, StringComparison.Ordinal))
                return limit.Limit;
        }
        return null;
    }

    /// <remarks>
    /// One group per achievement, each with its code and then a level and a limit per line. Flash
    /// counts both in four bytes; Unity sends each as an array, and its generic array reader takes
    /// two unless the message was built for another width, which this one was not.
    /// </remarks>
    public static BadgePointLimits Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BadgePointLimits ParseFlash(in PacketReader p) => ParseLimits(in p);

    private static BadgePointLimits ParseUnity(in PacketReader p) => ParseLimits(in p);

    private static BadgePointLimits ParseLimits(in PacketReader p)
    {
        int count_width = AchievementBadgeWire.CountWidth(p.Client);
        int group_minimum_bytes = checked(
            AchievementBadgeWire.StringPrefixBytes + count_width);
        int groups = AchievementBadgeWire.ReadCount(
            in p,
            group_minimum_bytes,
            0,
            nameof(Limits));
        var strings = AchievementBadgeWire.NewStringBudget();
        var limits = new List<BadgePointLimit>();
        for (int group = 0; group < groups; group++)
        {
            int sibling_bytes = checked((groups - group - 1) * group_minimum_bytes);
            string code = strings.Read(
                in p,
                nameof(BadgePointLimit.AchievementCode),
                checked(sibling_bytes + count_width));
            int levels = AchievementBadgeWire.ReadCount(
                in p,
                AchievementBadgeWire.PointLimitMinimumBytes,
                sibling_bytes,
                nameof(Limits));
            if (levels > AchievementBadgeWire.MaximumCollectionCount - limits.Count)
            {
                throw new InvalidDataException(
                    $"{nameof(Limits)} exceed the limit " +
                    $"{AchievementBadgeWire.MaximumCollectionCount}.");
            }
            for (int level = 0; level < levels; level++)
                limits.Add(new BadgePointLimit(code, p.ReadInt(), p.ReadInt()));
        }
        AchievementBadgeWire.RequireEmpty(in p, nameof(BadgePointLimits));
        return new BadgePointLimits(limits);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BadgePointLimits value, in PacketWriter p) =>
        ComposeLimits(value, in p);

    private static void ComposeUnity(BadgePointLimits value, in PacketWriter p) =>
        ComposeLimits(value, in p);

    private static void ComposeLimits(BadgePointLimits value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int limit_count = AchievementBadgeWire.RequireListCount(
            value.Limits,
            nameof(value.Limits));
        var strings = AchievementBadgeWire.NewStringBudget();
        var groups = new List<BadgePointLimitGroup>();
        for (int index = 0; index < limit_count; index++)
        {
            BadgePointLimit limit = value.Limits[index];
            ArgumentNullException.ThrowIfNull(limit, nameof(value.Limits));
            var snapshot = new BadgePointLimitWireValue(
                limit.AchievementCode,
                limit.Level,
                limit.Limit);
            if (groups.Count > 0 && string.Equals(
                    groups[^1].Code,
                    snapshot.AchievementCode,
                    StringComparison.Ordinal))
            {
                groups[^1].Limits.Add(snapshot);
            }
            else
            {
                strings.Require(
                    snapshot.AchievementCode,
                    nameof(BadgePointLimit.AchievementCode),
                    in p);
                groups.Add(new BadgePointLimitGroup(snapshot.AchievementCode, [snapshot]));
            }
        }
        AchievementBadgeWire.RequireCount(groups.Count, nameof(value.Limits));
        foreach (BadgePointLimitGroup group in groups)
            AchievementBadgeWire.RequireCount(group.Limits.Count, nameof(value.Limits));

        AchievementBadgeWire.WriteCount(groups.Count, in p);

        foreach (BadgePointLimitGroup group in groups)
        {
            p.WriteString(group.Code);
            AchievementBadgeWire.WriteCount(group.Limits.Count, in p);
            foreach (BadgePointLimitWireValue limit in group.Limits)
            {
                p.WriteInt(limit.Level);
                p.WriteInt(limit.Limit);
            }
        }
    }
}

internal sealed record BadgePointLimitGroup(
    string Code,
    List<BadgePointLimitWireValue> Limits);

internal readonly record struct BadgePointLimitWireValue(
    string AchievementCode,
    int Level,
    int Limit);

public sealed record Achievements : IParserComposer<Achievements>
{
    private IReadOnlyList<Achievement> _items =
        Array.AsReadOnly(Array.Empty<Achievement>());

    public Achievements(IReadOnlyList<Achievement> Items, string DefaultCategory)
    {
        this.Items = Items;
        this.DefaultCategory = DefaultCategory;
    }

    public IReadOnlyList<Achievement> Items
    {
        get => _items;
        init => _items = AchievementBadgeWire.FreezeReferences(value, nameof(Items));
    }

    public string DefaultCategory { get; init; }

    public void Deconstruct(
        out IReadOnlyList<Achievement> Items,
        out string DefaultCategory)
    {
        Items = this.Items;
        DefaultCategory = this.DefaultCategory;
    }

    public static Achievements Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static Achievements ParseFlash(in PacketReader p) => ParseList(in p);

    private static Achievements ParseUnity(in PacketReader p) => ParseList(in p);

    private static Achievements ParseList(in PacketReader p)
    {
        int count = AchievementBadgeWire.ReadCount(
            in p,
            AchievementBadgeWire.AchievementMinimumBytes,
            AchievementBadgeWire.StringPrefixBytes,
            nameof(Items));
        var strings = AchievementBadgeWire.NewStringBudget();
        var items = new Achievement[count];
        for (int index = 0; index < items.Length; index++)
        {
            int sibling_bytes = checked(
                (items.Length - index - 1) *
                AchievementBadgeWire.AchievementMinimumBytes);
            items[index] = Achievement.ParseWire(
                in p,
                checked(sibling_bytes + AchievementBadgeWire.StringPrefixBytes),
                ref strings);
        }
        string default_category = strings.Read(in p, nameof(DefaultCategory), 0);
        AchievementBadgeWire.RequireEmpty(in p, nameof(Achievements));
        return new Achievements(items, default_category);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(Achievements value, in PacketWriter p) =>
        ComposeList(value, in p);

    private static void ComposeUnity(Achievements value, in PacketWriter p) =>
        ComposeList(value, in p);

    private static void ComposeList(Achievements value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int count = AchievementBadgeWire.RequireListCount(value.Items, nameof(value.Items));
        var strings = AchievementBadgeWire.NewStringBudget();
        var items = new AchievementWireSnapshot[count];
        for (int index = 0; index < items.Length; index++)
        {
            items[index] = Achievement.PrepareWire(
                value.Items[index],
                ref strings,
                in p);
        }
        string default_category = value.DefaultCategory;
        strings.Require(default_category, nameof(value.DefaultCategory), in p);

        AchievementBadgeWire.WriteCount(items.Length, in p);
        foreach (AchievementWireSnapshot item in items)
            Achievement.WriteWire(item, in p);
        p.WriteString(default_category);
    }
}
