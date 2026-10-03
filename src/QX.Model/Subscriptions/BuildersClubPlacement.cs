namespace Qx.Model;

/// <summary>Represents where a Builders Club furni is to be placed.</summary>
/// <remarks>
/// The concrete placement is a <see cref="BuildersClubFloorPlacement"/> or a
/// <see cref="BuildersClubWallPlacement"/>.
/// </remarks>
public abstract record BuildersClubPlacement;

/// <summary>Represents a Builders Club placement on the room floor.</summary>
/// <param name="X">The tile x coordinate.</param>
/// <param name="Y">The tile y coordinate.</param>
/// <param name="Direction">The direction the furni faces, from 0 (north) to 7, clockwise.</param>
public sealed record BuildersClubFloorPlacement(
    int X,
    int Y,
    int Direction) : BuildersClubPlacement;

/// <summary>Represents a Builders Club placement on a room wall.</summary>
/// <param name="WallLocation">The wall location string as sent by the hotel.</param>
public sealed record BuildersClubWallPlacement(
    string WallLocation) : BuildersClubPlacement;
