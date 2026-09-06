using System.Globalization;
using System.Text;
using Qx.Messages;

namespace Qx.Interception.GEarth;

public sealed class HMessage
{
    public bool IsBlocked { get; set; }
    public int Index { get; set; }
    public Direction Direction { get; set; }
    public bool IsEdited { get; set; }
    public required Packet Packet { get; set; }

    public static HMessage Parse(string value, ClientType client)
    {
        string[] parts = value.Split('\t', 4);
        Direction direction = parts[2] == "TOCLIENT" ? Direction.In : Direction.Out;

        string hpacket = parts[3];
        bool edited = hpacket.Length > 0 && hpacket[0] == '1';
        byte[] raw = Encoding.Latin1.GetBytes(hpacket.AsSpan(1).ToString());

        return new HMessage
        {
            IsBlocked = parts[0] == "1",
            Index = int.Parse(parts[1], CultureInfo.InvariantCulture),
            Direction = direction,
            IsEdited = edited,
            Packet = EvaWire.ToPacket(raw, client, direction)
        };
    }

    public string Stringify()
    {
        byte[] raw = EvaWire.FromPacket(Packet);
        string hpacket = (IsEdited ? "1" : "0") + Encoding.Latin1.GetString(raw);
        string direction = Direction == Direction.In ? "TOCLIENT" : "TOSERVER";
        return $"{(IsBlocked ? "1" : "0")}\t{Index}\t{direction}\t{hpacket}";
    }
}
