using Qx.Messages;

namespace Qx.Model.Quests;

public sealed record QuestData : IParserComposer<QuestData>
{
    private string campaign_code = "";
    private string type = "";
    private string image_version = "";
    private string localization_code = "";
    private string catalog_page_name = "";
    private string chain_code = "";

    public QuestData(
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
        int? SeasonalSecondsLeft)
    {
        this.CampaignCode = CampaignCode;
        this.CompletedQuestsInCampaign = CompletedQuestsInCampaign;
        this.QuestCountInCampaign = QuestCountInCampaign;
        this.ActivityPointType = ActivityPointType;
        this.Id = Id;
        this.IsAccepted = IsAccepted;
        this.Type = Type;
        this.ImageVersion = ImageVersion;
        this.RewardCurrencyAmount = RewardCurrencyAmount;
        this.LocalizationCode = LocalizationCode;
        this.CompletedSteps = CompletedSteps;
        this.TotalSteps = TotalSteps;
        this.SortOrder = SortOrder;
        this.CatalogPageName = CatalogPageName;
        this.ChainCode = ChainCode;
        this.IsEasy = IsEasy;
        this.IsSeasonal = IsSeasonal;
        this.SeasonalSecondsLeft = SeasonalSecondsLeft;
    }

    public string CampaignCode
    {
        get => campaign_code;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(CampaignCode));
            campaign_code = value;
        }
    }

    public int CompletedQuestsInCampaign { get; init; }
    public int QuestCountInCampaign { get; init; }
    public int ActivityPointType { get; init; }
    public int Id { get; init; }
    public bool IsAccepted { get; init; }

    public string Type
    {
        get => type;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Type));
            type = value;
        }
    }

    public string ImageVersion
    {
        get => image_version;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ImageVersion));
            image_version = value;
        }
    }

    public int RewardCurrencyAmount { get; init; }

    public string LocalizationCode
    {
        get => localization_code;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(LocalizationCode));
            localization_code = value;
        }
    }

    public int CompletedSteps { get; init; }
    public int TotalSteps { get; init; }
    public int SortOrder { get; init; }

    public string CatalogPageName
    {
        get => catalog_page_name;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(CatalogPageName));
            catalog_page_name = value;
        }
    }

    public string ChainCode
    {
        get => chain_code;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ChainCode));
            chain_code = value;
        }
    }

    public bool IsEasy { get; init; }
    public bool IsSeasonal { get; init; }
    public int? SeasonalSecondsLeft { get; init; }

    public bool IsCompleted => CompletedSteps == TotalSteps;
    public bool IsCampaignCompleted => Id < 1;
    public bool IsLastQuestInCampaign => CompletedQuestsInCampaign >= QuestCountInCampaign;
    public string CampaignChainCode => IsSeasonal
        ? $"{CampaignCode}.{ChainCode}"
        : CampaignCode;

    public void Deconstruct(
        out string CampaignCode,
        out int CompletedQuestsInCampaign,
        out int QuestCountInCampaign,
        out int ActivityPointType,
        out int Id,
        out bool IsAccepted,
        out string Type,
        out string ImageVersion,
        out int RewardCurrencyAmount,
        out string LocalizationCode,
        out int CompletedSteps,
        out int TotalSteps,
        out int SortOrder,
        out string CatalogPageName,
        out string ChainCode,
        out bool IsEasy,
        out bool IsSeasonal,
        out int? SeasonalSecondsLeft)
    {
        CampaignCode = this.CampaignCode;
        CompletedQuestsInCampaign = this.CompletedQuestsInCampaign;
        QuestCountInCampaign = this.QuestCountInCampaign;
        ActivityPointType = this.ActivityPointType;
        Id = this.Id;
        IsAccepted = this.IsAccepted;
        Type = this.Type;
        ImageVersion = this.ImageVersion;
        RewardCurrencyAmount = this.RewardCurrencyAmount;
        LocalizationCode = this.LocalizationCode;
        CompletedSteps = this.CompletedSteps;
        TotalSteps = this.TotalSteps;
        SortOrder = this.SortOrder;
        CatalogPageName = this.CatalogPageName;
        ChainCode = this.ChainCode;
        IsEasy = this.IsEasy;
        IsSeasonal = this.IsSeasonal;
        SeasonalSecondsLeft = this.SeasonalSecondsLeft;
    }

    public static QuestData Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static QuestData ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static QuestData ParseUnity(in PacketReader p) => ParseRoot(in p);

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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(QuestData value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(QuestData value, in PacketWriter p) =>
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
