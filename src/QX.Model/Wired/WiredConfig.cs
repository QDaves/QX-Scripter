using Qx.Messages;

namespace Qx.Model.Wired;

public enum UnityWiredContextLayout
{
    None,
    Tags,
    Full
}

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
        WiredWire.RequireUnityCount(AllowedFurniSources.Count, nameof(AllowedFurniSources));
        WiredWire.RequireUnityCount(AllowedUserSources.Count, nameof(AllowedUserSources));
        WiredWire.RequireUnityCount(DefaultFurniSources.Count, nameof(DefaultFurniSources));
        WiredWire.RequireUnityCount(DefaultUserSources.Count, nameof(DefaultUserSources));
        foreach (IReadOnlyList<int> sources in AllowedFurniSources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            WiredWire.RequireUnityCount(sources.Count, nameof(AllowedFurniSources));
        }
        foreach (IReadOnlyList<int> sources in AllowedUserSources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            WiredWire.RequireUnityCount(sources.Count, nameof(AllowedUserSources));
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
    public IReadOnlyList<int> UnityContextTags { get; set; } = [];
    public UnityWiredContextLayout UnityContextLayout { get; set; } = UnityWiredContextLayout.Full;
    public bool? UnityConditionHasSeparateInvert { get; set; }
    public bool HasUnityContext
    {
        get => UnityContextLayout is not UnityWiredContextLayout.None;
        set => UnityContextLayout = value ? UnityWiredContextLayout.Full : UnityWiredContextLayout.None;
    }

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

    protected void ReadUnity(in PacketReader p)
    {
        MessageWireProfile wire_profile = WiredWire.RequireUnityConfigurationProfile(in p);
        UnityWiredContextLayout expected_layout = ExpectedUnityContextLayout(wire_profile);
        UnityConditionHasSeparateInvert = wire_profile.WiredConditionHasSeparateInvert;
        UnityContextLayout = expected_layout;
        FurniLimit = p.ReadInt();
        StuffIds = p.ReadIdArray();
        StuffIds2 = [];
        StuffTypeId = p.ReadInt();
        Id = p.ReadId();
        StringParam = p.ReadString();
        IntParams = p.ReadIntArray();
        VariableIds = [];
        FurniSourceTypes = p.ReadIntArray();
        UserSourceTypes = p.ReadIntArray();
        Code = p.ReadInt();
        ReadUnityDefinitionSpecifics(p);
        AdvancedMode = p.ReadBool();
        InputSources = InputSourcesConf.Parse(p);
        AllowWallFurni = p.ReadBool();
        ReadUnityTypeSpecifics(p);
        ReadUnityContext(p, expected_layout);
    }

    protected void WriteUnity(in PacketWriter p)
    {
        MessageWireProfile wire_profile = WiredWire.RequireUnityConfigurationProfile(in p);
        UnityWiredContextLayout expected_layout = ExpectedUnityContextLayout(wire_profile);
        if (UnityContextLayout != expected_layout)
            throw new InvalidOperationException("The Unity wired context layout does not match the active client build.");
        if (StuffIds2.Count != 0 || VariableIds.Count != 0)
            throw new NotSupportedException("Unity wired configurations cannot represent StuffIds2 or VariableIds.");
        ValidateCommon(in p, true);
        ValidateUnityContext();
        ValidateUnitySpecifics(wire_profile);

        p.WriteInt(FurniLimit);
        p.WriteIdArray(StuffIds);
        p.WriteInt(StuffTypeId);
        p.WriteId(Id);
        p.WriteString(StringParam);
        p.WriteIntArray(IntParams);
        p.WriteIntArray(FurniSourceTypes);
        p.WriteIntArray(UserSourceTypes);
        p.WriteInt(Code);
        WriteUnityDefinitionSpecifics(p);
        p.WriteBool(AdvancedMode);
        InputSources.Compose(p);
        p.WriteBool(AllowWallFurni);
        WriteUnityTypeSpecifics(p);
        if (UnityContextLayout is UnityWiredContextLayout.Tags)
        {
            p.WriteIntArray(UnityContextTags);
            p.WriteIntArray(DefaultIntParams);
        }
        else if (UnityContextLayout is UnityWiredContextLayout.Full)
        {
            Context.Compose(p);
            p.WriteIntArray(DefaultIntParams);
        }
    }

    private void ValidateUnityContext()
    {
        if (UnityContextLayout is UnityWiredContextLayout.None)
        {
            if (UnityContextTags.Count != 0 || Context.Entries.Count != 0 || DefaultIntParams.Count != 0)
                throw new NotSupportedException("This Unity wired layout cannot represent context data.");
            return;
        }

        if (UnityContextLayout is UnityWiredContextLayout.Tags)
        {
            if (Context.Entries.Count != 0)
                throw new NotSupportedException("The Unity wired tag layout cannot represent full context entries.");
            return;
        }

        if (UnityContextTags.Count != 0 &&
            !UnityContextTags.SequenceEqual(Context.Entries.Select(entry => entry.Tag)))
        {
            throw new InvalidOperationException("The Unity wired context tags do not match the full context entries.");
        }
    }

    private void ReadUnityContext(in PacketReader p, UnityWiredContextLayout expected_layout)
    {
        if (expected_layout is UnityWiredContextLayout.None)
        {
            if (p.Available != 0)
                throw new InvalidOperationException("The active Unity build does not define a wired context tail.");
            UnityContextLayout = UnityWiredContextLayout.None;
            UnityContextTags = [];
            Context = WiredContext.Empty;
            DefaultIntParams = [];
            return;
        }

        if (expected_layout is UnityWiredContextLayout.Tags)
        {
            ReadUnityTagContext(p);
            return;
        }

        if (expected_layout is UnityWiredContextLayout.Full)
        {
            ReadUnityFullContext(p);
            return;
        }

        throw new NotSupportedException("The active Unity build has an unsupported wired context layout.");
    }

    private void ReadUnityFullContext(in PacketReader p)
    {
        Context = WiredContext.Parse(p);
        DefaultIntParams = p.ReadIntArray();
        if (p.Available != 0)
            throw new InvalidOperationException("The Unity wired context contains trailing data.");
        UnityContextTags = [.. Context.Entries.Select(entry => entry.Tag)];
        UnityContextLayout = UnityWiredContextLayout.Full;
    }

    private void ReadUnityTagContext(in PacketReader p)
    {
        UnityContextTags = p.ReadIntArray();
        DefaultIntParams = p.ReadIntArray();
        if (p.Available != 0)
            throw new InvalidOperationException("The Unity wired tag context contains trailing data.");
        Context = WiredContext.Empty;
        UnityContextLayout = UnityWiredContextLayout.Tags;
    }

    private static UnityWiredContextLayout ExpectedUnityContextLayout(MessageWireProfile profile) =>
        profile.WiredContextLayout switch
    {
        MessageWiredContextLayout.None => UnityWiredContextLayout.None,
        MessageWiredContextLayout.Tags => UnityWiredContextLayout.Tags,
        MessageWiredContextLayout.Full => UnityWiredContextLayout.Full,
        _ => throw new NotSupportedException("The active Unity session has no compatible wired context layout.")
    };

    private void ValidateFlash(in PacketWriter p)
    {
        ValidateCommon(in p, false);
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

    private void ValidateCommon(in PacketWriter p, bool unity)
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
        ArgumentNullException.ThrowIfNull(UnityContextTags);
        WiredWire.RequireString(StringParam, nameof(StringParam), in p);
        WiredWire.RequireUnityCount(StuffIds.Count, nameof(StuffIds));
        WiredWire.RequireUnityCount(StuffIds2.Count, nameof(StuffIds2));
        WiredWire.RequireUnityCount(IntParams.Count, nameof(IntParams));
        WiredWire.RequireUnityCount(VariableIds.Count, nameof(VariableIds));
        WiredWire.RequireUnityCount(FurniSourceTypes.Count, nameof(FurniSourceTypes));
        WiredWire.RequireUnityCount(UserSourceTypes.Count, nameof(UserSourceTypes));
        WiredWire.RequireUnityCount(DefaultIntParams.Count, nameof(DefaultIntParams));
        WiredWire.RequireUnityCount(UnityContextTags.Count, nameof(UnityContextTags));
        InputSources.Validate();
        Context.Validate(in p, unity);
    }

    protected virtual void ReadDefinitionSpecifics(in PacketReader p) { }
    protected virtual void WriteDefinitionSpecifics(in PacketWriter p) { }
    protected virtual void ReadTypeSpecifics(in PacketReader p) { }
    protected virtual void WriteTypeSpecifics(in PacketWriter p) { }
    protected virtual void ReadUnityDefinitionSpecifics(in PacketReader p) => ReadDefinitionSpecifics(in p);
    protected virtual void WriteUnityDefinitionSpecifics(in PacketWriter p) => WriteDefinitionSpecifics(in p);
    protected virtual void ReadUnityTypeSpecifics(in PacketReader p) => ReadTypeSpecifics(in p);
    protected virtual void WriteUnityTypeSpecifics(in PacketWriter p) => WriteTypeSpecifics(in p);
    protected virtual void ValidateFlashSpecifics() { }
    protected virtual void ValidateUnitySpecifics(MessageWireProfile? profile) { }
}

public sealed class WiredTriggerConfig : WiredConfig, IParserComposer<WiredTriggerConfig>
{
    public static WiredTriggerConfig Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredTriggerConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredTriggerConfig();
        value.ReadFlash(in p);
        return value;
    }

    private static WiredTriggerConfig ParseUnity(in PacketReader p)
    {
        var value = new WiredTriggerConfig();
        value.ReadUnity(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredTriggerConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);

    private static void ComposeUnity(WiredTriggerConfig value, in PacketWriter p) =>
        value.WriteUnity(in p);
}

public sealed class WiredActionConfig : WiredConfig, IParserComposer<WiredActionConfig>
{
    public int DelayInPulses { get; set; }
    protected override void ReadDefinitionSpecifics(in PacketReader p) => DelayInPulses = p.ReadInt();
    protected override void WriteDefinitionSpecifics(in PacketWriter p) => p.WriteInt(DelayInPulses);
    public static WiredActionConfig Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredActionConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredActionConfig();
        value.ReadFlash(in p);
        return value;
    }

    private static WiredActionConfig ParseUnity(in PacketReader p)
    {
        var value = new WiredActionConfig();
        value.ReadUnity(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredActionConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);

    private static void ComposeUnity(WiredActionConfig value, in PacketWriter p) =>
        value.WriteUnity(in p);
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

    protected override void ReadUnityDefinitionSpecifics(in PacketReader p)
    {
        QuantifierCode = p.ReadInt();
        DefinitionIsInvert = p.ReadBool();
    }

    protected override void WriteDefinitionSpecifics(in PacketWriter p)
    {
        p.WriteInt(QuantifierCode);
    }

    protected override void WriteUnityDefinitionSpecifics(in PacketWriter p)
    {
        p.WriteInt(QuantifierCode);
        p.WriteBool(DefinitionIsInvert);
    }

    protected override void ReadTypeSpecifics(in PacketReader p)
    {
        QuantifierType = p.ReadByte();
        IsInvert = p.ReadBool();
    }

    protected override void ReadUnityTypeSpecifics(in PacketReader p)
    {
        QuantifierType = p.ReadByte();
        bool has_separate_invert = UnityConditionHasSeparateInvert ??
            throw new InvalidOperationException("The Unity wired condition layout is unknown.");
        IsInvert = has_separate_invert ? p.ReadBool() : DefinitionIsInvert;
    }

    protected override void WriteTypeSpecifics(in PacketWriter p)
    {
        p.WriteByte(checked((byte)QuantifierType));
        p.WriteBool(IsInvert);
    }

    protected override void WriteUnityTypeSpecifics(in PacketWriter p)
    {
        p.WriteByte(checked((byte)QuantifierType));
        bool? has_separate_invert = p.Context?.WireProfile is { IsExact: true } profile
            ? profile.WiredConditionHasSeparateInvert
            : UnityConditionHasSeparateInvert;
        if (has_separate_invert is null)
            throw new InvalidOperationException("The Unity wired condition layout is unknown.");
        if (has_separate_invert is true)
            p.WriteBool(IsInvert);
    }
    protected override void ValidateFlashSpecifics() =>
        _ = checked((byte)QuantifierType);

    protected override void ValidateUnitySpecifics(MessageWireProfile? profile)
    {
        _ = checked((byte)QuantifierType);
        bool? exact = profile is { IsExact: true }
            ? profile.Value.WiredConditionHasSeparateInvert
            : null;
        if (exact is bool expected &&
            UnityConditionHasSeparateInvert is bool configured &&
            configured != expected)
        {
            throw new InvalidOperationException("The Unity wired condition layout does not match the active client build.");
        }

        bool? has_separate_invert = exact ?? UnityConditionHasSeparateInvert;
        if (has_separate_invert is null)
            throw new InvalidOperationException("The Unity wired condition layout is unknown.");
        if (has_separate_invert is false && IsInvert != DefinitionIsInvert)
            throw new NotSupportedException("This Unity wired condition layout cannot represent a separate invert value.");
    }
    public static WiredConditionConfig Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredConditionConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredConditionConfig();
        value.ReadFlash(in p);
        return value;
    }

    private static WiredConditionConfig ParseUnity(in PacketReader p)
    {
        var value = new WiredConditionConfig();
        value.ReadUnity(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredConditionConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);

    private static void ComposeUnity(WiredConditionConfig value, in PacketWriter p) =>
        value.WriteUnity(in p);
}

public sealed class WiredSelectorConfig : WiredConfig, IParserComposer<WiredSelectorConfig>
{
    public bool IsFilter { get; set; }
    public bool IsInvert { get; set; }
    protected override void ReadDefinitionSpecifics(in PacketReader p) { IsFilter = p.ReadBool(); IsInvert = p.ReadBool(); }
    protected override void WriteDefinitionSpecifics(in PacketWriter p) { p.WriteBool(IsFilter); p.WriteBool(IsInvert); }
    public static WiredSelectorConfig Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredSelectorConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredSelectorConfig();
        value.ReadFlash(in p);
        return value;
    }

    private static WiredSelectorConfig ParseUnity(in PacketReader p)
    {
        var value = new WiredSelectorConfig();
        value.ReadUnity(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredSelectorConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);

    private static void ComposeUnity(WiredSelectorConfig value, in PacketWriter p) =>
        value.WriteUnity(in p);
}

public sealed class WiredAddonConfig : WiredConfig, IParserComposer<WiredAddonConfig>
{
    public static WiredAddonConfig Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredAddonConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredAddonConfig();
        value.ReadFlash(in p);
        return value;
    }

    private static WiredAddonConfig ParseUnity(in PacketReader p)
    {
        var value = new WiredAddonConfig();
        value.ReadUnity(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredAddonConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);

    private static void ComposeUnity(WiredAddonConfig value, in PacketWriter p) =>
        value.WriteUnity(in p);
}

public sealed class WiredVariableConfig : WiredConfig, IParserComposer<WiredVariableConfig>
{
    public static WiredVariableConfig Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredVariableConfig ParseFlash(in PacketReader p)
    {
        var value = new WiredVariableConfig();
        value.ReadFlash(in p);
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredVariableConfig value, in PacketWriter p) =>
        value.WriteFlash(in p);
}

// §1 — the six incoming config-read messages (server -> client on opening a wired box).
public sealed record WiredFurniTrigger(WiredTriggerConfig Config) : IParserComposer<WiredFurniTrigger>
{
    public static WiredFurniTrigger Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredFurniTrigger ParseFlash(in PacketReader p) =>
        new(WiredTriggerConfig.Parse(in p));

    private static WiredFurniTrigger ParseUnity(in PacketReader p) =>
        new(WiredTriggerConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredFurniTrigger value, in PacketWriter p) =>
        value.Config.Compose(in p);

    private static void ComposeUnity(WiredFurniTrigger value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniAction(WiredActionConfig Config) : IParserComposer<WiredFurniAction>
{
    public static WiredFurniAction Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredFurniAction ParseFlash(in PacketReader p) =>
        new(WiredActionConfig.Parse(in p));

    private static WiredFurniAction ParseUnity(in PacketReader p) =>
        new(WiredActionConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredFurniAction value, in PacketWriter p) =>
        value.Config.Compose(in p);

    private static void ComposeUnity(WiredFurniAction value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniCondition(WiredConditionConfig Config) : IParserComposer<WiredFurniCondition>
{
    public static WiredFurniCondition Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredFurniCondition ParseFlash(in PacketReader p) =>
        new(WiredConditionConfig.Parse(in p));

    private static WiredFurniCondition ParseUnity(in PacketReader p) =>
        new(WiredConditionConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredFurniCondition value, in PacketWriter p) =>
        value.Config.Compose(in p);

    private static void ComposeUnity(WiredFurniCondition value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniSelector(WiredSelectorConfig Config) : IParserComposer<WiredFurniSelector>
{
    public static WiredFurniSelector Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredFurniSelector ParseFlash(in PacketReader p) =>
        new(WiredSelectorConfig.Parse(in p));

    private static WiredFurniSelector ParseUnity(in PacketReader p) =>
        new(WiredSelectorConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredFurniSelector value, in PacketWriter p) =>
        value.Config.Compose(in p);

    private static void ComposeUnity(WiredFurniSelector value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniAddon(WiredAddonConfig Config) : IParserComposer<WiredFurniAddon>
{
    public static WiredFurniAddon Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WiredFurniAddon ParseFlash(in PacketReader p) =>
        new(WiredAddonConfig.Parse(in p));

    private static WiredFurniAddon ParseUnity(in PacketReader p) =>
        new(WiredAddonConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WiredFurniAddon value, in PacketWriter p) =>
        value.Config.Compose(in p);

    private static void ComposeUnity(WiredFurniAddon value, in PacketWriter p) =>
        value.Config.Compose(in p);
}

public sealed record WiredFurniVariable(WiredVariableConfig Config) : IParserComposer<WiredFurniVariable>
{
    public static WiredFurniVariable Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredFurniVariable ParseFlash(in PacketReader p) =>
        new(WiredVariableConfig.Parse(in p));

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredFurniVariable value, in PacketWriter p) =>
        value.Config.Compose(in p);
}
