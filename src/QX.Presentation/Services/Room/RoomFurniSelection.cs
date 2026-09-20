using Qx.Game;
using Qx.Model;

namespace Qx.Presentation.Services.Room;

public static class RoomFurniSelection
{
    public static IReadOnlyList<Furni> Resolve(GameState game, IReadOnlyList<Furni> chosen)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(chosen);
        if (chosen.Count == 0)
            return [];
        return game.Room.Capture(room =>
        {
            var live = new List<Furni>(chosen.Count);
            foreach (Furni item in chosen)
            {
                Furni? current = item.Type switch
                {
                    ItemType.Floor => room.FloorItem(item.Id),
                    ItemType.Wall => room.WallItem(item.Id),
                    _ => null
                };
                if (current is not null)
                    live.Add(current);
            }
            return (IReadOnlyList<Furni>)live;
        });
    }
}
