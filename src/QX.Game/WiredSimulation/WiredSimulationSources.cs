using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private static InputSourcesConf? copy_sources(InputSourcesConf? sources)
    {
        if (sources is null) return null;
        ArgumentNullException.ThrowIfNull(sources.AllowedFurniSources);
        ArgumentNullException.ThrowIfNull(sources.AllowedUserSources);
        ArgumentNullException.ThrowIfNull(sources.DefaultFurniSources);
        ArgumentNullException.ThrowIfNull(sources.DefaultUserSources);
        static IReadOnlyList<int> copy(IReadOnlyList<int> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            return values.ToArray();
        }
        return new(sources.AllowedFurniSources.Select(copy).ToArray(), sources.AllowedUserSources.Select(copy).ToArray(),
            sources.DefaultFurniSources.ToArray(), sources.DefaultUserSources.ToArray());
    }

    private void validate_sources(WiredSimulationNode node)
    {
        if (node.InputSources is not { } sources) return;
        void validate(string domain, IReadOnlyList<IReadOnlyList<int>> allowed, IReadOnlyList<int> defaults, IEnumerable<int> selected)
        {
            var values = selected.ToArray();
            if (values.Length > allowed.Count || defaults.Count > allowed.Count)
                fail("form.sources", $"The {domain} source count exceeds the captured source definition.", node.FurnitureId);
            for (int index = 0; index < allowed.Count; index++)
            {
                int? source = index < values.Length ? values[index] : index < defaults.Count ? defaults[index] : null;
                if (source is { } chosen && !allowed[index].Contains(chosen))
                    fail("form.sources", $"The captured definition does not allow {domain} source {chosen} in slot {index}.", node.FurnitureId);
            }
        }
        validate("furniture", sources.AllowedFurniSources, sources.DefaultFurniSources, (node.FurnitureSources ?? []).Select(value => (int)value));
        validate("user", sources.AllowedUserSources, sources.DefaultUserSources, (node.UserSources ?? []).Select(value => (int)value));
    }

    private static WiredFurnitureSource furniture_source(WiredSimulationNode node, int slot = 0) =>
        node.FurnitureSources is { } selected && slot < selected.Count ? selected[slot] :
        node.InputSources is { } sources && slot < sources.DefaultFurniSources.Count ? (WiredFurnitureSource)sources.DefaultFurniSources[slot] :
        WiredFurnitureSource.Picked;

    private static WiredUserSource user_source(WiredSimulationNode node, int slot = 0) =>
        node.UserSources is { } selected && slot < selected.Count ? selected[slot] :
        node.InputSources is { } sources && slot < sources.DefaultUserSources.Count ? (WiredUserSource)sources.DefaultUserSources[slot] :
        WiredUserSource.Triggering;

    private static bool has_user_source(WiredSimulationNode node) =>
        node.UserSources is { Count: > 0 } || node.InputSources?.DefaultUserSources.Count > 0;

    private static bool has_furniture_source(WiredSimulationNode node) =>
        node.FurnitureSources is { Count: > 0 } || node.InputSources?.DefaultFurniSources.Count > 0;
}
