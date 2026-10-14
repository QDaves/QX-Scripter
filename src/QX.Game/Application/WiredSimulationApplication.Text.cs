namespace Qx.Game.Application;

internal sealed partial class WiredSimulationApplication
{
    private static IReadOnlyDictionary<long, string>? captured_text(IEnumerable<(long Id, string Value)>? values)
    {
        if (values is null) return null;
        var entries = values.ToArray();
        if (entries.Any(entry => entry.Id is int.MinValue or int.MaxValue) ||
            entries.Select(entry => entry.Id).Distinct().Count() != entries.Length) return null;
        return entries.ToDictionary(entry => entry.Id, entry => entry.Value);
    }
}
