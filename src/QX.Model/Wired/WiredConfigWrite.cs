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
    }
}

public sealed record UpdateTrigger : WiredConfigWrite, IParserComposer<UpdateTrigger>
{
    public static UpdateTrigger Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UpdateTrigger ParseFlash(in PacketReader p)
    {
        var value = new UpdateTrigger();
        value.ReadFlash(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UpdateTrigger value, in PacketWriter p) =>
        value.ComposeFlash(in p);
}

public sealed record UpdateAction : WiredConfigWrite, IParserComposer<UpdateAction>
{
    public int Delay { get; set; }

    public static UpdateAction Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UpdateAction ParseFlash(in PacketReader p)
    {
        var value = new UpdateAction();
        value.ReadFlash(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    protected override void ReadExtra(in PacketReader p) => Delay = p.ReadInt();

    protected override void WriteExtra(in PacketWriter p) => p.WriteInt(Delay);

    private static void ComposeFlash(UpdateAction value, in PacketWriter p) =>
        value.ComposeFlash(in p);
}

public sealed record UpdateCondition : WiredConfigWrite, IParserComposer<UpdateCondition>
{
    public int Quantifier { get; set; }

    public static UpdateCondition Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UpdateCondition ParseFlash(in PacketReader p)
    {
        var value = new UpdateCondition();
        value.ReadFlash(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    protected override void ReadExtra(in PacketReader p) => Quantifier = p.ReadInt();

    protected override void WriteExtra(in PacketWriter p) => p.WriteInt(Quantifier);

    private static void ComposeFlash(UpdateCondition value, in PacketWriter p) =>
        value.ComposeFlash(in p);
}

public sealed record UpdateAddon : WiredConfigWrite, IParserComposer<UpdateAddon>
{
    public static UpdateAddon Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UpdateAddon ParseFlash(in PacketReader p)
    {
        var value = new UpdateAddon();
        value.ReadFlash(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UpdateAddon value, in PacketWriter p) =>
        value.ComposeFlash(in p);
}

public sealed record UpdateSelector : WiredConfigWrite, IParserComposer<UpdateSelector>
{
    public bool IsFilter { get; set; }
    public bool IsInvert { get; set; }

    public static UpdateSelector Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UpdateSelector ParseFlash(in PacketReader p)
    {
        var value = new UpdateSelector();
        value.ReadFlash(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

public sealed record UpdateVariable : WiredConfigWrite, IParserComposer<UpdateVariable>
{
    public static UpdateVariable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UpdateVariable ParseFlash(in PacketReader p)
    {
        var value = new UpdateVariable();
        value.ReadFlash(in p);
        return value;
    }

    public override void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UpdateVariable value, in PacketWriter p) =>
        value.ComposeFlash(in p);
}
