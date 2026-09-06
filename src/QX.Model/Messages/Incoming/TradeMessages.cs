using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record TradeOffers(TradeOffer First, TradeOffer Second) : IParserComposer<TradeOffers>
{
    public static TradeOffers Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TradeOffers ParseFlash(in PacketReader p)
    {
        var value = new TradeOffers(p.Parse<TradeOffer>(), p.Parse<TradeOffer>());
        ValidateParticipants(value);
        TradeWire.RequireEmpty(in p, nameof(TradeOffers));
        return value;
    }

    private static TradeOffers ParseUnity(in PacketReader p)
    {
        var value = new TradeOffers(p.Parse<TradeOffer>(), p.Parse<TradeOffer>());
        ValidateParticipants(value);
        TradeWire.RequireEmpty(in p, nameof(TradeOffers));
        return value;
    }

    public TradeOffer? OfferOf(Id user_id) =>
        First.UserId == user_id ? First : Second.UserId == user_id ? Second : null;

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TradeOffers value, in PacketWriter p)
    {
        ValidateParticipants(value);
        value.First.ValidateFlash(in p);
        value.Second.ValidateFlash(in p);
        p.Compose(value.First);
        p.Compose(value.Second);
    }

    private static void ComposeUnity(TradeOffers value, in PacketWriter p)
    {
        ValidateParticipants(value);
        value.First.ValidateUnity(in p);
        value.Second.ValidateUnity(in p);
        p.Compose(value.First);
        p.Compose(value.Second);
    }

    private static void ValidateParticipants(TradeOffers value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.First);
        ArgumentNullException.ThrowIfNull(value.Second);
        TradeWire.RequirePositiveId(value.First.UserId, nameof(TradeOffer.UserId));
        TradeWire.RequirePositiveId(value.Second.UserId, nameof(TradeOffer.UserId));
        if (value.First.UserId == value.Second.UserId)
            throw new InvalidDataException("Trade offers require two distinct participants.");
    }
}

public sealed record TradeOpened(
    Id UserId,
    bool UserCanTrade,
    Id OtherUserId,
    bool OtherUserCanTrade) : IParserComposer<TradeOpened>
{
    public bool? UnityExtensionFlag { get; init; }

    public static TradeOpened Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TradeOpened ParseFlash(in PacketReader p)
    {
        var value = new TradeOpened(
            p.ReadInt(),
            TradeWire.ReadBooleanInt(p.ReadInt(), nameof(UserCanTrade)),
            p.ReadInt(),
            TradeWire.ReadBooleanInt(p.ReadInt(), nameof(OtherUserCanTrade)));
        ValidateParticipants(value);
        TradeWire.RequireEmpty(in p, nameof(TradeOpened));
        return value;
    }

    private static TradeOpened ParseUnity(in PacketReader p)
    {
        var value = new TradeOpened(
            p.ReadLong(),
            TradeWire.ReadBooleanInt(p.ReadInt(), nameof(UserCanTrade)),
            p.ReadLong(),
            TradeWire.ReadBooleanInt(p.ReadInt(), nameof(OtherUserCanTrade)));
        if (p.Available > 0)
            value = value with { UnityExtensionFlag = p.ReadBool() };
        ValidateParticipants(value);
        TradeWire.RequireEmpty(in p, nameof(TradeOpened));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TradeOpened value, in PacketWriter p)
    {
        ValidateParticipants(value);
        if (value.UnityExtensionFlag.HasValue)
            throw new InvalidDataException("Flash trade-open messages cannot carry Unity metadata.");
        int user_id = TradeWire.FlashId(value.UserId, nameof(UserId));
        int other_user_id = TradeWire.FlashId(value.OtherUserId, nameof(OtherUserId));
        p.WriteInt(user_id);
        p.WriteInt(value.UserCanTrade ? 1 : 0);
        p.WriteInt(other_user_id);
        p.WriteInt(value.OtherUserCanTrade ? 1 : 0);
    }

    private static void ComposeUnity(TradeOpened value, in PacketWriter p)
    {
        ValidateParticipants(value);
        p.WriteLong(value.UserId);
        p.WriteInt(value.UserCanTrade ? 1 : 0);
        p.WriteLong(value.OtherUserId);
        p.WriteInt(value.OtherUserCanTrade ? 1 : 0);
        if (value.UnityExtensionFlag.HasValue)
            p.WriteBool(value.UnityExtensionFlag.Value);
    }

    private static void ValidateParticipants(TradeOpened value)
    {
        ArgumentNullException.ThrowIfNull(value);
        TradeWire.RequirePositiveId(value.UserId, nameof(UserId));
        TradeWire.RequirePositiveId(value.OtherUserId, nameof(OtherUserId));
        if (value.UserId == value.OtherUserId)
            throw new InvalidDataException("A trade requires two distinct participants.");
    }
}

public sealed record TradeAccepted(Id UserId, bool Accepted) : IParserComposer<TradeAccepted>
{
    public static TradeAccepted Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TradeAccepted ParseFlash(in PacketReader p)
    {
        var value = new TradeAccepted(
            p.ReadInt(),
            p.ReadInt() > 0);
        TradeWire.RequirePositiveId(value.UserId, nameof(UserId));
        TradeWire.RequireEmpty(in p, nameof(TradeAccepted));
        return value;
    }

    private static TradeAccepted ParseUnity(in PacketReader p)
    {
        var value = new TradeAccepted(
            p.ReadLong(),
            p.ReadInt() > 0);
        TradeWire.RequirePositiveId(value.UserId, nameof(UserId));
        TradeWire.RequireEmpty(in p, nameof(TradeAccepted));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TradeAccepted value, in PacketWriter p)
    {
        TradeWire.RequirePositiveFlashId(value.UserId, nameof(UserId));
        p.WriteInt(TradeWire.FlashId(value.UserId, nameof(UserId)));
        p.WriteInt(value.Accepted ? 1 : 0);
    }

    private static void ComposeUnity(TradeAccepted value, in PacketWriter p)
    {
        TradeWire.RequirePositiveId(value.UserId, nameof(UserId));
        p.WriteLong(value.UserId);
        p.WriteInt(value.Accepted ? 1 : 0);
    }
}

public sealed record TradeClosed(Id UserId, int Reason) : IParserComposer<TradeClosed>
{
    public static TradeClosed Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TradeClosed ParseFlash(in PacketReader p)
    {
        var value = new TradeClosed(p.ReadInt(), p.ReadInt());
        TradeWire.RequirePositiveId(value.UserId, nameof(UserId));
        TradeWire.RequireEmpty(in p, nameof(TradeClosed));
        return value;
    }

    private static TradeClosed ParseUnity(in PacketReader p)
    {
        var value = new TradeClosed(p.ReadLong(), p.ReadInt());
        TradeWire.RequirePositiveId(value.UserId, nameof(UserId));
        TradeWire.RequireEmpty(in p, nameof(TradeClosed));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TradeClosed value, in PacketWriter p)
    {
        TradeWire.RequirePositiveFlashId(value.UserId, nameof(UserId));
        p.WriteInt(TradeWire.FlashId(value.UserId, nameof(UserId)));
        p.WriteInt(value.Reason);
    }

    private static void ComposeUnity(TradeClosed value, in PacketWriter p)
    {
        TradeWire.RequirePositiveId(value.UserId, nameof(UserId));
        p.WriteLong(value.UserId);
        p.WriteInt(value.Reason);
    }
}

public sealed record TradeCompleted : IParserComposer<TradeCompleted>
{
    public static TradeCompleted Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TradeCompleted ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(TradeCompleted));
        return new();
    }

    private static TradeCompleted ParseUnity(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(TradeCompleted));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TradeCompleted value, in PacketWriter p) { }

    private static void ComposeUnity(TradeCompleted value, in PacketWriter p) { }
}

public sealed record TradeConfirmation : IParserComposer<TradeConfirmation>
{
    public static TradeConfirmation Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TradeConfirmation ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(TradeConfirmation));
        return new();
    }

    private static TradeConfirmation ParseUnity(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(TradeConfirmation));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TradeConfirmation value, in PacketWriter p) { }

    private static void ComposeUnity(TradeConfirmation value, in PacketWriter p) { }
}

public sealed record TradeSilverSet(int OwnSilver, int OtherSilver) : IParserComposer<TradeSilverSet>
{
    public static TradeSilverSet Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static TradeSilverSet ParseFlash(in PacketReader p)
    {
        var value = new TradeSilverSet(p.ReadInt(), p.ReadInt());
        Validate(value);
        TradeWire.RequireEmpty(in p, nameof(TradeSilverSet));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeSilverSet value, in PacketWriter p)
    {
        Validate(value);
        p.WriteInt(value.OwnSilver);
        p.WriteInt(value.OtherSilver);
    }

    private static void Validate(TradeSilverSet value)
    {
        TradeWire.RequireNonNegative(value.OwnSilver, nameof(OwnSilver));
        TradeWire.RequireNonNegative(value.OtherSilver, nameof(OtherSilver));
    }
}

public sealed record TradeSilverFee(int SilverFee) : IParserComposer<TradeSilverFee>
{
    public static TradeSilverFee Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static TradeSilverFee ParseFlash(in PacketReader p)
    {
        var value = new TradeSilverFee(p.ReadInt());
        TradeWire.RequireNonNegative(value.SilverFee, nameof(SilverFee));
        TradeWire.RequireEmpty(in p, nameof(TradeSilverFee));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeSilverFee value, in PacketWriter p)
    {
        TradeWire.RequireNonNegative(value.SilverFee, nameof(SilverFee));
        p.WriteInt(value.SilverFee);
    }
}

public sealed record TradeNftAsset : IParserComposer<TradeNftAsset>
{
    private IReadOnlyList<int> _figure_set_ids = Array.Empty<int>();

    public TradeNftAsset(
        long asset_id,
        short product_type_id,
        string item_type_id,
        int score,
        string pet_figure_string,
        IReadOnlyList<int> figure_set_ids,
        string product_code,
        string rarity)
    {
        AssetId = asset_id;
        ProductTypeId = product_type_id;
        ItemTypeId = item_type_id;
        Score = score;
        PetFigureString = pet_figure_string;
        FigureSetIds = figure_set_ids;
        ProductCode = product_code;
        Rarity = rarity;
    }

    public long AssetId { get; init; }

    public short ProductTypeId { get; init; }

    public string ItemTypeId { get; init; }

    public int Score { get; init; }

    public string PetFigureString { get; init; }

    public IReadOnlyList<int> FigureSetIds
    {
        get => _figure_set_ids;
        init => _figure_set_ids = TradeWire.FreezeValues(value, nameof(FigureSetIds));
    }

    public string ProductCode { get; init; }

    public string Rarity { get; init; }

    public static TradeNftAsset Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static TradeNftAsset ParseFlash(in PacketReader p)
    {
        long asset_id = p.ReadLong();
        short product_type_id = p.ReadShort();
        string item_type_id = p.ReadString();
        int score = p.ReadInt();
        string pet_figure_string = p.ReadString();
        int figure_count = TradeWire.RequireCount(
            p.ReadInt(),
            p.Available,
            sizeof(int),
            nameof(FigureSetIds));
        var figure_set_ids = new int[figure_count];
        for (int index = 0; index < figure_set_ids.Length; index++)
            figure_set_ids[index] = p.ReadInt();
        string product_code = p.ReadString();
        string rarity = p.ReadString();
        var value = new TradeNftAsset(
            asset_id,
            product_type_id,
            item_type_id,
            score,
            pet_figure_string,
            figure_set_ids,
            product_code,
            rarity);
        ValidateIdentity(value);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeNftAsset value, in PacketWriter p)
    {
        value.ValidateFlash(in p);
        p.WriteLong(value.AssetId);
        p.WriteShort(value.ProductTypeId);
        p.WriteString(value.ItemTypeId);
        p.WriteInt(value.Score);
        p.WriteString(value.PetFigureString);
        p.WriteInt(value.FigureSetIds.Count);
        foreach (int figure_set_id in value.FigureSetIds)
            p.WriteInt(figure_set_id);
        p.WriteString(value.ProductCode);
        p.WriteString(value.Rarity);
    }

    internal void ValidateFlash(in PacketWriter p)
    {
        ValidateIdentity(this);
        TradeWire.RequireString(ItemTypeId, nameof(ItemTypeId), in p);
        TradeWire.RequireString(PetFigureString, nameof(PetFigureString), in p);
        TradeWire.RequireString(ProductCode, nameof(ProductCode), in p);
        TradeWire.RequireString(Rarity, nameof(Rarity), in p);
    }

    private static void ValidateIdentity(TradeNftAsset value)
    {
        if (value.AssetId <= 0)
            throw new InvalidDataException("NFT asset IDs must be positive.");
        ArgumentNullException.ThrowIfNull(value.FigureSetIds);
    }
}

public sealed record TradeNftAssets : IParserComposer<TradeNftAssets>
{
    private IReadOnlyList<TradeNftAsset> _own_assets = Array.Empty<TradeNftAsset>();
    private IReadOnlyList<TradeNftAsset> _other_assets = Array.Empty<TradeNftAsset>();

    public TradeNftAssets(
        IReadOnlyList<TradeNftAsset> own_assets,
        IReadOnlyList<TradeNftAsset> other_assets)
    {
        OwnAssets = own_assets;
        OtherAssets = other_assets;
    }

    public IReadOnlyList<TradeNftAsset> OwnAssets
    {
        get => _own_assets;
        init => _own_assets = TradeWire.FreezeReferences(value, nameof(OwnAssets));
    }

    public IReadOnlyList<TradeNftAsset> OtherAssets
    {
        get => _other_assets;
        init => _other_assets = TradeWire.FreezeReferences(value, nameof(OtherAssets));
    }

    public static TradeNftAssets Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static TradeNftAssets ParseFlash(in PacketReader p)
    {
        int own_count = TradeWire.RequireCount(
            p.ReadInt(),
            p.Available - sizeof(int),
            TradeWire.NftAssetMinimumBytes,
            nameof(OwnAssets));
        var own_assets = new TradeNftAsset[own_count];
        for (int index = 0; index < own_assets.Length; index++)
            own_assets[index] = p.Parse<TradeNftAsset>();
        int other_count = TradeWire.RequireCount(
            p.ReadInt(),
            p.Available,
            TradeWire.NftAssetMinimumBytes,
            nameof(OtherAssets));
        var other_assets = new TradeNftAsset[other_count];
        for (int index = 0; index < other_assets.Length; index++)
            other_assets[index] = p.Parse<TradeNftAsset>();
        var value = new TradeNftAssets(own_assets, other_assets);
        ValidateAssetIds(value.OwnAssets, value.OtherAssets);
        TradeWire.RequireEmpty(in p, nameof(TradeNftAssets));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeNftAssets value, in PacketWriter p)
    {
        ValidateAssetIds(value.OwnAssets, value.OtherAssets);
        foreach (TradeNftAsset asset in value.OwnAssets)
            asset.ValidateFlash(in p);
        foreach (TradeNftAsset asset in value.OtherAssets)
            asset.ValidateFlash(in p);
        p.WriteInt(value.OwnAssets.Count);
        foreach (TradeNftAsset asset in value.OwnAssets)
            p.Compose(asset);
        p.WriteInt(value.OtherAssets.Count);
        foreach (TradeNftAsset asset in value.OtherAssets)
            p.Compose(asset);
    }

    private static void ValidateAssetIds(
        IReadOnlyList<TradeNftAsset> own_assets,
        IReadOnlyList<TradeNftAsset> other_assets)
    {
        ArgumentNullException.ThrowIfNull(own_assets);
        ArgumentNullException.ThrowIfNull(other_assets);
        var seen = new HashSet<long>();
        foreach (TradeNftAsset asset in own_assets.Concat(other_assets))
        {
            ArgumentNullException.ThrowIfNull(asset);
            if (asset.AssetId <= 0)
                throw new InvalidDataException("NFT asset IDs must be positive.");
            if (!seen.Add(asset.AssetId))
                throw new InvalidDataException($"Duplicate NFT asset ID {asset.AssetId}.");
        }
    }
}

public sealed record TradeNftAssetInventory : IParserComposer<TradeNftAssetInventory>
{
    private IReadOnlyList<TradeNftAsset> _assets = Array.Empty<TradeNftAsset>();

    public TradeNftAssetInventory(IReadOnlyList<TradeNftAsset> assets) => Assets = assets;

    public IReadOnlyList<TradeNftAsset> Assets
    {
        get => _assets;
        init => _assets = TradeWire.FreezeReferences(value, nameof(Assets));
    }

    public static TradeNftAssetInventory Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static TradeNftAssetInventory ParseFlash(in PacketReader p)
    {
        int count = TradeWire.RequireCount(
            p.ReadInt(),
            p.Available,
            TradeWire.NftAssetMinimumBytes,
            nameof(Assets));
        var assets = new TradeNftAsset[count];
        for (int index = 0; index < assets.Length; index++)
            assets[index] = p.Parse<TradeNftAsset>();
        var value = new TradeNftAssetInventory(assets);
        ValidateAssetIds(value.Assets);
        TradeWire.RequireEmpty(in p, nameof(TradeNftAssetInventory));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeNftAssetInventory value, in PacketWriter p)
    {
        ValidateAssetIds(value.Assets);
        foreach (TradeNftAsset asset in value.Assets)
            asset.ValidateFlash(in p);
        p.WriteInt(value.Assets.Count);
        foreach (TradeNftAsset asset in value.Assets)
            p.Compose(asset);
    }

    private static void ValidateAssetIds(IReadOnlyList<TradeNftAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var seen = new HashSet<long>();
        foreach (TradeNftAsset asset in assets)
        {
            ArgumentNullException.ThrowIfNull(asset);
            if (asset.AssetId <= 0)
                throw new InvalidDataException("NFT asset IDs must be positive.");
            if (!seen.Add(asset.AssetId))
                throw new InvalidDataException($"Duplicate NFT asset ID {asset.AssetId}.");
        }
    }
}

public sealed record TradeOpenFailed(int Reason, string OtherUserName) : IParserComposer<TradeOpenFailed>
{
    public static TradeOpenFailed Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TradeOpenFailed ParseFlash(in PacketReader p)
    {
        var value = new TradeOpenFailed(p.ReadInt(), p.ReadString());
        TradeWire.RequireEmpty(in p, nameof(TradeOpenFailed));
        return value;
    }

    private static TradeOpenFailed ParseUnity(in PacketReader p)
    {
        var value = new TradeOpenFailed(p.ReadInt(), p.ReadString());
        TradeWire.RequireEmpty(in p, nameof(TradeOpenFailed));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TradeOpenFailed value, in PacketWriter p)
    {
        TradeWire.RequireString(value.OtherUserName, nameof(OtherUserName), in p);
        p.WriteInt(value.Reason);
        p.WriteString(value.OtherUserName);
    }

    private static void ComposeUnity(TradeOpenFailed value, in PacketWriter p)
    {
        TradeWire.RequireString(value.OtherUserName, nameof(OtherUserName), in p);
        p.WriteInt(value.Reason);
        p.WriteString(value.OtherUserName);
    }
}
