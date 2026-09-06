using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record WardrobeRequest : IParserComposer<WardrobeRequest>
{
    public static WardrobeRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WardrobeRequest ParseFlash(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    private static WardrobeRequest ParseUnity(in PacketReader p)
    {
        RequireEmpty(in p);
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WardrobeRequest value, in PacketWriter p) { }

    private static void ComposeUnity(WardrobeRequest value, in PacketWriter p) { }

    private static void RequireEmpty(in PacketReader p)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{nameof(WardrobeRequest)} contains {p.Available} unexpected bytes.");
    }
}

public sealed record SaveWardrobeOutfitRequest(
    int SlotId,
    string Figure,
    string Gender) : IParserComposer<SaveWardrobeOutfitRequest>
{
    public static SaveWardrobeOutfitRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SaveWardrobeOutfitRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    private static SaveWardrobeOutfitRequest ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SaveWardrobeOutfitRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteInt(value.SlotId);
        p.WriteString(value.Figure);
        p.WriteString(value.Gender);
    }

    private static void ComposeUnity(SaveWardrobeOutfitRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteInt(value.SlotId);
        p.WriteString(value.Figure);
        p.WriteString(value.Gender);
    }

    private static void ValidateStrings(SaveWardrobeOutfitRequest value, in PacketWriter p)
    {
        ValidateString(value.Figure, nameof(Figure), in p);
        ValidateString(value.Gender, nameof(Gender), in p);
    }

    private static void ValidateString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        int length = p.Encoding.GetByteCount(value);
        if (length > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"String byte length ({length}) exceeds {ushort.MaxValue}.",
                name);
        }
    }
}
