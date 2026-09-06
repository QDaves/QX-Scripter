using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RoomSettings : IParserComposer<RoomSettings>
{
    private const int FlashFixedTailBytes = 57;
    private const int UnityVisitorLimitBytes = 13;
    private const int UnityModernBytes = 12;

    private IReadOnlyList<string> _tags = Array.AsReadOnly(Array.Empty<string>());
    private IReadOnlyList<Id> _nft_group_ids = Array.AsReadOnly(Array.Empty<Id>());

    public Id RoomId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public RoomDoorMode DoorMode { get; init; }
    public int CategoryId { get; init; }
    public int MaximumVisitors { get; init; }
    public int MaximumVisitorsLimit { get; init; }
    public int MaximumVisitorsLowerLimit { get; init; }
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        init => _tags = Freeze(value, nameof(Tags));
    }
    public RoomTradeMode TradeMode { get; init; }
    public bool AllowPets { get; init; }
    public bool AllowFoodConsume { get; init; }
    public bool AllowWalkThrough { get; init; }
    public bool HideWalls { get; init; }
    public RoomThickness WallThickness { get; init; }
    public RoomThickness FloorThickness { get; init; }
    public RoomChatFloodSensitivity ChatFloodSensitivity { get; init; }
    public bool LeaveOnDoorTile { get; init; }
    public bool IdleSleepEnabled { get; init; }
    public int IdleSleepTimeoutSeconds { get; init; }
    public bool IdleAutokickEnabled { get; init; }
    public int IdleAutokickTimeoutSeconds { get; init; }
    public bool MuteAllPets { get; init; }
    public bool HiddenByBc { get; init; }
    public bool IsGroupRoom { get; init; }
    public int GroupRightsPolicy { get; init; }
    public bool RequiresBuildersClub { get; init; }
    public IReadOnlyList<Id> NftGroupIds
    {
        get => _nft_group_ids;
        init => _nft_group_ids = Freeze(value, nameof(NftGroupIds));
    }
    public bool IsHabboXDemoRoom { get; init; }
    public RoomModerationPermission WhoCanMute { get; init; }
    public RoomModerationPermission WhoCanKick { get; init; }
    public RoomModerationPermission WhoCanBan { get; init; }

    internal UnityRoomSettingsWireLayout? UnityWireLayout { get; init; }
    internal int? UnityVisitorFieldCount { get; init; }

    public static RoomSettings Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomSettings ParseFlash(in PacketReader p)
    {
        Id room_id = p.ReadInt();
        string name = p.ReadString();
        string description = p.ReadString();
        RoomDoorMode door_mode = (RoomDoorMode)p.ReadInt();
        int category_id = p.ReadInt();
        int maximum_visitors = p.ReadInt();
        int maximum_visitors_limit = p.ReadInt();
        int tag_count = p.ReadInt();
        if (tag_count < 0)
            throw new InvalidDataException($"{nameof(RoomSettings)} has a negative tag count.");
        long minimum_remaining = FlashFixedTailBytes + (long)tag_count * 2;
        if (p.Available < minimum_remaining)
            throw new InvalidDataException($"{nameof(RoomSettings)} tag count exceeds the remaining payload.");

        var tags = new string[tag_count];
        for (int i = 0; i < tags.Length; i++)
            tags[i] = p.ReadString();

        RoomTradeMode trade_mode = (RoomTradeMode)p.ReadInt();
        bool allow_pets = p.ReadInt() == 1;
        bool allow_food = p.ReadInt() == 1;
        bool allow_walk = p.ReadInt() == 1;
        bool hide_walls = p.ReadInt() == 1;
        RoomThickness wall_thickness = (RoomThickness)p.ReadInt();
        RoomThickness floor_thickness = (RoomThickness)p.ReadInt();
        RoomChatFloodSensitivity chat_flood = (RoomChatFloodSensitivity)p.ReadInt();
        bool leave_on_door_tile = p.ReadBool();
        bool idle_sleep = p.ReadBool();
        int idle_sleep_timeout = p.ReadInt();
        bool idle_autokick = p.ReadBool();
        int idle_autokick_timeout = p.ReadInt();
        bool mute_all_pets = p.ReadBool();
        RoomModerationPermission who_can_mute = (RoomModerationPermission)p.ReadInt();
        RoomModerationPermission who_can_kick = (RoomModerationPermission)p.ReadInt();
        RoomModerationPermission who_can_ban = (RoomModerationPermission)p.ReadInt();
        bool hidden_by_bc = p.ReadBool();
        RequireEmpty(in p);

        return new RoomSettings
        {
            RoomId = room_id,
            Name = name,
            Description = description,
            DoorMode = door_mode,
            CategoryId = category_id,
            MaximumVisitors = maximum_visitors,
            MaximumVisitorsLimit = maximum_visitors_limit,
            Tags = tags,
            TradeMode = trade_mode,
            AllowPets = allow_pets,
            AllowFoodConsume = allow_food,
            AllowWalkThrough = allow_walk,
            HideWalls = hide_walls,
            WallThickness = wall_thickness,
            FloorThickness = floor_thickness,
            ChatFloodSensitivity = chat_flood,
            LeaveOnDoorTile = leave_on_door_tile,
            IdleSleepEnabled = idle_sleep,
            IdleSleepTimeoutSeconds = idle_sleep_timeout,
            IdleAutokickEnabled = idle_autokick,
            IdleAutokickTimeoutSeconds = idle_autokick_timeout,
            MuteAllPets = mute_all_pets,
            WhoCanMute = who_can_mute,
            WhoCanKick = who_can_kick,
            WhoCanBan = who_can_ban,
            HiddenByBc = hidden_by_bc
        };
    }

    private static RoomSettings ParseUnity(in PacketReader p)
    {
        UnityRoomSettingsWireLayout layout = RequireUnityLayout(in p);
        Id room_id = p.ReadLong();
        string name = p.ReadString();
        string description = p.ReadString();
        RoomDoorMode door_mode = (RoomDoorMode)p.ReadInt();
        int category_id = p.ReadInt();
        bool allow_pets = p.ReadInt() > 0;
        bool is_group_room = p.ReadBool();
        int group_rights_policy = p.ReadInt();
        bool requires_builders_club = p.ReadBool();
        RoomModerationPermission who_can_mute = (RoomModerationPermission)p.ReadInt();
        RoomModerationPermission who_can_kick = (RoomModerationPermission)p.ReadInt();
        RoomModerationPermission who_can_ban = (RoomModerationPermission)p.ReadInt();
        if (p.Available < 2)
            throw new InvalidDataException($"{nameof(RoomSettings)} has no NFT group count.");

        int nft_group_count = unchecked((ushort)p.ReadShort());
        int mandatory_tail = layout is UnityRoomSettingsWireLayout.Modern
            ? UnityVisitorLimitBytes + UnityModernBytes
            : 0;
        long minimum_remaining = (long)nft_group_count * 8 + mandatory_tail;
        if (p.Available < minimum_remaining)
            throw new InvalidDataException($"{nameof(RoomSettings)} NFT group count exceeds the remaining payload.");

        var nft_group_ids = new Id[nft_group_count];
        for (int i = 0; i < nft_group_ids.Length; i++)
            nft_group_ids[i] = p.ReadLong();

        int tail_length = p.Available;
        int visitor_field_count = layout switch
        {
            UnityRoomSettingsWireLayout.Legacy when tail_length == 0 => 0,
            UnityRoomSettingsWireLayout.Legacy when tail_length == 1 => 1,
            UnityRoomSettingsWireLayout.Legacy when tail_length == 5 => 2,
            UnityRoomSettingsWireLayout.Legacy when tail_length == 9 => 3,
            UnityRoomSettingsWireLayout.Legacy when tail_length == UnityVisitorLimitBytes => 4,
            UnityRoomSettingsWireLayout.Modern when tail_length == UnityVisitorLimitBytes + UnityModernBytes => 4,
            _ => throw new InvalidDataException(
                $"{nameof(RoomSettings)} has an invalid {layout} tail length ({tail_length}).")
        };

        bool is_habbo_x_demo_room = false;
        int maximum_visitors = 0;
        int maximum_visitors_limit = 0;
        int maximum_visitors_lower_limit = 0;
        if (visitor_field_count >= 1)
            is_habbo_x_demo_room = p.ReadBool();
        if (visitor_field_count >= 2)
            maximum_visitors = p.ReadInt();
        if (visitor_field_count >= 3)
            maximum_visitors_limit = p.ReadInt();
        if (visitor_field_count >= 4)
            maximum_visitors_lower_limit = p.ReadInt();

        RoomTradeMode trade_mode = RoomTradeMode.Disabled;
        bool allow_food_consume = false;
        bool allow_walk_through = false;
        if (layout is UnityRoomSettingsWireLayout.Modern)
        {
            trade_mode = (RoomTradeMode)p.ReadInt();
            allow_food_consume = p.ReadInt() > 0;
            allow_walk_through = p.ReadInt() > 0;
        }
        RequireEmpty(in p);

        return new RoomSettings
        {
            RoomId = room_id,
            Name = name,
            Description = description,
            DoorMode = door_mode,
            CategoryId = category_id,
            AllowPets = allow_pets,
            IsGroupRoom = is_group_room,
            GroupRightsPolicy = group_rights_policy,
            RequiresBuildersClub = requires_builders_club,
            WhoCanMute = who_can_mute,
            WhoCanKick = who_can_kick,
            WhoCanBan = who_can_ban,
            NftGroupIds = nft_group_ids,
            IsHabboXDemoRoom = is_habbo_x_demo_room,
            MaximumVisitors = maximum_visitors,
            MaximumVisitorsLimit = maximum_visitors_limit,
            MaximumVisitorsLowerLimit = maximum_visitors_lower_limit,
            TradeMode = trade_mode,
            AllowFoodConsume = allow_food_consume,
            AllowWalkThrough = allow_walk_through,
            UnityWireLayout = layout,
            UnityVisitorFieldCount = visitor_field_count
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomSettings value, in PacketWriter p)
    {
        int room_id = checked((int)(long)value.RoomId);
        string[] tags = [.. value.Tags];
        RequireString(value.Name, nameof(Name), in p);
        RequireString(value.Description, nameof(Description), in p);
        foreach (string tag in tags)
            RequireString(tag, nameof(Tags), in p);

        p.WriteInt(room_id);
        p.WriteString(value.Name);
        p.WriteString(value.Description);
        p.WriteInt((int)value.DoorMode);
        p.WriteInt(value.CategoryId);
        p.WriteInt(value.MaximumVisitors);
        p.WriteInt(value.MaximumVisitorsLimit);
        p.WriteInt(tags.Length);
        foreach (string tag in tags)
            p.WriteString(tag);
        p.WriteInt((int)value.TradeMode);
        p.WriteInt(value.AllowPets ? 1 : 0);
        p.WriteInt(value.AllowFoodConsume ? 1 : 0);
        p.WriteInt(value.AllowWalkThrough ? 1 : 0);
        p.WriteInt(value.HideWalls ? 1 : 0);
        p.WriteInt((int)value.WallThickness);
        p.WriteInt((int)value.FloorThickness);
        p.WriteInt((int)value.ChatFloodSensitivity);
        p.WriteBool(value.LeaveOnDoorTile);
        p.WriteBool(value.IdleSleepEnabled);
        p.WriteInt(value.IdleSleepTimeoutSeconds);
        p.WriteBool(value.IdleAutokickEnabled);
        p.WriteInt(value.IdleAutokickTimeoutSeconds);
        p.WriteBool(value.MuteAllPets);
        p.WriteInt((int)value.WhoCanMute);
        p.WriteInt((int)value.WhoCanKick);
        p.WriteInt((int)value.WhoCanBan);
        p.WriteBool(value.HiddenByBc);
    }

    private static void ComposeUnity(RoomSettings value, in PacketWriter p)
    {
        UnityRoomSettingsWireLayout layout = RequireUnityLayout(in p);
        if (value.UnityWireLayout is not UnityRoomSettingsWireLayout parsed_layout || parsed_layout != layout)
            throw new NotSupportedException("The active Unity session has no compatible room settings wire layout.");
        if (value.UnityVisitorFieldCount is not int visitor_field_count ||
            visitor_field_count is < 0 or > 4)
        {
            throw new NotSupportedException("Unity room settings require their parsed visitor-field count.");
        }
        if (layout is UnityRoomSettingsWireLayout.Modern && visitor_field_count != 4)
        {
            throw new NotSupportedException("The modern Unity room settings layout requires all visitor fields.");
        }
        if ((visitor_field_count < 1 && value.IsHabboXDemoRoom) ||
            (visitor_field_count < 2 && value.MaximumVisitors != 0) ||
            (visitor_field_count < 3 && value.MaximumVisitorsLimit != 0) ||
            (visitor_field_count < 4 && value.MaximumVisitorsLowerLimit != 0))
        {
            throw new NotSupportedException("The selected Unity room settings tail cannot represent later visitor fields.");
        }
        if (layout is UnityRoomSettingsWireLayout.Legacy &&
            (value.TradeMode is not RoomTradeMode.Disabled ||
             value.AllowFoodConsume ||
             value.AllowWalkThrough))
        {
            throw new NotSupportedException("The legacy Unity room settings layout cannot represent trade or consumption settings.");
        }

        Id[] nft_group_ids = [.. value.NftGroupIds];
        if (nft_group_ids.Length > ushort.MaxValue)
            throw new ArgumentException("NftGroupIds exceeds the Unity wire count limit.", nameof(NftGroupIds));
        RequireString(value.Name, nameof(Name), in p);
        RequireString(value.Description, nameof(Description), in p);

        p.WriteLong(value.RoomId);
        p.WriteString(value.Name);
        p.WriteString(value.Description);
        p.WriteInt((int)value.DoorMode);
        p.WriteInt(value.CategoryId);
        p.WriteInt(value.AllowPets ? 1 : 0);
        p.WriteBool(value.IsGroupRoom);
        p.WriteInt(value.GroupRightsPolicy);
        p.WriteBool(value.RequiresBuildersClub);
        p.WriteInt((int)value.WhoCanMute);
        p.WriteInt((int)value.WhoCanKick);
        p.WriteInt((int)value.WhoCanBan);
        p.WriteLength((Length)(ushort)nft_group_ids.Length);
        foreach (Id nft_group_id in nft_group_ids)
            p.WriteLong(nft_group_id);
        if (visitor_field_count >= 1)
            p.WriteBool(value.IsHabboXDemoRoom);
        if (visitor_field_count >= 2)
            p.WriteInt(value.MaximumVisitors);
        if (visitor_field_count >= 3)
            p.WriteInt(value.MaximumVisitorsLimit);
        if (visitor_field_count >= 4)
            p.WriteInt(value.MaximumVisitorsLowerLimit);
        if (layout is UnityRoomSettingsWireLayout.Modern)
        {
            p.WriteInt((int)value.TradeMode);
            p.WriteInt(value.AllowFoodConsume ? 1 : 0);
            p.WriteInt(value.AllowWalkThrough ? 1 : 0);
        }
    }

    private static UnityRoomSettingsWireLayout RequireUnityLayout(in PacketReader p) =>
        p.Context?.WireProfile.RequireUnityRoomSettingsLayout() ??
        throw new NotSupportedException("Unity room settings require wire-profile context.");

    private static UnityRoomSettingsWireLayout RequireUnityLayout(in PacketWriter p) =>
        p.Context?.WireProfile.RequireUnityRoomSettingsLayout() ??
        throw new NotSupportedException("Unity room settings require wire-profile context.");

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return Array.AsReadOnly(values.ToArray());
    }

    private static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        int length = p.Encoding.GetByteCount(value);
        if (length > ushort.MaxValue)
            throw new ArgumentException($"{name} exceeds the wire string limit.", name);
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{nameof(RoomSettings)} contains {p.Available} unexpected bytes.");
    }
}

public sealed record RoomSettingsSaved(Id RoomId) : IParserComposer<RoomSettingsSaved>
{
    public static RoomSettingsSaved Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomSettingsSaved ParseFlash(in PacketReader p)
    {
        Id room_id = p.ReadInt();
        RequireEmpty(in p);
        return new RoomSettingsSaved(room_id);
    }

    private static RoomSettingsSaved ParseUnity(in PacketReader p)
    {
        Id room_id = p.ReadLong();
        RequireEmpty(in p);
        return new RoomSettingsSaved(room_id);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomSettingsSaved value, in PacketWriter p)
    {
        int room_id = checked((int)(long)value.RoomId);
        p.WriteInt(room_id);
    }

    private static void ComposeUnity(RoomSettingsSaved value, in PacketWriter p) =>
        p.WriteLong(value.RoomId);

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{nameof(RoomSettingsSaved)} contains {p.Available} unexpected bytes.");
    }
}

public sealed record RoomSettingsError(Id RoomId, int ErrorCode) : IParserComposer<RoomSettingsError>
{
    public static RoomSettingsError Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomSettingsError ParseFlash(in PacketReader p)
    {
        Id room_id = p.ReadInt();
        int error_code = p.ReadInt();
        RequireEmpty(in p);
        return new RoomSettingsError(room_id, error_code);
    }

    private static RoomSettingsError ParseUnity(in PacketReader p)
    {
        Id room_id = p.ReadLong();
        int error_code = p.ReadInt();
        RequireEmpty(in p);
        return new RoomSettingsError(room_id, error_code);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomSettingsError value, in PacketWriter p)
    {
        int room_id = checked((int)(long)value.RoomId);
        p.WriteInt(room_id);
        p.WriteInt(value.ErrorCode);
    }

    private static void ComposeUnity(RoomSettingsError value, in PacketWriter p)
    {
        p.WriteLong(value.RoomId);
        p.WriteInt(value.ErrorCode);
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{nameof(RoomSettingsError)} contains {p.Available} unexpected bytes.");
    }
}

public sealed record RoomSettingsSaveError(Id RoomId, int ErrorCode, string Info)
    : IParserComposer<RoomSettingsSaveError>
{
    public static RoomSettingsSaveError Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomSettingsSaveError ParseFlash(in PacketReader p)
    {
        Id room_id = p.ReadInt();
        int error_code = p.ReadInt();
        string info = p.ReadString();
        RequireEmpty(in p);
        return new RoomSettingsSaveError(room_id, error_code, info);
    }

    private static RoomSettingsSaveError ParseUnity(in PacketReader p)
    {
        Id room_id = p.ReadLong();
        int error_code = p.ReadInt();
        string info = p.ReadString();
        RequireEmpty(in p);
        return new RoomSettingsSaveError(room_id, error_code, info);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomSettingsSaveError value, in PacketWriter p)
    {
        int room_id = checked((int)(long)value.RoomId);
        RequireString(value.Info, in p);
        p.WriteInt(room_id);
        p.WriteInt(value.ErrorCode);
        p.WriteString(value.Info);
    }

    private static void ComposeUnity(RoomSettingsSaveError value, in PacketWriter p)
    {
        RequireString(value.Info, in p);
        p.WriteLong(value.RoomId);
        p.WriteInt(value.ErrorCode);
        p.WriteString(value.Info);
    }

    private static void RequireString(string info, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(info, nameof(Info));
        if (p.Encoding.GetByteCount(info) > ushort.MaxValue)
            throw new ArgumentException("Info exceeds the wire string limit.", nameof(Info));
    }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{nameof(RoomSettingsSaveError)} contains {p.Available} unexpected bytes.");
    }
}
