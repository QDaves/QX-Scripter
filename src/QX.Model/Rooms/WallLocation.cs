using System.Globalization;
using Qx;
using Qx.Messages;

namespace Qx.Model;

public readonly record struct WallOrientation
{
    public static readonly WallOrientation Left = new('l');
    public static readonly WallOrientation Right = new('r');

    public readonly char Value;

    private WallOrientation(char value) => Value = value;

    public bool IsLeft => Value == 'l';
    public bool IsRight => Value == 'r';
    public WallOrientation Opposite => IsLeft ? Right : Left;

    public override string ToString() => Value.ToString();

    public static WallOrientation FromChar(char c) => c switch
    {
        'l' => Left,
        'r' => Right,
        _ => throw new ArgumentException($"Invalid wall orientation '{c}'. Must be 'l' or 'r'.")
    };

    public static implicit operator WallOrientation(char c) => FromChar(c);
    public static implicit operator char(WallOrientation o) => o.Value;
    public static implicit operator string(WallOrientation o) => o.ToString();
}

public readonly record struct WallLocation(Point Wall, Point Offset, WallOrientation Orientation) : IParserComposer<WallLocation>
{
    public static readonly WallLocation Zero = new((0, 0), (0, 0), WallOrientation.Left);

    public WallLocation(int wx, int wy, int lx, int ly, WallOrientation orientation)
        : this((wx, wy), (lx, ly), orientation) { }

    public WallLocation Flip() => this with { Orientation = Orientation.Opposite };
    public WallLocation Orient(WallOrientation orientation) => this with { Orientation = orientation };

    public override string ToString() => FormattableString.Invariant(
        $":w={Wall.X},{Wall.Y} l={Offset.X},{Offset.Y} {Orientation.Value}");

    public void Compose(in PacketWriter p)
    {
        if (p.Client is ClientType.Unity)
        {
            p.WriteInt(Wall.X);
            p.WriteInt(Wall.Y);
            p.WriteInt(Offset.X);
            p.WriteInt(Offset.Y);
            p.WriteString(Orientation.ToString());
        }
        else
        {
            p.WriteString(ToString());
        }
    }

    public static WallLocation Parse(in PacketReader p) => ParseString(p.ReadString());

    public static WallLocation ParseString(string value) =>
        TryParse(value, out WallLocation location)
            ? location
            : throw new FormatException($"Invalid wall location format: '{value}'.");

    public static bool TryParse(string value, out WallLocation location)
    {
        location = default;

        if (string.IsNullOrEmpty(value) || !value.StartsWith(':'))
            return false;

        string[] parts = value.Split(' ', 4);
        if (parts.Length < 3 ||
            parts[0].Length < 6 || parts[1].Length < 5 || parts[2].Length != 1 ||
            !parts[0].StartsWith(":w=", StringComparison.Ordinal) ||
            !parts[1].StartsWith("l=", StringComparison.Ordinal))
        {
            return false;
        }

        string[] wall = parts[0][3..].Split(',');
        if (wall.Length != 2 ||
            !int.TryParse(wall[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int wx) ||
            !int.TryParse(wall[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int wy))
            return false;

        string[] offset = parts[1][2..].Split(',');
        if (offset.Length != 2 ||
            !int.TryParse(offset[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lx) ||
            !int.TryParse(offset[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ly))
            return false;

        WallOrientation orientation;
        switch (parts[2][0])
        {
            case 'l': orientation = WallOrientation.Left; break;
            case 'r': orientation = WallOrientation.Right; break;
            default: return false;
        }

        location = new WallLocation(wx, wy, lx, ly, orientation);
        return true;
    }

    public static implicit operator WallLocation(string s) => ParseString(s);
}

internal static class RoomPlacementWire
{
    public static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireSize(in PacketReader p, int expected, string name)
    {
        if (p.Available != expected)
        {
            throw new InvalidDataException(
                $"{name} requires exactly {expected} bytes, received {p.Available}.");
        }
    }

    public static void RequireMinimum(in PacketReader p, int minimum, string name)
    {
        if (p.Available < minimum)
        {
            throw new InvalidDataException(
                $"{name} requires at least {minimum} bytes, received {p.Available}.");
        }
    }

    public static int RequireCategory(int category, string name)
    {
        if (category is not (1 or 2))
            throw new InvalidDataException($"{name} contains unsupported category {category}.");
        return category;
    }

    public static WallLocation ReadUnityWallLocation(in PacketReader p)
    {
        int wx = p.ReadInt();
        int wy = p.ReadInt();
        int lx = p.ReadInt();
        int ly = p.ReadInt();
        string orientation = p.ReadString();
        if (orientation.Length != 1 || orientation[0] is not ('l' or 'r'))
            throw new InvalidDataException("Unity wall location contains an invalid orientation.");
        return new WallLocation(wx, wy, lx, ly, orientation[0]);
    }

    public static WallLocation RequireWallLocation(WallLocation location, string name)
    {
        if (location.Orientation.Value is not ('l' or 'r'))
            throw new InvalidDataException($"{name} contains an invalid wall orientation.");
        return location;
    }

    public static Id ReadFlashStringId(in PacketReader p, string name)
    {
        string value = p.ReadString();
        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int identifier))
        {
            throw new InvalidDataException($"{name} contains an invalid Flash identifier.");
        }
        return identifier;
    }

    public static void WriteFlashStringId(
        Id value,
        string name,
        in PacketWriter p)
    {
        RequireId(value, name, in p);
        int identifier = checked((int)(long)value);
        p.WriteString(identifier.ToString(CultureInfo.InvariantCulture));
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException($"{name} exceeds the wire string limit.", name);
    }

    public static void RequireId(Id value, string name, in PacketWriter p)
    {
        switch (p.Client)
        {
            case ClientType.Flash:
                try
                {
                    _ = checked((int)(long)value);
                }
                catch (OverflowException error)
                {
                    throw new InvalidDataException($"{name} exceeds the Flash identifier range.", error);
                }
                break;
            case ClientType.Unity:
                break;
            default:
                throw new UnsupportedClientException(p.Client);
        }
    }

    public static void ValidateFloorItem(
        FloorItem item,
        bool include_owner_name,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(item);
        RequireId(item.Id, nameof(item.Id), in p);
        RequireId(item.Extra, nameof(item.Extra), in p);
        RequireId(item.OwnerId, nameof(item.OwnerId), in p);
        InventoryWire.ValidateItemData(
            item.Data,
            p.Client is ClientType.Unity,
            in p);
        if (item.Kind < 0)
            RequireString(item.Identifier ?? "", nameof(item.Identifier), in p);
        if (include_owner_name)
            RequireString(item.OwnerName, nameof(item.OwnerName), in p);
    }

    public static void ValidateWallItem(
        WallItem item,
        bool include_owner_name,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(item);
        RequireId(item.Id, nameof(item.Id), in p);
        RequireId(item.OwnerId, nameof(item.OwnerId), in p);
        WallLocation location = RequireWallLocation(item.Location, nameof(item.Location));
        RequireString(location.ToString(), nameof(item.Location), in p);
        RequireString(item.Data, nameof(item.Data), in p);
        if (include_owner_name)
            RequireString(item.OwnerName, nameof(item.OwnerName), in p);
    }
}
