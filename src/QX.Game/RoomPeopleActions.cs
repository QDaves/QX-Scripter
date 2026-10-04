using Qx.Messages;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game;

/// <summary>Specifies how long a room ban lasts.</summary>
public enum BanLength
{
    /// <summary>A ban that lasts one hour.</summary>
    Hour,
    /// <summary>A ban that lasts one day.</summary>
    Day,
    /// <summary>A ban that does not expire.</summary>
    Permanent
}

/// <summary>
/// Provides actions that target a person, pet or bot in the room rather than the room itself.
/// </summary>
/// <remarks>
/// The type holds no game state. Each action sends one message and does not wait for a response.
/// </remarks>
public sealed class RoomPeopleActions : GameStateManager
{
    /// <summary>
    /// Gets or sets the room manager used to check that the room has not changed before a message is sent.
    /// </summary>
    public RoomManager? Room { get; set; }
    internal Func<IRemotePeopleOperations?>? RemotePeopleOperations { get; set; }

    /// <inheritdoc/>
    protected override void OnAttach()
    {
    }

    /// <summary>
    /// Shows a whisper bubble over an avatar's head in the local client to locate it.
    /// </summary>
    /// <remarks>
    /// The message is written to the client only and never sent to the hotel, so nobody receives a
    /// whisper and nothing is said in the room. The bubble is placed by the avatar's room index.
    /// </remarks>
    /// <param name="avatar">The avatar to mark.</param>
    /// <param name="text">The text shown in the bubble.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="avatar"/> is <see langword="null"/>.</exception>
    public void Find(Avatar avatar, string text = "(here)")
    {
        ArgumentNullException.ThrowIfNull(avatar);

        SendToClient(
            MessageContracts.Room.Chat.Whisper,
            new AvatarChat(avatar.Index, text, 0, 0, [], 0, ChatType.Whisper));
    }

    /// <summary>Opens a user's profile in the game client.</summary>
    /// <param name="userId">The id of the user.</param>
    /// <exception cref="InvalidOperationException">Thrown when the remote people operations are not available.</exception>
    public void OpenProfile(Id userId) =>
        (RemotePeopleOperations?.Invoke() ??
            throw new InvalidOperationException("Remote-people operations are unavailable."))
            .OpenProfile(new RemoteProfileOpenRequest(userId));

    /// <summary>Sends a respect to a user.</summary>
    /// <param name="userId">The id of the user.</param>
    public void Respect(Id userId) =>
        SendMessage(
            MessageContracts.Room.Occupants.RespectRequest,
            new RespectUserRequest(userId));

    internal void Respect(
        Id user_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendRoomMessage(
            MessageContracts.Room.Occupants.RespectRequest,
            new RespectUserRequest(user_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Sends a respect to a pet.</summary>
    /// <param name="petId">The id of the pet.</param>
    public void RespectPet(Id petId) =>
        SendMessage(
            MessageContracts.Room.Occupants.Pet.RespectRequest,
            new RespectPetRequest(petId));

    internal void RespectPet(
        Id pet_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendRoomMessage(
            MessageContracts.Room.Occupants.Pet.RespectRequest,
            new RespectPetRequest(pet_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Mounts or dismounts a rideable pet.</summary>
    /// <param name="petId">The id of the pet.</param>
    /// <param name="mount"><see langword="true"/> to mount the pet, <see langword="false"/> to dismount it.</param>
    public void MountPet(Id petId, bool mount) =>
        SendMessage(
            MessageContracts.Room.Occupants.Pet.MountRequest,
            new MountPetRequest(petId, mount));

    internal void MountPet(
        Id pet_id,
        bool mount,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendRoomMessage(
            MessageContracts.Room.Occupants.Pet.MountRequest,
            new MountPetRequest(pet_id, mount),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Removes a pet from the room.</summary>
    /// <param name="petId">The id of the pet.</param>
    public void RemovePet(Id petId) =>
        SendMessage(
            MessageContracts.Room.Occupants.Pet.RemoveRequest,
            new RemovePetFromRoomRequest(petId));

    internal void RemovePet(
        Id pet_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendRoomMessage(
            MessageContracts.Room.Occupants.Pet.RemoveRequest,
            new RemovePetFromRoomRequest(pet_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Removes a bot from the room.</summary>
    /// <param name="botId">The id of the bot.</param>
    public void RemoveBot(Id botId) =>
        SendMessage(
            MessageContracts.Room.Occupants.Bot.RemoveRequest,
            new RemoveBotFromFlat(botId));

    internal void RemoveBot(
        Id bot_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendRoomMessage(
            MessageContracts.Room.Occupants.Bot.RemoveRequest,
            new RemoveBotFromFlat(bot_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    /// <summary>Gives a user rights in the current room.</summary>
    /// <param name="userId">The id of the user.</param>
    public void GiveRights(Id userId) =>
        SendMessage(
            MessageContracts.Room.Authority.ControllerGrantRequest,
            new GiveRoomRightsRequest(userId));

    internal void GiveRights(
        Id user_id,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token) =>
        SendRoomMessage(
            MessageContracts.Room.Authority.ControllerGrantRequest,
            new GiveRoomRightsRequest(user_id),
            expected_session,
            expected_room_generation,
            cancellation_token);

    private void SendRoomMessage<T>(
        MessageContract<T> contract,
        T message,
        Session expected_session,
        long expected_room_generation,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        RoomManager room = Room
            ?? throw new InvalidOperationException("The room people manager is not attached.");
        void ValidateDispatch() => room.Capture(state =>
        {
            if (state.Generation != expected_room_generation)
                throw new InvalidOperationException("The room changed before dispatch.");
            cancellation_token.ThrowIfCancellationRequested();
            return true;
        });
        SendMessage(contract, message, expected_session, cancellation_token, ValidateDispatch);
    }

}
