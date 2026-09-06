using Qx;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Crafting;
using Qx.Model.Messages.Incoming;
using Qx.Model.Polls;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Scripting;

internal interface IUnityIncomingCodec
{
    bool TryInvoke(Intercept intercept, Action<Intercept> handler);
    bool MatchesNative(IPacket packet);
    Packet Translate(Header header, IPacket flash_packet, IParserContext context);
}

internal static class UnityCompatibilityPacket
{
    public static Packet CreateFlashProjection(
        Header header,
        IParserContext? context = null) =>
        new(header, ClientType.Flash)
        {
            Context = context is null
                ? null
                : FlashProjectionContext(context),
            AllowLegacyIdProjection = true
        };

    public static IParserContext FlashProjectionContext(
        IParserContext context) =>
        new ParserContext(
            context.Messages,
            context.WireProfile with
            {
                FlashMarketplaceLayout =
                    FlashMarketplaceWireLayout.Modern
            });

    public static Packet CopyAs(IPacket source, ClientType client)
    {
        var copy = new Packet(source.Header, client);
        copy.Context = source.Context;
        copy.WriteSpan(source.Buffer.Span);
        copy.Position = 0;
        return copy;
    }
}

internal sealed class UnityIncomingCodec<T>(
    Func<T, T>? to_flash = null,
    Func<T, MessageWireProfile, T>? from_flash = null,
    Func<T, T, T>? to_unity = null,
    Func<T, T, MessageWireProfile, T>? to_unity_profile = null) : IUnityIncomingCodec
    where T : IParserComposer<T>
{
    public bool MatchesNative(IPacket packet)
    {
        int position = packet.Position;
        try
        {
            packet.Position = 0;
            PacketReader reader = packet.Reader();
            T message = reader.Parse<T>();
            if (reader.Available != 0)
                return false;

            using var roundtrip = new Packet(packet.Header, ClientType.Unity) { Context = packet.Context };
            roundtrip.Writer().Compose(message);
            return roundtrip.Buffer.Span.SequenceEqual(packet.Buffer.Span);
        }
        catch
        {
            return false;
        }
        finally
        {
            packet.Position = position;
        }
    }

    public bool TryInvoke(Intercept intercept, Action<Intercept> handler)
    {
        Packet unity_packet = intercept.Packet;
        T native_message;
        try
        {
            unity_packet.Position = 0;
            PacketReader native_reader = unity_packet.Reader();
            native_message = native_reader.Parse<T>();
            if (native_reader.Available != 0)
                throw new InvalidOperationException($"The native Unity payload contains {native_reader.Available} unparsed bytes.");
        }
        catch
        {
            unity_packet.Position = 0;
            return false;
        }

        var flash_packet = UnityCompatibilityPacket.CreateFlashProjection(
            unity_packet.Header,
            unity_packet.Context);
        bool lossless_roundtrip;
        try
        {
            flash_packet.Writer().Compose(to_flash is null ? native_message : to_flash(native_message));
            flash_packet.Position = 0;

            T roundtrip_message = flash_packet.Reader().Parse<T>();
            roundtrip_message = MergeUnity(native_message, roundtrip_message, unity_packet.Context);
            using var roundtrip_packet = new Packet(unity_packet.Header, ClientType.Unity) { Context = unity_packet.Context };
            roundtrip_packet.Writer().Compose(roundtrip_message);
            lossless_roundtrip = roundtrip_packet.Buffer.Span.SequenceEqual(unity_packet.Buffer.Span);
            flash_packet.Position = 0;
        }
        catch
        {
            flash_packet.Dispose();
            unity_packet.Position = 0;
            return false;
        }

        try
        {
            using (flash_packet)
            {
                byte[] original_bytes = flash_packet.Buffer.Span.ToArray();
                var view = new Intercept
                {
                    Packet = flash_packet,
                    Sequence = intercept.Sequence
                };
                Packet? normalized = null;
                try
                {
                    handler(view);

                    if (view.IsBlocked)
                        intercept.Block();

                    Packet edited = view.Packet;
                    if (edited.Client is ClientType.None)
                    {
                        normalized = UnityCompatibilityPacket.CopyAs(edited, ClientType.Flash);
                        edited = normalized;
                    }

                    if (edited.Client is not ClientType.Flash)
                        throw new InvalidOperationException($"Unity message '{typeof(T).Name}' was intercepted through a Flash wire view and can only be replaced with another Flash packet.");
                    if (edited.Header != unity_packet.Header)
                        throw new InvalidOperationException($"Unity message '{typeof(T).Name}' cannot change headers through a Flash wire view.");

                    if (!edited.Buffer.Span.SequenceEqual(original_bytes))
                    {
                        if (!lossless_roundtrip)
                            throw new InvalidOperationException($"Unity message '{typeof(T).Name}' cannot be edited through a Flash wire view without losing native fields.");

                        edited.Position = 0;
                        PacketReader changed_reader = edited.Reader();
                        T changed = changed_reader.Parse<T>();
                        if (changed_reader.Available != 0)
                            throw new InvalidOperationException($"The edited Flash payload contains {changed_reader.Available} unparsed bytes.");
                        changed = MergeUnity(native_message, changed, unity_packet.Context);
                        using var translated = new Packet(unity_packet.Header, ClientType.Unity) { Context = unity_packet.Context };
                        translated.Writer().Compose(changed);

                        translated.Position = 0;
                        PacketReader native_reader = translated.Reader();
                        T native_changed = native_reader.Parse<T>();
                        if (native_reader.Available != 0)
                            throw new InvalidOperationException($"The translated Unity payload contains {native_reader.Available} unparsed bytes.");
                        T projected = to_flash is null ? native_changed : to_flash(native_changed);
                        using var verification = UnityCompatibilityPacket.CreateFlashProjection(
                            unity_packet.Header,
                            unity_packet.Context);
                        verification.Writer().Compose(projected);
                        if (!verification.Buffer.Span.SequenceEqual(edited.Buffer.Span))
                            throw new InvalidOperationException($"The edited Flash payload for Unity message '{typeof(T).Name}' contains fields that cannot be represented by the native Unity layout.");

                        unity_packet.Clear();
                        unity_packet.WriteSpan(translated.Buffer.Span);
                    }
                }
                finally
                {
                    normalized?.Dispose();
                    if (!ReferenceEquals(view.Packet, flash_packet) && !ReferenceEquals(view.Packet, unity_packet))
                    {
                        view.Packet.Position = 0;
                        view.Packet.Dispose();
                    }
                }
            }

            return true;
        }
        finally
        {
            unity_packet.Position = 0;
        }
    }

    private T MergeUnity(T original, T changed, IParserContext? context)
    {
        if (to_unity_profile is not null)
            return to_unity_profile(original, changed, context?.WireProfile ?? default);
        return to_unity is null ? changed : to_unity(original, changed);
    }

    public Packet Translate(Header header, IPacket flash_packet, IParserContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (flash_packet.Client is not ClientType.Flash)
            throw new ArgumentException("The source packet must use the Flash wire format.", nameof(flash_packet));
        if (header.Direction is not Direction.In || flash_packet.Header.Direction is not Direction.In)
            throw new ArgumentException("Incoming compatibility only accepts incoming packets.", nameof(header));

        try
        {
            flash_packet.Position = 0;
            IParserContext flash_context =
                UnityCompatibilityPacket.FlashProjectionContext(context);
            PacketReader flash_reader = new(
                flash_packet,
                ref flash_packet.Position,
                flash_context);
            T flash_message = flash_reader.Parse<T>();
            if (flash_reader.Available != 0)
                throw new InvalidOperationException($"The Flash payload contains {flash_reader.Available} unparsed bytes.");

            T unity_message = from_flash is null ? flash_message : from_flash(flash_message, context.WireProfile);
            var unity_packet = new Packet(header, ClientType.Unity) { Context = context };
            try
            {
                unity_packet.Writer().Compose(unity_message);
                unity_packet.Position = 0;
                PacketReader unity_reader = unity_packet.Reader();
                T native_roundtrip = unity_reader.Parse<T>();
                if (unity_reader.Available != 0)
                    throw new InvalidOperationException($"The translated Unity payload contains {unity_reader.Available} unparsed bytes.");

                T flash_roundtrip = to_flash is null ? native_roundtrip : to_flash(native_roundtrip);
                using var verification = UnityCompatibilityPacket.CreateFlashProjection(
                    flash_packet.Header,
                    context);
                verification.Writer().Compose(flash_roundtrip);
                if (!verification.Buffer.Span.SequenceEqual(flash_packet.Buffer.Span))
                    throw new InvalidOperationException("The Flash payload cannot be represented by the verified Unity layout without changing its data.");

                unity_packet.Position = 0;
                return unity_packet;
            }
            catch
            {
                unity_packet.Dispose();
                throw;
            }
        }
        finally
        {
            flash_packet.Position = 0;
        }
    }
}

internal static class UnityIncomingCompatibility
{
    private static readonly MessageRegistry Registry = MessagesIniParser.ParseEmbeddedRegistry();
    private static readonly Dictionary<string, IUnityIncomingCodec> Codecs = Build();

    public static bool Supports(string name) => Codecs.ContainsKey(name);

    public static void ValidateNative(string name, IPacket packet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Client is not ClientType.Unity || packet.Header.Direction is not Direction.In)
            throw new ArgumentException("Native Unity incoming validation requires an incoming Unity packet.", nameof(packet));
        if (!Codecs.TryGetValue(name, out IUnityIncomingCodec? codec))
            throw new NotSupportedException($"Incoming Unity message '{name}' has no verified native wire codec.");
        if (!codec.MatchesNative(packet))
            throw new NotSupportedException($"Incoming Unity message '{name}' does not match its verified native wire layout.");
    }

    public static void Invoke(string name, Intercept intercept, Action<Intercept> handler)
    {
        if (intercept.Packet.Client is not ClientType.Unity)
        {
            handler(intercept);
            return;
        }

        if (!Codecs.TryGetValue(name, out IUnityIncomingCodec? codec))
            throw UnsupportedFlashView(name);
        if (!codec.TryInvoke(intercept, handler))
            throw new NotSupportedException($"Incoming Unity message '{name}' does not match its verified Flash wire projection. Use OnUnityIn(\"{name}\", ...) to inspect the native Unity packet.");
    }

    public static Packet Translate(
        string name,
        Header header,
        IPacket flash_packet,
        IParserContext context)
    {
        if (!Codecs.TryGetValue(name, out IUnityIncomingCodec? codec))
            throw new NotSupportedException($"Incoming message '{name}' has no verified Flash-to-Unity translation. Send a native Unity packet instead.");

        try
        {
            return codec.Translate(header, flash_packet, context);
        }
        catch (Exception error) when (error is not NotSupportedException)
        {
            throw new InvalidOperationException($"Incoming Flash message '{name}' cannot be represented by the verified Unity layout.", error);
        }
    }

    private static NotSupportedException UnsupportedFlashView(string name) =>
        new($"Incoming Unity message '{name}' has no verified Flash wire projection. Use OnUnityIn(\"{name}\", ...) to inspect the native Unity packet.");

    private static Dictionary<string, IUnityIncomingCodec> Build()
    {
        var codecs = new Dictionary<string, IUnityIncomingCodec>(StringComparer.OrdinalIgnoreCase);

        Add<AchievementUpdate>(codecs, MessageKeys.Achievements.Updated);
        Add<AchievementNotification>(codecs, MessageKeys.Achievements.Notification);
        Add<Achievements>(codecs, MessageKeys.Achievements.Snapshot);
        Add<ActivityPointNotification>(codecs, MessageKeys.Wallet.ActivityPointUpdated);
        Add<ActivityPoints>(codecs, MessageKeys.Wallet.ActivityPoints);
        Add<AvatarAction>(codecs, "Expression");
        Add<AvatarCarryUpdate>(codecs, "CarryObject");
        AddAvatarChat(codecs, MessageKeys.Room.Chat.Talk, ChatType.Talk, false);
        AddAvatarChat(codecs, MessageKeys.Room.Chat.Shout, ChatType.Shout, false);
        AddAvatarChat(codecs, MessageKeys.Room.Chat.Whisper, ChatType.Whisper, true);
        Add<AvatarDanceUpdate>(codecs, "Dance");
        Add<AvatarEffectUpdate>(codecs, "AvatarEffect");
        Add<AvatarRemove>(codecs, "UserRemove");
        Add<AvatarSleepUpdate>(codecs, "Sleep");
        Add<UserUnbannedFromRoom>(
            codecs,
            (original, changed) => changed with
            {
                RoomId = PreserveId(original.RoomId, changed.RoomId),
                UserId = PreserveId(original.UserId, changed.UserId)
            },
            "UserUnbannedFromRoom");
        Add<AvatarTypingUpdate>(codecs, "UserTyping");
        Add<BadgeInventory>(codecs, MessageKeys.Badges.Snapshot);
        Add<BannedUsersFromRoom>(codecs, MergeBannedUsers, "BannedUsersFromRoom");
        Add<BadgeReceived>(
            codecs,
            (original, changed) => changed with { BadgeId = PreserveId(original.BadgeId, changed.BadgeId) },
            "BadgeReceived");
        Add<BotAddedToInventory>(codecs, "BotAddedToInventory");
        Add<BotCommandConfigurationData>(
            codecs,
            "BotCommandConfiguration",
            "BotCommandConfigurationData");
        Add<BotError>(codecs, "BotError");
        Add<BotInventory>(codecs, "BotInventory");
        Add<BotReceived>(codecs, "BotReceived");
        Add<BotRemovedFromInventory>(codecs, "BotRemovedFromInventory");
        Add<BuildersClubFurniCount>(codecs, "BuildersClubFurniCount");
        Add<CanNotConnect>(codecs, "CantConnect", "CanNotConnect");
        Add<CatalogIndex>(codecs, "CatalogIndex");
        Add<CatalogPage>(codecs, MergeCatalogPage, "CatalogPage");
        Add<CatalogPublished>(codecs, MessageKeys.Catalog.Published);
        Add<ChatReviewSessionOfferedToGuide>(codecs, "ChatReviewSessionOfferedToGuide");
        Add<ChatReviewSessionStarted>(codecs, "ChatReviewSessionStarted");
        Add<CloseConnection>(
            codecs,
            (original, changed) => changed with { Reason = original.Reason },
            "CloseConnection");
        Add(
            codecs,
            MessageKeys.Friends.PrivateMessageReceived,
            new UnityIncomingCodec<ConsoleMessage>(
                message => message with { WireFormat = ConsoleMessageWireFormat.Legacy },
                (message, _) => message with { WireFormat = ConsoleMessageWireFormat.ContentEnvelope },
                (original, changed) => changed with
                {
                    ChatId = PreserveId(original.ChatId, changed.ChatId),
                    Content = original.ContentType is InstantMessageContentType.Habbicon ? original.Content : changed.Content,
                    SenderId = PreserveId(original.SenderId, changed.SenderId),
                    WireFormat = original.WireFormat
                }));
        var club_gift_info = new UnityIncomingCodec<ClubGiftInfo>(
            ProjectClubGiftInfo,
            CreateUnityClubGiftInfo,
            MergeClubGiftInfo);
        codecs["SelectableClubGiftInfo"] = club_gift_info;
        codecs["ClubGiftInfo"] = club_gift_info;
        codecs["ClubGiftSelected"] = new UnityIncomingCodec<ClubGiftSelected>(
            ProjectClubGiftSelected,
            CreateUnityClubGiftSelected,
            MergeClubGiftSelected);
        Add<CreditBalance>(codecs, MessageKeys.Wallet.CreditsBalance);
        Add(
            codecs,
            MessageKeys.Crafting.ProductsSnapshot,
            new UnityIncomingCodec<CraftableProducts>(
                ProjectCraftableProducts,
                CreateUnityCraftableProducts,
                MergeCraftableProducts));
        Add<CraftingRecipe>(codecs, MessageKeys.Crafting.RecipeSnapshot);
        Add<CraftingRecipesAvailable>(codecs, MessageKeys.Crafting.AvailabilitySnapshot);
        Add(
            codecs,
            MessageKeys.Crafting.Result,
            new UnityIncomingCodec<CraftingResult>(
                ProjectCraftingResult,
                CreateUnityCraftingResult,
                MergeCraftingResult));
        Add<CustomUserNotification>(codecs, "CustomUserNotification");
        Add<DisconnectReason>(codecs, MessageKeys.Session.DisconnectReason);
        Add<Doorbell>(codecs,
            (original, changed) => changed with
            {
                UnityUserId = original.UnityUserId,
                UnityFlagA = original.UnityFlagA,
                UnityFlagB = original.UnityFlagB
            },
            "Doorbell",
            "DoorbellRinging");
        Add<FlatProperty>(codecs, "FlatProperty", "RoomProperty");
        Add<FloorItemAdd>(codecs, "ObjectAdd", "ActiveObjectAdd");
        Add<FloorItemDataUpdate>(codecs, "ObjectDataUpdate", "ActiveObjectDataUpdate");
        Add<FloorItemRemove>(codecs, "ObjectRemove", "ActiveObjectRemove");
        Add<FloorItems>(codecs, "Objects", "ActiveObjects");
        Add<FloorItemsDataUpdate>(codecs, "ObjectsDataUpdate", "ActiveObjectsDataUpdate");
        Add<FloorItemUpdate>(codecs, "ObjectUpdate", "ActiveObjectUpdate");
        Add<FloorPlan>(codecs, MessageKeys.Room.Environment.FloorPlan);
        Add<DiceValue>(
            codecs,
            (original, changed) => changed with { ItemId = PreserveId(original.ItemId, changed.ItemId) },
            MessageKeys.Room.FloorItem.DiceValue);
        Add<OneWayDoorStatus>(
            codecs,
            (original, changed) => changed with
            {
                ItemId = PreserveId(original.ItemId, changed.ItemId),
                UnityTrailingValue = original.UnityTrailingValue
            },
            MessageKeys.Room.FloorItem.OneWayDoorStatus);
        Add<FriendFurniCancelLock>(codecs, (original, changed) => changed with { StuffId = PreserveId(original.StuffId, changed.StuffId) }, "FriendFurniCancelLock");
        Add<FriendListFragment>(codecs, MergeFriendListFragment, "FriendListFragment", "FriendsListFragment");
        Add<FriendListUpdate>(codecs, MergeFriendListUpdate, "FriendListUpdate");
        Add<FurniList>(codecs, MergeFurniList, "FurniList");
        Add<FurniListAddOrUpdate>(
            codecs,
            MergeFurniListAddOrUpdate,
            "FurniListAddOrUpdate",
            "InventoryAddOrUpdateFurni");
        Add<FurniListInvalidate>(codecs, "FurniListInvalidate", "InventoryInvalidate");
        Add<FurniListRemove>(codecs, "FurniListRemove", "InventoryRemoveFurni");
        Add<FigureUpdate>(codecs, "FigureUpdate");
        Add<GenericError>(codecs, "GenericError");
        Add<GiftWrappingConfiguration>(codecs, "GiftWrappingConfiguration");
        Add<GotMysteryBoxPrize>(codecs, "GotMysteryBoxPrize");
        Add<GroupData>(codecs,
            (original, changed) => changed with
            {
                Id = PreserveId(original.Id, changed.Id),
                RoomId = PreserveId(original.RoomId, changed.RoomId),
                UnityExtensionId = original.UnityExtensionId
            },
            "HabboGroupDetails");
        Add<GroupDetailsChanged>(codecs, (original, changed) => changed with { GroupId = PreserveId(original.GroupId, changed.GroupId) }, "GroupDetailsChanged");
        codecs["GetGuestRoomResult"] = new UnityIncomingCodec<GuestRoomResult>(
            ProjectGuestRoomResult,
            to_unity_profile: MergeGuestRoomResult);
        Add<GuildEditFailed>(codecs, "GuildEditFailed");
        Add(
            codecs,
            MessageKeys.Groups.Members.Snapshot,
            new UnityIncomingCodec<GuildMembers>(
                message => message with { SearchType = GuildMemberSearchType.All },
                (message, _) => message with { SearchType = null },
                MergeGuildMembers));
        Add<GuildMemberships>(codecs, MergeGuildMemberships, "GuildMemberships");
        Add<GuildMembershipRejected>(codecs,
            (original, changed) => changed with
            {
                GuildId = PreserveId(original.GuildId, changed.GuildId),
                UserId = PreserveId(original.UserId, changed.UserId)
            },
            "GuildMembershipRejected");
        Add<Heightmap>(codecs, "HeightMap", "StackingHeightmap");
        Add<HeightmapUpdate>(codecs, "HeightMapUpdate");
        Add<HandItemReceived>(codecs, MessageKeys.Room.HandItem.Received);
        Add<HabboBroadcast>(codecs, "HabboBroadcast");
        Add<IgnoreUserResult>(codecs, MessageKeys.Users.Ignore.Updated);

        // Held back until now for want of evidence of what Unity puts on the wire. The native IR
        // catalogue answers that for each of these, and each one's reads are what QX already parses
        // — three of them once the fields carrying an id were widened to an id's width.
        Add<BadgePointLimits>(codecs, MessageKeys.Achievements.PointLimits);
        Add<EarningStatus>(codecs, MessageKeys.Earnings.StatusSnapshot);
        Add<EarningClaimResult>(codecs, MessageKeys.Earnings.Claimed);
        Add<FavoriteMembershipUpdate>(codecs, MessageKeys.Room.Occupants.Identity.FavoriteGroup);
        Add<InstantMessageError>(codecs, "InstantMessageError");
        Add<MessengerError>(codecs, "MessengerError");
        Add<NavigatorSettings>(codecs, "NavigatorSettings");
        Add<PetFigureUpdate>(
            codecs,
            (original, changed) => changed with { PetId = PreserveId(original.PetId, changed.PetId) },
            MessageKeys.Room.Occupants.Pet.Figure);
        Add<PetLevelUpdate>(
            codecs,
            (original, changed) => changed with { PetId = PreserveId(original.PetId, changed.PetId) },
            MessageKeys.Room.Occupants.Pet.Level);
        Add<PetStatusUpdate>(
            codecs,
            (original, changed) => changed with { PetId = PreserveId(original.PetId, changed.PetId) },
            MessageKeys.Room.Occupants.Pet.Status);
        Add<PostItPlaced>(codecs, "PostItPlaced");
        Add<UserNameChanged>(
            codecs,
            (original, changed) => changed with { WebId = PreserveId(original.WebId, changed.WebId) },
            MessageKeys.Room.Occupants.Identity.Name);

        Add<InfoHotelClosing>(codecs, "InfoHotelClosing");
        Add<LatencyPingResponse>(codecs, "LatencyPingResponse");
        Add<LoginFailedHotelClosed>(codecs, "LoginFailedHotelClosed");
        Add<MarketplaceConfiguration>(
            codecs,
            "MarketplaceConfiguration");
        codecs["MarketplaceCanMakeOfferResult"] =
            new UnityIncomingCodec<MarketplaceCanMakeOfferResult>(
                message => message with
                {
                    TokenCount = message.TokenCount ?? 0
                },
                (message, _) => message with
                {
                    TokenCount = null
                },
                (original, changed) => changed with
                {
                    TokenCount = original.TokenCount
                });
        Add<MarketplaceMakeOfferResult>(codecs, "MarketplaceMakeOfferResult");
        codecs["MarketplaceItemStats"] =
            new UnityIncomingCodec<MarketplaceItemStats>(
                ProjectMarketplaceItemStats,
                to_unity: MergeMarketplaceItemStats);
        var marketplace_offers =
            new UnityIncomingCodec<MarketplaceOffers>(
                to_unity: MergeMarketplaceOffers);
        codecs["MarketplaceOpenOfferList"] = marketplace_offers;
        codecs["MarketPlaceOffers"] = marketplace_offers;
        var marketplace_own_offers =
            new UnityIncomingCodec<MarketplaceOwnOffers>(
                to_unity: MergeMarketplaceOwnOffers);
        codecs["MarketplaceOwnOfferList"] = marketplace_own_offers;
        codecs["MarketPlaceOwnOffers"] = marketplace_own_offers;
        Add<MarketplaceBuyResult>(
            codecs,
            MergeMarketplaceBuyResult,
            "MarketplaceBuyOfferResult");
        Add<MarketplaceCancelAllOffersResult>(
            codecs,
            MergeMarketplaceCancelAllOffersResult,
            "MarketplaceCancelAllOffersResult");
        Add<MessengerInit>(codecs,
            (original, changed) => changed with
            {
                Categories = MergeCategories(original.Categories, changed.Categories),
                FriendCount = original.FriendCount,
                FriendRequestCount = original.FriendRequestCount
            },
            "MessengerInit");
        Add<MiniMailUnreadCount>(codecs, "MiniMailUnreadCount");
        Add<NavigatorSearchResult>(codecs, MergeNavigatorSearchResult, "NavigatorSearchResultBlocks");
        Add<NestBreedingSuccess>(codecs, (original, changed) => changed with { PetId = PreserveId(original.PetId, changed.PetId) }, "NestBreedingSuccess");
        Add<NewFriendRequest>(
            codecs,
            (original, changed) => changed with
            {
                RequestId = PreserveId(original.RequestId, changed.RequestId)
            },
            MessageKeys.Friends.FriendRequestReceived);
        Add<NoobnessLevel>(codecs, "NoobnessLevel");
        Add<NoSuchFlat>(
            codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "NoSuchFlat");
        Add<OpenConnectionConfirmation>(
            codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "OpenConnection",
            "OpenConnectionConfirmation");
        Add<OpenPetPackageResult>(codecs, (original, changed) => changed with { ObjectId = PreserveId(original.ObjectId, changed.ObjectId) }, "OpenPetPackageResult");
        Add<PetAddedToInventory>(codecs, MergePetAddedToInventory, "PetAddedToInventory");
        Add<PetInfo>(codecs, MergePetInfo, "PetInfo");
        Add<PetInventory>(codecs, MergePetInventory, "PetInventory");
        Add<PetRemovedFromInventory>(
            codecs,
            (original, changed) => changed with { PetId = PreserveId(original.PetId, changed.PetId) },
            "PetRemovedFromInventory");
        Add<PetRespectFailed>(codecs, "PetRespectFailed");
        Add<PollContents>(codecs, MergePollContents, "PollContents");
        Add<PollError>(codecs, "PollError");
        Add<PollOffer>(codecs, "PollOffer");
        var present_opened = new UnityIncomingCodec<PresentOpened>(
            message => message with
            {
                PlacedItemId = unchecked((int)(long)message.PlacedItemId)
            },
            to_unity: (original, changed) => changed with
            {
                PlacedItemId = PreserveId(original.PlacedItemId, changed.PlacedItemId)
            });
        codecs["PresentOpen"] = present_opened;
        codecs["PresentOpened"] = present_opened;
        Add<PurchaseError>(codecs, "PurchaseError");
        Add<PurchaseNotAllowed>(codecs, "PurchaseNotAllowed");
        Add<PurchaseOK>(codecs, MergePurchaseOk, "PurchaseOk");
        Add<Quest>(codecs, "Quest");
        Add<QuestCancelled>(codecs, "QuestCancelled");
        Add<QuestCompleted>(codecs, "QuestCompleted");
        Add<QuestDaily>(codecs, "QuestDaily");
        Add<Quests>(codecs, "Quests");
        Add<QuestsSeasonal>(codecs, "QuestsSeasonal", "SeasonalQuests");
        Add<RelationshipStatus>(codecs, MergeRelationshipStatus, "RelationshipStatusInfo");
        Add<RightsList>(codecs, MergeRightsList, MessageKeys.Room.Authority.ControllersSnapshot);
        Add<RespectNotification>(
            codecs,
            (original, changed) => changed with
            {
                RespectedUserId = PreserveId(original.RespectedUserId, changed.RespectedUserId)
            },
            MessageKeys.Room.Occupants.Respect);
        Add<RoomEntryInfo>(codecs, (original, changed) => changed with { GuestRoomId = PreserveId(original.GuestRoomId, changed.GuestRoomId) }, "RoomEntryInfo");
        Add<RoomEntryTile>(codecs, "RoomEntryTile");
        Add<RoomChatSettings>(codecs, MergeStandaloneRoomChatSettings, "RoomChatSettings");
        Add<RoomForward>(
            codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "RoomForward");
        Add<RoomQueueStatus>(
            codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "RoomQueueStatus");
        Add<RoomReady>(
            codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "RoomReady");
        Add<RoomSettings>(codecs, MergeRoomSettings, "RoomSettingsData");
        Add<RoomSettingsSaved>(codecs, (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) }, "RoomSettingsSaved");
        Add<RoomSettingsError>(codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "RoomSettingsError");
        Add<RoomSettingsSaveError>(codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "RoomSettingsSaveError");
        Add<RoomVisualizationSettings>(codecs, "RoomVisualizationSettings");
        Add<RoomUsers>(codecs, "Users", "UsersInRoom");
        Add<ScrSendKickbackInfo>(codecs, "ScrSendKickbackInfo");
        Add<ScrSendUserInfo>(codecs, "ScrSendUserInfo");
        Add<SlideObjectBundle>(
            codecs,
            MergeSlideObjectBundle,
            MessageKeys.Room.Movement.Slide);
        Add<SpecialRoomEffect>(codecs, "SpecialRoomEffect");
        Add<Sticky>(codecs, "ItemDataUpdate");
        Add<TradeAccepted>(codecs, MessageKeys.Trade.AcceptanceUpdated);
        Add<TradeClosed>(codecs, MessageKeys.Trade.Closed);
        Add<TradeCompleted>(codecs, MessageKeys.Trade.Completed);
        Add<TradeConfirmation>(codecs, MessageKeys.Trade.Confirmation);
        Add<TradeOffers>(codecs, MessageKeys.Trade.Offers);
        Add<TradeOpened>(codecs,
            (original, changed) => changed with
            {
                UserId = PreserveId(original.UserId, changed.UserId),
                OtherUserId = PreserveId(original.OtherUserId, changed.OtherUserId),
                UnityExtensionFlag = original.UnityExtensionFlag
            },
            MessageKeys.Trade.Opened);
        Add<TradeOpenFailed>(codecs, MessageKeys.Trade.OpenFailed);
        Add<HabbiconInfo>(codecs, MessageKeys.Habbicons.InfoSnapshot);
        Add<HabbiconShopData>(codecs, MessageKeys.Habbicons.ShopSnapshot);
        Add<RoomUseHabbicon>(codecs, MessageKeys.Habbicons.RoomUsed);
        Add<UserBadges>(codecs,
            (original, changed) => changed with { UserId = PreserveId(original.UserId, changed.UserId) },
            MessageKeys.Badges.Selected);
        Add<UserHabbicons>(
            codecs,
            (original, changed) => changed with
            {
                RecentHabbiconIdsPresent = original.RecentHabbiconIdsPresent
            },
            MessageKeys.Habbicons.InventorySnapshot);
        Add<UserHabbiconStatusChanged>(codecs, MessageKeys.Habbicons.StatusUpdated);
        Add<UserChanged>(codecs, MessageKeys.Room.Occupants.Identity.Appearance);
        Add<UserData>(codecs, "UserObject");
        Add<UserProfile>(codecs, MergeUserProfile, "ExtendedProfile");
        Add<UserSearchResults>(codecs, "HabboSearchResult");
        Add<UserUpdate>(codecs, "UserUpdate");
        Add<YouAreController>(
            codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "YouAreController",
            "Room_Rights");
        Add<YouAreNotController>(
            codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "YouAreNotController",
            "Room_Rights_2");
        Add<YouAreOwner>(
            codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "YouAreOwner",
            "Room_Rights_3");
        Add<YouAreSpectator>(
            codecs,
            (original, changed) => changed with { RoomId = PreserveId(original.RoomId, changed.RoomId) },
            "YouAreSpectator");
        Add<WallItemAdd>(codecs, "ItemAdd");
        Add<WallItemRemove>(codecs, "ItemRemove");
        Add<WallItems>(codecs, "Items");
        Add<WallItemUpdate>(codecs, "ItemUpdate");
        Add<Wardrobe>(codecs, "Wardrobe");
        Add<VoucherRedeemError>(codecs, "VoucherRedeemError");
        Add<WiredRewardResult>(codecs, "WiredRewardResult");
        Add<WiredSaveSuccess>(codecs, "WiredSaveSuccess");
        AddWiredConfig<WiredFurniTrigger, WiredTriggerConfig>(codecs, config => new WiredFurniTrigger(config), message => message.Config, "WiredFurniTrigger");
        AddWiredConfig<WiredFurniAction, WiredActionConfig>(codecs, config => new WiredFurniAction(config), message => message.Config, "WiredFurniAction");
        AddWiredConfig<WiredFurniCondition, WiredConditionConfig>(codecs, config => new WiredFurniCondition(config), message => message.Config, "WiredFurniCondition");
        AddWiredConfig<WiredFurniAddon, WiredAddonConfig>(codecs, config => new WiredFurniAddon(config), message => message.Config, "WiredFurniAddon");
        AddWiredConfig<WiredFurniSelector, WiredSelectorConfig>(codecs, config => new WiredFurniSelector(config), message => message.Config, "WiredFurniSelector");
        Add<WiredTradeCancelled>(codecs, "WiredTradeCancelled");
        Add<WiredTradeCompleted>(codecs, "WiredTradeCompleted");
        Add<WiredTradeInitiate>(codecs, "WiredTradeInitiate");
        Add<WiredTradeItemsUpdate>(codecs, "WiredTradeItemsUpdate");
        Add<WiredTradeTransactionNotification>(codecs, MessageKeys.Wired.Trade.Notification);
        Add<WiredTransactionFail>(codecs, MessageKeys.Wired.Transaction.Failed);
        Add<WiredTransactionSuccess>(codecs, "WiredTransactionSuccess");
        Add<WiredValidationError>(codecs, "WiredValidationError");

        return codecs;
    }

    private static void Add<T>(Dictionary<string, IUnityIncomingCodec> codecs, params string[] names)
        where T : IParserComposer<T>
    {
        var codec = new UnityIncomingCodec<T>();
        foreach (string name in names)
            codecs[name] = codec;
    }

    private static void Add<T>(
        Dictionary<string, IUnityIncomingCodec> codecs,
        MessageKey key) where T : IParserComposer<T> =>
        Add(codecs, key, new UnityIncomingCodec<T>());

    private static void Add<T>(
        Dictionary<string, IUnityIncomingCodec> codecs,
        Func<T, T, T> to_unity,
        MessageKey key) where T : IParserComposer<T> =>
        Add(codecs, key, new UnityIncomingCodec<T>(to_unity: to_unity));

    private static void Add(
        Dictionary<string, IUnityIncomingCodec> codecs,
        MessageKey key,
        IUnityIncomingCodec codec)
    {
        if (!Registry.TryGet(key, out MessageDescriptor? descriptor) ||
            descriptor.Direction is not Direction.In)
        {
            throw new InvalidDataException($"Incoming message key '{key}' is not registered.");
        }

        foreach (MessageAlias alias in descriptor.Aliases)
            codecs[alias.Name] = codec;
    }

    private static void Add<T>(
        Dictionary<string, IUnityIncomingCodec> codecs,
        Func<T, T, T> to_unity,
        params string[] names) where T : IParserComposer<T>
    {
        var codec = new UnityIncomingCodec<T>(to_unity: to_unity);
        foreach (string name in names)
            codecs[name] = codec;
    }

    private static void Add<T>(
        Dictionary<string, IUnityIncomingCodec> codecs,
        Func<T, T, MessageWireProfile, T> to_unity,
        params string[] names) where T : IParserComposer<T>
    {
        var codec = new UnityIncomingCodec<T>(to_unity_profile: to_unity);
        foreach (string name in names)
            codecs[name] = codec;
    }

    private static void AddWiredConfig<TMessage, TConfig>(
        Dictionary<string, IUnityIncomingCodec> codecs,
        Func<TConfig, TMessage> create,
        Func<TMessage, TConfig> config,
        params string[] names)
        where TMessage : IParserComposer<TMessage>
        where TConfig : WiredConfig, new()
    {
        var codec = new UnityIncomingCodec<TMessage>(
            to_flash: message => create(ProjectWiredConfig(config(message))),
            from_flash: (message, profile) => create(ApplyWiredProfile(config(message), profile)),
            to_unity: (original, changed) => create(MergeWiredConfig(config(original), config(changed))));
        foreach (string name in names)
            codecs[name] = codec;
    }

    private static void AddAvatarChat(
        Dictionary<string, IUnityIncomingCodec> codecs,
        MessageKey key,
        ChatType type,
        bool has_whisper_id)
    {
        Add(codecs, key, new UnityIncomingCodec<AvatarChat>(
            to_flash: message => message with
            {
                ChatId = null,
                WhisperId = null
            },
            from_flash: (message, _) => message with
            {
                Type = type,
                ChatId = message.ChatId ?? 0,
                WhisperId = has_whisper_id ? message.WhisperId ?? 0 : null
            },
            to_unity: (original, changed) => changed with
            {
                Type = original.Type,
                ChatId = original.ChatId,
                WhisperId = original.WhisperId
            }));
    }

    private static MarketplaceItemStats ProjectMarketplaceItemStats(
        MarketplaceItemStats message) =>
        message.LowestPrice is null
            ? message with
            {
                LowestPrice = 0,
                SuggestedPrice = 0
            }
            : message;

    private static MarketplaceItemStats MergeMarketplaceItemStats(
        MarketplaceItemStats original,
        MarketplaceItemStats changed) =>
        original.LowestPrice is null
            ? changed with
            {
                LowestPrice = null,
                SuggestedPrice = null
            }
            : changed;

    private static MarketplaceOffers MergeMarketplaceOffers(
        MarketplaceOffers original,
        MarketplaceOffers changed) =>
        changed with
        {
            Offers = MergeByKey(
                original.Offers,
                changed.Offers,
                offer => ProjectedId(offer.OfferId),
                offer => ProjectedId(offer.OfferId),
                MergeMarketplaceOffer)
        };

    private static MarketplaceOwnOffers MergeMarketplaceOwnOffers(
        MarketplaceOwnOffers original,
        MarketplaceOwnOffers changed) =>
        changed with
        {
            CreditsWaiting = original.CreditsWaiting,
            Offers = MergeByKey(
                original.Offers,
                changed.Offers,
                offer => ProjectedId(offer.OfferId),
                offer => ProjectedId(offer.OfferId),
                MergeMarketplaceOffer)
        };

    private static MarketplaceOffer MergeMarketplaceOffer(
        MarketplaceOffer original,
        MarketplaceOffer changed) =>
        changed with
        {
            OfferId = PreserveId(
                original.OfferId,
                changed.OfferId),
            TradeVolume = original.TradeVolume,
            StatusTimeMilliseconds =
                original.StatusTimeMilliseconds
        };

    private static MarketplaceBuyResult MergeMarketplaceBuyResult(
        MarketplaceBuyResult original,
        MarketplaceBuyResult changed) =>
        changed with
        {
            RequestedOfferId = PreserveId(
                original.RequestedOfferId,
                changed.RequestedOfferId),
            NewOfferId = PreserveId(
                original.NewOfferId,
                changed.NewOfferId)
        };

    private static MarketplaceCancelAllOffersResult
        MergeMarketplaceCancelAllOffersResult(
            MarketplaceCancelAllOffersResult original,
            MarketplaceCancelAllOffersResult changed) =>
        changed with
        {
            OfferIds = MergeByKey(
                original.OfferIds,
                changed.OfferIds,
                ProjectedId,
                ProjectedId,
                PreserveId)
        };

    private static Id PreserveId(Id original, Id changed) =>
        unchecked((int)(long)original) == (long)changed ? original : changed;

    private static long PreserveLong(long original, long changed) =>
        unchecked((int)original) == changed ? original : changed;

    private static CraftableProducts ProjectCraftableProducts(CraftableProducts message) =>
        message with
        {
            Products = message.Products.Select(ProjectCraftingProduct).ToArray()
        };

    private static CraftingResult ProjectCraftingResult(CraftingResult message) =>
        message.Success
            ? message with
            {
                Product = ProjectCraftingProduct(message.Product ??
                    throw new InvalidDataException("A successful Unity crafting result has no product."))
            }
            : message with { Product = null };

    private static CraftingProduct ProjectCraftingProduct(CraftingProduct product) =>
        product.ProductCode is null
            ? product with { ProductCode = "" }
            : product;

    private static CraftableProducts MergeCraftableProducts(
        CraftableProducts original,
        CraftableProducts changed)
    {
        bool? has_product_code = CraftingProductLayout(original.Products);
        if (has_product_code is null)
        {
            if (changed.Products.Count != 0)
            {
                throw new InvalidOperationException(
                    "Cannot add products through an empty Unity craftable-products message because its native product layout is unknown.");
            }
            return changed;
        }

        return changed with
        {
            Products = changed.Products
                .Select(product => has_product_code.Value
                    ? product
                    : product with { ProductCode = null })
                .ToArray()
        };
    }

    private static CraftingResult MergeCraftingResult(
        CraftingResult original,
        CraftingResult changed)
    {
        CraftingProduct native_product = original.Product ??
            throw new InvalidDataException("A Unity crafting result has no native product.");
        if (!changed.Success)
            return changed with { Product = native_product };

        CraftingProduct product = changed.Product ??
            throw new InvalidDataException("A successful Flash crafting result has no product.");
        return changed with
        {
            Product = native_product.ProductCode is null
                ? product with { ProductCode = null }
                : product
        };
    }

    private static bool? CraftingProductLayout(IReadOnlyList<CraftingProduct> products)
    {
        if (products.Count == 0)
            return null;

        bool has_product_code = products[0].HasProductCode;
        if (products.Any(product => product.HasProductCode != has_product_code))
            throw new InvalidDataException("A Unity crafting message contains mixed product layouts.");
        return has_product_code;
    }

    private static CraftableProducts CreateUnityCraftableProducts(
        CraftableProducts message,
        MessageWireProfile profile)
    {
        bool has_product_code = profile.RequireUnityCraftingProductCode();
        return message with
        {
            Products = message.Products
                .Select(product => UnityCraftingProduct(
                    product,
                    has_product_code))
                .ToArray()
        };
    }

    private static CraftingResult CreateUnityCraftingResult(
        CraftingResult message,
        MessageWireProfile profile)
    {
        if (!message.Success || message.Product is null)
        {
            throw new NotSupportedException(
                "A failed Flash crafting result does not contain the product required by Unity.");
        }

        return message with
        {
            Product = UnityCraftingProduct(
                message.Product,
                profile.RequireUnityCraftingProductCode())
        };
    }

    private static CraftingProduct UnityCraftingProduct(
        CraftingProduct product,
        bool has_product_code) =>
        has_product_code
            ? product
            : product with { ProductCode = null };

    private static ClubGiftInfo ProjectClubGiftInfo(ClubGiftInfo message) =>
        message with
        {
            Offers = message.Offers.Select(ProjectClubGiftOffer).ToArray(),
            GiftEligibility = message.GiftEligibility
                .Select(eligibility => eligibility with { IsVip = false })
                .ToArray()
        };

    private static ClubGiftInfo CreateUnityClubGiftInfo(
        ClubGiftInfo message,
        MessageWireProfile profile)
    {
        _ = profile;
        return message with
        {
            Offers = message.Offers.Select(CreateUnityClubGiftOffer).ToArray(),
            GiftEligibility = message.GiftEligibility.Select(CreateUnityEligibility).ToArray()
        };
    }

    private static ClubGiftInfo MergeClubGiftInfo(
        ClubGiftInfo original,
        ClubGiftInfo changed)
    {
        IReadOnlyList<CatalogPageOffer> offers = MergeByKey(
            original.Offers,
            changed.Offers,
            offer => offer.OfferId,
            offer => offer.OfferId,
            MergeClubGiftOffer);
        ClubGiftEligibility[] normalized = changed.GiftEligibility
            .Select(CreateUnityEligibility)
            .ToArray();
        IReadOnlyList<ClubGiftEligibility> eligibility = MergeByKey(
            original.GiftEligibility,
            normalized,
            entry => entry.OfferId,
            entry => entry.OfferId,
            (native, entry) => entry with { IsVip = native.IsVip });
        return changed with
        {
            Offers = offers,
            GiftEligibility = eligibility
        };
    }

    private static CatalogPageOffer ProjectClubGiftOffer(CatalogPageOffer offer)
    {
        IReadOnlyList<CatalogPageProduct> unity_products = offer.UnityProducts ??
            throw new InvalidDataException("A Unity club gift offer has no native products.");
        return offer with
        {
            Products = unity_products.Select(ProjectUnityGiftProduct).ToArray()
        };
    }

    private static CatalogPageOffer CreateUnityClubGiftOffer(CatalogPageOffer offer)
    {
        CatalogPageProduct[] unity_products = offer.Products
            .Select(CreateUnityGiftProduct)
            .ToArray();
        return offer with
        {
            Products = RestoreUnityProductTypes(offer.Products, unity_products),
            UnityProductReferences = offer.UnityProductReferences ?? [],
            UnityProducts = unity_products
        };
    }

    private static CatalogPageOffer MergeClubGiftOffer(
        CatalogPageOffer original,
        CatalogPageOffer changed)
    {
        IReadOnlyList<CatalogPageProduct> native_products = original.UnityProducts ??
            throw new InvalidDataException("A Unity club gift offer has no native products.");
        CatalogPageProduct[] unity_products = MergeUnityGiftProducts(
            native_products,
            changed.Products);
        return changed with
        {
            Products = RestoreUnityProductTypes(changed.Products, unity_products),
            UnityProductReferences = original.UnityProductReferences,
            UnityProducts = unity_products
        };
    }

    private static ClubGiftEligibility CreateUnityEligibility(ClubGiftEligibility eligibility)
    {
        if (eligibility.IsVip is not false)
        {
            throw new NotSupportedException(
                "Unity club gift eligibility cannot represent a true or missing Flash VIP flag.");
        }
        return eligibility with { IsVip = null };
    }

    private static ClubGiftSelected ProjectClubGiftSelected(ClubGiftSelected message)
    {
        IReadOnlyList<CatalogPageProduct> unity_products = message.UnityProducts ??
            throw new InvalidDataException("A Unity club gift selection has no native products.");
        return message with
        {
            Products = unity_products.Select(ProjectUnityGiftProduct).ToArray()
        };
    }

    private static ClubGiftSelected CreateUnityClubGiftSelected(
        ClubGiftSelected message,
        MessageWireProfile profile)
    {
        _ = profile;
        CatalogPageProduct[] unity_products = message.Products
            .Select(CreateUnityGiftProduct)
            .ToArray();
        return message with
        {
            Products = RestoreUnityProductTypes(message.Products, unity_products),
            UnityProducts = unity_products
        };
    }

    private static ClubGiftSelected MergeClubGiftSelected(
        ClubGiftSelected original,
        ClubGiftSelected changed)
    {
        IReadOnlyList<CatalogPageProduct> native_products = original.UnityProducts ??
            throw new InvalidDataException("A Unity club gift selection has no native products.");
        CatalogPageProduct[] unity_products = MergeUnityGiftProducts(
            native_products,
            changed.Products);
        return changed with
        {
            Products = RestoreUnityProductTypes(changed.Products, unity_products),
            UnityProducts = unity_products
        };
    }

    private static CatalogProduct ProjectUnityGiftProduct(CatalogPageProduct product) =>
        new(
            FlashGiftProductType(product.ProductType),
            product.FurniClassId,
            product.ExtraParam,
            product.ProductCount,
            product.UniqueLimitedItem,
            product.UniqueLimitedItemSeriesSize,
            product.UniqueLimitedItemsLeft);

    private static CatalogPageProduct CreateUnityGiftProduct(CatalogProduct product) =>
        new(
            UnityGiftProductType(product),
            product.FurniClassId,
            product.ExtraParam,
            product.ProductCount,
            product.UniqueLimitedItem,
            product.UniqueLimitedItemSeriesSize,
            product.UniqueLimitedItemsLeft);

    private static CatalogPageProduct[] MergeUnityGiftProducts(
        IReadOnlyList<CatalogPageProduct> original,
        IReadOnlyList<CatalogProduct> changed)
    {
        if (original.Count != changed.Count)
        {
            throw new InvalidOperationException(
                "Unity club gift products cannot be added or removed through a Flash view without stable product identifiers.");
        }

        var products = new CatalogPageProduct[changed.Count];
        for (int index = 0; index < changed.Count; index++)
        {
            CatalogPageProduct native = original[index];
            CatalogProduct product = changed[index];
            short product_type = UnityGiftProductType(product);
            if (product_type != native.ProductType)
            {
                throw new InvalidOperationException(
                    "A Unity club gift product type cannot be changed through its Flash view.");
            }

            products[index] = native.ProductType is 4
                ? native with { ExtraParam = product.ExtraParam }
                : new CatalogPageProduct(
                    native.ProductType,
                    product.FurniClassId,
                    product.ExtraParam,
                    product.ProductCount,
                    product.UniqueLimitedItem,
                    product.UniqueLimitedItemSeriesSize,
                    product.UniqueLimitedItemsLeft);
        }
        return products;
    }

    private static CatalogProduct[] RestoreUnityProductTypes(
        IReadOnlyList<CatalogProduct> products,
        IReadOnlyList<CatalogPageProduct> unity_products)
    {
        if (products.Count != unity_products.Count)
            throw new InvalidOperationException("Unity club gift product collections do not align.");

        var restored = new CatalogProduct[products.Count];
        for (int index = 0; index < products.Count; index++)
        {
            restored[index] = products[index] with
            {
                UnityProductType = unity_products[index].ProductType
            };
        }
        return restored;
    }

    private static string FlashGiftProductType(short product_type) =>
        product_type switch
        {
            0 => CatalogProduct.TypeItem,
            1 => CatalogProduct.TypeStuff,
            2 => CatalogProduct.TypeEffect,
            4 => CatalogProduct.TypeBadge,
            _ => throw new NotSupportedException(
                $"Unity club gift product type {product_type} has no verified Flash representation.")
        };

    private static short UnityGiftProductType(CatalogProduct product)
    {
        short product_type = product.UnityProductType ??
            product.ProductType.ToLowerInvariant() switch
            {
                CatalogProduct.TypeItem => 0,
                CatalogProduct.TypeStuff => 1,
                CatalogProduct.TypeEffect => 2,
                CatalogProduct.TypeBadge => 4,
                _ => throw new NotSupportedException(
                    $"Flash club gift product type '{product.ProductType}' has no verified Unity representation.")
            };
        _ = FlashGiftProductType(product_type);
        return product_type;
    }

    private static TConfig ProjectWiredConfig<TConfig>(TConfig source)
        where TConfig : WiredConfig, new()
    {
        var projected = new TConfig
        {
            FurniLimit = source.FurniLimit,
            StuffIds = source.StuffIds,
            StuffIds2 = source.StuffIds2,
            StuffTypeId = source.StuffTypeId,
            Id = source.Id,
            StringParam = source.StringParam,
            IntParams = source.IntParams,
            VariableIds = source.VariableIds,
            FurniSourceTypes = source.FurniSourceTypes,
            UserSourceTypes = source.UserSourceTypes,
            Code = source.Code,
            AdvancedMode = source.AdvancedMode,
            InputSources = source.InputSources,
            AllowWallFurni = source.AllowWallFurni,
            Context = ProjectWiredContext(source.Context),
            DefaultIntParams = source.DefaultIntParams,
            UnityContextTags = source.UnityContextTags,
            UnityContextLayout = source.UnityContextLayout,
            UnityConditionHasSeparateInvert = source.UnityConditionHasSeparateInvert
        };

        switch (source, projected)
        {
            case (WiredActionConfig native, WiredActionConfig flash):
                flash.DelayInPulses = native.DelayInPulses;
                break;
            case (WiredConditionConfig native, WiredConditionConfig flash):
                flash.QuantifierCode = native.QuantifierCode;
                flash.QuantifierType = native.QuantifierType;
                flash.DefinitionIsInvert = native.DefinitionIsInvert;
                flash.IsInvert = native.IsInvert;
                break;
            case (WiredSelectorConfig native, WiredSelectorConfig flash):
                flash.IsFilter = native.IsFilter;
                flash.IsInvert = native.IsInvert;
                break;
        }

        return projected;
    }

    private static TConfig MergeWiredConfig<TConfig>(TConfig original, TConfig changed)
        where TConfig : WiredConfig
    {
        changed.StuffIds = MergeByKey(
            original.StuffIds,
            changed.StuffIds,
            ProjectedId,
            ProjectedId,
            PreserveId);
        changed.Id = PreserveId(original.Id, changed.Id);
        changed.UnityContextLayout = original.UnityContextLayout;
        changed.UnityContextTags = original.UnityContextTags;
        changed.UnityConditionHasSeparateInvert = original.UnityConditionHasSeparateInvert;
        changed.Context = original.UnityContextLayout is UnityWiredContextLayout.Full
            ? MergeWiredContext(original.Context, changed.Context)
            : original.Context;

        if (original is WiredConditionConfig native_condition && changed is WiredConditionConfig changed_condition)
        {
            changed_condition.DefinitionIsInvert = original.UnityConditionHasSeparateInvert is false
                ? changed_condition.IsInvert
                : native_condition.DefinitionIsInvert;
        }

        return changed;
    }

    private static TConfig ApplyWiredProfile<TConfig>(TConfig config, MessageWireProfile profile)
        where TConfig : WiredConfig
    {
        if (!profile.IsExact)
            throw new NotSupportedException("The active Unity build has no verified wired configuration layout.");
        config.UnityContextLayout = profile.WiredContextLayout switch
        {
            MessageWiredContextLayout.None => UnityWiredContextLayout.None,
            MessageWiredContextLayout.Tags => UnityWiredContextLayout.Tags,
            MessageWiredContextLayout.Full => UnityWiredContextLayout.Full,
            _ => throw new NotSupportedException("The active Unity build has no verified wired configuration layout.")
        };
        config.UnityConditionHasSeparateInvert = profile.WiredConditionHasSeparateInvert;
        config.UnityContextTags = config.UnityContextLayout is UnityWiredContextLayout.Tags
            ? [.. config.Context.Entries.Select(entry => entry.Tag)]
            : [];
        if (config is WiredConditionConfig condition)
        {
            if (config.UnityConditionHasSeparateInvert is true)
                throw new NotSupportedException("The Flash wired condition payload cannot represent the additional Unity condition flag for this build.");
            condition.DefinitionIsInvert = condition.IsInvert;
        }
        return config;
    }

    private static WiredContext ProjectWiredContext(WiredContext context) => new(
        [.. context.Entries.Select(entry => new WiredContextEntry(entry.Tag, ProjectWiredContextEntry(entry.Value)))]);

    private static IWiredContextEntry ProjectWiredContextEntry(IWiredContextEntry entry) => entry switch
    {
        VariableInfoAndHolders value => value with
        {
            Holders = [.. value.Holders.Select(holder => holder with { Value = unchecked((int)holder.Value) })]
        },
        VariableInfoAndValue value => value with { Value = unchecked((int)value.Value) },
        _ => entry
    };

    private static WiredContext MergeWiredContext(WiredContext original, WiredContext changed)
    {
        IReadOnlyList<(WiredContextEntry Entry, int Occurrence)> native_entries = IndexedWiredContext(original.Entries);
        IReadOnlyList<(WiredContextEntry Entry, int Occurrence)> changed_entries = IndexedWiredContext(changed.Entries);
        IReadOnlyList<WiredContextEntry> merged = MergeByKey(
            native_entries,
            changed_entries,
            entry => (entry.Entry.Tag, entry.Occurrence),
            entry => (entry.Entry.Tag, entry.Occurrence),
            (native, edited) => (
                new WiredContextEntry(
                    edited.Entry.Tag,
                    MergeWiredContextEntry(native.Entry.Value, edited.Entry.Value)),
                edited.Occurrence))
            .Select(entry => entry.Entry)
            .ToArray();
        return new WiredContext(merged);
    }

    private static IReadOnlyList<(WiredContextEntry Entry, int Occurrence)> IndexedWiredContext(
        IReadOnlyList<WiredContextEntry> entries)
    {
        var occurrences = new Dictionary<int, int>();
        var indexed = new (WiredContextEntry Entry, int Occurrence)[entries.Count];
        for (int index = 0; index < entries.Count; index++)
        {
            WiredContextEntry entry = entries[index];
            occurrences.TryGetValue(entry.Tag, out int occurrence);
            indexed[index] = (entry, occurrence);
            occurrences[entry.Tag] = occurrence + 1;
        }
        return indexed;
    }

    private static IWiredContextEntry MergeWiredContextEntry(
        IWiredContextEntry original,
        IWiredContextEntry changed) => (original, changed) switch
    {
        (VariableInfoAndHolders native, VariableInfoAndHolders edited) => edited with
        {
            Variable = MergeWiredVariable(native.Variable, edited.Variable),
            Holders = MergeByKey(
                native.Holders,
                edited.Holders,
                holder => ProjectedId(holder.ObjectId),
                holder => ProjectedId(holder.ObjectId),
                (native_holder, edited_holder) => edited_holder with
                {
                    ObjectId = PreserveId(native_holder.ObjectId, edited_holder.ObjectId),
                    Value = PreserveLong(native_holder.Value, edited_holder.Value)
                })
        },
        (VariableInfoAndValue native, VariableInfoAndValue edited) => edited with
        {
            Variable = MergeWiredVariable(native.Variable, edited.Variable),
            Value = PreserveLong(native.Value, edited.Value)
        },
        (SharedVariableList native, SharedVariableList edited) => edited with
        {
            SharedVariables = MergeByKey(
                native.SharedVariables,
                edited.SharedVariables,
                SharedVariableKey,
                SharedVariableKey,
                (native_variable, edited_variable) => edited_variable with
                {
                    RoomId = PreserveId(native_variable.RoomId, edited_variable.RoomId),
                    WiredVariable = MergeWiredVariable(native_variable.WiredVariable, edited_variable.WiredVariable)
                })
        },
        (VariableList native, VariableList edited) => edited with
        {
            Variables = MergeByKey(
                native.Variables,
                edited.Variables,
                variable => variable.VariableId,
                variable => variable.VariableId,
                MergeWiredVariable)
        },
        (SharedGlobalPlaceholderList native, SharedGlobalPlaceholderList edited) => edited with
        {
            SharedPlaceholders = MergeByKey(
                native.SharedPlaceholders,
                edited.SharedPlaceholders,
                SharedPlaceholderKey,
                SharedPlaceholderKey,
                (native_placeholder, edited_placeholder) => edited_placeholder with
                {
                    RoomId = PreserveId(native_placeholder.RoomId, edited_placeholder.RoomId)
                })
        },
        _ when original.GetType() == changed.GetType() => changed,
        _ => throw new InvalidOperationException($"Cannot merge Unity wired context entry '{original.GetType().Name}' with '{changed.GetType().Name}'.")
    };

    private static WiredVariable MergeWiredVariable(WiredVariable original, WiredVariable changed)
    {
        IReadOnlyList<KeyValuePair<Id, string>>? connector = changed.TextConnector;
        if (original.TextConnector is not null && changed.TextConnector is not null)
        {
            connector = MergeByKey(
                original.TextConnector,
                changed.TextConnector,
                entry => ProjectedId(entry.Key),
                entry => ProjectedId(entry.Key),
                (native, edited) => new KeyValuePair<Id, string>(PreserveId(native.Key, edited.Key), edited.Value));
        }

        return new WiredVariable
        {
            VariableId = changed.VariableId,
            VariableType = changed.VariableType,
            VariableName = changed.VariableName,
            AvailabilityType = changed.AvailabilityType,
            VariableTarget = changed.VariableTarget,
            AlwaysAvailable = changed.AlwaysAvailable,
            CanCreateAndDelete = changed.CanCreateAndDelete,
            HasValue = changed.HasValue,
            CanWriteValue = changed.CanWriteValue,
            CanInterceptChanges = changed.CanInterceptChanges,
            IsInvisible = changed.IsInvisible,
            CanReadCreationTime = changed.CanReadCreationTime,
            CanReadLastUpdateTime = changed.CanReadLastUpdateTime,
            TextConnector = connector
        };
    }

    private static (int RoomId, string VariableId) SharedVariableKey(SharedVariable variable) =>
        (ProjectedId(variable.RoomId), variable.WiredVariable.VariableId);

    private static (int RoomId, string Name) SharedPlaceholderKey(SharedGlobalPlaceholder placeholder) =>
        (ProjectedId(placeholder.RoomId), placeholder.PlaceholderName);

    private static IReadOnlyList<FriendCategory> MergeCategories(
        IReadOnlyList<FriendCategory> original,
        IReadOnlyList<FriendCategory> changed) =>
        MergeByKey(
            original,
            changed,
            category => ProjectedId(category.Id),
            category => ProjectedId(category.Id),
            (native, category) => category with { Id = native.Id });

    private static Friend MergeFriend(Friend original, Friend changed)
    {
        changed.Id = PreserveId(original.Id, changed.Id);
        changed.LastOnline = original.LastOnline;
        changed.UnityStatus = original.UnityStatus;
        changed.UnityPlatform = original.UnityPlatform;
        return changed;
    }

    private static IReadOnlyList<Friend> MergeFriends(IReadOnlyList<Friend> original, IReadOnlyList<Friend> changed) =>
        MergeByKey(
            original,
            changed,
            friend => ProjectedId(friend.Id),
            friend => ProjectedId(friend.Id),
            MergeFriend);

    private static FriendListFragment MergeFriendListFragment(FriendListFragment original, FriendListFragment changed) =>
        changed with { Friends = MergeFriends(original.Friends, changed.Friends) };

    private static FriendListUpdate MergeFriendListUpdate(FriendListUpdate original, FriendListUpdate changed)
    {
        IReadOnlyList<FriendUpdateEntry> updates = MergeByKey(
            original.Updates,
            changed.Updates,
            FriendUpdateKey,
            FriendUpdateKey,
            (native, entry) => entry.Kind is FriendUpdateKind.Removed
                ? entry with { RemovedId = native.RemovedId }
                : entry with { Friend = MergeFriend(native.Friend!, entry.Friend!) });
        return changed with
        {
            Categories = MergeCategories(original.Categories, changed.Categories),
            Updates = updates
        };
    }

    private static IReadOnlyList<InventoryItem> MergeInventoryItems(
        IReadOnlyList<InventoryItem> original,
        IReadOnlyList<InventoryItem> changed) =>
        MergeByKey(
            original,
            changed,
            item => ProjectedId(item.ItemId),
            item => ProjectedId(item.ItemId),
            MergeInventoryItem);

    private static InventoryItem MergeInventoryItem(InventoryItem original, InventoryItem changed)
    {
        changed.ItemId = PreserveId(original.ItemId, changed.ItemId);
        changed.Id = PreserveId(original.Id, changed.Id);
        changed.RoomId = PreserveId(original.RoomId, changed.RoomId);
        changed.IsUnseen = original.IsUnseen;
        changed.Timestamp = original.Timestamp;
        changed.IsNft = original.IsNft;
        changed.NftName = original.NftName;
        changed.IsExternalImage = original.IsExternalImage;
        changed.Extra = PreserveLong(original.Extra, changed.Extra);
        if (original.Data.Type == changed.Data.Type &&
            original.Data.IsLimitedRare &&
            changed.Data.IsLimitedRare)
        {
            changed.Data.UniqueLimitedData = original.Data.UniqueLimitedData;
        }
        return changed;
    }

    private static FurniList MergeFurniList(FurniList original, FurniList changed) =>
        changed with { Items = MergeInventoryItems(original.Items, changed.Items) };

    private static FurniListAddOrUpdate MergeFurniListAddOrUpdate(
        FurniListAddOrUpdate original,
        FurniListAddOrUpdate changed) =>
        changed with { Items = MergeInventoryItems(original.Items, changed.Items) };

    private static CatalogPage MergeCatalogPage(CatalogPage original, CatalogPage changed)
    {
        IReadOnlyList<CatalogPageOffer> offers = MergeByKey(
            original.Offers,
            changed.Offers,
            offer => offer.OfferId,
            offer => offer.OfferId,
            (native, offer) => offer with { UnityProductReferences = native.UnityProductReferences });
        return changed with { Offers = offers };
    }

    private static GuestRoomResult ProjectGuestRoomResult(GuestRoomResult original) =>
        original with
        {
            Details = original.Details is null
                ? new RoomResultDetails { OpeningConnection = false }
                : new RoomResultDetails
                {
                    Forward = original.Details.Forward,
                    IsStaffPick = original.Details.IsStaffPick,
                    IsGroupMember = original.Details.IsGroupMember,
                    IsRoomMuted = original.Details.IsRoomMuted,
                    Moderation = new RoomModerationSettings
                    {
                        Mute = original.Details.Moderation.Mute,
                        Kick = original.Details.Moderation.Kick,
                        Ban = original.Details.Moderation.Ban
                    },
                    CanMute = original.Details.CanMute,
                    Chat = new RoomChatSettings
                    {
                        Flow = original.Details.Chat.Flow,
                        BubbleWidth = original.Details.Chat.BubbleWidth,
                        ScrollSpeed = original.Details.Chat.ScrollSpeed,
                        TalkHearingDistance = original.Details.Chat.TalkHearingDistance,
                        FloodProtection = original.Details.Chat.FloodProtection
                    },
                    OpeningConnection = false
                }
        };

    private static GuestRoomResult MergeGuestRoomResult(
        GuestRoomResult original,
        GuestRoomResult changed,
        MessageWireProfile profile)
    {
        changed.Data.Id = PreserveId(original.Data.Id, changed.Data.Id);
        changed.Data.OwnerId = PreserveId(original.Data.OwnerId, changed.Data.OwnerId);
        changed.Data.GroupId = PreserveId(original.Data.GroupId, changed.Data.GroupId);
        if (original.Details is not null && changed.Details is not null)
        {
            changed.Details.UnityContextId = original.Details.UnityContextId;
            changed.Details.UnityThumbnail = original.Details.UnityThumbnail;
            changed.Details.Chat = MergeGuestRoomChatSettings(original.Details.Chat, changed.Details.Chat, profile);
        }
        return changed;
    }

    private static GuildMembers MergeGuildMembers(
        GuildMembers original,
        GuildMembers changed) =>
        changed with
        {
            GroupId = PreserveId(original.GroupId, changed.GroupId),
            BaseRoomId = PreserveId(original.BaseRoomId, changed.BaseRoomId),
            SearchType = null,
            Entries = MergeByKey(
                original.Entries,
                changed.Entries,
                member => ProjectedId(member.Id),
                member => ProjectedId(member.Id),
                (native, member) => member with
                {
                    Id = PreserveId(native.Id, member.Id)
                })
        };

    private static GuildMemberships MergeGuildMemberships(
        GuildMemberships original,
        GuildMemberships changed) =>
        changed with
        {
            Items = MergeByKey(
                original.Items,
                changed.Items,
                membership => ProjectedId(membership.Id),
                membership => ProjectedId(membership.Id),
                (native, membership) => membership with
                {
                    Id = PreserveId(native.Id, membership.Id),
                    OwnerId = PreserveId(native.OwnerId, membership.OwnerId)
                })
        };

    private static NavigatorSearchResult MergeNavigatorSearchResult(
        NavigatorSearchResult original,
        NavigatorSearchResult changed)
    {
        IReadOnlyList<NavigatorSearchBlock> blocks = MergeByKey(
            original.Blocks,
            changed.Blocks,
            NavigatorBlockKey,
            NavigatorBlockKey,
            (native, block) => block with
            {
                Rooms = MergeByKey(
                    native.Rooms,
                    block.Rooms,
                    room => ProjectedId(room.Id),
                    room => ProjectedId(room.Id),
                    (nativeRoom, room) =>
                    {
                        room.Id = PreserveId(nativeRoom.Id, room.Id);
                        room.OwnerId = PreserveId(nativeRoom.OwnerId, room.OwnerId);
                        room.GroupId = PreserveId(nativeRoom.GroupId, room.GroupId);
                        return room;
                    }),
                UnityMetadata = native.UnityMetadata
            });
        return changed with { Blocks = blocks };
    }

    private static InventoryPet MergeInventoryPet(InventoryPet original, InventoryPet changed)
    {
        changed.Id = PreserveId(original.Id, changed.Id);
        changed.RoomId = original.RoomId;
        changed.RoomName = original.RoomName;
        changed.RoomContext = original.RoomContext;
        return changed;
    }

    private static IReadOnlyList<InventoryPet> MergeInventoryPets(
        IReadOnlyList<InventoryPet> original,
        IReadOnlyList<InventoryPet> changed) =>
        MergeByKey(
            original,
            changed,
            pet => ProjectedId(pet.Id),
            pet => ProjectedId(pet.Id),
            MergeInventoryPet);

    private static PetAddedToInventory MergePetAddedToInventory(
        PetAddedToInventory original,
        PetAddedToInventory changed) =>
        changed with { Pet = MergeInventoryPet(original.Pet, changed.Pet) };

    private static PetInventory MergePetInventory(PetInventory original, PetInventory changed) =>
        changed with { Pets = MergeInventoryPets(original.Pets, changed.Pets) };

    private static PetInfo MergePetInfo(PetInfo original, PetInfo changed)
    {
        changed.Id = PreserveId(original.Id, changed.Id);
        changed.OwnerId = PreserveId(original.OwnerId, changed.OwnerId);
        return changed;
    }

    private static PollContents MergePollContents(PollContents original, PollContents changed) =>
        changed with
        {
            Questions = MergeByKey(
                original.Questions,
                changed.Questions,
                group => ProjectedId(group.Question.QuestionId),
                group => ProjectedId(group.Question.QuestionId),
                MergePollQuestionGroup)
        };

    private static PollQuestionGroup MergePollQuestionGroup(
        PollQuestionGroup original,
        PollQuestionGroup changed) =>
        changed with
        {
            Question = MergePollQuestion(original.Question, changed.Question),
            Children = MergeByKey(
                original.Children,
                changed.Children,
                question => ProjectedId(question.QuestionId),
                question => ProjectedId(question.QuestionId),
                MergePollQuestion)
        };

    private static PollQuestion MergePollQuestion(
        PollQuestion original,
        PollQuestion changed) =>
        changed with { QuestionId = PreserveId(original.QuestionId, changed.QuestionId) };

    private static RoomChatSettings MergeStandaloneRoomChatSettings(
        RoomChatSettings original,
        RoomChatSettings changed)
    {
        changed.Flow = original.Flow;
        changed.BubbleWidth = original.BubbleWidth;
        changed.ScrollSpeed = original.ScrollSpeed;
        changed.TalkHearingDistance = original.TalkHearingDistance;
        return changed;
    }

    private static RoomChatSettings MergeGuestRoomChatSettings(
        RoomChatSettings original,
        RoomChatSettings changed,
        MessageWireProfile profile)
    {
        switch (profile.RequireGuestRoomResultLayout(ClientType.Flash))
        {
            case GuestRoomResultWireLayout.FlashCompactChat:
                changed.Flow = original.Flow;
                changed.BubbleWidth = original.BubbleWidth;
                changed.ScrollSpeed = original.ScrollSpeed;
                changed.TalkHearingDistance = original.TalkHearingDistance;
                return changed;
            case GuestRoomResultWireLayout.FlashFullChat:
            case GuestRoomResultWireLayout.FlashFullChatWithOpening:
                return changed;
            default:
                throw new NotSupportedException("The active Flash build has no verified room chat settings layout.");
        }
    }

    private static PurchaseOK MergePurchaseOk(PurchaseOK original, PurchaseOK changed) =>
        changed with
        {
            Offer = changed.Offer with
            {
                GiftTo = original.Offer.GiftTo,
                RoomItems = original.Offer.RoomItems,
                WallItems = original.Offer.WallItems
            }
        };

    private static RelationshipStatus MergeRelationshipStatus(RelationshipStatus original, RelationshipStatus changed)
    {
        IReadOnlyList<RelationshipEntry> entries = MergeByKey(
            original.Entries,
            changed.Entries,
            entry => entry.Type,
            entry => entry.Type,
            (native, entry) => entry with { RandomFriendId = PreserveId(native.RandomFriendId, entry.RandomFriendId) });
        return changed with
        {
            UserId = PreserveId(original.UserId, changed.UserId),
            Entries = entries
        };
    }

    private static BannedUsersFromRoom MergeBannedUsers(
        BannedUsersFromRoom original,
        BannedUsersFromRoom changed)
    {
        IReadOnlyList<IdName> users = MergeByKey(
            original.Users,
            changed.Users,
            user => ProjectedId(user.Id),
            user => ProjectedId(user.Id),
            (native, user) => user with { Id = native.Id });
        return changed with
        {
            RoomId = PreserveId(original.RoomId, changed.RoomId),
            Users = users
        };
    }

    private static RightsList MergeRightsList(RightsList original, RightsList changed)
    {
        IReadOnlyList<IdName> users = MergeByKey(
            original.Users,
            changed.Users,
            user => ProjectedId(user.Id),
            user => ProjectedId(user.Id),
            (native, user) => user with { Id = native.Id });
        return changed with
        {
            RoomId = PreserveId(original.RoomId, changed.RoomId),
            Users = users
        };
    }

    private static RoomSettings MergeRoomSettings(RoomSettings original, RoomSettings changed) =>
        changed with
        {
            RoomId = PreserveId(original.RoomId, changed.RoomId),
            IsGroupRoom = original.IsGroupRoom,
            GroupRightsPolicy = original.GroupRightsPolicy,
            RequiresBuildersClub = original.RequiresBuildersClub,
            NftGroupIds = original.NftGroupIds,
            IsHabboXDemoRoom = original.IsHabboXDemoRoom,
            MaximumVisitorsLowerLimit = original.MaximumVisitorsLowerLimit
        };

    private static UserProfile MergeUserProfile(UserProfile original, UserProfile changed)
    {
        changed.Id = PreserveId(original.Id, changed.Id);
        changed.Groups = MergeByKey(
            original.Groups,
            changed.Groups,
            group => ProjectedId(group.Id),
            group => ProjectedId(group.Id),
            (native, group) => group with
            {
                Id = native.Id,
                OwnerId = PreserveId(native.OwnerId, group.OwnerId)
            });
        changed.NameColor = original.NameColor;
        changed.OldNames = original.OldNames;
        return changed;
    }

    private static SlideObjectBundle MergeSlideObjectBundle(
        SlideObjectBundle original,
        SlideObjectBundle changed) =>
        changed with
        {
            Objects = MergeByKey(
                original.Objects,
                changed.Objects,
                slide => ProjectedId(slide.Id),
                slide => ProjectedId(slide.Id),
                (native, edited) => edited with
                {
                    Id = PreserveId(native.Id, edited.Id)
                }),
            RollerId = PreserveId(original.RollerId, changed.RollerId),
            Avatar = original.Avatar is { } native_avatar &&
                     changed.Avatar is { } edited_avatar
                ? edited_avatar with
                {
                    Index = PreserveId(native_avatar.Index, edited_avatar.Index)
                }
                : changed.Avatar
        };

    private static (FriendUpdateKind Kind, int Id) FriendUpdateKey(FriendUpdateEntry entry)
    {
        Id id = entry.Kind is FriendUpdateKind.Removed
            ? entry.RemovedId
            : entry.Friend?.Id ?? throw new InvalidOperationException("A friend update entry has no projected identifier.");
        return (entry.Kind, ProjectedId(id));
    }

    private static (string SearchCode, string Text) NavigatorBlockKey(NavigatorSearchBlock block) =>
        (block.SearchCode, block.Text);

    private static int ProjectedId(Id id) => unchecked((int)(long)id);

    private static IReadOnlyList<TChanged> MergeByKey<TOriginal, TChanged, TKey>(
        IReadOnlyList<TOriginal> original,
        IReadOnlyList<TChanged> changed,
        Func<TOriginal, TKey> original_key,
        Func<TChanged, TKey> changed_key,
        Func<TOriginal, TChanged, TChanged> merge) where TKey : notnull
    {
        var originals = new Dictionary<TKey, TOriginal>();
        foreach (TOriginal item in original)
        {
            TKey key = original_key(item);
            if (!originals.TryAdd(key, item))
                throw AmbiguousListKey(typeof(TChanged), key);
        }

        var keys = new HashSet<TKey>();
        var result = new TChanged[changed.Count];
        for (int index = 0; index < changed.Count; index++)
        {
            TChanged item = changed[index];
            TKey key = changed_key(item);
            if (!keys.Add(key))
                throw AmbiguousListKey(typeof(TChanged), key);
            result[index] = originals.TryGetValue(key, out TOriginal? native) ? merge(native, item) : item;
        }
        return result;
    }

    private static InvalidOperationException AmbiguousListKey(Type type, object key) =>
        new($"Cannot preserve Unity fields for {type.Name}: projected list key '{key}' is not unique.");
}
