using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

/// <summary>Represents the decoration, layout and chat rules of the current room.</summary>
/// <remarks>
/// Every member is <see langword="null"/>, and the property map empty, until the message that
/// carries it has arrived.
/// </remarks>
/// <param name="EntryTile">The door tile and the direction arriving avatars face, or <see langword="null"/> before it arrives.</param>
/// <param name="Properties">
/// Every room property keyed exactly as the hotel sends it, for example <c>floor</c>,
/// <c>wallpaper</c>, <c>landscape</c> and <c>landscapeanim</c>. A copy, not a live view.
/// </param>
/// <param name="Floor">The <c>floor</c> property, which is the floor pattern identifier.</param>
/// <param name="Wallpaper">The <c>wallpaper</c> property, which is the wall pattern identifier.</param>
/// <param name="Landscape">The <c>landscape</c> property, which is the window backdrop identifier.</param>
/// <param name="AnimatedLandscape">The <c>landscapeanim</c> property, which is the animated backdrop identifier.</param>
/// <param name="Visualization">Whether the walls are hidden and how thick the walls and floor are drawn, or <see langword="null"/> before it arrives.</param>
/// <param name="Chat">
/// A copy of the room's chat rules, or <see langword="null"/> before the room result or a chat
/// settings message arrives.
/// </param>
public sealed record RoomEnvironmentState(
    RoomEntryTile? EntryTile,
    IReadOnlyDictionary<string, string> Properties,
    string? Floor,
    string? Wallpaper,
    string? Landscape,
    string? AnimatedLandscape,
    RoomVisualizationSettings? Visualization,
    RoomChatSettings? Chat);
