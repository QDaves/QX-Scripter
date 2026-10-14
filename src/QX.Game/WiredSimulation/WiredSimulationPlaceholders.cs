namespace Qx.Game.WiredSimulation;

public sealed record WiredSimulationSharedPlaceholder(long RoomId, string Name, string? Value = null, string? RoomName = null);

public sealed partial class WiredSimulation
{
    private void validate_shared_placeholders()
    {
        var keys = new HashSet<(long, string)>();
        foreach (var placeholder in initial.SharedPlaceholders ?? [])
            if (placeholder is null || placeholder.RoomId <= 0 || string.IsNullOrWhiteSpace(placeholder.Name) ||
                placeholder.Name.Length > 32 || placeholder.Name.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_')) ||
                !keys.Add((placeholder.RoomId, placeholder.Name)))
                fail("snapshot.shared_placeholder", "Shared placeholders require a positive room ID and a unique normalized name of 1..32 characters.");
    }

    private string global_placeholder(WiredSimulationNode node, HashSet<long> visiting)
    {
        if (!visiting.Add(node.FurnitureId) || visiting.Count > 64)
            unsupported(node, "Cyclic or excessively deep global placeholder references cannot be evaluated.");
        try
        {
            if (node.Number("mode") == 0) return node.Text("value_or_reference");
            if (node.Number("mode") != 1) unsupported(node, "Unknown global placeholder mode.");
            long room_id = node.Number("room_id");
            string name = node.Text("value_or_reference");
            if (room_id <= 0 || string.IsNullOrEmpty(name))
                unsupported(node, "A global placeholder reference requires its source room and normalized name.");
            if (room_id == initial.RoomId)
            {
                var matches = nodes.Values.Where(candidate => candidate.Name == "GlobalPlaceholder" &&
                    candidate.Text("placeholder_name") == name && furniture.ContainsKey(candidate.FurnitureId)).Take(2).ToArray();
                if (matches.Length != 1)
                    unsupported(node, "A same-room global placeholder reference must identify exactly one supplied definition.");
                return global_placeholder(matches[0], visiting);
            }
            var shared = initial.SharedPlaceholders?.SingleOrDefault(value => value.RoomId == room_id && value.Name == name);
            if (shared?.Value is not { } text)
                unsupported(node, "Shared placeholder names do not include their values; supply an explicit SharedPlaceholders value.");
            return shared!.Value!;
        }
        finally { visiting.Remove(node.FurnitureId); }
    }
}
