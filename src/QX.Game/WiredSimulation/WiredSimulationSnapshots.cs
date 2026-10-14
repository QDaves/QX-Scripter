using System.Globalization;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

internal static class WiredSimulationSnapshots
{
    internal static bool supports(WiredSimulationNode node) => node.Category switch
    {
        WiredFormCategory.Action => node.Code is 3 or 55,
        WiredFormCategory.Condition => node.Code is 0 or 13,
        WiredFormCategory.Trigger => node.Code == 20,
        _ => false
    };

    internal static IReadOnlyList<WiredSimulationSavedFurniture>? read(WiredSimulationNode node) => node.SavedSnapshot ??
        node.SavedFurniture?.Select(item => new WiredSimulationSavedFurniture(item.Id, item.State, item.Direction, item.X, item.Y, item.Z)).ToArray();

    internal static IReadOnlyList<WiredSimulationSavedFurniture> parse(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var rows = new List<WiredSimulationSavedFurniture>();
        var ids = new HashSet<long>();
        foreach (string line in text.Split(';'))
        {
            string[] fields = line.Split(',');
            if (fields.Length != 6 || !long.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id <= 0 || !ids.Add(id))
                throw new FormatException("A saved furniture row requires a distinct positive ID and six fields.");
            int? number(int index) => fields[index] == "N" ? null : int.TryParse(fields[index], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int value) ? value : throw new FormatException("A saved furniture component is not an integer or N.");
            double? height = null;
            if (fields[5] != "N")
            {
                if (!double.TryParse(fields[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                    throw new FormatException("A saved furniture altitude is not finite or N.");
                height = value / 100;
            }
            int? direction = number(2), x = number(3), y = number(4);
            if (direction is < 0 or > 7 || x.HasValue != y.HasValue)
                throw new FormatException("A saved furniture row requires a valid direction and a complete position pair.");
            rows.Add(new(id, number(1), direction, x, y, height));
        }
        return rows.ToArray();
    }

    internal static IReadOnlyList<WiredSimulationSavedFurniture> capture(WiredSimulationNode node, IEnumerable<WiredSimulationFurniture> items)
    {
        bool state = node.Name switch
        {
            "MatchSnapshot" => node.Flag("state"),
            "StatesMatch" => node.Flag("match_state"),
            "StateChange" => node.Number("state_mode") == 1,
            "PlaceFurni" => true,
            _ => false
        };
        bool direction = node.Name == "PlaceFurni" || node.Name == "MatchSnapshot" && node.Flag("direction") || node.Name == "StatesMatch" && node.Flag("match_direction");
        bool position = node.Name == "PlaceFurni" || node.Name == "MatchSnapshot" && node.Flag("position") || node.Name == "StatesMatch" && node.Flag("match_position");
        bool altitude = node.Name == "PlaceFurni" || node.Name == "MatchSnapshot" && node.Flag("altitude") || node.Name == "StatesMatch" && node.Flag("match_altitude");
        return items.Select(item => new WiredSimulationSavedFurniture(item.Id, state && item.State >= 0 ? item.State : null,
            direction ? item.Direction : null, position ? item.X : null, position ? item.Y : null, altitude ? item.Z : null)).ToArray();
    }
}
