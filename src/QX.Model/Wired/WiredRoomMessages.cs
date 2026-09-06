using Qx.Messages;

namespace Qx.Model.Wired;

// Wired room settings / permissions / environment / stats / logs / misc.
// Incoming parsers verified field-for-field against the July Flash decompile; outgoing


/// <summary>
/// The wired furni whose configuration is being opened.
/// </summary>
/// <remarks>
/// This carries both directions of the open handshake, which use the same single-identifier body.
/// The hotel pushes it (Flash <c>Open</c> 2635, Unity <c>UserDefinedRoomEventsOpen</c>) to say that
/// a wired dialog should open, and the client answers with the outgoing form (Flash <c>Open</c>
/// 1869) to request the configuration itself; only then does the hotel send the matching
/// <c>WiredFurni…</c> definition. A script that wants a configuration therefore sends this rather
/// than using the furni, which merely makes the game client perform the same round trip.
/// The identifier is client-sized: the Flash parser reads an int, and the extracted Unity schema
/// for <c>UserDefinedRoomEventsOpen</c> is a single <c>Int64</c>.
/// </remarks>
public sealed record WiredOpen(Id StuffId) : IParserComposer<WiredOpen>
{
    public static WiredOpen Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredOpen ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static WiredOpen ParseUnity(in PacketReader p) => new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredOpen value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.StuffId));

    private static void ComposeUnity(WiredOpen value, in PacketWriter p) =>
        p.WriteLong(value.StuffId);
}

// id 3483 — §_-d1K§. Rights gate for the wired menu.
public sealed record WiredPermissions(bool CanModify, bool CanRead) : IParserComposer<WiredPermissions>
{
    public static WiredPermissions Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredPermissions ParseFlash(in PacketReader p) =>
        new(p.ReadBool(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredPermissions value, in PacketWriter p)
    {
        p.WriteBool(value.CanModify);
        p.WriteBool(value.CanRead);
    }
}

// id 2827 — §_-M2L§. The achievement list is guarded by bytesAvailable: on a short packet the
// count field is absent entirely, so null (list section missing) is distinct from an empty list.
public sealed record WiredEnvironment(bool HasClickUserWired, IReadOnlyList<string>? EnabledAchievements)
    : IParserComposer<WiredEnvironment>
{
    public static WiredEnvironment Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredEnvironment ParseFlash(in PacketReader p) => Read(in p);

    private static WiredEnvironment ParseUnity(in PacketReader p) => Read(in p);

    private static WiredEnvironment Read(in PacketReader p)
    {
        bool hasClickUserWired = p.ReadBool();
        IReadOnlyList<string>? achievements = null;
        if (p.Available > 0)
        {
            int n = p.ReadLength();
            var list = new string[n];
            for (int i = 0; i < n; i++)
                list[i] = p.ReadString();
            achievements = list;
        }
        return new WiredEnvironment(hasClickUserWired, achievements);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredEnvironment value, in PacketWriter p) =>
        Write(value, in p);

    private static void ComposeUnity(WiredEnvironment value, in PacketWriter p) =>
        Write(value, in p);

    private static void Write(WiredEnvironment value, in PacketWriter p)
    {
        if (value.EnabledAchievements is not null)
        {
            WiredWire.RequireUnityCount(value.EnabledAchievements.Count, nameof(EnabledAchievements));
            foreach (string achievement in value.EnabledAchievements)
                WiredWire.RequireString(achievement, nameof(EnabledAchievements), in p);
        }
        p.WriteBool(value.HasClickUserWired);
        if (value.EnabledAchievements is not null)
        {
            p.WriteLength((Length)value.EnabledAchievements.Count);
            foreach (string a in value.EnabledAchievements)
                p.WriteString(a);
        }
    }
}

// §_-q1V§/WiredRoomStatsData — the two leading cost values are doubles (8 bytes each).
public sealed record WiredRoomStatsData(
    double ExecutionCost,
    double ExecutionCostCap,
    bool IsHeavy,
    int FloorItemCount,
    int FloorItemCap,
    int WallItemCount,
    int WallItemCap,
    int PermanentFurniVariables,
    int MaxPermanentFurniVariables,
    int PermanentUserVariables,
    int MaxPermanentUserVariables,
    int PermanentGlobalVariables,
    int MaxPermanentGlobalVariables) : IParserComposer<WiredRoomStatsData>
{
    public static WiredRoomStatsData Parse(in PacketReader p) => new(
        p.ReadDouble(),
        p.ReadDouble(),
        p.ReadBool(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt(),
        p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteDouble(ExecutionCost);
        p.WriteDouble(ExecutionCostCap);
        p.WriteBool(IsHeavy);
        p.WriteInt(FloorItemCount);
        p.WriteInt(FloorItemCap);
        p.WriteInt(WallItemCount);
        p.WriteInt(WallItemCap);
        p.WriteInt(PermanentFurniVariables);
        p.WriteInt(MaxPermanentFurniVariables);
        p.WriteInt(PermanentUserVariables);
        p.WriteInt(MaxPermanentUserVariables);
        p.WriteInt(PermanentGlobalVariables);
        p.WriteInt(MaxPermanentGlobalVariables);
    }
}

// id 1964 — §_-I2r§.
public sealed record WiredRoomStats(WiredRoomStatsData RoomStats) : IParserComposer<WiredRoomStats>
{
    public static WiredRoomStats Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredRoomStats ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredRoomStatsData>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredRoomStats value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.RoomStats);
        value.RoomStats.Compose(in p);
    }
}

// §_-Am§/WiredLogEntry — id and timestamp are longs (8 bytes); logLevel/logSource are single bytes.
public sealed record WiredLogEntry(
    long Id,
    int LogLevel,
    int LogSource,
    string LogMessage,
    long Timestamp,
    string TimestampStr) : IParserComposer<WiredLogEntry>
{
    public static WiredLogEntry Parse(in PacketReader p) => new(
        p.ReadLong(),
        p.ReadByte(),
        p.ReadByte(),
        p.ReadString(),
        p.ReadLong(),
        p.ReadString());

    public void Compose(in PacketWriter p)
    {
        byte log_level = checked((byte)LogLevel);
        byte log_source = checked((byte)LogSource);
        WiredWire.RequireString(LogMessage, nameof(LogMessage), in p);
        WiredWire.RequireString(TimestampStr, nameof(TimestampStr), in p);
        p.WriteLong(Id);
        p.WriteByte(log_level);
        p.WriteByte(log_source);
        p.WriteString(LogMessage);
        p.WriteLong(Timestamp);
        p.WriteString(TimestampStr);
    }
}

// §_-Am§/WiredLogPage — the three trailing filters are presence-guarded: null means the flag byte
// was false. Level/source filters read as a single byte; default -1/-1/null in the client.
public sealed record WiredLogPage(
    int TotalEntries,
    int CurrentPage,
    int Amount,
    IReadOnlyList<WiredLogEntry> Elements,
    int? LogLevelFilter,
    int? LogSourceFilter,
    string? Query) : IParserComposer<WiredLogPage>
{
    public static WiredLogPage Parse(in PacketReader p)
    {
        int totalEntries = p.ReadInt();
        int currentPage = p.ReadInt();
        int amount = p.ReadInt();
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 22, nameof(Elements));
        var elements = new WiredLogEntry[n];
        for (int i = 0; i < n; i++)
            elements[i] = p.Parse<WiredLogEntry>();
        int? logLevelFilter = p.ReadBool() ? p.ReadByte() : null;
        int? logSourceFilter = p.ReadBool() ? p.ReadByte() : null;
        string? query = p.ReadBool() ? p.ReadString() : null;
        return new WiredLogPage(totalEntries, currentPage, amount, elements, logLevelFilter, logSourceFilter, query);
    }

    public void Compose(in PacketWriter p)
    {
        byte? log_level_filter = LogLevelFilter is int log_level
            ? checked((byte)log_level)
            : null;
        byte? log_source_filter = LogSourceFilter is int log_source
            ? checked((byte)log_source)
            : null;
        p.WriteInt(TotalEntries);
        p.WriteInt(CurrentPage);
        p.WriteInt(Amount);
        p.WriteInt(Elements.Count);
        foreach (WiredLogEntry e in Elements)
            e.Compose(p);
        p.WriteBool(log_level_filter.HasValue);
        if (log_level_filter.HasValue)
            p.WriteByte(log_level_filter.Value);
        p.WriteBool(log_source_filter.HasValue);
        if (log_source_filter.HasValue)
            p.WriteByte(log_source_filter.Value);
        p.WriteBool(Query is not null);
        if (Query is not null)
            p.WriteString(Query);
    }
}

// id 1910 — §_-Z2X§.
public sealed record WiredRoomLogs(WiredLogPage Page) : IParserComposer<WiredRoomLogs>
{
    public static WiredRoomLogs Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredRoomLogs ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredLogPage>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredRoomLogs value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Page);
        ValidateLogPage(value.Page, in p);
        value.Page.Compose(in p);
    }

    private static void ValidateLogPage(WiredLogPage page, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(page.Elements);
        if (page.LogLevelFilter is int log_level_filter)
            _ = checked((byte)log_level_filter);
        if (page.LogSourceFilter is int log_source_filter)
            _ = checked((byte)log_source_filter);
        if (page.Query is not null)
            WiredWire.RequireString(page.Query, nameof(page.Query), in p);
        foreach (WiredLogEntry entry in page.Elements)
        {
            ArgumentNullException.ThrowIfNull(entry);
            _ = checked((byte)entry.LogLevel);
            _ = checked((byte)entry.LogSource);
            WiredWire.RequireString(entry.LogMessage, nameof(entry.LogMessage), in p);
            WiredWire.RequireString(entry.TimestampStr, nameof(entry.TimestampStr), in p);
        }
    }
}

// §_-q1V§/§_-Qa§ — one wired error stat row. msSinceLastOccurrence is a long (8 bytes).
public sealed record WiredError(
    int ErrorId,
    string ErrorName,
    string Category,
    int ThrowCount,
    long MsSinceLastOccurrence) : IParserComposer<WiredError>
{
    public static WiredError Parse(in PacketReader p) => new(
        p.ReadInt(),
        p.ReadString(),
        p.ReadString(),
        p.ReadInt(),
        p.ReadLong());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(ErrorId);
        p.WriteString(ErrorName);
        p.WriteString(Category);
        p.WriteInt(ThrowCount);
        p.WriteLong(MsSinceLastOccurrence);
    }
}

// id 3419 — §_-OT§.
public sealed record WiredErrorLogs(IReadOnlyList<WiredError> Errors) : IParserComposer<WiredErrorLogs>
{
    public static WiredErrorLogs Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredErrorLogs ParseFlash(in PacketReader p)
    {
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 20, nameof(Errors));
        var errors = new WiredError[n];
        for (int i = 0; i < n; i++)
            errors[i] = p.Parse<WiredError>();
        return new WiredErrorLogs(errors);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredErrorLogs value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Errors);
        foreach (WiredError error in value.Errors)
        {
            ArgumentNullException.ThrowIfNull(error);
            WiredWire.RequireString(error.ErrorName, nameof(error.ErrorName), in p);
            WiredWire.RequireString(error.Category, nameof(error.Category), in p);
        }
        p.WriteInt(value.Errors.Count);
        foreach (WiredError e in value.Errors)
            e.Compose(p);
    }
}

// §_-71G§ — a validation-error substitution parameter.
public sealed record WiredValidationParam(string Key, string Value) : IParserComposer<WiredValidationParam>
{
    public static WiredValidationParam Parse(in PacketReader p) => new(p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteString(Key);
        p.WriteString(Value);
    }
}

// id 3201 — §_-3k§.
public sealed record WiredValidationError(string LocalizationKey, IReadOnlyList<WiredValidationParam> Parameters)
    : IParserComposer<WiredValidationError>
{
    public static WiredValidationError Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredValidationError ParseFlash(in PacketReader p)
    {
        string localizationKey = p.ReadString();
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 4, nameof(Parameters));
        var parameters = new WiredValidationParam[n];
        for (int i = 0; i < n; i++)
            parameters[i] = p.Parse<WiredValidationParam>();
        return new WiredValidationError(localizationKey, parameters);
    }

    private static WiredValidationError ParseUnity(in PacketReader p)
    {
        string localization_key = p.ReadString();
        int count = p.ReadLength();
        WiredWire.RequireBoundedCount(count, p.Available, 4, nameof(Parameters));
        var parameters = new WiredValidationParam[count];
        for (int i = 0; i < count; i++)
            parameters[i] = p.Parse<WiredValidationParam>();
        return new WiredValidationError(localization_key, parameters);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredValidationError value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteString(value.LocalizationKey);
        p.WriteInt(value.Parameters.Count);
        foreach (WiredValidationParam param in value.Parameters)
            param.Compose(p);
    }

    private static void ComposeUnity(WiredValidationError value, in PacketWriter p)
    {
        Validate(value, in p);
        WiredWire.RequireUnityCount(value.Parameters.Count, nameof(Parameters));
        p.WriteString(value.LocalizationKey);
        p.WriteLength((Length)value.Parameters.Count);
        foreach (WiredValidationParam param in value.Parameters)
            param.Compose(p);
    }

    private static void Validate(WiredValidationError value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Parameters);
        WiredWire.RequireString(value.LocalizationKey, nameof(LocalizationKey), in p);
        foreach (WiredValidationParam parameter in value.Parameters)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            WiredWire.RequireString(parameter.Key, nameof(WiredValidationParam.Key), in p);
            WiredWire.RequireString(parameter.Value, nameof(WiredValidationParam.Value), in p);
        }
    }
}

// id 1192 — §_-4X§. Empty body: a bare "config save succeeded" signal.
public sealed record WiredSaveSuccess : IParserComposer<WiredSaveSuccess>
{
    public static WiredSaveSuccess Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredSaveSuccess ParseFlash(in PacketReader p) => Read(in p);

    private static WiredSaveSuccess ParseUnity(in PacketReader p) => Read(in p);

    private static WiredSaveSuccess Read(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredSaveSuccess));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredSaveSuccess value, in PacketWriter p) { }

    private static void ComposeUnity(WiredSaveSuccess value, in PacketWriter p) { }
}

// id 1230 — §_-lC§. errorCode is a 2-byte short on the wire.
public sealed record WiredMenuError(int ErrorCode) : IParserComposer<WiredMenuError>
{
    public static WiredMenuError Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredMenuError ParseFlash(in PacketReader p) =>
        new(p.ReadShort());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredMenuError value, in PacketWriter p)
    {
        short error_code = checked((short)value.ErrorCode);
        p.WriteShort(error_code);
    }
}

// id 3931 — §_-kC§.
public sealed record WiredClickSettings(int UserOption, int FurniOption) : IParserComposer<WiredClickSettings>
{
    public static WiredClickSettings Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredClickSettings ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    private static WiredClickSettings ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredClickSettings value, in PacketWriter p)
    {
        p.WriteInt(value.UserOption);
        p.WriteInt(value.FurniOption);
    }

    private static void ComposeUnity(WiredClickSettings value, in PacketWriter p)
    {
        p.WriteInt(value.UserOption);
        p.WriteInt(value.FurniOption);
    }
}

public sealed record WiredRoomSettings(int ModifyPermissionMask, int ReadPermissionMask, string Timezone)
    : IParserComposer<WiredRoomSettings>
{
    public static WiredRoomSettings Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredRoomSettings ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredRoomSettings value, in PacketWriter p)
    {
        WiredWire.RequireString(value.Timezone, nameof(Timezone), in p);
        p.WriteInt(value.ModifyPermissionMask);
        p.WriteInt(value.ReadPermissionMask);
        p.WriteString(value.Timezone);
    }
}

public sealed record WiredClickUserResponse(int Index, bool OpenMenu)
    : IParserComposer<WiredClickUserResponse>
{
    public static WiredClickUserResponse Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredClickUserResponse ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    private static WiredClickUserResponse ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredClickUserResponse value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteBool(value.OpenMenu);
    }

    private static void ComposeUnity(WiredClickUserResponse value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteBool(value.OpenMenu);
    }
}

public sealed record WiredRewardResult(int Reason) : IParserComposer<WiredRewardResult>
{
    public static WiredRewardResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredRewardResult ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static WiredRewardResult ParseUnity(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredRewardResult value, in PacketWriter p) =>
        p.WriteInt(value.Reason);

    private static void ComposeUnity(WiredRewardResult value, in PacketWriter p) =>
        p.WriteInt(value.Reason);
}

// Composers verified push-for-push against getMessageArray().
// Parse is the exact inverse of Compose so the round-trip tests can cover the wire layout.

// id 1862 — §_-z2§. Empty. Triggers WiredRoomSettings (491).
public sealed record WiredGetRoomSettings() : IParserComposer<WiredGetRoomSettings>
{
    public static WiredGetRoomSettings Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetRoomSettings ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredGetRoomSettings));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetRoomSettings value, in PacketWriter p) { }
}

// id 2553 — §_-X1G§. Args map 1:1 onto the incoming WiredRoomSettings (491).
public sealed record WiredSetRoomSettings(int ModifyPermissionMask, int ReadPermissionMask, string Timezone)
    : IParserComposer<WiredSetRoomSettings>
{
    public static WiredSetRoomSettings Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredSetRoomSettings ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredSetRoomSettings value, in PacketWriter p)
    {
        WiredWire.RequireString(value.Timezone, nameof(Timezone), in p);
        p.WriteInt(value.ModifyPermissionMask);
        p.WriteInt(value.ReadPermissionMask);
        p.WriteString(value.Timezone);
    }
}

// id 501 — §_-v1B§. Single raw Boolean (1 byte). Nothing to do with switching wired on or off:
// WiredMenuSettingsTab sends false from onClickReload (the reload_room_btn) and true from
// onRollbackConfirmed (the roll_back_btn, behind the ${wiredmenu.settings.room_state.roll_back}
// confirmation and its .warning text).
public sealed record WiredUpdateRoom(bool Rollback) : IParserComposer<WiredUpdateRoom>
{
    /// <summary>Reloads the room's state, discarding nothing that was saved.</summary>
    public static WiredUpdateRoom Reload => new(false);

    /// <summary>Rolls the room back to its last saved state, discarding everything since.</summary>
    public static WiredUpdateRoom RollBack => new(true);

    public static WiredUpdateRoom Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredUpdateRoom ParseFlash(in PacketReader p) => new(p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredUpdateRoom value, in PacketWriter p) =>
        p.WriteBool(value.Rollback);

}

// id 3124 — §_-h1H§. Six ctor args but SEVEN values pushed: a hardcoded int 0 sits between
// PlayTestMode and WiredWhisperDisabled. A byte-exact writer MUST emit it or every field after
// desyncs. Field names taken from WiredMenuController.sendPreferences().
public sealed record WiredSetPreferences(
    bool WiredMenuButton,
    bool WiredInspectButton,
    bool PlayTestMode,
    bool WiredWhisperDisabled,
    bool ShowAllNotifications,
    string UiStyle) : IParserComposer<WiredSetPreferences>
{
    public static WiredSetPreferences Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredSetPreferences ParseFlash(in PacketReader p)
    {
        bool wiredMenuButton = p.ReadBool();
        bool wiredInspectButton = p.ReadBool();
        bool playTestMode = p.ReadBool();
        p.ReadInt();
        bool wiredWhisperDisabled = p.ReadBool();
        bool showAllNotifications = p.ReadBool();
        string uiStyle = p.ReadString();
        return new WiredSetPreferences(
            wiredMenuButton, wiredInspectButton, playTestMode, wiredWhisperDisabled, showAllNotifications, uiStyle);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredSetPreferences value, in PacketWriter p)
    {
        WiredWire.RequireString(value.UiStyle, nameof(UiStyle), in p);
        p.WriteBool(value.WiredMenuButton);
        p.WriteBool(value.WiredInspectButton);
        p.WriteBool(value.PlayTestMode);
        p.WriteInt(0);
        p.WriteBool(value.WiredWhisperDisabled);
        p.WriteBool(value.ShowAllNotifications);
        p.WriteString(value.UiStyle);
    }
}

// id 427 — §_-Iv§. Empty. Triggers WiredRoomStats (1964).
public sealed record WiredGetRoomStats() : IParserComposer<WiredGetRoomStats>
{
    public static WiredGetRoomStats Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetRoomStats ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredGetRoomStats));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetRoomStats value, in PacketWriter p) { }
}

// id 706 — §_-P1c§. Triggers WiredRoomLogs (1910); the last three args echo into WiredLogPage's
// optional filter fields. Arg order confirmed from the fixed call (1, PAGE_SIZE, -1, -1, "").
public sealed record WiredGetRoomLogs(
    int Page,
    int PageSize,
    int LogLevelFilter,
    int LogSourceFilter,
    string Query) : IParserComposer<WiredGetRoomLogs>
{
    public static WiredGetRoomLogs Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetRoomLogs ParseFlash(in PacketReader p) => Read(in p);

    private static WiredGetRoomLogs Read(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetRoomLogs value, in PacketWriter p) =>
        Write(value, in p);

    private static void Write(WiredGetRoomLogs value, in PacketWriter p)
    {
        WiredWire.RequireString(value.Query, nameof(Query), in p);
        p.WriteInt(value.Page);
        p.WriteInt(value.PageSize);
        p.WriteInt(value.LogLevelFilter);
        p.WriteInt(value.LogSourceFilter);
        p.WriteString(value.Query);
    }
}

// id 452 — §_-ZD§. Empty. Triggers WiredErrorLogs (3419).
public sealed record WiredGetErrorLogs() : IParserComposer<WiredGetErrorLogs>
{
    public static WiredGetErrorLogs Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredGetErrorLogs ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredGetErrorLogs));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetErrorLogs value, in PacketWriter p) { }
}

// id 2386 — §_-722§. Empty. No direct payload response.
public sealed record WiredClearErrorLogs() : IParserComposer<WiredClearErrorLogs>
{
    public static WiredClearErrorLogs Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredClearErrorLogs ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredClearErrorLogs value, in PacketWriter p) { }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(WiredClearErrorLogs)} contains {p.Available} unexpected bytes.");
    }
}

// id 1953 — §_-42X§. Triggers WiredClickUserResponse (309), which echoes Index + OpenMenu.
public sealed record WiredClickUser(int Index) : IParserComposer<WiredClickUser>
{
    public static WiredClickUser Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredClickUser ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static WiredClickUser ParseUnity(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredClickUser value, in PacketWriter p) =>
        p.WriteInt(value.Index);

    private static void ComposeUnity(WiredClickUser value, in PacketWriter p) =>
        p.WriteInt(value.Index);
}

/// <summary>
/// Commits the wired furni's current state as its restore snapshot.
/// </summary>
/// <remarks>
/// Flash <c>ApplySnapshot</c> (2790) and Unity <c>UserDefinedRoomEventsApplySnapshot</c>. The Flash
/// menu sends it as <c>applySnapshot()</c> with the identifier of the wired furni whose dialog is
/// open. The two clients name it differently and <c>messages.ini</c> does not cross-map them, so
/// this is addressed per client rather than through one shared name.
/// </remarks>
public sealed record WiredApplySnapshot(Id FurniId) : IParserComposer<WiredApplySnapshot>
{
    public static WiredApplySnapshot Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredApplySnapshot ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static WiredApplySnapshot ParseUnity(in PacketReader p) => new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredApplySnapshot value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.FurniId));

    private static void ComposeUnity(WiredApplySnapshot value, in PacketWriter p) =>
        p.WriteLong(value.FurniId);
}
