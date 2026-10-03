using Qx.Model;
using Qx.Model.Wired;

namespace Qx.Game.Application;

/// <summary>Reads one area-hide editor from retained room furniture data.</summary>
/// <param name="FurniId">The furniture identifier.</param>
public sealed record WiredAreaHideGetRequest(Id FurniId);

/// <summary>The local area-hide editor and room identity it was read in.</summary>
/// <param name="RoomId">The current room ID.</param>
/// <param name="RoomGeneration">The generation required for subsequent edits.</param>
/// <param name="Settings">The furniture's complete settings.</param>
/// <param name="Region">The current floor-plan region when known.</param>
public sealed record WiredAreaHideView(Id RoomId, long RoomGeneration, WiredAreaHideSettings Settings, AreaHideData? Region);

/// <summary>Changes area-hide settings in the reviewed room while the effect is off.</summary>
/// <param name="Update">The complete replacement editor settings.</param>
/// <param name="ExpectedRoomGeneration">The room generation returned by the local read.</param>
public sealed record WiredAreaHideSetRequest(SetAreaHideData Update, long ExpectedRoomGeneration);

/// <summary>Toggles an area-hide furniture item using the client's default use parameter.</summary>
/// <param name="FurniId">The furniture identity.</param>
/// <param name="ExpectedRoomGeneration">The room generation returned by the local read.</param>
public sealed record WiredAreaHideToggleRequest(Id FurniId, long ExpectedRoomGeneration);
