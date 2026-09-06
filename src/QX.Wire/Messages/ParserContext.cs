namespace Qx.Messages;

public enum MessageWiredContextLayout
{
    Unknown,
    None,
    Tags,
    Full
}

public enum GuestRoomResultWireLayout
{
    FlashFullChat,
    FlashFullChatWithOpening,
    FlashCompactChat,
    Unity
}

public enum MarketplaceBuyWireLayout
{
    Unknown,
    OfferId,
    FurniDetails
}

public enum FlashMarketplaceWireLayout
{
    Unknown,
    Legacy,
    Modern
}

public enum ConsoleMessageWireLayout
{
    Unknown,
    LegacyFull,
    TaggedHabbicon,
    LegacyCompact
}

public enum UnityRoomSettingsWireLayout
{
    Unknown,
    Legacy,
    Modern
}

public sealed class WireProfilePendingException(string area)
    : InvalidOperationException(
        $"The client catalog is still loading, so the {area} wire profile is not known yet.")
{
    public string Area { get; } = area;
}

public readonly record struct MessageWireProfile(
    MessageWiredContextLayout WiredContextLayout,
    bool? WiredConditionHasSeparateInvert,
    bool IsAnalyzed = true,
    bool? UnityAvatarStatusHasTargetId = null,
    bool? UnityUpdateAvatarHasBadgeRank = null,
    bool? UnityInventoryItemHasExtendedMetadata = null,
    GuestRoomResultWireLayout? FlashGuestRoomResultLayout = null,
    bool? UnityGuestRoomResultHasExtendedData = null,
    bool? UnityCraftingProductHasProductCode = null,
    MarketplaceBuyWireLayout UnityMarketplaceBuyLayout =
        MarketplaceBuyWireLayout.Unknown,
    short? UnityMarketplaceBuyHeaderId = null,
    FlashMarketplaceWireLayout FlashMarketplaceLayout =
        FlashMarketplaceWireLayout.Unknown,
    ConsoleMessageWireLayout UnityConsoleMessageLayout =
        ConsoleMessageWireLayout.Unknown,
    UnityRoomSettingsWireLayout UnityRoomSettingsLayout =
        UnityRoomSettingsWireLayout.Unknown)
{
    public MessageWireProfile(
        MessageWiredContextLayout WiredContextLayout,
        bool? WiredConditionHasSeparateInvert,
        bool IsAnalyzed)
        : this(
            WiredContextLayout,
            WiredConditionHasSeparateInvert,
            IsAnalyzed,
            null,
            null,
            null,
            null,
            null,
            null,
            MarketplaceBuyWireLayout.Unknown,
            null,
            FlashMarketplaceWireLayout.Unknown)
    {
    }

    public bool IsExact =>
        IsAnalyzed &&
        WiredContextLayout is not MessageWiredContextLayout.Unknown &&
        WiredConditionHasSeparateInvert is not null;
    public bool IsUnsupported => IsAnalyzed && !IsExact;
    public bool HasExactUnityIncomingLayout =>
        IsAnalyzed &&
        UnityAvatarStatusHasTargetId is not null &&
        UnityUpdateAvatarHasBadgeRank is not null &&
        UnityInventoryItemHasExtendedMetadata is not null &&
        UnityGuestRoomResultHasExtendedData is true &&
        UnityCraftingProductHasProductCode is not null &&
        UnityConsoleMessageLayout is not ConsoleMessageWireLayout.Unknown &&
        UnityRoomSettingsLayout is not UnityRoomSettingsWireLayout.Unknown;

    public bool HasExactIncomingLayout(ClientType client) =>
        MissingIncomingCapabilities(client).Count == 0;

    public IReadOnlyList<string> MissingIncomingCapabilities(ClientType client)
    {
        var missing = new List<string>();
        if (!IsAnalyzed)
            missing.Add("analysis");
        if (WiredContextLayout is MessageWiredContextLayout.Unknown)
            missing.Add("wiredContext");
        if (WiredConditionHasSeparateInvert is null)
            missing.Add("wiredConditionInvert");

        switch (client)
        {
            case ClientType.Flash:
                if (FlashGuestRoomResultLayout is null)
                    missing.Add("guestRoomResult");
                break;
            case ClientType.Unity:
                if (UnityAvatarStatusHasTargetId is null)
                    missing.Add("avatarStatusTargetId");
                if (UnityUpdateAvatarHasBadgeRank is null)
                    missing.Add("updateAvatarBadgeRank");
                if (UnityInventoryItemHasExtendedMetadata is null)
                    missing.Add("inventoryExtendedMetadata");
                if (UnityGuestRoomResultHasExtendedData is not true)
                    missing.Add("guestRoomResult");
                if (UnityCraftingProductHasProductCode is null)
                    missing.Add("craftingProduct");
                if (UnityConsoleMessageLayout is ConsoleMessageWireLayout.Unknown)
                    missing.Add("consoleMessage");
                if (UnityRoomSettingsLayout is UnityRoomSettingsWireLayout.Unknown)
                    missing.Add("roomSettings");
                break;
            default:
                missing.Add("client");
                break;
        }

        return missing;
    }

    public bool RequireUnityAvatarStatusTargetId()
    {
        RequireAnalyzed("avatar status");
        return UnityAvatarStatusHasTargetId ??
            throw new NotSupportedException("The active Unity session has no compatible avatar status wire layout.");
    }

    public bool RequireUnityUpdateAvatarBadgeRank()
    {
        RequireAnalyzed("avatar update");
        return UnityUpdateAvatarHasBadgeRank ??
            throw new NotSupportedException("The active Unity session has no compatible avatar update wire layout.");
    }

    public bool RequireUnityInventoryExtendedMetadata()
    {
        RequireAnalyzed("inventory item");
        return UnityInventoryItemHasExtendedMetadata ??
            throw new NotSupportedException("The active Unity session has no compatible inventory item wire layout.");
    }

    public bool RequireUnityCraftingProductCode()
    {
        RequireAnalyzed("crafting product");
        return UnityCraftingProductHasProductCode ??
            throw new NotSupportedException("The active Unity session has no compatible crafting product wire layout.");
    }

    public ConsoleMessageWireLayout RequireUnityConsoleMessageLayout()
    {
        RequireAnalyzed("console message");
        if (UnityConsoleMessageLayout is ConsoleMessageWireLayout.Unknown)
            throw new NotSupportedException("The active Unity session has no compatible console message wire layout.");
        return UnityConsoleMessageLayout;
    }

    public UnityRoomSettingsWireLayout RequireUnityRoomSettingsLayout()
    {
        RequireAnalyzed("room settings");
        if (UnityRoomSettingsLayout is UnityRoomSettingsWireLayout.Unknown)
            throw new NotSupportedException("The active Unity session has no compatible room settings wire layout.");
        return UnityRoomSettingsLayout;
    }

    public MarketplaceBuyWireLayout RequireUnityMarketplaceBuyLayout()
    {
        RequireAnalyzed("marketplace purchase");
        if (UnityMarketplaceBuyLayout is MarketplaceBuyWireLayout.Unknown ||
            UnityMarketplaceBuyHeaderId is null)
        {
            throw new NotSupportedException(
                "The active Unity session has no compatible marketplace purchase wire layout.");
        }
        return UnityMarketplaceBuyLayout;
    }

    public FlashMarketplaceWireLayout RequireFlashMarketplaceLayout()
    {
        RequireAnalyzed("marketplace");
        if (FlashMarketplaceLayout is FlashMarketplaceWireLayout.Unknown)
        {
            throw new NotSupportedException(
                "The active Flash build has no exact marketplace wire profile.");
        }
        return FlashMarketplaceLayout;
    }

    /// <summary>
    /// Separates a profile that has not been read yet from one that was read and could not tell.
    /// </summary>
    /// <remarks>
    /// Reading the wire profile out of the Flash client takes the better part of a minute, and
    /// until it lands every layout reads as unknown. Reporting that as a build without a profile
    /// sends anyone looking at it after a parser that is in fact correct.
    /// </remarks>
    void RequireAnalyzed(string area)
    {
        if (!IsAnalyzed)
            throw new WireProfilePendingException(area);
    }

    public GuestRoomResultWireLayout RequireGuestRoomResultLayout(ClientType client)
    {
        RequireAnalyzed("guest room result");
        return client switch
        {
            ClientType.Flash => FlashGuestRoomResultLayout ??
                throw new NotSupportedException("The active Flash build has no exact guest room result wire profile."),
            ClientType.Unity when UnityGuestRoomResultHasExtendedData is true =>
                GuestRoomResultWireLayout.Unity,
            ClientType.Unity => throw new NotSupportedException(
                "The active Unity session has no compatible guest room result wire layout."),
            _ => throw new UnsupportedClientException(client)
        };
    }

    public void Deconstruct(
        out MessageWiredContextLayout wired_context_layout,
        out bool? wired_condition_has_separate_invert,
        out bool is_analyzed)
    {
        wired_context_layout = WiredContextLayout;
        wired_condition_has_separate_invert = WiredConditionHasSeparateInvert;
        is_analyzed = IsAnalyzed;
    }
}

public sealed record ParserContext(
    IMessageManager Messages,
    MessageWireProfile WireProfile = default) : IParserContext;
