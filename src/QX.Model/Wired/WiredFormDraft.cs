using System.Collections.Concurrent;

namespace Qx.Model.Wired;

public static partial class WiredFormRegistry
{
    private static readonly ConcurrentDictionary<Type, (WiredFormCategory Category, int Code)> draft_types = new();

    public static WiredForm Draft(WiredFormCategory category, int code)
    {
        WiredFormDefinition definition = Find(category, code)
            ?? throw new ArgumentException($"Unknown Wired form {category}/{code}.", nameof(code));
        WiredConfig config = category switch
        {
            WiredFormCategory.Trigger => new WiredTriggerConfig(),
            WiredFormCategory.Action => new WiredActionConfig(),
            WiredFormCategory.Condition => new WiredConditionConfig(),
            WiredFormCategory.Selector => new WiredSelectorConfig(),
            WiredFormCategory.Addon => new WiredAddonConfig(),
            WiredFormCategory.Variable => new WiredVariableConfig(),
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
        config.Code = definition.Code;
        config.Id = 1;
        config.FurniLimit = int.MaxValue;
        config.AllowWallFurni = true;
        config.InputSources = new(
            Enumerable.Range(0, 4).Select(_ => (IReadOnlyList<int>)Enum.GetValues<WiredFurnitureSource>().Select(value => (int)value).ToArray()).ToArray(),
            Enumerable.Range(0, 4).Select(_ => (IReadOnlyList<int>)Enum.GetValues<WiredUserSource>().Select(value => (int)value).ToArray()).ToArray(),
            [], []);
        return Create(config);
    }

    public static T Draft<T>() where T : WiredForm
    {
        var key = draft_types.GetOrAdd(typeof(T), type => {
            foreach (WiredFormDefinition definition in Definitions)
                if (Draft(definition.Category, definition.Code).GetType() == type)
                    return (definition.Category, definition.Code);
            throw new ArgumentException($"No registered Wired form for {type.Name}.");
        });
        return (T)Draft(key.Category, key.Code);
    }
}
