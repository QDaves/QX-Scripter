using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents who may mute, kick and ban in a room.</summary>
public sealed class RoomModerationSettings : IParserComposer<RoomModerationSettings>
{
    /// <summary>Gets or sets who may mute other users.</summary>
    public RoomModerationPermission Mute { get; set; }
    /// <summary>Gets or sets who may kick other users.</summary>
    public RoomModerationPermission Kick { get; set; }
    /// <summary>Gets or sets who may ban other users.</summary>
    public RoomModerationPermission Ban { get; set; }

    /// <summary>Reads the moderation settings from a packet as three integers.</summary>
    /// <param name="p">The packet to read from.</param>
    public static RoomModerationSettings Parse(in PacketReader p) => new()
    {
        Mute = (RoomModerationPermission)p.ReadInt(),
        Kick = (RoomModerationPermission)p.ReadInt(),
        Ban = (RoomModerationPermission)p.ReadInt()
    };

    /// <summary>Writes the moderation settings to a packet as three integers.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt((int)Mute);
        p.WriteInt((int)Kick);
        p.WriteInt((int)Ban);
    }
}

/// <summary>Represents a room's chat configuration.</summary>
/// <remarks>
/// Some Flash builds send only <see cref="FloodProtection"/>; the other properties then keep their
/// defaults.
/// </remarks>
public sealed class RoomChatSettings : IParserComposer<RoomChatSettings>
{
    private const int FlashCompactLength = sizeof(int);
    private const int FlashFullLength = sizeof(int) * 5;

    /// <summary>Gets or sets how chat bubbles flow in the room.</summary>
    public RoomChatFlowMode Flow { get; set; } = RoomChatFlowMode.FreeFlow;

    /// <summary>Gets or sets the chat bubble width the room requests.</summary>
    public RoomChatBubbleWidth BubbleWidth { get; set; } = RoomChatBubbleWidth.Normal;

    /// <summary>Gets or sets how fast chat bubbles scroll away.</summary>
    public RoomChatScrollSpeed ScrollSpeed { get; set; } = RoomChatScrollSpeed.Normal;

    /// <summary>Gets or sets how many tiles away chat is still heard; defaults to 14.</summary>
    public int TalkHearingDistance { get; set; } = 14;

    /// <summary>Gets or sets the strength of the chat flood filter.</summary>
    public RoomChatFloodSensitivity FloodProtection { get; set; } = RoomChatFloodSensitivity.Normal;

    /// <summary>
    /// The layout these settings were read with, so composing them again reproduces the same bytes
    /// even where the build's layout could not be established up front.
    /// </summary>
    internal GuestRoomResultWireLayout? ParsedLayout { get; set; }

    /// <summary>Reads standalone chat settings from a packet.</summary>
    /// <remarks>
    /// The layout is chosen by the bytes left: 4 for the compact form with only
    /// <see cref="FloodProtection"/>, or 20 for the full form.
    /// </remarks>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="NotSupportedException">Thrown when the bytes left match neither layout.</exception>
    public static RoomChatSettings Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    internal static RoomChatSettings ParseEmbedded(in PacketReader p) =>
        FlashWire.Parse(in p, ParseEmbeddedFlash);

    private static RoomChatSettings ParseFlash(in PacketReader p)
    {
        GuestRoomResultWireLayout layout = p.Available switch
        {
            FlashCompactLength => GuestRoomResultWireLayout.FlashCompactChat,
            FlashFullLength => GuestRoomResultWireLayout.FlashFullChat,
            _ => throw new NotSupportedException(
                $"Standalone Flash room chat settings contain {p.Available} bytes; " +
                $"the supported layouts contain {FlashCompactLength} or {FlashFullLength} bytes.")
        };
        RoomChatSettings settings = layout is GuestRoomResultWireLayout.FlashCompactChat
            ? new RoomChatSettings
            {
                FloodProtection = (RoomChatFloodSensitivity)p.ReadInt()
            }
            : new RoomChatSettings
            {
                Flow = (RoomChatFlowMode)p.ReadInt(),
                BubbleWidth = (RoomChatBubbleWidth)p.ReadInt(),
                ScrollSpeed = (RoomChatScrollSpeed)p.ReadInt(),
                TalkHearingDistance = p.ReadInt(),
                FloodProtection = (RoomChatFloodSensitivity)p.ReadInt()
            };
        settings.ParsedLayout = layout;
        return settings;
    }

    private static RoomChatSettings ParseEmbeddedFlash(in PacketReader p)
    {
        GuestRoomResultWireLayout layout = GuestRoomResultLayout.Resolve(in p);
        RoomChatSettings settings = layout switch
        {
            GuestRoomResultWireLayout.FlashCompactChat => new RoomChatSettings
            {
                FloodProtection = (RoomChatFloodSensitivity)p.ReadInt()
            },
            _ => new RoomChatSettings
            {
                Flow = (RoomChatFlowMode)p.ReadInt(),
                BubbleWidth = (RoomChatBubbleWidth)p.ReadInt(),
                ScrollSpeed = (RoomChatScrollSpeed)p.ReadInt(),
                TalkHearingDistance = p.ReadInt(),
                FloodProtection = (RoomChatFloodSensitivity)p.ReadInt()
            }
        };
        settings.ParsedLayout = layout;
        return settings;
    }

    /// <summary>Writes the chat settings to a packet.</summary>
    /// <remarks>
    /// The full form is written only when the settings were parsed from a full layout; otherwise
    /// only <see cref="FloodProtection"/> is written.
    /// </remarks>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    internal void ComposeEmbedded(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeEmbeddedFlash);

    private static void ComposeFlash(RoomChatSettings value, in PacketWriter p)
    {
        if (value.ParsedLayout is
            GuestRoomResultWireLayout.FlashFullChat or
            GuestRoomResultWireLayout.FlashFullChatWithOpening)
        {
            p.WriteInt((int)value.Flow);
            p.WriteInt((int)value.BubbleWidth);
            p.WriteInt((int)value.ScrollSpeed);
            p.WriteInt(value.TalkHearingDistance);
        }
        p.WriteInt((int)value.FloodProtection);
    }

    private static void ComposeEmbeddedFlash(RoomChatSettings value, in PacketWriter p)
    {
        GuestRoomResultWireLayout layout = GuestRoomResultLayout.Resolve(in p, value.ParsedLayout);
        if (layout is not GuestRoomResultWireLayout.FlashCompactChat)
        {
            p.WriteInt((int)value.Flow);
            p.WriteInt((int)value.BubbleWidth);
            p.WriteInt((int)value.ScrollSpeed);
            p.WriteInt(value.TalkHearingDistance);
        }
        p.WriteInt((int)value.FloodProtection);
    }
}

/// <summary>
/// Works out which of the guest room result layouts a packet uses.
/// </summary>
/// <remarks>
/// <para>
/// The build's wire profile answers this when the client analysis could establish it. It cannot
/// always: the profile is derived from the extracted parser payload, and that comes out empty for
/// any parser that builds nested structures, which this message does. A build in that state would
/// otherwise fail every guest room result outright.
/// </para>
/// <para>
/// So when the profile has no answer the layout is read off the packet instead. The details are the
/// last thing in the message and the three Flash layouts leave distinct amounts behind at the point
/// the chat settings begin: five bytes for the compact form, twenty-one and twenty for the two
/// full ones. That is measurement rather than a guess, and an amount matching none of them still
/// throws, now naming what was actually left.
/// </para>
/// </remarks>
internal static class GuestRoomResultLayout
{
    private const int CompactChatTail = 5;
    private const int FullChatWithOpeningTail = 21;
    private const int FullChatTail = 20;

    internal static GuestRoomResultWireLayout Resolve(in PacketReader p)
    {
        if (p.Context?.WireProfile.FlashGuestRoomResultLayout is { } known)
            return known;
        return FromRemainder(p.Available);
    }

    internal static GuestRoomResultWireLayout Resolve(
        in PacketWriter p,
        GuestRoomResultWireLayout? parsed)
    {
        if (p.Context?.WireProfile.FlashGuestRoomResultLayout is { } known)
            return known;
        // Nothing to measure while writing, so a value carried over from parsing is the only other
        // evidence there is.
        return parsed ??
            throw new NotSupportedException(
                "Guest room result details require an exact wire profile, or a layout carried over " +
                "from the packet they were read from.");
    }

    private static GuestRoomResultWireLayout FromRemainder(int available) => available switch
    {
        CompactChatTail => GuestRoomResultWireLayout.FlashCompactChat,
        FullChatWithOpeningTail => GuestRoomResultWireLayout.FlashFullChatWithOpening,
        FullChatTail => GuestRoomResultWireLayout.FlashFullChat,
        _ => throw new NotSupportedException(
            $"The active Flash build has no exact guest room result wire profile, and its chat " +
            $"settings leave {available} bytes, which matches no known layout " +
            $"({CompactChatTail}, {FullChatWithOpeningTail} or {FullChatTail}).")
    };
}

/// <summary>Represents a room's thumbnail image.</summary>
/// <param name="RoomId">The room identifier.</param>
/// <param name="Reference">The thumbnail reference as sent by the hotel.</param>
/// <param name="ImageUrl">The thumbnail image URL.</param>
public sealed record RoomThumbnailData(Id RoomId, string Reference, string ImageUrl)
    : IParserComposer<RoomThumbnailData>
{
    /// <summary>Reads thumbnail data from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static RoomThumbnailData Parse(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadString());

    /// <summary>Writes the thumbnail data to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteId(RoomId);
        p.WriteString(Reference);
        p.WriteString(ImageUrl);
    }
}

/// <summary>Represents the part of a guest room result that follows the <see cref="RoomData"/>.</summary>
public sealed class RoomResultDetails : IParserComposer<RoomResultDetails>
{
    /// <summary>Gets or sets the forward flag of the guest room result.</summary>
    public bool Forward { get; set; }
    /// <summary>Gets or sets whether the room is a staff pick.</summary>
    public bool IsStaffPick { get; set; }
    /// <summary>Gets or sets whether the local user is a member of the group that owns the room.</summary>
    public bool IsGroupMember { get; set; }
    /// <summary>Gets or sets whether the room is muted for everyone.</summary>
    public bool IsRoomMuted { get; set; }
    /// <summary>Gets or sets who may mute, kick and ban in the room.</summary>
    public RoomModerationSettings Moderation { get; set; } = new();
    /// <summary>Gets or sets whether the local user may mute others.</summary>
    public bool CanMute { get; set; }
    /// <summary>Gets or sets the room's chat configuration.</summary>
    public RoomChatSettings Chat { get; set; } = new();
    /// <summary>
    /// Gets or sets the trailing opening connection flag, or <see langword="null"/> when the
    /// packet's layout does not carry it.
    /// </summary>
    /// <remarks>Composing a layout that carries the flag requires a value.</remarks>
    public bool? OpeningConnection { get; set; }

    /// <inheritdoc cref="RoomChatSettings.ParsedLayout"/>
    internal GuestRoomResultWireLayout? ParsedLayout { get; set; }

    /// <summary>Reads the guest room details from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="NotSupportedException">Thrown when the layout of the chat settings cannot be determined.</exception>
    public static RoomResultDetails Parse(in PacketReader p)
    {
        var details = new RoomResultDetails
        {
            Forward = p.ReadBool(),
            IsStaffPick = p.ReadBool(),
            IsGroupMember = p.ReadBool(),
            IsRoomMuted = p.ReadBool(),
            Moderation = p.Parse<RoomModerationSettings>(),
            CanMute = p.ReadBool(),
            Chat = RoomChatSettings.ParseEmbedded(in p)
        };

        // Taken from the chat settings rather than resolved again: measuring the remainder only
        // works where the chat settings start, and by here four booleans and the moderation block
        // have already been consumed.
        GuestRoomResultWireLayout layout = details.Chat.ParsedLayout
            ?? GuestRoomResultLayout.Resolve(in p);
        details.ParsedLayout = layout;

        if (layout is
            GuestRoomResultWireLayout.FlashFullChatWithOpening or
            GuestRoomResultWireLayout.FlashCompactChat)
        {
            details.OpeningConnection = p.ReadBool();
        }

        return details;
    }

    /// <summary>Writes the guest room details to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown when the wire layout is unknown and the details were not parsed from a packet.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the layout carries the opening connection flag and
    /// <see cref="OpeningConnection"/> is <see langword="null"/>.
    /// </exception>
    public void Compose(in PacketWriter p)
    {
        GuestRoomResultWireLayout layout = GuestRoomResultLayout.Resolve(in p, ParsedLayout);
        p.WriteBool(Forward);
        p.WriteBool(IsStaffPick);
        p.WriteBool(IsGroupMember);
        p.WriteBool(IsRoomMuted);
        p.Compose(Moderation);
        p.WriteBool(CanMute);
        Chat.ComposeEmbedded(in p);

        if (layout is
            GuestRoomResultWireLayout.FlashFullChatWithOpening or
            GuestRoomResultWireLayout.FlashCompactChat)
        {
            p.WriteBool(OpeningConnection ??
                throw new InvalidOperationException("The compact Flash layout requires an opening-connection value."));
        }
    }
}
