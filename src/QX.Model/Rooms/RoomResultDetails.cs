using Qx.Messages;

namespace Qx.Model;

public sealed class RoomModerationSettings : IParserComposer<RoomModerationSettings>
{
    public RoomModerationPermission Mute { get; set; }
    public RoomModerationPermission Kick { get; set; }
    public RoomModerationPermission Ban { get; set; }

    public static RoomModerationSettings Parse(in PacketReader p) => new()
    {
        Mute = (RoomModerationPermission)p.ReadInt(),
        Kick = (RoomModerationPermission)p.ReadInt(),
        Ban = (RoomModerationPermission)p.ReadInt()
    };

    public void Compose(in PacketWriter p)
    {
        p.WriteInt((int)Mute);
        p.WriteInt((int)Kick);
        p.WriteInt((int)Ban);
    }
}

public sealed class RoomChatSettings : IParserComposer<RoomChatSettings>
{
    private const int FlashCompactLength = sizeof(int);
    private const int FlashFullLength = sizeof(int) * 5;

    /// <summary>How chat bubbles flow in the room.</summary>
    public RoomChatFlowMode Flow { get; set; } = RoomChatFlowMode.FreeFlow;

    /// <summary>The chat bubble width the room requests.</summary>
    public RoomChatBubbleWidth BubbleWidth { get; set; } = RoomChatBubbleWidth.Normal;

    /// <summary>How fast chat bubbles scroll away.</summary>
    public RoomChatScrollSpeed ScrollSpeed { get; set; } = RoomChatScrollSpeed.Normal;

    /// <summary>How many tiles away chat is still heard.</summary>
    public int TalkHearingDistance { get; set; } = 14;

    /// <summary>The strength of the chat flood filter.</summary>
    public RoomChatFloodSensitivity FloodProtection { get; set; } = RoomChatFloodSensitivity.Normal;

    /// <summary>
    /// The layout these settings were read with, so composing them again reproduces the same bytes
    /// even where the build's layout could not be established up front.
    /// </summary>
    internal GuestRoomResultWireLayout? ParsedLayout { get; set; }

    public static RoomChatSettings Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    internal static RoomChatSettings ParseEmbedded(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseEmbeddedFlash, ParseUnity);

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

    private static RoomChatSettings ParseUnity(in PacketReader p) => new()
    {
        Flow = (RoomChatFlowMode)p.ReadInt(),
        BubbleWidth = (RoomChatBubbleWidth)p.ReadInt(),
        ScrollSpeed = (RoomChatScrollSpeed)p.ReadInt(),
        TalkHearingDistance = p.ReadInt(),
        FloodProtection = (RoomChatFloodSensitivity)p.ReadInt(),
        ParsedLayout = GuestRoomResultWireLayout.Unity
    };

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    internal void ComposeEmbedded(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeEmbeddedFlash, ComposeUnity);

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

    private static void ComposeUnity(RoomChatSettings value, in PacketWriter p)
    {
        p.WriteInt((int)value.Flow);
        p.WriteInt((int)value.BubbleWidth);
        p.WriteInt((int)value.ScrollSpeed);
        p.WriteInt(value.TalkHearingDistance);
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
/// the chat settings begin — five bytes for the compact form, twenty-one and twenty for the two
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
        if (Known(p.Context?.WireProfile, p.Client) is { } known)
            return known;
        if (p.Client is ClientType.Flash)
            return FromRemainder(p.Available);
        return p.Context?.WireProfile.RequireGuestRoomResultLayout(p.Client) ??
            throw new NotSupportedException("Guest room result details require an exact wire profile.");
    }

    internal static GuestRoomResultWireLayout Resolve(
        in PacketWriter p,
        GuestRoomResultWireLayout? parsed)
    {
        if (Known(p.Context?.WireProfile, p.Client) is { } known)
            return known;
        // Nothing to measure while writing, so a value carried over from parsing is the only other
        // evidence there is.
        return parsed ??
            throw new NotSupportedException(
                "Guest room result details require an exact wire profile, or a layout carried over " +
                "from the packet they were read from.");
    }

    private static GuestRoomResultWireLayout? Known(MessageWireProfile? profile, ClientType client)
    {
        if (profile is not { } wire)
            return null;
        return client switch
        {
            ClientType.Flash => wire.FlashGuestRoomResultLayout,
            ClientType.Unity when wire.UnityGuestRoomResultHasExtendedData is true =>
                GuestRoomResultWireLayout.Unity,
            _ => null
        };
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

public sealed record RoomThumbnailData(Id RoomId, string Reference, string ImageUrl)
    : IParserComposer<RoomThumbnailData>
{
    public static RoomThumbnailData Parse(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteId(RoomId);
        p.WriteString(Reference);
        p.WriteString(ImageUrl);
    }
}

public sealed class RoomResultDetails : IParserComposer<RoomResultDetails>
{
    public bool Forward { get; set; }
    public bool IsStaffPick { get; set; }
    public bool IsGroupMember { get; set; }
    public bool IsRoomMuted { get; set; }
    public RoomModerationSettings Moderation { get; set; } = new();
    public bool CanMute { get; set; }
    public RoomChatSettings Chat { get; set; } = new();
    public bool? OpeningConnection { get; set; }
    public Id UnityContextId { get; set; }
    public RoomThumbnailData? UnityThumbnail { get; set; }

    /// <inheritdoc cref="RoomChatSettings.ParsedLayout"/>
    internal GuestRoomResultWireLayout? ParsedLayout { get; set; }

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
        else if (layout is GuestRoomResultWireLayout.Unity)
        {
            details.UnityContextId = p.ReadId();
            if (p.ReadBool())
                details.UnityThumbnail = p.Parse<RoomThumbnailData>();
        }

        return details;
    }

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
        else if (layout is GuestRoomResultWireLayout.Unity)
        {
            p.WriteId(UnityContextId);
            p.WriteBool(UnityThumbnail is not null);
            if (UnityThumbnail is not null)
                p.Compose(UnityThumbnail);
        }
    }
}
