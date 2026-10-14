using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private readonly record struct FxSlot(int ConfigId, string Variable, WiredSourceDomain Domain, long EntityId, long ViewerId);
    private sealed record FxMemory(long Value, long? Minimum, long? Maximum, string Extra, long? Until, bool IsInitialize);
    private readonly Dictionary<FxSlot, FxMemory> fx_memory = [];
    private readonly Dictionary<long, WiredSimulationHover> fx_hover = [];
    private readonly HashSet<(WiredSourceDomain Domain, long Id, string Variable)> fx_changes = [];
    private IReadOnlyList<WiredSimulationVariableFx> fx_current = [];
    private bool fx_initialized;

    private IReadOnlyList<WiredSimulationVariableFx> variable_fx()
    {
        if (!fx_initialized || !fx_pending()) refresh_fx();
        if (status == WiredSimulationStatus.Faulted) return [];
        return fx_current.Select(display => copy_fx_display(display) with
        {
            Views = display.Audience.Select(viewer => fx_view(display, viewer)).ToArray()
        }).ToArray();
    }

    private bool fx_pending() => queue.TryPeek(out _, out var priority) && priority.Time == now;

    private static WiredSimulationVariableFx copy_fx_display(WiredSimulationVariableFx display) => display with
    {
        Config = display.Config with { Extra = display.Config.Extra.ToDictionary() },
        Extra = display.Extra.ToDictionary(), Audience = display.Audience.ToArray(), Views = null
    };

    private IReadOnlyList<WiredSimulationVariableFx> refresh_fx()
    {
        var values = fx_values();
        if (status == WiredSimulationStatus.Faulted) return [];
        if (!fx_initialized && initial.FxPresentation is { } seed)
        {
            foreach (var hover in seed.Hovers)
            {
                if (!valid_hover(hover)) { fail("snapshot.fx_hover", "Invalid initial FX hover target."); return []; }
                if (hover.Active) fx_hover[hover.ViewerId] = hover;
            }
            foreach (var entry in seed.States)
            {
                var display = values.FirstOrDefault(d => d.Config.ConfigId == entry.ConfigId && d.VariableId == entry.VariableId &&
                    d.Domain == entry.Domain && d.EntityId == entry.EntityId && d.Audience.Contains(entry.ViewerId));
                if (display is null || display.Value != entry.Value || entry.RemainingMilliseconds is < 0 or > 20000 ||
                    !fx_memory.TryAdd(new(entry.ConfigId, entry.VariableId, entry.Domain, entry.EntityId, entry.ViewerId),
                        new(entry.Value, display.MinOverride, display.MaxOverride, fx_extra(display), entry.RemainingMilliseconds, entry.IsInitialize)))
                { fail("snapshot.fx_state", "Initial FX state must match a unique current display and eligible viewer."); return []; }
            }
        }
        var current = new HashSet<FxSlot>();
        foreach (var display in values)
        foreach (long viewer in display.Audience)
        {
            var slot = new FxSlot(display.Config.ConfigId, display.VariableId, display.Domain, display.EntityId, viewer);
            current.Add(slot);
            fx_memory.TryGetValue(slot, out var previous);
            string extra = fx_extra(display);
            bool forced = fx_changed(display);
            bool changed = previous is not null && (previous.Value != display.Value || previous.Minimum != display.MinOverride ||
                previous.Maximum != display.MaxOverride || previous.Extra != extra);
            bool initialize = previous?.IsInitialize ?? (!fx_initialized || !forced);
            long? until = previous?.Until;
            if (previous is null || changed || forced)
            {
                initialize = previous is null && (!fx_initialized || !forced);
                int mask = previous is null ? 1 : display.Value > previous.Value ? 2 : display.Value < previous.Value ? 4 : 8;
                if (!initialize && display.Config.ShowMode == 1 && (display.Config.VisibilityMask & mask) != 0)
                    until = checked(now + display.Config.ShowDuration);
            }
            fx_memory[slot] = new(display.Value, display.MinOverride, display.MaxOverride, extra, until, initialize);
        }
        foreach (var slot in fx_memory.Keys.Where(key => !current.Contains(key)).ToArray()) fx_memory.Remove(slot);
        foreach (var pair in fx_hover.Where(pair => !valid_hover(pair.Value)).ToArray()) fx_hover.Remove(pair.Key);
        fx_initialized = true;
        fx_changes.Clear();
        fx_current = values;
        return values;
    }

    private static string fx_extra(WiredSimulationVariableFx display) =>
        string.Join('\n', display.Extra.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));

    private bool fx_changed(WiredSimulationVariableFx display)
    {
        if (fx_changes.Contains((display.Domain, display.EntityId, display.VariableId))) return true;
        if (!nodes.TryGetValue(display.Config.ConfigId, out var node)) return false;
        foreach (string bound in new[] { "minimum", "maximum" })
        {
            if (!node.Flag("override_" + bound)) continue;
            var target = (WiredSourceDomain)node.Number(bound + "_target");
            long holder = target == WiredSourceDomain.Global ? 0 : display.EntityId;
            if (fx_changes.Contains((target, holder, resolve_reference(node.Text(bound + "_variable"))))) return true;
        }
        return false;
    }

    private WiredSimulationFxView fx_view(WiredSimulationVariableFx display, long viewer)
    {
        var slot = new FxSlot(display.Config.ConfigId, display.VariableId, display.Domain, display.EntityId, viewer);
        var memory = fx_memory[slot];
        bool hovered = fx_hover.TryGetValue(viewer, out var target) && target.Domain == display.Domain && target.EntityId == display.EntityId;
        bool visible = display.Config.ShowMode == 0 || hovered && display.Config.ShowOnMouseHover ||
            display.Config.ShowMode == 1 && memory.Until > now;
        return new(viewer, visible, hovered, memory.Until, memory.IsInitialize || !visible);
    }

    private bool valid_hover(WiredSimulationHover hover) =>
        users.TryGetValue(hover.ViewerId, out var viewer) && !viewer.IsBot && !viewer.IsPet &&
        (hover.Domain == WiredSourceDomain.Furniture && furniture.ContainsKey(hover.EntityId) ||
            hover.Domain == WiredSourceDomain.User && users.ContainsKey(hover.EntityId));

    private void hover_fx(WiredSimulationHover hover)
    {
        if (!valid_hover(hover)) { fail("event.fx_hover", "FX hover requires an existing viewer and furniture or user target."); return; }
        if (hover.Active) fx_hover[hover.ViewerId] = hover;
        else if (fx_hover.TryGetValue(hover.ViewerId, out var current) && current.Domain == hover.Domain && current.EntityId == hover.EntityId)
            fx_hover.Remove(hover.ViewerId);
        record("fx.hover", hover.EntityId, hover.Active ? "entered" : "left", [hover.ViewerId]);
    }

    private WiredSimulationFxPresentation fx_snapshot() => new(
        fx_memory.Select(pair => new WiredSimulationFxState(pair.Key.ConfigId, pair.Key.Variable, pair.Key.Domain,
            pair.Key.EntityId, pair.Key.ViewerId, pair.Value.Value,
            pair.Value.Until is { } until ? Math.Max(0, until - now) : null, pair.Value.IsInitialize)).ToArray(),
        fx_hover.Values.ToArray());

    private static WiredSimulationFxPresentation? copy_fx(WiredSimulationFxPresentation? presentation)
    {
        if (presentation is null) return null;
        if (presentation.States is null || presentation.Hovers is null ||
            presentation.States.Any(entry => entry is null) || presentation.Hovers.Any(entry => entry is null))
            throw new ArgumentException("FX presentation state cannot contain null entries.", nameof(presentation));
        return new(presentation.States.ToArray(), presentation.Hovers.ToArray());
    }
}
