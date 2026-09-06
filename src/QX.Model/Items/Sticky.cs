using Qx.Messages;

namespace Qx.Model;

public sealed record Sticky(Id Id, string Color, string Text) : IParserComposer<Sticky>
{
    private string color = Color ?? throw new ArgumentNullException(nameof(Color));
    private string text = Text ?? throw new ArgumentNullException(nameof(Text));

    public string Color
    {
        get => color;
        init => color = value ?? throw new ArgumentNullException(nameof(Color));
    }

    public string Text
    {
        get => text;
        init => text = value ?? throw new ArgumentNullException(nameof(Text));
    }

    public static Sticky Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static Sticky ParseFlash(in PacketReader p) => ParseMessage(in p, true);

    private static Sticky ParseUnity(in PacketReader p) => ParseMessage(in p, false);

    private static Sticky ParseMessage(in PacketReader p, bool string_id)
    {
        var strings = new RoomObjectReadStringBudget();
        Id id = string_id
            ? long.TryParse(strings.Read(in p, sizeof(short), nameof(Id)), out long value)
                ? value
                : 0
            : ReadUnityId(in p);
        string data = strings.Read(in p, 0, nameof(Text));

        int space = data.IndexOf(' ');
        string color = space > 0 ? data[..space] : "";
        string text = space >= 0 ? data[(space + 1)..] : data;
        RoomObjectReadWire.RequireEmpty(in p, nameof(Sticky));
        return new Sticky(id, color, text);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(Sticky value, in PacketWriter p)
    {
        StickyWireSnapshot snapshot = Prepare(value, in p, true);
        p.WriteString(snapshot.IdText!);
        p.WriteString(snapshot.Data);
    }

    private static void ComposeUnity(Sticky value, in PacketWriter p)
    {
        StickyWireSnapshot snapshot = Prepare(value, in p, false);
        p.WriteId(snapshot.Id);
        p.WriteString(snapshot.Data);
    }

    private static Id ReadUnityId(in PacketReader p)
    {
        RoomObjectReadWire.RequireRemaining(in p, sizeof(long), sizeof(short), nameof(Id));
        return p.ReadId();
    }

    private static StickyWireSnapshot Prepare(Sticky value, in PacketWriter p, bool string_id)
    {
        ArgumentNullException.ThrowIfNull(value);
        RoomObjectReadWire.RequireSupportedClient(p.Client);
        string data = value.Color.Length > 0 ? $"{value.Color} {value.Text}" : value.Text;
        string? id_text = string_id ? value.Id.ToString() : null;
        var strings = new RoomObjectReadStringBudget();
        if (id_text is not null)
            strings.Require(id_text, in p, nameof(Id));
        else
            RoomObjectReadWire.RequireWireId(p.Client, value.Id, nameof(Id));
        strings.Require(data, in p, nameof(Text));
        return new StickyWireSnapshot(value.Id, id_text, data);
    }
}

internal readonly record struct StickyWireSnapshot(Id Id, string? IdText, string Data);
