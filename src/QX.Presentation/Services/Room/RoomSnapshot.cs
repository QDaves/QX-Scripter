using Qx.Model;

namespace Qx.Presentation.Services.Room;

public enum RoomPresence
{
    Disconnected,
    Outside,
    Entering,
    Inside
}

public enum RoomPersonKind
{
    User,
    Bot,
    Pet
}

public sealed record RoomPerson(
    Id UserId,
    int Index,
    string Name,
    string Detail,
    string Position,
    RoomPersonKind Kind,
    bool IsStaff,
    bool IsIdle,
    bool IsTrading,
    string Figure,
    string Motto,
    Point Tile,
    long RoomGeneration,
    Avatar? Person);

public sealed record RoomVisit(
    Id UserId,
    int Index,
    string Name,
    string Window,
    int Visits,
    bool IsHere);

public sealed record RoomBanEntry(Id UserId, string Name, long RoomGeneration);

public sealed record RoomFurniPiece(
    Id ItemId,
    ItemType Placement,
    int Kind,
    string Name,
    string Identifier,
    string Owner,
    string Position,
    bool IsHidden,
    bool IsFloor,
    Point Tile,
    int Revision,
    Furni Item);

public sealed record RoomFact(string Caption, string Value);

public sealed record RoomRules(
    string Access,
    string Mute,
    string Kick,
    string Ban,
    string Flow,
    string Bubble,
    string Scroll,
    string Hearing,
    string Flood)
{
    public const string Missing = "not received";

    public static RoomRules Unknown { get; } =
        new(Missing, Missing, Missing, Missing, Missing, Missing, Missing, Missing, Missing);
}

public sealed record RoomIdentity(
    Id RoomId,
    long Generation,
    string Name,
    string OwnerName,
    string Description,
    string PictureRef,
    int UserCount,
    int MaxUserCount,
    bool IsOwner,
    bool HasRights)
{
    public static RoomIdentity None { get; } = new(0, 0, "", "", "", "", 0, 0, false, false);
}

public sealed record RoomSnapshot(
    RoomPresence Presence,
    RoomIdentity Identity,
    IReadOnlyList<RoomFact> Facts,
    RoomRules Rules,
    IReadOnlyList<RoomPerson> People,
    IReadOnlyList<RoomVisit> Visits,
    IReadOnlyList<RoomFurniPiece> Furni,
    int HiddenCount)
{
    public static RoomSnapshot Empty { get; } =
        new(RoomPresence.Outside, RoomIdentity.None, [], RoomRules.Unknown, [], [], [], 0);

    public bool IsInRoom => Presence is RoomPresence.Inside;
}
