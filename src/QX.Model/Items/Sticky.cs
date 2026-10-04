using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents the contents of a sticky note on a room wall.</summary>
/// <remarks>Received as the Flash <c>ItemDataUpdate</c> message.</remarks>
/// <param name="Id">The sticky note's item identifier, or 0 when the packet's identifier is not numeric.</param>
/// <param name="Color">The note color as a hex string, empty when the packet carries none.</param>
/// <param name="Text">The note text.</param>
public sealed record Sticky(Id Id, string Color, string Text) : IParserComposer<Sticky>
{
    private string color = Color ?? throw new ArgumentNullException(nameof(Color));
    private string text = Text ?? throw new ArgumentNullException(nameof(Text));

    /// <summary>Gets the note color as a hex string, empty when the packet carries none.</summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
    public string Color
    {
        get => color;
        init => color = value ?? throw new ArgumentNullException(nameof(Color));
    }

    /// <summary>Gets the note text.</summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
    public string Text
    {
        get => text;
        init => text = value ?? throw new ArgumentNullException(nameof(Text));
    }

    /// <summary>Reads a sticky note from a packet.</summary>
    /// <remarks>
    /// The identifier is sent as a string, followed by the note data in the form <c>color text</c>.
    /// The part before the first space becomes <see cref="Color"/> and the rest becomes
    /// <see cref="Text"/>; without a space the whole data string becomes <see cref="Text"/>.
    /// </remarks>
    /// <param name="p">The packet to read from.</param>
    public static Sticky Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static Sticky ParseFlash(in PacketReader p)
    {
        var strings = new RoomObjectReadStringBudget();
        Id id = long.TryParse(strings.Read(in p, sizeof(short), nameof(Id)), out long value)
            ? value
            : 0;
        string data = strings.Read(in p, 0, nameof(Text));

        int space = data.IndexOf(' ');
        string color = space > 0 ? data[..space] : "";
        string text = space >= 0 ? data[(space + 1)..] : data;
        RoomObjectReadWire.RequireEmpty(in p, nameof(Sticky));
        return new Sticky(id, color, text);
    }

    /// <summary>Writes the sticky note to a packet as <c>color text</c>, or as the text alone when <see cref="Color"/> is empty.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(Sticky value, in PacketWriter p)
    {
        StickyWireSnapshot snapshot = Prepare(value, in p, true);
        p.WriteString(snapshot.IdText!);
        p.WriteString(snapshot.Data);
    }

    private static StickyWireSnapshot Prepare(Sticky value, in PacketWriter p, bool string_id)
    {
        ArgumentNullException.ThrowIfNull(value);
        string data = value.Color.Length > 0 ? $"{value.Color} {value.Text}" : value.Text;
        string? id_text = string_id ? value.Id.ToString() : null;
        var strings = new RoomObjectReadStringBudget();
        if (id_text is not null)
            strings.Require(id_text, in p, nameof(Id));
        else
            RoomObjectReadWire.RequireWireId(value.Id, nameof(Id));
        strings.Require(data, in p, nameof(Text));
        return new StickyWireSnapshot(value.Id, id_text, data);
    }
}

internal readonly record struct StickyWireSnapshot(Id Id, string? IdText, string Data);
