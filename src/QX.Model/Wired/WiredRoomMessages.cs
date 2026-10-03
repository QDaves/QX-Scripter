using Qx.Messages;

namespace Qx.Model.Wired;

// Wired room settings / permissions / environment / stats / logs / misc.
// Incoming parsers verified field-for-field against the July Flash decompile; outgoing


/// <summary>Represents the message that requests or announces the configuration of a wired furni item.</summary>
/// <remarks>Sent and received as the Flash <c>Open</c> message. Sent, it asks the hotel for the configuration, which arrives as <see cref="WiredFurniTrigger"/>, <see cref="WiredFurniAction"/>, <see cref="WiredFurniCondition"/>, <see cref="WiredFurniSelector"/>, <see cref="WiredFurniAddon"/> or <see cref="WiredFurniVariable"/>. Received, it names the furni item whose configuration opens.</remarks>
/// <param name="StuffId">The id of the wired furni item, written as a 32 bit integer.</param>
public sealed record WiredOpen(Id StuffId) : IParserComposer<WiredOpen>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredOpen Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredOpen ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredOpen value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.StuffId));
}

// id 3483 — §_-d1K§. Rights gate for the wired menu.
/// <summary>Received with the user's wired permissions in the room.</summary>
/// <remarks>Received as the Flash <c>WiredPermissions</c> message. The hotel sends it on entering a room and when the wired menu is opened.</remarks>
/// <param name="CanModify">Whether the user can modify wired in the room.</param>
/// <param name="CanRead">Whether the user can read wired in the room.</param>
public sealed record WiredPermissions(bool CanModify, bool CanRead) : IParserComposer<WiredPermissions>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredPermissions Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredPermissions ParseFlash(in PacketReader p) =>
        new(p.ReadBool(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredPermissions value, in PacketWriter p)
    {
        p.WriteBool(value.CanModify);
        p.WriteBool(value.CanRead);
    }
}

// id 2827 — §_-M2L§. The achievement list is guarded by bytesAvailable: on a short packet the
// count field is absent entirely, so null (list section missing) is distinct from an empty list.
/// <summary>Received with the wired environment of the room.</summary>
/// <remarks>Received as the Flash <c>WiredEnvironment</c> message. The achievement list is read only when bytes remain after the first field, so <see langword="null"/> means the list was not sent.</remarks>
/// <param name="HasClickUserWired">Whether the room has a click user wired.</param>
/// <param name="EnabledAchievements">The achievements wired can award in the room, or <see langword="null"/> when the list was not sent.</param>
public sealed record WiredEnvironment(bool HasClickUserWired, IReadOnlyList<string>? EnabledAchievements)
    : IParserComposer<WiredEnvironment>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredEnvironment Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredEnvironment ParseFlash(in PacketReader p) => Read(in p);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredEnvironment value, in PacketWriter p) =>
        Write(value, in p);

    private static void Write(WiredEnvironment value, in PacketWriter p)
    {
        if (value.EnabledAchievements is not null)
        {
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
/// <summary>Represents the wired budget statistics of a room.</summary>
/// <param name="ExecutionCost">The current wired execution cost, sent as a double.</param>
/// <param name="ExecutionCostCap">The execution cost cap, sent as a double.</param>
/// <param name="IsHeavy">Whether the room is marked as heavy.</param>
/// <param name="FloorItemCount">The number of floor items in the room.</param>
/// <param name="FloorItemCap">The maximum number of floor items.</param>
/// <param name="WallItemCount">The number of wall items in the room.</param>
/// <param name="WallItemCap">The maximum number of wall items.</param>
/// <param name="PermanentFurniVariables">The number of permanent furni variables in use.</param>
/// <param name="MaxPermanentFurniVariables">The maximum number of permanent furni variables.</param>
/// <param name="PermanentUserVariables">The number of permanent user variables in use.</param>
/// <param name="MaxPermanentUserVariables">The maximum number of permanent user variables.</param>
/// <param name="PermanentGlobalVariables">The number of permanent global variables in use.</param>
/// <param name="MaxPermanentGlobalVariables">The maximum number of permanent global variables.</param>
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
    /// <summary>Parses the statistics from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the statistics into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received with the wired budget statistics of the room.</summary>
/// <remarks>Received as the Flash <c>WiredRoomStats</c> message. The hotel sends it in answer to <see cref="WiredGetRoomStats"/>.</remarks>
/// <param name="RoomStats">The statistics.</param>
public sealed record WiredRoomStats(WiredRoomStatsData RoomStats) : IParserComposer<WiredRoomStats>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredRoomStats Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredRoomStats ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredRoomStatsData>());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredRoomStats value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.RoomStats);
        value.RoomStats.Compose(in p);
    }
}

// §_-Am§/WiredLogEntry — id and timestamp are longs (8 bytes); logLevel/logSource are single bytes.
/// <summary>Represents one entry of the wired room log.</summary>
/// <param name="Id">The id of the entry, sent as a 64 bit integer.</param>
/// <param name="LogLevel">The log level code, sent as a signed byte.</param>
/// <param name="LogSource">The log source code, sent as a signed byte.</param>
/// <param name="LogMessage">The log message.</param>
/// <param name="Timestamp">The time of the entry as a 64 bit value sent by the hotel.</param>
/// <param name="TimestampStr">The time of the entry as text formatted by the hotel.</param>
public sealed record WiredLogEntry(
    long Id,
    int LogLevel,
    int LogSource,
    string LogMessage,
    long Timestamp,
    string TimestampStr) : IParserComposer<WiredLogEntry>
{
    /// <summary>Parses the entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredLogEntry Parse(in PacketReader p) => new(
        p.ReadLong(),
        unchecked((sbyte)p.ReadByte()),
        unchecked((sbyte)p.ReadByte()),
        p.ReadString(),
        p.ReadLong(),
        p.ReadString());

    /// <summary>Composes the entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        byte log_level = unchecked((byte)checked((sbyte)LogLevel));
        byte log_source = unchecked((byte)checked((sbyte)LogSource));
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
/// <summary>Represents one page of the wired room log.</summary>
/// <remarks>Each filter is preceded by a flag on the wire, and a filter that was not sent is <see langword="null"/>.</remarks>
/// <param name="TotalEntries">The total number of log entries.</param>
/// <param name="CurrentPage">The number of the page.</param>
/// <param name="Amount">The page size the hotel reports.</param>
/// <param name="Elements">The entries on the page.</param>
/// <param name="LogLevelFilter">The log level filter the page was built with, sent as a signed byte, or <see langword="null"/> when none was sent.</param>
/// <param name="LogSourceFilter">The log source filter the page was built with, sent as a signed byte, or <see langword="null"/> when none was sent.</param>
/// <param name="Query">The text filter the page was built with, or <see langword="null"/> when none was sent.</param>
public sealed record WiredLogPage(
    int TotalEntries,
    int CurrentPage,
    int Amount,
    IReadOnlyList<WiredLogEntry> Elements,
    int? LogLevelFilter,
    int? LogSourceFilter,
    string? Query) : IParserComposer<WiredLogPage>
{
    /// <summary>Parses the page from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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
        int? logLevelFilter = p.ReadBool() ? unchecked((sbyte)p.ReadByte()) : null;
        int? logSourceFilter = p.ReadBool() ? unchecked((sbyte)p.ReadByte()) : null;
        string? query = p.ReadBool() ? p.ReadString() : null;
        return new WiredLogPage(totalEntries, currentPage, amount, elements, logLevelFilter, logSourceFilter, query);
    }

    /// <summary>Composes the page into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        byte? log_level_filter = LogLevelFilter is int log_level
            ? unchecked((byte)checked((sbyte)log_level))
            : null;
        byte? log_source_filter = LogSourceFilter is int log_source
            ? unchecked((byte)checked((sbyte)log_source))
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
/// <summary>Received with one page of the wired room log.</summary>
/// <remarks>Received as the Flash <c>WiredRoomLogs</c> message. The hotel sends it in answer to <see cref="WiredGetRoomLogs"/>.</remarks>
/// <param name="Page">The log page.</param>
public sealed record WiredRoomLogs(WiredLogPage Page) : IParserComposer<WiredRoomLogs>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredRoomLogs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredRoomLogs ParseFlash(in PacketReader p) =>
        new(p.Parse<WiredLogPage>());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
            _ = checked((sbyte)log_level_filter);
        if (page.LogSourceFilter is int log_source_filter)
            _ = checked((sbyte)log_source_filter);
        if (page.Query is not null)
            WiredWire.RequireString(page.Query, nameof(page.Query), in p);
        foreach (WiredLogEntry entry in page.Elements)
        {
            ArgumentNullException.ThrowIfNull(entry);
            _ = checked((sbyte)entry.LogLevel);
            _ = checked((sbyte)entry.LogSource);
            WiredWire.RequireString(entry.LogMessage, nameof(entry.LogMessage), in p);
            WiredWire.RequireString(entry.TimestampStr, nameof(entry.TimestampStr), in p);
        }
    }
}

// §_-q1V§/§_-Qa§ — one wired error stat row. msSinceLastOccurrence is a long (8 bytes).
/// <summary>Represents the statistics of one kind of wired error in the room.</summary>
/// <param name="ErrorId">The id of the error kind.</param>
/// <param name="ErrorName">The name of the error kind.</param>
/// <param name="Category">The category of the error kind.</param>
/// <param name="ThrowCount">The number of times the error happened.</param>
/// <param name="MsSinceLastOccurrence">The time since the error last happened, in milliseconds.</param>
public sealed record WiredError(
    int ErrorId,
    string ErrorName,
    string Category,
    int ThrowCount,
    long MsSinceLastOccurrence) : IParserComposer<WiredError>
{
    /// <summary>Parses the error entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredError Parse(in PacketReader p) => new(
        p.ReadInt(),
        p.ReadString(),
        p.ReadString(),
        p.ReadInt(),
        p.ReadLong());

    /// <summary>Composes the error entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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
/// <summary>Received with the wired error statistics of the room.</summary>
/// <remarks>Received as the Flash <c>WiredErrorLogs</c> message. The hotel sends it in answer to <see cref="WiredGetErrorLogs"/>, with the whole list at once.</remarks>
/// <param name="Errors">One entry per kind of error.</param>
public sealed record WiredErrorLogs(IReadOnlyList<WiredError> Errors) : IParserComposer<WiredErrorLogs>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredErrorLogs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredErrorLogs ParseFlash(in PacketReader p)
    {
        int n = p.ReadInt();
        WiredWire.RequireBoundedCount(n, p.Available, 20, nameof(Errors));
        var errors = new WiredError[n];
        for (int i = 0; i < n; i++)
            errors[i] = p.Parse<WiredError>();
        return new WiredErrorLogs(errors);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
/// <summary>Represents a substitution parameter of a wired validation error.</summary>
/// <param name="Key">The name of the parameter in the localized text.</param>
/// <param name="Value">The value to substitute.</param>
public sealed record WiredValidationParam(string Key, string Value) : IParserComposer<WiredValidationParam>
{
    /// <summary>Parses the parameter from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredValidationParam Parse(in PacketReader p) => new(p.ReadString(), p.ReadString());

    /// <summary>Composes the parameter into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteString(Key);
        p.WriteString(Value);
    }
}

// id 3201 — §_-3k§.
/// <summary>Received when the hotel rejects a wired configuration save.</summary>
/// <remarks>Received as the Flash <c>WiredValidationError</c> message. The error carries a localization key and its substitution parameters instead of a finished message.</remarks>
/// <param name="LocalizationKey">The localization key of the error text.</param>
/// <param name="Parameters">The substitution parameters of the error text.</param>
public sealed record WiredValidationError(string LocalizationKey, IReadOnlyList<WiredValidationParam> Parameters)
    : IParserComposer<WiredValidationError>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredValidationError Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredValidationError value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteString(value.LocalizationKey);
        p.WriteInt(value.Parameters.Count);
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
/// <summary>Received when the hotel accepts a wired configuration save.</summary>
/// <remarks>Received as the Flash <c>WiredSaveSuccess</c> message. The message carries no fields, so it does not say which configuration it acknowledges.</remarks>
public sealed record WiredSaveSuccess : IParserComposer<WiredSaveSuccess>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredSaveSuccess Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredSaveSuccess ParseFlash(in PacketReader p) => Read(in p);

    private static WiredSaveSuccess Read(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredSaveSuccess));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredSaveSuccess value, in PacketWriter p) { }
}

// id 1230 — §_-lC§. errorCode is a 2-byte short on the wire.
/// <summary>Received when a wired menu operation fails.</summary>
/// <remarks>Received as the Flash <c>WiredMenuError</c> message. It is the usual answer to a wired request made without sufficient rights.</remarks>
/// <param name="ErrorCode">The error code, sent as a 16 bit integer.</param>
public sealed record WiredMenuError(int ErrorCode) : IParserComposer<WiredMenuError>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredMenuError Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredMenuError ParseFlash(in PacketReader p) =>
        new(p.ReadShort());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredMenuError value, in PacketWriter p)
    {
        short error_code = checked((short)value.ErrorCode);
        p.WriteShort(error_code);
    }
}

// id 3931 — §_-kC§.
/// <summary>Received with the room's wired click options.</summary>
/// <remarks>Received as the Flash <c>WiredClickSettings</c> message.</remarks>
/// <param name="UserOption">The option code for clicking a user.</param>
/// <param name="FurniOption">The option code for clicking a furni item.</param>
public sealed record WiredClickSettings(int UserOption, int FurniOption) : IParserComposer<WiredClickSettings>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredClickSettings Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredClickSettings ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredClickSettings value, in PacketWriter p)
    {
        p.WriteInt(value.UserOption);
        p.WriteInt(value.FurniOption);
    }
}

/// <summary>Received with the room's wired settings.</summary>
/// <remarks>Received as the Flash <c>WiredRoomSettings</c> message. The hotel sends it in answer to <see cref="WiredGetRoomSettings"/> and <see cref="WiredSetRoomSettings"/>.</remarks>
/// <param name="ModifyPermissionMask">The mask of who may modify wired in the room.</param>
/// <param name="ReadPermissionMask">The mask of who may read wired in the room.</param>
/// <param name="Timezone">The wired timezone of the room.</param>
public sealed record WiredRoomSettings(int ModifyPermissionMask, int ReadPermissionMask, string Timezone)
    : IParserComposer<WiredRoomSettings>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredRoomSettings Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredRoomSettings ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredRoomSettings value, in PacketWriter p)
    {
        WiredWire.RequireString(value.Timezone, nameof(Timezone), in p);
        p.WriteInt(value.ModifyPermissionMask);
        p.WriteInt(value.ReadPermissionMask);
        p.WriteString(value.Timezone);
    }
}

/// <summary>Received in answer to <see cref="WiredClickUser"/>.</summary>
/// <remarks>Received as the Flash <c>WiredClickUserResponse</c> message.</remarks>
/// <param name="Index">The room index of the clicked user, echoed from the request.</param>
/// <param name="OpenMenu">Whether the wired user menu opens.</param>
public sealed record WiredClickUserResponse(int Index, bool OpenMenu)
    : IParserComposer<WiredClickUserResponse>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredClickUserResponse Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredClickUserResponse ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredClickUserResponse value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteBool(value.OpenMenu);
    }
}

/// <summary>Received with the outcome of a wired reward.</summary>
/// <remarks>Received as the Flash <c>WiredRewardResult</c> message.</remarks>
/// <param name="Reason">The reason code that explains why the reward was or was not given.</param>
public sealed record WiredRewardResult(int Reason) : IParserComposer<WiredRewardResult>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredRewardResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredRewardResult ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredRewardResult value, in PacketWriter p) =>
        p.WriteInt(value.Reason);
}

// Composers verified push-for-push against getMessageArray().
// Parse is the exact inverse of Compose so the round-trip tests can cover the wire layout.

// id 1862 — §_-z2§. Empty. Triggers WiredRoomSettings (491).
/// <summary>Requests the room's wired settings.</summary>
/// <remarks>Sent as the Flash <c>WiredGetRoomSettings</c> message, which carries no fields. The hotel answers with <see cref="WiredRoomSettings"/>.</remarks>
public sealed record WiredGetRoomSettings() : IParserComposer<WiredGetRoomSettings>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetRoomSettings Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetRoomSettings ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredGetRoomSettings));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetRoomSettings value, in PacketWriter p) { }
}

// id 2553 — §_-X1G§. Args map 1:1 onto the incoming WiredRoomSettings (491).
/// <summary>Sent when the user changes the room's wired settings.</summary>
/// <remarks>Sent as the Flash <c>WiredSetRoomSettings</c> message. All three values are sent together, and the hotel answers with <see cref="WiredRoomSettings"/>.</remarks>
/// <param name="ModifyPermissionMask">The mask of who may modify wired in the room.</param>
/// <param name="ReadPermissionMask">The mask of who may read wired in the room.</param>
/// <param name="Timezone">The wired timezone of the room.</param>
public sealed record WiredSetRoomSettings(int ModifyPermissionMask, int ReadPermissionMask, string Timezone)
    : IParserComposer<WiredSetRoomSettings>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredSetRoomSettings Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredSetRoomSettings ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
/// <summary>Sent when the user reloads the room's state or rolls it back from the wired menu.</summary>
/// <remarks>Sent as the Flash <c>WiredUpdateRoom</c> message. A reload discards nothing. A rollback discards every change since the last saved state, furni included. The hotel sends no acknowledgement.</remarks>
/// <param name="Rollback">Whether the room is rolled back to its last saved state instead of reloaded.</param>
public sealed record WiredUpdateRoom(bool Rollback) : IParserComposer<WiredUpdateRoom>
{
    /// <summary>Gets a request that reloads the room's state without discarding anything.</summary>
    public static WiredUpdateRoom Reload => new(false);

    /// <summary>Gets a request that rolls the room back to its last saved state, discarding every change since.</summary>
    public static WiredUpdateRoom RollBack => new(true);

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredUpdateRoom Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredUpdateRoom ParseFlash(in PacketReader p) => new(p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredUpdateRoom value, in PacketWriter p) =>
        p.WriteBool(value.Rollback);

}

// id 3124 — §_-h1H§. Six ctor args but SEVEN values pushed: a hardcoded int 0 sits between
// PlayTestMode and WiredWhisperDisabled. A byte-exact writer MUST emit it or every field after
// desyncs. Field names taken from WiredMenuController.sendPreferences().
/// <summary>Sent when the user changes the wired menu preferences.</summary>
/// <remarks>Sent as the Flash <c>WiredSetPreferences</c> message. A fixed integer 0 is written between <paramref name="PlayTestMode"/> and <paramref name="WiredWhisperDisabled"/> and skipped when parsing. The hotel sends no acknowledgement.</remarks>
/// <param name="WiredMenuButton">Whether the wired menu button is shown.</param>
/// <param name="WiredInspectButton">Whether the wired inspect button is shown.</param>
/// <param name="PlayTestMode">Whether play test mode is on.</param>
/// <param name="WiredWhisperDisabled">Whether wired whispers are suppressed.</param>
/// <param name="ShowAllNotifications">Whether all notifications are shown.</param>
/// <param name="UiStyle">The UI style of the wired menu.</param>
public sealed record WiredSetPreferences(
    bool WiredMenuButton,
    bool WiredInspectButton,
    bool PlayTestMode,
    bool WiredWhisperDisabled,
    bool ShowAllNotifications,
    string UiStyle) : IParserComposer<WiredSetPreferences>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredSetPreferences Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
/// <summary>Requests the room's wired budget statistics.</summary>
/// <remarks>Sent as the Flash <c>WiredGetRoomStats</c> message, which carries no fields. The hotel answers with <see cref="WiredRoomStats"/>.</remarks>
public sealed record WiredGetRoomStats() : IParserComposer<WiredGetRoomStats>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetRoomStats Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetRoomStats ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredGetRoomStats));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetRoomStats value, in PacketWriter p) { }
}

// id 706 — §_-P1c§. Triggers WiredRoomLogs (1910); the last three args echo into WiredLogPage's
// optional filter fields. Arg order confirmed from the fixed call (1, PAGE_SIZE, -1, -1, "").
/// <summary>Requests a page of the room's wired log.</summary>
/// <remarks>Sent as the Flash <c>WiredGetRoomLogs</c> message. The hotel answers with <see cref="WiredRoomLogs"/>. The game client sends page 1 with both filters at -1 and an empty query.</remarks>
/// <param name="Page">The one based page number.</param>
/// <param name="PageSize">The number of entries per page.</param>
/// <param name="LogLevelFilter">The log level to keep, or -1 for no level filter.</param>
/// <param name="LogSourceFilter">The log source to keep, or -1 for no source filter.</param>
/// <param name="Query">The text to search for, or an empty string for no text filter.</param>
public sealed record WiredGetRoomLogs(
    int Page,
    int PageSize,
    int LogLevelFilter,
    int LogSourceFilter,
    string Query) : IParserComposer<WiredGetRoomLogs>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetRoomLogs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetRoomLogs ParseFlash(in PacketReader p) => Read(in p);

    private static WiredGetRoomLogs Read(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
/// <summary>Requests the room's wired error statistics.</summary>
/// <remarks>Sent as the Flash <c>WiredGetErrorLogs</c> message, which carries no fields. The hotel answers with <see cref="WiredErrorLogs"/>.</remarks>
public sealed record WiredGetErrorLogs() : IParserComposer<WiredGetErrorLogs>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredGetErrorLogs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredGetErrorLogs ParseFlash(in PacketReader p)
    {
        WiredWire.RequireEmpty(in p, nameof(WiredGetErrorLogs));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredGetErrorLogs value, in PacketWriter p) { }
}

// id 2386 — §_-722§. Empty. No direct payload response.
/// <summary>Sent when the user clears the room's wired error statistics.</summary>
/// <remarks>Sent as the Flash <c>WiredClearErrorLogs</c> message, which carries no fields. The hotel sends no acknowledgement.</remarks>
public sealed record WiredClearErrorLogs() : IParserComposer<WiredClearErrorLogs>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredClearErrorLogs Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredClearErrorLogs ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredClearErrorLogs value, in PacketWriter p) { }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(WiredClearErrorLogs)} contains {p.Available} unexpected bytes.");
    }
}

// id 1953 — §_-42X§. Triggers WiredClickUserResponse (309), which echoes Index + OpenMenu.
/// <summary>Sent when the user clicks a user in the room, which click user wired reacts to.</summary>
/// <remarks>Sent as the Flash <c>WiredClickUser</c> message. The hotel answers with <see cref="WiredClickUserResponse"/>.</remarks>
/// <param name="Index">The room index of the clicked user.</param>
public sealed record WiredClickUser(int Index) : IParserComposer<WiredClickUser>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredClickUser Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredClickUser ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredClickUser value, in PacketWriter p) =>
        p.WriteInt(value.Index);
}

/// <summary>Sent when the user stores the current state of a wired furni item as its restore snapshot.</summary>
/// <remarks>Sent as the Flash <c>ApplySnapshot</c> message.</remarks>
/// <param name="FurniId">The id of the wired furni item, written as a 32 bit integer.</param>
public sealed record WiredApplySnapshot(Id FurniId) : IParserComposer<WiredApplySnapshot>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredApplySnapshot Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredApplySnapshot ParseFlash(in PacketReader p) => new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredApplySnapshot value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.FurniId));
}
