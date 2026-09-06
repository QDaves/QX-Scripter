using Qx.Messages;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game;

/// <summary>How long somebody is barred for.</summary>
public enum BanLength
{
    Hour,
    Day,
    Permanent
}

/// <summary>
/// The things you do to a person rather than to the room.
/// </summary>
public sealed class RoomPeopleActions : GameStateManager
{
    /// <summary>The room, for the id the moderation messages carry.</summary>
    public RoomManager? Room { get; set; }
    internal Func<IRemotePeopleOperations?>? RemotePeopleOperations { get; set; }

    protected override void OnAttach()
    {
    }

    /// <summary>
    /// Makes somebody's own bubble appear over their head, so you can see where they are.
    /// </summary>
    /// <remarks>
    /// Written to the client and never to the hotel, so nobody is whispered at and nothing is said
    /// in the room. The index is what the client uses to place a bubble, which is why this needs
    /// the room's numbering rather than the account id.
    /// </remarks>
    public void Find(Avatar avatar, string text = "(here)")
    {
        ArgumentNullException.ThrowIfNull(avatar);

        SendToClient(
            MessageContracts.Room.Chat.Whisper,
            new AvatarChat(avatar.Index, text, 0, 0, [], 0, ChatType.Whisper));
    }

    /// <summary>Opens somebody's profile inside the game client.</summary>
    public void OpenProfile(Id userId) =>
        (RemotePeopleOperations?.Invoke() ??
            throw new InvalidOperationException("Remote-people operations are unavailable."))
            .OpenProfile(new RemoteProfileOpenRequest(userId));

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

    public void RespectPet(Id pet_id) =>
        SendMessage(
            MessageContracts.Room.Occupants.Pet.RespectRequest,
            new RespectPetRequest(pet_id));

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

    public void MountPet(Id pet_id, bool mount) =>
        SendMessage(
            MessageContracts.Room.Occupants.Pet.MountRequest,
            new MountPetRequest(pet_id, mount));

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

    public void RemovePet(Id pet_id) =>
        SendMessage(
            MessageContracts.Room.Occupants.Pet.RemoveRequest,
            new RemovePetFromRoomRequest(pet_id));

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

    public void RemoveBot(Id bot_id) =>
        SendMessage(
            MessageContracts.Room.Occupants.Bot.RemoveRequest,
            new RemoveBotFromFlat(bot_id));

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

    public void GiveRights(Id user_id) =>
        SendMessage(
            MessageContracts.Room.Authority.ControllerGrantRequest,
            new GiveRoomRightsRequest(user_id));

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
