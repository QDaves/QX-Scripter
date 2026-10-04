using System.Globalization;
using System.Text;
using Qx.Messages;

namespace Qx.Interception.GEarth;

/// <summary>Represents an intercepted packet in G-Earth's text form, with its block and edit flags.</summary>
/// <remarks>
/// The text form is four tab-separated fields: the blocked flag, the index, <c>TOCLIENT</c> or
/// <c>TOSERVER</c>, and the edited flag directly followed by the raw packet bytes as Latin-1 text.
/// </remarks>
internal sealed class HMessage
{
    /// <summary>Gets or sets whether the packet is blocked.</summary>
    public bool IsBlocked { get; set; }
    /// <summary>Gets or sets the index G-Earth assigned to the packet.</summary>
    public int Index { get; set; }
    /// <summary>Gets or sets the direction of the packet.</summary>
    public MessageDirection Direction { get; set; }
    /// <summary>Gets or sets whether the packet was modified.</summary>
    public bool IsEdited { get; set; }
    /// <summary>Gets or sets the packet.</summary>
    public required Packet Packet { get; set; }

    /// <summary>Parses a message from G-Earth's text form.</summary>
    /// <param name="value">The text form of the message.</param>
    /// <returns>The parsed message.</returns>
    /// <exception cref="InvalidDataException">Thrown when the raw packet is incomplete.</exception>
    /// <exception cref="FormatException">Thrown when the index is not an integer.</exception>
    public static HMessage Parse(string value)
    {
        string[] parts = value.Split('\t', 4);
        MessageDirection direction = parts[2] == "TOCLIENT" ? MessageDirection.In : MessageDirection.Out;

        string hpacket = parts[3];
        bool edited = hpacket.Length > 0 && hpacket[0] == '1';
        byte[] raw = Encoding.Latin1.GetBytes(hpacket.AsSpan(1).ToString());

        return new HMessage
        {
            IsBlocked = parts[0] == "1",
            Index = int.Parse(parts[1], CultureInfo.InvariantCulture),
            Direction = direction,
            IsEdited = edited,
            Packet = EvaWire.ToPacket(raw, direction)
        };
    }

    /// <summary>Formats the message in G-Earth's text form.</summary>
    /// <returns>The text form of the message.</returns>
    public string Stringify()
    {
        byte[] raw = EvaWire.FromPacket(Packet);
        string hpacket = (IsEdited ? "1" : "0") + Encoding.Latin1.GetString(raw);
        string direction = Direction == MessageDirection.In ? "TOCLIENT" : "TOSERVER";
        return $"{(IsBlocked ? "1" : "0")}\t{Index}\t{direction}\t{hpacket}";
    }
}
