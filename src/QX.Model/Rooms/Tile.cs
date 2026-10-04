using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a tile position with its height.</summary>
/// <param name="X">The x coordinate.</param>
/// <param name="Y">The y coordinate.</param>
/// <param name="Z">The height in tile units.</param>
public readonly record struct Tile(int X, int Y, float Z) : IParserComposer<Tile>
{
    /// <summary>Initializes a new instance of the <see cref="Tile"/> struct at height 0.</summary>
    /// <param name="X">The x coordinate.</param>
    /// <param name="Y">The y coordinate.</param>
    public Tile(int X, int Y) : this(X, Y, 0) { }

    /// <summary>Gets the x and y coordinates without the height.</summary>
    public Point XY => new(X, Y);

    /// <summary>Returns the position as text using the invariant culture.</summary>
    /// <returns>A string in the form <c>(X, Y, Z)</c>, which <see cref="TryParseString"/> accepts.</returns>
    public override string ToString() => $"({X}, {Y}, {Z.ToString("0.0#######", System.Globalization.CultureInfo.InvariantCulture)})";

    /// <summary>Reads a tile from a packet as two integers and a height.</summary>
    /// <param name="p">The packet to read from.</param>
    public static Tile Parse(in PacketReader p) => new(p.ReadInt(), p.ReadInt(), p.ReadFloat());

    /// <summary>Tries to parse a tile from text.</summary>
    /// <remarks>
    /// Accepts <c>x,y,z</c> with optional surrounding parentheses, the form <see cref="ToString"/>
    /// produces. The height is read with the invariant culture.
    /// </remarks>
    /// <param name="value">The text to parse.</param>
    /// <param name="tile">The parsed tile, or the default value when parsing fails.</param>
    /// <returns><see langword="true"/> when the text was parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseString(string value, out Tile tile)
    {
        tile = default;
        if (value.StartsWith('(') && value.EndsWith(')'))
            value = value[1..^1];

        string[] parts = value.Split(',');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out int x) ||
            !int.TryParse(parts[1], out int y) ||
            !float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out float z))
        {
            return false;
        }

        tile = new Tile(x, y, z);
        return true;
    }

    /// <summary>Parses a tile from text in the form <c>x,y,z</c>, with optional surrounding parentheses.</summary>
    /// <param name="value">The text to parse.</param>
    /// <returns>The parsed tile.</returns>
    /// <exception cref="FormatException">Thrown when the text is not a valid tile.</exception>
    public static Tile ParseString(string value) =>
        TryParseString(value, out Tile tile) ? tile : throw new FormatException($"Invalid tile format: '{value}'.");

    /// <summary>Writes the tile to a packet as two integers and a height.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p)
    {
        p.WriteInt(X);
        p.WriteInt(Y);
        p.WriteFloat(Z);
    }

    /// <summary>Moves a tile by an offset, keeping its height.</summary>
    /// <param name="t">The tile to move.</param>
    /// <param name="offset">The offset to add.</param>
    /// <returns>The moved tile.</returns>
    public static Tile operator +(Tile t, Point offset) => new(t.X + offset.X, t.Y + offset.Y, t.Z);
    /// <summary>Moves a tile back by an offset, keeping its height.</summary>
    /// <param name="t">The tile to move.</param>
    /// <param name="offset">The offset to subtract.</param>
    /// <returns>The moved tile.</returns>
    public static Tile operator -(Tile t, Point offset) => new(t.X - offset.X, t.Y - offset.Y, t.Z);

    /// <summary>Converts a tile to its x and y coordinates, dropping the height.</summary>
    /// <param name="t">The tile to convert.</param>
    public static implicit operator Point(Tile t) => new(t.X, t.Y);
    /// <summary>Converts an x, y and height tuple to a <see cref="Tile"/>.</summary>
    /// <param name="t">The coordinates and height.</param>
    public static implicit operator Tile((int X, int Y, float Z) t) => new(t.X, t.Y, t.Z);
    /// <summary>Converts an x, y and height tuple to a <see cref="Tile"/>, narrowing the height to <see cref="float"/>.</summary>
    /// <param name="t">The coordinates and height.</param>
    public static implicit operator Tile((int X, int Y, double Z) t) => new(t.X, t.Y, (float)t.Z);
}
