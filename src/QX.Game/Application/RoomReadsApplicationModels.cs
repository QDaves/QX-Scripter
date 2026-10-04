using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to load the navigator data of one room from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomDataGet"/>. One request is sent and the call completes
/// with the first room data response for <paramref name="RoomId"/>.
/// </remarks>
/// <param name="RoomId">The id of the room. Must be positive and fit in a 32-bit integer.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the matching response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record RoomDataReadRequest(
    Id RoomId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the navigator data of a room.
/// </summary>
/// <param name="Id">The id of the room.</param>
/// <param name="Name">The room name.</param>
/// <param name="OwnerId">The id of the room's owner.</param>
/// <param name="OwnerName">The name of the room's owner.</param>
/// <param name="DoorMode">Who may enter the room.</param>
/// <param name="UserCount">The number of users in the room.</param>
/// <param name="MaxUserCount">The room's user capacity.</param>
/// <param name="Description">The room description.</param>
/// <param name="TradeMode">Who may trade in the room.</param>
/// <param name="Score">The room's like count.</param>
/// <param name="Ranking">The room's position in the hotel ranking.</param>
/// <param name="Category">The id of the navigator category, numbered per hotel.</param>
/// <param name="Tags">The room's search tags.</param>
/// <param name="OfficialRoomPicRef">The official room picture reference, or <see langword="null"/> for an ordinary room.</param>
/// <param name="HasGroup">Whether the room belongs to a group.</param>
/// <param name="GroupId">The id of the group that owns the room, meaningful only when <paramref name="HasGroup"/> is <see langword="true"/>.</param>
/// <param name="GroupName">The name of the group that owns the room, meaningful only when <paramref name="HasGroup"/> is <see langword="true"/>.</param>
/// <param name="GroupBadge">The badge code of the group that owns the room, meaningful only when <paramref name="HasGroup"/> is <see langword="true"/>.</param>
/// <param name="HasEvent">Whether a room event is running.</param>
/// <param name="EventName">The title of the running event, meaningful only when <paramref name="HasEvent"/> is <see langword="true"/>.</param>
/// <param name="EventDescription">The description of the running event, meaningful only when <paramref name="HasEvent"/> is <see langword="true"/>.</param>
/// <param name="EventMinutesRemaining">The number of minutes before the running event ends.</param>
/// <param name="ShowOwner">Whether the navigator shows the owner's name.</param>
/// <param name="AllowPets">Whether visitors may bring pets into the room.</param>
/// <param name="DisplayRoomEntryAd">Whether the client shows an entry advertisement for the room.</param>
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

    /// <summary>
    /// Gets the room's search tags.
    /// </summary>
    /// <remarks>
    /// The setter stores a read-only copy and throws <see cref="ArgumentNullException"/> when the list or
    /// one of its values is <see langword="null"/>.
    /// </remarks>
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

/// <summary>
/// Represents the result of loading the navigator data of a room.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomDataGet"/>.
/// </remarks>
/// <param name="ReceivedAtUtc">The time the matching response was received.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the request ran in.</param>
/// <param name="RequestedRoomId">The id of the room that was requested.</param>
/// <param name="MessagesDispatched">The number of request messages sent.</param>
/// <param name="Room">The navigator data of the room.</param>
public sealed record RoomDataReadResult(
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    Id RequestedRoomId,
    int MessagesDispatched,
    RoomDataView Room);

/// <summary>
/// Represents a request to load the users holding rights in a room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomRightsList"/>. One request is sent and the call completes
/// with the first rights list for <paramref name="RoomId"/>. The hotel answers only for rooms the
/// local user controls.
/// </remarks>
/// <param name="RoomId">The id of the room. Must be positive and fit in a 32-bit integer.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the matching response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record RoomRightsReadRequest(
    Id RoomId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the users holding rights in a room.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomRightsList"/>.
/// </remarks>
/// <param name="ReceivedAtUtc">The time the matching response was received.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the request ran in.</param>
/// <param name="RoomId">The id of the room the rights list belongs to.</param>
/// <param name="MessagesDispatched">The number of request messages sent.</param>
/// <param name="Users">The id and name of each user holding rights in the room.</param>
public sealed record RoomRightsReadResult(
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    Id RoomId,
    int MessagesDispatched,
    IReadOnlyList<IdName> Users)
{
    private IReadOnlyList<IdName> users = Freeze(Users);

    /// <summary>
    /// Gets the id and name of each user holding rights in the room.
    /// </summary>
    /// <remarks>
    /// The setter stores a read-only copy and throws <see cref="ArgumentNullException"/> when the list is
    /// <see langword="null"/>.
    /// </remarks>
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

/// <summary>
/// Represents a request to load the statistics of one pet from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.PetsInfoGet"/>. One request is sent and the call completes
/// with the first pet info response for <paramref name="PetId"/>.
/// </remarks>
/// <param name="PetId">The id of the pet. Must be positive and fit in a 32-bit integer.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the matching response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record PetInfoReadRequest(
    Id PetId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the statistics of a pet.
/// </summary>
/// <remarks>
/// The pet info message does not carry the pet type, only the breed variant within it.
/// </remarks>
/// <param name="Id">The id of the pet.</param>
/// <param name="Name">The pet's name.</param>
/// <param name="Level">The pet's current level.</param>
/// <param name="MaxLevel">The highest level the pet can reach.</param>
/// <param name="Experience">The experience accumulated toward the next level.</param>
/// <param name="MaxExperience">The experience needed for the next level.</param>
/// <param name="Energy">The pet's current energy.</param>
/// <param name="MaxEnergy">The energy cap.</param>
/// <param name="Happiness">The pet's current nutrition, which the client calls <c>nutrition</c>.</param>
/// <param name="MaxHappiness">The nutrition cap, which the client calls <c>maxNutrition</c>.</param>
/// <param name="Scratches">The respect the pet received, which the client calls <c>respect</c>.</param>
/// <param name="OwnerId">The id of the pet's owner.</param>
/// <param name="Age">The pet's age in days.</param>
/// <param name="OwnerName">The name of the pet's owner.</param>
/// <param name="BreedId">
/// The breed variant within the pet type. Pet types without variants, such as the monsterplant, report 0.
/// </param>
/// <param name="HasFreeSaddle">Whether the pet's saddle is unlocked without a purchase.</param>
/// <param name="IsRiding">Whether a user is riding the pet.</param>
/// <param name="SkillThresholds">The experience thresholds at which the pet unlocks its skills.</param>
/// <param name="AccessRights">The hotel's access rights code for who may command the pet, passed through unchanged.</param>
/// <param name="CanBreed">Whether the pet may be bred now.</param>
/// <param name="CanHarvest">Whether the pet may be harvested now.</param>
/// <param name="CanRevive">Whether the pet is dead and may be revived.</param>
/// <param name="RarityLevel">The pet's rarity level as sent by the hotel.</param>
/// <param name="MaxWellbeingSeconds">The full duration of the wellbeing timer in seconds.</param>
/// <param name="RemainingWellbeingSeconds">The seconds of wellbeing left.</param>
/// <param name="RemainingGrowingSeconds">The seconds left in the current growth stage.</param>
/// <param name="HasBreedingPermission">Whether the local user may breed the pet.</param>
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

    /// <summary>
    /// Gets the experience thresholds at which the pet unlocks its skills.
    /// </summary>
    /// <remarks>
    /// The setter stores a read-only copy and throws <see cref="ArgumentNullException"/> when the list is
    /// <see langword="null"/>.
    /// </remarks>
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

/// <summary>
/// Represents the result of loading the statistics of a pet.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.PetsInfoGet"/>.
/// </remarks>
/// <param name="ReceivedAtUtc">The time the matching response was received.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the request ran in.</param>
/// <param name="RequestedPetId">The id of the pet that was requested.</param>
/// <param name="MessagesDispatched">The number of request messages sent.</param>
/// <param name="Pet">The statistics of the pet.</param>
public sealed record PetInfoReadResult(
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    Id RequestedPetId,
    int MessagesDispatched,
    PetInfoView Pet);

/// <summary>
/// Represents a request to load the color and text of one sticky note from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomStickyGet"/>. One request is sent and the call completes
/// with the first sticky data response for <paramref name="ItemId"/>.
/// </remarks>
/// <param name="ItemId">The id of the sticky note wall item. Must be positive and fit in a 32-bit integer.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the matching response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record StickyReadRequest(
    Id ItemId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the color and text of a sticky note.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomStickyGet"/>.
/// </remarks>
/// <param name="ReceivedAtUtc">The time the matching response was received.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the request ran in.</param>
/// <param name="ItemId">The id of the sticky note wall item.</param>
/// <param name="MessagesDispatched">The number of request messages sent.</param>
/// <param name="Color">The note color as a hex string, empty when the hotel sent none.</param>
/// <param name="Text">The note text.</param>
public sealed record StickyReadResult(
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    Id ItemId,
    int MessagesDispatched,
    string Color,
    string Text);

/// <summary>
/// Represents a request to load the rooms the local user may advertise with a room event.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.CatalogRoomAdInfoGet"/>. One request is sent and the call
/// completes with the first room advertisement info received after it.
/// </remarks>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record RoomAdInfoReadRequest(
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a room the local user may advertise with a room event.
/// </summary>
/// <param name="RoomId">The id of the room.</param>
/// <param name="RoomName">The room name.</param>
/// <param name="HasControllers">Whether users besides the owner hold rights in the room.</param>
public sealed record RoomAdRoomView(
    Id RoomId,
    string RoomName,
    bool HasControllers);

/// <summary>
/// Represents the rooms the local user may advertise with a room event.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.CatalogRoomAdInfoGet"/>.
/// </remarks>
/// <param name="ReceivedAtUtc">The time the response was received.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the request ran in.</param>
/// <param name="MessagesDispatched">The number of request messages sent.</param>
/// <param name="IsVip">Whether the account holds the membership that extends a room event.</param>
/// <param name="Rooms">The rooms that may be advertised.</param>
public sealed record RoomAdInfoReadResult(
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    int MessagesDispatched,
    bool IsVip,
    IReadOnlyList<RoomAdRoomView> Rooms)
{
    private IReadOnlyList<RoomAdRoomView> rooms = Freeze(Rooms);

    /// <summary>
    /// Gets the rooms that may be advertised.
    /// </summary>
    /// <remarks>
    /// The setter stores a read-only copy and throws <see cref="ArgumentNullException"/> when the list, one
    /// of its rooms or a room name is <see langword="null"/>.
    /// </remarks>
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
