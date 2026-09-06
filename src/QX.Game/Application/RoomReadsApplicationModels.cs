using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record RoomDataReadRequest(
    Id RoomId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record RoomDataView(
    Id Id,
    string Name,
    Id OwnerId,
    string OwnerName,
    RoomDoorMode DoorMode,
    int UserCount,
    int MaxUserCount,
    string Description,
    RoomTradeMode TradeMode,
    int Score,
    int Ranking,
    int Category,
    IReadOnlyList<string> Tags,
    string? OfficialRoomPicRef,
    bool HasGroup,
    Id GroupId,
    string GroupName,
    string GroupBadge,
    bool HasEvent,
    string EventName,
    string EventDescription,
    int EventMinutesRemaining,
    bool ShowOwner,
    bool AllowPets,
    bool DisplayRoomEntryAd)
{
    private IReadOnlyList<string> tags = Freeze(Tags);

    public IReadOnlyList<string> Tags
    {
        get => tags;
        init => tags = Freeze(value);
    }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copy = new string[values.Count];
        for (int index = 0; index < copy.Length; index++)
        {
            string value = values[index];
            ArgumentNullException.ThrowIfNull(value);
            copy[index] = value;
        }
        return Array.AsReadOnly(copy);
    }
}

public sealed record RoomDataReadResult(
    ClientType Client,
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    Id RequestedRoomId,
    int MessagesDispatched,
    RoomDataView Room);

public sealed record RoomRightsReadRequest(
    Id RoomId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record RoomRightsReadResult(
    ClientType Client,
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    Id RoomId,
    int MessagesDispatched,
    IReadOnlyList<IdName> Users)
{
    private IReadOnlyList<IdName> users = Freeze(Users);

    public IReadOnlyList<IdName> Users
    {
        get => users;
        init => users = Freeze(value);
    }

    private static IReadOnlyList<IdName> Freeze(IReadOnlyList<IdName> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record PetInfoReadRequest(
    Id PetId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record PetInfoView(
    Id Id,
    string Name,
    int Level,
    int MaxLevel,
    int Experience,
    int MaxExperience,
    int Energy,
    int MaxEnergy,
    int Happiness,
    int MaxHappiness,
    int Scratches,
    Id OwnerId,
    int Age,
    string OwnerName,
    int BreedId,
    bool HasFreeSaddle,
    bool IsRiding,
    IReadOnlyList<int> SkillThresholds,
    int AccessRights,
    bool CanBreed,
    bool CanHarvest,
    bool CanRevive,
    int RarityLevel,
    int MaxWellbeingSeconds,
    int RemainingWellbeingSeconds,
    int RemainingGrowingSeconds,
    bool HasBreedingPermission)
{
    private IReadOnlyList<int> skill_thresholds = Freeze(SkillThresholds);

    public IReadOnlyList<int> SkillThresholds
    {
        get => skill_thresholds;
        init => skill_thresholds = Freeze(value);
    }

    private static IReadOnlyList<int> Freeze(IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed record PetInfoReadResult(
    ClientType Client,
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    Id RequestedPetId,
    int MessagesDispatched,
    PetInfoView Pet);

public sealed record StickyReadRequest(
    Id ItemId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record StickyReadResult(
    ClientType Client,
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    Id ItemId,
    int MessagesDispatched,
    string Color,
    string Text);

public sealed record RoomAdInfoReadRequest(
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record RoomAdRoomView(
    Id RoomId,
    string RoomName,
    bool HasControllers);

public sealed record RoomAdInfoReadResult(
    ClientType Client,
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    int MessagesDispatched,
    bool IsVip,
    IReadOnlyList<RoomAdRoomView> Rooms)
{
    private IReadOnlyList<RoomAdRoomView> rooms = Freeze(Rooms);

    public IReadOnlyList<RoomAdRoomView> Rooms
    {
        get => rooms;
        init => rooms = Freeze(value);
    }

    private static IReadOnlyList<RoomAdRoomView> Freeze(IReadOnlyList<RoomAdRoomView> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copy = new RoomAdRoomView[values.Count];
        for (int index = 0; index < copy.Length; index++)
        {
            RoomAdRoomView value = values[index];
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.RoomName);
            copy[index] = value;
        }
        return Array.AsReadOnly(copy);
    }
}
