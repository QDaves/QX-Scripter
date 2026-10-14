using Qx.Model.Wired;

namespace Qx.Game.Application;

public sealed record WiredBuildOptions(
    bool HideSettings = true,
    bool VerifySaves = true,
    int TimeoutMilliseconds = 10000,
    int PlacementAttempts = 3,
    int PlacementSpacingMilliseconds = 0,
    double MaximumUsageFraction = 0.7,
    long? ExpectedRoomId = null,
    long? ExpectedRoomGeneration = null);

public sealed record WiredBuildItem(
    string Key,
    string ClassName,
    int X,
    int Y,
    int Direction = 0,
    Id? ExistingId = null,
    bool PlaceIfMissing = true,
    bool ReuseAtPosition = true,
    int? State = null,
    WiredFormCategory? Category = null,
    int? Code = null,
    WiredFormPatch? Settings = null,
    IReadOnlyList<string>? FurnitureKeys = null,
    IReadOnlyList<string>? SecondaryFurnitureKeys = null,
    bool CaptureSnapshot = false,
    IReadOnlyDictionary<string, string>? VariablesByName = null,
    int? AltitudeCentimeters = null);

public sealed record WiredWorkload(double ActivationsPerSecond, int ActionsPerActivation,
    int TargetsPerAction, int ReceiverActivations = 1, double? MeasuredCostPerWorkUnit = null);

public sealed record WiredBuildRequest(IReadOnlyList<WiredBuildItem> Items,
    WiredBuildOptions? Options = null, WiredWorkload? Workload = null);

public sealed record WiredConfigureRequest(Id FurniId, WiredFormCategory Category, int Code,
    WiredFormPatch Settings, bool CaptureSnapshot = false, WiredBuildOptions? Options = null);

public sealed record WiredUsageMeasureRequest(int DurationMilliseconds = 2000, int IntervalMilliseconds = 500);

public sealed record WiredUsageMeasurement(WiredRoomStatsData Start, WiredRoomStatsData End,
    int Samples, double PeakUsage, double AverageUsage, bool WasHeavy, IReadOnlyList<WiredError> NewErrors);

public sealed record WiredBuildAnalysis(int Items, int Stacks, int WiredItems,
    double? WorkUnitsPerSecond, double? EstimatedUsage,
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool Valid => Errors.Count == 0;
    public IReadOnlyList<WiredStackWorkload> AutomaticWorkloads { get; init; } = [];
}

public sealed record WiredStackWorkload(int X, int Y, int Actions, double? ActivationsPerSecond,
    double? WorkUnitsPerSecond);

public sealed record WiredBuildPreflight(WiredBuildAnalysis Analysis, WiredRoomStatsData Stats,
    IReadOnlyList<WiredError> Errors);

public sealed record WiredBuildProgress(string Stage, int Completed, int Total, string? Key);

public sealed record WiredBuildItemResult(string Key, Id Id, bool Created, bool Configured);

public sealed record WiredBuildResult(bool Success, long RoomId, long RoomGeneration,
    IReadOnlyList<WiredBuildItemResult> Items, string? FailedKey, string? Error,
    WiredBuildPreflight Preflight);

public sealed record WiredInspectRequest(IReadOnlyList<Id> FurnitureIds,
    bool HideSettings = true, int TimeoutMilliseconds = 10000);

public sealed record WiredInspectItem(Id Id, WiredFormView? Form, string? Error);

public sealed record WiredInspectResult(long RoomId, long RoomGeneration, IReadOnlyList<WiredInspectItem> Items);

public sealed class WiredBuildPlan
{
    private readonly List<WiredBuildItem> items = [];

    public WiredBuildOptions Options { get; set; } = new();
    public WiredWorkload? Workload { get; set; }

    public WiredBuildPlan Furni(string key, string class_name, int x, int y, int direction = 0,
        int? state = null, Id? existing_id = null, bool place_if_missing = true, bool reuse_at_position = true,
        int? altitude_centimeters = null)
    {
        items.Add(new(key, class_name, x, y, direction, existing_id, place_if_missing, reuse_at_position, state,
            AltitudeCentimeters: altitude_centimeters));
        return this;
    }

    public WiredStackPlan Stack(string key, int x, int y, int direction = 0) => new(this, key, x, y, direction);

    public WiredBuildPlan Add(WiredBuildItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        items.Add(item);
        return this;
    }

    public WiredBuildRequest ToRequest() => new(items.ToArray(), Options, Workload);
    public WiredBuildAnalysis Analyze() => WiredBuildAnalyzer.Analyze(ToRequest());

    public WiredBuildPlan Reuse(WiredBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var known = result.Items.ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        for (int index = 0; index < items.Count; index++)
            if (known.TryGetValue(items[index].Key, out Id id)) items[index] = items[index] with { ExistingId = id };
        Options = Options with { ExpectedRoomId = result.RoomId, ExpectedRoomGeneration = result.RoomGeneration };
        return this;
    }
}

public sealed class WiredStackPlan(WiredBuildPlan plan, string key, int x, int y, int direction)
{
    public WiredStackPlan Add<T>(string name, string class_name, Action<T> configure,
        IReadOnlyList<string>? furniture = null, IReadOnlyList<string>? secondary_furniture = null,
        bool capture_snapshot = false, Id? existing_id = null, bool place_if_missing = true,
        IReadOnlyDictionary<string, string>? variables = null) where T : WiredForm
    {
        ArgumentNullException.ThrowIfNull(configure);
        T form = WiredFormRegistry.Draft<T>();
        configure(form);
        plan.Add(new(key + "." + name, class_name, x, y, direction, existing_id, place_if_missing,
            Category: form.Definition.Category, Code: form.Definition.Code,
            Settings: WiredFormPatch.FromEdits(form), FurnitureKeys: furniture,
            SecondaryFurnitureKeys: secondary_furniture, CaptureSnapshot: capture_snapshot, VariablesByName: variables));
        return this;
    }
}

public static class WiredSettings
{
    public static WiredFormPatch For<T>(Action<T> configure) where T : WiredForm
    {
        ArgumentNullException.ThrowIfNull(configure);
        T form = WiredFormRegistry.Draft<T>();
        configure(form);
        return WiredFormPatch.FromEdits(form);
    }

    public static WiredValueReference Constant(int value) => new(WiredValueSource.Constant, value, WiredSourceDomain.Global, "n");
    public static WiredValueReference Variable(string id, WiredSourceDomain domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return new(WiredValueSource.Variable, 0, domain, id);
    }

    public static void Area(WiredFurnitureInAreaSelectorForm form, int x, int y, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(form);
        bounds(x, y, width, height);
        form.X = x;
        form.Y = y;
        form.Width = width;
        form.Height = height;
    }

    public static void Area(WiredUsersInAreaSelectorForm form, int x, int y, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(form);
        bounds(x, y, width, height);
        form.X = x;
        form.Y = y;
        form.Width = width;
        form.Height = height;
    }

    private static void bounds(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if ((long)x + width > int.MaxValue || (long)y + height > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "Area coordinates overflow.");
    }
}
