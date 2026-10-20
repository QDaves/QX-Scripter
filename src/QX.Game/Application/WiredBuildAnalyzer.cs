using Qx.Model.Wired;

namespace Qx.Game.Application;

public static class WiredBuildAnalyzer
{
    public static WiredBuildAnalysis Analyze(WiredBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Items);
        var errors = new List<string>();
        var warnings = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (WiredBuildItem item in request.Items)
        {
            if (item is null)
            {
                errors.Add("A build item is null.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.Key) || !keys.Add(item.Key)) errors.Add($"Duplicate or empty item key '{item.Key}'.");
            if (string.IsNullOrWhiteSpace(item.ClassName)) errors.Add($"{item.Key}: a furniture class name is required.");
            if (item.X < 0 || item.Y < 0 || item.Direction is < 0 or > 7) errors.Add($"{item.Key}: invalid tile or direction.");
            if (item.ExistingId is { } id && id <= 0) errors.Add($"{item.Key}: an existing floor item ID must be positive.");
            if (item.Category.HasValue != item.Code.HasValue) errors.Add($"{item.Key}: supply both category and code.");
            if (item.Category is null && (item.Settings is not null || item.CaptureSnapshot || item.VariablesByName is { Count: > 0 } ||
                item.FurnitureKeys is not null || item.SecondaryFurnitureKeys is not null))
                errors.Add($"{item.Key}: settings, references and snapshot capture require a typed form.");
            if (item.Category is { } category && item.Code is { } code)
            {
                try
                {
                    WiredForm draft = WiredFormRegistry.Draft(category, code);
                    item.Settings?.Apply(draft);
                    WiredFormRules.Check(draft);
                    foreach (var binding in item.VariablesByName ?? new Dictionary<string, string>())
                    {
                        if (!draft.Definition.Fields.Any(field => field.Name == binding.Key && field.Kind == WiredFormFieldKind.Variable && field.Writable) ||
                            string.IsNullOrWhiteSpace(binding.Value)) errors.Add($"{item.Key}: invalid named variable binding '{binding.Key}'.");
                        if (item.Settings?.Fields?.Any(field => field.Name == binding.Key) == true)
                            errors.Add($"{item.Key}: choose a variable name binding or an explicit field value for '{binding.Key}', not both.");
                    }
                    if (draft.Definition.Category == WiredFormCategory.Selector && item.Settings?.Fields is not { Count: > 0 })
                        warnings.Add($"{item.Key}: no selector fields were supplied; review its bounds, filters and references.");
                    if (item.VariablesByName is not { Count: > 0 } && draft.Definition.Fields.Any(field => field.Kind == WiredFormFieldKind.Variable) &&
                        item.Settings?.Fields?.Any(field => draft.Definition.Fields.Any(definition => definition.Name == field.Name && definition.Kind == WiredFormFieldKind.Variable)) != true)
                        warnings.Add($"{item.Key}: variable references rely on existing settings; new furniture needs explicit variable IDs.");
                }
                catch (ArgumentException ex) { errors.Add($"{item.Key}: {ex.Message}"); }
                catch (InvalidOperationException ex) { errors.Add($"{item.Key}: {ex.Message}"); }
            }
        }
        foreach (WiredBuildItem item in request.Items.Where(item => item is not null))
        {
            foreach (string target in (item.FurnitureKeys ?? []).Concat(item.SecondaryFurnitureKeys ?? []))
                if (!keys.Contains(target)) errors.Add($"{item.Key}: unknown furniture key '{target}'.");
            if (item.FurnitureKeys is not null && item.Settings?.Furniture is not null)
                errors.Add($"{item.Key}: choose furniture keys or furniture IDs, not both.");
            if (item.SecondaryFurnitureKeys is not null && item.Settings?.SecondaryFurniture is not null)
                errors.Add($"{item.Key}: choose secondary furniture keys or IDs, not both.");
        }
        foreach (IGrouping<(int X, int Y), WiredBuildItem> stack in request.Items.Where(item => item?.Category is not null).GroupBy(item => (item.X, item.Y)))
        {
            bool selector = stack.Any(item => item.Category == WiredFormCategory.Selector);
            bool consumer = stack.Any(item => item.Settings?.FurnitureSources?.Any(source => source.Source == WiredFurnitureSource.Selector) == true ||
                item.Settings?.UserSources?.Any(source => source.Source == WiredUserSource.Selector) == true);
            if (selector && !consumer) warnings.Add($"Stack ({stack.Key.X},{stack.Key.Y}): no explicit consumer uses Selector; verify target sources.");
            if (!selector && consumer) warnings.Add($"Stack ({stack.Key.X},{stack.Key.Y}): Selector source has no local selector; verify remote selection.");
        }
        WiredBuildOptions options = request.Options ?? new();
        if (!ValidOptions(options))
            errors.Add("Invalid timeout, placement attempts, spacing or usage headroom.");
        double? work = null;
        double? cost = null;
        if (request.Workload is { } load)
        {
            if (!double.IsFinite(load.ActivationsPerSecond) || load.ActivationsPerSecond < 0 || load.ActionsPerActivation < 0 ||
                load.TargetsPerAction < 0 || load.ReceiverActivations < 1 ||
                load.MeasuredCostPerWorkUnit is { } unit && (!double.IsFinite(unit) || unit < 0))
                errors.Add("Invalid workload estimate.");
            else
            {
                work = load.ActivationsPerSecond * load.ActionsPerActivation * load.TargetsPerAction * load.ReceiverActivations;
                cost = load.MeasuredCostPerWorkUnit * work;
                if (!double.IsFinite(work.Value) || cost is { } estimate && !double.IsFinite(estimate)) errors.Add("Workload estimate overflows.");
            }
        }
        warnings.Add("Server cost is not derivable from furniture count. Work units are planning estimates; usage requires live measurement and any cost coefficient is caller-supplied calibration.");
        warnings.Add("Configuration acceptance does not prove gameplay. Verify counters and actual targets; never blindly replay partially completed increments. Existing triggers remain active while building.");
        IReadOnlyList<WiredStackWorkload> automatic = workloads(request.Items);
        if (request.Workload is null && automatic.Count > 0 && automatic.All(stack => stack.WorkUnitsPerSecond is not null))
            work = automatic.Sum(stack => stack.WorkUnitsPerSecond!.Value);
        return new(request.Items.Count, request.Items.Where(item => item?.Category is not null).Select(item => (item.X, item.Y)).Distinct().Count(),
            request.Items.Count(item => item?.Category is not null), work, cost, errors.AsReadOnly(), warnings.AsReadOnly()) { AutomaticWorkloads = automatic };
    }

    private static IReadOnlyList<WiredStackWorkload> workloads(IReadOnlyList<WiredBuildItem> items)
    {
        var result = new List<WiredStackWorkload>();
        foreach (var stack in items.Where(item => item?.Category is not null).GroupBy(item => (item.X, item.Y)))
        {
            WiredBuildItem[] triggers = stack.Where(item => item.Category == WiredFormCategory.Trigger).ToArray();
            WiredBuildItem[] actions = stack.Where(item => item.Category == WiredFormCategory.Action).ToArray();
            double? rate = triggers.Length == 0 ? null : 0;
            foreach (WiredBuildItem trigger in triggers)
            {
                int? interval = trigger.Settings?.Fields?.LastOrDefault(field => field.Name == "interval_units")?.Value.Integer;
                if (interval is > 0 && trigger.Code is 12 or 19)
                    rate += 1 / (interval.Value * (trigger.Code == 19 ? 0.05 : 5));
                else rate = null;
            }
            double? targets = 0;
            foreach (WiredBuildItem action in actions)
            {
                IReadOnlyList<WiredFurnitureSourceEdit> sources = action.Settings?.FurnitureSources ?? [];
                if (sources.Count != 1 || sources[0].Selection != 0 || sources[0].Source != WiredFurnitureSource.Picked ||
                    action.Settings?.UserSources is { Count: > 0 })
                    targets = null;
                else if (action.FurnitureKeys is { } keys) targets += keys.Distinct().Count();
                else if (action.Settings?.Furniture is { } furniture) targets += furniture.Distinct().Count();
                else targets = null;
            }
            result.Add(new(stack.Key.X, stack.Key.Y, actions.Length, rate, rate * targets));
        }
        return result.AsReadOnly();
    }

    internal static bool ValidOptions(WiredBuildOptions options) =>
        options.TimeoutMilliseconds is >= 1 and <= 120000 && options.PlacementAttempts is >= 1 and <= 10 &&
        options.PlacementSpacingMilliseconds is >= 0 and <= 60000 && double.IsFinite(options.MaximumUsageFraction) &&
        options.MaximumUsageFraction is > 0 and < 1;
}
