using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed record WiredSimulationTransparency(double Start, int Target, bool ClickThrough,
    int Easing, long ChangedAt, bool Transition);

public sealed record WiredSimulationHighlight(int Type, double Start, int Target, int Color,
    int Animation, int Easing, long ChangedAt, long AnimationStartedAt, bool Transition);

public sealed record WiredSimulationVisualView(double Transparency, double Alpha, bool ClickThrough,
    int HighlightType, double HighlightStrength, int HighlightColor);

public sealed record WiredSimulationVisualState(WiredSimulationTransparency? Transparency = null,
    WiredSimulationHighlight? Highlight = null)
{
    public WiredSimulationVisualState SetTransparency(int transparency, bool click_through, int easing,
        long at, bool initialize = false)
    {
        double current = transparency_at(at);
        int target = Math.Clamp(transparency, 0, 100);
        bool transition = !initialize && easing != 0 && current != target;
        return this with { Transparency = new(current, target, click_through, easing, at, transition) };
    }

    public WiredSimulationVisualState SetHighlight(int type, int strength, int color, int animation,
        int easing, long at, bool initialize = false)
    {
        if (type is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(type));
        double current = highlight_at(at);
        int target = Math.Clamp(strength, 0, 100);
        animation = Math.Clamp(animation, 0, 1);
        easing = animation == 0 ? Math.Clamp(easing, 0, 4) : 0;
        long animation_start = Highlight is null || !initialize ? at : Highlight.AnimationStartedAt;
        bool transition = animation == 0 && !initialize && easing != 0 && current != target;
        return this with { Highlight = new(type, current, target, color & 0xffffff, animation, easing, at, animation_start, transition) };
    }

    public WiredSimulationVisualView At(long at)
    {
        double transparency = transparency_at(at);
        return new(transparency, 1 - transparency / 100, Transparency?.ClickThrough ?? false,
            Highlight?.Type ?? 0, highlight_at(at), Highlight?.Color ?? 0);
    }

    internal WiredSimulationVisualState Rebase(long at) => this with
    {
        Transparency = Transparency is { } transparency ? transparency with { ChangedAt = checked(transparency.ChangedAt - at) } : null,
        Highlight = Highlight is { } highlight ? highlight with
        { ChangedAt = checked(highlight.ChangedAt - at), AnimationStartedAt = checked(highlight.AnimationStartedAt - at) } : null
    };

    private double transparency_at(long at) => Transparency is not { } value ? 0 :
        value.Transition ? interpolate(value.Start, value.Target, at, value.ChangedAt, value.Easing) : value.Target;

    private double highlight_at(long at)
    {
        if (Highlight is not { } value) return 0;
        if (value.Animation == 1)
        {
            double elapsed = Math.Max(0, (double)at - value.AnimationStartedAt);
            double phase = elapsed % 1000 / 1000;
            return Math.Floor(value.Target * (1 - Math.Cos(phase * Math.PI * 2)) / 2 + 0.5);
        }
        return value.Transition ? interpolate(value.Start, value.Target, at, value.ChangedAt, value.Easing) : value.Target;
    }

    private static double interpolate(double start, int target, long at, long changed_at, int easing)
    {
        double progress = Math.Clamp(((double)at - changed_at) / 500, 0, 1);
        double amount = easing switch
        {
            2 => progress * progress,
            3 => 1 - (1 - progress) * (1 - progress),
            4 => progress < 0.5 ? 2 * progress * progress : 1 - Math.Pow(-2 * progress + 2, 2) / 2,
            _ => progress
        };
        return start + (target - start) * amount;
    }
}

public sealed record WiredSimulationVisual(WiredSourceDomain Domain, long EntityId, long ViewerId,
    WiredSimulationVisualState State);

public sealed record WiredSimulationVisualSample(WiredSourceDomain Domain, long EntityId, long ViewerId,
    WiredSimulationVisualView View);

public sealed record WiredSimulationVisualCommand(WiredSourceDomain Domain, IReadOnlyList<long> Entities,
    bool Everyone, int? Transparency = null, bool ClickThrough = false, int HighlightType = 0,
    int Strength = 0, int Color = 0, int Animation = 0, int Easing = 0, bool Initialize = false);

public sealed partial class WiredSimulation
{
    private readonly Dictionary<(WiredSourceDomain Domain, long Entity, long Viewer), WiredSimulationVisualState> visuals = [];

    private void restore_visuals()
    {
        foreach (var value in initial.Visuals ?? [])
        {
            if (value is null || value.State is null || !visual_target(value.Domain, value.EntityId) ||
                !users.TryGetValue(value.ViewerId, out var viewer) || viewer.IsBot || viewer.IsPet ||
                !valid_visual_state(value.State) || !visuals.TryAdd((value.Domain, value.EntityId, value.ViewerId), value.State))
                fail("snapshot.visual", "A visual snapshot requires a unique existing target, human viewer and valid state.");
        }
    }

    private static bool valid_visual_state(WiredSimulationVisualState state) =>
        (state.Transparency is not { } transparency || double.IsFinite(transparency.Start) && transparency.Start is >= 0 and <= 100 &&
            transparency.Target is >= 0 and <= 100) &&
        (state.Highlight is not { } highlight || double.IsFinite(highlight.Start) && highlight.Start is >= 0 and <= 100 &&
            highlight.Target is >= 0 and <= 100 && highlight.Type is >= 0 and <= 5 && highlight.Color is >= 0 and <= 0xffffff &&
            highlight.Animation is >= 0 and <= 1 && highlight.Easing is >= 0 and <= 4 && (highlight.Animation == 0 || !highlight.Transition));

    private bool visual_target(WiredSourceDomain domain, long id) => domain switch
    {
        WiredSourceDomain.Furniture => furniture.ContainsKey(id),
        WiredSourceDomain.User => users.ContainsKey(id),
        _ => false
    };

    private IEnumerable<KeyValuePair<(WiredSourceDomain Domain, long Entity, long Viewer), WiredSimulationVisualState>> current_visuals() =>
        visuals.Where(pair => visual_target(pair.Key.Domain, pair.Key.Entity) && users.TryGetValue(pair.Key.Viewer, out var viewer) && !viewer.IsBot && !viewer.IsPet)
            .OrderBy(pair => pair.Key.Domain).ThenBy(pair => pair.Key.Entity).ThenBy(pair => pair.Key.Viewer);

    private IReadOnlyList<WiredSimulationVisual> visual_snapshot() => current_visuals()
        .Select(pair => new WiredSimulationVisual(pair.Key.Domain, pair.Key.Entity, pair.Key.Viewer, pair.Value.Rebase(now))).ToArray();

    private IReadOnlyList<WiredSimulationVisualSample> visual_samples() => current_visuals()
        .Select(pair => new WiredSimulationVisualSample(pair.Key.Domain, pair.Key.Entity, pair.Key.Viewer, pair.Value.At(now))).ToArray();

    private void visual_action(WiredSimulationNode node, Frame frame)
    {
        var domain = node.Flag("target_users") ? WiredSourceDomain.User : WiredSourceDomain.Furniture;
        long[] targets = domain == WiredSourceDomain.User ? us(node, frame, 1) : fs(node, frame);
        int audience = (int)node.Number("audience");
        if (audience is < 0 or > 1) unsupported(node, "Unknown visualization audience.");
        long[] viewers = (audience == 1 ? users.Keys.ToArray() : us(node, frame))
            .Where(id => !users[id].IsBot && !users[id].IsPet).ToArray();
        bool transparency = node.Name == "SetFurniTransparency";
        int highlight_type = (int)node.Number("highlight_type"), animation = (int)node.Number("animation");
        if (!transparency && highlight_type == 4 && animation != 1)
            unsupported(node, "The client exposes Brighten only with pulse animation; other combinations are not verified.");
        if (!transparency && highlight_type == 3 && animation == 0 && domain == WiredSourceDomain.Furniture &&
            targets.Any(id => !furniture[id].Temporary && furniture[id].IntrinsicType != 1))
            unsupported(node, "Steady tint requires explicitly identified Builders Club or temporary furniture; ordinary-furniture handling is not verified.");
        var command = new WiredSimulationVisualCommand(domain, targets, audience == 1,
            transparency ? (int)node.Number("transparency") : null, node.Flag("allow_click_through"),
            highlight_type, (int)node.Number("strength"), (int)node.Number("color_rgb"), animation, (int)node.Number("easing"));
        foreach (long viewer in viewers)
        foreach (long target in targets)
        {
            var key = (domain, target, viewer);
            var previous = visuals.GetValueOrDefault(key) ?? new();
            visuals[key] = transparency
                ? previous.SetTransparency(command.Transparency!.Value, command.ClickThrough, command.Easing, now)
                : previous.SetHighlight(command.HighlightType, command.Strength, command.Color, command.Animation, command.Easing, now);
            charge(frame, 1, node.FurnitureId);
        }
        if (viewers.Length > 0 && targets.Length > 0)
            effects.Add(new(now, transparency ? "transparency" : "highlight", node.FurnitureId, viewers, Visual: command));
    }
}
