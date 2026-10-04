using Qx.Messages;

namespace Qx.Model.Wired;

/// <summary>The account preferences received from the hotel, including Wired preferences.</summary>
/// <remarks>Optional fields form a contiguous prefix. Null preserves an omitted field instead of substituting the client default.</remarks>
/// <param name="UiVolume">The UI sound volume.</param>
/// <param name="FurniVolume">The furniture sound volume.</param>
/// <param name="TraxVolume">The Trax volume.</param>
/// <param name="OpaqueFlag">The boolean discarded by the Flash client.</param>
/// <param name="RoomInvitesIgnored">Whether room invitations are ignored.</param>
/// <param name="RoomCameraFollowDisabled">Whether camera following is disabled.</param>
/// <param name="UiFlags">The UI preference mask.</param>
/// <param name="PreferredChatStyle">The preferred chat style.</param>
/// <param name="WiredMenuButton">Whether the Wired menu button is enabled.</param>
/// <param name="WiredInspectButton">Whether the Wired inspect button is enabled.</param>
/// <param name="PlayTestMode">Whether play-test mode is enabled.</param>
/// <param name="OpaqueValue">The integer discarded by the Flash client.</param>
/// <param name="WiredWhisperDisabled">Whether Wired whispers are disabled.</param>
/// <param name="ShowAllNotifications">Whether all notifications are shown. Null means absent from the message.</param>
/// <param name="WiredUiStyle">The Wired UI style. Null means absent from the message.</param>
/// <param name="ChatSizePreference">The preferred chat size. Null means absent from the message.</param>
/// <param name="ChatMode">The chat mode. Null means absent from the message.</param>
/// <param name="ChatBubbleWidth">The chat bubble width. Null means absent from the message.</param>
/// <param name="ChatScrollSpeed">The chat scroll speed. Null means absent from the message.</param>
/// <param name="OnlineIndicatorPreference">The online indicator preference. Null means absent from the message.</param>
public sealed record AccountPreferences(
    int UiVolume,
    int FurniVolume,
    int TraxVolume,
    bool OpaqueFlag,
    bool RoomInvitesIgnored,
    bool RoomCameraFollowDisabled,
    int UiFlags,
    int PreferredChatStyle,
    bool WiredMenuButton,
    bool WiredInspectButton,
    bool PlayTestMode,
    int OpaqueValue,
    bool WiredWhisperDisabled,
    bool? ShowAllNotifications = null,
    string? WiredUiStyle = null,
    int? ChatSizePreference = null,
    int? ChatMode = null,
    int? ChatBubbleWidth = null,
    int? ChatScrollSpeed = null,
    int? OnlineIndicatorPreference = null) : IParserComposer<AccountPreferences>
{
    /// <summary>Parses the fixed fields and each available optional field.</summary>
    /// <param name="p">The packet reader.</param>
    public static AccountPreferences Parse(in PacketReader p) =>
        FlashWire.Parse(in p, read);

    private static AccountPreferences read(in PacketReader p) => new(
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadBool(),
        p.ReadInt(),
        p.ReadBool(),
        p.Available > 0 ? p.ReadBool() : null,
        p.Available > 0 ? p.ReadString() : null,
        p.Available > 0 ? p.ReadInt() : null,
        p.Available > 0 ? p.ReadInt() : null,
        p.Available > 0 ? p.ReadInt() : null,
        p.Available > 0 ? p.ReadInt() : null,
        p.Available > 0 ? p.ReadInt() : null);

    /// <summary>Writes the preferences without filling omitted optional fields.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">An optional field follows an omitted field.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, write);

    private static void write(AccountPreferences value, in PacketWriter p)
    {
        if ((value.ShowAllNotifications is null && value.WiredUiStyle is not null) ||
            (value.WiredUiStyle is null && value.ChatSizePreference is not null) ||
            (value.ChatSizePreference is null && value.ChatMode is not null) ||
            (value.ChatMode is null && value.ChatBubbleWidth is not null) ||
            (value.ChatBubbleWidth is null && value.ChatScrollSpeed is not null) ||
            (value.ChatScrollSpeed is null && value.OnlineIndicatorPreference is not null))
            throw new InvalidDataException("Optional account preferences must form a contiguous prefix.");
        p.WriteInt(value.UiVolume);
        p.WriteInt(value.FurniVolume);
        p.WriteInt(value.TraxVolume);
        p.WriteBool(value.OpaqueFlag);
        p.WriteBool(value.RoomInvitesIgnored);
        p.WriteBool(value.RoomCameraFollowDisabled);
        p.WriteInt(value.UiFlags);
        p.WriteInt(value.PreferredChatStyle);
        p.WriteBool(value.WiredMenuButton);
        p.WriteBool(value.WiredInspectButton);
        p.WriteBool(value.PlayTestMode);
        p.WriteInt(value.OpaqueValue);
        p.WriteBool(value.WiredWhisperDisabled);
        if (value.ShowAllNotifications is { } show_all_notifications)
            p.WriteBool(show_all_notifications);
        if (value.WiredUiStyle is { } wired_ui_style)
            p.WriteString(wired_ui_style);
        if (value.ChatSizePreference is { } chat_size_preference)
            p.WriteInt(chat_size_preference);
        if (value.ChatMode is { } chat_mode)
            p.WriteInt(chat_mode);
        if (value.ChatBubbleWidth is { } chat_bubble_width)
            p.WriteInt(chat_bubble_width);
        if (value.ChatScrollSpeed is { } chat_scroll_speed)
            p.WriteInt(chat_scroll_speed);
        if (value.OnlineIndicatorPreference is { } online_indicator_preference)
            p.WriteInt(online_indicator_preference);
    }
}
