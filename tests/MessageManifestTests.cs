using System.Reflection;
using Qx;
using Qx.Game;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Crafting;
using Qx.Model.Forums;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Model.Quests;
using Qx.Model.Subscriptions;
using Qx.Model.Wired;
using Qx.Protocol;
using Xunit;

namespace QX.Tests;

public sealed class MessageManifestTests
{
    private const string FlashClubGiftInfoHex =
        "000000030000000100000001" +
        "0000002A00056F6666657200" +
        "0000000A00000014000000050000000301" +
        "0000000100017300000FA10001300000000100" +
        "000000020100000170" +
        "000000010000002A010000001E01";

    private const string UnityClubGiftInfoHex =
        "00000003000000010001" +
        "0000002A00056F6666657200" +
        "0000000A00000014000000050000000301" +
        "0001000100056173736574" +
        "0001000100000FA10001300000000100" +
        "00020100000170" +
        "00010000002A0000001E01";

    private const string CurrentCraftingProductHex =
        "000272310002703200056675726E69";

    private const string LegacyUnityCraftingProductHex =
        "0002723100056675726E69";

    private const string CraftingIngredientHex =
        "0000000300056368616972";

    private const string CraftingFurnitureClassesHex =
        "0005636861697200057461626C65";

    [Theory]
    [InlineData(-123)]
    [InlineData(0)]
    public void flash_trade_offers_preserve_opaque_item_identifiers(int item_id)
    {
        using var packet = new Packet(new Header(Direction.In, 1), ClientType.Flash);
        PacketWriter writer = packet.Writer();
        writer.WriteInt(11);
        writer.WriteInt(1);
        writer.WriteInt(item_id);
        writer.WriteString("S");
        writer.WriteInt(321);
        writer.WriteInt(42);
        writer.WriteInt(1);
        writer.WriteBool(true);
        writer.Compose(new LegacyData { Value = "" });
        writer.WriteInt(15);
        writer.WriteInt(8);
        writer.WriteInt(2026);
        writer.WriteInt(0);
        writer.WriteInt(1);
        writer.WriteInt(0);
        writer.WriteInt(22);
        writer.WriteInt(0);
        writer.WriteInt(0);
        writer.WriteInt(0);
        byte[] raw = packet.Buffer.Span.ToArray();
        packet.Position = 0;

        TradeOffers offers = packet.Reader().Parse<TradeOffers>();

        TradeItem item = Assert.Single(offers.First.Items);
        Assert.Equal(item_id, (long)item.ItemId);
        Assert.Equal(0, packet.Available);
        using var roundtrip = new Packet(new Header(Direction.In, 1), ClientType.Flash);
        roundtrip.Writer().Compose(offers);
        Assert.Equal(raw, roundtrip.Buffer.Span.ToArray());
    }

    [Fact]
    public void flash_trade_commands_accept_signed_nonzero_item_identifiers()
    {
        using var add_packet = new Packet(new Header(Direction.Out, 1), ClientType.Flash);
        add_packet.Writer().Compose(
            new AddTradeItemsRequest(Array.AsReadOnly<Id>([-123])));
        Assert.Equal(Convert.FromHexString("00000001FFFFFF85"), add_packet.Buffer.Span.ToArray());
        add_packet.Position = 0;
        AddTradeItemsRequest add = add_packet.Reader().Parse<AddTradeItemsRequest>();
        Assert.Equal([-123L], add.ItemIds.Select(value => (long)value).ToArray());

        using var remove_packet = new Packet(new Header(Direction.Out, 1), ClientType.Flash);
        remove_packet.Writer().Compose(new RemoveTradeItemRequest(-123));
        Assert.Equal(Convert.FromHexString("FFFFFF85"), remove_packet.Buffer.Span.ToArray());
        remove_packet.Position = 0;
        RemoveTradeItemRequest remove = remove_packet.Reader().Parse<RemoveTradeItemRequest>();
        Assert.Equal(-123, (long)remove.ItemId);
    }

    [Fact]
    public void flash_wired_chest_offers_preserve_signed_inventory_identifiers()
    {
        Id[] ids = [-919361823, -919361824, -919361813];
        using var packet = new Packet(new Header(Direction.Out, 1), ClientType.Flash);

        packet.Writer().Compose(WiredTradeAddDeleteItems.Add(ids));

        Assert.Equal(
            Convert.FromHexString("0000000003C933A6E1C933A6E0C933A6EB"),
            packet.Buffer.Span.ToArray());
        packet.Position = 0;
        WiredTradeAddDeleteItems parsed = packet.Reader().Parse<WiredTradeAddDeleteItems>();
        Assert.False(parsed.IsRemove);
        Assert.Equal(ids, parsed.Ids);
        Assert.Equal(0, packet.Available);
    }

    [Fact]
    public void unity_wired_chest_trade_matches_the_live_wire_layout()
    {
        Id[] ids = [-919361823, -919361824, -919361813];
        using var offer = new Packet(new Header(Direction.Out, 1), ClientType.Unity);

        offer.Writer().Compose(WiredTradeAddDeleteItems.Add(ids));

        Assert.Equal(
            Convert.FromHexString(
                "000003FFFFFFFFC933A6E1FFFFFFFFC933A6E0FFFFFFFFC933A6EB"),
            offer.Buffer.Span.ToArray());
        offer.Position = 0;
        WiredTradeAddDeleteItems parsed_offer = offer.Reader().Parse<WiredTradeAddDeleteItems>();
        Assert.False(parsed_offer.IsRemove);
        Assert.Equal(ids, parsed_offer.Ids);
        Assert.Equal(0, offer.Available);

        using var update = new Packet(
            new Header(Direction.In, 1),
            ClientType.Unity,
            new PacketBuffer(Convert.FromHexString(
                "000000000403D4C6" +
                "00000000" +
                "00000000" +
                "00000000" +
                "0000000000000000" +
                "00000000" +
                "00000000" +
                "00000000" +
                "01" +
                "00000000")));
        WiredTradeItemsUpdate parsed_update = update.Reader().Parse<WiredTradeItemsUpdate>();
        Assert.Equal(67359942, (long)parsed_update.TradingItems.FirstUserId);
        Assert.Equal(0, (long)parsed_update.TradingItems.SecondUserId);
        Assert.True(parsed_update.CanAccept);
        Assert.Equal(0, update.Available);

        using var roundtrip = new Packet(new Header(Direction.In, 1), ClientType.Unity);
        roundtrip.Writer().Compose(parsed_update);
        Assert.Equal(update.Buffer.Span.ToArray(), roundtrip.Buffer.Span.ToArray());

        using var chunk = new Packet(
            new Header(Direction.In, 1),
            ClientType.Unity,
            new PacketBuffer(Convert.FromHexString(
                "0000000036CC77F0" +
                "00000001" +
                "00000000" +
                "0000")));
        ItemsChestContentsChunk parsed_chunk = chunk.Reader().Parse<ItemsChestContentsChunk>();
        Assert.Equal(919369712, parsed_chunk.ChestId);
        Assert.Equal(1, parsed_chunk.TotalFragments);
        Assert.Equal(0, parsed_chunk.FragmentNo);
        Assert.Empty(parsed_chunk.StorageChunk);
        Assert.Equal(0, chunk.Available);

        using var chunk_roundtrip = new Packet(new Header(Direction.In, 1), ClientType.Unity);
        chunk_roundtrip.Writer().Compose(parsed_chunk);
        Assert.Equal(chunk.Buffer.Span.ToArray(), chunk_roundtrip.Buffer.Span.ToArray());

        using var contents_update = new Packet(
            new Header(Direction.In, 1),
            ClientType.Unity,
            new PacketBuffer(Convert.FromHexString("0000000036CC77F000000000")));
        ItemsChestContentsUpdated parsed_contents_update = contents_update
            .Reader()
            .Parse<ItemsChestContentsUpdated>();
        Assert.Equal(919369712, parsed_contents_update.ChestId);
        Assert.Empty(parsed_contents_update.RemovedIds);
        Assert.Empty(parsed_contents_update.AddedStorage);
        Assert.Equal(0, contents_update.Available);

        using var contents_update_roundtrip = new Packet(new Header(Direction.In, 1), ClientType.Unity);
        contents_update_roundtrip.Writer().Compose(parsed_contents_update);
        Assert.Equal(
            contents_update.Buffer.Span.ToArray(),
            contents_update_roundtrip.Buffer.Span.ToArray());

        using var populated_update = new Packet(
            new Header(Direction.In, 1),
            ClientType.Unity,
            new PacketBuffer(Convert.FromHexString(
                "0000000036CC77F0" +
                "0000" +
                "0001" +
                "FFFFFFFFC9338CC8" +
                "00000000" +
                "000000000014C17F" +
                "00" +
                "000005FE" +
                "0000" +
                "01" +
                "00000001" +
                "00000000" +
                "0000" +
                "0000000000000000")));
        ItemsChestContentsUpdated parsed_populated_update = populated_update
            .Reader()
            .Parse<ItemsChestContentsUpdated>();
        ChestStorage stored = Assert.Single(parsed_populated_update.AddedStorage);
        Assert.Equal(-919368504, stored.InventoryId);
        Assert.Equal(0, stored.LockState);
        Assert.Equal(1360255, stored.TransactionId);
        Assert.False(stored.Type.IsWallItem);
        Assert.Equal(1534, stored.Type.TypeId);
        Assert.Equal("", stored.Type.LegacyPosterId);
        Assert.True(stored.Groupable);
        Assert.Equal(1, stored.SpecialType);
        Assert.IsType<LegacyData>(stored.StuffData);
        Assert.Equal("", stored.StuffData.Value);
        Assert.Equal(0, stored.Extra);
        Assert.Equal(0, populated_update.Available);

        using var populated_roundtrip = new Packet(new Header(Direction.In, 1), ClientType.Unity);
        populated_roundtrip.Writer().Compose(parsed_populated_update);
        Assert.Equal(
            populated_update.Buffer.Span.ToArray(),
            populated_roundtrip.Buffer.Span.ToArray());
    }

    [Theory]
    [InlineData(ClientType.Unity, Direction.In, "CancelBuddyRequest")]
    [InlineData(ClientType.Unity, Direction.Out, "CancelFriendRequest")]
    [InlineData(ClientType.Flash, Direction.In, "CanCreateRoomEvent")]
    [InlineData(ClientType.Flash, Direction.In, "EmailStatusResult")]
    [InlineData(ClientType.Flash, Direction.Out, "GetHeightMap")]
    [InlineData(ClientType.Flash, Direction.Out, "IgnoreUserId")]
    [InlineData(ClientType.Flash, Direction.In, "NavigatorCollapsedCategories")]
    [InlineData(ClientType.Flash, Direction.In, "NewConsole")]
    [InlineData(ClientType.Unity, Direction.In, "PurchaseRoomAdResult")]
    [InlineData(ClientType.Flash, Direction.In, "RoomEvent")]
    public void unverified_wire_aliases_are_not_registered(
        ClientType client,
        Direction direction,
        string name)
    {
        MessageRegistry registry = MessagesIniParser.ParseEmbeddedRegistry();

        Assert.False(registry.TryGet(client, direction, name, out _));
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void gift_wire_roundtrips_every_verified_modern_route(ClientType client)
    {
        string wrapping_hex = client is ClientType.Flash
            ? "0100000005000000020000000AFFFFFFFF000000010000001400000000000000020000001E00000028"
            : "010000000500020000000AFFFFFFFF000100000014000000020000001E00000028";
        GiftWrappingConfiguration wrapping = AssertGiftFixture(
            MessageContracts.Gifts.WrappingConfiguration,
            client,
            Direction.In,
            wrapping_hex);
        Assert.Equal([10, -1], wrapping.StuffTypes);
        Assert.Equal([20], wrapping.BoxTypes);
        Assert.Empty(wrapping.RibbonTypes);
        Assert.Equal([30, 40], wrapping.DefaultStuffTypes);

        string present_hex = client is ClientType.Flash
            ? "000173010203040004636F6465112233440005666C6F6F72010003706574"
            : "000173010203040004636F646501020304050607080005666C6F6F72010003706574";
        PresentOpened present = AssertGiftFixture(
            MessageContracts.Gifts.PresentOpened,
            client,
            Direction.In,
            present_hex);
        Assert.Equal(client is ClientType.Flash ? 0x11223344L : 0x0102030405060708L,
            (long)present.PlacedItemId);

        ClubGiftInfo club_info = AssertGiftFixture(
            MessageContracts.Gifts.ClubInfo,
            client,
            Direction.In,
            client is ClientType.Flash ? FlashClubGiftInfoHex : UnityClubGiftInfoHex);
        Assert.Equal(client is ClientType.Flash ? true : null,
            Assert.Single(club_info.GiftEligibility).IsVip);
        Assert.Equal(4001, Assert.Single(Assert.Single(club_info.Offers).Products).FurniClassId);

        string selected_hex = client is ClientType.Flash
            ? "0004676966740000000100016200054241444745"
            : "0004676966740001000600000FA300036961700000000100";
        ClubGiftSelected selected = AssertGiftFixture(
            MessageContracts.Gifts.ClubSelected,
            client,
            Direction.In,
            selected_hex);
        Assert.Equal(client is ClientType.Flash ? "b" : "unity:6",
            Assert.Single(selected.Products).ProductType);

        _ = AssertGiftFixture(
            MessageContracts.Gifts.NewUserIncomplete,
            client,
            Direction.In,
            "");
        _ = AssertGiftFixture(
            MessageContracts.Gifts.WrappingConfigurationRequest,
            client,
            Direction.Out,
            "");
        _ = AssertGiftFixture(
            MessageContracts.Gifts.PresentOpen,
            client,
            Direction.Out,
            client is ClientType.Flash ? "11223344" : "0000000011223344");
        _ = AssertGiftFixture(
            MessageContracts.Gifts.Purchase,
            client,
            Direction.Out,
            (client is ClientType.Flash
                ? "000000030000000400000003626F6200046769667400000005000000060000000701"
                : "000000030000000400000003626F620004676966740000000500000006000000070100000008"));
        _ = AssertGiftFixture(
            MessageContracts.Gifts.ClubInfoRequest,
            client,
            Direction.Out,
            "");
        _ = AssertGiftFixture(
            MessageContracts.Gifts.ClubSelect,
            client,
            Direction.Out,
            "000470726F64");
        _ = AssertGiftFixture(
            MessageContracts.Gifts.OfferGiftabilityRequest,
            client,
            Direction.Out,
            "01020304");
        _ = AssertGiftFixture(
            MessageContracts.Gifts.NewUserSelect,
            client,
            Direction.Out,
            client is ClientType.Flash
                ? "00000006000000010000000200000003000000040000000500000006"
                : "0006000000010000000200000003000000040000000500000006");
        _ = AssertGiftFixture(
            MessageContracts.Gifts.NewUserAdvance,
            client,
            Direction.Out,
            "");
    }

    [Fact]
    public void gift_flash_only_routes_fail_closed_on_unverified_unity_codecs()
    {
        _ = AssertGiftFixture(
            MessageContracts.Gifts.ReceiverNotFound,
            ClientType.Flash,
            Direction.In,
            "");
        ClubGiftNotification notification = AssertGiftFixture(
            MessageContracts.Gifts.ClubNotification,
            ClientType.Flash,
            Direction.In,
            "00000005");
        Assert.Equal(5, notification.NumGifts);
        IsOfferGiftable giftable = AssertGiftFixture(
            MessageContracts.Gifts.OfferGiftability,
            ClientType.Flash,
            Direction.In,
            "0000002A01");
        Assert.Equal(42, giftable.OfferId);
        Assert.True(giftable.IsGiftable);
        NuxGiftOffer offer = AssertGiftFixture(
            MessageContracts.Gifts.NewUserOffer,
            ClientType.Flash,
            Direction.In,
            "0000000100000002000000030000000100097468756D622E706E6700000001000563686169720000");
        Assert.Null(Assert.Single(Assert.Single(Assert.Single(offer.Steps).Options).Products).LocalizationKey);

        using var unity = new Packet(new Header(Direction.In, 91), ClientType.Unity);
        Assert.Throws<UnsupportedClientException>(() =>
            MessageContracts.Gifts.ReceiverNotFound.Parse(unity.Reader()));
        Assert.Throws<UnsupportedClientException>(() =>
            MessageContracts.Gifts.ClubNotification.Parse(unity.Reader()));
        Assert.Throws<UnsupportedClientException>(() =>
            MessageContracts.Gifts.OfferGiftability.Parse(unity.Reader()));
        Assert.Throws<UnsupportedClientException>(() =>
            MessageContracts.Gifts.NewUserOffer.Parse(unity.Reader()));
        Assert.Equal(0, unity.Position);
    }

    [Fact]
    public void gift_models_freeze_lists_and_preflight_atomically()
    {
        var mutable_values = new List<int> { 1 };
        var wrapping = new GiftWrappingConfiguration(
            true,
            2,
            mutable_values,
            [],
            [],
            []);
        mutable_values[0] = 9;
        mutable_values.Clear();
        Assert.Equal([1], wrapping.StuffTypes);

        var mutable_products = new List<NuxGiftProduct> { new("chair", null) };
        var option = new NuxGiftOption(null, mutable_products);
        mutable_products.Clear();
        var mutable_options = new List<NuxGiftOption> { option };
        var step = new NuxGiftStep(1, 2, mutable_options);
        mutable_options.Clear();
        var mutable_steps = new List<NuxGiftStep> { step };
        var offer = new NuxGiftOffer(mutable_steps);
        mutable_steps.Clear();
        Assert.Single(Assert.Single(Assert.Single(offer.Steps).Options).Products);
        Assert.Throws<ArgumentNullException>(() => new NuxGiftOffer(null!));
        Assert.Throws<ArgumentNullException>(() => new NuxGiftOffer([null!]));
        Assert.Throws<InvalidDataException>(() =>
            new NuxGiftOffer(new NuxGiftStep[4097]));

        AssertGiftComposeIsAtomic(
            MessageContracts.Gifts.Purchase,
            new PurchaseFromCatalogAsGift(
                1,
                2,
                "valid",
                "receiver",
                new string('x', ushort.MaxValue + 1),
                3,
                4,
                5,
                false,
                1),
            ClientType.Unity,
            Direction.Out);
        AssertGiftComposeIsAtomic(
            MessageContracts.Gifts.Purchase,
            new PurchaseFromCatalogAsGift(1, 2, "", "receiver", "gift", 3, 4, 5, false),
            ClientType.Unity,
            Direction.Out);
        AssertGiftComposeIsAtomic(
            MessageContracts.Gifts.PresentOpened,
            new PresentOpened("s", 1, "code", long.MaxValue, "floor", true, ""),
            ClientType.Flash,
            Direction.In);
        AssertGiftComposeIsAtomic(
            MessageContracts.Gifts.ClubInfo,
            new ClubGiftInfo(1, 1, [], [new ClubGiftEligibility(1, true, 2, true)]),
            ClientType.Unity,
            Direction.In);
        AssertGiftComposeIsAtomic(
            MessageContracts.Gifts.NewUserOffer,
            new NuxGiftOffer(
            [
                new NuxGiftStep(
                    1,
                    2,
                    [new NuxGiftOption("valid", [new NuxGiftProduct("valid", new string('x', ushort.MaxValue + 1))])])
            ]),
            ClientType.Flash,
            Direction.In);
    }

    [Fact]
    public void gift_parsers_reject_invalid_counts_tails_and_trailing_bytes()
    {
        AssertGiftParseFails<GiftWrappingConfiguration>(
            "0100000005000000010000000000000000000000",
            ClientType.Flash,
            Direction.In);
        AssertGiftParseFails<NuxGetGifts>(
            "00020000000100000002",
            ClientType.Unity,
            Direction.Out);
        AssertGiftParseFails<NuxGiftOffer>(
            "FFFFFFFF",
            ClientType.Flash,
            Direction.In);
        AssertGiftParseFails<ClubGiftInfo>(
            "00000001000000010000000100000000",
            ClientType.Flash,
            Direction.In);

        byte[] sibling_tail = Convert.FromHexString(
            "0000000000020001000000000000");
        using var selected = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash,
            new PacketBuffer(sibling_tail));
        Assert.Throws<InvalidDataException>(() =>
            MessageContracts.Gifts.ClubSelected.Parse(selected.Reader()));
        Assert.Equal(6, selected.Available);

        using var empty_with_tail = new Packet(
            new Header(Direction.Out, 91),
            ClientType.Unity,
            new PacketBuffer([0x7f]));
        Assert.Throws<InvalidDataException>(() =>
            MessageContracts.Gifts.NewUserAdvance.Parse(empty_with_tail.Reader()));
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void crafting_wire_roundtrips_every_verified_route(ClientType client)
    {
        CraftableProducts products = AssertCraftingFixture(
            MessageContracts.Crafting.ProductsSnapshot,
            client,
            Direction.In,
            client is ClientType.Flash
                ? "00000001" + CurrentCraftingProductHex + "00000002" +
                    CraftingFurnitureClassesHex
                : "0001" + CurrentCraftingProductHex + "0002" +
                    CraftingFurnitureClassesHex);
        CraftingProduct product = Assert.Single(products.Products);
        Assert.Equal("r1", product.RecipeCode);
        Assert.Equal("p2", product.ProductCode);
        Assert.Equal("furni", product.FurnitureClassName);
        Assert.Equal(["chair", "table"], products.UsableInventoryFurnitureClasses);

        CraftingRecipe recipe = AssertCraftingFixture(
            MessageContracts.Crafting.RecipeSnapshot,
            client,
            Direction.In,
            (client is ClientType.Flash ? "00000001" : "0001") +
                CraftingIngredientHex);
        CraftingIngredient ingredient = Assert.Single(recipe.Ingredients);
        Assert.Equal(3, ingredient.Count);
        Assert.Equal("chair", ingredient.FurnitureClassName);

        CraftingResult success = AssertCraftingFixture(
            MessageContracts.Crafting.Result,
            client,
            Direction.In,
            "01" + CurrentCraftingProductHex);
        Assert.True(success.Success);
        Assert.Equal("p2", success.Product?.ProductCode);

        CraftingResult failure = AssertCraftingFixture(
            MessageContracts.Crafting.Result,
            client,
            Direction.In,
            client is ClientType.Flash
                ? "00"
                : "00" + CurrentCraftingProductHex);
        Assert.False(failure.Success);
        Assert.Equal(client is ClientType.Flash ? null : "p2", failure.Product?.ProductCode);

        CraftingRecipesAvailable available = AssertCraftingFixture(
            MessageContracts.Crafting.AvailabilitySnapshot,
            client,
            Direction.In,
            "0000000401");
        Assert.Equal(4, available.Count);
        Assert.True(available.IsRecipeComplete);

        GetCraftableProducts products_request = AssertCraftingFixture(
            MessageContracts.Crafting.ProductsRequest,
            client,
            Direction.Out,
            client is ClientType.Flash
                ? "01020304"
                : "0102030405060708");
        Assert.Equal(
            client is ClientType.Flash
                ? 0x01020304L
                : 0x0102030405060708L,
            (long)products_request.CraftingFurnitureId);

        GetCraftingRecipe recipe_request = AssertCraftingFixture(
            MessageContracts.Crafting.RecipeRequest,
            client,
            Direction.Out,
            "00027231");
        Assert.Equal("r1", recipe_request.RecipeCode);

        Qx.Model.Messages.Incoming.Craft craft = AssertCraftingFixture(
            MessageContracts.Crafting.Craft,
            client,
            Direction.Out,
            client is ClientType.Flash
                ? "0102030400027231"
                : "010203040506070800027231");
        Assert.Equal("r1", craft.RecipeCode);

        string ingredient_request_hex = client is ClientType.Flash
            ? "01020304000000021112131421222324"
            : "0102030405060708000211121314151617182122232425262728";
        CraftSecret secret = AssertCraftingFixture(
            MessageContracts.Crafting.SecretCraft,
            client,
            Direction.Out,
            ingredient_request_hex);
        GetCraftingRecipesAvailable availability_request = AssertCraftingFixture(
            MessageContracts.Crafting.AvailabilityRequest,
            client,
            Direction.Out,
            ingredient_request_hex);
        Assert.Equal(2, secret.IngredientItemIds.Count);
        Assert.Equal(secret.CraftingFurnitureId, availability_request.CraftingFurnitureId);
        Assert.Equal(secret.IngredientItemIds, availability_request.IngredientItemIds);
    }

    [Fact]
    public void crafting_unity_product_layouts_and_empty_snapshot_are_exact()
    {
        CraftableProducts legacy_products = AssertCraftingFixture(
            MessageContracts.Crafting.ProductsSnapshot,
            ClientType.Unity,
            Direction.In,
            "0001" + LegacyUnityCraftingProductHex + "0002" +
                CraftingFurnitureClassesHex);
        Assert.Null(Assert.Single(legacy_products.Products).ProductCode);

        foreach (bool success in new[] { false, true })
        {
            CraftingResult result = AssertCraftingFixture(
                MessageContracts.Crafting.Result,
                ClientType.Unity,
                Direction.In,
                (success ? "01" : "00") + LegacyUnityCraftingProductHex);
            Assert.Equal(success, result.Success);
            Assert.Null(result.Product?.ProductCode);
        }

        CraftableProducts empty = AssertCraftingFixture(
            MessageContracts.Crafting.ProductsSnapshot,
            ClientType.Unity,
            Direction.In,
            "00000000");
        Assert.Empty(empty.Products);
        Assert.Empty(empty.UsableInventoryFurnitureClasses);
    }

    [Fact]
    public void crafting_models_freeze_lists_and_preflight_atomically()
    {
        var mutable_products = new List<CraftingProduct>
        {
            new("r1", "p2", "chair")
        };
        var mutable_classes = new List<string> { "chair" };
        var products = new CraftableProducts(mutable_products, mutable_classes);
        mutable_products.Clear();
        mutable_classes.Clear();
        Assert.Single(products.Products);
        Assert.Equal(["chair"], products.UsableInventoryFurnitureClasses);

        var replacement_products = new List<CraftingProduct>
        {
            new("r2", "p3", "table")
        };
        CraftableProducts changed_products = products with
        {
            Products = replacement_products
        };
        replacement_products.Clear();
        Assert.Equal("r2", Assert.Single(changed_products.Products).RecipeCode);

        var mutable_ingredients = new List<CraftingIngredient>
        {
            new(2, "chair")
        };
        var recipe = new CraftingRecipe(mutable_ingredients);
        mutable_ingredients.Clear();
        Assert.Single(recipe.Ingredients);

        var mutable_ids = new List<Id> { 1, 2 };
        var secret = new CraftSecret(3, mutable_ids);
        var available = new GetCraftingRecipesAvailable(3, mutable_ids);
        mutable_ids.Clear();
        Assert.Equal(2, secret.IngredientItemIds.Count);
        Assert.Equal(2, available.IngredientItemIds.Count);

        var replacement_ids = new List<Id> { 4 };
        CraftSecret changed_secret = secret with
        {
            IngredientItemIds = replacement_ids
        };
        replacement_ids.Clear();
        Assert.Equal((Id)4, Assert.Single(changed_secret.IngredientItemIds));

        Assert.Throws<ArgumentNullException>(() => new CraftableProducts(null!, []));
        Assert.Throws<ArgumentNullException>(() => new CraftableProducts([], null!));
        Assert.Throws<ArgumentNullException>(() => new CraftableProducts([null!], []));
        Assert.Throws<ArgumentNullException>(() => new CraftableProducts([], [null!]));
        Assert.Throws<ArgumentNullException>(() => new CraftingRecipe(null!));
        Assert.Throws<ArgumentNullException>(() => new CraftingRecipe([null!]));
        Assert.Throws<ArgumentNullException>(() => new CraftSecret(1, null!));
        Assert.Throws<ArgumentNullException>(() => new GetCraftingRecipesAvailable(1, null!));
        Assert.Throws<InvalidDataException>(() =>
            new CraftableProducts(new CraftingProduct[ushort.MaxValue + 1], []));
        Assert.Throws<InvalidDataException>(() =>
            new CraftSecret(1, new Id[ushort.MaxValue + 1]));

        using (var product_packet = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity))
        {
            var invalid_product = new CraftingProduct(
                "valid",
                "valid",
                new string('x', ushort.MaxValue + 1));
            Assert.Throws<InvalidDataException>(() =>
                invalid_product.Compose(product_packet.Writer()));
            Assert.Equal(0, product_packet.Position);
            Assert.Equal(0, product_packet.Length);
        }

        using (var null_product_packet = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity))
        {
            var invalid_product = new CraftingProduct(null!, "valid", "valid");
            Assert.Throws<ArgumentNullException>(() =>
                invalid_product.Compose(null_product_packet.Writer()));
            Assert.Equal(0, null_product_packet.Position);
            Assert.Equal(0, null_product_packet.Length);
        }

        using (var ingredient_packet = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash))
        {
            var invalid_ingredient = new CraftingIngredient(
                1,
                new string('x', ushort.MaxValue + 1));
            Assert.Throws<InvalidDataException>(() =>
                invalid_ingredient.Compose(ingredient_packet.Writer()));
            Assert.Equal(0, ingredient_packet.Position);
            Assert.Equal(0, ingredient_packet.Length);
        }

        AssertCraftingComposeFails<CraftableProducts, InvalidDataException>(
            MessageContracts.Crafting.ProductsSnapshot,
            new CraftableProducts(
                [new CraftingProduct("r", null, "chair")],
                []),
            ClientType.Flash,
            Direction.In);
        AssertCraftingComposeFails<CraftableProducts, InvalidDataException>(
            MessageContracts.Crafting.ProductsSnapshot,
            new CraftableProducts(
            [
                new CraftingProduct("r", null, "chair"),
                new CraftingProduct("r", "p", "chair")
            ],
            []),
            ClientType.Unity,
            Direction.In);
        AssertCraftingComposeFails<CraftingResult, InvalidDataException>(
            MessageContracts.Crafting.Result,
            new CraftingResult(true, null),
            ClientType.Flash,
            Direction.In);
        AssertCraftingComposeFails<CraftingResult, InvalidDataException>(
            MessageContracts.Crafting.Result,
            new CraftingResult(false, new CraftingProduct("r", "p", "chair")),
            ClientType.Flash,
            Direction.In);
        AssertCraftingComposeFails<CraftingResult, InvalidDataException>(
            MessageContracts.Crafting.Result,
            new CraftingResult(false, null),
            ClientType.Unity,
            Direction.In);

        string oversized = new('x', ushort.MaxValue + 1);
        AssertCraftingComposeFails<GetCraftingRecipe, ArgumentNullException>(
            MessageContracts.Crafting.RecipeRequest,
            new GetCraftingRecipe(null!),
            ClientType.Unity,
            Direction.Out);
        AssertCraftingComposeFails<Qx.Model.Messages.Incoming.Craft, InvalidDataException>(
            MessageContracts.Crafting.Craft,
            new Qx.Model.Messages.Incoming.Craft(1, oversized),
            ClientType.Flash,
            Direction.Out);
        AssertCraftingComposeFails<Qx.Model.Messages.Incoming.Craft, OverflowException>(
            MessageContracts.Crafting.Craft,
            new Qx.Model.Messages.Incoming.Craft(long.MaxValue, "r"),
            ClientType.Flash,
            Direction.Out);
        AssertCraftingComposeFails<CraftSecret, OverflowException>(
            MessageContracts.Crafting.SecretCraft,
            new CraftSecret(1, [long.MaxValue]),
            ClientType.Flash,
            Direction.Out);
        AssertCraftingComposeFails<GetCraftingRecipesAvailable, OverflowException>(
            MessageContracts.Crafting.AvailabilityRequest,
            new GetCraftingRecipesAvailable(long.MaxValue, []),
            ClientType.Flash,
            Direction.Out);

        var repeated_product = new CraftingProduct("", "", "");
        AssertCraftingComposeFails<CraftableProducts, InvalidDataException>(
            MessageContracts.Crafting.ProductsSnapshot,
            new CraftableProducts(
                Enumerable.Repeat(repeated_product, ushort.MaxValue).ToArray(),
                ["", "", "", ""]),
            ClientType.Unity,
            Direction.In);

        string maximum_string = new('x', ushort.MaxValue);
        var byte_heavy_product = new CraftingProduct(
            maximum_string,
            maximum_string,
            maximum_string);
        AssertCraftingComposeFails<CraftableProducts, InvalidDataException>(
            MessageContracts.Crafting.ProductsSnapshot,
            new CraftableProducts(
                Enumerable.Repeat(byte_heavy_product, 86).ToArray(),
                []),
            ClientType.Unity,
            Direction.In);
    }

    [Fact]
    public void crafting_parsers_reject_invalid_counts_layouts_and_truncation()
    {
        AssertCraftingParseFails(
            MessageContracts.Crafting.ProductsSnapshot,
            ClientType.Flash,
            Direction.In,
            "FFFFFFFF00000000");
        AssertCraftingParseFails(
            MessageContracts.Crafting.ProductsSnapshot,
            ClientType.Flash,
            Direction.In,
            "0001000000000000");
        AssertCraftingParseFails(
            MessageContracts.Crafting.ProductsSnapshot,
            ClientType.Flash,
            Direction.In,
            "00000000FFFFFFFF");
        AssertCraftingParseFails(
            MessageContracts.Crafting.ProductsSnapshot,
            ClientType.Unity,
            Direction.In,
            "000100027231");
        AssertCraftingParseFails(
            MessageContracts.Crafting.ProductsSnapshot,
            ClientType.Unity,
            Direction.In,
            "000100000000000200000000");
        AssertCraftingParseFails(
            MessageContracts.Crafting.RecipeSnapshot,
            ClientType.Flash,
            Direction.In,
            "00000001");
        AssertCraftingParseFails(
            MessageContracts.Crafting.RecipeSnapshot,
            ClientType.Unity,
            Direction.In,
            "0001");
        AssertCraftingParseFails(
            MessageContracts.Crafting.Result,
            ClientType.Flash,
            Direction.In,
            "01");
        AssertCraftingParseFails(
            MessageContracts.Crafting.Result,
            ClientType.Unity,
            Direction.In,
            "01");
        AssertCraftingParseFails(
            MessageContracts.Crafting.AvailabilitySnapshot,
            ClientType.Flash,
            Direction.In,
            "00000004");
        AssertCraftingParseFails(
            MessageContracts.Crafting.ProductsRequest,
            ClientType.Unity,
            Direction.Out,
            "00000000");
        AssertCraftingParseFails(
            MessageContracts.Crafting.RecipeRequest,
            ClientType.Flash,
            Direction.Out,
            "000261");
        AssertCraftingParseFails(
            MessageContracts.Crafting.Craft,
            ClientType.Flash,
            Direction.Out,
            "01020304000261");
        AssertCraftingParseFails(
            MessageContracts.Crafting.SecretCraft,
            ClientType.Flash,
            Direction.Out,
            "0000000100000001000000");
        AssertCraftingParseFails(
            MessageContracts.Crafting.AvailabilityRequest,
            ClientType.Unity,
            Direction.Out,
            "0000000000000001000100000000000000");
    }

    [Fact]
    public void crafting_wire_rejects_unsupported_clients_before_io()
    {
        using var incoming = new Packet(
            new Header(Direction.In, 91),
            ClientType.None,
            new PacketBuffer(Convert.FromHexString("0000000401")));
        Assert.Throws<UnsupportedClientException>(() =>
            CraftingRecipesAvailable.Parse(incoming.Reader()));
        Assert.Equal(0, incoming.Position);

        using var outgoing = new Packet(
            new Header(Direction.Out, 91),
            ClientType.None);
        Assert.Throws<UnsupportedClientException>(() =>
            new GetCraftableProducts(1).Compose(outgoing.Writer()));
        Assert.Equal(0, outgoing.Position);
        Assert.Equal(0, outgoing.Length);
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void achievement_badge_wire_roundtrips_every_supported_route(ClientType client)
    {
        const string achievement_hex =
            "0000002A00000003000E4143485F526F6F6D456E74727933" +
            "00000064000000FA0000000F00000000000000B400" +
            "00076578706C6F7265000373756200000007000000000000";
        string count = client is ClientType.Flash ? "00000001" : "0001";

        _ = AssertAchievementBadgeFixture(
            MessageContracts.Achievements.Request,
            client,
            Direction.Out,
            "");
        Qx.Model.Messages.Incoming.Achievements achievements =
            AssertAchievementBadgeFixture(
                MessageContracts.Achievements.Snapshot,
                client,
                Direction.In,
                count + achievement_hex + "00076578706C6F7265");
        Assert.Equal("explore", achievements.DefaultCategory);
        Assert.Equal("ACH_RoomEntry3", Assert.Single(achievements.Items).BadgeCode);

        AchievementUpdate update = AssertAchievementBadgeFixture(
            MessageContracts.Achievements.Updated,
            client,
            Direction.In,
            achievement_hex);
        Assert.Equal(42, update.Achievement.Id);

        _ = AssertAchievementBadgeFixture(
            MessageContracts.Achievements.PointLimitsRequest,
            client,
            Direction.Out,
            "");
        BadgePointLimits limits = AssertAchievementBadgeFixture(
            MessageContracts.Achievements.PointLimits,
            client,
            Direction.In,
            client is ClientType.Flash
                ? "000000010009526F6F6D456E747279000000010000000200000032"
                : "00010009526F6F6D456E74727900010000000200000032");
        Assert.Equal(50, limits.Limit("RoomEntry", 2));

        AchievementNotification notification = AssertAchievementBadgeFixture(
            MessageContracts.Achievements.Notification,
            client,
            Direction.In,
            "00000001000000020000000300094143485F5465737432" +
            "0000000400000005000000060000000700000008" +
            "00094143485F5465737431000567616D657301" +
            (client is ClientType.Flash ? "000000090000000A" : ""));
        Assert.Equal(client is ClientType.Flash ? 9 : 0, notification.OwnerCount);
        Assert.Equal(client is ClientType.Flash ? 10 : 0, notification.BadgeRarityId);

        _ = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Request,
            client,
            Direction.Out,
            "");
        BadgeInventory inventory = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Snapshot,
            client,
            Direction.In,
            client is ClientType.Flash
                ? "00000002000000010000000100000065000341444D0000000C00000002"
                : "0000000200000001000100000065000341444D");
        Assert.Equal("ADM", Assert.Single(inventory.Badges).Code);

        SelectedBadgesRequest selected_request = AssertAchievementBadgeFixture(
            MessageContracts.Badges.SelectedRequest,
            client,
            Direction.Out,
            client is ClientType.Flash
                ? "01020304"
                : "0102030405060708");
        Assert.Equal(
            client is ClientType.Flash ? 0x01020304L : 0x0102030405060708L,
            (long)selected_request.UserId);

        BadgeReceived received = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Received,
            client,
            Direction.In,
            client is ClientType.Flash
                ? "00000065000341444D0000000C00000002"
                : "0102030405060708000341444D0000000C00000002");
        Assert.True(received.HasRarityData);

        UserBadges selected = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Selected,
            client,
            Direction.In,
            client is ClientType.Flash
                ? "0102030400000001000000030003564950000001F400000004"
                : "01020304050607080001000000030003564950");
        Assert.Equal(3, Assert.Single(selected.Badges).Slot);
    }

    [Fact]
    public void achievement_score_is_strictly_flash_only()
    {
        AchievementScore score = AssertAchievementBadgeFixture(
            MessageContracts.Achievements.Score,
            ClientType.Flash,
            Direction.In,
            "00001075");
        Assert.Equal(4213, score.Score);
        Assert.True(MessageContracts.Achievements.Score.Supports(ClientType.Flash));
        Assert.False(MessageContracts.Achievements.Score.Supports(ClientType.Unity));

        using var incoming = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity,
            new PacketBuffer(Convert.FromHexString("00001075")));
        Assert.Throws<UnsupportedClientException>(() =>
            MessageContracts.Achievements.Score.Parse(incoming.Reader()));
        Assert.Equal(0, incoming.Position);

        using var composed = new Packet(new Header(Direction.In, 91), ClientType.Unity);
        Assert.Throws<UnsupportedClientException>(() =>
            score.Compose(composed.Writer()));
        Assert.Equal(0, composed.Position);
        Assert.Equal(0, composed.Length);
    }

    [Fact]
    public void badge_fragment_generations_are_local_and_exact()
    {
        BadgeInventory compact_inventory = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Snapshot,
            ClientType.Flash,
            Direction.In,
            "000000010000000000000002000000010001410000000200024242");
        BadgeInventory expanded_inventory = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Snapshot,
            ClientType.Flash,
            Direction.In,
            "00000002000000010000000100000065000341444D0000000C00000002");
        Assert.All(compact_inventory.Badges, badge => Assert.False(badge.HasRarityData));
        Assert.True(Assert.Single(expanded_inventory.Badges).HasRarityData);

        BadgeInventory empty_flash = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Snapshot,
            ClientType.Flash,
            Direction.In,
            "000000010000000000000000");
        BadgeInventory empty_unity = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Snapshot,
            ClientType.Unity,
            Direction.In,
            "00000001000000000000");
        Assert.Empty(empty_flash.Badges);
        Assert.Empty(empty_unity.Badges);

        UserBadges compact_selected = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Selected,
            ClientType.Flash,
            Direction.In,
            "0102030400000001000000030003564950");
        UserBadges expanded_selected = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Selected,
            ClientType.Flash,
            Direction.In,
            "0102030400000001000000030003564950000001F400000004");
        Assert.False(Assert.Single(compact_selected.Badges).HasRarityData);
        Assert.True(Assert.Single(expanded_selected.Badges).HasRarityData);

        BadgeReceived legacy_flash = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Received,
            ClientType.Flash,
            Direction.In,
            "00000065000341444D");
        BadgeReceived current_flash = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Received,
            ClientType.Flash,
            Direction.In,
            "00000065000341444D0000000C00000002");
        BadgeReceived legacy_unity = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Received,
            ClientType.Unity,
            Direction.In,
            "0102030405060708000341444D");
        BadgeReceived current_unity = AssertAchievementBadgeFixture(
            MessageContracts.Badges.Received,
            ClientType.Unity,
            Direction.In,
            "0102030405060708000341444D0000000C00000002");
        Assert.False(legacy_flash.HasRarityData);
        Assert.True(current_flash.HasRarityData);
        Assert.False(legacy_unity.HasRarityData);
        Assert.True(current_unity.HasRarityData);
    }

    [Fact]
    public void achievement_badge_models_freeze_with_and_preflight_atomically()
    {
        var achievement = new Achievement
        {
            BadgeCode = "ACH_Test1",
            Category = "games",
            Subcategory = "daily"
        };
        var mutable_achievements = new List<Achievement> { achievement };
        var achievements = new Qx.Model.Messages.Incoming.Achievements(
            mutable_achievements,
            "games");
        mutable_achievements.Clear();
        Assert.Single(achievements.Items);

        var replacement_achievements = new List<Achievement>
        {
            new()
            {
                BadgeCode = "ACH_Test2",
                Category = "games",
                Subcategory = "weekly"
            }
        };
        Qx.Model.Messages.Incoming.Achievements changed_achievements = achievements with
        {
            Items = replacement_achievements
        };
        replacement_achievements.Clear();
        Assert.Equal("ACH_Test2", Assert.Single(changed_achievements.Items).BadgeCode);

        var mutable_limits = new List<BadgePointLimit>
        {
            new("Test", 1, 10)
        };
        var limits = new BadgePointLimits(mutable_limits);
        mutable_limits.Clear();
        Assert.Single(limits.Limits);
        var replacement_limits = new List<BadgePointLimit>
        {
            new("Test", 2, 20)
        };
        BadgePointLimits changed_limits = limits with { Limits = replacement_limits };
        replacement_limits.Clear();
        Assert.Equal(2, Assert.Single(changed_limits.Limits).Level);

        var mutable_owned = new List<OwnedBadge>
        {
            new(1, "A", 0, 0, false)
        };
        var inventory = new BadgeInventory(1, 0, mutable_owned);
        mutable_owned.Clear();
        Assert.Single(inventory.Badges);
        var replacement_owned = new List<OwnedBadge>
        {
            new(2, "B", 0, 0, false)
        };
        BadgeInventory changed_inventory = inventory with { Badges = replacement_owned };
        replacement_owned.Clear();
        Assert.Equal("B", Assert.Single(changed_inventory.Badges).Code);

        var mutable_selected = new List<SelectedBadge>
        {
            new(1, "A", 0, 0, false)
        };
        var selected = new UserBadges(1, mutable_selected);
        mutable_selected.Clear();
        Assert.Single(selected.Badges);
        var replacement_selected = new List<SelectedBadge>
        {
            new(2, "B", 0, 0, false)
        };
        UserBadges changed_selected = selected with { Badges = replacement_selected };
        replacement_selected.Clear();
        Assert.Equal("B", Assert.Single(changed_selected.Badges).Code);

        Assert.Throws<ArgumentNullException>(() =>
            new Qx.Model.Messages.Incoming.Achievements(null!, ""));
        Assert.Throws<ArgumentNullException>(() =>
            new Qx.Model.Messages.Incoming.Achievements([null!], ""));
        Assert.Throws<ArgumentNullException>(() => new BadgePointLimits(null!));
        Assert.Throws<ArgumentNullException>(() => new BadgePointLimits([null!]));
        Assert.Throws<ArgumentNullException>(() => new BadgeInventory(0, 0, null!));
        Assert.Throws<ArgumentNullException>(() => new UserBadges(0, null!));
        Assert.Throws<InvalidDataException>(() =>
            new Qx.Model.Messages.Incoming.Achievements(
                new Achievement[ushort.MaxValue + 1],
                ""));
        Assert.Throws<InvalidDataException>(() =>
            new BadgeInventory(0, 0, new OwnedBadge[ushort.MaxValue + 1]));
        Assert.Throws<InvalidDataException>(() =>
            new UserBadges(0, new SelectedBadge[ushort.MaxValue + 1]));
        Assert.Throws<InvalidDataException>(() =>
            new BadgePointLimits(new BadgePointLimit[ushort.MaxValue + 1]));

        achievement.BadgeCode = null!;
        AssertAchievementBadgeComposeFails<
            Qx.Model.Messages.Incoming.Achievements,
            ArgumentNullException>(
            MessageContracts.Achievements.Snapshot,
            achievements,
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeComposeFails<AchievementNotification, InvalidDataException>(
            MessageContracts.Achievements.Notification,
            new AchievementNotification(
                1, 2, 3, "badge", 4, 5, 6, 7, 8, "old", "games", true, 9, 10),
            ClientType.Unity,
            Direction.In);
        AssertAchievementBadgeComposeFails<BadgeInventory, InvalidOperationException>(
            MessageContracts.Badges.Snapshot,
            new BadgeInventory(
                1,
                0,
                [
                    new OwnedBadge(1, "A", 0, 0, false),
                    new OwnedBadge(2, "B", 3, 4)
                ]),
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeComposeFails<BadgeInventory, InvalidDataException>(
            MessageContracts.Badges.Snapshot,
            new BadgeInventory(1, 0, [new OwnedBadge(1, "A", 3, 4)]),
            ClientType.Unity,
            Direction.In);
        AssertAchievementBadgeComposeFails<BadgeInventory, OverflowException>(
            MessageContracts.Badges.Snapshot,
            new BadgeInventory(
                1,
                0,
                [new OwnedBadge(long.MaxValue, "A", 0, 0, false)]),
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeComposeFails<BadgeReceived, InvalidOperationException>(
            MessageContracts.Badges.Received,
            new BadgeReceived(1, "A", 2, null),
            ClientType.Unity,
            Direction.In);
        AssertAchievementBadgeComposeFails<UserBadges, InvalidDataException>(
            MessageContracts.Badges.Selected,
            new UserBadges(
                1,
                [
                    new SelectedBadge(1, "A", 0, 0, false),
                    new SelectedBadge(2, "B", 3, 4)
                ]),
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeComposeFails<UserBadges, InvalidDataException>(
            MessageContracts.Badges.Selected,
            new UserBadges(1, [new SelectedBadge(1, "A", 3, 4)]),
            ClientType.Unity,
            Direction.In);
        AssertAchievementBadgeComposeFails<UserBadges, OverflowException>(
            MessageContracts.Badges.Selected,
            new UserBadges(long.MaxValue, []),
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeComposeFails<BadgePointLimits, ArgumentNullException>(
            MessageContracts.Achievements.PointLimits,
            new BadgePointLimits([new BadgePointLimit(null!, 1, 2)]),
            ClientType.Flash,
            Direction.In);

        string maximum_string = new('x', ushort.MaxValue);
        var byte_heavy_achievement = new Achievement
        {
            BadgeCode = maximum_string,
            Category = maximum_string,
            Subcategory = maximum_string
        };
        AssertAchievementBadgeComposeFails<
            Qx.Model.Messages.Incoming.Achievements,
            InvalidDataException>(
            MessageContracts.Achievements.Snapshot,
            new Qx.Model.Messages.Incoming.Achievements(
                Enumerable.Repeat(byte_heavy_achievement, 86).ToArray(),
                ""),
            ClientType.Unity,
            Direction.In);
    }

    [Fact]
    public void achievement_badge_parsers_reject_counts_layouts_and_truncation()
    {
        AssertAchievementBadgeParseFails(
            MessageContracts.Achievements.Snapshot,
            ClientType.Flash,
            Direction.In,
            "FFFFFFFF0000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Achievements.Snapshot,
            ClientType.Flash,
            Direction.In,
            "000000010000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Achievements.PointLimits,
            ClientType.Flash,
            Direction.In,
            "FFFFFFFF");
        AssertAchievementBadgeParseFails(
            MessageContracts.Achievements.PointLimits,
            ClientType.Unity,
            Direction.In,
            "00010001410001");
        AssertAchievementBadgeParseFails(
            MessageContracts.Badges.Snapshot,
            ClientType.Flash,
            Direction.In,
            "000000010000000000010000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Badges.Selected,
            ClientType.Flash,
            Direction.In,
            "00000001FFFFFFFF");
        AssertAchievementBadgeParseFails(
            MessageContracts.Badges.Received,
            ClientType.Unity,
            Direction.In,
            "010203040506070800014100000001");
        AssertAchievementBadgeParseFails(
            MessageContracts.Achievements.Notification,
            ClientType.Unity,
            Direction.In,
            "000000010000000200000003000141000000040000000500000006" +
            "000000070000000800014200014301000000090000000A");
        AssertAchievementBadgeParseFails(
            MessageContracts.Achievements.Score,
            ClientType.Flash,
            Direction.In,
            "000001");

        AssertAchievementBadgeParseFails(
            MessageContracts.Badges.Snapshot,
            ClientType.Flash,
            Direction.In,
            "000000010000000000000002" +
            "0000000100000000000200104142" +
            "0000000300000000000400000005");
        AssertAchievementBadgeParseFails(
            MessageContracts.Badges.Selected,
            ClientType.Flash,
            Direction.In,
            "0000000100000002" +
            "0000000100000000000200104142" +
            "0000000300000000000400000005");

        using var achievement = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash,
            new PacketBuffer(Convert.FromHexString(
                "0000000000000000000000000000000000000000000000000000000000" +
                "000000000000000000000000000000007F")));
        Assert.Throws<InvalidDataException>(() => Achievement.Parse(achievement.Reader()));

        using var owned_badge = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity,
            new PacketBuffer(Convert.FromHexString("000000010001417F")));
        Assert.Throws<InvalidDataException>(() => OwnedBadge.Parse(owned_badge.Reader()));
    }

    [Fact]
    public void achievement_badge_wire_rejects_unsupported_clients_before_io()
    {
        using var incoming = new Packet(
            new Header(Direction.In, 91),
            ClientType.None,
            new PacketBuffer(Convert.FromHexString("00000000")));
        Assert.Throws<UnsupportedClientException>(() => AchievementScore.Parse(incoming.Reader()));
        Assert.Equal(0, incoming.Position);

        using var outgoing = new Packet(
            new Header(Direction.Out, 91),
            ClientType.None);
        Assert.Throws<UnsupportedClientException>(() =>
            new BadgeInventoryRequest().Compose(outgoing.Writer()));
        Assert.Equal(0, outgoing.Position);
        Assert.Equal(0, outgoing.Length);
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void earning_wire_roundtrips_every_supported_route(ClientType client)
    {
        string count = client is ClientType.Flash ? "00000002" : "0002";
        string empty_count = client is ClientType.Flash ? "00000000" : "0000";

        _ = AssertAchievementBadgeFixture(
            MessageContracts.Earnings.StatusRequest,
            client,
            Direction.Out,
            "");
        EarningStatus status = AssertAchievementBadgeFixture(
            MessageContracts.Earnings.StatusSnapshot,
            client,
            Direction.In,
            count + "01000000002800000B01000000FA00067468726F6E65");
        Assert.Equal(
            [EarningCategory.DailyGift, EarningCategory.Games],
            status.Categories);
        Assert.Equal(40, status.Duckets(EarningCategory.DailyGift));
        Assert.Equal(250, status.Credits(EarningCategory.Games));
        Assert.Equal(1, status.Products(EarningCategory.Games));
        Assert.Single(status.For(EarningCategory.DailyGift));
        Assert.False(status.HasClaimable(EarningCategory.DailyGift));
        Assert.True(status.HasClaimable(EarningCategory.Games));

        EarningStatus empty = AssertAchievementBadgeFixture(
            MessageContracts.Earnings.StatusSnapshot,
            client,
            Direction.In,
            empty_count);
        Assert.Empty(empty.Entries);

        EarningClaimRequest request = AssertAchievementBadgeFixture(
            MessageContracts.Earnings.Claim,
            client,
            Direction.Out,
            "FF");
        Assert.Equal(EarningCategory.All, request.Category);

        EarningClaimRequest unknown_request = AssertAchievementBadgeFixture(
            MessageContracts.Earnings.Claim,
            client,
            Direction.Out,
            "80");
        Assert.Equal(-128, (int)unknown_request.Category);

        EarningClaimResult result = AssertAchievementBadgeFixture(
            MessageContracts.Earnings.Claimed,
            client,
            Direction.In,
            "FF01");
        Assert.True(result.IsClaimAll);
        Assert.True(result.Success);

        EarningClaimResult unknown_result = AssertAchievementBadgeFixture(
            MessageContracts.Earnings.Claimed,
            client,
            Direction.In,
            "FE00");
        Assert.Equal(-2, (int)unknown_result.Category);
        Assert.False(unknown_result.Success);
    }

    [Fact]
    public void earning_notification_is_strictly_flash_only()
    {
        EarningNotification notification = AssertAchievementBadgeFixture(
            MessageContracts.Earnings.Notification,
            ClientType.Flash,
            Direction.In,
            "0A");
        Assert.Equal(EarningCategory.Snowstorm, notification.Category);
        EarningNotification unknown_notification = AssertAchievementBadgeFixture(
            MessageContracts.Earnings.Notification,
            ClientType.Flash,
            Direction.In,
            "80");
        Assert.Equal(-128, (int)unknown_notification.Category);
        Assert.True(MessageContracts.Earnings.Notification.Supports(ClientType.Flash));
        Assert.False(MessageContracts.Earnings.Notification.Supports(ClientType.Unity));

        using var incoming = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity,
            new PacketBuffer([0x0A]));
        Assert.Throws<UnsupportedClientException>(() =>
            EarningNotification.Parse(incoming.Reader()));
        Assert.Equal(0, incoming.Position);

        using var outgoing = new Packet(new Header(Direction.In, 91), ClientType.Unity);
        Assert.Throws<UnsupportedClientException>(() =>
            notification.Compose(outgoing.Writer()));
        Assert.Equal(0, outgoing.Position);
        Assert.Equal(0, outgoing.Length);
    }

    [Fact]
    public void earning_models_freeze_with_and_preflight_atomically()
    {
        var entry = new EarningEntry(
            Category: EarningCategory.Games,
            Kind: EarningRewardKind.Credits,
            Amount: 25,
            ProductCode: "A");
        var mutable_entries = new List<EarningEntry> { entry };
        var status = new EarningStatus(mutable_entries);
        mutable_entries.Clear();
        Assert.Single(status.Entries);

        var replacement_entries = new List<EarningEntry>
        {
            new(EarningCategory.DailyGift, EarningRewardKind.Duckets, 40, "B")
        };
        EarningStatus changed_status = status with { Entries = replacement_entries };
        replacement_entries.Clear();
        Assert.Equal("B", Assert.Single(changed_status.Entries).ProductCode);

        EarningEntry changed_entry = entry with { ProductCode = "C" };
        var (category, kind, amount, product_code) = changed_entry;
        Assert.Equal(EarningCategory.Games, category);
        Assert.Equal(EarningRewardKind.Credits, kind);
        Assert.Equal(25, amount);
        Assert.Equal("C", product_code);
        changed_status.Deconstruct(out IReadOnlyList<EarningEntry> deconstructed_entries);
        Assert.Single(deconstructed_entries);

        Assert.Throws<ArgumentNullException>(() => new EarningStatus(null!));
        Assert.Throws<ArgumentNullException>(() => new EarningStatus([null!]));
        Assert.Throws<InvalidDataException>(() =>
            new EarningStatus(new EarningEntry[ushort.MaxValue + 1]));
        Assert.Throws<ArgumentNullException>(() =>
            new EarningEntry(
                EarningCategory.Games,
                EarningRewardKind.Credits,
                1,
                null!));
        Assert.Throws<ArgumentNullException>(() => entry with { ProductCode = null! });

        string maximum_string = new('x', ushort.MaxValue);
        using var maximum_packet = new Packet(new Header(Direction.In, 91), ClientType.Flash);
        new EarningEntry(
            EarningCategory.Games,
            EarningRewardKind.Credits,
            1,
            maximum_string).Compose(maximum_packet.Writer());
        Assert.Equal(sizeof(byte) * 2 + sizeof(int) + sizeof(short) + ushort.MaxValue,
            maximum_packet.Length);

        AssertAchievementBadgeComposeFails<EarningStatus, InvalidDataException>(
            MessageContracts.Earnings.StatusSnapshot,
            new EarningStatus(
                [
                    new EarningEntry(
                        EarningCategory.Games,
                        EarningRewardKind.Credits,
                        1,
                        new string('x', ushort.MaxValue + 1))
                ]),
            ClientType.Flash,
            Direction.In);

        var byte_heavy_entry = new EarningEntry(
            EarningCategory.Games,
            EarningRewardKind.Credits,
            1,
            maximum_string);
        AssertAchievementBadgeComposeFails<EarningStatus, InvalidDataException>(
            MessageContracts.Earnings.StatusSnapshot,
            new EarningStatus(Enumerable.Repeat(byte_heavy_entry, 257).ToArray()),
            ClientType.Unity,
            Direction.In);

        var empty_entry = new EarningEntry(
            EarningCategory.Games,
            EarningRewardKind.Credits,
            1,
            "");
        var maximum_status = new EarningStatus(
            Enumerable.Repeat(empty_entry, ushort.MaxValue).ToArray());
        using var maximum_status_packet = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity);
        MessageContracts.Earnings.StatusSnapshot.Compose(
            maximum_status,
            maximum_status_packet.Writer());
        Assert.Equal(sizeof(short) + ushort.MaxValue * 8, maximum_status_packet.Length);

        AssertAchievementBadgeComposeFails<EarningStatusRequest, ArgumentNullException>(
            MessageContracts.Earnings.StatusRequest,
            null!,
            ClientType.Flash,
            Direction.Out);
    }

    [Fact]
    public void earning_public_abi_remains_positional_and_exact()
    {
        Assert.Equal(
            ["Tutorial", "DailyGift", "Achievements", "Marketplace", "HabboClub",
                "LevelProgression", "RoomBundleSales", "BonusBag", "Donation", "Surprise",
                "Snowstorm", "Games", "WiredChest", "Agency", "All"],
            Enum.GetNames<EarningCategory>());
        Assert.Equal(
            [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, -1],
            Enum.GetValues<EarningCategory>().Select(value => (int)value).ToArray());
        Assert.Equal(["Duckets", "Credits"], Enum.GetNames<EarningRewardKind>());
        Assert.Equal(
            [0, 1],
            Enum.GetValues<EarningRewardKind>().Select(value => (int)value).ToArray());

        AssertEarningConstructor(
            typeof(EarningEntry),
            (typeof(EarningCategory), "Category"),
            (typeof(EarningRewardKind), "Kind"),
            (typeof(int), "Amount"),
            (typeof(string), "ProductCode"));
        AssertEarningProperties(
            typeof(EarningEntry),
            (nameof(EarningEntry.Category), typeof(EarningCategory), true),
            (nameof(EarningEntry.Kind), typeof(EarningRewardKind), true),
            (nameof(EarningEntry.Amount), typeof(int), true),
            (nameof(EarningEntry.ProductCode), typeof(string), true),
            (nameof(EarningEntry.IsProduct), typeof(bool), false));
        AssertEarningDeconstruct(
            typeof(EarningEntry),
            (typeof(EarningCategory), "Category"),
            (typeof(EarningRewardKind), "Kind"),
            (typeof(int), "Amount"),
            (typeof(string), "ProductCode"));

        AssertEarningConstructor(
            typeof(EarningStatus),
            (typeof(IReadOnlyList<EarningEntry>), "Entries"));
        AssertEarningProperties(
            typeof(EarningStatus),
            (nameof(EarningStatus.Entries), typeof(IReadOnlyList<EarningEntry>), true),
            (nameof(EarningStatus.Categories), typeof(IReadOnlyList<EarningCategory>), false));
        AssertEarningDeconstruct(
            typeof(EarningStatus),
            (typeof(IReadOnlyList<EarningEntry>), "Entries"));
        AssertEarningCategoryMethod(nameof(EarningStatus.Credits), typeof(int), true);
        AssertEarningCategoryMethod(nameof(EarningStatus.Duckets), typeof(int), true);
        AssertEarningCategoryMethod(nameof(EarningStatus.Products), typeof(int), true);
        AssertEarningCategoryMethod(nameof(EarningStatus.For), typeof(IReadOnlyList<EarningEntry>), false);
        AssertEarningCategoryMethod(nameof(EarningStatus.HasClaimable), typeof(bool), true);

        AssertEarningConstructor(
            typeof(EarningClaimResult),
            (typeof(EarningCategory), "Category"),
            (typeof(bool), "Success"));
        AssertEarningProperties(
            typeof(EarningClaimResult),
            (nameof(EarningClaimResult.Category), typeof(EarningCategory), true),
            (nameof(EarningClaimResult.Success), typeof(bool), true),
            (nameof(EarningClaimResult.IsClaimAll), typeof(bool), false));
        AssertEarningDeconstruct(
            typeof(EarningClaimResult),
            (typeof(EarningCategory), "Category"),
            (typeof(bool), "Success"));

        AssertEarningConstructor(
            typeof(EarningNotification),
            (typeof(EarningCategory), "Category"));
        AssertEarningProperties(
            typeof(EarningNotification),
            (nameof(EarningNotification.Category), typeof(EarningCategory), true));
        AssertEarningDeconstruct(
            typeof(EarningNotification),
            (typeof(EarningCategory), "Category"));

        AssertEarningConstructor(typeof(EarningStatusRequest));
        AssertEarningProperties(typeof(EarningStatusRequest));
        AssertEarningConstructor(
            typeof(EarningClaimRequest),
            (typeof(EarningCategory), "Category"));
        AssertEarningProperties(
            typeof(EarningClaimRequest),
            (nameof(EarningClaimRequest.Category), typeof(EarningCategory), true));
        AssertEarningDeconstruct(
            typeof(EarningClaimRequest),
            (typeof(EarningCategory), "Category"));
    }

    [Fact]
    public void earning_parsers_reject_counts_capacity_and_truncation()
    {
        AssertAchievementBadgeParseFails(
            MessageContracts.Earnings.StatusSnapshot,
            ClientType.Flash,
            Direction.In,
            "FFFFFFFF");
        AssertAchievementBadgeParseFails(
            MessageContracts.Earnings.StatusSnapshot,
            ClientType.Flash,
            Direction.In,
            "00010000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Earnings.StatusSnapshot,
            ClientType.Flash,
            Direction.In,
            "00000001");
        AssertAchievementBadgeParseFails(
            MessageContracts.Earnings.StatusSnapshot,
            ClientType.Unity,
            Direction.In,
            "0001");
        AssertAchievementBadgeParseFails(
            MessageContracts.Earnings.StatusSnapshot,
            ClientType.Flash,
            Direction.In,
            "00000001010000000001000241");
        AssertAchievementBadgeParseFails(
            MessageContracts.Earnings.Claim,
            ClientType.Unity,
            Direction.Out,
            "");
        AssertAchievementBadgeParseFails(
            MessageContracts.Earnings.Claimed,
            ClientType.Flash,
            Direction.In,
            "FF");
        AssertAchievementBadgeParseFails(
            MessageContracts.Earnings.Notification,
            ClientType.Flash,
            Direction.In,
            "");

        byte[] raw_entry = Convert.FromHexString("FE7F80000000000141");
        foreach (ClientType client in new[] { ClientType.Flash, ClientType.Unity })
        {
            using var incoming = new Packet(
                new Header(Direction.In, 91),
                client,
                new PacketBuffer(raw_entry));
            EarningEntry parsed = EarningEntry.Parse(incoming.Reader());
            Assert.Equal(-2, (int)parsed.Category);
            Assert.Equal(127, (int)parsed.Kind);
            Assert.Equal(int.MinValue, parsed.Amount);
            Assert.Equal("A", parsed.ProductCode);
            Assert.Equal(0, incoming.Available);

            using var outgoing = new Packet(new Header(Direction.In, 91), client);
            parsed.Compose(outgoing.Writer());
            Assert.Equal(raw_entry, outgoing.Buffer.Span.ToArray());

            using var trailing = new Packet(
                new Header(Direction.In, 91),
                client,
                new PacketBuffer([.. raw_entry, 0x7F]));
            Assert.Throws<InvalidDataException>(() => EarningEntry.Parse(trailing.Reader()));
        }
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void earning_status_parser_preserves_each_sibling_minimum(ClientType client)
    {
        string count = client is ClientType.Flash ? "00000002" : "0002";
        byte[] payload = Convert.FromHexString(
            count + "0100000000010008" + "0201000000020000");
        using var packet = new Packet(
            new Header(Direction.In, 91),
            client,
            new PacketBuffer(payload));

        Assert.Throws<InvalidDataException>(() =>
            MessageContracts.Earnings.StatusSnapshot.Parse(packet.Reader()));
        Assert.Equal((client is ClientType.Flash ? sizeof(int) : sizeof(short)) + 8,
            packet.Position);
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void earning_status_parser_enforces_the_root_string_budget(ClientType client)
    {
        string maximum_string = new('x', ushort.MaxValue);
        using var packet = new Packet(new Header(Direction.In, 91), client);
        {
            PacketWriter writer = packet.Writer();
            if (client is ClientType.Flash)
                writer.WriteInt(257);
            else
                writer.WriteShort(257);

            for (int index = 0; index < 256; index++)
            {
                writer.WriteByte(0);
                writer.WriteByte(0);
                writer.WriteInt(1);
                writer.WriteString(maximum_string);
            }

            writer.WriteByte(0);
            writer.WriteByte(0);
            writer.WriteInt(1);
            writer.WriteString(new string('x', 257));
        }
        packet.Position = 0;

        Assert.Throws<InvalidDataException>(() =>
            MessageContracts.Earnings.StatusSnapshot.Parse(packet.Reader()));
    }

    [Fact]
    public void earning_wire_rejects_unsupported_clients_before_io()
    {
        using var incoming = new Packet(
            new Header(Direction.In, 91),
            ClientType.None,
            new PacketBuffer(Convert.FromHexString("00000000")));
        Assert.Throws<UnsupportedClientException>(() => EarningStatus.Parse(incoming.Reader()));
        Assert.Equal(0, incoming.Position);

        using var outgoing = new Packet(new Header(Direction.Out, 91), ClientType.None);
        Assert.Throws<UnsupportedClientException>(() =>
            new EarningClaimRequest(EarningCategory.All).Compose(outgoing.Writer()));
        Assert.Equal(0, outgoing.Position);
        Assert.Equal(0, outgoing.Length);
    }

    [Fact]
    public void daily_task_wire_roundtrips_every_verified_route()
    {
        const string task =
            "0102030405060708" +
            "00047461736B" +
            "00057175657374" +
            "01" +
            "00027631" +
            "0000" +
            "00000005" +
            "00000003" +
            "01" +
            "0000003C" +
            "00000001" +
            "00070005626164676500054143485F3100000002";

        _ = AssertAchievementBadgeFixture(
            MessageContracts.DailyTasks.Request,
            ClientType.Flash,
            Direction.Out,
            "");
        DailyTasksActiveList snapshot = AssertAchievementBadgeFixture(
            MessageContracts.DailyTasks.Snapshot,
            ClientType.Flash,
            Direction.In,
            "00000001" + task);
        DailyTask parsed = Assert.Single(snapshot.Tasks);
        Assert.Equal(0x0102030405060708L, parsed.TaskId);
        Assert.Equal("task", parsed.TaskCode);
        Assert.Equal("quest", parsed.QuestTypeCode);
        Assert.True(parsed.IsBonus);
        Assert.Equal("v1", parsed.ImageVersion);
        Assert.Equal("", parsed.CatalogName);
        Assert.Equal(5, parsed.RequiredRepeats);
        Assert.Equal(3, parsed.Repeats);
        Assert.Equal(DailyTaskStatus.Completed, parsed.Status);
        Assert.Equal(60, parsed.SecondsLeftAtArrival);
        DailyTaskReward reward = Assert.Single(parsed.Rewards);
        Assert.Equal((short)7, reward.ProductItemTypeId);
        Assert.Equal("badge", reward.RewardTypeId);
        Assert.Equal("ACH_1", reward.ExtraParams);
        Assert.Equal(2, reward.Amount);

        DailyTasksTasksAdded added = AssertAchievementBadgeFixture(
            MessageContracts.DailyTasks.Added,
            ClientType.Flash,
            Direction.In,
            "00000001" + task);
        Assert.Equal(parsed.TaskId, Assert.Single(added.Tasks).TaskId);

        DailyTasksTaskUpdate updated = AssertAchievementBadgeFixture(
            MessageContracts.DailyTasks.Updated,
            ClientType.Flash,
            Direction.In,
            "01020304050607080000000402FFFFFFFF");
        Assert.Equal(DailyTaskStatus.Claimed, updated.Status);
        Assert.Equal(-1, updated.SecondsLeftAtArrival);

        DailyTaskClaimRequest claim = AssertAchievementBadgeFixture(
            MessageContracts.DailyTasks.Claim,
            ClientType.Flash,
            Direction.Out,
            "80000001");
        Assert.Equal(-2147483647L, claim.TaskId);

        using var narrowed = new Packet(new Header(Direction.Out, 91), ClientType.Flash);
        new DailyTaskClaimRequest(0x0000000180000001L).Compose(narrowed.Writer());
        Assert.Equal(Convert.FromHexString("80000001"), narrowed.Buffer.Span.ToArray());

        using var direct_task = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash,
            new PacketBuffer(Convert.FromHexString(task)));
        Assert.Equal(parsed.TaskId, DailyTask.Parse(direct_task.Reader()).TaskId);
        Assert.Equal(0, direct_task.Available);

        const string reward_hex = "00070005626164676500054143485F3100000002";
        using var direct_reward = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash,
            new PacketBuffer(Convert.FromHexString(reward_hex)));
        Assert.Equal(reward, DailyTaskReward.Parse(direct_reward.Reader()));
        Assert.Equal(0, direct_reward.Available);
    }

    [Fact]
    public void daily_task_models_freeze_with_and_preflight_atomically()
    {
        var reward = new DailyTaskReward(1, "badge", "ACH_1", 2);
        var mutable_rewards = new List<DailyTaskReward> { reward };
        DailyTask task = CreateDailyTask(mutable_rewards);
        mutable_rewards.Clear();
        Assert.Single(task.Rewards);

        var replacement_rewards = new List<DailyTaskReward>
        {
            new(2, "furni", "chair", 1)
        };
        DailyTask changed = task with { Rewards = replacement_rewards };
        replacement_rewards.Clear();
        Assert.Equal("furni", Assert.Single(changed.Rewards).RewardTypeId);

        var mutable_tasks = new List<DailyTask> { task };
        var snapshot = new DailyTasksActiveList(mutable_tasks);
        mutable_tasks.Clear();
        Assert.Single(snapshot.Tasks);
        var replacement_tasks = new List<DailyTask> { changed };
        DailyTasksActiveList changed_snapshot = snapshot with { Tasks = replacement_tasks };
        replacement_tasks.Clear();
        Assert.Equal("furni", Assert.Single(Assert.Single(changed_snapshot.Tasks).Rewards).RewardTypeId);

        Assert.Throws<ArgumentNullException>(() => new DailyTaskReward(1, null!, "", 1));
        Assert.Throws<ArgumentNullException>(() => reward with { ExtraParams = null! });
        Assert.Throws<ArgumentNullException>(() => CreateDailyTask(null!));
        Assert.Throws<ArgumentNullException>(() => CreateDailyTask([null!]));
        Assert.Throws<InvalidDataException>(() =>
            CreateDailyTask(new DailyTaskReward[ushort.MaxValue + 1]));
        Assert.Throws<ArgumentNullException>(() => new DailyTasksActiveList(null!));
        Assert.Throws<ArgumentNullException>(() => new DailyTasksTasksAdded([null!]));
        Assert.Throws<InvalidDataException>(() =>
            new DailyTasksActiveList(new DailyTask[ushort.MaxValue + 1]));

        DailyTask oversized_string = CreateDailyTask(
            [],
            task_code: new string('x', ushort.MaxValue + 1));
        AssertAchievementBadgeComposeFails<DailyTasksActiveList, InvalidDataException>(
            MessageContracts.DailyTasks.Snapshot,
            new DailyTasksActiveList([oversized_string]),
            ClientType.Flash,
            Direction.In);

        string maximum_string = new('x', ushort.MaxValue);
        var large_reward = new DailyTaskReward(1, maximum_string, maximum_string, 1);
        DailyTask byte_heavy = CreateDailyTask(Enumerable.Repeat(large_reward, 129).ToArray());
        AssertAchievementBadgeComposeFails<DailyTasksActiveList, InvalidDataException>(
            MessageContracts.DailyTasks.Snapshot,
            new DailyTasksActiveList([byte_heavy]),
            ClientType.Flash,
            Direction.In);

        AssertAchievementBadgeComposeFails<DailyTaskListRequest, ArgumentNullException>(
            MessageContracts.DailyTasks.Request,
            null!,
            ClientType.Flash,
            Direction.Out);
    }

    [Fact]
    public void daily_task_public_abi_remains_positional_and_exact()
    {
        Assert.Equal(
            ["InProgress", "Completed", "Claimed"],
            Enum.GetNames<DailyTaskStatus>());
        Assert.Equal(
            [0, 1, 2],
            Enum.GetValues<DailyTaskStatus>().Select(value => (int)value).ToArray());

        AssertEarningConstructor(
            typeof(DailyTaskReward),
            (typeof(short), "ProductItemTypeId"),
            (typeof(string), "RewardTypeId"),
            (typeof(string), "ExtraParams"),
            (typeof(int), "Amount"));
        AssertEarningProperties(
            typeof(DailyTaskReward),
            (nameof(DailyTaskReward.ProductItemTypeId), typeof(short), true),
            (nameof(DailyTaskReward.RewardTypeId), typeof(string), true),
            (nameof(DailyTaskReward.ExtraParams), typeof(string), true),
            (nameof(DailyTaskReward.Amount), typeof(int), true));
        AssertEarningDeconstruct(
            typeof(DailyTaskReward),
            (typeof(short), "ProductItemTypeId"),
            (typeof(string), "RewardTypeId"),
            (typeof(string), "ExtraParams"),
            (typeof(int), "Amount"));

        AssertEarningConstructor(
            typeof(DailyTask),
            (typeof(long), "TaskId"),
            (typeof(string), "TaskCode"),
            (typeof(string), "QuestTypeCode"),
            (typeof(bool), "IsBonus"),
            (typeof(string), "ImageVersion"),
            (typeof(string), "CatalogName"),
            (typeof(int), "RequiredRepeats"),
            (typeof(int), "Repeats"),
            (typeof(DailyTaskStatus), "Status"),
            (typeof(int), "SecondsLeftAtArrival"),
            (typeof(DateTimeOffset), "ReceivedAt"),
            (typeof(IReadOnlyList<DailyTaskReward>), "Rewards"));
        AssertEarningProperties(
            typeof(DailyTask),
            (nameof(DailyTask.TaskId), typeof(long), true),
            (nameof(DailyTask.TaskCode), typeof(string), true),
            (nameof(DailyTask.QuestTypeCode), typeof(string), true),
            (nameof(DailyTask.IsBonus), typeof(bool), true),
            (nameof(DailyTask.ImageVersion), typeof(string), true),
            (nameof(DailyTask.CatalogName), typeof(string), true),
            (nameof(DailyTask.RequiredRepeats), typeof(int), true),
            (nameof(DailyTask.Repeats), typeof(int), true),
            (nameof(DailyTask.Status), typeof(DailyTaskStatus), true),
            (nameof(DailyTask.SecondsLeftAtArrival), typeof(int), true),
            (nameof(DailyTask.ReceivedAt), typeof(DateTimeOffset), true),
            (nameof(DailyTask.Rewards), typeof(IReadOnlyList<DailyTaskReward>), true),
            (nameof(DailyTask.SecondsLeft), typeof(int), false),
            (nameof(DailyTask.IsExpired), typeof(bool), false),
            (nameof(DailyTask.IsClaimable), typeof(bool), false));
        AssertEarningDeconstruct(
            typeof(DailyTask),
            (typeof(long), "TaskId"),
            (typeof(string), "TaskCode"),
            (typeof(string), "QuestTypeCode"),
            (typeof(bool), "IsBonus"),
            (typeof(string), "ImageVersion"),
            (typeof(string), "CatalogName"),
            (typeof(int), "RequiredRepeats"),
            (typeof(int), "Repeats"),
            (typeof(DailyTaskStatus), "Status"),
            (typeof(int), "SecondsLeftAtArrival"),
            (typeof(DateTimeOffset), "ReceivedAt"),
            (typeof(IReadOnlyList<DailyTaskReward>), "Rewards"));

        AssertDailyTaskListAbi(typeof(DailyTasksActiveList));
        AssertDailyTaskListAbi(typeof(DailyTasksTasksAdded));
        AssertEarningConstructor(
            typeof(DailyTasksTaskUpdate),
            (typeof(long), "TaskId"),
            (typeof(int), "Repeats"),
            (typeof(DailyTaskStatus), "Status"),
            (typeof(int), "SecondsLeftAtArrival"));
        AssertEarningConstructor(typeof(DailyTaskListRequest));
        AssertEarningConstructor(
            typeof(DailyTaskClaimRequest),
            (typeof(long), "TaskId"));
    }

    [Fact]
    public void daily_task_parsers_reject_counts_capacity_truncation_and_tails()
    {
        AssertAchievementBadgeParseFails(
            MessageContracts.DailyTasks.Snapshot,
            ClientType.Flash,
            Direction.In,
            "FFFFFFFF");
        AssertAchievementBadgeParseFails(
            MessageContracts.DailyTasks.Snapshot,
            ClientType.Flash,
            Direction.In,
            "00010000");
        AssertAchievementBadgeParseFails(
            MessageContracts.DailyTasks.Snapshot,
            ClientType.Flash,
            Direction.In,
            "00000001");
        AssertAchievementBadgeParseFails(
            MessageContracts.DailyTasks.Updated,
            ClientType.Flash,
            Direction.In,
            "00000000000000000000000000");
        AssertAchievementBadgeParseFails(
            MessageContracts.DailyTasks.Claim,
            ClientType.Flash,
            Direction.Out,
            "0000");

        const string minimal_task =
            "0000000000000000" +
            "0000" +
            "0000" +
            "00" +
            "0000" +
            "0000" +
            "00000000" +
            "00000000" +
            "00" +
            "00000000" +
            "00000000";
        string stealing_task = "0000000000000000" + "0001" + minimal_task[20..];
        AssertAchievementBadgeParseFails(
            MessageContracts.DailyTasks.Snapshot,
            ClientType.Flash,
            Direction.In,
            "00000002" + stealing_task + minimal_task);

        using var direct_task = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash,
            new PacketBuffer(Convert.FromHexString(minimal_task + "7F")));
        Assert.Throws<InvalidDataException>(() => DailyTask.Parse(direct_task.Reader()));

        const string minimal_reward = "00000000000000000000";
        using var direct_reward = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash,
            new PacketBuffer(Convert.FromHexString(minimal_reward + "7F")));
        Assert.Throws<InvalidDataException>(() => DailyTaskReward.Parse(direct_reward.Reader()));
    }

    [Fact]
    public void daily_task_wire_rejects_unsupported_clients_before_io()
    {
        using var incoming = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity,
            new PacketBuffer(Convert.FromHexString("00000000")));
        Assert.Throws<UnsupportedClientException>(() =>
            MessageContracts.DailyTasks.Snapshot.Parse(incoming.Reader()));
        Assert.Equal(0, incoming.Position);

        using var outgoing = new Packet(new Header(Direction.Out, 91), ClientType.Unity);
        Assert.Throws<UnsupportedClientException>(() =>
            MessageContracts.DailyTasks.Claim.Compose(
                new DailyTaskClaimRequest(1),
                outgoing.Writer()));
        Assert.Equal(0, outgoing.Position);
        Assert.Equal(0, outgoing.Length);
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void quest_wire_roundtrips_every_verified_route(ClientType client)
    {
        const string quest_data =
            "000463616D70" +
            "00000001" +
            "00000002" +
            "00000003" +
            "0000002A" +
            "01" +
            "000474797065" +
            "00027631" +
            "0000000A" +
            "00036C6F63" +
            "00000004" +
            "00000005" +
            "00000006" +
            "000470616765" +
            "0005636861696E" +
            "01" +
            "00";
        string seasonal_data = quest_data[..^2] + "01" + "0000003C";
        string count = client is ClientType.Flash ? "00000001" : "0001";
        string id = client is ClientType.Flash ? "0000002A" : "000000000000002A";

        _ = AssertAchievementBadgeFixture(
            MessageContracts.Quests.Request,
            client,
            Direction.Out,
            "");
        Quests snapshot = AssertAchievementBadgeFixture(
            MessageContracts.Quests.Snapshot,
            client,
            Direction.In,
            count + quest_data + "01");
        QuestData data = Assert.Single(snapshot.Items);
        Assert.Equal("camp", data.CampaignCode);
        Assert.Equal(1, data.CompletedQuestsInCampaign);
        Assert.Equal(2, data.QuestCountInCampaign);
        Assert.Equal(3, data.ActivityPointType);
        Assert.Equal(42, data.Id);
        Assert.True(data.IsAccepted);
        Assert.Equal("type", data.Type);
        Assert.Equal("v1", data.ImageVersion);
        Assert.Equal(10, data.RewardCurrencyAmount);
        Assert.Equal("loc", data.LocalizationCode);
        Assert.Equal(4, data.CompletedSteps);
        Assert.Equal(5, data.TotalSteps);
        Assert.Equal(6, data.SortOrder);
        Assert.Equal("page", data.CatalogPageName);
        Assert.Equal("chain", data.ChainCode);
        Assert.True(data.IsEasy);
        Assert.False(data.IsSeasonal);
        Assert.Null(data.SeasonalSecondsLeft);
        Assert.False(data.IsCompleted);
        Assert.False(data.IsCampaignCompleted);
        Assert.False(data.IsLastQuestInCampaign);
        Assert.Equal("camp", data.CampaignChainCode);
        Assert.True(snapshot.OpenWindow);

        _ = AssertAchievementBadgeFixture(
            MessageContracts.Quests.SeasonalRequest,
            client,
            Direction.Out,
            "");
        QuestsSeasonal seasonal = AssertAchievementBadgeFixture(
            MessageContracts.Quests.SeasonalSnapshot,
            client,
            Direction.In,
            count + seasonal_data);
        QuestData seasonal_quest = Assert.Single(seasonal.Items);
        Assert.True(seasonal_quest.IsSeasonal);
        Assert.Equal(60, seasonal_quest.SeasonalSecondsLeft);
        Assert.Equal("camp.chain", seasonal_quest.CampaignChainCode);

        Quest updated = AssertAchievementBadgeFixture(
            MessageContracts.Quests.Updated,
            client,
            Direction.In,
            quest_data);
        Assert.Equal(42, updated.Data.Id);
        QuestCompleted completed = AssertAchievementBadgeFixture(
            MessageContracts.Quests.Completed,
            client,
            Direction.In,
            quest_data + "01");
        Assert.True(completed.ShowDialog);
        QuestCancelled cancelled = AssertAchievementBadgeFixture(
            MessageContracts.Quests.Cancelled,
            client,
            Direction.In,
            "01" + quest_data);
        Assert.True(cancelled.IsExpired);
        QuestDaily daily = AssertAchievementBadgeFixture(
            MessageContracts.Quests.Daily,
            client,
            Direction.In,
            "01" + quest_data + "0000000700000008");
        Assert.True(daily.HasQuest);
        Assert.Equal(7, daily.EasyQuestCount);
        Assert.Equal(8, daily.HardQuestCount);
        QuestDaily empty_daily = AssertAchievementBadgeFixture(
            MessageContracts.Quests.Daily,
            client,
            Direction.In,
            "00");
        Assert.False(empty_daily.HasQuest);

        GetDailyQuest daily_request = AssertAchievementBadgeFixture(
            MessageContracts.Quests.DailyRequest,
            client,
            Direction.Out,
            "0100000003");
        Assert.True(daily_request.IsEasy);
        Assert.Equal(3, daily_request.Index);
        Assert.Equal(42L, (long)AssertAchievementBadgeFixture(
            MessageContracts.Quests.Accept,
            client,
            Direction.Out,
            id).QuestId);
        Assert.Equal(42L, (long)AssertAchievementBadgeFixture(
            MessageContracts.Quests.Activate,
            client,
            Direction.Out,
            id).QuestId);
        Assert.Equal(42L, (long)AssertAchievementBadgeFixture(
            MessageContracts.Quests.Reject,
            client,
            Direction.Out,
            id).QuestId);
        _ = AssertAchievementBadgeFixture(
            MessageContracts.Quests.Cancel,
            client,
            Direction.Out,
            "");
        _ = AssertAchievementBadgeFixture(
            MessageContracts.Quests.TrackerOpen,
            client,
            Direction.Out,
            "");
        _ = AssertAchievementBadgeFixture(
            MessageContracts.Quests.FriendRequestCompleted,
            client,
            Direction.Out,
            "");

        using var direct = new Packet(
            new Header(Direction.In, 91),
            client,
            new PacketBuffer(Convert.FromHexString(quest_data)));
        Assert.Equal(data, QuestData.Parse(direct.Reader()));
        Assert.Equal(0, direct.Available);
    }

    [Fact]
    public void quest_models_freeze_with_and_preflight_atomically()
    {
        QuestData data = CreateQuestData();
        var mutable = new List<QuestData> { data };
        var snapshot = new Quests(mutable, true);
        mutable.Clear();
        Assert.Single(snapshot.Items);

        var replacement = new List<QuestData> { data with { Id = 43 } };
        Quests changed = snapshot with { Items = replacement };
        replacement.Clear();
        Assert.Equal(43, Assert.Single(changed.Items).Id);

        var seasonal = new QuestsSeasonal([data]);
        var seasonal_replacement = new List<QuestData> { data with { IsEasy = false } };
        QuestsSeasonal changed_seasonal = seasonal with { Items = seasonal_replacement };
        seasonal_replacement.Clear();
        Assert.False(Assert.Single(changed_seasonal.Items).IsEasy);

        Assert.Throws<ArgumentNullException>(() => CreateQuestData(campaign_code: null!));
        Assert.Throws<ArgumentNullException>(() => data with { Type = null! });
        Assert.Throws<ArgumentNullException>(() => new Quest(null!));
        Assert.Throws<ArgumentNullException>(() => new QuestCompleted(null!, false));
        Assert.Throws<ArgumentNullException>(() => new QuestCancelled(false, null!));
        Assert.Throws<ArgumentNullException>(() => new Quests(null!, false));
        Assert.Throws<ArgumentNullException>(() => new Quests([null!], false));
        Assert.Throws<ArgumentNullException>(() => new QuestsSeasonal([null!]));
        Assert.Throws<InvalidDataException>(() =>
            new Quests(new QuestData[ushort.MaxValue + 1], false));

        AssertAchievementBadgeComposeFails<Quest, InvalidDataException>(
            MessageContracts.Quests.Updated,
            new Quest(data with { IsSeasonal = true }),
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeComposeFails<Quest, InvalidDataException>(
            MessageContracts.Quests.Updated,
            new Quest(data with { SeasonalSecondsLeft = 1 }),
            ClientType.Unity,
            Direction.In);
        AssertAchievementBadgeComposeFails<QuestDaily, InvalidDataException>(
            MessageContracts.Quests.Daily,
            new QuestDaily(null, 1, 0),
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeComposeFails<AcceptQuest, OverflowException>(
            MessageContracts.Quests.Accept,
            new AcceptQuest(long.MaxValue),
            ClientType.Flash,
            Direction.Out);
        AssertAchievementBadgeComposeFails<Quests, InvalidDataException>(
            MessageContracts.Quests.Snapshot,
            new Quests([data with { CampaignCode = new string('x', ushort.MaxValue + 1) }], false),
            ClientType.Unity,
            Direction.In);

        string maximum_string = new('x', ushort.MaxValue);
        QuestData byte_heavy = CreateQuestData(
            maximum_string,
            maximum_string,
            maximum_string,
            maximum_string,
            maximum_string,
            maximum_string);
        AssertAchievementBadgeComposeFails<QuestsSeasonal, InvalidDataException>(
            MessageContracts.Quests.SeasonalSnapshot,
            new QuestsSeasonal(Enumerable.Repeat(byte_heavy, 43).ToArray()),
            ClientType.Unity,
            Direction.In);
        AssertAchievementBadgeComposeFails<Quests, InvalidDataException>(
            MessageContracts.Quests.Snapshot,
            new Quests(Enumerable.Repeat(CreateQuestData("", "", "", "", "", ""), 32_769).ToArray(), false),
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeComposeFails<GetQuests, ArgumentNullException>(
            MessageContracts.Quests.Request,
            null!,
            ClientType.Flash,
            Direction.Out);

        using var direct = new Packet(new Header(Direction.In, 91), ClientType.Flash);
        Assert.Throws<InvalidDataException>(() =>
            (data with { IsSeasonal = true }).Compose(direct.Writer()));
        Assert.Equal(0, direct.Position);
        Assert.Equal(0, direct.Length);
    }

    [Fact]
    public void quest_public_abi_remains_positional_and_exact()
    {
        AssertEarningConstructor(
            typeof(QuestData),
            (typeof(string), "CampaignCode"),
            (typeof(int), "CompletedQuestsInCampaign"),
            (typeof(int), "QuestCountInCampaign"),
            (typeof(int), "ActivityPointType"),
            (typeof(int), "Id"),
            (typeof(bool), "IsAccepted"),
            (typeof(string), "Type"),
            (typeof(string), "ImageVersion"),
            (typeof(int), "RewardCurrencyAmount"),
            (typeof(string), "LocalizationCode"),
            (typeof(int), "CompletedSteps"),
            (typeof(int), "TotalSteps"),
            (typeof(int), "SortOrder"),
            (typeof(string), "CatalogPageName"),
            (typeof(string), "ChainCode"),
            (typeof(bool), "IsEasy"),
            (typeof(bool), "IsSeasonal"),
            (typeof(int?), "SeasonalSecondsLeft"));
        AssertEarningProperties(
            typeof(QuestData),
            (nameof(QuestData.CampaignCode), typeof(string), true),
            (nameof(QuestData.CompletedQuestsInCampaign), typeof(int), true),
            (nameof(QuestData.QuestCountInCampaign), typeof(int), true),
            (nameof(QuestData.ActivityPointType), typeof(int), true),
            (nameof(QuestData.Id), typeof(int), true),
            (nameof(QuestData.IsAccepted), typeof(bool), true),
            (nameof(QuestData.Type), typeof(string), true),
            (nameof(QuestData.ImageVersion), typeof(string), true),
            (nameof(QuestData.RewardCurrencyAmount), typeof(int), true),
            (nameof(QuestData.LocalizationCode), typeof(string), true),
            (nameof(QuestData.CompletedSteps), typeof(int), true),
            (nameof(QuestData.TotalSteps), typeof(int), true),
            (nameof(QuestData.SortOrder), typeof(int), true),
            (nameof(QuestData.CatalogPageName), typeof(string), true),
            (nameof(QuestData.ChainCode), typeof(string), true),
            (nameof(QuestData.IsEasy), typeof(bool), true),
            (nameof(QuestData.IsSeasonal), typeof(bool), true),
            (nameof(QuestData.SeasonalSecondsLeft), typeof(int?), true),
            (nameof(QuestData.IsCompleted), typeof(bool), false),
            (nameof(QuestData.IsCampaignCompleted), typeof(bool), false),
            (nameof(QuestData.IsLastQuestInCampaign), typeof(bool), false),
            (nameof(QuestData.CampaignChainCode), typeof(string), false));
        AssertEarningDeconstruct(
            typeof(QuestData),
            (typeof(string), "CampaignCode"),
            (typeof(int), "CompletedQuestsInCampaign"),
            (typeof(int), "QuestCountInCampaign"),
            (typeof(int), "ActivityPointType"),
            (typeof(int), "Id"),
            (typeof(bool), "IsAccepted"),
            (typeof(string), "Type"),
            (typeof(string), "ImageVersion"),
            (typeof(int), "RewardCurrencyAmount"),
            (typeof(string), "LocalizationCode"),
            (typeof(int), "CompletedSteps"),
            (typeof(int), "TotalSteps"),
            (typeof(int), "SortOrder"),
            (typeof(string), "CatalogPageName"),
            (typeof(string), "ChainCode"),
            (typeof(bool), "IsEasy"),
            (typeof(bool), "IsSeasonal"),
            (typeof(int?), "SeasonalSecondsLeft"));

        AssertQuestRecordAbi(typeof(Quest), (typeof(QuestData), "Data"));
        AssertQuestRecordAbi(
            typeof(Quests),
            (typeof(IReadOnlyList<QuestData>), "Items"),
            (typeof(bool), "OpenWindow"));
        AssertQuestRecordAbi(
            typeof(QuestsSeasonal),
            (typeof(IReadOnlyList<QuestData>), "Items"));
        AssertQuestRecordAbi(
            typeof(QuestCompleted),
            (typeof(QuestData), "Data"),
            (typeof(bool), "ShowDialog"));
        AssertQuestRecordAbi(
            typeof(QuestCancelled),
            (typeof(bool), "IsExpired"),
            (typeof(QuestData), "Data"));
        AssertEarningConstructor(
            typeof(QuestDaily),
            (typeof(QuestData), "Data"),
            (typeof(int), "EasyQuestCount"),
            (typeof(int), "HardQuestCount"));
        AssertEarningProperties(
            typeof(QuestDaily),
            (nameof(QuestDaily.Data), typeof(QuestData), true),
            (nameof(QuestDaily.EasyQuestCount), typeof(int), true),
            (nameof(QuestDaily.HardQuestCount), typeof(int), true),
            (nameof(QuestDaily.HasQuest), typeof(bool), false));
        AssertEarningDeconstruct(
            typeof(QuestDaily),
            (typeof(QuestData), "Data"),
            (typeof(int), "EasyQuestCount"),
            (typeof(int), "HardQuestCount"));

        AssertQuestRecordAbi(typeof(AcceptQuest), (typeof(Id), "QuestId"));
        AssertQuestRecordAbi(typeof(ActivateQuest), (typeof(Id), "QuestId"));
        AssertQuestRecordAbi(typeof(RejectQuest), (typeof(Id), "QuestId"));
        AssertQuestRecordAbi(typeof(GetDailyQuest),
            (typeof(bool), "IsEasy"),
            (typeof(int), "Index"));
        AssertEmptyQuestRecordAbi(typeof(CancelQuest));
        AssertEmptyQuestRecordAbi(typeof(GetQuests));
        AssertEmptyQuestRecordAbi(typeof(GetSeasonalQuests));
        AssertEmptyQuestRecordAbi(typeof(OpenQuestTracker));
        AssertEmptyQuestRecordAbi(typeof(FriendRequestQuestComplete));
    }

    [Fact]
    public void quest_parsers_reject_counts_capacity_truncation_and_tails()
    {
        const string minimal_quest =
            "0000" +
            "00000000" +
            "00000000" +
            "00000000" +
            "00000000" +
            "00" +
            "0000" +
            "0000" +
            "00000000" +
            "0000" +
            "00000000" +
            "00000000" +
            "00000000" +
            "0000" +
            "0000" +
            "00" +
            "00";
        Assert.Equal(47, Convert.FromHexString(minimal_quest).Length);

        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Snapshot,
            ClientType.Flash,
            Direction.In,
            "FFFFFFFF00");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Snapshot,
            ClientType.Flash,
            Direction.In,
            "0001000000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Snapshot,
            ClientType.Flash,
            Direction.In,
            "0000000100");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.SeasonalSnapshot,
            ClientType.Unity,
            Direction.In,
            "0001");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Updated,
            ClientType.Flash,
            Direction.In,
            "");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Completed,
            ClientType.Flash,
            Direction.In,
            minimal_quest);
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Cancelled,
            ClientType.Unity,
            Direction.In,
            "00");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Daily,
            ClientType.Flash,
            Direction.In,
            "01" + minimal_quest + "00000000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Daily,
            ClientType.Unity,
            Direction.In,
            "0000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.DailyRequest,
            ClientType.Flash,
            Direction.Out,
            "01000000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Accept,
            ClientType.Flash,
            Direction.Out,
            "0000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Activate,
            ClientType.Unity,
            Direction.Out,
            "00000000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.Cancel,
            ClientType.Flash,
            Direction.Out,
            "00");

        string stealing_quest = "0001" + "00" + minimal_quest[4..];
        string truncated_sibling = minimal_quest[..^2];
        AssertAchievementBadgeParseFails(
            MessageContracts.Quests.SeasonalSnapshot,
            ClientType.Flash,
            Direction.In,
            "00000002" + stealing_quest + truncated_sibling);

        using var direct = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash,
            new PacketBuffer(Convert.FromHexString(minimal_quest + "7F")));
        Assert.Throws<InvalidDataException>(() => QuestData.Parse(direct.Reader()));
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void quest_parser_enforces_the_root_string_budget(ClientType client)
    {
        string maximum_string = new('x', ushort.MaxValue);
        using var packet = new Packet(new Header(Direction.In, 91), client);
        PacketWriter writer = packet.Writer();
        if (client is ClientType.Flash)
            writer.WriteInt(43);
        else
            writer.WriteShort(43);
        for (int index = 0; index < 43; index++)
        {
            writer.WriteString(maximum_string);
            writer.WriteInt(0);
            writer.WriteInt(0);
            writer.WriteInt(0);
            writer.WriteInt(0);
            writer.WriteBool(false);
            writer.WriteString(maximum_string);
            writer.WriteString(maximum_string);
            writer.WriteInt(0);
            writer.WriteString(maximum_string);
            writer.WriteInt(0);
            writer.WriteInt(0);
            writer.WriteInt(0);
            writer.WriteString(maximum_string);
            writer.WriteString(maximum_string);
            writer.WriteBool(false);
            writer.WriteBool(false);
        }
        packet.Position = 0;

        Assert.Throws<InvalidDataException>(() =>
            MessageContracts.Quests.SeasonalSnapshot.Parse(packet.Reader()));
    }

    [Fact]
    public void quest_wire_rejects_unsupported_clients_before_io()
    {
        using var incoming = new Packet(
            new Header(Direction.In, 91),
            ClientType.None,
            new PacketBuffer(Convert.FromHexString("00000000")));
        Assert.Throws<UnsupportedClientException>(() => QuestData.Parse(incoming.Reader()));
        Assert.Equal(0, incoming.Position);

        using var outgoing = new Packet(new Header(Direction.Out, 91), ClientType.None);
        Assert.Throws<UnsupportedClientException>(() =>
            new AcceptQuest(1).Compose(outgoing.Writer()));
        Assert.Equal(0, outgoing.Position);
        Assert.Equal(0, outgoing.Length);
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void subscription_wire_roundtrips_all_supported_routes(ClientType client)
    {
        _ = RoundtripSubscription(
            MessageContracts.Subscriptions.UserInfo,
            new ScrSendUserInfo(
                "habbo_club",
                1,
                2,
                3,
                4,
                true,
                false,
                5,
                6,
                7,
                8),
            client,
            Direction.In);
        _ = RoundtripSubscription(
            MessageContracts.Subscriptions.UserInfoRequest,
            new SubscriptionGetUserInfo("habbo_club"),
            client,
            Direction.Out);
        _ = RoundtripSubscription(
            MessageContracts.Subscriptions.KickbackInfo,
            new ScrSendKickbackInfo(1, "2020-01-01", 0.5, 2, 3, 4, 5, 6, 7),
            client,
            Direction.In);
        _ = RoundtripSubscription(
            MessageContracts.Subscriptions.KickbackInfoRequest,
            new SubscriptionGetKickbackInfo(),
            client,
            Direction.Out);
        _ = RoundtripSubscription(
            MessageContracts.Subscriptions.BuildersClubFurniCount,
            new BuildersClubFurniCount(42),
            client,
            Direction.In);
        _ = RoundtripSubscription(
            MessageContracts.Subscriptions.BuildersClubFurniCountRequest,
            new BuildersClubQueryFurniCount(),
            client,
            Direction.Out);
        _ = RoundtripClubOffers(
            new HabboClubOffers([ClubOffer()], 9),
            client);
        _ = RoundtripSubscription(
            MessageContracts.Subscriptions.ClubOffersRequest,
            new GetClubOffers(2),
            client,
            Direction.Out);
        _ = RoundtripSubscription(
            MessageContracts.Subscriptions.BuildersClubFloorOfferPlace,
            new BuildersClubPlaceRoomItem(1, 2, "x", 3, 4, 5, true),
            client,
            Direction.Out);
        _ = RoundtripSubscription(
            MessageContracts.Subscriptions.BuildersClubWallOfferPlace,
            new BuildersClubPlaceWallItem(1, 2, "x", ":w=3,4 l=5,6 r", true),
            client,
            Direction.Out);

        if (client is ClientType.Flash)
        {
            _ = RoundtripSubscription(
                MessageContracts.Subscriptions.BuildersClubMembershipStatus,
                new BuildersClubMembershipStatus(10, 20, 30, 40),
                client,
                Direction.In);
            _ = RoundtripSubscription(
                MessageContracts.Subscriptions.BuildersClubPlacementWarning,
                new BuildersClubPlacementWarning(
                    1,
                    2,
                    "floor",
                    new BuildersClubFloorPlacement(3, 4, 5)),
                client,
                Direction.In);
            _ = RoundtripSubscription(
                MessageContracts.Subscriptions.BuildersClubPlacementWarning,
                new BuildersClubPlacementWarning(
                    6,
                    7,
                    "wall",
                    new BuildersClubWallPlacement(":w=1,2 l=3,4")),
                client,
                Direction.In);
        }
    }

    [Fact]
    public void subscription_optional_tails_and_string_preflight_are_exact()
    {
        ScrSendUserInfo flash_without_tail = RoundtripSubscription(
            MessageContracts.Subscriptions.UserInfo,
            new ScrSendUserInfo("habbo_club", 1, 2, 3, 4, true, false, 5, 6, 7, null),
            ClientType.Flash,
            Direction.In,
            reject_trailing: false);
        Assert.Null(flash_without_tail.MinutesSinceLastModified);

        BuildersClubMembershipStatus membership_without_tail = RoundtripSubscription(
            MessageContracts.Subscriptions.BuildersClubMembershipStatus,
            new BuildersClubMembershipStatus(10, 20, 30, null),
            ClientType.Flash,
            Direction.In,
            reject_trailing: false);
        Assert.Null(membership_without_tail.SecondsLeftWithGrace);
        Assert.Equal(10, membership_without_tail.EffectiveSecondsLeftWithGrace);

        using (var unity_missing_tail = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity))
        {
            WriteUserInfo(unity_missing_tail.Writer());
            unity_missing_tail.Position = 0;
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.UserInfo.Parse(unity_missing_tail.Reader()));
        }

        using (var unity_preflight = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity))
        {
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.UserInfo.Compose(
                    new ScrSendUserInfo(
                        "habbo_club",
                        1,
                        2,
                        3,
                        4,
                        true,
                        false,
                        5,
                        6,
                        7,
                        null),
                    unity_preflight.Writer()));
            Assert.Equal(0, unity_preflight.Length);
        }

        using (var null_product = new Packet(
            new Header(Direction.Out, 91),
            ClientType.Flash))
        {
            Assert.Throws<ArgumentNullException>(() =>
                MessageContracts.Subscriptions.UserInfoRequest.Compose(
                    new SubscriptionGetUserInfo(null!),
                    null_product.Writer()));
            Assert.Equal(0, null_product.Length);
        }

        using (var oversized_product = new Packet(
            new Header(Direction.Out, 91),
            ClientType.Unity))
        {
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.UserInfoRequest.Compose(
                    new SubscriptionGetUserInfo(new string('x', ushort.MaxValue + 1)),
                    oversized_product.Writer()));
            Assert.Equal(0, oversized_product.Length);
        }

        using (var null_kickback = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash))
        {
            Assert.Throws<ArgumentNullException>(() =>
                MessageContracts.Subscriptions.KickbackInfo.Compose(
                    new ScrSendKickbackInfo(1, null!, 0, 2, 3, 4, 5, 6, 7),
                    null_kickback.Writer()));
            Assert.Equal(0, null_kickback.Length);
        }

        using (var oversized_kickback = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity))
        {
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.KickbackInfo.Compose(
                    new ScrSendKickbackInfo(
                        1,
                        new string('x', ushort.MaxValue + 1),
                        0,
                        2,
                        3,
                        4,
                        5,
                        6,
                        7),
                    oversized_kickback.Writer()));
            Assert.Equal(0, oversized_kickback.Length);
        }

        using (var null_extra = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash))
        {
            Assert.Throws<ArgumentNullException>(() =>
                MessageContracts.Subscriptions.BuildersClubPlacementWarning.Compose(
                    new BuildersClubPlacementWarning(
                        1,
                        2,
                        null!,
                        new BuildersClubFloorPlacement(3, 4, 5)),
                    null_extra.Writer()));
            Assert.Equal(0, null_extra.Length);
        }

        using (var oversized_wall = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash))
        {
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.BuildersClubPlacementWarning.Compose(
                    new BuildersClubPlacementWarning(
                        1,
                        2,
                        "valid",
                        new BuildersClubWallPlacement(
                            new string('x', ushort.MaxValue + 1))),
                    oversized_wall.Writer()));
            Assert.Equal(0, oversized_wall.Length);
        }

        using (var null_wall = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash))
        {
            Assert.Throws<ArgumentNullException>(() =>
                MessageContracts.Subscriptions.BuildersClubPlacementWarning.Compose(
                    new BuildersClubPlacementWarning(
                        1,
                        2,
                        "valid",
                        new BuildersClubWallPlacement(null!)),
                    null_wall.Writer()));
            Assert.Equal(0, null_wall.Length);
        }
    }

    [Fact]
    public void subscription_supported_routes_have_exact_flash_and_unity_bytes()
    {
        foreach (ClientType client in new[] { ClientType.Flash, ClientType.Unity })
        {
            AssertSubscriptionHex(
                MessageContracts.Subscriptions.UserInfo,
                new ScrSendUserInfo("x", 1, 2, 3, 4, true, false, 5, 6, 7, 8),
                client,
                Direction.In,
                "000178000000010000000200000003000000040100" +
                    "00000005000000060000000700000008");
            AssertSubscriptionHex(
                MessageContracts.Subscriptions.UserInfoRequest,
                new SubscriptionGetUserInfo("x"),
                client,
                Direction.Out,
                "000178");
            AssertSubscriptionHex(
                MessageContracts.Subscriptions.KickbackInfo,
                new ScrSendKickbackInfo(1, "x", 0, 2, 3, 4, 5, 6, 7),
                client,
                Direction.In,
                "0000000100017800000000000000000000000200000003" +
                    "00000004000000050000000600000007");
            AssertSubscriptionHex(
                MessageContracts.Subscriptions.KickbackInfoRequest,
                new SubscriptionGetKickbackInfo(),
                client,
                Direction.Out,
                "");
            AssertSubscriptionHex(
                MessageContracts.Subscriptions.BuildersClubFurniCount,
                new BuildersClubFurniCount(42),
                client,
                Direction.In,
                "0000002A");
            AssertSubscriptionHex(
                MessageContracts.Subscriptions.BuildersClubFurniCountRequest,
                new BuildersClubQueryFurniCount(),
                client,
                Direction.Out,
                "");
            AssertClubOffersHex(
                new HabboClubOffers([ClubOffer()], 9),
                client,
                (client is ClientType.Flash ? "00000001" : "0001") +
                    "000000010001780000000002000000030000000401" +
                    "00000005000000060000000007000007EA000000080000000B00000009");
            AssertSubscriptionHex(
                MessageContracts.Subscriptions.ClubOffersRequest,
                new GetClubOffers(2),
                client,
                Direction.Out,
                "00000002");
            AssertSubscriptionHex(
                MessageContracts.Subscriptions.BuildersClubFloorOfferPlace,
                new BuildersClubPlaceRoomItem(1, 2, "x", 3, 4, 5, true),
                client,
                Direction.Out,
                "000000010000000200017800000003000000040000000501");
        }

        AssertSubscriptionHex(
            MessageContracts.Subscriptions.BuildersClubWallOfferPlace,
            new BuildersClubPlaceWallItem(1, 2, "x", ":w=3,4 l=5,6 r", true),
            ClientType.Flash,
            Direction.Out,
            "0000000100000002000178000E3A773D332C34206C3D352C36207201");
        AssertSubscriptionHex(
            MessageContracts.Subscriptions.BuildersClubWallOfferPlace,
            new BuildersClubPlaceWallItem(1, 2, "x", ":w=3,4 l=5,6 r", true),
            ClientType.Unity,
            Direction.Out,
            "00000001000000020001780000000300000004000000050000000600017201");

        AssertSubscriptionHex(
            MessageContracts.Subscriptions.BuildersClubMembershipStatus,
            new BuildersClubMembershipStatus(1, 2, 3, 4),
            ClientType.Flash,
            Direction.In,
            "00000001000000020000000300000004");
        AssertSubscriptionHex(
            MessageContracts.Subscriptions.BuildersClubPlacementWarning,
            new BuildersClubPlacementWarning(
                1,
                2,
                "x",
                new BuildersClubFloorPlacement(3, 4, 5)),
            ClientType.Flash,
            Direction.In,
            "000000000000000100000002000178000000030000000400000005");
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void subscription_club_offer_reserved_wire_flag_is_preserved(ClientType client)
    {
        string raw_hex = (client is ClientType.Flash ? "00000001" : "0001") +
            "000000010001780100000002000000030000000401" +
            "00000005000000060000000007000007EA000000080000000B00000009";
        byte[] raw = Convert.FromHexString(raw_hex);
        using var parsed_packet = new Packet(new Header(Direction.In, 91), client);
        parsed_packet.WriteSpan(raw);
        parsed_packet.Position = 0;
        HabboClubOffers parsed =
            MessageContracts.Subscriptions.ClubOffersSnapshot.Parse(parsed_packet.Reader());
        Assert.Equal(0, parsed_packet.Available);
        Assert.Equal(9, parsed.DaysLeft);
        HabboClubOffer parsed_offer = Assert.Single(parsed.Offers);
        AssertClubOfferPublicEqual(ClubOffer(), parsed_offer);
        Assert.NotEqual(ClubOffer(), parsed_offer);
        HabboClubOffer copied_offer = parsed_offer with { };
        Assert.Equal(parsed_offer, copied_offer);
        HabboClubOffers copied = parsed with { Offers = [copied_offer] };

        using var composed = new Packet(new Header(Direction.In, 91), client);
        MessageContracts.Subscriptions.ClubOffersSnapshot.Compose(
            copied,
            composed.Writer());
        Assert.Equal(raw, composed.Buffer.Span.ToArray());
    }

    [Fact]
    public void subscription_club_offer_models_freeze_and_preflight_atomically()
    {
        HabboClubOffer original = ClubOffer();
        var mutable = new List<HabboClubOffer> { original };
        var frozen = new HabboClubOffers(mutable, 9);
        mutable[0] = ClubOffer("changed");
        mutable.Clear();
        Assert.Same(original, Assert.Single(frozen.Offers));

        var replacement = new List<HabboClubOffer> { original };
        HabboClubOffers copied = frozen with { Offers = replacement };
        replacement.Clear();
        Assert.Same(original, Assert.Single(copied.Offers));
        Assert.Throws<ArgumentNullException>(() => new HabboClubOffers(null!, 0));
        Assert.Throws<ArgumentNullException>(() =>
            new HabboClubOffers([null!], 0));
        var over_limit = new HabboClubOffer[ushort.MaxValue + 1];
        Assert.Throws<InvalidDataException>(() =>
            new HabboClubOffers(over_limit, 0));

        ConstructorInfo offer_constructor = Assert.Single(
            typeof(HabboClubOffer).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.Equal(13, offer_constructor.GetParameters().Length);
        MethodInfo offer_deconstruct = Assert.Single(
            typeof(HabboClubOffer).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.Name == "Deconstruct");
        Assert.Equal(13, offer_deconstruct.GetParameters().Length);
        ConstructorInfo offers_constructor = Assert.Single(
            typeof(HabboClubOffers).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.Equal(2, offers_constructor.GetParameters().Length);
        MethodInfo offers_deconstruct = Assert.Single(
            typeof(HabboClubOffers).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.Name == "Deconstruct");
        Assert.Equal(2, offers_deconstruct.GetParameters().Length);

        string maximum_string = new('x', ushort.MaxValue);
        HabboClubOffer maximum_offer = ClubOffer(maximum_string);
        var aggregate = new HabboClubOffers(
            Enumerable.Repeat(maximum_offer, 129).ToArray(),
            9);
        var null_product = new HabboClubOffers([ClubOffer(null!)], 9);
        var oversized_product = new HabboClubOffers(
            [ClubOffer(new string('x', ushort.MaxValue + 1))],
            9);

        foreach (ClientType client in new[] { ClientType.Flash, ClientType.Unity })
        {
            using var over_limit_packet = new Packet(new Header(Direction.In, 91), client);
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.ClubOffersSnapshot.Compose(
                    new HabboClubOffers(over_limit, 0),
                    over_limit_packet.Writer()));
            Assert.Equal(0, over_limit_packet.Position);
            Assert.Equal(0, over_limit_packet.Length);

            using var aggregate_packet = new Packet(new Header(Direction.In, 91), client);
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.ClubOffersSnapshot.Compose(
                    aggregate,
                    aggregate_packet.Writer()));
            Assert.Equal(0, aggregate_packet.Position);
            Assert.Equal(0, aggregate_packet.Length);

            using var null_packet = new Packet(new Header(Direction.In, 91), client);
            Assert.Throws<ArgumentNullException>(() =>
                MessageContracts.Subscriptions.ClubOffersSnapshot.Compose(
                    null_product,
                    null_packet.Writer()));
            Assert.Equal(0, null_packet.Position);
            Assert.Equal(0, null_packet.Length);

            using var oversized_packet = new Packet(new Header(Direction.In, 91), client);
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.ClubOffersSnapshot.Compose(
                    oversized_product,
                    oversized_packet.Writer()));
            Assert.Equal(0, oversized_packet.Position);
            Assert.Equal(0, oversized_packet.Length);

            using var floor_packet = new Packet(new Header(Direction.Out, 91), client);
            Assert.Throws<ArgumentNullException>(() =>
                MessageContracts.Subscriptions.BuildersClubFloorOfferPlace.Compose(
                    new BuildersClubPlaceRoomItem(1, 2, null!, 3, 4, 5),
                    floor_packet.Writer()));
            Assert.Equal(0, floor_packet.Position);
            Assert.Equal(0, floor_packet.Length);

            using var oversized_floor_packet = new Packet(
                new Header(Direction.Out, 91),
                client);
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.BuildersClubFloorOfferPlace.Compose(
                    new BuildersClubPlaceRoomItem(
                        1,
                        2,
                        new string('x', ushort.MaxValue + 1),
                        3,
                        4,
                        5),
                    oversized_floor_packet.Writer()));
            Assert.Equal(0, oversized_floor_packet.Position);
            Assert.Equal(0, oversized_floor_packet.Length);
        }

        using var invalid_wall = new Packet(
            new Header(Direction.Out, 91),
            ClientType.Unity);
        Assert.Throws<FormatException>(() =>
            MessageContracts.Subscriptions.BuildersClubWallOfferPlace.Compose(
                new BuildersClubPlaceWallItem(1, 2, "x", "invalid"),
                invalid_wall.Writer()));
        Assert.Equal(0, invalid_wall.Position);
        Assert.Equal(0, invalid_wall.Length);

        using var null_wall = new Packet(
            new Header(Direction.Out, 91),
            ClientType.Flash);
        Assert.Throws<ArgumentNullException>(() =>
            MessageContracts.Subscriptions.BuildersClubWallOfferPlace.Compose(
                new BuildersClubPlaceWallItem(1, 2, "x", null!),
                null_wall.Writer()));
        Assert.Equal(0, null_wall.Position);
        Assert.Equal(0, null_wall.Length);
    }

    [Fact]
    public void subscription_club_offer_counts_fail_before_allocation()
    {
        using (var negative = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash))
        {
            PacketWriter writer = negative.Writer();
            writer.WriteInt(-1);
            writer.WriteInt(9);
            negative.Position = 0;
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.ClubOffersSnapshot.Parse(negative.Reader()));
        }

        using (var flash_overflow = new Packet(
            new Header(Direction.In, 91),
            ClientType.Flash))
        {
            PacketWriter writer = flash_overflow.Writer();
            writer.WriteInt(ushort.MaxValue + 1);
            writer.WriteInt(9);
            flash_overflow.Position = 0;
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.ClubOffersSnapshot.Parse(
                    flash_overflow.Reader()));
        }

        using (var unity_impossible = new Packet(
            new Header(Direction.In, 91),
            ClientType.Unity))
        {
            PacketWriter writer = unity_impossible.Writer();
            writer.WriteShort(unchecked((short)ushort.MaxValue));
            writer.WriteInt(9);
            unity_impossible.Position = 0;
            Assert.Throws<InvalidDataException>(() =>
                MessageContracts.Subscriptions.ClubOffersSnapshot.Parse(
                    unity_impossible.Reader()));
        }
    }

    [Theory]
    [InlineData(":w=0,9 l=1,18 l a=294", 0, 9, 1, 18, 'l')]
    [InlineData(":w=19,22 l=3,-107 l a=680", 19, 22, 3, -107, 'l')]
    [InlineData(":w=-4,7 l=-12,31 r future=value extra", -4, 7, -12, 31, 'r')]
    public void flash_wall_items_accept_client_location_extensions(
        string raw_location,
        int wall_x,
        int wall_y,
        int local_x,
        int local_y,
        char orientation)
    {
        using var packet = new Packet(new Header(Direction.In, 91), ClientType.Flash);
        PacketWriter writer = packet.Writer();
        writer.WriteString("42");
        writer.WriteInt(7);
        writer.WriteString(raw_location);
        writer.WriteString("0");
        writer.WriteInt(0);
        writer.WriteInt(0);
        writer.WriteId((Id)9);
        packet.Position = 0;

        WallItem item = WallItem.Parse(packet.Reader());

        Assert.Equal(wall_x, item.WX);
        Assert.Equal(wall_y, item.WY);
        Assert.Equal(local_x, item.LX);
        Assert.Equal(local_y, item.LY);
        Assert.Equal(orientation, item.Orientation.Value);
        Assert.Equal(0, packet.Available);
    }

    [Theory]
    [InlineData(":w=0,9 l=1,18")]
    [InlineData(":w=0,9 l=1,18 x a=294")]
    [InlineData(":w=0,9 x=1,18 l a=294")]
    [InlineData("w=0,9 l=1,18 l a=294")]
    public void flash_wall_location_extensions_do_not_relax_the_base_shape(string raw_location)
    {
        Assert.False(WallLocation.TryParse(raw_location, out _));
    }

    [Fact]
    public void subscription_builders_club_wall_capability_is_fail_closed()
    {
        OutgoingMessageSchema reference = MoveWallSchema("WallLocation");
        OutgoingMessageSchema target = BuildersClubWallSchema("WallLocation");
        Assert.True(BuildersClubWallCapability([reference], [target]).Available);
        Assert.False(BuildersClubWallCapability([], [target]).Available);
        Assert.False(BuildersClubWallCapability([MoveWallSchema(null!)], [target]).Available);
        Assert.False(BuildersClubWallCapability([MoveWallSchema("")], [target]).Available);
        Assert.False(BuildersClubWallCapability(
            [MoveWallSchema(null!), reference],
            [target]).Available);
        Assert.False(BuildersClubWallCapability(
            [reference, MoveWallSchema("OtherWallLocation")],
            [target]).Available);
        Assert.False(BuildersClubWallCapability([reference], []).Available);
        Assert.False(BuildersClubWallCapability(
            [reference],
            [BuildersClubWallSchema(null!)]).Available);
        Assert.False(BuildersClubWallCapability(
            [reference],
            [BuildersClubWallSchema("OtherWallLocation")]).Available);
        Assert.False(BuildersClubWallCapability(
            [reference],
            [BuildersClubWallSchema(
                "WallLocation",
                OutgoingCollectionKind.List)]).Available);
    }

    [Fact]
    public void subscription_builders_club_unity_wall_send_uses_verified_matcher()
    {
        (MessageManager messages, Header target_header) = BuildersClubWallMessages(
            [MoveWallSchema("WallLocation")],
            [BuildersClubWallSchema("WallLocation")]);
        var session = new Session("localhost", 0, "smoke", "smoke", ClientType.Unity);
        var interceptor = new CaptureInterceptor(messages, session);
        using var requests = new RequestBroker();
        requests.Attach(interceptor);

        requests.SendComposer(
            MessageContracts.Subscriptions.BuildersClubWallOfferPlace,
            new BuildersClubPlaceWallItem(1, 2, "x", ":w=3,4 l=5,6 r", true));

        Assert.Equal(1, interceptor.SendCount);
        Assert.Equal(target_header, interceptor.Header);
        Assert.Equal(ClientType.Unity, interceptor.Client);
        Assert.Equal(
            Convert.FromHexString(
                "00000001000000020001780000000300000004000000050000000600017201"),
            interceptor.Payload);

        (MessageManager invalid_messages, _) = BuildersClubWallMessages(
            [MoveWallSchema(null!), MoveWallSchema("WallLocation")],
            [BuildersClubWallSchema("WallLocation")]);
        var invalid_interceptor = new CaptureInterceptor(invalid_messages, session);
        using var invalid_requests = new RequestBroker();
        invalid_requests.Attach(invalid_interceptor);
        Assert.Throws<NotSupportedException>(() =>
            invalid_requests.SendComposer(
                MessageContracts.Subscriptions.BuildersClubWallOfferPlace,
                new BuildersClubPlaceWallItem(1, 2, "x", ":w=3,4 l=5,6 r", true)));
        Assert.Equal(0, invalid_interceptor.SendCount);
    }

    [Fact]
    public void leaderboard_flash_routes_roundtrip_exact_wire_shapes()
    {
        var entry = new LeaderboardEntry(1, 2, 3, "A", "B", "M");
        var board = new Leaderboard([entry], 10, 11);
        var period = new WeeklyLeaderboardPeriod(2026, 7, 8, 1, 9);

        Assert.Equal(
            new LeaderboardRequest(11, -1, 0, 8, 50),
            RoundtripForum(
                MessageContracts.Leaderboards.TotalRequest,
                new LeaderboardRequest(11, -1, 0, 8, 50),
                ClientType.Flash,
                Direction.Out));
        Assert.Equal(
            new WeeklyLeaderboardRequest(11, 1, -1, 0, 8, 50),
            RoundtripForum(
                MessageContracts.Leaderboards.WeeklyTotalRequest,
                new WeeklyLeaderboardRequest(11, 1, -1, 0, 8, 50),
                ClientType.Flash,
                Direction.Out));
        RoundtripForum(
            MessageContracts.Leaderboards.TotalSnapshot,
            new TotalLeaderboard(board),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Leaderboards.FriendsSnapshot,
            new FriendsLeaderboard(board),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Leaderboards.GroupsSnapshot,
            new TotalGroupLeaderboard(board, 12),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Leaderboards.WeeklyTotalSnapshot,
            new WeeklyLeaderboard(period, board),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Leaderboards.WeeklyFriendsSnapshot,
            new WeeklyFriendsLeaderboard(period, board),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Leaderboards.WeeklyGroupsSnapshot,
            new WeeklyGroupLeaderboard(period, board, 12),
            ClientType.Flash,
            Direction.In);

        AssertAchievementBadgeParseFails(
            MessageContracts.Leaderboards.TotalRequest,
            ClientType.Flash,
            Direction.Out,
            "00000001000000020000000300000004");
        AssertAchievementBadgeParseFails(
            MessageContracts.Leaderboards.WeeklyTotalRequest,
            ClientType.Flash,
            Direction.Out,
            "0000000100000002000000030000000400000005");
    }

    [Fact]
    public void leaderboard_models_freeze_bound_and_compose_atomically()
    {
        var entry = new LeaderboardEntry(1, 2, 3, "A", "B", "M");
        var mutable = new List<LeaderboardEntry> { entry };
        var board = new Leaderboard(mutable, 10, 11);
        mutable.Clear();
        Assert.Same(entry, Assert.Single(board.Entries));

        var replacement = new List<LeaderboardEntry> { entry with { Name = "C" } };
        Leaderboard copied = board with { Entries = replacement };
        replacement.Clear();
        Assert.Equal("C", Assert.Single(copied.Entries).Name);
        Assert.Throws<ArgumentNullException>(() => new Leaderboard(null!, 0, 0));
        Assert.Throws<ArgumentNullException>(() => new Leaderboard([null!], 0, 0));
        Assert.Throws<InvalidDataException>(() =>
            new Leaderboard(new LeaderboardEntry[ushort.MaxValue + 1], 0, 0));
        Assert.Throws<ArgumentNullException>(() => entry with { Name = null! });
        Assert.Throws<ArgumentNullException>(() => new TotalLeaderboard(null!));
        Assert.Throws<ArgumentNullException>(() => new WeeklyLeaderboard(null!, board));

        AssertAchievementBadgeComposeFails<TotalLeaderboard, InvalidDataException>(
            MessageContracts.Leaderboards.TotalSnapshot,
            new TotalLeaderboard(
                new Leaderboard(
                    [entry with { Name = new string('x', ushort.MaxValue + 1) }],
                    1,
                    1)),
            ClientType.Flash,
            Direction.In);

        string maximum = new('x', ushort.MaxValue);
        var heavy = new LeaderboardEntry(1, 2, 3, maximum, maximum, maximum);
        AssertAchievementBadgeComposeFails<TotalLeaderboard, InvalidDataException>(
            MessageContracts.Leaderboards.TotalSnapshot,
            new TotalLeaderboard(
                new Leaderboard(Enumerable.Repeat(heavy, 86).ToArray(), 86, 1)),
            ClientType.Flash,
            Direction.In);

        AssertAchievementBadgeParseFails(
            MessageContracts.Leaderboards.TotalSnapshot,
            ClientType.Flash,
            Direction.In,
            "FFFFFFFF0000000000000000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Leaderboards.TotalSnapshot,
            ClientType.Flash,
            Direction.In,
            "000000020000000000000000");

        IMessageContract[] contracts =
        [
            MessageContracts.Leaderboards.TotalRequest,
            MessageContracts.Leaderboards.TotalSnapshot,
            MessageContracts.Leaderboards.FriendsRequest,
            MessageContracts.Leaderboards.FriendsSnapshot,
            MessageContracts.Leaderboards.GroupsRequest,
            MessageContracts.Leaderboards.GroupsSnapshot,
            MessageContracts.Leaderboards.WeeklyTotalRequest,
            MessageContracts.Leaderboards.WeeklyTotalSnapshot,
            MessageContracts.Leaderboards.WeeklyFriendsRequest,
            MessageContracts.Leaderboards.WeeklyFriendsSnapshot,
            MessageContracts.Leaderboards.WeeklyGroupsRequest,
            MessageContracts.Leaderboards.WeeklyGroupsSnapshot
        ];
        Assert.All(contracts, contract =>
        {
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.False(contract.Supports(ClientType.Unity));
        });
        AssertAchievementBadgeComposeFails<LeaderboardRequest, UnsupportedClientException>(
            MessageContracts.Leaderboards.TotalRequest,
            new LeaderboardRequest(1, -1, 0, 8, 50),
            ClientType.Unity,
            Direction.Out);
        AssertAchievementBadgeComposeFails<TotalLeaderboard, UnsupportedClientException>(
            MessageContracts.Leaderboards.TotalSnapshot,
            new TotalLeaderboard(board),
            ClientType.Unity,
            Direction.In);
    }

    [Fact]
    public void leaderboard_public_model_abi_remains_positional_and_exact()
    {
        (Type type, Type[] parameters)[] models =
        [
            (typeof(LeaderboardEntry), [typeof(int), typeof(int), typeof(int), typeof(string), typeof(string), typeof(string)]),
            (typeof(Leaderboard), [typeof(IReadOnlyList<LeaderboardEntry>), typeof(int), typeof(int)]),
            (typeof(TotalLeaderboard), [typeof(Leaderboard)]),
            (typeof(FriendsLeaderboard), [typeof(Leaderboard)]),
            (typeof(TotalGroupLeaderboard), [typeof(Leaderboard), typeof(int)]),
            (typeof(WeeklyLeaderboardPeriod), [typeof(int), typeof(int), typeof(int), typeof(int), typeof(int)]),
            (typeof(WeeklyLeaderboard), [typeof(WeeklyLeaderboardPeriod), typeof(Leaderboard)]),
            (typeof(WeeklyFriendsLeaderboard), [typeof(WeeklyLeaderboardPeriod), typeof(Leaderboard)]),
            (typeof(WeeklyGroupLeaderboard), [typeof(WeeklyLeaderboardPeriod), typeof(Leaderboard), typeof(int)])
        ];

        foreach ((Type type, Type[] parameters) in models)
        {
            ConstructorInfo constructor = Assert.Single(
                type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
            Assert.Equal(parameters, constructor.GetParameters().Select(parameter => parameter.ParameterType));
            MethodInfo deconstruct = Assert.Single(
                type.GetMethods(BindingFlags.Instance | BindingFlags.Public),
                method => method.Name == "Deconstruct");
            Assert.Equal(parameters, deconstruct.GetParameters().Select(parameter => parameter.ParameterType.GetElementType()));
        }
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void habbicon_routes_roundtrip_exact_wire_shapes(ClientType client)
    {
        string count = client is ClientType.Flash ? "00000001" : "0001";
        string two = client is ClientType.Flash ? "00000002" : "0002";
        const string icon =
            "000000010001410000000200000003000000040000000500000006";
        string collection =
            "0000000A000143010000000B000000010000000C0000000D0000000E" +
            count + icon;

        _ = AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.ShopRequest,
            client,
            Direction.Out,
            "");
        HabbiconShopData shop = AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.ShopSnapshot,
            client,
            Direction.In,
            count + collection);
        Assert.Equal("C", Assert.Single(shop.Collections).Name);
        Assert.Equal("A", Assert.Single(Assert.Single(shop.Collections).Habbicons).Name);

        UserHabbicons inventory = AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.InventorySnapshot,
            client,
            Direction.In,
            count + "0000000100000002" + two + "0000000100000002");
        Assert.True(inventory.RecentHabbiconIdsPresent);
        Assert.Equal([1, 2], inventory.RecentHabbiconIds);

        UserHabbiconStatusChanged status = AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.StatusUpdated,
            client,
            Direction.In,
            "0000000100000003");
        Assert.Equal(HabbiconState.Favorite, status.State);

        HabbiconInfoRequest info_request = AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.InfoRequest,
            client,
            Direction.Out,
            "00000001");
        Assert.Equal(1, info_request.HabbiconId);

        HabbiconInfo info = AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.InfoSnapshot,
            client,
            Direction.In,
            icon);
        Assert.Equal("A", info.Habbicon.Name);

        RoomUseHabbicon used = AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.RoomUsed,
            client,
            Direction.In,
            "0000000700000001");
        Assert.Equal((7, 1), (used.RoomIndex, used.HabbiconId));

        if (client is ClientType.Unity)
        {
            UserHabbicons compact = AssertAchievementBadgeFixture(
                MessageContracts.Habbicons.InventorySnapshot,
                client,
                Direction.In,
                count + "0000000100000002");
            Assert.False(compact.RecentHabbiconIdsPresent);
            Assert.Empty(compact.RecentHabbiconIds);
            return;
        }

        Assert.Equal(1, AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.Buy,
            client,
            Direction.Out,
            "00000001").HabbiconId);
        Assert.Equal(10, AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.BuyCollection,
            client,
            Direction.Out,
            "0000000A").CollectionId);
        Assert.Equal(1, AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.Claim,
            client,
            Direction.Out,
            "00000001").HabbiconId);
        Assert.Equal(1, AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.Favorite,
            client,
            Direction.Out,
            "00000001").HabbiconId);
        Assert.Equal(1, AssertAchievementBadgeFixture(
            MessageContracts.Habbicons.Unfavorite,
            client,
            Direction.Out,
            "00000001").HabbiconId);
    }

    [Fact]
    public void habbicon_models_freeze_bound_and_compose_atomically()
    {
        var icon = new Habbicon(1, "A", 2, HabbiconState.Owned, 3, 4, 5);
        var mutable_icons = new List<Habbicon> { icon };
        var collection = new HabbiconCollection(
            2,
            "C",
            false,
            3,
            HabbiconState.Claimable,
            4,
            5,
            6,
            mutable_icons);
        mutable_icons.Clear();
        Assert.Same(icon, Assert.Single(collection.Habbicons));

        var mutable_collections = new List<HabbiconCollection> { collection };
        var shop = new HabbiconShopData(mutable_collections);
        mutable_collections.Clear();
        Assert.Same(collection, Assert.Single(shop.Collections));

        var mutable_states = new List<UserHabbiconState>
        {
            new(1, HabbiconState.Owned)
        };
        var mutable_recent = new List<int> { 1, 2 };
        var inventory = new UserHabbicons(mutable_states, mutable_recent);
        mutable_states.Clear();
        mutable_recent.Clear();
        Assert.Single(inventory.Habbicons);
        Assert.Equal([1, 2], inventory.RecentHabbiconIds);

        Assert.Throws<ArgumentNullException>(() => icon with { Name = null! });
        Assert.Throws<ArgumentNullException>(() => collection with { Name = null! });
        Assert.Throws<ArgumentNullException>(() => collection with { Habbicons = null! });
        Assert.Throws<ArgumentNullException>(() => collection with { Habbicons = [null!] });
        Assert.Throws<ArgumentNullException>(() => new HabbiconShopData(null!));
        Assert.Throws<ArgumentNullException>(() => new HabbiconShopData([null!]));
        Assert.Throws<ArgumentNullException>(() => new UserHabbicons(null!, []));
        Assert.Throws<ArgumentNullException>(() => new UserHabbicons([], null!));
        Assert.Throws<ArgumentNullException>(() => new HabbiconInfo(null!));
        Assert.Throws<InvalidDataException>(() =>
            new HabbiconShopData(new HabbiconCollection[ushort.MaxValue + 1]));

        AssertAchievementBadgeComposeFails<HabbiconShopData, InvalidDataException>(
            MessageContracts.Habbicons.ShopSnapshot,
            new HabbiconShopData(
            [
                collection with
                {
                    Habbicons =
                    [
                        icon with { Name = new string('x', ushort.MaxValue + 1) }
                    ]
                }
            ]),
            ClientType.Flash,
            Direction.In);

        string maximum = new('x', ushort.MaxValue);
        var heavy = icon with { Name = maximum };
        AssertAchievementBadgeComposeFails<HabbiconShopData, InvalidDataException>(
            MessageContracts.Habbicons.ShopSnapshot,
            new HabbiconShopData(
            [
                collection with { Habbicons = Enumerable.Repeat(heavy, 257).ToArray() }
            ]),
            ClientType.Unity,
            Direction.In);

        AssertAchievementBadgeComposeFails<UserHabbicons, InvalidOperationException>(
            MessageContracts.Habbicons.InventorySnapshot,
            inventory with { RecentHabbiconIdsPresent = false },
            ClientType.Unity,
            Direction.In);

        AssertAchievementBadgeParseFails(
            MessageContracts.Habbicons.ShopSnapshot,
            ClientType.Flash,
            Direction.In,
            "FFFFFFFF");
        AssertAchievementBadgeParseFails(
            MessageContracts.Habbicons.ShopSnapshot,
            ClientType.Flash,
            Direction.In,
            "0000000200000000");
        AssertAchievementBadgeParseFails(
            MessageContracts.Habbicons.StatusUpdated,
            ClientType.Unity,
            Direction.In,
            "000000010000000200");
        AssertAchievementBadgeParseFails(
            MessageContracts.Habbicons.InfoRequest,
            ClientType.Flash,
            Direction.Out,
            "0000000100");
        AssertAchievementBadgeParseFails(
            MessageContracts.Habbicons.ShopRequest,
            ClientType.Unity,
            Direction.Out,
            "00");

        IMessageContract[] contracts =
        [
            MessageContracts.Habbicons.ShopRequest,
            MessageContracts.Habbicons.ShopSnapshot,
            MessageContracts.Habbicons.InventorySnapshot,
            MessageContracts.Habbicons.StatusUpdated,
            MessageContracts.Habbicons.InfoRequest,
            MessageContracts.Habbicons.InfoSnapshot,
            MessageContracts.Habbicons.RoomUsed,
            MessageContracts.Habbicons.Buy,
            MessageContracts.Habbicons.BuyCollection,
            MessageContracts.Habbicons.Claim,
            MessageContracts.Habbicons.Favorite,
            MessageContracts.Habbicons.Unfavorite
        ];
        IMessageContract[] all = MessageContracts.All.ToArray();
        int start = Array.IndexOf(all, MessageContracts.Habbicons.ShopRequest);
        Assert.True(start >= 0);
        Assert.Equal(contracts, all.Skip(start).Take(contracts.Length));
        Assert.All(contracts.Take(7), contract =>
        {
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.True(contract.Supports(ClientType.Unity));
        });
        Assert.All(contracts.Skip(7), contract =>
        {
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.False(contract.Supports(ClientType.Unity));
        });
        AssertAchievementBadgeComposeFails<HabbiconBuyRequest, UnsupportedClientException>(
            MessageContracts.Habbicons.Buy,
            new HabbiconBuyRequest(1),
            ClientType.Unity,
            Direction.Out);
    }

    [Fact]
    public void habbicon_public_model_abi_remains_positional_and_exact()
    {
        (Type Type, Type[] Parameters)[] models =
        [
            (typeof(Habbicon), [typeof(int), typeof(string), typeof(int), typeof(HabbiconState), typeof(int), typeof(int), typeof(int)]),
            (typeof(HabbiconCollection), [typeof(int), typeof(string), typeof(bool), typeof(int), typeof(HabbiconState), typeof(int), typeof(int), typeof(int), typeof(IReadOnlyList<Habbicon>)]),
            (typeof(UserHabbicons), [typeof(IReadOnlyList<UserHabbiconState>), typeof(IReadOnlyList<int>)]),
            (typeof(UserHabbiconState), [typeof(int), typeof(HabbiconState)]),
            (typeof(UserHabbiconStatusChanged), [typeof(int), typeof(HabbiconState)]),
            (typeof(HabbiconShopData), [typeof(IReadOnlyList<HabbiconCollection>)]),
            (typeof(HabbiconInfo), [typeof(Habbicon)]),
            (typeof(RoomUseHabbicon), [typeof(int), typeof(int)])
        ];

        foreach ((Type type, Type[] parameters) in models)
        {
            ConstructorInfo constructor = Assert.Single(
                type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
            Assert.Equal(parameters, constructor.GetParameters().Select(parameter => parameter.ParameterType));
            MethodInfo deconstruct = Assert.Single(
                type.GetMethods(BindingFlags.Instance | BindingFlags.Public),
                method => method.Name == "Deconstruct");
            Assert.Equal(
                parameters,
                deconstruct.GetParameters().Select(parameter => parameter.ParameterType.GetElementType()));
        }

        Assert.True(new UserHabbicons([], []).RecentHabbiconIdsPresent);
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void room_object_read_routes_roundtrip_and_freeze(ClientType client)
    {
        Id id = client is ClientType.Unity ? 9_000_000_601 : 601;
        Id owner_id = (long)id + 1;
        GetPetInfoRequest pet_request = RoundtripRoomObject(
            MessageContracts.Room.Occupants.Pet.InfoRequest,
            new GetPetInfoRequest(id),
            client,
            Direction.Out);
        Assert.Equal(id, pet_request.PetId);

        var mutable_thresholds = new List<int> { 2, 4, 8 };
        var pet = new PetInfo
        {
            Id = id,
            Name = "Pet",
            Level = 3,
            MaxLevel = 20,
            Experience = 40,
            MaxExperience = 80,
            Energy = 50,
            MaxEnergy = 100,
            Happiness = 60,
            MaxHappiness = 120,
            Scratches = 7,
            OwnerId = owner_id,
            Age = 9,
            OwnerName = "Owner",
            BreedId = 11,
            HasFreeSaddle = true,
            IsRiding = false,
            SkillThresholds = mutable_thresholds,
            AccessRights = 13,
            CanBreed = true,
            CanHarvest = false,
            CanRevive = true,
            RarityLevel = 15,
            MaxWellbeingSeconds = 16,
            RemainingWellbeingSeconds = 17,
            RemainingGrowingSeconds = 18,
            HasBreedingPermission = true
        };
        mutable_thresholds.Clear();
        Assert.Equal([2, 4, 8], pet.SkillThresholds);
        PetInfo parsed_pet = RoundtripRoomObject(
            MessageContracts.Room.Occupants.Pet.Info,
            pet,
            client,
            Direction.In);
        Assert.Equal(id, parsed_pet.Id);
        Assert.Equal(owner_id, parsed_pet.OwnerId);
        Assert.Equal("Pet", parsed_pet.Name);
        Assert.Equal("Owner", parsed_pet.OwnerName);
        Assert.Equal([2, 4, 8], parsed_pet.SkillThresholds);
        Assert.True(Assert.IsAssignableFrom<IList<int>>(parsed_pet.SkillThresholds).IsReadOnly);
        Assert.True(parsed_pet.HasBreedingPermission);

        GetStickyDataRequest sticky_request = RoundtripRoomObject(
            MessageContracts.Room.WallItem.StickyDataRequest,
            new GetStickyDataRequest(id),
            client,
            Direction.Out);
        Assert.Equal(id, sticky_request.ItemId);
        Sticky sticky = RoundtripRoomObject(
            MessageContracts.Room.WallItem.StickyData,
            new Sticky(id, "yellow", "note text"),
            client,
            Direction.In);
        Assert.Equal((id, "yellow", "note text"), (sticky.Id, sticky.Color, sticky.Text));
        Assert.Throws<ArgumentNullException>(() => sticky with { Color = null! });
        Assert.Throws<ArgumentNullException>(() => sticky with { Text = null! });
    }

    [Fact]
    public void room_object_read_contracts_are_bounded_atomic_and_manifested()
    {
        IMessageContract[] all = MessageContracts.All.ToArray();
        Assert.Equal(
            Array.IndexOf(all, MessageContracts.Room.Occupants.Pet.Figure) + 1,
            Array.IndexOf(all, MessageContracts.Room.Occupants.Pet.InfoRequest));
        Assert.Equal(
            Array.IndexOf(all, MessageContracts.Room.Occupants.Pet.InfoRequest) + 1,
            Array.IndexOf(all, MessageContracts.Room.Occupants.Pet.Info));
        Assert.Equal(
            Array.IndexOf(all, MessageContracts.Room.WallItem.StickyDataSet) + 1,
            Array.IndexOf(all, MessageContracts.Room.WallItem.StickyDataRequest));
        Assert.Equal(
            Array.IndexOf(all, MessageContracts.Room.WallItem.StickyDataRequest) + 1,
            Array.IndexOf(all, MessageContracts.Room.WallItem.StickyData));

        IMessageContract[] contracts =
        [
            MessageContracts.Room.Occupants.Pet.InfoRequest,
            MessageContracts.Room.Occupants.Pet.Info,
            MessageContracts.Room.WallItem.StickyDataRequest,
            MessageContracts.Room.WallItem.StickyData
        ];
        Assert.All(contracts, contract =>
        {
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.True(contract.Supports(ClientType.Unity));
        });

        AssertAchievementBadgeComposeFails<GetPetInfoRequest, OverflowException>(
            MessageContracts.Room.Occupants.Pet.InfoRequest,
            new GetPetInfoRequest(9_000_000_001),
            ClientType.Flash,
            Direction.Out);
        AssertAchievementBadgeComposeFails<Sticky, InvalidDataException>(
            MessageContracts.Room.WallItem.StickyData,
            new Sticky(1, "yellow", new string('x', ushort.MaxValue + 1)),
            ClientType.Unity,
            Direction.In);
        AssertAchievementBadgeComposeFails<PetInfo, InvalidDataException>(
            MessageContracts.Room.Occupants.Pet.Info,
            new PetInfo { Id = 1, OwnerId = 2, Name = new string('x', ushort.MaxValue + 1) },
            ClientType.Flash,
            Direction.In);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PetInfo
        {
            SkillThresholds = new int[ushort.MaxValue + 1]
        });

        MessageRegistry registry = MessagesIniParser.ParseEmbeddedRegistry();
        Assert.True(registry.TryGet(MessageKeys.Room.Occupants.Pet.InfoRequest, out MessageDescriptor pet_request));
        Assert.Equal(["GetPetInfo"], pet_request.NamesFor(ClientType.Flash));
        Assert.Equal(["GetNewPetInfo"], pet_request.NamesFor(ClientType.Unity));
        Assert.True(registry.TryGet(MessageKeys.Room.Occupants.Pet.Info, out MessageDescriptor pet_info));
        Assert.Equal(["PetInfo"], pet_info.NamesFor(ClientType.Flash));
        Assert.Equal(["PetInfo"], pet_info.NamesFor(ClientType.Unity));
        Assert.True(registry.TryGet(MessageKeys.Room.WallItem.StickyDataRequest, out MessageDescriptor sticky_request));
        Assert.Equal(["GetItemData"], sticky_request.NamesFor(ClientType.Flash));
        Assert.Equal(["GetItemData"], sticky_request.NamesFor(ClientType.Unity));
        Assert.True(registry.TryGet(MessageKeys.Room.WallItem.StickyData, out MessageDescriptor sticky_data));
        Assert.Equal(["ItemDataUpdate"], sticky_data.NamesFor(ClientType.Flash));
        Assert.Equal(["ItemData"], sticky_data.NamesFor(ClientType.Unity));
    }

    [Fact]
    public void room_ad_info_is_flash_only_bounded_atomic_and_frozen()
    {
        IMessageContract[] all = MessageContracts.All.ToArray();
        Assert.Equal(
            Array.IndexOf(all, MessageContracts.Catalog.Published) + 1,
            Array.IndexOf(all, MessageContracts.Catalog.RoomAdInfoRequest));
        Assert.Equal(
            Array.IndexOf(all, MessageContracts.Catalog.RoomAdInfoRequest) + 1,
            Array.IndexOf(all, MessageContracts.Catalog.RoomAdInfo));
        Assert.True(MessageContracts.Catalog.RoomAdInfoRequest.Supports(ClientType.Flash));
        Assert.False(MessageContracts.Catalog.RoomAdInfoRequest.Supports(ClientType.Unity));
        Assert.True(MessageContracts.Catalog.RoomAdInfo.Supports(ClientType.Flash));
        Assert.False(MessageContracts.Catalog.RoomAdInfo.Supports(ClientType.Unity));

        GetRoomAdPurchaseInfo request = RoundtripRoomObject(
            MessageContracts.Catalog.RoomAdInfoRequest,
            new GetRoomAdPurchaseInfo(),
            ClientType.Flash,
            Direction.Out);
        Assert.NotNull(request);

        var mutable_rooms = new List<RoomAdRoom>
        {
            new(41, "First", true),
            new(42, "Second", false)
        };
        var info = new RoomAdPurchaseInfo(true, mutable_rooms);
        mutable_rooms.Clear();
        Assert.Equal([41L, 42L], info.Rooms.Select(room => (long)room.RoomId));
        RoomAdPurchaseInfo parsed = RoundtripRoomObject(
            MessageContracts.Catalog.RoomAdInfo,
            info,
            ClientType.Flash,
            Direction.In);
        Assert.True(parsed.IsVip);
        Assert.Equal(["First", "Second"], parsed.Rooms.Select(room => room.RoomName));
        Assert.True(Assert.IsAssignableFrom<IList<RoomAdRoom>>(parsed.Rooms).IsReadOnly);
        Assert.Throws<ArgumentNullException>(() => info with { Rooms = null! });
        Assert.Throws<ArgumentNullException>(() => info with { Rooms = [null!] });
        Assert.Throws<ArgumentNullException>(() => parsed.Rooms[0] with { RoomName = null! });

        AssertAchievementBadgeComposeFails<GetRoomAdPurchaseInfo, UnsupportedClientException>(
            MessageContracts.Catalog.RoomAdInfoRequest,
            new GetRoomAdPurchaseInfo(),
            ClientType.Unity,
            Direction.Out);
        AssertAchievementBadgeComposeFails<RoomAdPurchaseInfo, UnsupportedClientException>(
            MessageContracts.Catalog.RoomAdInfo,
            info,
            ClientType.Unity,
            Direction.In);
        AssertAchievementBadgeComposeFails<RoomAdPurchaseInfo, InvalidDataException>(
            MessageContracts.Catalog.RoomAdInfo,
            new RoomAdPurchaseInfo(false, [new RoomAdRoom(1, new string('x', ushort.MaxValue + 1), false)]),
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeComposeFails<RoomAdPurchaseInfo, OverflowException>(
            MessageContracts.Catalog.RoomAdInfo,
            new RoomAdPurchaseInfo(false, [new RoomAdRoom(9_000_000_001, "wide", false)]),
            ClientType.Flash,
            Direction.In);
        AssertAchievementBadgeParseFails(
            MessageContracts.Catalog.RoomAdInfo,
            ClientType.Flash,
            Direction.In,
            "01FFFFFFFF");
        AssertAchievementBadgeParseFails(
            MessageContracts.Catalog.RoomAdInfo,
            ClientType.Flash,
            Direction.In,
            "010000000100000001");

        MessageRegistry registry = MessagesIniParser.ParseEmbeddedRegistry();
        Assert.True(registry.TryGet(MessageKeys.Catalog.RoomAdInfoRequest, out MessageDescriptor request_descriptor));
        Assert.Equal(["GetRoomAdPurchaseInfo"], request_descriptor.NamesFor(ClientType.Flash));
        Assert.Empty(request_descriptor.NamesFor(ClientType.Unity));
        Assert.True(registry.TryGet(MessageKeys.Catalog.RoomAdInfo, out MessageDescriptor info_descriptor));
        Assert.Equal(["RoomAdPurchaseInfo"], info_descriptor.NamesFor(ClientType.Flash));
        Assert.Empty(info_descriptor.NamesFor(ClientType.Unity));
    }

    [Fact]
    public void forum_flash_responses_roundtrip_without_unity_guesswork()
    {
        var summary = new ForumSummary(
            10,
            "Forum",
            "Description",
            "Icon",
            7,
            -2,
            20,
            3,
            30,
            40,
            "Author",
            60);
        var permissions = new ForumPermissions(
            0,
            1,
            2,
            3,
            "",
            "members_only",
            "",
            "admins_only",
            "disabled");
        var details = new ForumDetails(summary, permissions, true, false);
        var thread = new Qx.Model.Forums.ForumThread(
            20,
            1,
            "Author",
            "Header",
            true,
            false,
            30,
            5,
            2,
            31,
            2,
            "Latest",
            8,
            10,
            3,
            "Moderator",
            4);
        var post = new ForumPost(
            30,
            3,
            1,
            "Author",
            "hd-1-1",
            12,
            "Text",
            20,
            3,
            "Moderator",
            4,
            77);

        RoundtripForum(
            MessageContracts.Forums.Stats,
            new ForumData(details),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Forums.List,
            new ForumsList(ForumListCode.Active, 1, 0, [summary]),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Forums.Threads,
            new ForumThreads(10, 0, [thread]),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Forums.Messages,
            new ThreadMessages(10, 20, 0, [post]),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Forums.ThreadCreated,
            new PostThread(10, thread),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Forums.MessageCreated,
            new PostMessage(10, 20, post),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Forums.ThreadUpdated,
            new UpdateThread(10, thread),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Forums.MessageUpdated,
            new UpdateMessage(10, 20, post),
            ClientType.Flash,
            Direction.In);
        RoundtripForum(
            MessageContracts.Forums.UnreadCount,
            new UnreadForumsCount(7),
            ClientType.Flash,
            Direction.In);

        IMessageContract[] incoming =
        [
            MessageContracts.Forums.Stats,
            MessageContracts.Forums.List,
            MessageContracts.Forums.Threads,
            MessageContracts.Forums.Messages,
            MessageContracts.Forums.ThreadCreated,
            MessageContracts.Forums.MessageCreated,
            MessageContracts.Forums.ThreadUpdated,
            MessageContracts.Forums.MessageUpdated,
            MessageContracts.Forums.UnreadCount
        ];
        Assert.All(incoming, contract =>
        {
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.False(contract.Supports(ClientType.Unity));
        });
    }

    [Theory]
    [InlineData(ClientType.Flash)]
    [InlineData(ClientType.Unity)]
    public void forum_requests_roundtrip_every_verified_route(ClientType client)
    {
        Id group_id = client is ClientType.Flash ? 10 : 0x0102030405060708L;
        RoundtripForum(
            MessageContracts.Forums.StatsRequest,
            new GetForumStats(group_id),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.ListRequest,
            new GetForumsList(ForumListCode.Popular, 2, 20),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.ThreadsRequest,
            new GetForumThreads(group_id, 2, 20),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.MessagesRequest,
            new GetForumThreadMessages(group_id, 20, 3, 20),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.ThreadRequest,
            new GetForumThread(group_id, 20),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.UnreadCountRequest,
            new GetUnreadForumsCount(),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.Post,
            new PostMessage(group_id, 20, "Subject", "Body"),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.ThreadModerate,
            new ModerateForumThread(group_id, 20, 10),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.MessageModerate,
            new ModerateForumMessage(group_id, 20, 30, 20),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.SettingsUpdate,
            new UpdateForumSettings(group_id, 0, 1, 2, 3),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.ReadMarkersUpdate,
            new UpdateForumReadMarkers([new ForumReadMarker(group_id, 30, true)]),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.ThreadUpdate,
            new UpdateThread(group_id, 20, true, false),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.ThreadReport,
            new CallForHelpFromForumThread(
                group_id,
                20,
                3,
                "Report",
                client is ClientType.Flash ? "First" : "",
                client is ClientType.Flash ? "Second" : ""),
            client,
            Direction.Out);
        RoundtripForum(
            MessageContracts.Forums.MessageReport,
            new CallForHelpFromForumMessage(
                group_id,
                20,
                30,
                4,
                "Report",
                client is ClientType.Flash ? "First" : "",
                client is ClientType.Flash ? "Second" : ""),
            client,
            Direction.Out);
    }

    [Fact]
    public void forum_models_freeze_bounds_and_preflight_atomically()
    {
        var summary = new ForumSummary(
            10,
            "Forum",
            "Description",
            "Icon",
            1,
            2,
            3,
            0,
            4,
            5,
            "Author",
            6);
        var mutable = new List<ForumSummary> { summary };
        var list = new ForumsList(ForumListCode.Active, 1, 0, mutable);
        mutable.Clear();
        Assert.Single(list.Forums);
        var replacement = new List<ForumSummary> { summary with { Name = "Changed" } };
        ForumsList changed = list with { Forums = replacement };
        replacement.Clear();
        Assert.Equal("Changed", Assert.Single(changed.Forums).Name);

        Assert.Throws<ArgumentNullException>(() => new ForumsList(ForumListCode.Active, 0, 0, null!));
        Assert.Throws<ArgumentNullException>(() =>
            new ForumsList(ForumListCode.Active, 1, 0, [null!]));
        Assert.Throws<InvalidDataException>(() =>
            new ForumsList(
                ForumListCode.Active,
                0,
                0,
                new ForumSummary[ushort.MaxValue + 1]));

        AssertAchievementBadgeComposeFails<GetForumThread, InvalidDataException>(
            MessageContracts.Forums.ThreadRequest,
            new GetForumThread(1, long.MaxValue),
            ClientType.Unity,
            Direction.Out);
        AssertAchievementBadgeComposeFails<PostMessage, InvalidDataException>(
            MessageContracts.Forums.Post,
            new PostMessage(1, 2, new string('x', ushort.MaxValue + 1), "Body"),
            ClientType.Flash,
            Direction.Out);
        AssertAchievementBadgeComposeFails<CallForHelpFromForumThread, NotSupportedException>(
            MessageContracts.Forums.ThreadReport,
            new CallForHelpFromForumThread(1, 2, 3, "Report", "Context", ""),
            ClientType.Unity,
            Direction.Out);
        AssertAchievementBadgeParseFails(
            MessageContracts.Forums.List,
            ClientType.Flash,
            Direction.In,
            "000000000000000000000000FFFFFFFF");
        AssertAchievementBadgeParseFails(
            MessageContracts.Forums.Threads,
            ClientType.Flash,
            Direction.In,
            "000000010000000000000001");

        string maximum = new('x', ushort.MaxValue);
        ForumSummary heavy = summary with
        {
            Name = maximum,
            Description = maximum,
            Icon = maximum,
            LastMessageAuthorName = maximum
        };
        AssertAchievementBadgeComposeFails<ForumsList, InvalidDataException>(
            MessageContracts.Forums.List,
            new ForumsList(
                ForumListCode.Active,
                65,
                0,
                Enumerable.Repeat(heavy, 65).ToArray()),
            ClientType.Flash,
            Direction.In);
    }

    [Fact]
    public void embedded_manifest_has_the_flash_and_unity_shape()
    {
        string manifest = ReadManifest();
        int rows = 0;
        int incoming = 0;
        int outgoing = 0;
        int unity = 0;
        int flash = 0;
        int keys = 0;
        Direction direction = Direction.None;

        foreach (string raw_line in manifest.Split('\n'))
        {
            string line = raw_line.Trim();
            if (line.Length == 0 || line[0] == ';')
                continue;
            if (line[0] == '[')
            {
                direction = line switch
                {
                    "[Incoming]" => Direction.In,
                    "[Outgoing]" => Direction.Out,
                    _ => Direction.None
                };
                Assert.NotEqual(Direction.None, direction);
                continue;
            }

            int comment = line.IndexOf(';');
            if (comment >= 0)
                line = line[..comment].Trim();

            bool has_alias = false;
            foreach (string field in line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {
                Assert.False(field.StartsWith('!'), $"Separate alias remains in '{field}'.");
                int colon = field.IndexOf(':');
                Assert.True(colon > 0, $"Malformed manifest field '{field}'.");
                string runes = field[..colon];
                string name = field[(colon + 1)..];
                Assert.True(runes is "k" or "u" or "f" or "uf", $"Unsupported manifest runes '{runes}'.");
                Assert.NotEmpty(name);
                if (runes == "k")
                {
                    keys++;
                    continue;
                }
                if (name == "-")
                    continue;

                has_alias = true;
                if (runes.Contains('u'))
                    unity++;
                if (runes.Contains('f'))
                    flash++;
            }

            Assert.True(has_alias, $"Manifest row '{line}' has no Flash or Unity alias.");
            rows++;
            if (direction == Direction.In)
                incoming++;
            else
            {
                Assert.Equal(Direction.Out, direction);
                outgoing++;
            }
        }

        MessageRegistry registry = MessagesIniParser.ParseEmbeddedRegistry();
        Assert.Equal(1571, rows);
        Assert.Equal(803, incoming);
        Assert.Equal(768, outgoing);
        Assert.Equal(1182, unity);
        Assert.Equal(1180, flash);
        Assert.Equal(509, keys);
        Assert.Equal(rows, registry.Count);
        Assert.Equal(unity + flash, registry.AliasCount);
        Assert.All(registry.Descriptors, descriptor =>
            Assert.All(descriptor.Aliases, alias =>
                Assert.True(alias.Client is ClientType.Flash or ClientType.Unity)));

        var contracts = new MessageContractCatalog(registry, MessageContracts.All);
        (MessageKey key, string[] flash, string[] unity, Direction direction, Type message_type,
            bool flash_supported, bool unity_supported)[] achievement_badge_routes =
        [
            (MessageKeys.Achievements.Request,
                ["GetAchievements"], ["GetUserAchievements"], Direction.Out,
                typeof(AchievementsRequest), true, true),
            (MessageKeys.Achievements.Snapshot,
                ["Achievements"], ["PossibleUserAchievements"], Direction.In,
                typeof(Qx.Model.Messages.Incoming.Achievements), true, true),
            (MessageKeys.Achievements.Updated,
                ["Achievement"], ["PossibleAchievement"], Direction.In,
                typeof(AchievementUpdate), true, true),
            (MessageKeys.Achievements.Score,
                ["AchievementsScore"], ["AchievementScore"], Direction.In,
                typeof(AchievementScore), true, false),
            (MessageKeys.Achievements.PointLimitsRequest,
                ["GetBadgePointLimits"], ["GetBadgePointLimits"], Direction.Out,
                typeof(BadgePointLimitsRequest), true, true),
            (MessageKeys.Achievements.PointLimits,
                ["BadgePointLimits"], ["BadgePointLimits"], Direction.In,
                typeof(BadgePointLimits), true, true),
            (MessageKeys.Achievements.Notification,
                ["HabboAchievementNotification"], ["AchievementNotification"], Direction.In,
                typeof(AchievementNotification), true, true),
            (MessageKeys.Badges.Request,
                ["GetBadges"], ["GetAvailableBadges"], Direction.Out,
                typeof(BadgeInventoryRequest), true, true),
            (MessageKeys.Badges.Snapshot,
                ["Badges"], ["AvailableBadges"], Direction.In,
                typeof(BadgeInventory), true, true),
            (MessageKeys.Badges.SelectedRequest,
                ["GetSelectedBadges"], ["GetSelectedBadges"], Direction.Out,
                typeof(SelectedBadgesRequest), true, true),
            (MessageKeys.Badges.Received,
                ["BadgeReceived"], ["BadgeReceived"], Direction.In,
                typeof(BadgeReceived), true, true),
            (MessageKeys.Badges.Selected,
                ["HabboUserBadges"], ["SelectedBadges"], Direction.In,
                typeof(UserBadges), true, true)
        ];
        Assert.Equal(7, ReadMessageKeys(typeof(MessageKeys.Achievements)).Length);
        Assert.Equal(5, ReadMessageKeys(typeof(MessageKeys.Badges)).Length);
        Assert.Equal(12, achievement_badge_routes.Select(route => route.key).Distinct().Count());
        Assert.Equal(
            achievement_badge_routes.Select(route => route.key),
            MessageContracts.All
                .Where(contract => achievement_badge_routes.Any(route => route.key == contract.Key))
                .Select(contract => contract.Key));
        Assert.Equal(
            24,
            achievement_badge_routes.Sum(route => route.flash.Length + route.unity.Length));
        Assert.All(achievement_badge_routes, route =>
        {
            Assert.True(registry.TryGet(route.key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Equal(route.direction, descriptor.Direction);
            Assert.Equal(route.flash, descriptor.NamesFor(ClientType.Flash));
            Assert.Equal(route.unity, descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(route.key, out IMessageContract contract));
            Assert.Equal(route.message_type, contract.MessageType);
            Assert.Equal(route.flash_supported, contract.Supports(ClientType.Flash));
            Assert.Equal(route.unity_supported, contract.Supports(ClientType.Unity));
        });

        (MessageKey key, string[] flash, string[] unity, Direction direction, Type message_type,
            bool flash_supported, bool unity_supported)[] earning_routes =
        [
            (MessageKeys.Earnings.StatusRequest,
                ["IncomeRewardStatus"], ["EarningStatus"], Direction.Out,
                typeof(EarningStatusRequest), true, true),
            (MessageKeys.Earnings.StatusSnapshot,
                ["IncomeRewardStatus"], ["EarningStatus"], Direction.In,
                typeof(EarningStatus), true, true),
            (MessageKeys.Earnings.Claim,
                ["IncomeRewardClaim"], ["ClaimEarning"], Direction.Out,
                typeof(EarningClaimRequest), true, true),
            (MessageKeys.Earnings.Claimed,
                ["IncomeRewardClaimResponse"], ["ClaimEarningResult"], Direction.In,
                typeof(EarningClaimResult), true, true),
            (MessageKeys.Earnings.Notification,
                ["IncomeRewardNotification"], [], Direction.In,
                typeof(EarningNotification), true, false)
        ];
        Assert.Equal(5, ReadMessageKeys(typeof(MessageKeys.Earnings)).Length);
        Assert.Equal(5, earning_routes.Select(route => route.key).Distinct().Count());
        Assert.Equal(
            earning_routes.Select(route => route.key),
            MessageContracts.All
                .Where(contract => earning_routes.Any(route => route.key == contract.Key))
                .Select(contract => contract.Key));
        Assert.Equal(9, earning_routes.Sum(route => route.flash.Length + route.unity.Length));
        Assert.All(earning_routes, route =>
        {
            Assert.True(registry.TryGet(route.key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Equal(route.direction, descriptor.Direction);
            Assert.Equal(route.flash, descriptor.NamesFor(ClientType.Flash));
            Assert.Equal(route.unity, descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(route.key, out IMessageContract contract));
            Assert.Equal(route.message_type, contract.MessageType);
            Assert.Equal(route.flash_supported, contract.Supports(ClientType.Flash));
            Assert.Equal(route.unity_supported, contract.Supports(ClientType.Unity));
        });

        (MessageKey key, string alias, Direction direction, Type message_type)[]
            daily_task_routes =
            [
                (MessageKeys.DailyTasks.Request,
                    "GetDailyTasks", Direction.Out, typeof(DailyTaskListRequest)),
                (MessageKeys.DailyTasks.Snapshot,
                    "DailyTasksActiveList", Direction.In, typeof(DailyTasksActiveList)),
                (MessageKeys.DailyTasks.Added,
                    "DailyTasksTasksAdded", Direction.In, typeof(DailyTasksTasksAdded)),
                (MessageKeys.DailyTasks.Updated,
                    "DailyTasksTaskUpdate", Direction.In, typeof(DailyTasksTaskUpdate)),
                (MessageKeys.DailyTasks.Claim,
                    "ClaimDailyTask", Direction.Out, typeof(DailyTaskClaimRequest))
            ];
        Assert.Equal(5, ReadMessageKeys(typeof(MessageKeys.DailyTasks)).Length);
        Assert.Equal(5, daily_task_routes.Select(route => route.key).Distinct().Count());
        Assert.Equal(
            daily_task_routes.Select(route => route.key),
            MessageContracts.All
                .Where(contract => daily_task_routes.Any(route => route.key == contract.Key))
                .Select(contract => contract.Key));
        Assert.All(daily_task_routes, route =>
        {
            Assert.True(registry.TryGet(route.key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Equal(route.direction, descriptor.Direction);
            Assert.Equal([route.alias], descriptor.NamesFor(ClientType.Flash));
            Assert.Empty(descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(route.key, out IMessageContract contract));
            Assert.Equal(route.message_type, contract.MessageType);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.False(contract.Supports(ClientType.Unity));
        });

        (MessageKey key, string[] flash, string[] unity, Direction direction, Type message_type)[]
            quest_routes =
            [
                (MessageKeys.Quests.Request,
                    ["GetQuests"], ["GetQuests"], Direction.Out, typeof(GetQuests)),
                (MessageKeys.Quests.Snapshot,
                    ["Quests"], ["Quests"], Direction.In, typeof(Quests)),
                (MessageKeys.Quests.SeasonalRequest,
                    ["GetSeasonalQuestsOnly"], ["GetSeasonalQuests"], Direction.Out,
                    typeof(GetSeasonalQuests)),
                (MessageKeys.Quests.SeasonalSnapshot,
                    ["SeasonalQuests"], ["QuestsSeasonal"], Direction.In,
                    typeof(QuestsSeasonal)),
                (MessageKeys.Quests.Updated,
                    ["Quest"], ["Quest"], Direction.In, typeof(Quest)),
                (MessageKeys.Quests.Completed,
                    ["QuestCompleted"], ["QuestCompleted"], Direction.In,
                    typeof(QuestCompleted)),
                (MessageKeys.Quests.Cancelled,
                    ["QuestCancelled"], ["QuestCancelled"], Direction.In,
                    typeof(QuestCancelled)),
                (MessageKeys.Quests.DailyRequest,
                    ["GetDailyQuest"], ["GetDailyQuest"], Direction.Out,
                    typeof(GetDailyQuest)),
                (MessageKeys.Quests.Daily,
                    ["QuestDaily"], ["QuestDaily"], Direction.In, typeof(QuestDaily)),
                (MessageKeys.Quests.Accept,
                    ["AcceptQuest"], ["AcceptQuest"], Direction.Out, typeof(AcceptQuest)),
                (MessageKeys.Quests.Activate,
                    ["ActivateQuest"], ["ActivateQuest"], Direction.Out,
                    typeof(ActivateQuest)),
                (MessageKeys.Quests.Reject,
                    ["RejectQuest"], ["RejectQuest"], Direction.Out, typeof(RejectQuest)),
                (MessageKeys.Quests.Cancel,
                    ["CancelQuest"], ["CancelQuest"], Direction.Out, typeof(CancelQuest)),
                (MessageKeys.Quests.TrackerOpen,
                    ["OpenQuestTracker"], ["OpenQuestTracker"], Direction.Out,
                    typeof(OpenQuestTracker)),
                (MessageKeys.Quests.FriendRequestCompleted,
                    ["FriendRequestQuestComplete"], ["FriendRequestQuestComplete"], Direction.Out,
                    typeof(FriendRequestQuestComplete))
            ];
        Assert.Equal(15, ReadMessageKeys(typeof(MessageKeys.Quests)).Length);
        Assert.Equal(15, quest_routes.Select(route => route.key).Distinct().Count());
        Assert.Equal(
            quest_routes.Select(route => route.key),
            MessageContracts.All
                .Where(contract => quest_routes.Any(route => route.key == contract.Key))
                .Select(contract => contract.Key));
        Assert.Equal(30, quest_routes.Sum(route => route.flash.Length + route.unity.Length));
        Assert.All(quest_routes, route =>
        {
            Assert.True(registry.TryGet(route.key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Equal(route.direction, descriptor.Direction);
            Assert.Equal(route.flash, descriptor.NamesFor(ClientType.Flash));
            Assert.Equal(route.unity, descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(route.key, out IMessageContract contract));
            Assert.Equal(route.message_type, contract.MessageType);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.True(contract.Supports(ClientType.Unity));
        });

        (MessageKey key, string alias, Direction direction, Type message_type)[]
            leaderboard_routes =
            [
                (MessageKeys.Leaderboards.Total.Request,
                    "Game2GetTotalLeaderboard", Direction.Out, typeof(LeaderboardRequest)),
                (MessageKeys.Leaderboards.Total.Snapshot,
                    "Game2TotalLeaderboard", Direction.In, typeof(TotalLeaderboard)),
                (MessageKeys.Leaderboards.Friends.Request,
                    "Game2GetFriendsLeaderboard", Direction.Out, typeof(LeaderboardRequest)),
                (MessageKeys.Leaderboards.Friends.Snapshot,
                    "Game2FriendsLeaderboard", Direction.In, typeof(FriendsLeaderboard)),
                (MessageKeys.Leaderboards.Groups.Request,
                    "Game2GetTotalGroupLeaderboard", Direction.Out, typeof(LeaderboardRequest)),
                (MessageKeys.Leaderboards.Groups.Snapshot,
                    "Game2TotalGroupLeaderboard", Direction.In, typeof(TotalGroupLeaderboard)),
                (MessageKeys.Leaderboards.WeeklyTotal.Request,
                    "Game2GetWeeklyLeaderboard", Direction.Out, typeof(WeeklyLeaderboardRequest)),
                (MessageKeys.Leaderboards.WeeklyTotal.Snapshot,
                    "Game2WeeklyLeaderboard", Direction.In, typeof(WeeklyLeaderboard)),
                (MessageKeys.Leaderboards.WeeklyFriends.Request,
                    "Game2GetWeeklyFriendsLeaderboard", Direction.Out, typeof(WeeklyLeaderboardRequest)),
                (MessageKeys.Leaderboards.WeeklyFriends.Snapshot,
                    "Game2WeeklyFriendsLeaderboard", Direction.In, typeof(WeeklyFriendsLeaderboard)),
                (MessageKeys.Leaderboards.WeeklyGroups.Request,
                    "Game2GetWeeklyGroupLeaderboard", Direction.Out, typeof(WeeklyLeaderboardRequest)),
                (MessageKeys.Leaderboards.WeeklyGroups.Snapshot,
                    "Game2WeeklyGroupLeaderboard", Direction.In, typeof(WeeklyGroupLeaderboard))
            ];
        Assert.Equal(12, ReadMessageKeys(typeof(MessageKeys.Leaderboards)).Length);
        Assert.Equal(12, leaderboard_routes.Select(route => route.key).Distinct().Count());
        Assert.Equal(
            leaderboard_routes.Select(route => route.key),
            MessageContracts.All
                .Where(contract => leaderboard_routes.Any(route => route.key == contract.Key))
                .Select(contract => contract.Key));
        Assert.All(leaderboard_routes, route =>
        {
            Assert.True(registry.TryGet(route.key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Equal(route.direction, descriptor.Direction);
            Assert.Equal([route.alias], descriptor.NamesFor(ClientType.Flash));
            Assert.Empty(descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(route.key, out IMessageContract contract));
            Assert.Equal(route.message_type, contract.MessageType);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.False(contract.Supports(ClientType.Unity));
        });

        (MessageKey key, string[] flash, string[] unity, Direction direction, Type message_type,
            bool unity_supported)[] forum_routes =
        [
            (MessageKeys.Forums.Stats,
                ["ForumData"], ["ForumStats"], Direction.In, typeof(ForumData), false),
            (MessageKeys.Forums.List,
                ["ForumsList"], ["ForumsList"], Direction.In, typeof(ForumsList), false),
            (MessageKeys.Forums.Threads,
                ["ForumThreads"], ["ForumThreads"], Direction.In, typeof(ForumThreads), false),
            (MessageKeys.Forums.Messages,
                ["ThreadMessages"], ["ForumThreadMessages"], Direction.In,
                typeof(ThreadMessages), false),
            (MessageKeys.Forums.ThreadCreated,
                ["PostThread"], ["PostForumThreadOk"], Direction.In, typeof(PostThread), false),
            (MessageKeys.Forums.MessageCreated,
                ["PostMessage"], ["PostForumMessageOk"], Direction.In,
                typeof(PostMessage), false),
            (MessageKeys.Forums.ThreadUpdated,
                ["UpdateThread"], ["ForumThread"], Direction.In, typeof(UpdateThread), false),
            (MessageKeys.Forums.MessageUpdated,
                ["UpdateMessage"], ["ForumMessage"], Direction.In, typeof(UpdateMessage), false),
            (MessageKeys.Forums.UnreadCount,
                ["UnreadForumsCount"], ["UnreadForumsCount"], Direction.In,
                typeof(UnreadForumsCount), false),
            (MessageKeys.Forums.StatsRequest,
                ["GetForumStats"], ["GetForumStats"], Direction.Out, typeof(GetForumStats), true),
            (MessageKeys.Forums.ListRequest,
                ["GetForumsList"], ["GetForumsList"], Direction.Out, typeof(GetForumsList), true),
            (MessageKeys.Forums.ThreadsRequest,
                ["GetThreads"], ["GetForumThreads"], Direction.Out,
                typeof(GetForumThreads), true),
            (MessageKeys.Forums.MessagesRequest,
                ["GetMessages"], ["GetForumThreadMessages"], Direction.Out,
                typeof(GetForumThreadMessages), true),
            (MessageKeys.Forums.ThreadRequest,
                ["GetThread"], ["GetForumThread"], Direction.Out, typeof(GetForumThread), true),
            (MessageKeys.Forums.UnreadCountRequest,
                ["GetUnreadForumsCount"], ["GetUnreadForumsCount"], Direction.Out,
                typeof(GetUnreadForumsCount), true),
            (MessageKeys.Forums.Post,
                ["PostMessage"], ["PostForumMessage"], Direction.Out, typeof(PostMessage), true),
            (MessageKeys.Forums.ThreadModerate,
                ["ModerateThread"], ["ModerateForumThread"], Direction.Out,
                typeof(ModerateForumThread), true),
            (MessageKeys.Forums.MessageModerate,
                ["ModerateMessage"], ["ModerateForumMessage"], Direction.Out,
                typeof(ModerateForumMessage), true),
            (MessageKeys.Forums.SettingsUpdate,
                ["UpdateForumSettings"], ["UpdateForumSettings"], Direction.Out,
                typeof(UpdateForumSettings), true),
            (MessageKeys.Forums.ReadMarkersUpdate,
                ["UpdateForumReadMarker"], ["UpdateForumReadMarkers"], Direction.Out,
                typeof(UpdateForumReadMarkers), true),
            (MessageKeys.Forums.ThreadUpdate,
                ["UpdateThread"], ["UpdateForumThread"], Direction.Out,
                typeof(UpdateThread), true),
            (MessageKeys.Forums.ThreadReport,
                ["CallForHelpFromForumThread"], ["ReportForumThread"], Direction.Out,
                typeof(CallForHelpFromForumThread), true),
            (MessageKeys.Forums.MessageReport,
                ["CallForHelpFromForumMessage"], ["ReportForumMessage"], Direction.Out,
                typeof(CallForHelpFromForumMessage), true)
        ];
        Assert.Equal(23, ReadMessageKeys(typeof(MessageKeys.Forums)).Length);
        Assert.Equal(23, forum_routes.Select(route => route.key).Distinct().Count());
        Assert.Equal(
            forum_routes.Select(route => route.key),
            MessageContracts.All
                .Where(contract => forum_routes.Any(route => route.key == contract.Key))
                .Select(contract => contract.Key));
        Assert.Equal(46, forum_routes.Sum(route => route.flash.Length + route.unity.Length));
        Assert.All(forum_routes, route =>
        {
            Assert.True(registry.TryGet(route.key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Equal(route.direction, descriptor.Direction);
            Assert.Equal(route.flash, descriptor.NamesFor(ClientType.Flash));
            Assert.Equal(route.unity, descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(route.key, out IMessageContract contract));
            Assert.Equal(route.message_type, contract.MessageType);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.Equal(route.unity_supported, contract.Supports(ClientType.Unity));
        });

        MessageKey[] wallet_keys = ReadMessageKeys(typeof(MessageKeys.Wallet));
        Assert.Equal(4, wallet_keys.Length);
        Assert.Equal(4, wallet_keys.Distinct().Count());
        Assert.All(wallet_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.True(contract.Supports(ClientType.Unity));
        });
        MessageKey[] wallet_earning_order =
        [
            MessageKeys.Wallet.CreditsRequest,
            MessageKeys.Wallet.CreditsBalance,
            MessageKeys.Wallet.ActivityPoints,
            MessageKeys.Wallet.ActivityPointUpdated,
            MessageKeys.Earnings.StatusRequest,
            MessageKeys.Earnings.StatusSnapshot,
            MessageKeys.Earnings.Claim,
            MessageKeys.Earnings.Claimed,
            MessageKeys.Earnings.Notification,
            MessageKeys.DailyTasks.Request,
            MessageKeys.DailyTasks.Snapshot,
            MessageKeys.DailyTasks.Added,
            MessageKeys.DailyTasks.Updated,
            MessageKeys.DailyTasks.Claim
        ];
        Assert.Equal(
            wallet_earning_order,
            MessageContracts.All
                .Where(contract => wallet_earning_order.Contains(contract.Key))
                .Select(contract => contract.Key));

        MessageKey[] poll_keys = ReadMessageKeys(typeof(MessageKeys.Polls));
        Assert.Equal(6, poll_keys.Length);
        Assert.Equal(6, poll_keys.Distinct().Count());
        Assert.All(poll_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.True(contract.Supports(ClientType.Unity));
        });
        Assert.True(registry.TryGet(MessageKeys.Polls.Reject, out MessageDescriptor poll_reject));
        Assert.Equal(["PollReject"], poll_reject.NamesFor(ClientType.Flash));
        Assert.Equal(["RejectPoll"], poll_reject.NamesFor(ClientType.Unity));
        Assert.True(registry.TryGet(MessageKeys.Polls.Start, out MessageDescriptor poll_start));
        Assert.Equal(["PollStart"], poll_start.NamesFor(ClientType.Flash));
        Assert.Equal(["StartPoll"], poll_start.NamesFor(ClientType.Unity));

        MessageKey[] room_settings_keys = ReadMessageKeys(typeof(MessageKeys.Room.Settings));
        Assert.Equal(6, room_settings_keys.Distinct().Count());
        Assert.All(room_settings_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor) &&
                descriptor.HasExplicitKey);
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.All(
                new[] { ClientType.Flash, ClientType.Unity },
                client => Assert.True(contract.Supports(client)));
        });

        MessageKey[] placement_keys =
        [
            MessageKeys.Room.Item.Place,
            MessageKeys.Room.Item.Pickup,
            MessageKeys.Room.Item.PickupConfirmation,
            MessageKeys.Room.FloorItem.Move,
            MessageKeys.Room.FloorItem.Added,
            MessageKeys.Room.FloorItem.Updated,
            MessageKeys.Room.FloorItem.Removed,
            MessageKeys.Room.WallItem.Move,
            MessageKeys.Room.WallItem.Added,
            MessageKeys.Room.WallItem.Updated,
            MessageKeys.Room.WallItem.Removed
        ];
        Assert.Equal(11, placement_keys.Length);
        Assert.Equal(11, placement_keys.Distinct().Count());
        Assert.All(placement_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.Equal(
                key != MessageKeys.Room.Item.PickupConfirmation,
                contract.Supports(ClientType.Unity));
        });
        Assert.True(registry.TryGet(
            MessageKeys.Room.Item.Place,
            out MessageDescriptor item_place));
        Assert.Equal(["PlaceObject"], item_place.NamesFor(ClientType.Flash));
        Assert.Equal(
            ["PlaceRoomItem", "PlaceWallItem"],
            item_place.NamesFor(ClientType.Unity));

        MessageKey[] people_group_keys =
        [
            MessageKeys.Users.ExtendedProfileRequest,
            MessageKeys.Users.ExtendedProfileSnapshot,
            MessageKeys.Users.Relationship.Request,
            MessageKeys.Users.Relationship.Snapshot,
            MessageKeys.Badges.SelectedRequest,
            MessageKeys.Badges.Selected,
            MessageKeys.Groups.Details.Request,
            MessageKeys.Groups.Details.Snapshot,
            MessageKeys.Groups.Members.Request,
            MessageKeys.Groups.Members.Snapshot,
            MessageKeys.Groups.Memberships.Request,
            MessageKeys.Groups.Memberships.Snapshot
        ];
        Assert.Equal(12, people_group_keys.Length);
        Assert.Equal(12, people_group_keys.Distinct().Count());
        Assert.All(people_group_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Single(descriptor.NamesFor(ClientType.Flash));
            Assert.Single(descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.True(contract.Supports(ClientType.Unity));
        });
        Assert.Equal(
            24,
            people_group_keys.Sum(key =>
                registry.Descriptors.Single(descriptor => descriptor.Key == key).Aliases.Count));
        Assert.Equal(
            ["HabboUserBadges"],
            registry.Descriptors.Single(descriptor => descriptor.Key == MessageKeys.Badges.Selected)
                .NamesFor(ClientType.Flash));
        Assert.Equal(
            ["SelectedBadges"],
            registry.Descriptors.Single(descriptor => descriptor.Key == MessageKeys.Badges.Selected)
                .NamesFor(ClientType.Unity));

        MessageKey[] catalog_keys =
        [
            MessageKeys.Catalog.IndexRequest,
            MessageKeys.Catalog.IndexSnapshot,
            MessageKeys.Catalog.PageRequest,
            MessageKeys.Catalog.PageSnapshot,
            MessageKeys.Catalog.Purchase,
            MessageKeys.Catalog.PurchaseAccepted,
            MessageKeys.Catalog.PurchaseFailed,
            MessageKeys.Catalog.PurchaseForbidden,
            MessageKeys.Catalog.Published
        ];
        Assert.Equal(9, catalog_keys.Length);
        Assert.Equal(9, catalog_keys.Distinct().Count());
        Assert.All(catalog_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Single(descriptor.NamesFor(ClientType.Flash));
            Assert.Single(descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.True(contract.Supports(ClientType.Unity));
        });
        Assert.Equal(
            18,
            catalog_keys.Sum(key =>
                registry.Descriptors.Single(descriptor => descriptor.Key == key).Aliases.Count));
        Assert.Equal(
            ["PurchaseFromCatalog"],
            registry.Descriptors.Single(descriptor => descriptor.Key == MessageKeys.Catalog.Purchase)
                .NamesFor(ClientType.Flash));
        Assert.Equal(
            ["PurchaseOk"],
            registry.Descriptors.Single(
                descriptor => descriptor.Key == MessageKeys.Catalog.PurchaseAccepted)
                .NamesFor(ClientType.Unity));
        Assert.Equal(
            ["PurchaseError"],
            registry.Descriptors.Single(
                descriptor => descriptor.Key == MessageKeys.Catalog.PurchaseFailed)
                .NamesFor(ClientType.Flash));
        Assert.Equal(
            ["PurchaseNotAllowed"],
            registry.Descriptors.Single(
                descriptor => descriptor.Key == MessageKeys.Catalog.PurchaseForbidden)
                .NamesFor(ClientType.Unity));
        Assert.Equal(
            ["CatalogPublished"],
            registry.Descriptors.Single(descriptor => descriptor.Key == MessageKeys.Catalog.Published)
                .NamesFor(ClientType.Flash));
        Assert.Equal(
            ["CatalogExpired"],
            registry.Descriptors.Single(descriptor => descriptor.Key == MessageKeys.Catalog.Published)
                .NamesFor(ClientType.Unity));

        (MessageKey key, string[] flash, string[] unity, bool flash_supported, bool unity_supported)[]
            gift_routes =
            [
                (MessageKeys.Gifts.WrappingConfiguration,
                    ["GiftWrappingConfiguration"], ["GiftWrappingConfiguration"], true, true),
                (MessageKeys.Gifts.PresentOpened,
                    ["PresentOpened"], ["PresentOpen"], true, true),
                (MessageKeys.Gifts.ClubInfo,
                    ["ClubGiftInfo"], ["SelectableClubGiftInfo"], true, true),
                (MessageKeys.Gifts.ClubSelected,
                    ["ClubGiftSelected"], ["ClubGiftSelected"], true, true),
                (MessageKeys.Gifts.ReceiverNotFound,
                    ["GiftReceiverNotFound"], [], true, false),
                (MessageKeys.Gifts.ClubNotification,
                    ["ClubGiftNotification"], ["CSubscriptionUserGifts"], true, false),
                (MessageKeys.Gifts.OfferGiftability,
                    ["IsOfferGiftable"], ["IsOfferGiftable"], true, false),
                (MessageKeys.Gifts.NewUserOffer,
                    ["NewUserExperienceGiftOffer"], ["NuxGiftOffer"], true, false),
                (MessageKeys.Gifts.NewUserIncomplete,
                    ["NewUserExperienceNotComplete"], ["NuxNotComplete"], true, true),
                (MessageKeys.Gifts.WrappingConfigurationRequest,
                    ["GetGiftWrappingConfiguration"], ["GetGiftWrappingConfiguration"], true, true),
                (MessageKeys.Gifts.PresentOpen,
                    ["PresentOpen"], ["PresentOpen"], true, true),
                (MessageKeys.Gifts.Purchase,
                    ["PurchaseFromCatalogAsGift"], ["PurchaseFromCatalogAsGift"], true, true),
                (MessageKeys.Gifts.ClubInfoRequest,
                    ["GetClubGift"], ["GetSelectableClubGiftInfo"], true, true),
                (MessageKeys.Gifts.ClubSelect,
                    ["SelectClubGift"], ["SelectClubGift"], true, true),
                (MessageKeys.Gifts.OfferGiftabilityRequest,
                    ["GetIsOfferGiftable"], ["GetIsOfferGiftable"], true, true),
                (MessageKeys.Gifts.NewUserSelect,
                    ["NewUserExperienceGetGifts"], ["NuxGetGifts"], true, true),
                (MessageKeys.Gifts.NewUserAdvance,
                    ["NewUserExperienceScriptProceed"], ["ScriptProceed"], true, true)
            ];
        Assert.Equal(17, ReadMessageKeys(typeof(MessageKeys.Gifts)).Length);
        Assert.Equal(17, gift_routes.Select(route => route.key).Distinct().Count());
        Assert.All(gift_routes, route =>
        {
            Assert.True(registry.TryGet(route.key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Equal(route.flash, descriptor.NamesFor(ClientType.Flash));
            Assert.Equal(route.unity, descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(route.key, out IMessageContract contract));
            Assert.Equal(route.flash_supported, contract.Supports(ClientType.Flash));
            Assert.Equal(route.unity_supported, contract.Supports(ClientType.Unity));
        });
        Assert.True(registry.TryGet(
            ClientType.Unity,
            Direction.Out,
            "GiveGift",
            out MessageDescriptor raw_give_gift));
        Assert.False(raw_give_gift.HasExplicitKey);
        Assert.False(contracts.TryGet(raw_give_gift.Key, out _));
        Assert.True(registry.TryGet(
            ClientType.Flash,
            Direction.Out,
            "GetGift",
            out MessageDescriptor raw_get_gift));
        Assert.False(raw_get_gift.HasExplicitKey);
        Assert.False(contracts.TryGet(raw_get_gift.Key, out _));

        (MessageKey key, string[] flash, string[] unity, bool flash_supported, bool unity_supported)[]
            subscription_routes =
            [
                (MessageKeys.Subscriptions.UserInfo,
                    ["ScrSendUserInfo"], ["ScrSendUserInfo"], true, true),
                (MessageKeys.Subscriptions.UserInfoRequest,
                    ["ScrGetUserInfo"], ["SubscriptionGetUserInfo"], true, true),
                (MessageKeys.Subscriptions.KickbackInfo,
                    ["ScrSendKickbackInfo"], ["ScrSendKickbackInfo"], true, true),
                (MessageKeys.Subscriptions.KickbackInfoRequest,
                    ["ScrGetKickbackInfo"], ["SubscriptionGetKickbackInfo"], true, true),
                (MessageKeys.Subscriptions.ClubOffersSnapshot,
                    ["HabboClubOffers"], ["HabboClubOffers"], true, true),
                (MessageKeys.Subscriptions.ClubOffersRequest,
                    ["GetClubOffers"], ["GetHabboClubOffers"], true, true),
                (MessageKeys.Subscriptions.BuildersClubFurniCount,
                    ["BuildersClubFurniCount"], ["BuildersClubFurniCount"], true, true),
                (MessageKeys.Subscriptions.BuildersClubFurniCountRequest,
                    ["BuildersClubQueryFurniCount"], ["BuildersClubQueryFurniCount"], true, true),
                (MessageKeys.Subscriptions.BuildersClubMembershipStatus,
                    ["BuildersClubSubscriptionStatus"], ["BuildersClubMembershipStatus"], true, false),
                (MessageKeys.Subscriptions.BuildersClubPlacementWarning,
                    ["BuildersClubPlacementWarning"], [], true, false),
                (MessageKeys.Subscriptions.BuildersClubFloorOfferPlace,
                    ["BuildersClubPlaceRoomItem"], ["BuildersClubPlaceRoomItem"], true, true),
                (MessageKeys.Subscriptions.BuildersClubWallOfferPlace,
                    ["BuildersClubPlaceWallItem"], ["BuildersClubPlaceWallItem"], true, true)
            ];
        Assert.Equal(12, ReadMessageKeys(typeof(MessageKeys.Subscriptions)).Length);
        Assert.All(subscription_routes, route =>
        {
            Assert.True(registry.TryGet(route.key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Equal(route.flash, descriptor.NamesFor(ClientType.Flash));
            Assert.Equal(route.unity, descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(route.key, out IMessageContract contract));
            Assert.Equal(route.flash_supported, contract.Supports(ClientType.Flash));
            Assert.Equal(route.unity_supported, contract.Supports(ClientType.Unity));
        });

        (MessageKey key, string alias, Direction direction, Type message_type)[]
            crafting_routes =
            [
                (MessageKeys.Crafting.ProductsRequest,
                    "GetCraftableProducts", Direction.Out, typeof(GetCraftableProducts)),
                (MessageKeys.Crafting.ProductsSnapshot,
                    "CraftableProducts", Direction.In, typeof(CraftableProducts)),
                (MessageKeys.Crafting.RecipeRequest,
                    "GetCraftingRecipe", Direction.Out, typeof(GetCraftingRecipe)),
                (MessageKeys.Crafting.RecipeSnapshot,
                    "CraftingRecipe", Direction.In, typeof(CraftingRecipe)),
                (MessageKeys.Crafting.Craft,
                    "Craft", Direction.Out, typeof(Qx.Model.Messages.Incoming.Craft)),
                (MessageKeys.Crafting.SecretCraft,
                    "CraftSecret", Direction.Out, typeof(CraftSecret)),
                (MessageKeys.Crafting.AvailabilityRequest,
                    "GetCraftingRecipesAvailable", Direction.Out,
                    typeof(GetCraftingRecipesAvailable)),
                (MessageKeys.Crafting.AvailabilitySnapshot,
                    "CraftingRecipesAvailable", Direction.In,
                    typeof(CraftingRecipesAvailable)),
                (MessageKeys.Crafting.Result,
                    "CraftingResult", Direction.In, typeof(CraftingResult))
            ];
        Assert.Equal(9, ReadMessageKeys(typeof(MessageKeys.Crafting)).Length);
        Assert.Equal(9, crafting_routes.Select(route => route.key).Distinct().Count());
        Assert.Equal(
            crafting_routes.Select(route => route.key),
            MessageContracts.All
                .Where(contract => crafting_routes.Any(route => route.key == contract.Key))
                .Select(contract => contract.Key));
        Assert.All(crafting_routes, route =>
        {
            Assert.True(registry.TryGet(route.key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.Equal(route.direction, descriptor.Direction);
            Assert.Equal([route.alias], descriptor.NamesFor(ClientType.Flash));
            Assert.Equal([route.alias], descriptor.NamesFor(ClientType.Unity));
            Assert.True(contracts.TryGet(route.key, out IMessageContract contract));
            Assert.Equal(route.message_type, contract.MessageType);
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.True(contract.Supports(ClientType.Unity));
            Direction opposite = route.direction is Direction.In
                ? Direction.Out
                : Direction.In;
            Assert.False(registry.TryGet(ClientType.Flash, opposite, route.alias, out _));
            Assert.False(registry.TryGet(ClientType.Unity, opposite, route.alias, out _));
        });

        MessageKey[] marketplace_keys =
        [
            MessageKeys.Marketplace.Configuration.Request,
            MessageKeys.Marketplace.Configuration.Snapshot,
            MessageKeys.Marketplace.Eligibility.Request,
            MessageKeys.Marketplace.Eligibility.Result,
            MessageKeys.Marketplace.Credits.Redeem,
            MessageKeys.Marketplace.Tokens.Buy,
            MessageKeys.Marketplace.Offers.SearchRequest,
            MessageKeys.Marketplace.Offers.SearchResult,
            MessageKeys.Marketplace.Offers.OwnRequest,
            MessageKeys.Marketplace.Offers.OwnSnapshot,
            MessageKeys.Marketplace.Offers.Make,
            MessageKeys.Marketplace.Offers.MakeResult,
            MessageKeys.Marketplace.Offers.Buy,
            MessageKeys.Marketplace.Offers.BuyResult,
            MessageKeys.Marketplace.Offers.Cancel,
            MessageKeys.Marketplace.Offers.CancelResult,
            MessageKeys.Marketplace.Offers.CancelAll,
            MessageKeys.Marketplace.Offers.CancelAllResult,
            MessageKeys.Marketplace.Offers.ClearOwnHistory,
            MessageKeys.Marketplace.Offers.ClearOwnHistoryResult,
            MessageKeys.Marketplace.ItemStats.Request,
            MessageKeys.Marketplace.ItemStats.Snapshot
        ];
        Assert.Equal(22, marketplace_keys.Length);
        Assert.Equal(22, marketplace_keys.Distinct().Count());
        Assert.All(marketplace_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
        });
        Assert.True(registry.TryGet(
            ClientType.Unity,
            Direction.In,
            "MarketplaceCancelOfferResult",
            out MessageDescriptor raw_cancel_result));
        Assert.False(raw_cancel_result.HasExplicitKey);
        Assert.True(registry.TryGet(
            ClientType.Unity,
            Direction.Out,
            "MarketplaceGetItemStats",
            out MessageDescriptor raw_item_stats));
        Assert.False(raw_item_stats.HasExplicitKey);

        MessageKey[] wired_keys = ReadMessageKeys(typeof(MessageKeys.Wired));
        Assert.Equal(88, wired_keys.Length);
        Assert.Equal(88, wired_keys.Distinct().Count());
        Assert.All(wired_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.Equal(
                descriptor.NamesFor(ClientType.Flash).Count > 0,
                contract.Supports(ClientType.Flash));
            Assert.Equal(
                descriptor.NamesFor(ClientType.Unity).Count > 0,
                contract.Supports(ClientType.Unity));
        });

        MessageKey[] profile_keys =
        [
            .. ReadMessageKeys(typeof(MessageKeys.Users)),
            .. ReadMessageKeys(typeof(MessageKeys.Wardrobe))
        ];
        Assert.Equal(32, profile_keys.Length);
        Assert.Equal(32, profile_keys.Distinct().Count());
        Assert.All(profile_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.Equal(
                descriptor.NamesFor(ClientType.Flash).Count > 0,
                contract.Supports(ClientType.Flash));
            Assert.Equal(
                descriptor.NamesFor(ClientType.Unity).Count > 0,
                contract.Supports(ClientType.Unity));
        });

        MessageKey[] inventory_keys = ReadMessageKeys(typeof(MessageKeys.Inventory));
        Assert.Equal(12, inventory_keys.Length);
        Assert.Equal(12, inventory_keys.Distinct().Count());
        Assert.All(inventory_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.Equal(
                descriptor.NamesFor(ClientType.Flash).Count > 0,
                contract.Supports(ClientType.Flash));
            Assert.Equal(
                descriptor.NamesFor(ClientType.Unity).Count > 0,
                contract.Supports(ClientType.Unity));
        });
        Assert.True(contracts.TryGet(
            MessageKeys.Inventory.Furni.RemovedMultiple,
            out IMessageContract removed_multiple));
        Assert.True(removed_multiple.Supports(ClientType.Flash));
        Assert.False(removed_multiple.Supports(ClientType.Unity));

        MessageKey[] trade_keys = ReadMessageKeys(typeof(MessageKeys.Trade));
        Assert.Equal(19, trade_keys.Length);
        Assert.Equal(19, trade_keys.Distinct().Count());
        Assert.All(trade_keys, key =>
        {
            Assert.True(registry.TryGet(key, out MessageDescriptor descriptor));
            Assert.True(descriptor.HasExplicitKey);
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.Equal(key, contract.Key);
            Assert.Equal(
                descriptor.NamesFor(ClientType.Flash).Count > 0,
                contract.Supports(ClientType.Flash));
            Assert.Equal(
                descriptor.NamesFor(ClientType.Unity).Count > 0,
                contract.Supports(ClientType.Unity));
        });
        MessageKey[] flash_only_trade_keys =
        [
            MessageKeys.Trade.NftOffers,
            MessageKeys.Trade.NftInventory,
            MessageKeys.Trade.SilverUpdated,
            MessageKeys.Trade.SilverFee,
            MessageKeys.Trade.NftInventoryRequest
        ];
        Assert.All(flash_only_trade_keys, key =>
        {
            Assert.True(contracts.TryGet(key, out IMessageContract contract));
            Assert.True(contract.Supports(ClientType.Flash));
            Assert.False(contract.Supports(ClientType.Unity));
        });

        string[] unverified_unity_outgoing =
        [
            "LockAllChests",
            "SetChestNotificationPreferences",
            "UpgradeChest",
            "WiredGetRoomLogs",
            "WiredGetUserPermanentVariables",
            "WiredGetVariableOwnersPage",
            "WiredOpenContract",
            "WiredSetUserPermanentVariable",
            "WiredTransactionGetChestLogs",
            "WiredTransactionGetLogDetails",
            "WiredTransactionGetRoomLogs",
            "WiredUpdateContract",
            "WiredUpdateRoom"
        ];
        Assert.All(unverified_unity_outgoing, name =>
        {
            Assert.True(registry.TryGet(
                ClientType.Unity,
                Direction.Out,
                name,
                out MessageDescriptor descriptor));
            Assert.False(descriptor.HasExplicitKey);
        });
    }

    private static void AssertEarningConstructor(
        Type type,
        params (Type Type, string Name)[] expected)
    {
        ConstructorInfo constructor = Assert.Single(
            type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));
        ParameterInfo[] parameters = constructor.GetParameters();
        Assert.Equal(expected.Select(value => value.Type), parameters.Select(value => value.ParameterType));
        Assert.Equal(expected.Select(value => value.Name), parameters.Select(value => value.Name));
    }

    private static void AssertEarningProperties(
        Type type,
        params (string Name, Type Type, bool InitOnly)[] expected)
    {
        PropertyInfo[] properties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expected.Select(value => value.Name).OrderBy(name => name, StringComparer.Ordinal),
            properties.Select(property => property.Name));

        foreach ((string name, Type property_type, bool init_only) in expected)
        {
            PropertyInfo property = Assert.Single(properties, value => value.Name == name);
            Assert.Equal(property_type, property.PropertyType);
            MethodInfo getter = Assert.IsAssignableFrom<MethodInfo>(property.GetMethod);
            Assert.True(getter.IsPublic);
            if (!init_only)
            {
                Assert.Null(property.SetMethod);
                continue;
            }

            MethodInfo setter = Assert.IsAssignableFrom<MethodInfo>(property.SetMethod);
            Assert.True(setter.IsPublic);
            Assert.Contains(
                typeof(System.Runtime.CompilerServices.IsExternalInit),
                setter.ReturnParameter.GetRequiredCustomModifiers());
        }
    }

    private static void AssertEarningDeconstruct(
        Type type,
        params (Type Type, string Name)[] expected)
    {
        MethodInfo method = Assert.Single(
            type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly),
            value => value.Name == "Deconstruct");
        Assert.Equal(typeof(void), method.ReturnType);
        ParameterInfo[] parameters = method.GetParameters();
        Assert.Equal(expected.Select(value => value.Name), parameters.Select(value => value.Name));
        Assert.Equal(expected.Select(value => value.Type), parameters.Select(value => value.ParameterType.GetElementType()));
        Assert.All(parameters, parameter => Assert.True(parameter.IsOut));
    }

    private static void AssertEarningCategoryMethod(
        string name,
        Type return_type,
        bool has_default)
    {
        MethodInfo method = Assert.Single(
            typeof(EarningStatus).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly),
            value => value.Name == name);
        Assert.Equal(return_type, method.ReturnType);
        ParameterInfo parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(EarningCategory), parameter.ParameterType);
        Assert.Equal("category", parameter.Name);
        Assert.Equal(has_default, parameter.IsOptional);
        Assert.Equal(has_default, parameter.HasDefaultValue);
        if (has_default)
            Assert.Equal(EarningCategory.All, Assert.IsType<EarningCategory>(parameter.DefaultValue));
    }

    private static DailyTask CreateDailyTask(
        IReadOnlyList<DailyTaskReward> rewards,
        string task_code = "task") =>
        new(
            1,
            task_code,
            "quest",
            false,
            "v1",
            "",
            5,
            2,
            DailyTaskStatus.InProgress,
            60,
            DateTimeOffset.UnixEpoch,
            rewards);

    private static QuestData CreateQuestData(
        string campaign_code = "camp",
        string type = "type",
        string image_version = "v1",
        string localization_code = "loc",
        string catalog_page_name = "page",
        string chain_code = "chain") =>
        new(
            campaign_code,
            1,
            2,
            3,
            42,
            true,
            type,
            image_version,
            10,
            localization_code,
            4,
            5,
            6,
            catalog_page_name,
            chain_code,
            true,
            false,
            null);

    private static void AssertQuestRecordAbi(
        Type type,
        params (Type Type, string Name)[] expected)
    {
        AssertEarningConstructor(type, expected);
        AssertEarningProperties(
            type,
            expected.Select(value => (value.Name, value.Type, true)).ToArray());
        AssertEarningDeconstruct(type, expected);
    }

    private static void AssertEmptyQuestRecordAbi(Type type)
    {
        AssertEarningConstructor(type);
        AssertEarningProperties(type);
    }

    private static void AssertDailyTaskListAbi(Type type)
    {
        AssertEarningConstructor(
            type,
            (typeof(IReadOnlyList<DailyTask>), "Tasks"));
        AssertEarningProperties(
            type,
            ("Tasks", typeof(IReadOnlyList<DailyTask>), true));
        AssertEarningDeconstruct(
            type,
            (typeof(IReadOnlyList<DailyTask>), "Tasks"));
    }

    private static T AssertAchievementBadgeFixture<T>(
        MessageContract<T> contract,
        ClientType client,
        Direction direction,
        string expected_hex) where T : IParserComposer<T>
    {
        byte[] expected = Convert.FromHexString(expected_hex);
        using var parsed_packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(expected));
        T parsed = contract.Parse(parsed_packet.Reader());
        Assert.Equal(0, parsed_packet.Available);

        using var composed_packet = new Packet(new Header(direction, 91), client);
        contract.Compose(parsed, composed_packet.Writer());
        Assert.Equal(expected, composed_packet.Buffer.Span.ToArray());

        byte[] trailing = [.. expected, 0x7f];
        using var trailing_packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(trailing));
        Assert.Throws<InvalidDataException>(() => contract.Parse(trailing_packet.Reader()));
        return parsed;
    }

    private static T RoundtripRoomObject<T>(
        MessageContract<T> contract,
        T value,
        ClientType client,
        Direction direction) where T : IParserComposer<T>
    {
        using var packet = new Packet(new Header(direction, 91), client);
        contract.Compose(value, packet.Writer());
        byte[] payload = packet.Buffer.Span.ToArray();
        packet.Position = 0;
        T parsed = contract.Parse(packet.Reader());
        Assert.Equal(0, packet.Available);

        using var trailing = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer([.. payload, 0x7f]));
        Assert.Throws<InvalidDataException>(() => contract.Parse(trailing.Reader()));
        return parsed;
    }

    private static void AssertAchievementBadgeComposeFails<T, TException>(
        MessageContract<T> contract,
        T value,
        ClientType client,
        Direction direction)
        where T : IParserComposer<T>
        where TException : Exception
    {
        using var packet = new Packet(new Header(direction, 91), client);
        Assert.Throws<TException>(() => contract.Compose(value, packet.Writer()));
        Assert.Equal(0, packet.Position);
        Assert.Equal(0, packet.Length);
    }

    private static void AssertAchievementBadgeParseFails<T>(
        MessageContract<T> contract,
        ClientType client,
        Direction direction,
        string hex) where T : IParserComposer<T>
    {
        using var packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(Convert.FromHexString(hex)));
        Assert.Throws<InvalidDataException>(() => contract.Parse(packet.Reader()));
    }

    private static T AssertCraftingFixture<T>(
        MessageContract<T> contract,
        ClientType client,
        Direction direction,
        string expected_hex) where T : IParserComposer<T>
    {
        byte[] expected = Convert.FromHexString(expected_hex);
        using var parsed_packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(expected));
        T parsed = contract.Parse(parsed_packet.Reader());
        Assert.Equal(0, parsed_packet.Available);

        using var composed_packet = new Packet(new Header(direction, 91), client);
        contract.Compose(parsed, composed_packet.Writer());
        Assert.Equal(expected, composed_packet.Buffer.Span.ToArray());

        byte[] trailing = [.. expected, 0x7f];
        using var trailing_packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(trailing));
        Assert.Throws<InvalidDataException>(() => contract.Parse(trailing_packet.Reader()));
        return parsed;
    }

    private static void AssertCraftingComposeFails<T, TException>(
        MessageContract<T> contract,
        T value,
        ClientType client,
        Direction direction)
        where T : IParserComposer<T>
        where TException : Exception
    {
        using var packet = new Packet(new Header(direction, 91), client);
        Assert.Throws<TException>(() => contract.Compose(value, packet.Writer()));
        Assert.Equal(0, packet.Position);
        Assert.Equal(0, packet.Length);
    }

    private static void AssertCraftingParseFails<T>(
        MessageContract<T> contract,
        ClientType client,
        Direction direction,
        string hex) where T : IParserComposer<T>
    {
        using var packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(Convert.FromHexString(hex)));
        Assert.Throws<InvalidDataException>(() => contract.Parse(packet.Reader()));
    }

    private static T AssertGiftFixture<T>(
        MessageContract<T> contract,
        ClientType client,
        Direction direction,
        string expected_hex) where T : IParserComposer<T>
    {
        byte[] expected = Convert.FromHexString(expected_hex);
        using var parsed_packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(expected));
        T parsed = contract.Parse(parsed_packet.Reader());
        Assert.Equal(0, parsed_packet.Available);

        using var composed_packet = new Packet(new Header(direction, 91), client);
        contract.Compose(parsed, composed_packet.Writer());
        Assert.Equal(expected, composed_packet.Buffer.Span.ToArray());

        byte[] trailing = [.. expected, 0x7f];
        using var trailing_packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(trailing));
        Assert.Throws<InvalidDataException>(() => contract.Parse(trailing_packet.Reader()));
        return parsed;
    }

    private static void AssertGiftComposeIsAtomic<T>(
        MessageContract<T> contract,
        T value,
        ClientType client,
        Direction direction) where T : IParserComposer<T>
    {
        using var packet = new Packet(new Header(direction, 91), client);
        Assert.Throws<InvalidDataException>(() => contract.Compose(value, packet.Writer()));
        Assert.Equal(0, packet.Position);
        Assert.Equal(0, packet.Length);
    }

    private static void AssertGiftParseFails<T>(
        string hex,
        ClientType client,
        Direction direction) where T : IParserComposer<T>
    {
        using var packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(Convert.FromHexString(hex)));
        Assert.ThrowsAny<InvalidDataException>(() => packet.Reader().Parse<T>());
    }

    private static HabboClubOffer ClubOffer(string product_code = "x") => new(
        1,
        product_code,
        2,
        3,
        4,
        true,
        5,
        6,
        false,
        7,
        2026,
        8,
        11);

    private static HabboClubOffers RoundtripClubOffers(
        HabboClubOffers expected,
        ClientType client)
    {
        using var packet = new Packet(new Header(Direction.In, 91), client);
        MessageContracts.Subscriptions.ClubOffersSnapshot.Compose(
            expected,
            packet.Writer());
        packet.Position = 0;
        HabboClubOffers parsed =
            MessageContracts.Subscriptions.ClubOffersSnapshot.Parse(packet.Reader());
        AssertClubOffersEqual(expected, parsed);
        Assert.Equal(0, packet.Available);
        packet.Position = packet.Length;
        packet.Writer().WriteByte(0x7f);
        packet.Position = 0;
        Assert.Throws<InvalidDataException>(() =>
            MessageContracts.Subscriptions.ClubOffersSnapshot.Parse(packet.Reader()));
        return parsed;
    }

    private static void AssertClubOffersHex(
        HabboClubOffers expected,
        ClientType client,
        string expected_hex)
    {
        byte[] expected_bytes = Convert.FromHexString(expected_hex);
        using var composed = new Packet(new Header(Direction.In, 91), client);
        MessageContracts.Subscriptions.ClubOffersSnapshot.Compose(
            expected,
            composed.Writer());
        Assert.Equal(expected_bytes, composed.Buffer.Span.ToArray());

        using var parsed = new Packet(new Header(Direction.In, 91), client);
        parsed.WriteSpan(expected_bytes);
        parsed.Position = 0;
        HabboClubOffers actual =
            MessageContracts.Subscriptions.ClubOffersSnapshot.Parse(parsed.Reader());
        AssertClubOffersEqual(expected, actual);
        Assert.Equal(0, parsed.Available);
    }

    private static void AssertClubOffersEqual(
        HabboClubOffers expected,
        HabboClubOffers actual)
    {
        Assert.Equal(expected.DaysLeft, actual.DaysLeft);
        Assert.Equal(expected.Offers.ToArray(), actual.Offers.ToArray());
    }

    private static void AssertClubOfferPublicEqual(
        HabboClubOffer expected,
        HabboClubOffer actual)
    {
        Assert.Equal(expected.OfferId, actual.OfferId);
        Assert.Equal(expected.ProductCode, actual.ProductCode);
        Assert.Equal(expected.PriceCredits, actual.PriceCredits);
        Assert.Equal(expected.PriceActivityPoints, actual.PriceActivityPoints);
        Assert.Equal(expected.PriceActivityPointType, actual.PriceActivityPointType);
        Assert.Equal(expected.IsVip, actual.IsVip);
        Assert.Equal(expected.Months, actual.Months);
        Assert.Equal(expected.ExtraDays, actual.ExtraDays);
        Assert.Equal(expected.IsGiftable, actual.IsGiftable);
        Assert.Equal(expected.DaysLeftAfterPurchase, actual.DaysLeftAfterPurchase);
        Assert.Equal(expected.Year, actual.Year);
        Assert.Equal(expected.Month, actual.Month);
        Assert.Equal(expected.Day, actual.Day);
    }

    private static MessageDialectCapability BuildersClubWallCapability(
        IReadOnlyList<OutgoingMessageSchema> reference_schemas,
        IReadOnlyList<OutgoingMessageSchema> target_schemas)
    {
        (MessageManager messages, Header header) = BuildersClubWallMessages(
            reference_schemas,
            target_schemas);
        return MessageContracts.Subscriptions.BuildersClubWallOfferPlace.Capability(
            ClientType.Unity,
            messages,
            header);
    }

    private static (MessageManager Messages, Header TargetHeader) BuildersClubWallMessages(
        IReadOnlyList<OutgoingMessageSchema> reference_schemas,
        IReadOnlyList<OutgoingMessageSchema> target_schemas)
    {
        MessageManager messages = MessageManager.CreateWithEmbeddedMap();
        var catalog = new MessageCatalog();
        catalog.Add(Direction.Out, 90, Msg.Out.MoveWallItem);
        foreach (OutgoingMessageSchema schema in reference_schemas)
            catalog.AddOutgoingSchema(90, schema);
        catalog.Add(Direction.Out, 91, Msg.Out.BuildersClubPlaceWallItem);
        foreach (OutgoingMessageSchema schema in target_schemas)
            catalog.AddOutgoingSchema(91, schema);
        messages.BindSessionCatalog(new SessionCatalogBinding(
            ClientType.Unity,
            catalog,
            new CatalogProvenance(
                CatalogOrigin.EmbeddedReference,
                ClientType.Unity,
                "subscription-adjunct-smoke")));
        Assert.True(messages.TryGetHeader(
            MessageKeys.Subscriptions.BuildersClubWallOfferPlace,
            out Header target_header));
        return (messages, target_header);
    }

    private static OutgoingMessageSchema MoveWallSchema(string source_type) => new(
        Msg.Out.MoveWallItem,
        [
            SubscriptionScalar(0, OutgoingWireType.Int64, "long"),
            SubscriptionScalar(1, OutgoingWireType.Unknown, source_type)
        ]);

    private static OutgoingMessageSchema BuildersClubWallSchema(
        string source_type,
        OutgoingCollectionKind custom_collection = OutgoingCollectionKind.None) => new(
        Msg.Out.BuildersClubPlaceWallItem,
        [
            SubscriptionScalar(0, OutgoingWireType.Int32, "int"),
            SubscriptionScalar(1, OutgoingWireType.Int32, "int"),
            SubscriptionScalar(2, OutgoingWireType.String, "string"),
            SubscriptionScalar(
                3,
                OutgoingWireType.Unknown,
                source_type,
                custom_collection),
            SubscriptionScalar(4, OutgoingWireType.Boolean, "bool")
        ]);

    private static OutgoingParameterSchema SubscriptionScalar(
        int position,
        OutgoingWireType wire_type,
        string source_type,
        OutgoingCollectionKind collection = OutgoingCollectionKind.None) => new(
        position,
        source_type,
        $"value_{position}",
        null,
        wire_type,
        collection);

    private static MessageKey[] ReadMessageKeys(Type type) =>
    [
        .. type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(MessageKey))
            .Select(field => (MessageKey)field.GetValue(null)!),
        .. type.GetNestedTypes(BindingFlags.Public)
            .SelectMany(ReadMessageKeys)
    ];

    private static T RoundtripSubscription<T>(
        MessageContract<T> contract,
        T expected,
        ClientType client,
        Direction direction,
        bool reject_trailing = true)
        where T : IParserComposer<T>
    {
        using var packet = new Packet(new Header(direction, 91), client);
        contract.Compose(expected, packet.Writer());
        packet.Position = 0;
        T parsed = contract.Parse(packet.Reader());
        Assert.Equal(expected, parsed);
        Assert.Equal(0, packet.Available);
        if (reject_trailing)
        {
            packet.Position = packet.Length;
            packet.Writer().WriteByte(0x7f);
            packet.Position = 0;
            Assert.Throws<InvalidDataException>(() => contract.Parse(packet.Reader()));
        }
        return parsed;
    }

    private static T RoundtripForum<T>(
        MessageContract<T> contract,
        T expected,
        ClientType client,
        Direction direction)
        where T : IParserComposer<T>
    {
        using var expected_packet = new Packet(new Header(direction, 91), client);
        contract.Compose(expected, expected_packet.Writer());
        byte[] expected_bytes = expected_packet.Buffer.Span.ToArray();

        using var parsed_packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer(expected_bytes));
        T parsed = contract.Parse(parsed_packet.Reader());
        Assert.Equal(0, parsed_packet.Available);

        using var actual_packet = new Packet(new Header(direction, 91), client);
        contract.Compose(parsed, actual_packet.Writer());
        Assert.Equal(expected_bytes, actual_packet.Buffer.Span.ToArray());

        using var trailing_packet = new Packet(
            new Header(direction, 91),
            client,
            new PacketBuffer([.. expected_bytes, 0x7f]));
        Assert.Throws<InvalidDataException>(() => contract.Parse(trailing_packet.Reader()));
        return parsed;
    }

    private static void AssertSubscriptionHex<T>(
        MessageContract<T> contract,
        T expected,
        ClientType client,
        Direction direction,
        string expected_hex)
        where T : IParserComposer<T>
    {
        byte[] expected_bytes = Convert.FromHexString(expected_hex);
        using var composed = new Packet(new Header(direction, 91), client);
        contract.Compose(expected, composed.Writer());
        Assert.Equal(expected_bytes, composed.Buffer.Span.ToArray());

        using var parsed = new Packet(new Header(direction, 91), client);
        parsed.WriteSpan(expected_bytes);
        parsed.Position = 0;
        Assert.Equal(expected, contract.Parse(parsed.Reader()));
        Assert.Equal(0, parsed.Available);
    }

    private static void WriteUserInfo(PacketWriter writer)
    {
        writer.WriteString("habbo_club");
        writer.WriteInt(1);
        writer.WriteInt(2);
        writer.WriteInt(3);
        writer.WriteInt(4);
        writer.WriteBool(true);
        writer.WriteBool(false);
        writer.WriteInt(5);
        writer.WriteInt(6);
        writer.WriteInt(7);
    }

    private static string ReadManifest()
    {
        Assembly assembly = typeof(MessagesIniParser).Assembly;
        using Stream stream = assembly.GetManifestResourceStream("Qx.Protocol.messages.ini")
            ?? throw new InvalidOperationException("Embedded resource 'Qx.Protocol.messages.ini' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class CaptureInterceptor : IInterceptor
    {
        public CaptureInterceptor(MessageManager messages, Session session)
        {
            Messages = messages;
            Session = session;
        }

        public bool IsConnected => true;
        public Session? Session { get; }
        public MessageManager Messages { get; }
        public int SendCount { get; private set; }
        public Header? Header { get; private set; }
        public ClientType Client { get; private set; }
        public byte[] Payload { get; private set; } = [];

        public event Action<Session>? Connected
        {
            add { }
            remove { }
        }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public event Action<Intercept>? Intercepted
        {
            add { }
            remove { }
        }

        public InterceptorSessionCatalog CaptureSessionCatalog() =>
            new(Session, Messages.ActiveCatalogBinding);

        public void Send(IPacket packet) =>
            Send(packet, Session, Messages.ActiveCatalogBinding, null);

        public void Send(
            IPacket packet,
            Session? expected_session,
            SessionCatalogBinding? expected_catalog,
            Action? dispatch_guard)
        {
            if (!ReferenceEquals(Session, expected_session))
                throw new InvalidOperationException("The connection session changed before dispatch.");
            if (!ReferenceEquals(Messages.ActiveCatalogBinding, expected_catalog))
                throw new InvalidOperationException("The message catalog changed before dispatch.");
            dispatch_guard?.Invoke();
            Header = packet.Header;
            Client = packet.Client;
            Payload = packet.Buffer.Span.ToArray();
            SendCount++;
        }

        public IDisposable Intercept(Header header, Action<Intercept> callback) =>
            TestSubscription.Instance;

        public IDisposable Intercept(Identifier identifier, Action<Intercept> callback) =>
            TestSubscription.Instance;
    }

    private sealed class TestSubscription : IDisposable
    {
        public static TestSubscription Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
