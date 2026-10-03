using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

/// <summary>Represents whether one message of an application member can be used.</summary>
/// <param name="Key">The semantic key of the message.</param>
/// <param name="Direction">The direction the message travels.</param>
/// <param name="Role">Whether the member sends or observes the message.</param>
/// <param name="Required">Whether the member needs the message to be available.</param>
/// <param name="Registered">Whether the message registry knows the message key.</param>
/// <param name="Supported">Whether the message registry declares the message in the required direction and a message contract exists for it.</param>
/// <param name="Resolved">Whether the message resolved to headers in the active session: exactly one header for a message the member sends, at least one for a message it observes.</param>
/// <param name="ModelType">The model type of the message contract, or <see langword="null"/> when there is no contract.</param>
/// <param name="Headers">The resolved header values in ascending order, or an empty list when the message was not resolved.</param>
/// <param name="WireCapability">The name of the wire capability that decides whether the message can be used, or <see langword="null"/> when there is none.</param>
/// <param name="WireAvailable">Whether the wire capability is available, or <see langword="null"/> when there is no named capability.</param>
/// <param name="WireReason">The reason the wire capability is not available, or <see langword="null"/>.</param>
/// <param name="HeaderCapabilities">The wire capability of each resolved header, ordered by header value.</param>
public sealed record ApplicationMessageAvailability(
    MessageKey Key,
    MessageDirection Direction,
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

/// <summary>Represents the wire capability of one resolved message header.</summary>
/// <param name="Header">The header value.</param>
/// <param name="Capability">The name of the capability, or <see langword="null"/> when the header has no named capability.</param>
/// <param name="Available">Whether the message can be used with this header.</param>
/// <param name="Reason">The reason the message cannot be used, or <see langword="null"/> when it is available.</param>
public sealed record ApplicationMessageHeaderCapability(
    int Header,
    string? Capability,
    bool Available,
    string? Reason);

/// <summary>Represents whether an application member can be invoked in the active session.</summary>
/// <remarks>
/// A member is available when every required state is satisfied and, for an operation, every
/// required message is supported, resolved and not blocked by a wire capability. An event checks
/// only the required messages it observes, and a query checks no messages.
/// </remarks>
/// <param name="Available">Whether the member can be invoked.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="MissingStates">The required states that are not satisfied.</param>
/// <param name="ActiveMessages">The availability of each message of the member in the active session.</param>
/// <param name="CatalogProvenance">The origin of the message catalog bound to the active session, or <see langword="null"/> when there is no session or no catalog is bound.</param>
public sealed record ApplicationAvailability(
    bool Available,
    bool Connected,
    IReadOnlyList<ApplicationStateKey> MissingStates,
    IReadOnlyList<ApplicationMessageAvailability> ActiveMessages,
    CatalogProvenance? CatalogProvenance);

/// <summary>Represents an application member together with its current availability.</summary>
/// <remarks>Returned by <see cref="IApplicationRuntime.Describe(string)"/>.</remarks>
/// <param name="Descriptor">The metadata of the member.</param>
/// <param name="Availability">Whether the member can be invoked in the active session.</param>
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
        bool catalog_bound = session is not null && binding is not null;
        ApplicationStateKey[] missing_states = descriptor.RequiredStates
            .Where(state => !StateAvailable(state, session, room, room_bans, room_settings, profile, friends, navigator, marketplace, inventory, wallet, trade))
            .ToArray();
        ApplicationMessageAvailability[] active_messages = descriptor.Messages
            .Select(message => Message(message, catalog_bound))
            .ToArray();
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
        return new ApplicationAvailability(
            missing_states.Length == 0 && messages_available,
            session is not null,
            Array.AsReadOnly(missing_states),
            Array.AsReadOnly(active_messages),
            catalog_bound ? binding!.Provenance : null);
    }

    private ApplicationMessageAvailability Message(
        ApplicationMessageRequirement requirement,
        bool resolve)
    {
        bool registered = interceptor.Messages.Registry.TryGet(
            requirement.Key,
            out MessageDescriptor descriptor);
        bool registry_support = registered &&
            descriptor.Direction == requirement.Direction;
        bool contracted = contracts.TryGet(requirement.Key, out IMessageContract contract);
        int[] headers = [];
        MessageCapability capability = MessageCapability.Ready();
        ApplicationMessageHeaderCapability[] header_capabilities = [];
        if (resolve && registry_support && contracted &&
            interceptor.Messages.TryGetHeaders(requirement.Key, out IReadOnlyList<Header> resolved))
        {
            Header[] matching_headers = resolved
                .Where(header => header.Direction == requirement.Direction)
                .Distinct()
                .ToArray();
            var capabilities = new List<ApplicationMessageHeaderCapability>();
            foreach (Header header in matching_headers)
            {
                MessageCapability current = contract.Capability(
                    interceptor.Messages,
                    header);
                capabilities.Add(new ApplicationMessageHeaderCapability(
                    (int)unchecked((ushort)header.Value),
                    current.Name,
                    current.Available,
                    current.Reason));
                if (!current.Available || capability.Name is null && current.Name is not null)
                    capability = current;
            }
            header_capabilities = capabilities
                .OrderBy(candidate => candidate.Header)
                .ToArray();
            headers = matching_headers
                .Select(header => (int)unchecked((ushort)header.Value))
                .Distinct()
                .Order()
                .ToArray();
        }
        bool header_resolved = requirement.Role is not ApplicationMessageRole.Send
            ? headers.Length != 0
            : headers.Length == 1;
        bool? wire_available = capability.Name is null ? null : capability.Available;
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
