using Qx.Messages;

namespace Qx.Model.Wired;

// §4 — jagged allowed-source arrays + flat default-source arrays.
public sealed record InputSourcesConf(
    IReadOnlyList<IReadOnlyList<int>> AllowedFurniSources,
    IReadOnlyList<IReadOnlyList<int>> AllowedUserSources,
    IReadOnlyList<int> DefaultFurniSources,
    IReadOnlyList<int> DefaultUserSources) : IParserComposer<InputSourcesConf>
{
    public static InputSourcesConf Empty { get; } = new([], [], [], []);

    public int AmountFurniSelections => AllowedFurniSources.Count;
    public int AmountUserSelections => AllowedUserSources.Count;

    public static InputSourcesConf Parse(in PacketReader p)
    {
        int[][] furni = read_jagged(p);
        int[][] user = read_jagged(p);
        int[] defFurni = WiredIo.IntArray(p);
        int[] defUser = WiredIo.IntArray(p);
        return new InputSourcesConf(furni, user, defFurni, defUser);
    }

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
public abstract class WiredConfig
{
    public int FurniLimit { get; set; }
    public IReadOnlyList<Id> StuffIds { get; set; } = [];
    public IReadOnlyList<Id> StuffIds2 { get; set; } = [];
    public int StuffTypeId { get; set; }
    public Id Id { get; set; }
    public string StringParam { get; set; } = "";
    public IReadOnlyList<int> IntParams { get; set; } = [];
    public IReadOnlyList<string> VariableIds { get; set; } = [];
    public IReadOnlyList<int> FurniSourceTypes { get; set; } = [];
    public IReadOnlyList<int> UserSourceTypes { get; set; } = [];
    public int Code { get; set; }
    public bool AdvancedMode { get; set; }
    public InputSourcesConf InputSources { get; set; } = InputSourcesConf.Empty;
    public bool AllowWallFurni { get; set; }
    public WiredContext Context { get; set; } = WiredContext.Empty;
    public IReadOnlyList<int> DefaultIntParams { get; set; } = [];

    public string GetString(int index)
    {
        string[] parts = StringParam.Split('\t');
        return index >= 0 && index < parts.Length ? parts[index] : "";
    }

    public bool GetBoolean(int index) => index >= 0 && index < IntParams.Count && IntParams[index] == 1;
    public int GetInt(int index) => index >= 0 && index < IntParams.Count ? IntParams[index] : 0;

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

    protected virtual void ReadDefinitionSpecifics(in PacketReader p) { }
    protected virtual void WriteDefinitionSpecifics(in PacketWriter p) { }
    protected virtual void ReadTypeSpecifics(in PacketReader p) { }
    protected virtual void WriteTypeSpecifics(in PacketWriter p) { }
    protected virtual void ValidateFlashSpecifics() { }
}

public sealed class WiredTriggerConfig : WiredConfig, IParserComposer<WiredTriggerConfig>
{
    public static WiredTriggerConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredTriggerConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredTriggerConfig();
        value.ReadFlash(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredTriggerConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

public sealed class WiredActionConfig : WiredConfig, IParserComposer<WiredActionConfig>
{
    public int DelayInPulses { get; set; }
    protected override void ReadDefinitionSpecifics(in PacketReader p) => DelayInPulses = p.ReadInt();
    protected override void WriteDefinitionSpecifics(in PacketWriter p) => p.WriteInt(DelayInPulses);
    public static WiredActionConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredActionConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredActionConfig();
        value.ReadFlash(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredActionConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

public sealed class WiredConditionConfig : WiredConfig, IParserComposer<WiredConditionConfig>
{
    public int QuantifierCode { get; set; }
    public int QuantifierType { get; set; } // 1 byte on the wire
    public bool DefinitionIsInvert { get; set; }
    public bool IsInvert { get; set; }
    protected override void ReadDefinitionSpecifics(in PacketReader p)
    {
        QuantifierCode = p.ReadInt();
    }

    protected override void WriteDefinitionSpecifics(in PacketWriter p)
    {
        p.WriteInt(QuantifierCode);
    }

    protected override void ReadTypeSpecifics(in PacketReader p)
    {
        QuantifierType = p.ReadByte();
        IsInvert = p.ReadBool();
    }

    protected override void WriteTypeSpecifics(in PacketWriter p)
    {
        p.WriteByte(checked((byte)QuantifierType));
        p.WriteBool(IsInvert);
    }
    protected override void ValidateFlashSpecifics() =>
        _ = checked((byte)QuantifierType);
    public static WiredConditionConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredConditionConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredConditionConfig();
        value.ReadFlash(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredConditionConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

public sealed class WiredSelectorConfig : WiredConfig, IParserComposer<WiredSelectorConfig>
{
    public bool IsFilter { get; set; }
    public bool IsInvert { get; set; }
    protected override void ReadDefinitionSpecifics(in PacketReader p) { IsFilter = p.ReadBool(); IsInvert = p.ReadBool(); }
    protected override void WriteDefinitionSpecifics(in PacketWriter p) { p.WriteBool(IsFilter); p.WriteBool(IsInvert); }
    public static WiredSelectorConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredSelectorConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredSelectorConfig();
        value.ReadFlash(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredSelectorConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

public sealed class WiredAddonConfig : WiredConfig, IParserComposer<WiredAddonConfig>
{
    public static WiredAddonConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredAddonConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredAddonConfig();
        value.ReadFlash(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredAddonConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

public sealed class WiredVariableConfig : WiredConfig, IParserComposer<WiredVariableConfig>
{
    public static WiredVariableConfig Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredVariableConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredVariableConfig();
        value.ReadFlash(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredVariableConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

// §1 — the six incoming config-read messages (server -> client on opening a wired box).
public sealed record WiredFurniTrigger(WiredTriggerConfig Config) : IParserComposer<WiredFurniTrigger>
{
    public static WiredFurniTrigger Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniTrigger ParseFlash(in PacketReader p) =>
        new(WiredTriggerConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniTrigger value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniAction(WiredActionConfig Config) : IParserComposer<WiredFurniAction>
{
    public static WiredFurniAction Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniAction ParseFlash(in PacketReader p) =>
        new(WiredActionConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniAction value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniCondition(WiredConditionConfig Config) : IParserComposer<WiredFurniCondition>
{
    public static WiredFurniCondition Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniCondition ParseFlash(in PacketReader p) =>
        new(WiredConditionConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniCondition value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniSelector(WiredSelectorConfig Config) : IParserComposer<WiredFurniSelector>
{
    public static WiredFurniSelector Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniSelector ParseFlash(in PacketReader p) =>
        new(WiredSelectorConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniSelector value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniAddon(WiredAddonConfig Config) : IParserComposer<WiredFurniAddon>
{
    public static WiredFurniAddon Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniAddon ParseFlash(in PacketReader p) =>
        new(WiredAddonConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniAddon value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniVariable(WiredVariableConfig Config) : IParserComposer<WiredFurniVariable>
{
    public static WiredFurniVariable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredFurniVariable ParseFlash(in PacketReader p) =>
        new(WiredVariableConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniVariable value, in PacketWriter p) =>
        value.Config.Compose(in p);
}
