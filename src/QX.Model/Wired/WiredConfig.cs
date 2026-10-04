using Qx.Messages;

namespace Qx.Model.Wired;

// §4 — jagged allowed-source arrays + flat default-source arrays.
/// <summary>Represents the input sources a wired configuration allows and selects by default.</summary>
/// <param name="AllowedFurniSources">The allowed furni source types, one list per furni selection.</param>
/// <param name="AllowedUserSources">The allowed user source types, one list per user selection.</param>
/// <param name="DefaultFurniSources">The default furni source types.</param>
/// <param name="DefaultUserSources">The default user source types.</param>
public sealed record InputSourcesConf(
    IReadOnlyList<IReadOnlyList<int>> AllowedFurniSources,
    IReadOnlyList<IReadOnlyList<int>> AllowedUserSources,
    IReadOnlyList<int> DefaultFurniSources,
    IReadOnlyList<int> DefaultUserSources) : IParserComposer<InputSourcesConf>
{
    /// <summary>Gets an instance without any sources.</summary>
    public static InputSourcesConf Empty { get; } = new([], [], [], []);

    /// <summary>Gets the number of furni selections, which is the number of lists in <see cref="AllowedFurniSources"/>.</summary>
    public int AmountFurniSelections => AllowedFurniSources.Count;
    /// <summary>Gets the number of user selections, which is the number of lists in <see cref="AllowedUserSources"/>.</summary>
    public int AmountUserSelections => AllowedUserSources.Count;

    /// <summary>Parses the input sources from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static InputSourcesConf Parse(in PacketReader p)
    {
        int[][] furni = read_jagged(p);
        int[][] user = read_jagged(p);
        int[] defFurni = WiredIo.IntArray(p);
        int[] defUser = WiredIo.IntArray(p);
        return new InputSourcesConf(furni, user, defFurni, defUser);
    }

    /// <summary>Composes the input sources into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        Validate();
        write_jagged(p, AllowedFurniSources);
        write_jagged(p, AllowedUserSources);
        WiredIo.WriteIntArray(p, DefaultFurniSources);
        WiredIo.WriteIntArray(p, DefaultUserSources);
    }

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(AllowedFurniSources);
        ArgumentNullException.ThrowIfNull(AllowedUserSources);
        ArgumentNullException.ThrowIfNull(DefaultFurniSources);
        ArgumentNullException.ThrowIfNull(DefaultUserSources);
        foreach (IReadOnlyList<int> sources in AllowedFurniSources)
        {
            ArgumentNullException.ThrowIfNull(sources);
        }
        foreach (IReadOnlyList<int> sources in AllowedUserSources)
        {
            ArgumentNullException.ThrowIfNull(sources);
        }
    }

    private static int[][] read_jagged(in PacketReader p)
    {
        int outer = p.ReadLength();
        var a = new int[outer][];
        for (int i = 0; i < outer; i++)
            a[i] = p.ReadIntArray();
        return a;
    }

    private static void write_jagged(in PacketWriter p, IReadOnlyList<IReadOnlyList<int>> a)
    {
        p.WriteLength((Length)a.Count);
        foreach (IReadOnlyList<int> inner in a)
            p.WriteIntArray(inner);
    }
}

// §2/§3 — shared wired-config base. Subclass extras are injected mid-stream via the two hooks.
/// <summary>Represents the configuration of a wired furni item, with the fields every kind of wired shares.</summary>
/// <remarks>Derived configurations add their own fields after <see cref="WiredConfig.Code"/> and after <see cref="WiredConfig.AllowWallFurni"/> on the wire.</remarks>
public abstract class WiredConfig
{
    /// <summary>Gets or sets the maximum number of furni that can be selected.</summary>
    public int FurniLimit { get; set; }
    /// <summary>Gets or sets the ids of the selected furni.</summary>
    public IReadOnlyList<Id> StuffIds { get; set; } = [];
    /// <summary>Gets or sets the ids of the furni in the second selection.</summary>
    public IReadOnlyList<Id> StuffIds2 { get; set; } = [];
    /// <summary>Gets or sets the furni type id of the wired furni item.</summary>
    public int StuffTypeId { get; set; }
    /// <summary>Gets or sets the id of the wired furni item in the room.</summary>
    public Id Id { get; set; }
    /// <summary>Gets or sets the string parameter, with multiple values separated by tabs.</summary>
    public string StringParam { get; set; } = "";
    /// <summary>Gets or sets the integer parameters.</summary>
    public IReadOnlyList<int> IntParams { get; set; } = [];
    /// <summary>Gets or sets the ids of the variables the configuration references.</summary>
    public IReadOnlyList<string> VariableIds { get; set; } = [];
    /// <summary>Gets or sets the selected furni source types.</summary>
    public IReadOnlyList<int> FurniSourceTypes { get; set; } = [];
    /// <summary>Gets or sets the selected user source types.</summary>
    public IReadOnlyList<int> UserSourceTypes { get; set; } = [];
    /// <summary>Gets or sets the code that identifies the wired type.</summary>
    public int Code { get; set; }
    /// <summary>Gets or sets whether the advanced mode is enabled.</summary>
    public bool AdvancedMode { get; set; }
    /// <summary>Gets or sets the allowed and default input sources.</summary>
    public InputSourcesConf InputSources { get; set; } = InputSourcesConf.Empty;
    /// <summary>Gets or sets whether wall items can be selected.</summary>
    public bool AllowWallFurni { get; set; }
    /// <summary>Gets or sets the variable context the hotel sent with the configuration.</summary>
    public WiredContext Context { get; set; } = WiredContext.Empty;
    /// <summary>Gets or sets the default integer parameters.</summary>
    public IReadOnlyList<int> DefaultIntParams { get; set; } = [];

    /// <summary>Gets one of the tab separated values of <see cref="StringParam"/>.</summary>
    /// <param name="index">The zero based index of the value.</param>
    /// <returns>The value, or an empty string when <paramref name="index"/> is out of range.</returns>
    public string GetString(int index)
    {
        string[] parts = StringParam.Split('\t');
        return index >= 0 && index < parts.Length ? parts[index] : "";
    }

    /// <summary>Gets whether the integer parameter at an index is 1.</summary>
    /// <param name="index">The zero based index in <see cref="IntParams"/>.</param>
    /// <returns><see langword="true"/> when the parameter is 1, or <see langword="false"/> when it is another value or <paramref name="index"/> is out of range.</returns>
    public bool GetBoolean(int index) => index >= 0 && index < IntParams.Count && IntParams[index] == 1;
    /// <summary>Gets the integer parameter at an index.</summary>
    /// <param name="index">The zero based index in <see cref="IntParams"/>.</param>
    /// <returns>The parameter, or 0 when <paramref name="index"/> is out of range.</returns>
    public int GetInt(int index) => index >= 0 && index < IntParams.Count ? IntParams[index] : 0;

    /// <summary>Reads every configuration field from a Flash packet into the properties.</summary>
    /// <param name="p">The packet reader.</param>
    protected void ReadFlash(in PacketReader p)
    {
        FurniLimit = p.ReadInt();
        StuffIds = p.ReadIdArray();
        StuffIds2 = p.ReadIdArray();
        StuffTypeId = p.ReadInt();
        Id = p.ReadId();
        StringParam = p.ReadString();
        IntParams = WiredIo.IntArray(p);
        VariableIds = WiredIo.StringArray(p);
        FurniSourceTypes = WiredIo.IntArray(p);
        UserSourceTypes = WiredIo.IntArray(p);
        Code = p.ReadInt();
        ReadDefinitionSpecifics(p);
        AdvancedMode = p.ReadBool();
        InputSources = InputSourcesConf.Parse(p);
        AllowWallFurni = p.ReadBool();
        ReadTypeSpecifics(p);
        Context = WiredContext.Parse(p);
        DefaultIntParams = WiredIo.IntArray(p);
    }

    /// <summary>Validates the properties and writes every configuration field to a Flash packet.</summary>
    /// <param name="p">The packet writer.</param>
    protected void WriteFlash(in PacketWriter p)
    {
        ValidateFlash(in p);
        p.WriteInt(FurniLimit);
        p.WriteIdArray(StuffIds);
        p.WriteIdArray(StuffIds2);
        p.WriteInt(StuffTypeId);
        p.WriteId(Id);
        p.WriteString(StringParam);
        WiredIo.WriteIntArray(p, IntParams);
        WiredIo.WriteStringArray(p, VariableIds);
        WiredIo.WriteIntArray(p, FurniSourceTypes);
        WiredIo.WriteIntArray(p, UserSourceTypes);
        p.WriteInt(Code);
        WriteDefinitionSpecifics(p);
        p.WriteBool(AdvancedMode);
        InputSources.Compose(p);
        p.WriteBool(AllowWallFurni);
        WriteTypeSpecifics(p);
        Context.Compose(p);
        WiredIo.WriteIntArray(p, DefaultIntParams);
    }

    private void ValidateFlash(in PacketWriter p)
    {
        ValidateCommon(in p);
        ValidateFlashSpecifics();
        foreach (Id id in StuffIds)
            _ = WiredWire.FlashId(id);
        foreach (Id id in StuffIds2)
            _ = WiredWire.FlashId(id);
        _ = WiredWire.FlashId(Id);
        WiredWire.RequireString(StringParam, nameof(StringParam), in p);
        foreach (string variable_id in VariableIds)
            WiredWire.RequireString(variable_id, nameof(VariableIds), in p);
    }

    private void ValidateCommon(in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(StuffIds);
        ArgumentNullException.ThrowIfNull(StuffIds2);
        ArgumentNullException.ThrowIfNull(IntParams);
        ArgumentNullException.ThrowIfNull(VariableIds);
        ArgumentNullException.ThrowIfNull(FurniSourceTypes);
        ArgumentNullException.ThrowIfNull(UserSourceTypes);
        ArgumentNullException.ThrowIfNull(InputSources);
        ArgumentNullException.ThrowIfNull(Context);
        ArgumentNullException.ThrowIfNull(DefaultIntParams);
        WiredWire.RequireString(StringParam, nameof(StringParam), in p);
        InputSources.Validate();
        Context.Validate(in p);
    }

    /// <summary>Reads the fields a derived configuration adds after <see cref="Code"/>.</summary>
    /// <param name="p">The packet reader.</param>
    protected virtual void ReadDefinitionSpecifics(in PacketReader p) { }
    /// <summary>Writes the fields a derived configuration adds after <see cref="Code"/>.</summary>
    /// <param name="p">The packet writer.</param>
    protected virtual void WriteDefinitionSpecifics(in PacketWriter p) { }
    /// <summary>Reads the fields a derived configuration adds after <see cref="AllowWallFurni"/>.</summary>
    /// <param name="p">The packet reader.</param>
    protected virtual void ReadTypeSpecifics(in PacketReader p) { }
    /// <summary>Writes the fields a derived configuration adds after <see cref="AllowWallFurni"/>.</summary>
    /// <param name="p">The packet writer.</param>
    protected virtual void WriteTypeSpecifics(in PacketWriter p) { }
    /// <summary>Validates the fields a derived configuration adds before they are written.</summary>
    protected virtual void ValidateFlashSpecifics() { }
}

/// <summary>Represents the configuration of a wired trigger.</summary>
public sealed class WiredTriggerConfig : WiredConfig, IParserComposer<WiredTriggerConfig>
{
    /// <summary>Parses the configuration from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredTriggerConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTriggerConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredTriggerConfig();
        value.ReadFlash(in p);
        return value;
    }

    /// <summary>Composes the configuration into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTriggerConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

/// <summary>Represents the configuration of a wired action, also called an effect.</summary>
public sealed class WiredActionConfig : WiredConfig, IParserComposer<WiredActionConfig>
{
    /// <summary>Gets or sets the delay of the action in pulses.</summary>
    public int DelayInPulses { get; set; }
    /// <summary>Reads <see cref="DelayInPulses"/>.</summary>
    /// <param name="p">The packet reader.</param>
    protected override void ReadDefinitionSpecifics(in PacketReader p) => DelayInPulses = p.ReadInt();
    /// <summary>Writes <see cref="DelayInPulses"/>.</summary>
    /// <param name="p">The packet writer.</param>
    protected override void WriteDefinitionSpecifics(in PacketWriter p) => p.WriteInt(DelayInPulses);
    /// <summary>Parses the configuration from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredActionConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredActionConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredActionConfig();
        value.ReadFlash(in p);
        return value;
    }

    /// <summary>Composes the configuration into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredActionConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

/// <summary>Represents the configuration of a wired condition.</summary>
public sealed class WiredConditionConfig : WiredConfig, IParserComposer<WiredConditionConfig>
{
    /// <summary>Gets or sets the quantifier code.</summary>
    public int QuantifierCode { get; set; }
    /// <summary>Gets or sets the quantifier type, which is written as a signed byte.</summary>
    public int QuantifierType { get; set; } // 1 byte on the wire
    /// <summary>Gets or sets whether the condition definition is inverted.</summary>
    /// <remarks>The value is not read from or written to the packet, so it is <see langword="false"/> after parsing.</remarks>
    public bool DefinitionIsInvert { get; set; }
    /// <summary>Gets or sets whether the condition is inverted.</summary>
    public bool IsInvert { get; set; }
    /// <summary>Reads <see cref="QuantifierCode"/>.</summary>
    /// <param name="p">The packet reader.</param>
    protected override void ReadDefinitionSpecifics(in PacketReader p)
    {
        QuantifierCode = p.ReadInt();
    }

    /// <summary>Writes <see cref="QuantifierCode"/>.</summary>
    /// <param name="p">The packet writer.</param>
    protected override void WriteDefinitionSpecifics(in PacketWriter p)
    {
        p.WriteInt(QuantifierCode);
    }

    /// <summary>Reads <see cref="QuantifierType"/> as a byte, then <see cref="IsInvert"/>.</summary>
    /// <param name="p">The packet reader.</param>
    protected override void ReadTypeSpecifics(in PacketReader p)
    {
        QuantifierType = unchecked((sbyte)p.ReadByte());
        IsInvert = p.ReadBool();
    }

    /// <summary>Writes <see cref="QuantifierType"/> as a byte, then <see cref="IsInvert"/>.</summary>
    /// <param name="p">The packet writer.</param>
    protected override void WriteTypeSpecifics(in PacketWriter p)
    {
        p.WriteByte(unchecked((byte)checked((sbyte)QuantifierType)));
        p.WriteBool(IsInvert);
    }
    /// <summary>Checks that <see cref="QuantifierType"/> fits in a byte.</summary>
    /// <exception cref="OverflowException">Thrown when <see cref="QuantifierType"/> is outside -128 to 127.</exception>
    protected override void ValidateFlashSpecifics() =>
        _ = checked((sbyte)QuantifierType);
    /// <summary>Parses the configuration from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredConditionConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredConditionConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredConditionConfig();
        value.ReadFlash(in p);
        return value;
    }

    /// <summary>Composes the configuration into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredConditionConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

/// <summary>Represents the configuration of a wired selector.</summary>
public sealed class WiredSelectorConfig : WiredConfig, IParserComposer<WiredSelectorConfig>
{
    /// <summary>Gets or sets whether the selector is a filter.</summary>
    public bool IsFilter { get; set; }
    /// <summary>Gets or sets whether the selector is inverted.</summary>
    public bool IsInvert { get; set; }
    /// <summary>Reads <see cref="IsFilter"/>, then <see cref="IsInvert"/>.</summary>
    /// <param name="p">The packet reader.</param>
    protected override void ReadDefinitionSpecifics(in PacketReader p) { IsFilter = p.ReadBool(); IsInvert = p.ReadBool(); }
    /// <summary>Writes <see cref="IsFilter"/>, then <see cref="IsInvert"/>.</summary>
    /// <param name="p">The packet writer.</param>
    protected override void WriteDefinitionSpecifics(in PacketWriter p) { p.WriteBool(IsFilter); p.WriteBool(IsInvert); }
    /// <summary>Parses the configuration from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredSelectorConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredSelectorConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredSelectorConfig();
        value.ReadFlash(in p);
        return value;
    }

    /// <summary>Composes the configuration into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredSelectorConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

/// <summary>Represents the configuration of a wired add-on.</summary>
public sealed class WiredAddonConfig : WiredConfig, IParserComposer<WiredAddonConfig>
{
    /// <summary>Parses the configuration from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredAddonConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredAddonConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredAddonConfig();
        value.ReadFlash(in p);
        return value;
    }

    /// <summary>Composes the configuration into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredAddonConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

/// <summary>Represents the configuration of a wired variable furni.</summary>
public sealed class WiredVariableConfig : WiredConfig, IParserComposer<WiredVariableConfig>
{
    /// <summary>Parses the configuration from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredVariableConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredVariableConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredVariableConfig();
        value.ReadFlash(in p);
        return value;
    }

    /// <summary>Composes the configuration into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredVariableConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

// §1 — the six incoming config-read messages (server -> client on opening a wired box).
/// <summary>Received with the configuration of a wired trigger when its settings are opened.</summary>
/// <remarks>Received as the Flash <c>WiredFurniTrigger</c> message.</remarks>
/// <param name="Config">The trigger configuration.</param>
public sealed record WiredFurniTrigger(WiredTriggerConfig Config) : IParserComposer<WiredFurniTrigger>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredFurniTrigger Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniTrigger ParseFlash(in PacketReader p) =>
        new(WiredTriggerConfig.Parse(in p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniTrigger value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

/// <summary>Received with the configuration of a wired action when its settings are opened.</summary>
/// <remarks>Received as the Flash <c>WiredFurniAction</c> message.</remarks>
/// <param name="Config">The action configuration.</param>
public sealed record WiredFurniAction(WiredActionConfig Config) : IParserComposer<WiredFurniAction>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredFurniAction Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniAction ParseFlash(in PacketReader p) =>
        new(WiredActionConfig.Parse(in p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniAction value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

/// <summary>Received with the configuration of a wired condition when its settings are opened.</summary>
/// <remarks>Received as the Flash <c>WiredFurniCondition</c> message.</remarks>
/// <param name="Config">The condition configuration.</param>
public sealed record WiredFurniCondition(WiredConditionConfig Config) : IParserComposer<WiredFurniCondition>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredFurniCondition Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniCondition ParseFlash(in PacketReader p) =>
        new(WiredConditionConfig.Parse(in p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniCondition value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

/// <summary>Received with the configuration of a wired selector when its settings are opened.</summary>
/// <remarks>Received as the Flash <c>WiredFurniSelector</c> message.</remarks>
/// <param name="Config">The selector configuration.</param>
public sealed record WiredFurniSelector(WiredSelectorConfig Config) : IParserComposer<WiredFurniSelector>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredFurniSelector Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniSelector ParseFlash(in PacketReader p) =>
        new(WiredSelectorConfig.Parse(in p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniSelector value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

/// <summary>Received with the configuration of a wired add-on when its settings are opened.</summary>
/// <remarks>Received as the Flash <c>WiredFurniAddon</c> message.</remarks>
/// <param name="Config">The add-on configuration.</param>
public sealed record WiredFurniAddon(WiredAddonConfig Config) : IParserComposer<WiredFurniAddon>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredFurniAddon Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniAddon ParseFlash(in PacketReader p) =>
        new(WiredAddonConfig.Parse(in p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniAddon value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

/// <summary>Received with the configuration of a wired variable when its settings are opened.</summary>
/// <remarks>Received as the Flash <c>WiredFurniVariable</c> message.</remarks>
/// <param name="Config">The variable configuration.</param>
public sealed record WiredFurniVariable(WiredVariableConfig Config) : IParserComposer<WiredFurniVariable>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredFurniVariable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniVariable ParseFlash(in PacketReader p) =>
        new(WiredVariableConfig.Parse(in p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniVariable value, in PacketWriter p) =>
        value.Config.Compose(in p);
}
