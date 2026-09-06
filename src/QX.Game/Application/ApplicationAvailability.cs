using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

public sealed record ApplicationMessageAvailability(
    MessageKey Key,
    Direction Direction,
    ApplicationMessageRole Role,
    bool Required,
    bool Registered,
    bool Supported,
    bool Resolved,
    Type? ModelType,
    IReadOnlyList<int> Headers,
    string? WireCapability,
    bool? WireAvailable,
    string? WireReason,
    IReadOnlyList<ApplicationMessageHeaderCapability> HeaderCapabilities);

public sealed record ApplicationMessageHeaderCapability(
    int Header,
    string? Capability,
    bool Available,
    string? Reason);

public sealed record ApplicationClientAvailability(
    ClientType Client,
    bool Supported,
    IReadOnlyList<ApplicationMessageAvailability> Messages);

public sealed record ApplicationAvailability(
    bool Available,
    ClientType Client,
    IReadOnlyList<ApplicationStateKey> MissingStates,
    IReadOnlyList<ApplicationMessageAvailability> ActiveMessages,
    IReadOnlyList<ApplicationClientAvailability> Clients,
    CatalogProvenance? CatalogProvenance);

public sealed record ApplicationMemberDescription(
    ApplicationDescriptor Descriptor,
    ApplicationAvailability Availability);

internal sealed class ApplicationAvailabilityResolver(
    IInterceptor interceptor,
    GameState game,
    MessageContractCatalog contracts)
{
    public ApplicationAvailability Read(ApplicationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            InterceptorSessionCatalog session_catalog = interceptor.CaptureSessionCatalog();
            RoomAvailability room = CaptureRoom();
            RoomBanAvailability room_bans = CaptureRoomBans();
            RoomSettingsAvailability room_settings = CaptureRoomSettings();
            ProfileAvailability profile = CaptureProfile();
            FriendAvailability friends = CaptureFriends();
            NavigatorAvailability navigator = CaptureNavigator();
            MarketplaceAvailability marketplace = CaptureMarketplace();
            InventoryAvailability inventory = CaptureInventory();
            WalletAvailability wallet = CaptureWallet();
            TradeAvailability trade = CaptureTrade();
            ApplicationAvailability availability = Read(
                descriptor,
                session_catalog.Session,
                session_catalog.Catalog,
                room,
                room_bans,
                room_settings,
                profile,
                friends,
                navigator,
                marketplace,
                inventory,
                wallet,
                trade);
            InterceptorSessionCatalog current = interceptor.CaptureSessionCatalog();
            if (ReferenceEquals(session_catalog.Session, current.Session) &&
                ReferenceEquals(session_catalog.Catalog, current.Catalog) &&
                room == CaptureRoom() &&
                room_bans == CaptureRoomBans() &&
                room_settings == CaptureRoomSettings() &&
                profile == CaptureProfile() &&
                friends == CaptureFriends() &&
                navigator == CaptureNavigator() &&
                marketplace == CaptureMarketplace() &&
                inventory == CaptureInventory() &&
                wallet == CaptureWallet() &&
                trade == CaptureTrade())
            {
                return availability;
            }
        }
        return Read(
            descriptor,
            null,
            null,
            CaptureRoom(),
            CaptureRoomBans(),
            CaptureRoomSettings(),
            CaptureProfile(),
            CaptureFriends(),
            CaptureNavigator(),
            CaptureMarketplace(),
            CaptureInventory(),
            CaptureWallet(),
            CaptureTrade());
    }

    private ApplicationAvailability Read(
        ApplicationDescriptor descriptor,
        Session? session,
        SessionCatalogBinding? binding,
        RoomAvailability room,
        RoomBanAvailability room_bans,
        RoomSettingsAvailability room_settings,
        ProfileAvailability profile,
        FriendAvailability friends,
        NavigatorAvailability navigator,
        MarketplaceAvailability marketplace,
        InventoryAvailability inventory,
        WalletAvailability wallet,
        TradeAvailability trade)
    {
        ClientType active_client = session?.Client ?? ClientType.None;
        bool catalog_matches = session is not null && binding?.Client == active_client;
        ApplicationStateKey[] missing_states = descriptor.RequiredStates
            .Where(state => !StateAvailable(state, session, room, room_bans, room_settings, profile, friends, navigator, marketplace, inventory, wallet, trade))
            .ToArray();
        ApplicationMessageAvailability[] active_messages = ClientTypes.IsSupported(active_client)
            ? descriptor.Messages.Select(message => Message(active_client, message, catalog_matches)).ToArray()
            : descriptor.Messages.Select(message => Message(ClientType.None, message, false)).ToArray();
        bool messages_available = descriptor.Kind switch
        {
            ApplicationMemberKind.Operation => active_messages
                .Where(message => message.Required)
                .All(message =>
                    message.Supported &&
                    message.Resolved &&
                    message.WireAvailable is not false),
            ApplicationMemberKind.Event => active_messages
                .Where(message =>
                    message.Required &&
                    message.Role is ApplicationMessageRole.Observe)
                .All(message =>
                    message.Supported &&
                    message.Resolved &&
                    message.WireAvailable is not false),
            _ => true
        };
        ApplicationClientAvailability[] clients =
        [
            Client(descriptor, ClientType.Flash),
            Client(descriptor, ClientType.Unity)
        ];
        return new ApplicationAvailability(
            missing_states.Length == 0 && messages_available,
            active_client,
            Array.AsReadOnly(missing_states),
            Array.AsReadOnly(active_messages),
            Array.AsReadOnly(clients),
            catalog_matches ? binding!.Provenance : null);
    }

    private ApplicationClientAvailability Client(
        ApplicationDescriptor descriptor,
        ClientType client)
    {
        ApplicationMessageAvailability[] messages = descriptor.Messages
            .Select(message => Message(client, message, false))
            .ToArray();
        return new ApplicationClientAvailability(
            client,
            messages
                .Where(message => message.Required)
                .All(message => message.Supported),
            Array.AsReadOnly(messages));
    }

    private ApplicationMessageAvailability Message(
        ClientType client,
        ApplicationMessageRequirement requirement,
        bool resolve)
    {
        bool registered = interceptor.Messages.Registry.TryGet(
            requirement.Key,
            out MessageDescriptor descriptor);
        bool registry_support = registered &&
            ClientTypes.IsSupported(client) &&
            descriptor.Direction == requirement.Direction &&
            descriptor.NamesFor(client).Count != 0;
        bool contracted = contracts.TryGet(requirement.Key, out IMessageContract contract) &&
            contract.Supports(client);
        bool schema_selected = contracted && contract.AllowsSchemaSelectedHeader(client);
        string? required_schema_capability = schema_selected
            ? requirement.SchemaCapability
            : null;
        int[] headers = [];
        MessageDialectCapability capability = MessageDialectCapability.Ready();
        ApplicationMessageHeaderCapability[] header_capabilities = [];
        if (resolve && registry_support && contracted &&
            interceptor.Messages.TryGetHeaders(client, requirement.Key, out IReadOnlyList<Header> resolved))
        {
            Header[] matching_headers = resolved
                .Where(header => header.Direction == requirement.Direction)
                .Distinct()
                .ToArray();
            var capabilities = new List<ApplicationMessageHeaderCapability>();
            foreach (Header header in matching_headers)
            {
                MessageDialectCapability current = contract.Capability(
                    client,
                    interceptor.Messages,
                    header);
                bool schema_available = !schema_selected ||
                    interceptor.Messages.TryGetOutgoingSchemas(
                        client,
                        header,
                        out IReadOnlyList<OutgoingMessageSchema> schemas) &&
                    schemas.Count != 0;
                bool available = current.Available && schema_available;
                string? reason = current.Reason;
                if (!schema_available && reason is null)
                    reason = "The resolved header has no verified outgoing schema.";
                capabilities.Add(new ApplicationMessageHeaderCapability(
                    (int)unchecked((ushort)header.Value),
                    current.Name,
                    available,
                    reason));
                if (!current.Available || capability.Name is null && current.Name is not null)
                    capability = current;
            }
            header_capabilities = capabilities
                .OrderBy(candidate => candidate.Header)
                .ToArray();
            if (required_schema_capability is not null)
            {
                header_capabilities = header_capabilities
                    .Where(candidate => string.Equals(
                        candidate.Capability,
                        required_schema_capability,
                        StringComparison.Ordinal))
                    .ToArray();
                headers = header_capabilities
                    .Select(candidate => candidate.Header)
                    .Distinct()
                    .Order()
                    .ToArray();
                if (header_capabilities.Any(candidate => candidate.Available))
                {
                    capability = MessageDialectCapability.Ready(required_schema_capability);
                }
                else
                {
                    capability = MessageDialectCapability.Missing(
                        required_schema_capability,
                        header_capabilities.FirstOrDefault()?.Reason ??
                        $"No resolved header provides the required wire capability '{required_schema_capability}'.");
                }
            }
            else
            {
                headers = matching_headers
                    .Select(header => (int)unchecked((ushort)header.Value))
                    .Distinct()
                    .Order()
                    .ToArray();
                if (schema_selected)
                {
                    ApplicationMessageHeaderCapability? available = header_capabilities
                        .FirstOrDefault(candidate => candidate.Available);
                    if (available is not null)
                    {
                        capability = MessageDialectCapability.Ready(available.Capability);
                    }
                    else if (header_capabilities.FirstOrDefault() is { } missing)
                    {
                        capability = MessageDialectCapability.Missing(
                            missing.Capability ?? "schemaSelectedHeader",
                            missing.Reason ?? "No resolved header has a verified outgoing schema.");
                    }
                }
            }
        }
        if (resolve && required_schema_capability is not null && capability.Name is null)
        {
            capability = MessageDialectCapability.Missing(
                required_schema_capability,
                $"No resolved header provides the required wire capability '{required_schema_capability}'.");
        }
        bool header_resolved = requirement.Role is not ApplicationMessageRole.Send
            ? headers.Length != 0
            : required_schema_capability is not null
                ? headers.Length == 1
                : schema_selected
                ? headers.Length != 0
                : headers.Length == 1;
        bool? wire_available = schema_selected && header_capabilities.Length != 0
            ? header_capabilities.Any(candidate => candidate.Available)
            : capability.Name is null
                ? null
                : capability.Available;
        return new ApplicationMessageAvailability(
            requirement.Key,
            requirement.Direction,
            requirement.Role,
            requirement.Required,
            registered,
            registry_support && contracted,
            header_resolved,
            contracted ? contract.MessageType : null,
            Array.AsReadOnly(headers),
            capability.Name,
            wire_available,
            capability.Reason,
            Array.AsReadOnly(header_capabilities));
    }

    private RoomAvailability CaptureRoom() => game.Room.Capture(room =>
        new RoomAvailability(room.IsInRoom, room.IsReady, room.Generation, (Id)room.RoomId));

    private RoomBanAvailability CaptureRoomBans()
    {
        RoomBanState state = game.RoomBans.State;
        return new RoomBanAvailability(
            state.Session,
            state.SessionGeneration,
            state.Revision,
            state.RoomGeneration,
            state.RoomId,
            state.Loaded);
    }

    private RoomSettingsAvailability CaptureRoomSettings()
    {
        RoomSettingsManagerState state = game.RoomSettings.State;
        return new RoomSettingsAvailability(
            state.Session,
            state.Rooms.Values.Any(entry => entry.Loaded),
            state.SessionGeneration,
            state.Revision);
    }

    private ProfileAvailability CaptureProfile()
    {
        ProfileState state = game.Profile.State;
        return new ProfileAvailability(
            state.Loaded,
            state.BlockListLoaded,
            state.IgnoreListLoaded,
            state.FigureSetsLoaded,
            state.SanctionsLoaded,
            state.Generation,
            state.Revision);
    }

    private FriendAvailability CaptureFriends() => game.Friends.Capture(friends =>
        new FriendAvailability(friends.IsLoaded, friends.Generation, friends.Revision));

    private NavigatorAvailability CaptureNavigator()
    {
        NavigatorState state = game.Navigator.State;
        return new NavigatorAvailability(
            state.MetadataLoaded,
            state.FlatCategoriesLoaded,
            state.Generation,
            state.Revision);
    }

    private MarketplaceAvailability CaptureMarketplace()
    {
        MarketplaceSnapshot state = game.Marketplace.Snapshot;
        return new MarketplaceAvailability(
            state.ConfigurationLoaded,
            state.EligibilityLoaded,
            state.Generation,
            state.Revision);
    }

    private InventoryAvailability CaptureInventory()
    {
        InventoryState state = game.Inventory.State;
        return new InventoryAvailability(
            state.Session,
            state.Furni.Loaded,
            state.Pets.Loaded,
            state.Generation,
            state.Revision);
    }

    private WalletAvailability CaptureWallet()
    {
        WalletState state = game.Economy.State;
        return new WalletAvailability(
            state.Session,
            state.CreditsLoaded,
            state.ActivityPointsLoaded,
            state.Generation,
            state.Revision);
    }

    private TradeAvailability CaptureTrade()
    {
        TradeState state = game.Trade.State;
        TradeEpochState? active = state.Active;
        ProfileState profile = game.Profile.State;
        TradeParticipantState? local = ReferenceEquals(profile.Session, state.Session) &&
            profile.Identity is { } identity &&
            active is not null
                ? active.FirstParticipant.UserId == identity.Id
                    ? active.FirstParticipant
                    : active.SecondParticipant.UserId == identity.Id
                        ? active.SecondParticipant
                        : null
                : null;
        return new TradeAvailability(
            state.Session,
            active is not null,
            active?.Phase,
            local?.CanTrade == true,
            active is not null && (long)active.OwnSilver + active.OtherSilver >= active.SilverFee,
            state.NftInventory.Loaded,
            state.Generation,
            state.Revision,
            state.Epoch);
    }

    private static bool StateAvailable(
        ApplicationStateKey state,
        Session? session,
        RoomAvailability room,
        RoomBanAvailability room_bans,
        RoomSettingsAvailability room_settings,
        ProfileAvailability profile,
        FriendAvailability friends,
        NavigatorAvailability navigator,
        MarketplaceAvailability marketplace,
        InventoryAvailability inventory,
        WalletAvailability wallet,
        TradeAvailability trade) => state switch
        {
            ApplicationStateKey.HotelConnected => session is not null,
            ApplicationStateKey.CatalogCache => true,
            ApplicationStateKey.CatalogPurchase => true,
            ApplicationStateKey.Subscriptions => true,
            ApplicationStateKey.Gifts => true,
            ApplicationStateKey.Crafting => true,
            ApplicationStateKey.Achievements => true,
            ApplicationStateKey.BadgeInventory => true,
            ApplicationStateKey.Earnings => true,
            ApplicationStateKey.DailyTasks => true,
            ApplicationStateKey.Quests => true,
            ApplicationStateKey.Forums => true,
            ApplicationStateKey.Leaderboards => true,
            ApplicationStateKey.Habbicons => true,
            ApplicationStateKey.RoomActive => room.Active,
            ApplicationStateKey.RoomReady => room.Ready,
            ApplicationStateKey.RoomBansLoaded =>
                room_bans.Loaded &&
                ReferenceEquals(room_bans.Session, session) &&
                room.Active &&
                room_bans.RoomGeneration == room.Generation &&
                room_bans.RoomId == room.RoomId,
            ApplicationStateKey.RoomSettingsLoaded =>
                room_settings.Loaded && ReferenceEquals(room_settings.Session, session),
            ApplicationStateKey.ProfileLoaded => profile.Loaded,
            ApplicationStateKey.ProfileBlockListLoaded => profile.BlockListLoaded,
            ApplicationStateKey.ProfileIgnoreListLoaded => profile.IgnoreListLoaded,
            ApplicationStateKey.ProfileFigureSetsLoaded => profile.FigureSetsLoaded,
            ApplicationStateKey.ProfileSanctionsLoaded => profile.SanctionsLoaded,
            ApplicationStateKey.FriendsLoaded => friends.Loaded,
            ApplicationStateKey.NavigatorMetadataLoaded => navigator.MetadataLoaded,
            ApplicationStateKey.NavigatorFlatCategoriesLoaded => navigator.FlatCategoriesLoaded,
            ApplicationStateKey.MarketplaceConfigurationLoaded => marketplace.ConfigurationLoaded,
            ApplicationStateKey.MarketplaceEligibilityLoaded => marketplace.EligibilityLoaded,
            ApplicationStateKey.InventoryFurniLoaded =>
                inventory.FurniLoaded && ReferenceEquals(inventory.Session, session),
            ApplicationStateKey.InventoryPetsLoaded =>
                inventory.PetsLoaded && ReferenceEquals(inventory.Session, session),
            ApplicationStateKey.WalletLoaded =>
                wallet.CreditsLoaded &&
                wallet.ActivityPointsLoaded &&
                ReferenceEquals(wallet.Session, session),
            ApplicationStateKey.TradeInactive =>
                !trade.Active && ReferenceEquals(trade.Session, session),
            ApplicationStateKey.TradeActive =>
                trade.Active && ReferenceEquals(trade.Session, session),
            ApplicationStateKey.TradeTrading =>
                trade.Phase == TradePhase.Trading && ReferenceEquals(trade.Session, session),
            ApplicationStateKey.TradeAwaitingConfirmation =>
                trade.Phase == TradePhase.AwaitingConfirmation && ReferenceEquals(trade.Session, session),
            ApplicationStateKey.TradeLocalCanTrade =>
                trade.LocalCanTrade && ReferenceEquals(trade.Session, session),
            ApplicationStateKey.TradeSilverFeeReached =>
                trade.SilverFeeReached && ReferenceEquals(trade.Session, session),
            ApplicationStateKey.TradeNftInventoryLoaded =>
                trade.NftInventoryLoaded && ReferenceEquals(trade.Session, session),
            _ => false
        };

    private readonly record struct RoomAvailability(bool Active, bool Ready, long Generation, Id RoomId);
    private readonly record struct RoomBanAvailability(
        Session? Session,
        long SessionGeneration,
        long Revision,
        long RoomGeneration,
        Id RoomId,
        bool Loaded);
    private readonly record struct RoomSettingsAvailability(
        Session? Session,
        bool Loaded,
        long SessionGeneration,
        long Revision);
    private readonly record struct ProfileAvailability(
        bool Loaded,
        bool BlockListLoaded,
        bool IgnoreListLoaded,
        bool FigureSetsLoaded,
        bool SanctionsLoaded,
        long Generation,
        long Revision);
    private readonly record struct FriendAvailability(bool Loaded, long Generation, long Revision);
    private readonly record struct NavigatorAvailability(
        bool MetadataLoaded,
        bool FlatCategoriesLoaded,
        long Generation,
        long Revision);
    private readonly record struct MarketplaceAvailability(
        bool ConfigurationLoaded,
        bool EligibilityLoaded,
        long Generation,
        long Revision);
    private readonly record struct InventoryAvailability(
        Session? Session,
        bool FurniLoaded,
        bool PetsLoaded,
        long Generation,
        long Revision);
    private readonly record struct WalletAvailability(
        Session? Session,
        bool CreditsLoaded,
        bool ActivityPointsLoaded,
        long Generation,
        long Revision);
    private readonly record struct TradeAvailability(
        Session? Session,
        bool Active,
        TradePhase? Phase,
        bool LocalCanTrade,
        bool SilverFeeReached,
        bool NftInventoryLoaded,
        long Generation,
        long Revision,
        long Epoch);
}
