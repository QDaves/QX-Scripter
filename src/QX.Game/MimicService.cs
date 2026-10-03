using Qx.Game.Protocol;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

/// <summary>
/// Specifies which parts of another avatar to copy.
/// </summary>
/// <remarks>
/// Values can be combined. <see cref="MimicService.Copy"/> acts on the appearance parts,
/// <see cref="MimicParts.Typing"/> and <see cref="MimicParts.Walk"/>. <see cref="MimicParts.Talk"/>,
/// <see cref="MimicParts.Shout"/> and <see cref="MimicParts.Expression"/> are not copied by it;
/// callers repeat them with <see cref="MimicService.Say"/> and <see cref="MimicService.Express"/>.
/// </remarks>
[Flags]
public enum MimicParts
{
    /// <summary>No parts.</summary>
    None = 0,
    /// <summary>The avatar's figure.</summary>
    Figure = 1 << 0,
    /// <summary>The avatar's motto.</summary>
    Motto = 1 << 1,
    /// <summary>The avatar's dance.</summary>
    Dance = 1 << 2,
    /// <summary>The direction the avatar faces.</summary>
    Direction = 1 << 3,
    /// <summary>The sign the avatar is holding up.</summary>
    Sign = 1 << 4,
    /// <summary>The avatar's effect.</summary>
    Effect = 1 << 5,
    /// <summary>The avatar's typing indicator.</summary>
    Typing = 1 << 6,
    /// <summary>The tile the avatar stands on.</summary>
    Walk = 1 << 7,
    /// <summary>The avatar's talk messages.</summary>
    Talk = 1 << 8,
    /// <summary>The avatar's shout messages.</summary>
    Shout = 1 << 9,
    /// <summary>The avatar's expressions, such as a wave or a laugh.</summary>
    Expression = 1 << 10,

    /// <summary>The figure, motto, dance, direction, sign and effect.</summary>
    Appearance = Figure | Motto | Dance | Direction | Sign | Effect,

    /// <summary>The typing indicator, position, talk, shout and expressions.</summary>
    Behaviour = Typing | Walk | Talk | Shout | Expression,

    /// <summary>Every part.</summary>
    All = Appearance | Behaviour
}

/// <summary>
/// Provides methods that copy another avatar's appearance and behavior onto the local user's avatar.
/// </summary>
/// <param name="game">The game state that avatars are read from and requests are sent through.</param>
/// <remarks>
/// Each call copies once. Nothing subscribes to the target or runs a timer, so continuous mimicry
/// calls again whenever the target changes.
/// </remarks>
public sealed class MimicService(GameState game)
{
    private readonly GameState _game =
        game ?? throw new ArgumentNullException(nameof(game));

    /// <summary>
    /// Copies parts of an avatar onto the local user's avatar.
    /// </summary>
    /// <param name="target">The avatar to copy.</param>
    /// <param name="parts">The parts to copy.</param>
    /// <returns>The parts that were sent.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the profile or room avatar operations are not bound.</exception>
    /// <remarks>
    /// <para>
    /// A part the target has no value for is skipped instead of being sent as a default: an empty
    /// figure or motto, a dance or effect of 0, or no sign. Sending those would reset the local
    /// user's avatar instead of copying the target.
    /// </para>
    /// <para>
    /// The direction is copied by looking at the tile one step ahead of the target in the direction it
    /// faces. <see cref="MimicParts.Walk"/> walks to the target's tile, and
    /// <see cref="MimicParts.Typing"/> sends the target's typing state. The figure is sent with the
    /// female gender when the target is a female user and with the male gender otherwise.
    /// </para>
    /// </remarks>
    public MimicParts Copy(Avatar target, MimicParts parts = MimicParts.Appearance)
    {
        ArgumentNullException.ThrowIfNull(target);
        MimicParts done = MimicParts.None;

        if (parts.HasFlag(MimicParts.Figure) && target.Figure is { Length: > 0 } figure)
        {
            RequireProfileOperations().UpdateFigure(Gender(target), figure);
            done |= MimicParts.Figure;
        }
        if (parts.HasFlag(MimicParts.Motto) && target.Motto is { Length: > 0 } motto)
        {
            RequireProfileOperations().UpdateMotto(motto);
            done |= MimicParts.Motto;
        }
        if (parts.HasFlag(MimicParts.Dance) && target.Dance > 0)
        {
            RequireRoomAvatarOperations().Dance(
                new Application.RoomAvatarDanceRequest(target.Dance));
            done |= MimicParts.Dance;
        }
        if (parts.HasFlag(MimicParts.Effect) && target.Effect > 0)
        {
            RequireRoomAvatarOperations().Effect(
                new Application.RoomAvatarEffectRequest(target.Effect));
            done |= MimicParts.Effect;
        }
        if (parts.HasFlag(MimicParts.Sign) && Sign(target) is int sign)
        {
            RequireRoomAvatarOperations().Sign(
                new Application.RoomAvatarSignRequest(sign));
            done |= MimicParts.Sign;
        }
        if (parts.HasFlag(MimicParts.Direction))
        {
            // Facing is sent as a tile to look at rather than as an angle, so the target's own tile
            // stepped one square along the way they face is what makes you face the same way.
            (int x, int y) = Ahead(target);
            RequireRoomAvatarOperations().Look(new Application.RoomAvatarLookRequest(x, y));
            done |= MimicParts.Direction;
        }
        if (parts.HasFlag(MimicParts.Typing))
        {
            RequireRoomAvatarOperations().Typing(
                new Application.RoomAvatarTypingRequest(target.IsTyping));
            done |= MimicParts.Typing;
        }
        if (parts.HasFlag(MimicParts.Walk))
        {
            RequireRoomAvatarOperations().Walk(
                new Application.RoomAvatarWalkRequest(target.Location.X, target.Location.Y));
            done |= MimicParts.Walk;
        }
        return done;
    }

    /// <summary>Repeats a chat message as talk or shout, matching the way it was said.</summary>
    /// <param name="message">The message to repeat.</param>
    /// <param name="type">The type of chat the message was sent as.</param>
    /// <param name="bubble">The chat bubble style.</param>
    /// <returns>
    /// <see langword="true"/> when the message was sent; <see langword="false"/> when it is empty or
    /// <paramref name="type"/> is neither <see cref="ChatType.Talk"/> nor <see cref="ChatType.Shout"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the room chat operations are not bound.</exception>
    /// <remarks>
    /// Whispers are not repeated, because they were addressed to one user.
    /// </remarks>
    public bool Say(string message, ChatType type, int bubble = 0)
    {
        if (message is not { Length: > 0 })
            return false;
        switch (type)
        {
            case ChatType.Talk:
                RequireRoomChatOperations().Talk(
                    new Application.RoomChatTalkRequest(message, bubble));
                return true;
            case ChatType.Shout:
                RequireRoomChatOperations().Shout(
                    new Application.RoomChatShoutRequest(message, bubble));
                return true;
            default:
                return false;
        }
    }

    /// <summary>Performs an avatar expression such as a wave, a laugh or going idle.</summary>
    /// <param name="expression">The id of the expression.</param>
    /// <exception cref="InvalidOperationException">Thrown when the room avatar operations are not bound.</exception>
    public void Express(int expression) =>
        RequireRoomAvatarOperations().Expression(
            new Application.RoomAvatarExpressionRequest(expression));

    /// <summary>Sends a request to follow a friend into the room they are in.</summary>
    /// <param name="friendId">The id of the friend.</param>
    /// <exception cref="InvalidOperationException">Thrown when the friend operations are not bound.</exception>
    public void Follow(Id friendId) =>
        RequireFriendOperations().Follow(
            new Application.FriendFollowRequest(friendId),
            default);

    /// <summary>
    /// Finds an avatar in the current room by name.
    /// </summary>
    /// <param name="name">The name of the avatar.</param>
    /// <returns>
    /// The first avatar with a matching name, or <see langword="null"/> when none matches or
    /// <paramref name="name"/> is empty.
    /// </returns>
    /// <remarks>
    /// The name is compared without regard to case.
    /// </remarks>
    public Avatar? Find(string name) =>
        name is { Length: > 0 }
            ? _game.Room.Avatars.FirstOrDefault(avatar =>
                string.Equals(avatar.Name, name, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <summary>
    /// Gets the gender code the target's figure is sent with.
    /// </summary>
    /// <param name="target">The avatar whose figure is copied.</param>
    /// <remarks>
    /// F for a female user and M for every other avatar.
    /// </remarks>
    private static string Gender(Avatar target) =>
        target is User user && user.Gender is Model.Gender.Female ? "F" : "M";

    private Application.IProfileOperations RequireProfileOperations() =>
        _game.ProfileOperations
        ?? throw new InvalidOperationException("Profile operations are not bound.");

    private Application.IRoomChatOperations RequireRoomChatOperations() =>
        _game.RoomChatOperations
        ?? throw new InvalidOperationException("Room-chat operations are not bound.");

    private Application.IRoomAvatarOperations RequireRoomAvatarOperations() =>
        _game.RoomAvatarOperations
        ?? throw new InvalidOperationException("Room-avatar operations are not bound.");

    private Application.IFriendOperations RequireFriendOperations() =>
        _game.FriendOperations
        ?? throw new InvalidOperationException("Friend operations are not bound.");

    private static int? Sign(Avatar target) =>
        target.CurrentUpdate?.Sign is int sign and > 0 ? sign : null;

    private static (int X, int Y) Ahead(Avatar target)
    {
        (int dx, int dy) = target.Direction switch
        {
            0 => (0, -1),
            1 => (1, -1),
            2 => (1, 0),
            3 => (1, 1),
            4 => (0, 1),
            5 => (-1, 1),
            6 => (-1, 0),
            _ => (-1, -1)
        };
        return (target.Location.X + dx, target.Location.Y + dy);
    }
}
