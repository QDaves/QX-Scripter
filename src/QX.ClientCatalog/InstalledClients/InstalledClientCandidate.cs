namespace Qx.ClientCatalog.InstalledClients;

public enum InstalledClientFamily
{
    Flash,
    Unity
}

public sealed record InstalledClientCandidate(
    InstalledClientFamily Family,
    string Version,
    string Path,
    string Source,
    DateTimeOffset LastModified,
    IReadOnlyList<string> Files)
{
    public string? ContentRevision { get; init; }
}

public sealed class InstalledClientCandidateChangedEventArgs : EventArgs
{
    public InstalledClientCandidateChangedEventArgs(
        InstalledClientFamily family,
        InstalledClientCandidate? previous,
        InstalledClientCandidate? candidate)
    {
        Family = family;
        Previous = previous;
        Candidate = candidate;
    }

    public InstalledClientFamily Family { get; }

    public InstalledClientCandidate? Previous { get; }

    public InstalledClientCandidate? Candidate { get; }
}
