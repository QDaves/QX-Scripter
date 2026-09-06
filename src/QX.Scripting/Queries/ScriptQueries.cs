using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public sealed class ScriptQueries
{
    private readonly GameState _game;
    private readonly IApplicationRuntime _application;

    public ScriptQueries(GameState game, IApplicationRuntime application)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(application);
        _game = game;
        _application = application;
    }

    public AvatarQuery Avatars =>
        _game.Room.Capture(room => new AvatarQuery(room.Avatars));

    public FloorItemQuery FloorItems =>
        _game.Room.Capture(room => new FloorItemQuery(room.FloorItems, _game.GameData.Furni));

    public WallItemQuery WallItems =>
        _game.Room.Capture(room => new WallItemQuery(room.WallItems, _game.GameData.Furni));

    public InventoryItemQuery InventoryItems =>
        new(ScriptGlobals.ReadInventoryItems(_application), _game.GameData.Furni);

    public InventoryPetQuery InventoryPets =>
        new(ScriptGlobals.ReadInventoryPetModels(_application));

    public BadgeQuery OwnedBadges =>
        new(_game.Badges.OwnedBadges);

    public FriendQuery Friends =>
        new(_game.Friends.Friends);

    public RoomDataQuery CurrentRoom =>
        new(_game.Room.Data is { } room ? [room] : []);

    public AchievementQuery Achievements =>
        new(_game.Achievements.All);

    public AvatarQuery From(IEnumerable<Avatar> avatars) =>
        new(avatars);

    public FloorItemQuery From(IEnumerable<FloorItem> items) =>
        new(items, _game.GameData.Furni);

    public WallItemQuery From(IEnumerable<WallItem> items) =>
        new(items, _game.GameData.Furni);

    public InventoryItemQuery From(IEnumerable<InventoryItem> items) =>
        new(items, _game.GameData.Furni);

    public InventoryPetQuery From(IEnumerable<InventoryPet> pets) =>
        new(pets);

    public BadgeQuery From(IEnumerable<OwnedBadge> badges) =>
        new(badges);

    public SelectedBadgeQuery From(IEnumerable<SelectedBadge> badges) =>
        new(badges);

    public FriendQuery From(IEnumerable<Friend> friends) =>
        new(friends);

    public RoomDataQuery From(IEnumerable<RoomData> rooms) =>
        new(rooms);

    public AchievementQuery From(IEnumerable<Achievement> achievements) =>
        new(achievements);
}

public static class GameQueryExtensions
{
    public static AvatarQuery Query(this IEnumerable<Avatar> avatars) =>
        new(avatars);

    public static FloorItemQuery Query(this IEnumerable<FloorItem> items, GameState game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return new FloorItemQuery(items, game.GameData.Furni);
    }

    public static WallItemQuery Query(this IEnumerable<WallItem> items, GameState game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return new WallItemQuery(items, game.GameData.Furni);
    }

    public static InventoryItemQuery Query(this IEnumerable<InventoryItem> items, GameState game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return new InventoryItemQuery(items, game.GameData.Furni);
    }

    public static InventoryPetQuery Query(this IEnumerable<InventoryPet> pets) =>
        new(pets);

    public static BadgeQuery Query(this IEnumerable<OwnedBadge> badges) =>
        new(badges);

    public static SelectedBadgeQuery Query(this IEnumerable<SelectedBadge> badges) =>
        new(badges);

    public static FriendQuery Query(this IEnumerable<Friend> friends) =>
        new(friends);

    public static RoomDataQuery Query(this IEnumerable<RoomData> rooms) =>
        new(rooms);

    public static AchievementQuery Query(this IEnumerable<Achievement> achievements) =>
        new(achievements);
}
