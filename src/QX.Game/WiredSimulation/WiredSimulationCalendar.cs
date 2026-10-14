using NodaTime;
using NodaTime.TimeZones;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private DateTimeOffset calendar_instant(WiredSimulationNode node)
    {
        if (initial.Epoch is null) unsupported(node, "Calendar conditions require an explicit snapshot epoch.");
        string timezone = node.Text("timezone");
        if (timezone.Length == 0) timezone = initial.CalendarTimezone ?? "";
        if (timezone.Length == 0) unsupported(node, "An empty calendar timezone requires the hotel's explicit CalendarTimezone.");
        return Instant.FromDateTimeOffset(initial.Epoch!.Value.AddMilliseconds(now)).InZone(calendar_zone(node, timezone)).ToDateTimeOffset();
    }

    private DateTimeZone calendar_zone(WiredSimulationNode node, string name)
    {
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(name);
        if (zone is not null) return zone;
        if (TzdbDateTimeZoneSource.Default.WindowsMapping.PrimaryMapping.TryGetValue(name, out var mapped))
            return DateTimeZoneProviders.Tzdb[mapped];
        unsupported(node, "The configured calendar timezone cannot be resolved in the bundled timezone database.");
        return DateTimeZone.Utc;
    }

    private bool calendar_range(WiredSimulationNode node, string unit, int current, int minimum, int maximum, bool cyclic = true)
    {
        if (!node.Flag("use_" + unit)) return true;
        long lower = node.Number(unit + "_minimum"), upper = node.Number(unit + "_maximum");
        if (lower < minimum || lower > maximum || upper < minimum || upper > maximum)
            unsupported(node, "The calendar range is outside the configured unit's bounds.");
        if (lower <= upper) return current >= lower && current <= upper;
        if (!cyclic) unsupported(node, "An inverted year range is not a saved server configuration; inspect its normalized readback.");
        return current >= lower || current <= upper;
    }

    private bool calendar_matches(WiredSimulationNode node)
    {
        var instant = calendar_instant(node);
        if (node.Name == "TimeMatches")
            return calendar_range(node, "second", instant.Second, 0, 59)
                && calendar_range(node, "minute", instant.Minute, 0, 59)
                && calendar_range(node, "hour", instant.Hour, 0, 23);
        long weekdays = node.Number("weekday_mask"), months = node.Number("month_mask");
        if ((weekdays & ~127L) != 0 || (months & ~4095L) != 0)
            unsupported(node, "Calendar masks contain undefined weekday or month bits.");
        int weekday = ((int)instant.DayOfWeek + 6) % 7;
        return (weekdays & (1L << weekday)) != 0 && (months & (1L << (instant.Month - 1))) != 0
            && calendar_range(node, "day", instant.Day, 1, 31)
            && calendar_range(node, "year", instant.Year, 0, 9999, false);
    }
}
