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
    FlashCompactChat
}

public enum FlashMarketplaceWireLayout
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
    GuestRoomResultWireLayout? FlashGuestRoomResultLayout = null,
    FlashMarketplaceWireLayout FlashMarketplaceLayout =
        FlashMarketplaceWireLayout.Unknown)
{
    public bool IsExact =>
        IsAnalyzed &&
        WiredContextLayout is not MessageWiredContextLayout.Unknown &&
        WiredConditionHasSeparateInvert is not null;
    public bool IsUnsupported => IsAnalyzed && !IsExact;

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
            default:
                missing.Add("client");
                break;
        }

        return missing;
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
