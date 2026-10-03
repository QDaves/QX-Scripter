using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>TradingItemList</c> message, received with the items both trade participants offer.</summary>
/// <remarks>The two offers must belong to two distinct users with positive IDs, otherwise parsing and composing throw <see cref="InvalidDataException"/>.</remarks>
/// <param name="First">The offer of the first participant.</param>
/// <param name="Second">The offer of the second participant.</param>
public sealed record TradeOffers(TradeOffer First, TradeOffer Second) : IParserComposer<TradeOffers>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeOffers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TradeOffers ParseFlash(in PacketReader p)
    {
        var value = new TradeOffers(p.Parse<TradeOffer>(), p.Parse<TradeOffer>());
        ValidateParticipants(value);
        TradeWire.RequireEmpty(in p, nameof(TradeOffers));
        return value;
    }

    /// <summary>Gets the offer of the specified participant.</summary>
    /// <param name="userId">The user ID of the participant.</param>
    /// <returns>The participant's offer, or <see langword="null"/> when neither offer belongs to the user.</returns>
    public TradeOffer? OfferOf(Id userId) =>
        First.UserId == userId ? First : Second.UserId == userId ? Second : null;

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeOffers value, in PacketWriter p)
    {
        ValidateParticipants(value);
        value.First.ValidateFlash(in p);
        value.Second.ValidateFlash(in p);
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

/// <summary>Represents the <c>TradingOpen</c> message, received when a trade opens.</summary>
/// <remarks>
/// The two flags are sent as integers that must be 0 or 1, and the two users must be distinct with
/// positive IDs, otherwise parsing throws <see cref="InvalidDataException"/>.
/// </remarks>
/// <param name="UserId">The user ID of the first participant.</param>
/// <param name="UserCanTrade">Whether the first participant can trade.</param>
/// <param name="OtherUserId">The user ID of the second participant.</param>
/// <param name="OtherUserCanTrade">Whether the second participant can trade.</param>
public sealed record TradeOpened(
    Id UserId,
    bool UserCanTrade,
    Id OtherUserId,
    bool OtherUserCanTrade) : IParserComposer<TradeOpened>
{

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeOpened Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeOpened value, in PacketWriter p)
    {
        ValidateParticipants(value);
        int user_id = TradeWire.FlashId(value.UserId, nameof(UserId));
        int other_user_id = TradeWire.FlashId(value.OtherUserId, nameof(OtherUserId));
        p.WriteInt(user_id);
        p.WriteInt(value.UserCanTrade ? 1 : 0);
        p.WriteInt(other_user_id);
        p.WriteInt(value.OtherUserCanTrade ? 1 : 0);
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

/// <summary>Represents the <c>TradingAccept</c> message, received when a trade participant accepts the offers or withdraws acceptance.</summary>
/// <param name="UserId">The user ID of the participant.</param>
/// <param name="Accepted">Whether the participant accepted, sent as an integer where any value above 0 reads as <see langword="true"/>.</param>
public sealed record TradeAccepted(Id UserId, bool Accepted) : IParserComposer<TradeAccepted>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeAccepted Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TradeAccepted ParseFlash(in PacketReader p)
    {
        var value = new TradeAccepted(
            p.ReadInt(),
            p.ReadInt() > 0);
        TradeWire.RequirePositiveId(value.UserId, nameof(UserId));
        TradeWire.RequireEmpty(in p, nameof(TradeAccepted));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeAccepted value, in PacketWriter p)
    {
        TradeWire.RequirePositiveFlashId(value.UserId, nameof(UserId));
        p.WriteInt(TradeWire.FlashId(value.UserId, nameof(UserId)));
        p.WriteInt(value.Accepted ? 1 : 0);
    }
}

/// <summary>Represents the <c>TradingClose</c> message, received when a participant closes the trade.</summary>
/// <param name="UserId">The user ID of the participant who closed the trade.</param>
/// <param name="Reason">The close reason code sent by the hotel.</param>
public sealed record TradeClosed(Id UserId, int Reason) : IParserComposer<TradeClosed>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeClosed Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TradeClosed ParseFlash(in PacketReader p)
    {
        var value = new TradeClosed(p.ReadInt(), p.ReadInt());
        TradeWire.RequirePositiveId(value.UserId, nameof(UserId));
        TradeWire.RequireEmpty(in p, nameof(TradeClosed));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeClosed value, in PacketWriter p)
    {
        TradeWire.RequirePositiveFlashId(value.UserId, nameof(UserId));
        p.WriteInt(TradeWire.FlashId(value.UserId, nameof(UserId)));
        p.WriteInt(value.Reason);
    }
}

/// <summary>Represents the <c>TradingCompleted</c> message, received when a trade is completed.</summary>
/// <remarks>The message carries no data.</remarks>
public sealed record TradeCompleted : IParserComposer<TradeCompleted>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeCompleted Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TradeCompleted ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(TradeCompleted));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeCompleted value, in PacketWriter p) { }
}

/// <summary>Represents the <c>TradingConfirmation</c> message, received when both participants have accepted and the trade waits for confirmation.</summary>
/// <remarks>The message carries no data.</remarks>
public sealed record TradeConfirmation : IParserComposer<TradeConfirmation>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeConfirmation Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TradeConfirmation ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(TradeConfirmation));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeConfirmation value, in PacketWriter p) { }
}

/// <summary>Represents the <c>TradeSilverSet</c> message, received with the silver amounts both participants put into the trade.</summary>
/// <param name="OwnSilver">The user's silver amount, never negative.</param>
/// <param name="OtherSilver">The other participant's silver amount, never negative.</param>
public sealed record TradeSilverSet(int OwnSilver, int OtherSilver) : IParserComposer<TradeSilverSet>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeSilverSet Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TradeSilverSet ParseFlash(in PacketReader p)
    {
        var value = new TradeSilverSet(p.ReadInt(), p.ReadInt());
        Validate(value);
        TradeWire.RequireEmpty(in p, nameof(TradeSilverSet));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the <c>TradeSilverFee</c> message, received with the silver fee of the trade.</summary>
/// <param name="SilverFee">The silver fee, never negative.</param>
public sealed record TradeSilverFee(int SilverFee) : IParserComposer<TradeSilverFee>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeSilverFee Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TradeSilverFee ParseFlash(in PacketReader p)
    {
        var value = new TradeSilverFee(p.ReadInt());
        TradeWire.RequireNonNegative(value.SilverFee, nameof(SilverFee));
        TradeWire.RequireEmpty(in p, nameof(TradeSilverFee));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeSilverFee value, in PacketWriter p)
    {
        TradeWire.RequireNonNegative(value.SilverFee, nameof(SilverFee));
        p.WriteInt(value.SilverFee);
    }
}

/// <summary>Represents an NFT asset in a trade offer or in the trade NFT inventory.</summary>
/// <remarks>The values are passed through as the hotel sent them. The asset ID must be positive, otherwise parsing and composing throw <see cref="InvalidDataException"/>.</remarks>
public sealed record TradeNftAsset : IParserComposer<TradeNftAsset>
{
    private IReadOnlyList<int> _figure_set_ids = Array.Empty<int>();

    /// <summary>Initializes a new instance of the <see cref="TradeNftAsset"/> class.</summary>
    /// <param name="assetId">The ID of the asset.</param>
    /// <param name="productTypeId">The product type ID of the asset.</param>
    /// <param name="itemTypeId">The item type ID of the asset.</param>
    /// <param name="score">The score of the asset.</param>
    /// <param name="petFigureString">The pet figure string of the asset.</param>
    /// <param name="figureSetIds">The figure set IDs of the asset.</param>
    /// <param name="productCode">The product code of the asset.</param>
    /// <param name="rarity">The rarity of the asset.</param>
    public TradeNftAsset(
        long assetId,
        short productTypeId,
        string itemTypeId,
        int score,
        string petFigureString,
        IReadOnlyList<int> figureSetIds,
        string productCode,
        string rarity)
    {
        AssetId = assetId;
        ProductTypeId = productTypeId;
        ItemTypeId = itemTypeId;
        Score = score;
        PetFigureString = petFigureString;
        FigureSetIds = figureSetIds;
        ProductCode = productCode;
        Rarity = rarity;
    }

    /// <summary>Gets the ID of the asset, sent as a 64 bit integer.</summary>
    public long AssetId { get; init; }

    /// <summary>Gets the product type ID of the asset, sent as a 16 bit integer.</summary>
    public short ProductTypeId { get; init; }

    /// <summary>Gets the item type ID of the asset.</summary>
    public string ItemTypeId { get; init; }

    /// <summary>Gets the score of the asset.</summary>
    public int Score { get; init; }

    /// <summary>Gets the pet figure string of the asset.</summary>
    public string PetFigureString { get; init; }

    /// <summary>Gets the figure set IDs of the asset.</summary>
    public IReadOnlyList<int> FigureSetIds
    {
        get => _figure_set_ids;
        init => _figure_set_ids = TradeWire.FreezeValues(value, nameof(FigureSetIds));
    }

    /// <summary>Gets the product code of the asset.</summary>
    public string ProductCode { get; init; }

    /// <summary>Gets the rarity of the asset.</summary>
    public string Rarity { get; init; }

    /// <summary>Parses the asset from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeNftAsset Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the asset into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the <c>TradeNftAssets</c> message, received with the NFT assets both trade participants offer.</summary>
/// <remarks>Every asset ID must be positive and unique across both lists, otherwise parsing and composing throw <see cref="InvalidDataException"/>.</remarks>
public sealed record TradeNftAssets : IParserComposer<TradeNftAssets>
{
    private IReadOnlyList<TradeNftAsset> _own_assets = Array.Empty<TradeNftAsset>();
    private IReadOnlyList<TradeNftAsset> _other_assets = Array.Empty<TradeNftAsset>();

    /// <summary>Initializes a new instance of the <see cref="TradeNftAssets"/> class.</summary>
    /// <param name="ownAssets">The NFT assets offered by the user.</param>
    /// <param name="otherAssets">The NFT assets offered by the other participant.</param>
    public TradeNftAssets(
        IReadOnlyList<TradeNftAsset> ownAssets,
        IReadOnlyList<TradeNftAsset> otherAssets)
    {
        OwnAssets = ownAssets;
        OtherAssets = otherAssets;
    }

    /// <summary>Gets the NFT assets offered by the user.</summary>
    public IReadOnlyList<TradeNftAsset> OwnAssets
    {
        get => _own_assets;
        init => _own_assets = TradeWire.FreezeReferences(value, nameof(OwnAssets));
    }

    /// <summary>Gets the NFT assets offered by the other participant.</summary>
    public IReadOnlyList<TradeNftAsset> OtherAssets
    {
        get => _other_assets;
        init => _other_assets = TradeWire.FreezeReferences(value, nameof(OtherAssets));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeNftAssets Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the <c>TradeNftAssetInventory</c> message, received with the NFT assets the user can offer in a trade.</summary>
/// <remarks>Every asset ID must be positive and unique, otherwise parsing and composing throw <see cref="InvalidDataException"/>.</remarks>
public sealed record TradeNftAssetInventory : IParserComposer<TradeNftAssetInventory>
{
    private IReadOnlyList<TradeNftAsset> _assets = Array.Empty<TradeNftAsset>();

    /// <summary>Initializes a new instance of the <see cref="TradeNftAssetInventory"/> class.</summary>
    /// <param name="assets">The NFT assets the user can offer.</param>
    public TradeNftAssetInventory(IReadOnlyList<TradeNftAsset> assets) => Assets = assets;

    /// <summary>Gets the NFT assets the user can offer.</summary>
    public IReadOnlyList<TradeNftAsset> Assets
    {
        get => _assets;
        init => _assets = TradeWire.FreezeReferences(value, nameof(Assets));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeNftAssetInventory Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the <c>TradeOpenFailed</c> message, received when the hotel refuses to open a trade.</summary>
/// <param name="Reason">The failure reason code sent by the hotel.</param>
/// <param name="OtherUserName">The name of the other user sent by the hotel.</param>
public sealed record TradeOpenFailed(int Reason, string OtherUserName) : IParserComposer<TradeOpenFailed>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static TradeOpenFailed Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static TradeOpenFailed ParseFlash(in PacketReader p)
    {
        var value = new TradeOpenFailed(p.ReadInt(), p.ReadString());
        TradeWire.RequireEmpty(in p, nameof(TradeOpenFailed));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(TradeOpenFailed value, in PacketWriter p)
    {
        TradeWire.RequireString(value.OtherUserName, nameof(OtherUserName), in p);
        p.WriteInt(value.Reason);
        p.WriteString(value.OtherUserName);
    }
}
