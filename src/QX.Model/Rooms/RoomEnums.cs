namespace Qx.Model;

/// <summary>
/// Who is let into a room, as sent in <c>RoomSettingsData</c>, <c>GetGuestRoomResult</c> and
/// <c>RoomData</c>. Flash names the values through <c>getDoorModeLocalizationKey</c>:
/// <c>navigator.door.mode.{open,closed,password,invisible,noobs_only}</c>.
/// </summary>
public enum RoomDoorMode
{
    /// <summary>0: anyone walks straight in. <c>navigator.door.mode.open</c>.</summary>
    Open = 0,
    /// <summary>
    /// 1: visitors must ring the bell and be let in by someone inside.
    /// <c>navigator.door.mode.closed</c>, the <c>doormode_doorbell</c> radio button.
    /// </summary>
    Doorbell = 1,
    /// <summary>2: visitors must type the room password. <c>navigator.door.mode.password</c>.</summary>
    Password = 2,
    /// <summary>
    /// 3: the room is hidden from the navigator and can only be reached by a direct link.
    /// <c>navigator.door.mode.invisible</c>.
    /// </summary>
    Invisible = 3,
    /// <summary>
    /// 4: only new accounts may enter. <c>navigator.door.mode.noobs_only</c>; the value behind
    /// <c>RoomSession.isNoobRoom</c>.
    /// </summary>
    NewUsersOnly = 4
}

/// <summary>
/// Who may trade inside a room, named by
/// <c>com.sulake.habbo.session.enum.RoomTradingLevelEnum</c> (<c>NO_TRADING</c>,
/// <c>ROOM_CONTROLLER_REQUIRED</c>, <c>FREE_TRADING</c>).
/// </summary>
public enum RoomTradeMode
{
    /// <summary>0: trading is switched off in this room. <c>NO_TRADING</c> / <c>trading.mode.not.allowed</c>.</summary>
    Disabled = 0,
    /// <summary>
    /// 1: at least one side of the trade must hold room rights.
    /// <c>ROOM_CONTROLLER_REQUIRED</c> / <c>trading.mode.controller</c>.
    /// </summary>
    RightsHolders = 1,
    /// <summary>2: any two visitors may trade. <c>FREE_TRADING</c> / <c>trading.mode.free</c>.</summary>
    Everyone = 2
}

/// <summary>
/// Who may mute, kick or ban in a room. Flash offers <c>[0,1]</c> for mute and ban and
/// <c>[0,1,2]</c> for kick in a normal room, extended with <c>[4,5]</c> once the room
/// belongs to a group.
/// </summary>
public enum RoomModerationPermission
{
    /// <summary>0: only the room owner may perform the action. <c>navigator.roomsettings.moderation.none</c>.</summary>
    OwnerOnly = 0,
    /// <summary>
    /// 1: the owner and anyone holding room rights may perform the action.
    /// <c>navigator.roomsettings.moderation.rights</c>.
    /// </summary>
    RightsHolders = 1,
    /// <summary>
    /// 2: every visitor may perform the action. <c>navigator.roomsettings.moderation.all</c>;
    /// the hotel offers this for kick only.
    /// </summary>
    Everyone = 2,
    /// <summary>
    /// 4: the owner and the group's administrators may perform the action.
    /// <c>navigator.roomsettings.moderation.group_admins</c>; group rooms only.
    /// </summary>
    GroupAdmins = 4,
    /// <summary>
    /// 5: the owner, the group's administrators and anyone holding room rights may perform the
    /// action. <c>navigator.roomsettings.moderation.group_admins_and_rights</c>; group rooms
    /// only. Flash-only: no Unity build in the corpus declares this value.
    /// </summary>
    GroupAdminsAndRightsHolders = 5
}

/// <summary>
/// How chunky the room's walls and floor slabs are drawn. The Flash room settings drop menus
/// map selection index <c>0..3</c> onto the wire value <c>index - 2</c>, and the client scales
/// the geometry by 2 raised to that value.
/// </summary>
public enum RoomThickness
{
    /// <summary>-2: quarter thickness. <c>navigator.roomsettings.wall_thickness.thinnest</c>.</summary>
    Thinnest = -2,
    /// <summary>-1: half thickness. <c>navigator.roomsettings.wall_thickness.thin</c>.</summary>
    Thin = -1,
    /// <summary>0: the default thickness. <c>navigator.roomsettings.wall_thickness.normal</c>.</summary>
    Normal = 0,
    /// <summary>1: double thickness. <c>navigator.roomsettings.wall_thickness.thick</c>.</summary>
    Thick = 1
}

/// <summary>
/// How chat bubbles are laid out in the room, the <c>mode</c> field of the Flash chat settings
/// object.
/// </summary>
public enum RoomChatFlowMode
{
    /// <summary>
    /// 0: bubbles float above each speaker and drift upwards.
    /// <c>navigator.roomsettings.chat.mode.free.flow</c>.
    /// </summary>
    FreeFlow = 0,
    /// <summary>
    /// 1: bubbles stack as a scrolling transcript instead of floating.
    /// <c>navigator.roomsettings.chat.mode.line.by.line</c>.
    /// </summary>
    LineByLine = 1
}

/// <summary>
/// How wide chat bubbles may grow before wrapping, resolved by
/// <c>ChatBubbleWidth.accordingToRoomChatSetting</c> to <c>2000</c>, <c>350</c> and <c>240</c>
/// pixels.
/// </summary>
public enum RoomChatBubbleWidth
{
    /// <summary>
    /// 0: effectively unlimited, 2000 pixels.
    /// <c>navigator.roomsettings.chat.bubbles.width.wide</c>; <c>ChatBubbleWidth.WIDE</c>.
    /// </summary>
    Wide = 0,
    /// <summary>
    /// 1: 350 pixels. <c>navigator.roomsettings.chat.bubbles.width.normal</c>;
    /// <c>ChatBubbleWidth.NORMAL</c>.
    /// </summary>
    Normal = 1,
    /// <summary>
    /// 2: 240 pixels, wrapping soonest.
    /// <c>navigator.roomsettings.chat.bubbles.width.thin</c>; <c>ChatBubbleWidth.THIN</c>.
    /// </summary>
    Thin = 2
}

/// <summary>
/// How long a chat bubble stays on screen. <c>ChatFlowStage.refreshSettings</c> turns the value
/// into a bubble lifetime of <c>3000</c>, <c>6000</c> or <c>12000</c> milliseconds.
/// </summary>
public enum RoomChatScrollSpeed
{
    /// <summary>0: bubbles disappear after 3000 ms. <c>navigator.roomsettings.chat.speed.fast</c>.</summary>
    Fast = 0,
    /// <summary>1: bubbles disappear after 6000 ms. <c>navigator.roomsettings.chat.speed.normal</c>.</summary>
    Normal = 1,
    /// <summary>2: bubbles disappear after 12000 ms. <c>navigator.roomsettings.chat.speed.slow</c>.</summary>
    Slow = 2
}

/// <summary>
/// How aggressively the room silences repeated or rapid chat, the
/// <c>chat_flood_sensitivity</c> drop menu whose selection index is written straight to the
/// wire.
/// </summary>
public enum RoomChatFloodSensitivity
{
    /// <summary>0: the filter trips soonest. <c>navigator.roomsettings.chat.flood.strict</c>.</summary>
    Strict = 0,
    /// <summary>1: the hotel default. <c>navigator.roomsettings.chat.flood.normal</c>.</summary>
    Normal = 1,
    /// <summary>2: the filter tolerates the most chat. <c>navigator.roomsettings.chat.flood.loose</c>.</summary>
    Loose = 2
}
