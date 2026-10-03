namespace Qx.Model;

/// <summary>Specifies the eight directions an avatar or item can face, clockwise from north.</summary>
/// <remarks>North points towards negative y and east towards positive x.</remarks>
public enum Direction
{
    /// <summary>North, towards negative y.</summary>
    North = 0,
    /// <summary>North-east.</summary>
    NorthEast = 1,
    /// <summary>East, towards positive x.</summary>
    East = 2,
    /// <summary>South-east.</summary>
    SouthEast = 3,
    /// <summary>South, towards positive y.</summary>
    South = 4,
    /// <summary>South-west.</summary>
    SouthWest = 5,
    /// <summary>West, towards negative x.</summary>
    West = 6,
    /// <summary>North-west.</summary>
    NorthWest = 7
}

/// <summary>Specifies the dance an avatar is doing.</summary>
public enum Dances
{
    /// <summary>No dance.</summary>
    None = 0,
    /// <summary>The normal dance.</summary>
    Normal = 1,
    /// <summary>The Pogo Mogo dance.</summary>
    PogoMogo = 2,
    /// <summary>The Duck Funk dance.</summary>
    DuckFunk = 3,
    /// <summary>The Rollie dance.</summary>
    RolldaFunk = 4
}
