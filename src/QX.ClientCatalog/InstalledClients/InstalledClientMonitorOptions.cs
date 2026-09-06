namespace Qx.ClientCatalog.InstalledClients;

public sealed record InstalledClientMonitorOptions
{
    public string? LauncherDataPath { get; init; }

    public string? CacheRootPath { get; init; }

    public TimeSpan DebouncePeriod { get; init; } = TimeSpan.FromMilliseconds(350);

    public TimeSpan QuietPeriod { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan StabilityProbePeriod { get; init; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan ReconcilePeriod { get; init; } = TimeSpan.FromMinutes(3);

    internal void Validate()
    {
        RequireNonNegative(DebouncePeriod, nameof(DebouncePeriod));
        RequirePositive(QuietPeriod, nameof(QuietPeriod));
        RequirePositive(StabilityProbePeriod, nameof(StabilityProbePeriod));
        RequirePositive(ReconcilePeriod, nameof(ReconcilePeriod));

        if (LauncherDataPath is not null && string.IsNullOrWhiteSpace(LauncherDataPath))
            throw new ArgumentException("Launcher data path cannot be empty.", nameof(LauncherDataPath));
        if (CacheRootPath is not null && string.IsNullOrWhiteSpace(CacheRootPath))
            throw new ArgumentException("Cache root path cannot be empty.", nameof(CacheRootPath));
        if (LauncherDataPath is not null)
            _ = System.IO.Path.GetFullPath(LauncherDataPath);
        if (CacheRootPath is not null)
            _ = System.IO.Path.GetFullPath(CacheRootPath);
    }

    static void RequireNonNegative(TimeSpan value, string name)
    {
        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(name);
    }

    static void RequirePositive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(name);
    }
}
