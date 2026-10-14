using NodaTime;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private WiredSimulationValue? time_utility_value(WiredSimulationNode addon, WiredSimulationDerivedVariable derived, WiredSimulationValue source)
    {
        int mode = (int)addon.Number("time_source");
        if (derived.TimeSource != mode) return null;
        long milliseconds = source.Value;
        if (mode is 1 or 2)
        {
            if (initial.Epoch is null) unsupported(addon, "Creation and update time utilities require an explicit simulation epoch.");
            long timestamp = known_timestamp(source, mode == 1, addon);
            if (timestamp > now) unsupported(addon, "Variable timestamps must be relative to simulation time and cannot be in the future.");
            try { milliseconds = checked(initial.Epoch!.Value.ToUnixTimeMilliseconds() + timestamp); }
            catch (OverflowException)
            {
                unsupported(addon, "The variable timestamp exceeds the supported range.");
                return null;
            }
            if (derived.Index == 0) return new(milliseconds);
        }
        else if (mode != 0) unsupported(addon, "Unknown time utility source.");
        long divisor = derived.Index switch { 20 => 1, 21 => 1000, 22 => 60000, 23 => 3600000, _ => 0 };
        if (divisor != 0) return new(milliseconds / divisor);
        if (derived.Index is not (>= 1 and <= 10 or >= 24 and <= 26)) unsupported(addon, "Unknown time utility subvariable index.");
        var profile = initial.TimeUtilities;
        if (profile is null || string.IsNullOrEmpty(profile.Timezone))
            unsupported(addon, "Time utility calendar output requires an explicit hotel TimeUtilities profile.");
        if (!Enum.IsDefined(profile!.FirstDayOfWeek) || profile.MinimumDaysInFirstWeek is < 1 or > 7)
            unsupported(addon, "The time utility week profile is invalid.");
        var timezone = calendar_zone(addon, profile.Timezone);
        DateTime instant;
        try
        {
            var zoned = Instant.FromUnixTimeMilliseconds(milliseconds).InZone(timezone);
            if (zoned.Year is < 1 or > 9999) unsupported(addon, "The time utility timestamp is outside the supported calendar range.");
            instant = zoned.ToDateTimeUnspecified();
        }
        catch (ArgumentOutOfRangeException)
        {
            unsupported(addon, "The time utility timestamp is outside the supported calendar range.");
            return null;
        }
        if (derived.Index >= 24)
        {
            var origin = Instant.FromUnixTimeMilliseconds(0).InZone(timezone).ToDateTimeUnspecified();
            if (derived.Index != 26)
            {
                long days = (instant.Date - origin.Date).Days;
                if (days > 0 && instant.TimeOfDay < origin.TimeOfDay) days--;
                else if (days < 0 && instant.TimeOfDay > origin.TimeOfDay) days++;
                return new(derived.Index == 24 ? days : days / 7);
            }
            int months = (instant.Year - origin.Year) * 12 + instant.Month - origin.Month;
            var boundary = origin.AddMonths(months);
            if (months > 0 && instant < boundary) months--;
            else if (months < 0 && instant > boundary) months++;
            return new(months);
        }
        return new(derived.Index switch
        {
            1 => instant.Millisecond,
            2 => instant.Second,
            3 => instant.Minute,
            4 => instant.Hour,
            5 => ((int)instant.DayOfWeek + 6) % 7 + 1,
            6 => instant.Day,
            7 => instant.DayOfYear,
            8 => utility_week(addon, instant, profile),
            9 => instant.Month,
            10 => instant.Year,
            _ => throw new InvalidOperationException("Unknown calendar component.")
        });
    }

    private int utility_week(WiredSimulationNode addon, DateTime instant, WiredSimulationTimeUtilitiesProfile profile)
    {
        if (instant.Year <= 1) unsupported(addon, "Boundary-year week numbering requires separate conformance.");
        DateTime beginning(int year)
        {
            var january = new DateTime(year, 1, 1);
            int preceding = (7 + (int)january.DayOfWeek - (int)profile.FirstDayOfWeek) % 7;
            return january.AddDays(-preceding + (7 - preceding < profile.MinimumDaysInFirstWeek ? 7 : 0));
        }
        var start = beginning(instant.Year);
        if (instant.Date < start)
        {
            if (instant.Year <= 1) unsupported(addon, "Week numbering before year one is outside the supported calendar range.");
            start = beginning(instant.Year - 1);
        }
        return (int)(instant.Date - start).TotalDays / 7 + 1;
    }

    private void write_derived(Qx.Model.Wired.WiredSourceDomain domain, long id, string variable,
        WiredSimulationDerivedVariable derived, long number, Frame frame, bool create, bool remove, int origin)
    {
        if (!nodes.TryGetValue(derived.AddonId, out var addon)) { unsupported_variable(variable); return; }
        if (addon.Name == "VariableLevelUp" || create || remove)
        {
            note(WiredSimulationSeverity.Warning, "variable.read_only", "This derived variable cannot be created, removed or written: " + variable, frame.Stack);
            return;
        }
        if (addon.Name != "VariableTimeUtil") { unsupported(addon, "Writes through this derived variable require conformance."); return; }
        if (addon.Number("time_source") is 1 or 2)
        {
            note(WiredSimulationSeverity.Warning, "variable.read_only", "Creation and update time utility outputs are read-only: " + variable, frame.Stack);
            return;
        }
        if (derived.Index is < 0 or > 30) unsupported(addon, "Invalid derived-variable metadata.");
        if ((addon.Number("subvariable_mask") & (1L << derived.Index)) == 0) return;
        if (derivation(derived.Parent) is not null) unsupported(addon, "Chained derived-variable writes require separate conformance.");
        var source = value(domain, id, derived.Parent, frame);
        if (source is null) return;
        var current = time_utility_value(addon, derived, source);
        if (current is null) return;
        long changed;
        try
        {
            long factor = derived.Index switch { 20 => 1, 21 => 1000, 22 => 60000, 23 => 3600000, _ => 0 };
            if (factor != 0) changed = checked(source.Value + checked(number - current.Value) * factor);
            else
            {
                var zone = calendar_zone(addon, initial.TimeUtilities!.Timezone);
                var instant = Instant.FromUnixTimeMilliseconds(source.Value).InZone(zone);
                var local = instant.LocalDateTime;
                long delta = checked(number - current.Value);
                bool valid = derived.Index switch
                {
                    1 => number is >= 0 and <= 999,
                    2 or 3 => number is >= 0 and <= 59,
                    4 => number is >= 0 and <= 23,
                    5 => number is >= 1 and <= 7,
                    6 => number >= 1 && number <= CalendarSystem.Iso.GetDaysInMonth(local.Year, local.Month),
                    7 => number >= 1 && number <= (CalendarSystem.Iso.IsLeapYear(local.Year) ? 366 : 365),
                    8 => number is >= 1 and <= 53,
                    9 => number is >= 1 and <= 12,
                    10 => number is >= 1 and <= 9999,
                    24 or 25 or 26 => true,
                    _ => false
                };
                if (!valid) unsupported(addon, "This time-component assignment is outside its supported bounds.");
                var destination = derived.Index switch
                {
                    1 => local.PlusMilliseconds(delta),
                    2 => local.PlusSeconds(delta),
                    3 => local.PlusMinutes(delta),
                    4 => local.PlusHours(delta),
                    5 or 6 or 7 or 24 => local.PlusDays(checked((int)delta)),
                    8 or 25 => local.PlusDays(checked((int)(delta * 7))),
                    9 or 26 => local.PlusMonths(checked((int)delta)),
                    10 => local.PlusYears(checked((int)delta)),
                    _ => throw new InvalidOperationException("Unknown writable time component.")
                };
                var mapping = zone.MapLocal(destination);
                if (mapping.Count != 1) unsupported(addon, "Time assignments into timezone gaps or overlaps require separate conformance.");
                changed = mapping.Single().ToInstant().ToUnixTimeMilliseconds();
            }
        }
        catch (Exception error) when (error is OverflowException or ArgumentOutOfRangeException)
        {
            unsupported(addon, "The time-component assignment exceeds the supported timestamp range.");
            return;
        }
        write(domain, id, derived.Parent, changed, frame, origin: origin);
    }
}
