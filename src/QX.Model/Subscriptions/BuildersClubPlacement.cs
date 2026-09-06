namespace Qx.Model.Subscriptions;

public abstract record BuildersClubPlacement;

public sealed record BuildersClubFloorPlacement(
    int X,
    int Y,
    int Direction) : BuildersClubPlacement;

public sealed record BuildersClubWallPlacement(
    string WallLocation) : BuildersClubPlacement;
