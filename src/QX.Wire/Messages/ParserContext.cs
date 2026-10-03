namespace Qx.Messages;

/// <summary>Specifies how a client build lays out the context data at the end of wired configuration messages.</summary>
public enum MessageWiredContextLayout
{
    /// <summary>The layout is not known.</summary>
    Unknown,
    /// <summary>Wired configurations carry no context data.</summary>
    None,
    /// <summary>Wired configurations carry context tags and default integer parameters.</summary>
    Tags,
    /// <summary>Wired configurations carry full context entries and default integer parameters.</summary>
    Full
}

/// <summary>Specifies how a Flash build lays out the chat settings and trailing fields of a guest room result.</summary>
public enum GuestRoomResultWireLayout
{
    /// <summary>Full chat settings (flow, bubble width, scroll speed, hearing distance and flood protection) with no opening connection flag.</summary>
    FlashFullChat,
    /// <summary>Full chat settings followed by an opening connection flag.</summary>
    FlashFullChatWithOpening,
    /// <summary>Only the flood protection setting, followed by an opening connection flag.</summary>
    FlashCompactChat
}

/// <summary>Specifies which marketplace message layout a Flash build uses.</summary>
public enum FlashMarketplaceWireLayout
{
    /// <summary>The layout is not known.</summary>
    Unknown,
    /// <summary>The older layout, in which an offer is made for a single item.</summary>
    Legacy,
    /// <summary>The newer layout, in which an offer is made for a list of items and requests carry extra fields.</summary>
    Modern
}

/// <summary>Thrown when a wire profile is needed before the client catalog has finished loading.</summary>
/// <param name="area">The protocol area that needs the profile, used in the message.</param>
public sealed class WireProfilePendingException(string area)
    : InvalidOperationException(
        $"The client catalog is still loading, so the {area} wire profile is not known yet.")
{
    /// <summary>Gets the protocol area that needed the profile, for example <c>marketplace</c>.</summary>
    public string Area { get; } = area;
}

/// <summary>Represents the wire layouts of a client build for the messages whose format differs between builds.</summary>
/// <remarks>
/// The <see langword="default"/> value is not analyzed, so its <c>Require</c> methods throw
/// <see cref="WireProfilePendingException"/>.
/// </remarks>
/// <param name="WiredContextLayout">The layout of the context data at the end of wired configuration messages.</param>
/// <param name="WiredConditionHasSeparateInvert">Whether wired condition configurations carry a separate invert flag, or <see langword="null"/> when not known.</param>
/// <param name="IsAnalyzed">Whether the profile comes from an analysis of the client build.</param>
/// <param name="FlashGuestRoomResultLayout">The Flash guest room result layout, or <see langword="null"/> when not known.</param>
/// <param name="FlashMarketplaceLayout">The Flash marketplace layout.</param>
public readonly record struct MessageWireProfile(
    MessageWiredContextLayout WiredContextLayout,
    bool? WiredConditionHasSeparateInvert,
    bool IsAnalyzed = true,
    GuestRoomResultWireLayout? FlashGuestRoomResultLayout = null,
    FlashMarketplaceWireLayout FlashMarketplaceLayout =
        FlashMarketplaceWireLayout.Unknown)
{
    /// <summary>Gets whether the profile is analyzed and both the wired context layout and the wired condition invert flag are known.</summary>
    public bool IsExact =>
        IsAnalyzed &&
        WiredContextLayout is not MessageWiredContextLayout.Unknown &&
        WiredConditionHasSeparateInvert is not null;
    /// <summary>Gets whether the profile is analyzed but not exact.</summary>
    public bool IsUnsupported => IsAnalyzed && !IsExact;

    /// <summary>Gets whether every layout that incoming messages need is known.</summary>
    /// <returns><see langword="true"/> if <see cref="MissingIncomingCapabilities"/> returns an empty list; otherwise, <see langword="false"/>.</returns>
    public bool HasExactIncomingLayout() =>
        MissingIncomingCapabilities().Count == 0;

    /// <summary>Gets the names of the layouts that are still unknown for incoming messages.</summary>
    /// <returns>Any of <c>analysis</c>, <c>wiredContext</c>, <c>wiredConditionInvert</c> and <c>guestRoomResult</c>.</returns>
    public IReadOnlyList<string> MissingIncomingCapabilities()
    {
        var missing = new List<string>();
        if (!IsAnalyzed)
            missing.Add("analysis");
        if (WiredContextLayout is MessageWiredContextLayout.Unknown)
            missing.Add("wiredContext");
        if (WiredConditionHasSeparateInvert is null)
            missing.Add("wiredConditionInvert");
        if (FlashGuestRoomResultLayout is null)
            missing.Add("guestRoomResult");
        return missing;
    }

    /// <summary>Gets the Flash marketplace layout and throws when it is not known.</summary>
    /// <returns>The Flash marketplace layout, which is never <see cref="FlashMarketplaceWireLayout.Unknown"/>.</returns>
    /// <exception cref="WireProfilePendingException">Thrown when the profile is not analyzed yet.</exception>
    /// <exception cref="NotSupportedException">Thrown when the layout is <see cref="FlashMarketplaceWireLayout.Unknown"/>.</exception>
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

    /// <summary>Gets the guest room result layout and throws when it is not known.</summary>
    /// <returns>The guest room result layout.</returns>
    /// <exception cref="WireProfilePendingException">Thrown when the profile is not analyzed yet.</exception>
    /// <exception cref="NotSupportedException">Thrown when the Flash layout is not known.</exception>
    public GuestRoomResultWireLayout RequireGuestRoomResultLayout()
    {
        RequireAnalyzed("guest room result");
        return FlashGuestRoomResultLayout ??
            throw new NotSupportedException("The active Flash build has no exact guest room result wire profile.");
    }
}

/// <summary>Represents the parser context of a session, made of its message manager and wire profile.</summary>
/// <param name="Messages">The message manager of the session.</param>
/// <param name="WireProfile">The wire profile of the connected client build.</param>
public sealed record ParserContext(
    IMessageManager Messages,
    MessageWireProfile WireProfile = default) : IParserContext;
