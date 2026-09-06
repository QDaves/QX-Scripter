using Qx.Messages;

namespace Qx.Model.Wired;

public abstract record WiredConfigWrite : IComposer
{
    public Id FurniId { get; set; }
    public IReadOnlyList<int> IntParams { get; set; } = [];
    public string StringParam { get; set; } = "";
    public IReadOnlyList<Id> StuffIds { get; set; } = [];
    public IReadOnlyList<Id> StuffIds2 { get; set; } = [];
    public IReadOnlyList<int> FurniSourceTypes { get; set; } = [];
    public IReadOnlyList<int> UserSourceTypes { get; set; } = [];
    public IReadOnlyList<string> VariableIds { get; set; } = [];

    public abstract void Compose(in PacketWriter p);

    protected void ReadFlash(in PacketReader p)
    {
        FurniId = p.ReadInt();
        IntParams = p.ReadIntArray();
        StringParam = p.ReadString();
        StuffIds = p.ReadIdArray();
        ReadExtra(in p);
        FurniSourceTypes = p.ReadIntArray();
        UserSourceTypes = p.ReadIntArray();
        VariableIds = p.ReadStringArray();
        StuffIds2 = p.ReadIdArray();
    }

    protected void ReadUnity(in PacketReader p)
    {
        FurniId = p.ReadLong();
        IntParams = p.ReadIntArray();
        StringParam = p.ReadString();
        StuffIds = p.ReadIdArray();
        ReadExtra(in p);
        FurniSourceTypes = p.ReadIntArray();
        UserSourceTypes = p.ReadIntArray();
        VariableIds = [];
        StuffIds2 = [];
    }

    protected void ComposeFlash(in PacketWriter p)
    {
        ValidateFlash(in p);
        p.WriteInt(WiredWire.FlashId(FurniId));
        p.WriteIntArray(IntParams);
        p.WriteString(StringParam);
        p.WriteIdArray(StuffIds);
        WriteExtra(in p);
        p.WriteIntArray(FurniSourceTypes);
        p.WriteIntArray(UserSourceTypes);
        p.WriteStringArray(VariableIds);
        p.WriteIdArray(StuffIds2);
    }

    protected void ComposeUnity(in PacketWriter p)
    {
        ValidateUnity(in p);
        p.WriteLong(FurniId);
        p.WriteIntArray(IntParams);
        p.WriteString(StringParam);
        p.WriteIdArray(StuffIds);
        WriteExtra(in p);
        p.WriteIntArray(FurniSourceTypes);
        p.WriteIntArray(UserSourceTypes);
    }

    protected virtual void ReadExtra(in PacketReader p) { }

    protected virtual void WriteExtra(in PacketWriter p) { }

    private void ValidateFlash(in PacketWriter p)
    {
        ValidateCommon(in p);
        _ = WiredWire.FlashId(FurniId);
        foreach (Id id in StuffIds)
            _ = WiredWire.FlashId(id);
        foreach (Id id in StuffIds2)
            _ = WiredWire.FlashId(id);
        foreach (string variable_id in VariableIds)
            WiredWire.RequireString(variable_id, nameof(VariableIds), in p);
    }

    private void ValidateUnity(in PacketWriter p)
    {
        ValidateCommon(in p);
        if (VariableIds.Count != 0 || StuffIds2.Count != 0)
            throw new NotSupportedException("Unity wired configuration saves cannot represent VariableIds or StuffIds2.");
    }

    private void ValidateCommon(in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(IntParams);
        ArgumentNullException.ThrowIfNull(StringParam);
        ArgumentNullException.ThrowIfNull(StuffIds);
        ArgumentNullException.ThrowIfNull(StuffIds2);
        ArgumentNullException.ThrowIfNull(FurniSourceTypes);
        ArgumentNullException.ThrowIfNull(UserSourceTypes);
        ArgumentNullException.ThrowIfNull(VariableIds);
        WiredWire.RequireString(StringParam, nameof(StringParam), in p);
        WiredWire.RequireUnityCount(IntParams.Count, nameof(IntParams));
        WiredWire.RequireUnityCount(StuffIds.Count, nameof(StuffIds));
        WiredWire.RequireUnityCount(StuffIds2.Count, nameof(StuffIds2));
        WiredWire.RequireUnityCount(FurniSourceTypes.Count, nameof(FurniSourceTypes));
        WiredWire.RequireUnityCount(UserSourceTypes.Count, nameof(UserSourceTypes));
        WiredWire.RequireUnityCount(VariableIds.Count, nameof(VariableIds));
    }
}

public sealed record UpdateTrigger : WiredConfigWrite, IParserComposer<UpdateTrigger>
{
    public static UpdateTrigger Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UpdateTrigger ParseFlash(in PacketReader p)
    {
        var value = new UpdateTrigger();
        value.ReadFlash(in p);
        return value;
    }

    private static UpdateTrigger ParseUnity(in PacketReader p)
    {
        var value = new UpdateTrigger();
        value.ReadUnity(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UpdateTrigger value, in PacketWriter p) =>
        value.ComposeFlash(in p);

    private static void ComposeUnity(UpdateTrigger value, in PacketWriter p) =>
        value.ComposeUnity(in p);
}

public sealed record UpdateAction : WiredConfigWrite, IParserComposer<UpdateAction>
{
    public int Delay { get; set; }

    public static UpdateAction Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UpdateAction ParseFlash(in PacketReader p)
    {
        var value = new UpdateAction();
        value.ReadFlash(in p);
        return value;
    }

    private static UpdateAction ParseUnity(in PacketReader p)
    {
        var value = new UpdateAction();
        value.ReadUnity(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    protected override void ReadExtra(in PacketReader p) => Delay = p.ReadInt();

    protected override void WriteExtra(in PacketWriter p) => p.WriteInt(Delay);

    private static void ComposeFlash(UpdateAction value, in PacketWriter p) =>
        value.ComposeFlash(in p);

    private static void ComposeUnity(UpdateAction value, in PacketWriter p) =>
        value.ComposeUnity(in p);
}

public sealed record UpdateCondition : WiredConfigWrite, IParserComposer<UpdateCondition>
{
    public int Quantifier { get; set; }

    public static UpdateCondition Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UpdateCondition ParseFlash(in PacketReader p)
    {
        var value = new UpdateCondition();
        value.ReadFlash(in p);
        return value;
    }

    private static UpdateCondition ParseUnity(in PacketReader p)
    {
        var value = new UpdateCondition();
        value.ReadUnity(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    protected override void ReadExtra(in PacketReader p) => Quantifier = p.ReadInt();

    protected override void WriteExtra(in PacketWriter p) => p.WriteInt(Quantifier);

    private static void ComposeFlash(UpdateCondition value, in PacketWriter p) =>
        value.ComposeFlash(in p);

    private static void ComposeUnity(UpdateCondition value, in PacketWriter p) =>
        value.ComposeUnity(in p);
}

public sealed record UpdateAddon : WiredConfigWrite, IParserComposer<UpdateAddon>
{
    public static UpdateAddon Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UpdateAddon ParseFlash(in PacketReader p)
    {
        var value = new UpdateAddon();
        value.ReadFlash(in p);
        return value;
    }

    private static UpdateAddon ParseUnity(in PacketReader p)
    {
        var value = new UpdateAddon();
        value.ReadUnity(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UpdateAddon value, in PacketWriter p) =>
        value.ComposeFlash(in p);

    private static void ComposeUnity(UpdateAddon value, in PacketWriter p) =>
        value.ComposeUnity(in p);
}

public sealed record UpdateSelector : WiredConfigWrite, IParserComposer<UpdateSelector>
{
    public bool IsFilter { get; set; }
    public bool IsInvert { get; set; }

    public static UpdateSelector Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UpdateSelector ParseFlash(in PacketReader p)
    {
        var value = new UpdateSelector();
        value.ReadFlash(in p);
        return value;
    }

    private static UpdateSelector ParseUnity(in PacketReader p)
    {
        var value = new UpdateSelector();
        value.ReadUnity(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    protected override void ReadExtra(in PacketReader p)
    {
        IsFilter = p.ReadBool();
        IsInvert = p.ReadBool();
    }

    protected override void WriteExtra(in PacketWriter p)
    {
        p.WriteBool(IsFilter);
        p.WriteBool(IsInvert);
    }

    private static void ComposeFlash(UpdateSelector value, in PacketWriter p) =>
        value.ComposeFlash(in p);

    private static void ComposeUnity(UpdateSelector value, in PacketWriter p) =>
        value.ComposeUnity(in p);
}

public sealed record UpdateVariable : WiredConfigWrite, IParserComposer<UpdateVariable>
{
    public static UpdateVariable Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static UpdateVariable ParseFlash(in PacketReader p)
    {
        var value = new UpdateVariable();
        value.ReadFlash(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(UpdateVariable value, in PacketWriter p) =>
        value.ComposeFlash(in p);
}
