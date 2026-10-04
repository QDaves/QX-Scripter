using System.Globalization;
using Qx;
using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents the side of a wall an item hangs on, <c>l</c> for left or <c>r</c> for right.</summary>
public readonly record struct WallOrientation
{
    /// <summary>The left wall orientation, <c>l</c>.</summary>
    public static readonly WallOrientation Left = new('l');
    /// <summary>The right wall orientation, <c>r</c>.</summary>
    public static readonly WallOrientation Right = new('r');

    /// <summary>The orientation character, <c>l</c> or <c>r</c>, or <c>'\0'</c> for the default value.</summary>
    public readonly char Value;

    private WallOrientation(char value) => Value = value;

    /// <summary>Gets whether this is the left orientation.</summary>
    public bool IsLeft => Value == 'l';
    /// <summary>Gets whether this is the right orientation.</summary>
    public bool IsRight => Value == 'r';
    /// <summary>Gets the opposite orientation, which is <see cref="Left"/> for anything but <see cref="Left"/>.</summary>
    public WallOrientation Opposite => IsLeft ? Right : Left;

    /// <summary>Returns the orientation character as a string.</summary>
    /// <returns><c>l</c> or <c>r</c>.</returns>
    public override string ToString() => Value.ToString();

    /// <summary>Converts an orientation character to a <see cref="WallOrientation"/>.</summary>
    /// <param name="c">The character, <c>l</c> or <c>r</c>.</param>
    /// <returns><see cref="Left"/> or <see cref="Right"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="c"/> is not <c>l</c> or <c>r</c>.</exception>
    public static WallOrientation FromChar(char c) => c switch
    {
        'l' => Left,
        'r' => Right,
        _ => throw new ArgumentException($"Invalid wall orientation '{c}'. Must be 'l' or 'r'.")
    };

    /// <summary>Converts an orientation character to a <see cref="WallOrientation"/>.</summary>
    /// <param name="c">The character, <c>l</c> or <c>r</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="c"/> is not <c>l</c> or <c>r</c>.</exception>
    public static implicit operator WallOrientation(char c) => FromChar(c);
    /// <summary>Converts an orientation to its character.</summary>
    /// <param name="o">The orientation.</param>
    public static implicit operator char(WallOrientation o) => o.Value;
    /// <summary>Converts an orientation to its character as a string.</summary>
    /// <param name="o">The orientation.</param>
    public static implicit operator string(WallOrientation o) => o.ToString();
}

/// <summary>Represents where a wall item hangs.</summary>
/// <remarks>The text form is <c>:w=x,y l=x,y o</c>, for example <c>:w=3,5 l=12,40 r</c>.</remarks>
/// <param name="Wall">The wall tile the item hangs on.</param>
/// <param name="Offset">The item's offset on the wall tile.</param>
/// <param name="Orientation">Whether the item hangs on a left or a right wall.</param>
public readonly record struct WallLocation(Point Wall, Point Offset, WallOrientation Orientation) : IParserComposer<WallLocation>
{
    /// <summary>The location <c>:w=0,0 l=0,0 l</c>.</summary>
    public static readonly WallLocation Zero = new((0, 0), (0, 0), WallOrientation.Left);

    /// <summary>Initializes a new instance of the <see cref="WallLocation"/> struct from its coordinates.</summary>
    /// <param name="wx">The x coordinate of the wall tile.</param>
    /// <param name="wy">The y coordinate of the wall tile.</param>
    /// <param name="lx">The x offset on the wall tile.</param>
    /// <param name="ly">The y offset on the wall tile.</param>
    /// <param name="Orientation">Whether the item hangs on a left or a right wall.</param>
    public WallLocation(int wx, int wy, int lx, int ly, WallOrientation Orientation)
        : this((wx, wy), (lx, ly), Orientation) { }

    /// <summary>Returns the same location on the opposite wall orientation.</summary>
    /// <returns>A copy with <see cref="WallOrientation.Opposite"/> as its orientation.</returns>
    public WallLocation Flip() => this with { Orientation = Orientation.Opposite };
    /// <summary>Returns the same location with a given wall orientation.</summary>
    /// <param name="orientation">The orientation to use.</param>
    /// <returns>A copy with the given orientation.</returns>
    public WallLocation Orient(WallOrientation orientation) => this with { Orientation = orientation };

    /// <summary>Returns the location in the text form the hotel uses.</summary>
    /// <returns>A string in the form <c>:w=x,y l=x,y o</c>.</returns>
    public override string ToString() => FormattableString.Invariant(
        $":w={Wall.X},{Wall.Y} l={Offset.X},{Offset.Y} {Orientation.Value}");

    /// <summary>Writes the location to a packet as its text form.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteString(ToString());
    }

    /// <summary>Reads a location from a packet as its text form.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="FormatException">Thrown when the text is not a valid wall location.</exception>
    public static WallLocation Parse(in PacketReader p) => ParseString(p.ReadString());

    /// <summary>Parses a location from its text form.</summary>
    /// <param name="value">The text, in the form <c>:w=x,y l=x,y o</c>.</param>
    /// <returns>The parsed location.</returns>
    /// <exception cref="FormatException">Thrown when the text is not a valid wall location.</exception>
    public static WallLocation ParseString(string value) =>
        TryParse(value, out WallLocation location)
            ? location
            : throw new FormatException($"Invalid wall location format: '{value}'.");

    /// <summary>Tries to parse a location from its text form.</summary>
    /// <param name="value">The text, in the form <c>:w=x,y l=x,y o</c>.</param>
    /// <param name="location">The parsed location, or the default value when parsing fails.</param>
    /// <returns><see langword="true"/> when the text was parsed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>Converts a location's text form to a <see cref="WallLocation"/>.</summary>
    /// <param name="s">The text, in the form <c>:w=x,y l=x,y o</c>.</param>
    /// <exception cref="FormatException">Thrown when the text is not a valid wall location.</exception>
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
        RequireId(value, name);
        int identifier = checked((int)(long)value);
        p.WriteString(identifier.ToString(CultureInfo.InvariantCulture));
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException($"{name} exceeds the wire string limit.", name);
    }

    public static void RequireId(Id value, string name)
    {
        try
        {
            _ = checked((int)(long)value);
        }
        catch (OverflowException error)
        {
            throw new InvalidDataException($"{name} exceeds the Flash identifier range.", error);
        }
    }

    public static void ValidateFloorItem(
        FloorItem item,
        bool include_owner_name,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(item);
        RequireId(item.Id, nameof(item.Id));
        RequireId(item.Extra, nameof(item.Extra));
        RequireId(item.OwnerId, nameof(item.OwnerId));
        InventoryWire.ValidateItemData(
            item.Data,
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
        RequireId(item.Id, nameof(item.Id));
        RequireId(item.OwnerId, nameof(item.OwnerId));
        WallLocation location = RequireWallLocation(item.Location, nameof(item.Location));
        RequireString(location.ToString(), nameof(item.Location), in p);
        RequireString(item.Data, nameof(item.Data), in p);
        if (include_owner_name)
            RequireString(item.OwnerName, nameof(item.OwnerName), in p);
    }
}
