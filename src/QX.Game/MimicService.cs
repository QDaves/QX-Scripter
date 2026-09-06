using Qx.Game.Protocol;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

/// <summary>
/// Which parts of someone else are worth copying.
/// </summary>
/// <remarks>
/// Named individually rather than as one switch because they are wanted separately: copying a look
/// is a wardrobe act, copying a dance and a direction is a joke, and copying chat is something to
/// turn on deliberately and off quickly.
/// </remarks>
[Flags]
public enum MimicParts
{
    None = 0,
    Figure = 1 << 0,
    Motto = 1 << 1,
    Dance = 1 << 2,
    Direction = 1 << 3,
    Sign = 1 << 4,
    Effect = 1 << 5,
    Typing = 1 << 6,
    Walk = 1 << 7,
    Talk = 1 << 8,
    Shout = 1 << 9,
    Expression = 1 << 10,

    /// <summary>Everything a person carries about with them, without following them around.</summary>
    Appearance = Figure | Motto | Dance | Direction | Sign | Effect,

    /// <summary>What they are doing rather than what they look like.</summary>
    Behaviour = Typing | Walk | Talk | Shout | Expression,

    All = Appearance | Behaviour
}

/// <summary>
/// Copies what another avatar is doing onto your own.
/// </summary>
/// <remarks>
/// <para>
/// Copying is one-way and one-shot. Nothing here subscribes to the target or holds a timer: a caller
/// that wants continuous mimicry calls again when the room says the target changed, which keeps the
/// decision about how eager to be with the caller rather than buried in here.
/// </para>
/// </remarks>
public sealed class MimicService(GameState game)
{
    private readonly GameState _game =
        game ?? throw new ArgumentNullException(nameof(game));

    /// <summary>
    /// Copies the named parts of an avatar onto your own.
    /// </summary>
    /// <returns>What was actually sent, which is what the avatar had to copy.</returns>
    /// <remarks>
    /// A part the target has nothing for is skipped rather than sent as a default. Sending an empty
    /// figure or a dance of zero would not copy them, it would reset you — which is the opposite of
    /// what was asked and is not obvious afterwards.
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

    /// <summary>Says what they said, in the way they said it.</summary>
    /// <remarks>
    /// A whisper is deliberately not repeated. It was addressed to someone, and echoing it into the
    /// room is not mimicry but disclosure.
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

    /// <summary>Performs an expression: a wave, a laugh, an idle.</summary>
    public void Express(int expression) =>
        RequireRoomAvatarOperations().Expression(
            new Application.RoomAvatarExpressionRequest(expression));

    /// <summary>Walks after a friend, which the hotel does on its own once told who.</summary>
    public void Follow(Id friend_id) =>
        RequireFriendOperations().Follow(
            new Application.FriendFollowRequest(friend_id),
            default);

    /// <summary>
    /// The avatar in the room going by that name, if one is.
    /// </summary>
    /// <remarks>
    /// Matched without regard to case because a name typed by hand rarely matches the capitals the
    /// hotel holds, and no two people in a room share a name.
    /// </remarks>
    public Avatar? Find(string name) =>
        name is { Length: > 0 }
            ? _game.Room.Avatars.FirstOrDefault(avatar =>
                string.Equals(avatar.Name, name, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <summary>
    /// Which figure set the look belongs to.
    /// </summary>
    /// <remarks>
    /// Read off the figure itself rather than from a separate field, because the message carries the
    /// two together and a mismatch between them is what makes a copied look come back wrong.
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
