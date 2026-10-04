namespace Qx.Model.Wired;

/// <summary>Verified WiredFxCategory identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxCategory
{
    /// <summary>Health.</summary>
    Health = 0,
    /// <summary>Progress.</summary>
    Progress = 1,
    /// <summary>Level.</summary>
    Level = 2,
    /// <summary>Status.</summary>
    Status = 3,
    /// <summary>Boss.</summary>
    Boss = 4,
    /// <summary>Number.</summary>
    Number = 5
}

/// <summary>Verified WiredFxColor identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxColor
{
    /// <summary>NOT APPLICABLE.</summary>
    NotApplicable = -1,
    /// <summary>GREEN.</summary>
    Green = 1,
    /// <summary>LIME GREEN.</summary>
    LimeGreen = 2,
    /// <summary>YELLOW.</summary>
    Yellow = 3,
    /// <summary>ORANGE.</summary>
    Orange = 4,
    /// <summary>RED.</summary>
    Red = 5,
    /// <summary>CYAN.</summary>
    Cyan = 6,
    /// <summary>BLUE.</summary>
    Blue = 7,
    /// <summary>PURPLE.</summary>
    Purple = 8,
    /// <summary>PINK.</summary>
    Pink = 9,
    /// <summary>BROWN.</summary>
    Brown = 10,
    /// <summary>BEIGE.</summary>
    Beige = 11,
    /// <summary>TEAL.</summary>
    Teal = 12,
    /// <summary>INDIGO.</summary>
    Indigo = 13,
    /// <summary>MAGENTA.</summary>
    Magenta = 14,
    /// <summary>LIGHT BLUE.</summary>
    LightBlue = 15,
    /// <summary>FIRE ORANGE.</summary>
    FireOrange = 16,
    /// <summary>DARK GREEN.</summary>
    DarkGreen = 17,
    /// <summary>DARK BLUE.</summary>
    DarkBlue = 18,
    /// <summary>WHITE.</summary>
    White = 19,
    /// <summary>BRONZE.</summary>
    Bronze = 100,
    /// <summary>SILVER.</summary>
    Silver = 101,
    /// <summary>GOLD.</summary>
    Gold = 102,
    /// <summary>DIAMOND.</summary>
    Diamond = 103,
    /// <summary>EMERALD.</summary>
    Emerald = 104,
    /// <summary>DYNAMIC RED TO GREEN.</summary>
    DynamicRedToGreen = 1000,
    /// <summary>DYNAMIC LEVELLING.</summary>
    DynamicLevelling = 1001,
    /// <summary>DYNAMIC TEAM COLOR.</summary>
    DynamicTeamColor = 1002
}

/// <summary>Verified WiredFxWidth identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxWidth
{
    /// <summary>not applicable.</summary>
    NotApplicable = -1,
    /// <summary>extra small.</summary>
    ExtraSmall = 0,
    /// <summary>small.</summary>
    Small = 1,
    /// <summary>medium.</summary>
    Medium = 2,
    /// <summary>large.</summary>
    Large = 3,
    /// <summary>extra large.</summary>
    ExtraLarge = 4,
    /// <summary>big mahoosive chonky.</summary>
    BigMahoosiveChonky = 100
}

/// <summary>Verified WiredFxRenderer identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxRenderer
{
    /// <summary>classic progress.</summary>
    ClassicProgress = 0,
    /// <summary>classic mini progress.</summary>
    ClassicMiniProgress = 1,
    /// <summary>block progress.</summary>
    BlockProgress = 2,
    /// <summary>striped progress.</summary>
    StripedProgress = 3,
    /// <summary>arrow progress.</summary>
    ArrowProgress = 4,
    /// <summary>health progress.</summary>
    HealthProgress = 10,
    /// <summary>masked heart fill.</summary>
    MaskedHeartFill = 11,
    /// <summary>stacked health points.</summary>
    StackedHealthPoints = 12,
    /// <summary>thermometer health points.</summary>
    ThermometerHealthPoints = 13,
    /// <summary>level with progress.</summary>
    LevelWithProgress = 20,
    /// <summary>level with bar and numerical progress.</summary>
    LevelWithBarAndNumericalProgress = 21,
    /// <summary>boss health bar.</summary>
    BossHealthBar = 100,
    /// <summary>numerical progress.</summary>
    NumericalProgress = 101,
    /// <summary>number recolorable.</summary>
    NumberRecolorable = 200,
    /// <summary>number baked colors.</summary>
    NumberBakedColors = 201
}

/// <summary>Verified WiredFxHealthStyle identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxHealthStyle
{
    /// <summary>HEALTH BAR.</summary>
    HealthBar = 0,
    /// <summary>SINGLE HEART.</summary>
    SingleHeart = 3,
    /// <summary>STACKED.</summary>
    Stacked = 1,
    /// <summary>THERMOMETER.</summary>
    Thermometer = 2
}

/// <summary>Verified WiredFxProgressStyle identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxProgressStyle
{
    /// <summary>CLASSIC BAR.</summary>
    ClassicBar = 0,
    /// <summary>BLOCK BAR.</summary>
    BlockBar = 1,
    /// <summary>STRIPED BAR.</summary>
    StripedBar = 2,
    /// <summary>ARROW BAR.</summary>
    ArrowBar = 3,
    /// <summary>CLASSIC MINI BAR.</summary>
    ClassicMiniBar = 4
}

/// <summary>Verified WiredFxLevelStyle identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxLevelStyle
{
    /// <summary>LEVEL AND BAR.</summary>
    LevelAndBar = 0,
    /// <summary>LEVEL DETAILS.</summary>
    LevelDetails = 1
}

/// <summary>Verified WiredFxStatusStyle identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxStatusStyle
{
    /// <summary>STATUS ENERGY.</summary>
    StatusEnergy = 0,
    /// <summary>STATUS SHIELD.</summary>
    StatusShield = 1,
    /// <summary>STATUS MAGIC.</summary>
    StatusMagic = 2,
    /// <summary>STATUS FOOD.</summary>
    StatusFood = 3,
    /// <summary>STATUS STAMINA.</summary>
    StatusStamina = 4,
    /// <summary>STATUS POISON.</summary>
    StatusPoison = 5,
    /// <summary>STATUS MANA.</summary>
    StatusMana = 6,
    /// <summary>STATUS HEALTH.</summary>
    StatusHealth = 7,
    /// <summary>STATUS GOLD.</summary>
    StatusGold = 8,
    /// <summary>STATUS GEMS.</summary>
    StatusGems = 9,
    /// <summary>STATUS HONOR.</summary>
    StatusHonor = 10,
    /// <summary>STATUS REPUTATION.</summary>
    StatusReputation = 11,
    /// <summary>STATUS COOLDOWN.</summary>
    StatusCooldown = 12,
    /// <summary>STATUS TIME LEFT.</summary>
    StatusTimeLeft = 13,
    /// <summary>STATUS BURNING.</summary>
    StatusBurning = 14,
    /// <summary>STATUS FREEZING.</summary>
    StatusFreezing = 15,
    /// <summary>STATUS BATTERY.</summary>
    StatusBattery = 16,
    /// <summary>STATUS REPAIRING.</summary>
    StatusRepairing = 17,
    /// <summary>STATUS STEALTH.</summary>
    StatusStealth = 18,
    /// <summary>STATUS UPGRADING.</summary>
    StatusUpgrading = 19,
    /// <summary>STATUS STAR POWER.</summary>
    StatusStarPower = 20,
    /// <summary>STATUS WATER.</summary>
    StatusWater = 21
}

/// <summary>Verified WiredFxBossStyle identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxBossStyle
{
    /// <summary>BOSS HEALTH AND SKULL.</summary>
    BossHealthAndSkull = 0,
    /// <summary>BOSS HEALTH.</summary>
    BossHealth = 1
}

/// <summary>Verified WiredFxNumberStyle identifiers from the Flash variable-FX editor.</summary>
public enum WiredFxNumberStyle
{
    /// <summary>NUMBER FREEZE.</summary>
    NumberFreeze = 0,
    /// <summary>NUMBER SHALIMAR.</summary>
    NumberShalimar = 1,
    /// <summary>NUMBER BLOCKY.</summary>
    NumberBlocky = 2
}

public static partial class WiredFxStyles
{
    /// <summary>Gets all 38 styles in client order, with immutable allowed-option lists.</summary>
    public static IReadOnlyList<WiredFxStyleDefinition> All { get; } = Array.AsReadOnly<WiredFxStyleDefinition>(
    [
        new(WiredFxCategory.Health, 0, "HEALTH_BAR", WiredFxRenderer.HealthProgress, WiredFxColor.DynamicRedToGreen, Array.AsReadOnly<WiredFxColor>([WiredFxColor.DynamicRedToGreen]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.HealthProgress])),
        new(WiredFxCategory.Health, 3, "SINGLE_HEART", WiredFxRenderer.MaskedHeartFill, WiredFxColor.Red, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicTeamColor]), WiredFxWidth.NotApplicable, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.NotApplicable]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.MaskedHeartFill])),
        new(WiredFxCategory.Health, 1, "STACKED", WiredFxRenderer.StackedHealthPoints, WiredFxColor.Red, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicTeamColor]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.Medium, WiredFxWidth.Large]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.StackedHealthPoints])),
        new(WiredFxCategory.Health, 2, "THERMOMETER", WiredFxRenderer.ThermometerHealthPoints, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.ThermometerHealthPoints])),
        new(WiredFxCategory.Progress, 0, "CLASSIC_BAR", WiredFxRenderer.ClassicProgress, WiredFxColor.Green, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicTeamColor]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.ClassicProgress])),
        new(WiredFxCategory.Progress, 1, "BLOCK_BAR", WiredFxRenderer.BlockProgress, WiredFxColor.Green, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicTeamColor]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress])),
        new(WiredFxCategory.Progress, 2, "STRIPED_BAR", WiredFxRenderer.StripedProgress, WiredFxColor.Green, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicTeamColor]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.StripedProgress])),
        new(WiredFxCategory.Progress, 3, "ARROW_BAR", WiredFxRenderer.ArrowProgress, WiredFxColor.Green, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicTeamColor]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Progress, 4, "CLASSIC_MINI_BAR", WiredFxRenderer.ClassicMiniProgress, WiredFxColor.Green, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicTeamColor]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.ClassicMiniProgress])),
        new(WiredFxCategory.Level, 0, "LEVEL_AND_BAR", WiredFxRenderer.LevelWithProgress, WiredFxColor.DynamicLevelling, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicLevelling, WiredFxColor.DynamicTeamColor]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.LevelWithProgress])),
        new(WiredFxCategory.Level, 1, "LEVEL_DETAILS", WiredFxRenderer.LevelWithBarAndNumericalProgress, WiredFxColor.DynamicLevelling, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicLevelling, WiredFxColor.DynamicTeamColor]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.LevelWithBarAndNumericalProgress])),
        new(WiredFxCategory.Status, 0, "STATUS_ENERGY", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 1, "STATUS_SHIELD", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 2, "STATUS_MAGIC", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 3, "STATUS_FOOD", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 4, "STATUS_STAMINA", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 5, "STATUS_POISON", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 6, "STATUS_MANA", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 7, "STATUS_HEALTH", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 8, "STATUS_GOLD", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 9, "STATUS_GEMS", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 10, "STATUS_HONOR", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 11, "STATUS_REPUTATION", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 12, "STATUS_COOLDOWN", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 13, "STATUS_TIME_LEFT", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 14, "STATUS_BURNING", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 15, "STATUS_FREEZING", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 16, "STATUS_BATTERY", WiredFxRenderer.BlockProgress, WiredFxColor.DynamicRedToGreen, Array.AsReadOnly<WiredFxColor>([WiredFxColor.DynamicRedToGreen]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 17, "STATUS_REPAIRING", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 18, "STATUS_STEALTH", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 19, "STATUS_UPGRADING", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 20, "STATUS_STAR_POWER", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Status, 21, "STATUS_WATER", WiredFxRenderer.BlockProgress, WiredFxColor.NotApplicable, Array.AsReadOnly<WiredFxColor>([WiredFxColor.NotApplicable]), WiredFxWidth.Medium, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.ExtraSmall, WiredFxWidth.Small, WiredFxWidth.Medium, WiredFxWidth.Large, WiredFxWidth.ExtraLarge]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BlockProgress, WiredFxRenderer.StripedProgress, WiredFxRenderer.ArrowProgress])),
        new(WiredFxCategory.Boss, 0, "BOSS_HEALTH_AND_SKULL", WiredFxRenderer.BossHealthBar, WiredFxColor.Red, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Red]), WiredFxWidth.ExtraLarge, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.Large, WiredFxWidth.ExtraLarge, WiredFxWidth.BigMahoosiveChonky]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BossHealthBar])),
        new(WiredFxCategory.Boss, 1, "BOSS_HEALTH", WiredFxRenderer.BossHealthBar, WiredFxColor.Red, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicRedToGreen, WiredFxColor.DynamicTeamColor]), WiredFxWidth.ExtraLarge, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.Large, WiredFxWidth.ExtraLarge, WiredFxWidth.BigMahoosiveChonky]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.BossHealthBar])),
        new(WiredFxCategory.Number, 0, "NUMBER_FREEZE", WiredFxRenderer.NumberBakedColors, WiredFxColor.Green, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Red, WiredFxColor.Green, WiredFxColor.Blue, WiredFxColor.Yellow, WiredFxColor.White, WiredFxColor.DynamicTeamColor]), WiredFxWidth.NotApplicable, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.NotApplicable]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.NumberBakedColors])),
        new(WiredFxCategory.Number, 1, "NUMBER_SHALIMAR", WiredFxRenderer.NumberRecolorable, WiredFxColor.Green, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicTeamColor]), WiredFxWidth.NotApplicable, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.NotApplicable]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.NumberRecolorable])),
        new(WiredFxCategory.Number, 2, "NUMBER_BLOCKY", WiredFxRenderer.NumberRecolorable, WiredFxColor.Green, Array.AsReadOnly<WiredFxColor>([WiredFxColor.Green, WiredFxColor.LimeGreen, WiredFxColor.Yellow, WiredFxColor.Orange, WiredFxColor.Red, WiredFxColor.Cyan, WiredFxColor.Blue, WiredFxColor.Purple, WiredFxColor.Pink, WiredFxColor.Brown, WiredFxColor.Beige, WiredFxColor.Teal, WiredFxColor.Indigo, WiredFxColor.Magenta, WiredFxColor.LightBlue, WiredFxColor.FireOrange, WiredFxColor.DarkGreen, WiredFxColor.DarkBlue, WiredFxColor.White, WiredFxColor.Bronze, WiredFxColor.Silver, WiredFxColor.Gold, WiredFxColor.Diamond, WiredFxColor.Emerald, WiredFxColor.DynamicTeamColor]), WiredFxWidth.NotApplicable, Array.AsReadOnly<WiredFxWidth>([WiredFxWidth.NotApplicable]), Array.AsReadOnly<WiredFxRenderer>([WiredFxRenderer.NumberRecolorable])),
    ]);

    /// <summary>Gets a health style definition.</summary>
    /// <param name="style">The typed style identifier.</param>
    /// <returns>The style and all supported options.</returns>
    public static WiredFxStyleDefinition Get(WiredFxHealthStyle style) =>
        Find(WiredFxCategory.Health, (int)style) ?? throw new ArgumentOutOfRangeException(nameof(style));

    /// <summary>Gets a progress style definition.</summary>
    /// <param name="style">The typed style identifier.</param>
    /// <returns>The style and all supported options.</returns>
    public static WiredFxStyleDefinition Get(WiredFxProgressStyle style) =>
        Find(WiredFxCategory.Progress, (int)style) ?? throw new ArgumentOutOfRangeException(nameof(style));

    /// <summary>Gets a level style definition.</summary>
    /// <param name="style">The typed style identifier.</param>
    /// <returns>The style and all supported options.</returns>
    public static WiredFxStyleDefinition Get(WiredFxLevelStyle style) =>
        Find(WiredFxCategory.Level, (int)style) ?? throw new ArgumentOutOfRangeException(nameof(style));

    /// <summary>Gets a status style definition.</summary>
    /// <param name="style">The typed style identifier.</param>
    /// <returns>The style and all supported options.</returns>
    public static WiredFxStyleDefinition Get(WiredFxStatusStyle style) =>
        Find(WiredFxCategory.Status, (int)style) ?? throw new ArgumentOutOfRangeException(nameof(style));

    /// <summary>Gets a boss style definition.</summary>
    /// <param name="style">The typed style identifier.</param>
    /// <returns>The style and all supported options.</returns>
    public static WiredFxStyleDefinition Get(WiredFxBossStyle style) =>
        Find(WiredFxCategory.Boss, (int)style) ?? throw new ArgumentOutOfRangeException(nameof(style));

    /// <summary>Gets a number style definition.</summary>
    /// <param name="style">The typed style identifier.</param>
    /// <returns>The style and all supported options.</returns>
    public static WiredFxStyleDefinition Get(WiredFxNumberStyle style) =>
        Find(WiredFxCategory.Number, (int)style) ?? throw new ArgumentOutOfRangeException(nameof(style));

}
