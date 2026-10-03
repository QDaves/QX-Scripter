using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to give a respect to a user in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPeopleRespect"/>.
/// </remarks>
/// <param name="UserId">The id of the user.</param>
public sealed record RoomUserRespectRequest(Id UserId);

/// <summary>
/// Represents a request to give a user rights in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPeopleRightsGrant"/>.
/// </remarks>
/// <param name="UserId">The id of the user.</param>
public sealed record RoomRightsGrantRequest(Id UserId);

/// <summary>
/// Represents a request to give a respect to a pet in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPetRespect"/>.
/// </remarks>
/// <param name="PetId">The id of the pet.</param>
public sealed record RoomPetRespectRequest(Id PetId);

/// <summary>
/// Represents a request to mount or dismount a rideable pet in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPetMountSet"/>.
/// </remarks>
/// <param name="PetId">The id of the pet.</param>
/// <param name="Mount">Whether to mount the pet instead of dismounting it.</param>
public sealed record RoomPetMountRequest(Id PetId, bool Mount = true);

/// <summary>
/// Represents a request to remove a pet from the current room and return it to its owner's inventory.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPetRemove"/>.
/// </remarks>
/// <param name="PetId">The id of the pet.</param>
public sealed record RoomPetRemoveRequest(Id PetId);

/// <summary>
/// Represents a request to remove a bot from the current room and return it to its owner's inventory.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomBotRemove"/>.
/// </remarks>
/// <param name="BotId">The id of the bot.</param>
public sealed record RoomBotRemoveRequest(Id BotId);

/// <summary>
/// Represents the result of sending a room people message to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomPeopleRespect"/>, <see cref="ApplicationMemberIds.RoomPeopleRightsGrant"/>,
/// <see cref="ApplicationMemberIds.RoomPetRespect"/>, <see cref="ApplicationMemberIds.RoomPetMountSet"/>,
/// <see cref="ApplicationMemberIds.RoomPetRemove"/> and <see cref="ApplicationMemberIds.RoomBotRemove"/>. The message
/// is only sent while the room generation is unchanged, and the hotel's response is not awaited.
/// </remarks>
/// <param name="RoomId">The id of the current room, or <see langword="null"/> when no room is loaded.</param>
/// <param name="RoomGeneration">The room state generation the message was sent in.</param>
/// <param name="Dispatched">Whether the message was sent.</param>
/// <param name="ServerConfirmed">Whether the hotel confirmed the action, which is always <see langword="false"/>.</param>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
public sealed record RoomPeopleDispatchResult(
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
