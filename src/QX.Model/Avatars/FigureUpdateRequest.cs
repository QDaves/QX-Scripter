using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Represents the request that changes the local user's figure.</summary>
/// <remarks>Sent as the Flash <c>UpdateFigureData</c> message.</remarks>
/// <param name="Gender">The figure gender code, for example <c>M</c> or <c>F</c>.</param>
/// <param name="Figure">The new figure string.</param>
public sealed record FigureUpdateRequest(string Gender, string Figure)
    : IParserComposer<FigureUpdateRequest>
{
    /// <summary>Reads the request from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static FigureUpdateRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FigureUpdateRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    /// <summary>Writes the request to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <see cref="Gender"/> or <see cref="Figure"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a string exceeds the protocol length limit.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FigureUpdateRequest value, in PacketWriter p)
    {
        ValidateStrings(value, in p);
        p.WriteString(value.Gender);
        p.WriteString(value.Figure);
    }

    private static void ValidateStrings(FigureUpdateRequest value, in PacketWriter p)
    {
        ValidateString(value.Gender, nameof(Gender), in p);
        ValidateString(value.Figure, nameof(Figure), in p);
    }

    private static void ValidateString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException("String exceeds the protocol limit.", name);
    }
}
