using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a quest with its campaign, progress and reward.</summary>
public sealed record QuestData : IParserComposer<QuestData>
{
    private string campaign_code = "";
    private string type = "";
    private string image_version = "";
    private string localization_code = "";
    private string catalog_page_name = "";
    private string chain_code = "";

    /// <summary>Initializes a new instance of the <see cref="QuestData"/> record.</summary>
    /// <param name="campaignCode">The code of the campaign the quest belongs to.</param>
    /// <param name="completedQuestsInCampaign">The number of quests completed in the campaign.</param>
    /// <param name="questCountInCampaign">The number of quests in the campaign.</param>
    /// <param name="activityPointType">The activity point type the reward is paid in, 0 for duckets.</param>
    /// <param name="id">The quest id, less than 1 when the campaign is completed.</param>
    /// <param name="isAccepted">Whether the local user has accepted the quest.</param>
    /// <param name="type">The quest type code.</param>
    /// <param name="imageVersion">The image version of the quest.</param>
    /// <param name="rewardCurrencyAmount">The amount of activity points the quest rewards.</param>
    /// <param name="localizationCode">The localization code of the quest texts.</param>
    /// <param name="completedSteps">The number of steps completed.</param>
    /// <param name="totalSteps">The number of steps the quest has.</param>
    /// <param name="sortOrder">The sort order of the quest.</param>
    /// <param name="catalogPageName">The name of the catalog page linked to the quest.</param>
    /// <param name="chainCode">The code of the quest chain.</param>
    /// <param name="isEasy">Whether the quest is an easy quest.</param>
    /// <param name="isSeasonal">Whether the quest belongs to a seasonal campaign.</param>
    /// <param name="seasonalSecondsLeft">The seconds left before the seasonal campaign closes, or <see langword="null"/> for a quest that is not seasonal.</param>
    public QuestData(
        string campaignCode,
        int completedQuestsInCampaign,
        int questCountInCampaign,
        int activityPointType,
        int id,
        bool isAccepted,
        string type,
        string imageVersion,
        int rewardCurrencyAmount,
        string localizationCode,
        int completedSteps,
        int totalSteps,
        int sortOrder,
        string catalogPageName,
        string chainCode,
        bool isEasy,
        bool isSeasonal,
        int? seasonalSecondsLeft)
    {
        CampaignCode = campaignCode;
        CompletedQuestsInCampaign = completedQuestsInCampaign;
        QuestCountInCampaign = questCountInCampaign;
        ActivityPointType = activityPointType;
        Id = id;
        IsAccepted = isAccepted;
        Type = type;
        ImageVersion = imageVersion;
        RewardCurrencyAmount = rewardCurrencyAmount;
        LocalizationCode = localizationCode;
        CompletedSteps = completedSteps;
        TotalSteps = totalSteps;
        SortOrder = sortOrder;
        CatalogPageName = catalogPageName;
        ChainCode = chainCode;
        IsEasy = isEasy;
        IsSeasonal = isSeasonal;
        SeasonalSecondsLeft = seasonalSecondsLeft;
    }

    /// <summary>Gets the code of the campaign the quest belongs to.</summary>
    public string CampaignCode
    {
        get => campaign_code;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(CampaignCode));
            campaign_code = value;
        }
    }

    /// <summary>Gets the number of quests completed in the campaign.</summary>
    public int CompletedQuestsInCampaign { get; init; }
    /// <summary>Gets the number of quests in the campaign.</summary>
    public int QuestCountInCampaign { get; init; }
    /// <summary>Gets the activity point type the reward is paid in, 0 for duckets.</summary>
    public int ActivityPointType { get; init; }
    /// <summary>Gets the quest id, less than 1 when the campaign is completed.</summary>
    public int Id { get; init; }
    /// <summary>Gets whether the local user has accepted the quest.</summary>
    public bool IsAccepted { get; init; }

    /// <summary>Gets the quest type code.</summary>
    public string Type
    {
        get => type;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Type));
            type = value;
        }
    }

    /// <summary>Gets the image version of the quest.</summary>
    public string ImageVersion
    {
        get => image_version;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ImageVersion));
            image_version = value;
        }
    }

    /// <summary>Gets the amount of activity points the quest rewards.</summary>
    public int RewardCurrencyAmount { get; init; }

    /// <summary>Gets the localization code of the quest texts.</summary>
    public string LocalizationCode
    {
        get => localization_code;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(LocalizationCode));
            localization_code = value;
        }
    }

    /// <summary>Gets the number of steps completed.</summary>
    public int CompletedSteps { get; init; }
    /// <summary>Gets the number of steps the quest has.</summary>
    public int TotalSteps { get; init; }
    /// <summary>Gets the sort order of the quest.</summary>
    public int SortOrder { get; init; }

    /// <summary>Gets the name of the catalog page linked to the quest.</summary>
    public string CatalogPageName
    {
        get => catalog_page_name;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(CatalogPageName));
            catalog_page_name = value;
        }
    }

    /// <summary>Gets the code of the quest chain.</summary>
    public string ChainCode
    {
        get => chain_code;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ChainCode));
            chain_code = value;
        }
    }

    /// <summary>Gets whether the quest is an easy quest.</summary>
    public bool IsEasy { get; init; }
    /// <summary>Gets whether the quest belongs to a seasonal campaign.</summary>
    public bool IsSeasonal { get; init; }
    /// <summary>Gets the seconds left before the seasonal campaign closes, or <see langword="null"/> for a quest that is not seasonal.</summary>
    public int? SeasonalSecondsLeft { get; init; }

    /// <summary>Gets whether every step is completed, which is when <see cref="CompletedSteps"/> equals <see cref="TotalSteps"/>.</summary>
    public bool IsCompleted => CompletedSteps == TotalSteps;
    /// <summary>Gets whether the whole campaign is completed, which is when <see cref="QuestData.Id"/> is less than 1.</summary>
    public bool IsCampaignCompleted => Id < 1;
    /// <summary>Gets whether the quest is the last one of its campaign, which is when <see cref="CompletedQuestsInCampaign"/> reaches <see cref="QuestCountInCampaign"/>.</summary>
    public bool IsLastQuestInCampaign => CompletedQuestsInCampaign >= QuestCountInCampaign;
    /// <summary>Gets the campaign code, followed by <c>.</c> and <see cref="ChainCode"/> for seasonal quests.</summary>
    public string CampaignChainCode => IsSeasonal
        ? $"{CampaignCode}.{ChainCode}"
        : CampaignCode;

    /// <summary>Deconstructs the quest into its fields.</summary>
    /// <param name="campaignCode">The code of the campaign the quest belongs to.</param>
    /// <param name="completedQuestsInCampaign">The number of quests completed in the campaign.</param>
    /// <param name="questCountInCampaign">The number of quests in the campaign.</param>
    /// <param name="activityPointType">The activity point type the reward is paid in.</param>
    /// <param name="id">The quest id.</param>
    /// <param name="isAccepted">Whether the local user has accepted the quest.</param>
    /// <param name="type">The quest type code.</param>
    /// <param name="imageVersion">The image version of the quest.</param>
    /// <param name="rewardCurrencyAmount">The amount of activity points the quest rewards.</param>
    /// <param name="localizationCode">The localization code of the quest texts.</param>
    /// <param name="completedSteps">The number of steps completed.</param>
    /// <param name="totalSteps">The number of steps the quest has.</param>
    /// <param name="sortOrder">The sort order of the quest.</param>
    /// <param name="catalogPageName">The name of the catalog page linked to the quest.</param>
    /// <param name="chainCode">The code of the quest chain.</param>
    /// <param name="isEasy">Whether the quest is an easy quest.</param>
    /// <param name="isSeasonal">Whether the quest belongs to a seasonal campaign.</param>
    /// <param name="seasonalSecondsLeft">The seconds left before the seasonal campaign closes, or <see langword="null"/>.</param>
    public void Deconstruct(
        out string campaignCode,
        out int completedQuestsInCampaign,
        out int questCountInCampaign,
        out int activityPointType,
        out int id,
        out bool isAccepted,
        out string type,
        out string imageVersion,
        out int rewardCurrencyAmount,
        out string localizationCode,
        out int completedSteps,
        out int totalSteps,
        out int sortOrder,
        out string catalogPageName,
        out string chainCode,
        out bool isEasy,
        out bool isSeasonal,
        out int? seasonalSecondsLeft)
    {
        campaignCode = CampaignCode;
        completedQuestsInCampaign = CompletedQuestsInCampaign;
        questCountInCampaign = QuestCountInCampaign;
        activityPointType = ActivityPointType;
        id = Id;
        isAccepted = IsAccepted;
        type = Type;
        imageVersion = ImageVersion;
        rewardCurrencyAmount = RewardCurrencyAmount;
        localizationCode = LocalizationCode;
        completedSteps = CompletedSteps;
        totalSteps = TotalSteps;
        sortOrder = SortOrder;
        catalogPageName = CatalogPageName;
        chainCode = ChainCode;
        isEasy = IsEasy;
        isSeasonal = IsSeasonal;
        seasonalSecondsLeft = SeasonalSecondsLeft;
    }

    /// <summary>Parses quest data from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static QuestData Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static QuestData ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static QuestData ParseRoot(in PacketReader p)
    {
        var strings = QuestWire.NewStringBudget();
        QuestData value = ParseWire(in p, 0, ref strings);
        QuestWire.RequireEmpty(in p, nameof(QuestData));
        return value;
    }

    internal static QuestData ParseWire(
        in PacketReader p,
        int trailing_bytes,
        ref QuestStringBudget strings)
    {
        QuestWire.RequireRemaining(
            in p,
            QuestWire.QuestMinimumBytes,
            trailing_bytes,
            nameof(QuestData));
        string campaign_code = strings.Read(
            in p,
            nameof(CampaignCode),
            checked(trailing_bytes + 45));
        int completed_quests = p.ReadInt();
        int quest_count = p.ReadInt();
        int activity_point_type = p.ReadInt();
        int id = p.ReadInt();
        bool is_accepted = p.ReadBool();
        string type = strings.Read(in p, nameof(Type), checked(trailing_bytes + 26));
        string image_version = strings.Read(
            in p,
            nameof(ImageVersion),
            checked(trailing_bytes + 24));
        int reward_currency_amount = p.ReadInt();
        string localization_code = strings.Read(
            in p,
            nameof(LocalizationCode),
            checked(trailing_bytes + 18));
        int completed_steps = p.ReadInt();
        int total_steps = p.ReadInt();
        int sort_order = p.ReadInt();
        string catalog_page_name = strings.Read(
            in p,
            nameof(CatalogPageName),
            checked(trailing_bytes + 4));
        string chain_code = strings.Read(
            in p,
            nameof(ChainCode),
            checked(trailing_bytes + 2));
        bool is_easy = p.ReadBool();
        bool is_seasonal = p.ReadBool();
        int? seasonal_seconds_left = null;
        if (is_seasonal)
        {
            QuestWire.RequireRemaining(
                in p,
                sizeof(int),
                trailing_bytes,
                nameof(SeasonalSecondsLeft));
            seasonal_seconds_left = p.ReadInt();
        }
        return new QuestData(
            campaign_code,
            completed_quests,
            quest_count,
            activity_point_type,
            id,
            is_accepted,
            type,
            image_version,
            reward_currency_amount,
            localization_code,
            completed_steps,
            total_steps,
            sort_order,
            catalog_page_name,
            chain_code,
            is_easy,
            is_seasonal,
            seasonal_seconds_left);
    }

    /// <summary>Composes the quest data into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">Thrown when <see cref="IsSeasonal"/> does not match whether <see cref="SeasonalSecondsLeft"/> is set.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(QuestData value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeRoot(QuestData value, in PacketWriter p)
    {
        var strings = QuestWire.NewStringBudget();
        QuestDataWireSnapshot snapshot = PrepareWire(value, ref strings, in p);
        WriteWire(snapshot, in p);
    }

    internal static QuestDataWireSnapshot PrepareWire(
        QuestData value,
        ref QuestStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IsSeasonal != value.SeasonalSecondsLeft.HasValue)
        {
            throw new InvalidDataException(
                "Seasonal quest data requires exactly one seasonal lifetime value.");
        }
        var snapshot = new QuestDataWireSnapshot(
            value.CampaignCode,
            value.CompletedQuestsInCampaign,
            value.QuestCountInCampaign,
            value.ActivityPointType,
            value.Id,
            value.IsAccepted,
            value.Type,
            value.ImageVersion,
            value.RewardCurrencyAmount,
            value.LocalizationCode,
            value.CompletedSteps,
            value.TotalSteps,
            value.SortOrder,
            value.CatalogPageName,
            value.ChainCode,
            value.IsEasy,
            value.IsSeasonal,
            value.SeasonalSecondsLeft);
        strings.Require(snapshot.CampaignCode, nameof(CampaignCode), in p);
        strings.Require(snapshot.Type, nameof(Type), in p);
        strings.Require(snapshot.ImageVersion, nameof(ImageVersion), in p);
        strings.Require(snapshot.LocalizationCode, nameof(LocalizationCode), in p);
        strings.Require(snapshot.CatalogPageName, nameof(CatalogPageName), in p);
        strings.Require(snapshot.ChainCode, nameof(ChainCode), in p);
        return snapshot;
    }

    internal static void WriteWire(QuestDataWireSnapshot value, in PacketWriter p)
    {
        p.WriteString(value.CampaignCode);
        p.WriteInt(value.CompletedQuestsInCampaign);
        p.WriteInt(value.QuestCountInCampaign);
        p.WriteInt(value.ActivityPointType);
        p.WriteInt(value.Id);
        p.WriteBool(value.IsAccepted);
        p.WriteString(value.Type);
        p.WriteString(value.ImageVersion);
        p.WriteInt(value.RewardCurrencyAmount);
        p.WriteString(value.LocalizationCode);
        p.WriteInt(value.CompletedSteps);
        p.WriteInt(value.TotalSteps);
        p.WriteInt(value.SortOrder);
        p.WriteString(value.CatalogPageName);
        p.WriteString(value.ChainCode);
        p.WriteBool(value.IsEasy);
        p.WriteBool(value.IsSeasonal);
        if (value.SeasonalSecondsLeft is int seconds_left)
            p.WriteInt(seconds_left);
    }
}

internal readonly record struct QuestDataWireSnapshot(
    string CampaignCode,
    int CompletedQuestsInCampaign,
    int QuestCountInCampaign,
    int ActivityPointType,
    int Id,
    bool IsAccepted,
    string Type,
    string ImageVersion,
    int RewardCurrencyAmount,
    string LocalizationCode,
    int CompletedSteps,
    int TotalSteps,
    int SortOrder,
    string CatalogPageName,
    string ChainCode,
    bool IsEasy,
    bool IsSeasonal,
    int? SeasonalSecondsLeft);
