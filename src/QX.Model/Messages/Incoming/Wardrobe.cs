using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public readonly record struct WardrobeOutfit(int SlotId, string Figure, string Gender)
    : IParserComposer<WardrobeOutfit>
{
    public static WardrobeOutfit Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WardrobeOutfit ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    private static WardrobeOutfit ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WardrobeOutfit value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(value.SlotId);
        p.WriteString(value.Figure);
        p.WriteString(value.Gender);
    }

    private static void ComposeUnity(WardrobeOutfit value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(value.SlotId);
        p.WriteString(value.Figure);
        p.WriteString(value.Gender);
    }

    internal static void Validate(WardrobeOutfit value, in PacketWriter p)
    {
        WardrobeWire.RequireString(value.Figure, nameof(Figure), in p);
        WardrobeWire.RequireString(value.Gender, nameof(Gender), in p);
    }
}

public sealed record Wardrobe : IParserComposer<Wardrobe>
{
    private IReadOnlyList<WardrobeOutfit> _outfits = Array.Empty<WardrobeOutfit>();

    public Wardrobe(int state, IReadOnlyList<WardrobeOutfit> outfits)
    {
        State = state;
        Outfits = outfits;
    }

    public int State { get; init; }

    public IReadOnlyList<WardrobeOutfit> Outfits
    {
        get => _outfits;
        init => _outfits = WardrobeWire.Freeze(value, nameof(Outfits));
    }

    public static Wardrobe Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static Wardrobe ParseFlash(in PacketReader p)
    {
        int state = p.ReadInt();
        int count = WardrobeWire.ReadFlashCount(in p, nameof(Outfits));
        var outfits = new WardrobeOutfit[count];
        for (int i = 0; i < count; i++)
            outfits[i] = p.Parse<WardrobeOutfit>();
        return new Wardrobe(state, outfits);
    }

    private static Wardrobe ParseUnity(in PacketReader p)
    {
        int state = p.ReadInt();
        int count = WardrobeWire.ReadUnityCount(in p, nameof(Outfits));
        var outfits = new WardrobeOutfit[count];
        for (int i = 0; i < count; i++)
            outfits[i] = p.Parse<WardrobeOutfit>();
        return new Wardrobe(state, outfits);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(Wardrobe value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(value.State);
        p.WriteInt(value.Outfits.Count);
        foreach (WardrobeOutfit outfit in value.Outfits)
            p.Compose(outfit);
    }

    private static void ComposeUnity(Wardrobe value, in PacketWriter p)
    {
        WardrobeWire.RequireUnityCount(value.Outfits.Count, nameof(Outfits));
        Validate(value, in p);
        p.WriteInt(value.State);
        p.WriteLength((Length)value.Outfits.Count);
        foreach (WardrobeOutfit outfit in value.Outfits)
            p.Compose(outfit);
    }

    private static void Validate(Wardrobe value, in PacketWriter p)
    {
        foreach (WardrobeOutfit outfit in value.Outfits)
            WardrobeOutfit.Validate(outfit, in p);
    }
}

internal static class WardrobeWire
{
    public static IReadOnlyList<WardrobeOutfit> Freeze(
        IReadOnlyList<WardrobeOutfit> values,
        string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return Array.AsReadOnly(values.ToArray());
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException($"{name} exceeds the wire string limit.", name);
    }

    public static int ReadFlashCount(in PacketReader p, string name)
    {
        int available = p.Available;
        int count = p.ReadInt();
        return RequireBoundedCount(count, available - sizeof(int), name);
    }

    public static int ReadUnityCount(in PacketReader p, string name)
    {
        int available = p.Available;
        int count = p.ReadLength();
        return RequireBoundedCount(count, available - sizeof(short), name);
    }

    public static void RequireUnityCount(int count, string name)
    {
        if ((uint)count > ushort.MaxValue)
            throw new InvalidDataException($"{name} count {count} exceeds the Unity wire limit.");
    }

    private static int RequireBoundedCount(int count, int available, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        if (available < 0 || count > available / 8)
            throw new InvalidDataException($"{name} count {count} exceeds the remaining payload capacity.");
        return count;
    }
}
