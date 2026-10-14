using System.Numerics;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private string variable_name(string id) => variables.TryGetValue(id, out var definition) ? definition.Name : id;

    private long[] fs(WiredSimulationNode node, Frame frame, int slot = 0)
    {
        var source = furniture_source(node, slot);
        IEnumerable<long> values = source switch
        {
            WiredFurnitureSource.Triggering => frame.Event.FurnitureId is { } id ? [id] : [],
            WiredFurnitureSource.Picked => node.Furniture ?? [],
            WiredFurnitureSource.SecondaryPicked => node.SecondaryFurniture ?? [],
            WiredFurnitureSource.Snapshot => WiredSimulationSnapshots.read(node)?.Select(item => item.Id) ?? [],
            WiredFurnitureSource.Selector => frame.SelectedFurniture,
            WiredFurnitureSource.Signal => frame.Event.Furniture ?? [],
            WiredFurnitureSource.Room => furniture.Keys,
            WiredFurnitureSource.MovedByStack => frame.Moved,
            _ => invalid_source(node, "furniture", (int)source)
        };
        return values.Where(furniture.ContainsKey).Distinct().ToArray();
    }

    private long[] us(WiredSimulationNode node, Frame frame, int slot = 0, bool include_departed = false)
    {
        var source = user_source(node, slot);
        IEnumerable<long> values = source switch
        {
            WiredUserSource.Triggering => frame.Event.UserId is { } id ? [id] : [],
            WiredUserSource.Reached or WiredUserSource.Clicked => frame.Event.Users ?? [],
            WiredUserSource.NamedBot => users.Values.Where(user => user.IsBot && user.Name.Equals(node.Text("bot_name", node.Text("optional_bot_name")), StringComparison.OrdinalIgnoreCase)).Select(user => user.Id),
            WiredUserSource.NamedUser => users.Values.Where(user => user.Name == node.Text("username")).Select(user => user.Id),
            WiredUserSource.Selector => frame.SelectedUsers,
            WiredUserSource.Signal => frame.Event.Users ?? [],
            WiredUserSource.Room => users.Keys,
            _ => invalid_source(node, "user", (int)source)
        };
        return values.Where(id => users.ContainsKey(id) || include_departed && frame.DepartedUser?.Id == id).Distinct().ToArray();
    }

    private long[] invalid_source(WiredSimulationNode node, string kind, int code)
    {
        unsupported(node, $"Unsupported {kind} source {code}.");
        return [];
    }

    private (WiredSourceDomain Domain, long Id)[] targets(WiredSimulationNode node, Frame frame, string field, int slot = 0, bool include_departed = false)
    {
        var domain = (WiredSourceDomain)node.Number(field);
        return domain switch
        {
            WiredSourceDomain.Furniture => fs(node, frame, slot).Select(id => (domain, id)).ToArray(),
            WiredSourceDomain.User => us(node, frame, slot, include_departed).Select(id => (domain, id)).ToArray(),
            WiredSourceDomain.Global or WiredSourceDomain.Context => [(domain, 0)],
            _ => invalid_targets(node, domain)
        };
    }

    private (WiredSourceDomain Domain, long Id)[] invalid_targets(WiredSimulationNode node, WiredSourceDomain domain)
    {
        unsupported(node, $"Unsupported variable target domain {(int)domain}.");
        return [];
    }

    private string resolve_reference(string variable, Frame? frame = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (variables.TryGetValue(variable, out var definition))
        {
            if (definition.Reference is null && definition.PointerAccessor is null)
            {
                if (definition.RequiresExecutionContext == true && definition.Derived is null)
                    pointer_failure("pointer.metadata", "An execution-context-dependent variable has no captured evaluator metadata.", frame);
                break;
            }
            if (!seen.Add(variable)) { fail("variable.reference_cycle", "Variable reference cycle: " + variable); throw new SimulationFailure(); }
            if (seen.Count > 64) pointer_failure("variable.reference_depth", "The local reference resolution depth exceeds 64.", frame);
            variable = definition.PointerAccessor is not null ? pointer_target(definition, frame) : definition.Reference!;
            if (!variables.TryGetValue(variable, out var target))
                pointer_failure("variable.reference_missing", "The referenced variable definition is unavailable: " + variable, frame);
            if (target!.Domain != definition.Domain)
                pointer_failure("variable.reference_domain", "The referenced variable domain differs from its alias: " + definition.Id, frame);
        }
        return variable;
    }

    private void unsupported_variable(string name)
    {
        note(WiredSimulationSeverity.Unsupported, "variable.unsupported", "No model for intrinsic variable " + name);
        status = WiredSimulationStatus.Faulted;
        throw new SimulationFailure();
    }

    private WiredSimulationValue? value(WiredSourceDomain domain, long id, string variable, Frame frame, bool require_exact = true)
    {
        reads++;
        variable = resolve_reference(variable, frame);
        if (external_variables.Contains(variable)) return external_value(variable, domain, id, require_exact);
        if (derivation(variable) is { } derived) return derived_value(domain, id, variable, derived, frame);
        string name = variable_name(variable);
        long? native = null;
        if (domain == WiredSourceDomain.Furniture && furniture.TryGetValue(id, out var item))
        {
            if (recolor_component(name) is >= 0 and var component)
            {
                if (item.RecolorRgb is null && item.ClassName.EndsWith("_recolorable", StringComparison.Ordinal))
                    unsupported_variable(name + " without captured furniture color");
                return item.RecolorRgb is { } rgb ? new(component == 0 ? rgb : (rgb >> ((3 - component) * 8)) & 255) : null;
            }
            if (name.StartsWith("@projectile.animation.", StringComparison.Ordinal))
            {
                if (!projectile_variable(name)) unsupported_variable(name);
                return projectile_value(item.Projectile, name) is { } projectile ? new(projectile) : null;
            }
            native = name switch
            {
                "@id" => id, "@class_id" => item.Kind,
                "@state" => item.IntrinsicState ?? (item.ClassName is "wf_antenna1" or "wf_antenna2" ? 0 : item.State),
                "@position" => item.X * 256L + item.Y, "@position.x" => item.X, "@position.y" => item.Y,
                "@altitude" => WiredSimulationGeometry.height_value(item.Z), "@height" => WiredSimulationGeometry.height_value(item.Height),
                "@occupation" => item.X * 65536L + item.Y * 256L + rotation_value(item),
                "@direction" => item.Direction, "@rotation" => rotation_value(item), "@type" => item.Temporary ? 2 : item.IntrinsicType, _ => null
            };
            if (!native.HasValue)
            {
                if (name.StartsWith('@')) unsupported_variable(name);
                return exact_value(item.Variables?.GetValueOrDefault(variable), name, require_exact);
            }
        }
        else if (domain == WiredSourceDomain.User && context_user(id, frame) is { } user)
        {
            if (name.StartsWith("@transaction.", StringComparison.Ordinal)) return trade_value(user, name);
            if (name is "@team.color" or "@team.type" or "@team.score") return user_team_value(user, name);
            if (name == "@dance")
            {
                if (user.Activity?.Dance is not { } dance) unsupported_variable("@dance without a known dance state");
                return user.Activity!.Dance is > 0 and var current_dance ? new(current_dance) : null;
            }
            if (name == "@sign")
                return active_sign(user, 0) is { } sign ? new(sign) : null;
            native = name switch
            {
                "@id" or "@user_id" => id, "@index" => user.Index, "@position" => user.X * 256L + user.Y, "@position.x" => user.X,
                "@position.y" => user.Y, "@altitude" => WiredSimulationGeometry.height_value(user.Z), "@direction" => user.Direction,
                _ => null
            };
            if (!native.HasValue)
            {
                if (name.StartsWith('@')) unsupported_variable(name);
                return exact_value(user.Variables?.GetValueOrDefault(variable) ??
                    stored_values.GetValueOrDefault(variable)?.GetValueOrDefault(id)?.Value, name, require_exact);
            }
        }
        else if (domain == WiredSourceDomain.Global)
        {
            if (team_score_variable(name) is { } team) return new(known_team_score(team));
            return name == "@wired_timer" ? new(room_timer_value()) : exact_value(globals.GetValueOrDefault(variable), name, require_exact);
        }
        else if (domain == WiredSourceDomain.Context && frame.Context.TryGetValue(variable, out long context)) native = context;
        return native.HasValue ? new(native.Value) : null;
    }

    private WiredSimulationUser? context_user(long id, Frame frame) => users.GetValueOrDefault(id) ??
        (frame.DepartedUser?.Id == id ? frame.DepartedUser : null);

    private WiredSimulationValue? exact_value(WiredSimulationValue? value, string name, bool require_exact)
    {
        if (require_exact && value is { IsExact: false })
        {
            note(WiredSimulationSeverity.Unsupported, "variable.precision", name + " has only a possibly clamped client display value; supply the verified 64-bit value.");
            status = WiredSimulationStatus.Faulted;
            throw new SimulationFailure();
        }
        return value;
    }

    private long? operand(WiredSimulationNode node, Frame frame, string kind, string number, string domain_field, string reference, int slot = 1, (WiredSourceDomain Domain, long Id)? holder = null)
    {
        if (node.Number(kind) == 0)
        {
            string high = number switch { "operand_value" => "operand_high", "reference_value" => "reference_high", "initial_value" => "initial_value_high", "duration" => "duration_high", _ => "" };
            return high.Length > 0 && node.Fields.ContainsKey(high)
                ? unchecked((node.Number(high) << 32) | (uint)node.Number(number)) : node.Number(number);
        }
        if (node.Flag("reference_same_holder") && holder is { } selected)
        {
            if ((WiredSourceDomain)node.Number(domain_field) != selected.Domain)
                unsupported(node, "A same-holder reference requires matching destination and reference domains.");
            return value(selected.Domain, selected.Id, node.Text(reference), frame)?.Value;
        }
        var references = targets(node, frame, domain_field, slot, include_departed: true);
        if (references.Length > 1)
        { unsupported(node, "Multiple operand holders have unresolved pairing semantics. Narrow the operand source to one holder."); }
        return references.Length == 0 ? null : value(references[0].Domain, references[0].Id, node.Text(reference), frame)?.Value;
    }

    private void write(WiredSourceDomain domain, long id, string variable, long number, Frame frame, bool create = false, bool remove = false,
        int origin = 0, bool replace = false, bool notify = true)
    {
        bool permitted(WiredSimulationVariable candidate) => create || remove ? candidate.CanCreateAndDelete ?? candidate.Writable : candidate.Writable;
        if (variables.TryGetValue(variable, out var alias) && !permitted(alias))
        { note(WiredSimulationSeverity.Warning, "variable.read_only", "Attempted to write " + variable, frame.Stack); return; }
        variable = resolve_reference(variable, frame);
        if (derivation(variable) is { } derived)
        {
            write_derived(domain, id, variable, derived, number, frame, create, remove, origin);
            return;
        }
        string name = variable_name(variable);
        if (variables.TryGetValue(variable, out var definition))
        {
            if (!permitted(definition)) { note(WiredSimulationSeverity.Warning, "variable.read_only", "Attempted to write " + variable, frame.Stack); return; }
            if (definition.Domain != domain) { fail("variable.domain", "Variable target does not match its definition: " + variable, frame.Stack); throw new SimulationFailure(); }
            if (!definition.HasValue) number = -1;
        }
        if (string.IsNullOrWhiteSpace(variable) || variable is "n" or "0")
        { fail("variable.missing", "No variable was selected.", frame.Stack); throw new SimulationFailure(); }
        if (variable.StartsWith('~'))
        {
            if (domain != WiredSourceDomain.Furniture || recolor_component(name) != 0 || create || remove)
                unsupported_variable(name);
            recolor(id, number, frame);
            return;
        }
        var prior = value(domain, id, variable, frame);
        if (!create && prior is null) { record("variable.absent", frame.Stack, "Change did not create the absent variable " + variable, [id]); return; }
        number = options.VariableBits == 32 ? unchecked((int)number) : number;
        if (name.StartsWith('@') && domain != WiredSourceDomain.Furniture) unsupported_variable(name);
        if (name.StartsWith('@') && domain == WiredSourceDomain.Furniture && furniture.TryGetValue(id, out var native))
        {
            if (remove) { note(WiredSimulationSeverity.Warning, "variable.native_remove", "Native variables cannot be removed.", frame.Stack); return; }
            switch (name)
            {
                case "@state":
                    if (native.ClassName is "wf_antenna1" or "wf_antenna2")
                    {
                        record("variable.ignored", frame.Stack, "Signal antenna @state does not change its visual state or emit a signal.", [id], prior?.Value, prior?.Value);
                        return;
                    }
                    if (native.IntrinsicState.HasValue) unsupported_variable(name);
                    set_state(native, checked((int)number), frame);
                    return;
                case "@position": move_item(native, checked((int)(number / 256)), checked((int)(number % 256)), null, frame, packed_movement: true); return;
                case "@occupation": occupation_variable(native, number, frame); return;
                case "@position.x": move_item(native, checked((int)number), native.Y, null, frame, packed_movement: true); return;
                case "@position.y": move_item(native, native.X, checked((int)number), null, frame, packed_movement: true); return;
                case "@altitude": move_item(native, native.X, native.Y, number / 100.0, frame, vertical_movement: true); return;
                case "@rotation": rotate_variable(native, number, frame); return;
                case "@direction": rotate_item(native, (int)(number & 7), frame); return;
                default: note(WiredSimulationSeverity.Warning, "variable.native_read_only", "Unsupported native write: " + name, frame.Stack); return;
            }
        }
        var updated = new WiredSimulationValue(number, replace ? now : prior?.CreatedAt ?? now, now,
            CreationTimeKnown: replace || prior?.CreationTimeKnown != false);
        var entries = domain switch
        {
            WiredSourceDomain.Furniture when furniture.TryGetValue(id, out var item) => item.Variables?.ToDictionary(StringComparer.Ordinal) ?? new(),
            WiredSourceDomain.User when users.TryGetValue(id, out var user) => user.Variables?.ToDictionary(StringComparer.Ordinal) ?? new(),
            WiredSourceDomain.Global => globals,
            WiredSourceDomain.Context => null,
            _ => null
        };
        if (domain == WiredSourceDomain.Context)
        { if (remove) frame.Context.Remove(variable); else frame.Context[variable] = number; }
        else if (entries is not null)
        {
            if (remove) entries.Remove(variable); else entries[variable] = updated;
            if (domain == WiredSourceDomain.Furniture) furniture[id] = furniture[id] with { Variables = entries };
            if (domain == WiredSourceDomain.User)
            {
                users[id] = users[id] with { Variables = entries };
                store_user_value(variable, id, updated, remove);
            }
        }
        else return;
        writes++; charge(frame, 1, frame.Stack);
        fx_changes.Add((domain, id, variable));
        record("variable", frame.Stack, variable, [id], prior?.Value, remove ? null : number);
        if (notify) enqueue(now, frame with { Event = new(WiredSimulationEventKind.VariableChanged, now,
            domain == WiredSourceDomain.User ? id : null, domain == WiredSourceDomain.Furniture ? id : null,
            Variable: variable, OldValue: prior?.Value, NewValue: remove ? null : number,
            Change: remove ? 4 : prior is null || replace ? 1 : 2, Origin: origin) }, external: true);
    }

    private long arithmetic(long left, long operation, long right, WiredSimulationNode node)
    {
        unchecked
        {
            return operation switch
            {
                0 => right, 1 => left + right, 2 => left - right, 3 => left * right,
                4 when right != 0 => left == long.MinValue && right == -1 ? long.MinValue : left / right,
                6 when right != 0 => left == long.MinValue && right == -1 ? 0 : left % right,
                5 => power(left, right),
                40 => Math.Max(left, right), 41 => Math.Min(left, right),
                50 when right > 0 => (long)(next_random() % (ulong)right),
                60 => left == long.MinValue ? long.MinValue : Math.Abs(left),
                100 => left & right, 101 => left | right, 102 => left ^ right, 103 => ~left,
                104 => left << ((int)right & (options.VariableBits - 1)),
                105 => options.VariableBits == 32 ? (int)left >> ((int)right & 31) : left >> ((int)right & 63),
                110 => BitOperations.PopCount(options.VariableBits == 32 ? (uint)left : (ulong)left),
                _ => invalid_arithmetic(operation, node)
            };
        }
    }
    private long invalid_arithmetic(long operation, WiredSimulationNode node)
    { unsupported(node, $"Undefined arithmetic operation or operand: {operation}."); return 0; }
    private static long power(long value, long exponent)
    {
        if (exponent == 0 || value == 1) return 1;
        if (value == -1) return (exponent & 1) == 0 ? 1 : -1;
        if (exponent < 0 || value == 0) return 0;
        if (exponent > 64) return value < 0 && (exponent & 1) != 0 ? long.MinValue : long.MaxValue;
        long result = 1;
        unchecked
        {
            while (exponent > 0)
            {
                if ((exponent & 1) != 0) result *= value;
                exponent >>= 1;
                if (exponent > 0) value *= value;
            }
        }
        return result;
    }
    private static bool compare(long left, long operation, long right) => operation switch
    { 0 => left < right, 1 => left == right, 2 => left > right, 3 => left <= right, 4 => left != right, 5 => left >= right, _ => false };
}
